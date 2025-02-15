﻿using DocumentFormat.OpenXml.Presentation;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Mailing;
using FSH.WebApi.Application.Common.SpeedSMS;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Profile;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace FSH.WebApi.Infrastructure.Identity;

internal partial class UserService
{
    /// <summary>
    /// This is used when authenticating with AzureAd.
    /// The local user is retrieved using the objectidentifier claim present in the ClaimsPrincipal.
    /// If no such claim is found, an InternalServerException is thrown.
    /// If no user is found with that ObjectId, a new one is created and populated with the values from the ClaimsPrincipal.
    /// If a role claim is present in the principal, and the user is not yet in that roll, then the user is added to that role.
    /// </summary>
    public async Task<string> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal)
    {
        string? objectId = principal.GetObjectId();
        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new InternalServerException(_t["Invalid objectId"]);
        }

        var user = await _userManager.Users.Where(u => u.ObjectId == objectId).FirstOrDefaultAsync()
            ?? await CreateOrUpdateFromPrincipalAsync(principal);

        if (principal.FindFirstValue(ClaimTypes.Role) is string role &&
            await _roleManager.RoleExistsAsync(role) &&
            !await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        return user.Id;
    }

    private async Task<ApplicationUser> CreateOrUpdateFromPrincipalAsync(ClaimsPrincipal principal)
    {
        string? email = principal.FindFirstValue(ClaimTypes.Upn);
        string? username = principal.GetDisplayName();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username))
        {
            throw new InternalServerException(string.Format(_t["Username or Email not valid."]));
        }

        var user = await _userManager.FindByNameAsync(username);
        if (user is not null && !string.IsNullOrWhiteSpace(user.ObjectId))
        {
            throw new InternalServerException(string.Format(_t["Username {0} is already taken."], username));
        }

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user is not null && !string.IsNullOrWhiteSpace(user.ObjectId))
            {
                throw new InternalServerException(string.Format(_t["Email {0} is already taken."], email));
            }
        }

        IdentityResult? result;
        if (user is not null)
        {
            user.ObjectId = principal.GetObjectId();
            result = await _userManager.UpdateAsync(user);

            await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));
        }
        else
        {
            user = new ApplicationUser
            {
                ObjectId = principal.GetObjectId(),
                FirstName = principal.FindFirstValue(ClaimTypes.GivenName),
                LastName = principal.FindFirstValue(ClaimTypes.Surname),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = username,
                NormalizedUserName = username.ToUpperInvariant(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true
            };
            result = await _userManager.CreateAsync(user);

            await _events.PublishAsync(new ApplicationUserCreatedEvent(user.Id));
        }

        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Validation Errors Occurred."], result.GetErrors(_t));
        }

        return user;
    }
    //checked
    public async Task<string> CreateAsync(CreateUserRequest request, bool isMobile, string local, string origin, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if(request.Role == FSHRoles.Dentist)
            {
                if(!CheckValidExpYear(request.BirthDay.Value, request.DoctorProfile.YearOfExp).Result)
                    throw new Exception("The Exp year is not available with birthday");
            }
            var role = await _roleManager.FindByNameAsync(request.Role) ?? throw new InternalServerException(_t["Role is unavailable."]);
            var user = new ApplicationUser
            {
                Email = request.Email,
                Gender = request.IsMale,
                BirthDate = request.BirthDay,
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserName = request.UserName,
                PhoneNumber = request.PhoneNumber,
                Address = request.Address != null ? request.Address : null,
                Job = request.Job,
                IsActive = true,
            };
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                throw new InternalServerException(_t["Validation Errors Occurred."], result.GetErrors(_t));
            }

            await _userManager.AddToRoleAsync(user, role.Name);
            if (request.Role.Equals(FSHRoles.Dentist))
            {
                request.DoctorProfile.DoctorID = user.Id;
                await UpdateDoctorProfile(request.DoctorProfile, cancellationToken);
            }
            else if (request.Role.Equals(FSHRoles.Patient))
            {
                var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
                string code = "BN";
                if (amountP.Count() < 10)
                {
                    code += $"00{amountP.Count()}";
                }
                else if (amountP.Count() < 100)
                {
                    code += $"0{amountP.Count()}";
                }
                else
                {
                    code += $"{amountP.Count()}";
                }
                await _db.PatientProfiles.AddAsync(new PatientProfile
                {
                    UserId = user.Id,
                    PatientCode = code
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            var messages = new List<string> { string.Format(_t["User {0} Registered."], user.UserName) };

            if (_securitySettings.RequireConfirmedAccount && !string.IsNullOrEmpty(user.Email) && !isMobile)
            {
                // send verification email
                string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
                RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
                {
                    Email = user.Email,
                    UserName = user.UserName,
                    Url = emailVerificationUri,
                    Password = request.Password,
                };
                if (local.Equals("en"))
                {
                    var mailRequest = new MailRequest(
                    new List<string> { user.Email },
                    _t["Confirm Registration"],
                    _templateService.GenerateEmailTemplate("email-confirmation-en", eMailModel));
                    _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
                }
                else
                {
                    var mailRequest = new MailRequest(
                    new List<string> { user.Email },
                    _t["Confirm Registration"],
                    _templateService.GenerateEmailTemplate("email-confirmation-vie", eMailModel));
                    _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
                }

                messages.Add(_t[$"Please check {user.Email} to verify your account!"]);
            }
             else if(_securitySettings.RequireConfirmedAccount && !string.IsNullOrEmpty(user.PhoneNumber) && isMobile)
            {
                string code = await _userManager.GenerateChangePhoneNumberTokenAsync(user, request.PhoneNumber);
                _speedSMSService.sendSMS(new string[] { request.PhoneNumber }, $"Your verification code is: {code}", SpeedSMSType.TYPE_CSKH);
            }

            await _events.PublishAsync(new ApplicationUserCreatedEvent(user.Id));
            await transaction.CommitAsync(cancellationToken);
            return string.Join(Environment.NewLine, messages);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }
    public async Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId!) ?? throw new NotFoundException(_t["User Not Found."]);
            var role = await GetRolesAsync(user.Id, cancellationToken);

            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.Gender = request.Gender ?? user.Gender;
            user.BirthDate = request.BirthDate ?? user.BirthDate;
            user.Address = request.Address ?? user.Address;
            user.Job = request.Job ?? user.Job;

            var result = await _userManager.UpdateAsync(user);

            await _signInManager.RefreshSignInAsync(user);

            await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));

            if (!result.Succeeded)
            {
                throw new InternalServerException(_t["Update profile failed"], result.GetErrors(_t));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> UpdateEmailAsync(UpdateEmailRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);

        _ = user ?? throw new NotFoundException(_t["User Not Found."]);

        var result = await _userManager.SetEmailAsync(user, request.Email);
        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Update email failed"], result.GetErrors(_t));
        }

        if (_securitySettings.RequireConfirmedAccount)
        {
            string emailVerificationUri = await GetEmailVerificationUriAsync(user, request.Origin);
            RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
            {
                Email = user.Email!,
                UserName = user.UserName!,
                Url = emailVerificationUri
            };
            var mailRequest = new MailRequest(
                               new List<string> { user.Email },
                               _t["Confirm Registration"],
                               _templateService.GenerateEmailTemplate("email-confirmation", eMailModel));
            _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
            return emailVerificationUri;
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));
        return _t["Email updated successfully."];
    }

    public async Task UpdatePhoneNumberAsync(UpdatePhoneNumberRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId!);

        _ = user ?? throw new NotFoundException(_t["User Not Found."]);

        var result = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);

        string code = await _userManager.GenerateChangePhoneNumberTokenAsync(user, request.PhoneNumber);
        _speedSMSService.sendSMS(new string[] { request.PhoneNumber }, $"Your verification code is: {code}", SpeedSMSType.TYPE_CSKH);

        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Update phone number failed"], result.GetErrors(_t));
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));
    }

    public async Task UpdateAvatarAsync(UpdateAvatarRequest request, CancellationToken cancellationToken)
    {
        if (_currentUserService.GetUserId().ToString() != request.UserId)
        {
            throw new BadRequestException("Only user update for personal.");
        }
        var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException(_t["User Not Found."]);

        string currentImage = user.ImageUrl ?? string.Empty;

        if (request.Image != null)
        {
            if (!IsDefaultImage(currentImage))
            {
                RemoveCurrentAvatar(currentImage);
            }

            user.ImageUrl = await _fileStorage.SaveFileAsync(request.Image, cancellationToken);
            if (string.IsNullOrEmpty(user.ImageUrl))
            {
                throw new InternalServerException(_t["Image upload failed"]);
            }
        }
        else if (request.DeleteCurrentImage)
        {
            if (!IsDefaultImage(currentImage))
            {
                RemoveCurrentAvatar(currentImage);
            }

            user.ImageUrl = null;
        }

        var result = await _userManager.UpdateAsync(user);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));

        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Update profile failed"], result.GetErrors(_t));
        }
    }

    private void RemoveCurrentAvatar(string currentImage)
    {
        if (string.IsNullOrEmpty(currentImage)) return;
        string root = Directory.GetCurrentDirectory();
        _fileStorage.Remove(Path.Combine(root, currentImage));
    }

    private bool IsDefaultImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return false;

        string[] defaultImages = new[]
        {
        "Files/Image/png/male.png",
        "Files/Image/png/female.png"
        };

        return defaultImages.Contains(imagePath);
    }
}
