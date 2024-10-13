using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Events;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.FileStorage;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Mailing;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Persistence;
using FSH.WebApi.Application.Common.ReCaptchaV3;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Common.SpeedSMS;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Auth;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FSH.WebApi.Infrastructure.Identity;

internal partial class UserService : IUserService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly IJobService _jobService;
    private readonly IMailService _mailService;
    private readonly SecuritySettings _securitySettings;
    private readonly IEmailTemplateService _templateService;
    private readonly IFileStorageService _fileStorage;
    private readonly IEventPublisher _events;
    private readonly ICacheService _cache;
    private readonly ICacheKeyService _cacheKeys;
    private readonly ITenantInfo _currentTenant;
    private readonly IReCAPTCHAv3Service _reCAPTCHAv3Service;
    private readonly ISpeedSMSService _speedSMSService;
    private readonly ICurrentUser _currentUserService;
    private readonly IMedicalHistoryService _medicalHistoryService;
    public UserService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        IStringLocalizer<UserService> localizer,
        IJobService jobService,
        IMailService mailService,
        IEmailTemplateService templateService,
        IFileStorageService fileStorage,
        IEventPublisher events,
        ICacheService cache,
        ICacheKeyService cacheKeys,
        ITenantInfo currentTenant,
        IReCAPTCHAv3Service reCAPTCHAv3Service,
        IOptions<SecuritySettings> securitySettings,
        ISpeedSMSService speedSMSService,
        ICurrentUser currentUser,
        IMedicalHistoryService medicalHistoryService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _t = localizer;
        _jobService = jobService;
        _mailService = mailService;
        _templateService = templateService;
        _fileStorage = fileStorage;
        _events = events;
        _cache = cache;
        _cacheKeys = cacheKeys;
        _currentTenant = currentTenant;
        _reCAPTCHAv3Service = reCAPTCHAv3Service;
        _securitySettings = securitySettings.Value;
        _speedSMSService = speedSMSService;
        _currentUserService = currentUser;
        _medicalHistoryService = medicalHistoryService;
    }

    public async Task<PaginationResponse<ListUserDTO>> SearchAsync(UserListFilter filter, CancellationToken cancellationToken)
    {
        var list_user = new List<ListUserDTO>();
        var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(filter);

        var users = await _userManager.Users
            .AsNoTracking()
            .WithSpecification(spec)
            .ProjectToType<UserDetailsDto>()
            .ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            list_user.Add(new ListUserDTO
            {
                Id = user.Id.ToString(),
                UserName = user.UserName,
                Address = user.Address,
                Email = user.Email,
                Gender = user.Gender,
                ImageUrl = user.ImageUrl,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Role = await GetRolesAsync(user.Id.ToString(), cancellationToken),
            });
        }
        int count = await _userManager.Users
            .CountAsync(cancellationToken);

        return new PaginationResponse<ListUserDTO>(list_user, count, filter.PageNumber, filter.PageSize);
    }

    public async Task<bool> ExistsWithUserIDAsync(string userID)
    {
        EnsureValidTenant();
        var user = await _userManager.FindByIdAsync(userID);
        return user is not null;
    }

    public async Task<bool> ExistsWithNameAsync(string name)
    {
        EnsureValidTenant();
        return await _userManager.FindByNameAsync(name) is not null;
    }

    public async Task<bool> CheckConfirmEmail(string userID)
    {
        EnsureValidTenant();
        var user = await _userManager.FindByIdAsync(userID);
        if (user == null)
        {
            return false;
        }
        return await _userManager.IsEmailConfirmedAsync(user);
    }

    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null)
    {
        EnsureValidTenant();
        return await _userManager.FindByEmailAsync(email.Normalize()) is ApplicationUser user && user.Id != exceptId;
    }

    public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null)
    {
        EnsureValidTenant();
        return await _userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber) is ApplicationUser user && user.Id != exceptId;
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant?.Id))
        {
            throw new UnauthorizedException(_t["Invalid Tenant."]);
        }
    }

    public async Task<List<ListUserDTO>> GetListAsync(CancellationToken cancellationToken)
    {
        var list_user = new List<ListUserDTO>();
        var list = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        foreach(var user in list)
        {
            list_user.Add(new ListUserDTO
            {
                Id = user.Id,
                UserName = user.UserName,
                Address = user.Address,
                Email = user.Email,
                Gender = user.Gender,
                ImageUrl = user.ImageUrl,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Role = await GetRolesAsync(user.Id, cancellationToken),
            });
        }
        return list_user;
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        _userManager.Users.AsNoTracking().CountAsync(cancellationToken);

    public async Task<UserDetailsDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(_t["User Not Found."]);
        return user.Adapt<UserDetailsDto>();
    }

    public async Task ToggleStatusAsync(ToggleUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.Where(u => u.Id == request.UserId).FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(_t["User Not Found."]);


        bool isAdmin = await _userManager.IsInRoleAsync(user, FSHRoles.Admin);
        if (isAdmin)
        {
            throw new ConflictException(_t["Administrators Profile's Status cannot be toggled"]);
        }

        user.IsActive = request.ActivateUser;

        await _userManager.UpdateAsync(user);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));
    }

    public async Task<UserDetailsDto> GetUserDetailByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
           .AsNoTracking()
           .Where(u => u.Email.Trim().ToLower().Equals(email.Trim().ToLower()))
           .FirstOrDefaultAsync(cancellationToken);
        return user is null ? new UserDetailsDto() : user.Adapt<UserDetailsDto>();
    }

    public async Task<UserDetailsDto> GetUserDetailByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
           .AsNoTracking()
           .Where(u => u.PhoneNumber.Trim().ToLower().Equals(phoneNumber.Trim().ToLower()) && u.IsActive)
           .FirstOrDefaultAsync(cancellationToken);
        return user is null ? new UserDetailsDto() : user.Adapt<UserDetailsDto>();
    }

    public async Task<bool> CheckBirthDayValid(DateOnly? date, string? role)
    {
        bool birthDayValid = false;

        if (role.Equals(FSHRoles.Patient) || role.Equals(FSHRoles.Staff) || role.Equals(FSHRoles.Admin))
        {
            birthDayValid = date.Value < DateOnly.FromDateTime(DateTime.Today).AddYears(-18);
        }else if (role.Equals(FSHRoles.Dentist))
        {
            birthDayValid = date.Value < DateOnly.FromDateTime(DateTime.Today).AddYears(-25);
        }
        return birthDayValid;
    }
    public async Task GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = _userManager.Users
           .AsNoTracking()
           .Where(u => u.Id.Trim().Equals(userId))
           .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException(_t["User Not Found."]);
    }

    public async Task<string> GetFullName(DefaultIdType userId)
    {
        var user = await GetAsync(userId.ToString(), CancellationToken.None);
        return string.Join(" ", user.FirstName, user.LastName);
    }

    public Task GetUserByIdAsync(DefaultIdType? userId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
    public async Task UpdateDoctorProfile(UpdateDoctorProfile request, CancellationToken cancellationToken)
    {
        var profile = _db.DoctorProfiles.Where(p => p.DoctorId == request.DoctorID).FirstOrDefault();
        if (profile != null)
        {
            profile.LastModifiedBy = _currentUserService.GetUserId();
            profile.Certification = request.Certification;
            profile.College = request.College;
            profile.Education = request.Education;
            profile.SeftDescription = request.SeftDescription;
            profile.YearOfExp = request.YearOfExp;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _db.DoctorProfiles.Add(new DoctorProfile
            {
                DoctorId = request.DoctorID,
                CreatedBy = _currentUserService.GetUserId(),
                Certification = request.Certification,
                College = request.College,
                Education = request.Education,
                SeftDescription = request.SeftDescription,
                YearOfExp = request.YearOfExp,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserProfileResponse> GetUserProfileAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == null)
        {
            throw new NotFoundException("Can not found user ID.");
        }
        var user = await _userManager.FindByIdAsync(userId.ToString()) ?? throw new BadRequestException("User is not found.");
        var user_role = GetRolesAsync(user.Id, cancellationToken).Result;
        var profile = new UserProfileResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Gender = user.Gender,
            BirthDate = user.BirthDate,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Job = user.Job,
            ImageUrl = user.ImageUrl,
            Address = user.Address,
        };
        if (user_role.RoleName.Equals(FSHRoles.Dentist))
        {
            profile.DoctorProfile = await _db.DoctorProfiles.Where(p => p.DoctorId == user.Id).FirstOrDefaultAsync();
        }
        else if(user_role.RoleName.Equals(FSHRoles.Patient)) {
            profile.PatientFamily = await _db.PatientFamilys.Where(p => p.PatientId == user.Id).FirstOrDefaultAsync();
            profile.MedicalHistory = await _db.MedicalHistorys.Where(p => p.PatientId == user.Id).FirstOrDefaultAsync();
        }
        return profile;
    }
}