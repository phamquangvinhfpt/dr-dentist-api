using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Verify;
using FSH.WebApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FSH.WebApi.Host.Controllers.Identity;

public class UsersController : VersionNeutralApiController
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUserService;
    private readonly IServiceService _serviceService;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(IUserService userService, ICurrentUser currentUserService, IServiceService serviceService, UserManager<ApplicationUser> userManager)
    {
        _userService = userService;
        _currentUserService = currentUserService;
        _serviceService = serviceService;
        _userManager = userManager;
    }

    //checked
    [HttpPost("get-users")]
    [MustHavePermission(FSHAction.View, FSHResource.Users)]
    [OpenApiOperation("Get list of all users.", "")]
    public Task<PaginationResponse<ListUserDTO>> GetListAsync(UserListFilter request, CancellationToken cancellationToken)
    {
        return _userService.SearchAsync(request, cancellationToken);
    }
    //checked
    [HttpGet("{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.Users)]
    [OpenApiOperation("Get a user's details.", "")]
    public Task<UserDetailsDto> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return _userService.GetAsync(id, cancellationToken);
    }

    [HttpGet("{id}/roles")]
    [MustHavePermission(FSHAction.View, FSHResource.UserRoles)]
    [OpenApiOperation("Get a user's roles.", "")]
    public Task<UserRoleDto> GetRolesAsync(string id, CancellationToken cancellationToken)
    {
        return _userService.GetRolesAsync(id, cancellationToken);
    }

    [HttpPost("{id}/roles")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    [MustHavePermission(FSHAction.Update, FSHResource.UserRoles)]
    [OpenApiOperation("Update a user's assigned roles.", "")]
    public Task<string> AssignRolesAsync(string id, UserRolesRequest request, CancellationToken cancellationToken)
    {
        return _userService.AssignRolesAsync(id, request, cancellationToken);
    }
    //checked
    [HttpPost("create-user")]
    [MustHavePermission(FSHAction.Create, FSHResource.Users)]
    [OpenApiOperation("Creates a new Staff/Doctor.", "")]
    public Task<string> CreateAsync([FromForm]CreateUserRequest request, CancellationToken cancellation)
    {
        var validation = new CreateUserRequestValidator(_userService, _currentUserService, _serviceService).ValidateAsync(request);
        if (!validation.IsCompleted)
        {
            var t = validation.Result;
            if (!t.IsValid)
            {
                throw new BadRequestException(t.Errors[0].ErrorMessage);
            }
        }
        if(request.Role == FSHRoles.Dentist)
        {
            request.Job = FSHRoles.Dentist;
        }
        else
        {
            request.Job = FSHRoles.Staff;
        }
        return _userService.CreateAsync(request, IsMobile(), GetLanguageFromRequest(), GetOriginFromRequest(), cancellation);
    }
    //checked
    [HttpPost("get-doctors")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Get All Doctor For Customer.", "")]
    public Task<PaginationResponse<GetDoctorResponse>> GetAllDoctor(UserListFilter request, [FromQuery] DateOnly date)
    {
        return _userService.GetAllDoctor(request, date);
    }

    //checked
    [HttpGet("get-top-doctors")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Get TOp 4 Doctor For Customer.", "")]
    public Task<List<GetDoctorResponse>> GetTop4Doctor()
    {
        return _userService.GetTop4Doctors();
    }

    //checked
    [HttpPost("self-register")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Regist new patient.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public Task<string> SelfRegisterAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = new CreateUserRequestValidator(_userService, _currentUserService, _serviceService).ValidateAsync(request);
        if (!validation.IsCompleted)
        {
            var t = validation.Result;
            if (!t.IsValid)
            {
                throw new BadRequestException(t.Errors[0].ErrorMessage);
            }
        }
        return _userService.CreateAsync(request, IsMobile(), GetLanguageFromRequest(), GetOriginFromRequest(), cancellationToken);
    }
    //checked
    [HttpPost("{id}/toggle-status")]
    [MustHavePermission(FSHAction.Update, FSHResource.Users)]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    [OpenApiOperation("Toggle a user's active status.", "")]
    public async Task<ActionResult> ToggleStatusAsync(ToggleStatusRequest request, CancellationToken cancellationToken)
    {
        await _userService.ToggleStatusAsync(request, cancellationToken);
        return Ok();
    }
    [HttpGet("confirm-email")]
    [AllowAnonymous]
    [OpenApiOperation("Confirm email address for a user.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Search))]
    public Task<string> ConfirmEmailAsync([FromQuery] string tenant, [FromQuery] string userId, [FromQuery] string code, CancellationToken cancellationToken)
    {
        return _userService.ConfirmEmailAsync(userId, code, tenant, cancellationToken);
    }

    [HttpGet("confirm-phone-number")]
    [OpenApiOperation("Confirm phone number for a user.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Search))]
    public Task<string> ConfirmPhoneNumberAsync([FromQuery] string code)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }

        return _userService.ConfirmPhoneNumberAsync(userId, code);
    }

    [HttpGet("confirm-account-phone-number")]
    [OpenApiOperation("Confirm account using phone number.", "")]
    [AllowAnonymous]
    [TenantIdHeader]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Search))]
    public async Task<string> ConfirmAccountByPhoneNumberAsync(string phone, string code)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        return await _userService.ConfirmPhoneNumberAsync(user.Id, code);
    }

    [HttpGet("resend-phone-confirm")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Resend phone confirmation code.", "")]
    public async Task<string> ResendPhoneConfirmAsync(string phone)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        return await _userService.ResendPhoneNumberCodeConfirm(user.Id);
    }

    [HttpGet("resend-phone-number-code")]
    [OpenApiOperation("Resend phone number confirmation code.", "")]
    public Task<string> ResendPhoneNumberCodeConfirmAsync()
    {
        return Mediator.Send(new ResendPhoneCodeRequest());
    }

    [HttpGet("resend-email-confirm")]
    [OpenApiOperation("Resend email confirmation code.", "")]
    public Task<string> ResendEmailConfirmAsync()
    {
        var request = new ResendEmailConfirmRequest { Origin = GetOriginFromRequest(), Local = GetLanguageFromRequest() };
        return Mediator.Send(request);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Request a password reset email for a user.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        return _userService.ForgotPasswordAsync(request, GetLanguageFromRequest(), GetOriginFromRequest());
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [OpenApiOperation("Reset a user's password.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return _userService.ResetPasswordAsync(request);
    }

    //checked
    [HttpPost("get-patients")]
    [MustHavePermission(FSHAction.View, FSHResource.Users)]
    [OpenApiOperation("Get list of all patient.", "")]
    public Task<PaginationResponse<ListUserDTO>> GetListPatientAsync(UserListFilter request, CancellationToken cancellationToken)
    {
        return _userService.GetListPatientAsync(request, cancellationToken);
    }

    [HttpPost("customer/get-doctor/{id}")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Get Doctor Detail For Customer.", "")]
    public async Task<DoctorDetailResponse> GetDoctorDetailAsync(string id)
    {
        return await _userService.GetDoctorDetail(id);
    }

    //checked
    [HttpGet("get-user/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.Users)]
    [OpenApiOperation("Get User Detail by ID for Admin.", "")]
    public Task<UserProfileResponse> GetUserDetailByIDAsync(string id, CancellationToken cancellationToken)
    {
        return _userService.GetUserDetailByID(id, cancellationToken);
    }

    //checked
    [HttpPost("get-staffs")]
    [MustHavePermission(FSHAction.View, FSHResource.Users)]
    [OpenApiOperation("Get All Staff For Admin.", "")]
    public Task<PaginationResponse<ListUserDTO>> GetAllStaff(UserListFilter request, CancellationToken cancellationToken)
    {
        return _userService.GetAllStaff(request, cancellationToken);
    }
    [HttpGet("test")]
    [OpenApiOperation("Test send mail.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public async Task TestSendmail()
    {
        _userService.TestSendMail();
    }
    private string GetOriginFromRequest()
    {
        if (Request.Headers.TryGetValue("x-from-host", out var values))
        {
            return $"{Request.Scheme}://{values.First()}";
        }

        return $"{Request.Scheme}://{Request.Host.Value}{Request.PathBase.Value}";
    }

    private string GetLanguageFromRequest()
    {
        return HttpContext.Request.Headers["Accept-Language"].ToString();
    }

    public bool IsMobile()
    {
        string userAgent = Request.Headers["User-Agent"].ToString();

        string[] mobileKeywords = new[]
        {
            "Android", "webOS", "iPhone", "iPad", "iPod", "BlackBerry", "IEMobile", "Opera Mini",
            "Mobile", "mobile", "Tablet", "tablet"
        };

        return mobileKeywords.Any(userAgent.Contains);
    }
}
