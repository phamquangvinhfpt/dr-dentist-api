using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class CreateAppointmentRequest : IRequest<string>
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public AppointmentType Type { get; set; }
    public string? Notes { get; set; }
    public DepositRequest? DepositRequest { get; set; }
}

public class CreateAppointmentRequestValidator : CustomValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator(IUserService userService, ICurrentUser currentUser, IServiceService serviceService, IAppointmentService appointmentService)
    {
        RuleFor(p => p.PatientId)
            .NotNull()
            .WithMessage("Patient information is empty")
            .MustAsync(async (id, _) =>
            {
                if (await userService.CheckUserInRoleAsync(currentUser.GetUserId().ToString(), FSHRoles.Patient))
                {
                    return id == currentUser.GetUserId().ToString();
                }
                return true;
            })
            .WithMessage("You can only create appointments for yourself as a patient")
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
            .WithMessage((_, id) => $"Patient {id} is not valid.")
            .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Patient))
            .WithMessage((_, id) => $"User {id} is not patient.");

        RuleFor(p => p.DentistId)
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
            .When(p => p.DentistId != null)
            .WithMessage((_, id) => $"Patient {id} is not valid.")
            .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Dentist))
            .When(p => p.DentistId != null)
            .WithMessage((_, id) => $"User {id} is not patient.");

        RuleFor(p => p.ServiceId)
            .NotNull()
            .WithMessage("Service information is empty")
            .MustAsync(async (id, _) => await serviceService.CheckExistingService(id))
            .WithMessage((_, id) => $"Service {id} is not valid.");

        RuleFor(p => p.AppointmentDate)
            .NotNull()
            .WithMessage("Date should be filled")
            .MustAsync(async (date, _) => await appointmentService.CheckAppointmentDateValid(date))
            .WithMessage((_, date) => $"Date {date} is not valid.");

        RuleFor(p => p.StartTime)
            .NotNull()
            .WithMessage("Start time is required")
            .Must(time => time >= TimeSpan.FromHours(8) && time <= TimeSpan.FromHours(17))
            .WithMessage("Start time must be between 8:00 AM and 5:00 PM");

        RuleFor(p => p.Duration)
            .NotNull()
            .WithMessage("Duration is required")
            .Must(duration => duration >= TimeSpan.FromMinutes(30) && duration <= TimeSpan.FromHours(1))
            .WithMessage("Duration must be between 30 minutes and 1 hours")
            .Must((request, duration) =>
                (request.StartTime + duration) <= TimeSpan.FromHours(17))
            .WithMessage("Appointment must end before 5:00 PM");

        RuleFor(p => p.Type)
            .IsInEnum()
            .WithMessage("Invalid appointment type")
            .NotEqual(AppointmentType.None)
            .WithMessage("Appointment type must be selected");

        RuleFor(p => p.DepositRequest)
            .SetValidator(new DepositRequestValidator());

        RuleFor(p => p.Notes)
            .MaximumLength(500)
            .When(p => p.Notes != null)
            .WithMessage("Notes cannot exceed 500 characters");
    }
}

public class CreateAppointmentRequestHandler : IRequestHandler<CreateAppointmentRequest, string>
{
    public CreateAppointmentRequestHandler()
    {
    }

    public Task<string> Handle(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}