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

    public async Task<string> CreateAsync(CreateUserRequest request, string origin, CancellationToken cancellationToken)
    {
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
            IsActive = true
        };
        if (request.Role.Equals(FSHRoles.Patient))
        {
            user.Job = request.Job;
            user.Address = request.Address;
        }

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Validation Errors Occurred."], result.GetErrors(_t));
        }

        await _userManager.AddToRoleAsync(user, role.Name);
        request.DoctorProfile.DoctorID = user.Id;
        if (request.Role.Equals(FSHRoles.Dentist))
        {
            await UpdateDoctorProfile(request.DoctorProfile, cancellationToken);
        }

        var messages = new List<string> { string.Format(_t["User {0} Registered."], user.UserName) };

        if (_securitySettings.RequireConfirmedAccount && !string.IsNullOrEmpty(user.Email))
        {
            // send verification email
            string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
            RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
            {
                Email = user.Email,
                UserName = user.UserName,
                Url = emailVerificationUri
            };
            var mailRequest = new MailRequest(
                new List<string> { user.Email },
                _t["Confirm Registration"],
                _templateService.GenerateEmailTemplate("email-confirmation", eMailModel));
            _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
            messages.Add(_t[$"Please check {user.Email} to verify your account!"]);
        }

        await _events.PublishAsync(new ApplicationUserCreatedEvent(user.Id));

        return string.Join(Environment.NewLine, messages);
    }
    public async Task<string> CreateOrUpdatePatientFamily(UpdatePatientFamilyRequest request, CancellationToken cancellationToken)
    {
        var n = await _db.PatientFamilys.Where(p => p.PatientId == request.PatientId).FirstOrDefaultAsync(cancellationToken);
        if (n != null)
        {
            n.Name = request.Name ?? n.Name;
            n.Email = request.Email ?? n.Email;
            n.Phone = request.Phone ?? n.Phone;
            n.Relationship = request.Relationship;
            n.LastModifiedBy = _currentUserService.GetUserId();
            n.LastModifiedOn = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else {
            _db.PatientFamilys.Add(new PatientFamily {
                PatientId = request.PatientId,
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                Relationship = request.Relationship,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        return _t["Success"];
    }

    //public async Task<string> UpdatePatientRecordAsync(CreatePatientRecord request)
    //{
    //    var user = _userManager.FindByIdAsync(request.PatientId).Result;

    //    user.PhoneNumber = request.PhoneNumber;
    //    user.LastName = request.LastName;
    //    user.FirstName = request.FirstName;
    //    user.Address = request.Address;
    //    user.BirthDate = request.BirthDay;
    //    user.Gender = request.IsMale;
    //    user.Job = request.Job;

    //    var result = await _userManager.UpdateAsync(user);

    //    if (!result.Succeeded)
    //    {
    //        throw new InternalServerException(_t["Validation Errors Occurred."], result.GetErrors(_t));
    //    }


    //    await _events.PublishAsync(new ApplicationUserCreatedEvent(user.Id));

    //    return _t["Update Record Successfully"];
    //}

    public async Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId!) ?? throw new NotFoundException(_t["User Not Found."]);
        var role = await GetRolesAsync(user.Id, cancellationToken);

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.Gender = request.Gender ?? user.Gender;
        user.BirthDate = request.BirthDate ?? user.BirthDate;

        if (role.RoleName == FSHRoles.Patient)
        {
            user.Job = request.Job ?? user.Job;
            user.Address = request.Address ?? user.Address;
            if (request.PatientFamily != null) {
                await CreateOrUpdatePatientFamily(request.PatientFamily, cancellationToken);
            }
            if (request.MedicalHistory != null)
            {
                await _medicalHistoryService.CreateAndUpdateMedicalHistory(request.MedicalHistory, cancellationToken);
            }
        }
        else if(role.RoleName == FSHRoles.Dentist)
        {
            await UpdateDoctorProfile(request.DoctorProfile, cancellationToken);
        }

        var result = await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));

        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Update profile failed"], result.GetErrors(_t));
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
        if(_currentUserService.GetUserId().ToString() != request.UserId)
        {
            throw new BadRequestException("Only user update for personal.");
        }
        var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException(_t["User Not Found."]);

        string currentImage = user.ImageUrl ?? string.Empty;

        if (request.Image != null)
        {
            RemoveCurrentAvatar(currentImage);
            user.ImageUrl = await _fileStorage.SaveFileAsync(request.Image, cancellationToken);
            if (string.IsNullOrEmpty(user.ImageUrl))
            {
                throw new InternalServerException(_t["Image upload failed"]);
            }
        }
        else if (request.DeleteCurrentImage)
        {
            RemoveCurrentAvatar(currentImage);
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
}
