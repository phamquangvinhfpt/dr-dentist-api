using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Office2010.Excel;
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
using FSH.WebApi.Application.Common.ReCaptchaV3;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Common.SpeedSMS;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Profile;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Auditing;
using FSH.WebApi.Infrastructure.Auth;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Infrastructure.Treatments;
using FSH.WebApi.Shared.Authorization;
using Hangfire.Common;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Numerics;
using System.Threading;
using static QRCoder.PayloadGenerator;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    private readonly IAppointmentCalendarService _workingCalendarService;
    private readonly ILogger<UserService> _logger;
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
        IMedicalHistoryService medicalHistoryService,
        IAppointmentCalendarService workingCalendarService,
        ILogger<UserService> logger)
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
        _workingCalendarService = workingCalendarService;
        _logger = logger;
    }
    //checked
    public async Task<PaginationResponse<ListUserDTO>> SearchAsync(UserListFilter filter, CancellationToken cancellationToken)
    {
        var list_user = new List<ListUserDTO>();
        int count = 0;
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(filter);

            var users = await _userManager.Users
                .AsNoTracking()
                .Where(p => p.IsActive == filter.IsActive)
                .WithSpecification(spec)
                .ToListAsync(cancellationToken);
            foreach (var user in users)
            {
                var role = await GetRolesAsync(user.Id, cancellationToken);
                if (role.RoleName != FSHRoles.Admin)
                {
                    list_user.Add(new ListUserDTO
                    {
                        Id = user.Id.ToString(),
                        Name = $"{user.FirstName} {user.LastName}",
                        UserName = user.UserName,
                        Address = user.Address,
                        Email = user.Email,
                        Gender = user.Gender,
                        ImageUrl = user.ImageUrl,
                        PhoneNumber = user.PhoneNumber,
                        IsActive = user.IsActive,
                        Role = role,
                        isBanned = await _userManager.IsLockedOutAsync(user)
                    });
                }
            }
            var spec2 = new EntitiesByBaseFilterSpec<ApplicationUser>(filter);
            count = await _userManager.Users.WithSpecification(spec2)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message, ex);
        }

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
        foreach (var user in list)
        {
            list_user.Add(new ListUserDTO
            {
                Id = user.Id,
                Name = $"{user.FirstName} {user.LastName}",
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
    //checked
    public async Task<UserDetailsDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(_t["User Not Found."]);
        return user.Adapt<UserDetailsDto>();
    }

    public async Task ToggleStatusAsync(ToggleStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.Users.Where(u => u.Id == request.Id).FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(_t["User Not Found."]);


            bool isAdmin = await _userManager.IsInRoleAsync(user, FSHRoles.Admin);
            if (isAdmin)
            {
                throw new ConflictException(_t["Administrators Profile's Status cannot be toggled"]);
            }

            user.IsActive = request.Activate;

            await _userManager.UpdateAsync(user);

            await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
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
        }
        else if (role.Equals(FSHRoles.Dentist))
        {
            birthDayValid = date.Value < DateOnly.FromDateTime(DateTime.Today).AddYears(-25);
        }
        return birthDayValid;
    }
    public async Task GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
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
    public async Task UpdateDoctorProfile(UpdateDoctorProfile request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.DoctorID == null)
            {
                throw new BadRequestException("Doctor Information should be include");
            }
            var user = await _userManager.FindByIdAsync(request.DoctorID);
            if(user == null)
            {
                throw new NotFoundException("Warning: Error when find user");
            }
            var profile = _db.DoctorProfiles.Where(p => p.DoctorId == request.DoctorID).FirstOrDefault();
            if (profile != null)
            {
                profile.LastModifiedBy = _currentUserService.GetUserId();
                profile.Certification = request.Certification ?? profile.Certification;
                profile.College = request.College ?? profile.College;
                profile.Education = request.Education ?? profile.Education;
                profile.SeftDescription = request.SeftDescription ?? profile.SeftDescription;
                profile.YearOfExp = request.YearOfExp.ToString() ?? profile.YearOfExp;
                profile.WorkingType = request.WorkingType;
                profile.TypeServiceID = request.TypeServiceID;
                if (request.CertificationImage != null)
                {
                    if(profile.CertificationImage != null)
                    {
                        _fileStorage.RemoveAll(profile.CertificationImage);
                    }
                    profile.CertificationImage = await _fileStorage.SaveFilesAsync(request.CertificationImage, cancellationToken);
                }
                await _db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var d = new DoctorProfile
                {
                    DoctorId = request.DoctorID,
                    CreatedBy = _currentUserService.GetUserId(),
                    Certification = request.Certification,
                    TypeServiceID = request.TypeServiceID,
                    College = request.College,
                    Education = request.Education,
                    SeftDescription = request.SeftDescription,
                    YearOfExp = request.YearOfExp.ToString(),
                    WorkingType = request.WorkingType,
                    IsActive = false,
                };
                if (request.CertificationImage != null)
                {
                    d.CertificationImage = await _fileStorage.SaveFilesAsync(request.CertificationImage, cancellationToken);
                }
                _db.DoctorProfiles.Add(d);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
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
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            Job = user.Job,
            ImageUrl = user.ImageUrl,
            Address = user.Address,
        };
        if (user_role.RoleName.Equals(FSHRoles.Dentist))
        {
            profile.DoctorProfile = await _db.DoctorProfiles.Where(p => p.DoctorId == user.Id).FirstOrDefaultAsync();
        }
        else if (user_role.RoleName.Equals(FSHRoles.Patient))
        {
             profile.PatientProfile = await _db.PatientProfiles.Where(p => p.UserId == user.Id).FirstOrDefaultAsync(cancellationToken);
             profile.PatientFamily = await _db.PatientFamilys.Where(p => p.PatientProfileId == profile.PatientProfile.Id).FirstOrDefaultAsync(cancellationToken);
             profile.MedicalHistory = await _db.MedicalHistorys.Where(p => p.PatientProfileId == profile.PatientProfile.Id).FirstOrDefaultAsync(cancellationToken);
        }
        return profile;
    }

    public async Task<PaginationResponse<GetDoctorResponse>> GetAllDoctor(UserListFilter request, DateOnly date)
    {
        var doctorResponses = new List<GetDoctorResponse>();
        int totalRecords = 0;
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(request);
            var role = _db.Roles
                .Where(p => p.Name == FSHRoles.Dentist)
                .FirstOrDefault();

            if (role == null)
            {
                return new PaginationResponse<GetDoctorResponse>(new List<GetDoctorResponse>(), 0, request.PageNumber, request.PageSize);
            }

            var query = _db.UserRoles
                .AsNoTracking()
                .Where(p => p.RoleId == role.Id)
                .Join(
                    _db.Users,
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => u
                );
            var spec2 = new EntitiesByBaseFilterSpec<ApplicationUser>(request);
            totalRecords = query.WithSpecification(spec2).Count();

            var users = query
                .Where(p => p.IsActive == request.IsActive)
                .WithSpecification(spec)
                .ToList();

            foreach (var user in users)
            {
                var doctor = _db.DoctorProfiles.FirstOrDefault(p => p.DoctorId == user.Id);
                double rating = await GetDoctorRating(doctor.Id);
                bool check = false;
                if(date != default)
                {
                    var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == doctor.Id && p.Date == date && p.Status == WorkingStatus.Accept);
                    if (workingTime != null)
                    {
                        var current_time = DateTime.Now.TimeOfDay;
                        check = await _db.TimeWorkings.Where(p =>
                        p.CalendarID == workingTime.Id &&
                        p.StartTime <= current_time &&
                        p.EndTime >= current_time &&
                        p.IsActive
                        ).AnyAsync();
                    }
                }
                doctorResponses.Add(new GetDoctorResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    Gender = user.Gender,
                    ImageUrl = user.ImageUrl,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    UserName = user.UserName,
                    DoctorProfile = doctor,
                    Rating = Math.Round(rating, 0),
                    isWorked = check
                });
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        return new PaginationResponse<GetDoctorResponse>(doctorResponses, totalRecords, request.PageNumber, request.PageSize);
    }

    private async Task<double> GetDoctorRating(Guid doctorProfileID)
    {
        var rating = await _db.Feedbacks
        .Where(f => f.DoctorProfileId == doctorProfileID)
        .GroupBy(f => f.DoctorProfileId)
        .Select(group => new
        {
            AverageRating = group.Average(f => f.Rating),
            TotalFeedbacks = group.Count()
        })
        .FirstOrDefaultAsync();

        return rating?.AverageRating ?? 0;
    }

    public async Task UpdateOrCreatePatientProfile(UpdateOrCreatePatientProfile request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (request.IsUpdateProfile)
            {
                var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == request.PatientProfileId && p.UserId == request.Profile.UserId) ?? throw new BadRequestException("Profile is not found.");
                profile.IDCardNumber = request.Profile.IDCardNumber ?? profile.IDCardNumber;
                profile.Occupation = request.Profile.Occupation ?? profile.Occupation;
                profile.LastModifiedBy = _currentUserService.GetUserId();
                profile.LastModifiedOn = DateTime.Now;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (request.IsUpdateMedicalHistory)
            {
                var history = await _db.MedicalHistorys.FirstOrDefaultAsync(p => p.PatientProfileId == request.PatientProfileId);
                if (history != null)
                {
                    history.MedicalName = request.MedicalHistory.MedicalName ?? history.MedicalName;
                    history.Note = request.MedicalHistory.Note ?? history.Note;
                    history.LastModifiedBy = _currentUserService.GetUserId();
                    history.LastModifiedOn = DateTime.Now;
                }
                else
                {
                    var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == request.PatientProfileId) ?? throw new BadRequestException("Profile is not found.");
                    await _db.MedicalHistorys.AddAsync(new MedicalHistory
                    {
                        PatientProfileId = request.PatientProfileId,
                        MedicalName = request.MedicalHistory.MedicalName,
                        Note = request.MedicalHistory.Note,
                        CreatedBy = _currentUserService.GetUserId(),
                        CreatedOn = DateTime.Now,
                    });
                }
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (request.IsUpdatePatientFamily)
            {
                var family = await _db.PatientFamilys.FirstOrDefaultAsync(p => p.PatientProfileId == request.PatientProfileId);
                var phone_existing = await _db.PatientFamilys.Where(p => p.Phone == request.PatientFamily.Phone).FirstOrDefaultAsync();
                if (phone_existing != null)
                {
                    throw new BadRequestException("Phone number is existing");
                }
                if (family != null)
                {
                    family.Phone = request.PatientFamily.Phone ?? family.Phone;
                    family.Relationship = request.PatientFamily.Relationship;
                    family.Name = request.PatientFamily.Name ?? family.Name;
                    family.Email = request.PatientFamily.Email ?? family.Email;
                    family.LastModifiedBy = _currentUserService.GetUserId();
                    family.LastModifiedOn = DateTime.Now;
                }
                else
                {
                    var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == request.PatientProfileId) ?? throw new BadRequestException("Profile is not found.");
                    await _db.PatientFamilys.AddAsync(new PatientFamily
                    {
                        PatientProfileId = request.PatientProfileId,
                        Name = request.PatientFamily.Name,
                        Relationship = request.PatientFamily.Relationship,
                        Email = request.PatientFamily.Email,
                        Phone = request.PatientFamily.Phone,
                        CreatedBy = _currentUserService.GetUserId(),
                        CreatedOn = DateTime.Now,
                    });
                }
                await _db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task<bool> CheckUserInRoleAsync(string userID, string roleName)
    {
        var user = await _userManager.Users.Where(p => p.Id == userID && p.IsActive).FirstOrDefaultAsync();
        if(user == null)
        {
            return false;
        }
        return await _userManager.IsInRoleAsync(user, roleName);
    }

    public async Task<PaginationResponse<ListUserDTO>> GetListPatientAsync(UserListFilter request, CancellationToken cancellationToken)
    {
        var list_user = new List<ListUserDTO>();
        int totalRecords = 0;
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(request);
            var role = await _db.Roles
                .Where(p => p.Name == FSHRoles.Patient)
                .FirstOrDefaultAsync(cancellationToken);

            if (role == null)
            {
                return new PaginationResponse<ListUserDTO>(new List<ListUserDTO>(), 0, request.PageNumber, request.PageSize);
            }

            var query = _db.UserRoles
                .AsNoTracking()
                .Where(p => p.RoleId == role.Id)
                .Join(
                    _db.Users,
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => u
                );
            var spec2 = new EntitiesByBaseFilterSpec<ApplicationUser>(request);
            totalRecords = await query.WithSpecification(spec2).CountAsync(cancellationToken);

            var users = await query
                .Where(p => p.IsActive == request.IsActive)
                .WithSpecification(spec)
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                list_user.Add(new ListUserDTO
                {
                    Id = user.Id.ToString(),
                    Name = $"{user.FirstName} {user.LastName}",
                    UserName = user.UserName,
                    Address = user.Address,
                    Email = user.Email,
                    Gender = user.Gender,
                    ImageUrl = user.ImageUrl,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    Role = await GetRolesAsync(user.Id, cancellationToken),
                    isBanned = await _userManager.IsLockedOutAsync(user)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }

        return new PaginationResponse<ListUserDTO>(list_user, totalRecords, request.PageNumber, request.PageSize);
    }

    public async Task<List<GetDoctorResponse>> GetTop4Doctors()
    {
        var doctorResponses = new List<GetDoctorResponse>();
        try
        {
            var topDoctors = await _db.Feedbacks
            //.Where(f => f.DoctorProfileId != null)
            .GroupBy(f => f.DoctorProfileId)
            .Select(group => new
            {
                DoctorId = group.Key!.Value,
                AverageRating = group.Average(f => f.Rating),
                TotalFeedbacks = group.Count()
            })
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.TotalFeedbacks)
            .Take(4)
            .ToListAsync();
            if (topDoctors.Any())
            {
                foreach (var item in topDoctors)
                {
                    var doctorProfile = await _db.DoctorProfiles
                        .FirstOrDefaultAsync(d => d.Id == item.DoctorId);
                    var user = await _userManager.FindByIdAsync(doctorProfile.DoctorId);
                    bool check = false;
                    var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == doctorProfile.Id && p.Date == DateOnly.FromDateTime(DateTime.Now) && p.Status == WorkingStatus.Accept);
                    if (workingTime != null)
                    {
                        var current_time = DateTime.Now.TimeOfDay;
                        bool time = await _db.TimeWorkings.Where(p =>
                        p.CalendarID == workingTime.Id &&
                        p.StartTime <= current_time &&
                        p.EndTime >= current_time &&
                        p.IsActive
                        ).AnyAsync();

                        if (time)
                        {
                            check = true;
                        }
                    }
                    if (user.IsActive && !_userManager.IsLockedOutAsync(user).Result)
                    {
                        doctorResponses.Add(new GetDoctorResponse
                        {
                            Id = user.Id,
                            Email = user.Email,
                            FirstName = user.FirstName,
                            Gender = user.Gender,
                            ImageUrl = user.ImageUrl,
                            LastName = user.LastName,
                            PhoneNumber = user.PhoneNumber,
                            UserName = user.UserName,
                            DoctorProfile = doctorProfile,
                            Rating = Math.Round(item.AverageRating, 0),
                            isWorked = check
                        });
                    }
                }
            }
            if (doctorResponses.Count() < 4)
            {
                var existingDoctorIds = doctorResponses.Select(d => d.Id).ToList();

                var role = await _db.Roles.FirstOrDefaultAsync(p => p.Name == FSHRoles.Dentist);

                var additionalDoctors = await _db.Users
                    .Join(
                        _db.UserRoles.Where(ur => ur.RoleId == role.Id),
                        user => user.Id,
                        userRole => userRole.UserId,
                        (user, userRole) => user
                    )
                    .Where(u => u.IsActive && !existingDoctorIds.Contains(u.Id) &&
                        (!u.LockoutEnabled || (u.LockoutEnabled && (!u.LockoutEnd.HasValue || u.LockoutEnd <= DateTime.Now))))
                    .Take(4 - doctorResponses.Count)
                    .ToListAsync();

                foreach (var user in additionalDoctors)
                {
                    var doctorProfile = await _db.DoctorProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.DoctorId == user.Id);

                    if (doctorProfile != null)
                    {
                        doctorResponses.Add(new GetDoctorResponse
                        {
                            Id = user.Id,
                            Email = user.Email,
                            FirstName = user.FirstName,
                            Gender = user.Gender,
                            ImageUrl = user.ImageUrl,
                            LastName = user.LastName,
                            PhoneNumber = user.PhoneNumber,
                            UserName = user.UserName,
                            DoctorProfile = doctorProfile,
                            Rating = 0
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        return doctorResponses;
    }

    public async Task<DoctorDetailResponse> GetDoctorDetail(string doctorId)
    {
        var result = new DoctorDetailResponse();
        try
        {
            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == doctorId) ?? throw new NotFoundException("Doctor profile not found");

            var totalRating = await _db.Feedbacks
                .Where(f => f.DoctorProfileId == dProfile.Id)
                .GroupBy(f => f.DoctorProfileId)
                .Select(group => new
                {
                    AverageRating = group.Average(f => f.Rating),
                    TotalFeedbacks = group.Count()
                })
                .FirstOrDefaultAsync();

            var feedbackByRating = await _db.Feedbacks
            .Where(p => p.DoctorProfileId == dProfile.Id)
            .GroupBy(f => f.Rating)
            .Select(group => new
            {
                Rating = group.Key,
                TotalFeedbacks = group.Count(),
                //ServiceIds = group.Select(f => f.ServiceId).Distinct().ToList(),
                //Doctor = dProfile,
                Feedbacks = group.Select(f => new
                {
                    f.Id,
                    f.PatientProfileId,
                    f.DoctorProfileId,
                    f.ServiceId,
                    f.Message,
                    f.Rating,
                    f.CreatedOn,
                    Appointment = _db.Appointments.FirstOrDefault(p => p.Id == f.AppointmentId)
                }).ToList()
            })
            .OrderByDescending(x => x.Rating)
            .ToListAsync();

            var user = await _userManager.FindByIdAsync(doctorId);
            if (user == null)
                throw new NotFoundException("User not found");

            result.Id = user.Id;
            result.UserName = user.UserName;
            result.PhoneNumber = user.PhoneNumber;
            result.FirstName = user.FirstName;
            result.LastName = user.LastName;
            result.Email = user.Email;
            result.Gender = user.Gender;
            result.ImageUrl = user.ImageUrl;
            result.DoctorProfile = dProfile;
            result.Rating = 0;
            result.TotalFeedback = 0;
            if (totalRating != null)
            {
                result.Rating = Math.Round(totalRating.AverageRating, 0);
                result.TotalFeedback = totalRating?.TotalFeedbacks;
                result.DoctorFeedback = new List<FeedBackDoctorResponse>();

                foreach (var ratingGroup in feedbackByRating)
                {
                    var feedbackDoctorResponse = new FeedBackDoctorResponse
                    {
                        RatingType = ratingGroup.Rating,
                        TotalRating = ratingGroup.TotalFeedbacks,
                        Feedbacks = new List<FeedBackResponse>()
                    };

                    foreach (var feedback in ratingGroup.Feedbacks)
                    {
                        var service = await _db.Services.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == feedback.ServiceId);

                        var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == feedback.PatientProfileId);
                        var patientUser = patientProfile != null ?
                            await _userManager.FindByIdAsync(patientProfile.UserId) : null;

                        var feedbackResponse = new FeedBackResponse
                        {
                            FeedbackID = feedback.Id,
                            ServiceID = feedback.ServiceId,
                            ServiceName = service?.ServiceName,
                            PatientID = patientUser.Id,
                            PatientName = patientUser != null ? $"{patientUser.FirstName} {patientUser.LastName}" : null,
                            CreateDate = feedback.CreatedOn,
                            Ratings = feedback.Rating,
                            Message = feedback.Message,
                            CanFeedback = feedback.Appointment.canFeedback,
                            PatientAvatar = patientUser.ImageUrl
                        };

                        feedbackDoctorResponse.Feedbacks.Add(feedbackResponse);
                    }

                    result.DoctorFeedback.Add(feedbackDoctorResponse);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        return result;
    }

    public async Task<UserProfileResponse> GetUserDetailByID(string userId, CancellationToken cancellationToken)
    {
        var profile = new UserProfileResponse();
        try
        {
            var user = await _userManager.FindByIdAsync(userId) ?? throw new BadRequestException("User is not found.");
            var user_role = GetRolesAsync(user.Id, cancellationToken).Result;
            profile.Id = user.Id;
            profile.UserName = user.UserName;
            profile.FirstName = user.FirstName;
            profile.LastName = user.LastName;
            profile.Gender = user.Gender;
            profile.BirthDate = user.BirthDate;
            profile.Email = user.Email;
            profile.IsActive = user.IsActive;
            profile.EmailConfirmed = user.EmailConfirmed;
            profile.PhoneNumber = user.PhoneNumber;
            profile.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
            profile.Job = user.Job;
            profile.ImageUrl = user.ImageUrl;
            profile.Address = user.Address;
            if (user_role.RoleName.Equals(FSHRoles.Dentist))
            {
                profile.DoctorProfile = await _db.DoctorProfiles.Where(p => p.DoctorId == user.Id).FirstOrDefaultAsync();
            }
            else if (user_role.RoleName.Equals(FSHRoles.Patient))
            {
                profile.PatientProfile = await _db.PatientProfiles.Where(p => p.UserId == user.Id).FirstOrDefaultAsync(cancellationToken);
                profile.PatientFamily = await _db.PatientFamilys.Where(p => p.PatientProfileId == profile.PatientProfile.Id).FirstOrDefaultAsync(cancellationToken);
                profile.MedicalHistory = await _db.MedicalHistorys.Where(p => p.PatientProfileId == profile.PatientProfile.Id).FirstOrDefaultAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        return profile;
    }

    public async Task<PaginationResponse<ListUserDTO>> GetAllStaff(UserListFilter request, CancellationToken cancellationToken)
    {
        var list_user = new List<ListUserDTO>();
        int totalRecords = 0;
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(request);
            var role = await _db.Roles
                .Where(p => p.Name == FSHRoles.Staff)
                .FirstOrDefaultAsync(cancellationToken);

            if (role == null)
            {
                return new PaginationResponse<ListUserDTO>(new List<ListUserDTO>(), 0, request.PageNumber, request.PageSize);
            }

            var query = _db.UserRoles
                .AsNoTracking()
                .Where(p => p.RoleId == role.Id)
                .Join(
                    _db.Users,
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => u
                );
            var spec2 = new EntitiesByBaseFilterSpec<ApplicationUser>(request);
            totalRecords = await query.WithSpecification(spec2).CountAsync(cancellationToken);

            var users = await query
                .Where(p => p.IsActive == request.IsActive)
                .WithSpecification(spec)
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                if (await _userManager.IsInRoleAsync(user, FSHRoles.Staff))
                {
                    list_user.Add(new ListUserDTO
                    {
                        Name = $"{user.FirstName} {user.LastName}",
                        Id = user.Id.ToString(),
                        UserName = user.UserName,
                        Address = user.Address,
                        Email = user.Email,
                        Gender = user.Gender,
                        ImageUrl = user.ImageUrl,
                        PhoneNumber = user.PhoneNumber,
                        IsActive = user.IsActive,
                        Role = await GetRolesAsync(user.Id, cancellationToken),
                        isBanned = await _userManager.IsLockedOutAsync(user)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }

        return new PaginationResponse<ListUserDTO>(list_user, totalRecords, request.PageNumber, request.PageSize);
    }

    public void TestSendMail()
    {
        RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
        {
            Email = "toandoan2804@gmail.com",
            UserName = "Toan",
            BanReason = "Was Ban"
        };
        var mailRequest = new MailRequest(
                    new List<string> { "toandoan2804@gmail.com" },
                    "Ban",
                    _templateService.GenerateEmailTemplate("email-ban-user", eMailModel));
        _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
    }

    public Task<bool> CheckValidExpYear(DateOnly date, int exp)
    {
        try
        {
            var currentDate = DateOnly.FromDateTime(DateTime.Now);
            date = date.AddYears(exp);

            return Task.FromResult(currentDate > date);
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }
}