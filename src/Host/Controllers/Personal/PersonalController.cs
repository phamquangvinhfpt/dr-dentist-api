using FSH.WebApi.Application.Auditing;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Profile;
using System.Security.Claims;

namespace FSH.WebApi.Host.Controllers.Identity;

public class PersonalController : VersionNeutralApiController
{
    private readonly IUserService _userService;
    private readonly IAuditService _auditService;
    private readonly IAppointmentCalendarService _workingCalendarService;

    public PersonalController(IUserService userService, IAuditService auditService, IAppointmentCalendarService workingCalendarService)
    {
        _userService = userService;
        _auditService = auditService;
        _workingCalendarService = workingCalendarService;
    }


    //checked
    [HttpGet("profile")]
    [OpenApiOperation("Get profile details of currently logged in user.", "")]
    public async Task<UserProfileResponse> GetProfileAsync(CancellationToken cancellationToken)
    {
        return await _userService.GetUserProfileAsync(cancellationToken);
    }
    //checked
    [HttpPut("profile")]
    [OpenApiOperation("Update profile details of currently logged in user.", "")]
    public Task<string> UpdateProfileAsync(UpdateUserRequest request)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }

        request.UserId = userId;
        return Mediator.Send(request);
    }

    [HttpPut("update-password")]
    [OpenApiOperation("Update password of currently logged in user.", "")]
    [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Register))]
    public async Task<ActionResult> ChangePasswordAsync(ChangePasswordRequest model)
    {
        await _userService.ChangePasswordAsync(model);
        return Ok();
    }

    [HttpGet("permissions")]
    [OpenApiOperation("Get permissions of currently logged in user.", "")]
    public async Task<ActionResult<List<string>>> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        return User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId)
            ? Unauthorized()
            : Ok(await _userService.GetPermissionsAsync(userId, cancellationToken));
    }

    [HttpPost("logs")]
    [OpenApiOperation("Get audit logs of currently logged in user.", "")]
    public Task<PaginationResponse<AuditDto>> GetLogsAsync(GetMyAuditLogsRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpGet("logs/resource-type")]
    [OpenApiOperation("Get resource type for audit logs.", "")]
    public async Task<List<string>> GetResourceNamesAsync()
    {
        return await _auditService.GetResourceName();
    }

    [HttpPut("update-email")]
    [OpenApiOperation("Update email of currently logged in user.", "")]
    public Task<string> UpdateEmailAsync(UpdateEmailRequest request)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }
        request.UserId = userId;
        request.Origin = GetOriginFromRequest();
        return Mediator.Send(request);
    }

    [HttpPut("update-phone")]
    [OpenApiOperation("Update phone number of currently logged in user.", "")]
    public Task<string> UpdatePhoneNumberAsync(UpdatePhoneNumberRequest request)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }
        request.UserId = userId;
        return Mediator.Send(request);
    }

    [HttpPut("update-avatar")]
    [OpenApiOperation("Update avatar of currently logged in user.", "")]
    public Task<string> UpdateAvatarAsync([FromForm] UpdateAvatarRequest request)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }

        request.UserId = userId;
        return Mediator.Send(request);
    }
    //checked
    [HttpPut("patient/update-profile")]
    [OpenApiOperation("Update Patient Profile, Medical History, Patient Family.", "")]
    public Task<string> UpdatePatientProfileAsync(UpdateOrCreatePatientProfile request)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }
        return Mediator.Send(request);
    }
    //checked
    [HttpPost("update-doctor-profile")]
    [MustHavePermission(FSHAction.Update, FSHResource.Users)]
    [OpenApiOperation("Update Doctor Profile", "")]
    public Task<string> UpdateDoctorProfile(UpdateDoctorProfile request)
    {
        return Mediator.Send(request);
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