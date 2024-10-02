using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Verify;
using System.Security.Claims;

namespace FSH.WebApi.Host.Controllers.Identity;

public class UsersController : VersionNeutralApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    [MustHavePermission(FSHAction.View, FSHResource.Users)]
    [OpenApiOperation("Get list of all users.", "")]
    public Task<List<ListUserDTO>> GetListAsync(CancellationToken cancellationToken)
    {
        return _userService.GetListAsync(cancellationToken);
    }

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

    [HttpPost]
    [MustHavePermission(FSHAction.Create, FSHResource.Users)]
    [OpenApiOperation("Creates a new Staff/Doctor.", "")]
    public Task<string> CreateAsync(CreateUserRequest request)
    {
        var validation = new CreateUserRequestValidator(_userService).ValidateAsync(request);
        if (!validation.IsCompleted)
        {
            var t = validation.Result;
            if (!t.IsValid)
                throw new BadRequestException(t.Errors[0].ErrorMessage);
        }
        return _userService.CreateAsync(request, GetOriginFromRequest());
    }

    [HttpPost("update-patient-record")]
    [MustHavePermission(FSHAction.Update, FSHResource.Users)]
    [OpenApiOperation("Update Patient Record.", "")]
    public Task<string> UpdatePatientRecord(CreatePatientRecord request)
    {
        // TODO: check if registering anonymous users is actually allowed (should probably be an appsetting)
        // and return UnAuthorized when it isn't
        // Also: add other protection to prevent automatic posting (captcha?)
        var validation = new CreatePatientRecordValidator(_userService).ValidateAsync(request);
        if (!validation.IsCompleted)
        {
            var t = validation.Result;
            if (!t.IsValid)
                throw new BadRequestException(t.Errors[0].ErrorMessage);
        }
        return _userService.UpdatePatientRecordAsync(request);
    }

    [HttpPost("self-register")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Regist new patient.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public Task<string> SelfRegisterAsync(SeftRegistNewPatient request)
    {
        // TODO: check if registering anonymous users is actually allowed (should probably be an appsetting)
        // and return UnAuthorized when it isn't
        // Also: add other protection to prevent automatic posting (captcha?)
        var validation = new SeftRegistNewPatientValidator(_userService).ValidateAsync(request);
        if (!validation.IsCompleted) {
            var t = validation.Result;
            if(!t.IsValid)
                throw new BadRequestException(t.Errors[0].ErrorMessage);
        }
        return _userService.RegisterNewPatientAsync(request, GetOriginFromRequest());
    }

    [HttpPost("{id}/toggle-status")]
    [MustHavePermission(FSHAction.Update, FSHResource.Users)]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    [OpenApiOperation("Toggle a user's active status.", "")]
    public async Task<ActionResult> ToggleStatusAsync(ToggleUserStatusRequest request, CancellationToken cancellationToken)
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
        var request = new ResendEmailConfirmRequest { Origin = GetOriginFromRequest() };
        return Mediator.Send(request);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Request a password reset email for a user.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        return _userService.ForgotPasswordAsync(request, GetOriginFromRequest());
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [OpenApiOperation("Reset a user's password.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return _userService.ResetPasswordAsync(request);
    }

    private string GetOriginFromRequest()
    {
        if (Request.Headers.TryGetValue("x-from-host", out var values))
        {
            return $"{Request.Scheme}://{values.First()}";
        }

        return $"{Request.Scheme}://{Request.Host.Value}{Request.PathBase.Value}";
    }
}
