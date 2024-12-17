using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.AppointmentCalendars;
public class GetAvailableTimeRequest : IRequest<List<AvailableTimeResponse>>
{
    public string? DoctorID { get; set; }
    public DateOnly Date { get; set; }
}

public class GetAvailableTimeRequestValidator : CustomValidator<GetAvailableTimeRequest>
{
    public GetAvailableTimeRequestValidator(IUserService userService)
    {
        RuleFor(p => p.DoctorID)
            .NotEmpty()
            .WithMessage("Doctor information is empty")
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
            .WithMessage((_, id) => "User is not found")
            .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Dentist))
            .WithMessage((_, id) => "User is not dentist");

        RuleFor(p => p.Date)
            .NotEmpty()
            .MustAsync(async (date, _) => (date >= DateOnly.FromDateTime(DateTime.Now)))
            .WithMessage((_, date) => $"Date {date} is not available");
    }
}

public class GetAvailableTimeRequestHandler : IRequestHandler<GetAvailableTimeRequest, List<AvailableTimeResponse>>
{
    private readonly IAppointmentCalendarService _workingCalendarService;
    private readonly IStringLocalizer<GetAvailableTimeRequest> _t;

    public GetAvailableTimeRequestHandler(IAppointmentCalendarService workingCalendarService, IStringLocalizer<GetAvailableTimeRequest> t)
    {
        _workingCalendarService = workingCalendarService;
        _t = t;
    }

    public async Task<List<AvailableTimeResponse>> Handle(GetAvailableTimeRequest request, CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetAvailableTimeSlot(request, cancellationToken);
    }
}
