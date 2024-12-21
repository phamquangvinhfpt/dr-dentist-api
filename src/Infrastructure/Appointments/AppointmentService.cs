using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using FluentValidation;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Mailing;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.Appointments;
internal class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly ILogger<AppointmentService> _logger;
    private readonly IAppointmentCalendarService _workingCalendarService;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;
    private static string APPOINTMENT = "APPOINTMENT";
    private static string FOLLOW = "FOLLOW";
    private static string NON = "NON";
    private static string REEXAM = "REEXAM";
    public AppointmentService(
        ApplicationDbContext db,
        ICacheService cacheService,
        IStringLocalizer<AppointmentService> t,
        ICurrentUser currentUserService,
        UserManager<ApplicationUser> userManager,
        IJobService jobService,
        ILogger<AppointmentService> logger,
        IAppointmentCalendarService workingCalendarService,
        IMailService mailService,
        IEmailTemplateService templateService,
        INotificationService notificationService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
        _workingCalendarService = workingCalendarService;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _templateService = templateService;
        _mailService = mailService;
    }

    public Task<bool> CheckAppointmentDateValid(DateOnly date)
    {
        return Task.FromResult(date >= DateOnly.FromDateTime(DateTime.Now));
    }

    public async Task<bool> CheckAppointmentExisting(Guid appointmentId)
    {
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
        return appointment is not null;
    }

    public async Task<bool> CheckAvailableAppointment(string? patientId)
    {
        var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == patientId);
        bool appointment = await _db.Appointments
            .Where(p => p.PatientId == patient.Id &&
            (p.Status == Domain.Appointments.AppointmentStatus.Pending || p.Status == AppointmentStatus.Confirmed)
            ).AnyAsync();
        bool isSunday = DateOnly.FromDateTime(DateTime.Now).DayOfWeek == DayOfWeek.Sunday;
        return !appointment && !isSunday;
    }

    public async Task<PayAppointmentRequest> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            bool hasDoctor = request.DentistId == null;

            var doctor = hasDoctor ? null : await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DentistId)
                ?? throw new NotFoundException($"Doctor with ID {request.DentistId} not found");

            var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == request.PatientId)
                ?? throw new NotFoundException($"Patient with ID {request.PatientId} not found");

            var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceId)
                ?? throw new NotFoundException($"Service with ID {request.ServiceId} not found");

            string currentUserRole = _currentUserService.GetRole();
            bool isStaffOrAdmin = currentUserRole == FSHRoles.Admin || currentUserRole == FSHRoles.Staff;

            var app = new Domain.Appointments.Appointment
            {
                PatientId = patient.Id,
                ServiceId = service.Id,
                AppointmentDate = request.AppointmentDate,
                StartTime = request.StartTime,
                Duration = request.Duration,
                Status = isStaffOrAdmin ? AppointmentStatus.Confirmed : AppointmentStatus.Pending,
                Notes = request.Notes,
                canFeedback = false,
            };

            if (!hasDoctor)
            {
                app.DentistId = doctor.Id;
            }

            var appointment = _db.Appointments.Add(app).Entity;

            var cal = new Domain.Identity.AppointmentCalendar
            {
                PatientId = patient.Id,
                AppointmentId = appointment.Id,
                Date = appointment.AppointmentDate,
                StartTime = appointment.StartTime,
                EndTime = appointment.StartTime.Add(appointment.Duration),
                Status = isStaffOrAdmin
                    ? Domain.Identity.CalendarStatus.Booked
                    : Domain.Identity.CalendarStatus.Waiting,
                Type = AppointmentType.Appointment,
            };

            if (!hasDoctor)
            {
                cal.DoctorId = doctor.Id;
            }

            var calendar = _db.AppointmentCalendars.Add(cal).Entity;
            double deposit = Math.Round(service.TotalPrice * 0.3, 0);
            var pay = _db.Payments.Add(new Domain.Payments.Payment
            {
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
                PatientProfileId = appointment.PatientId,
                ServiceId = service.Id,
                AppointmentId = appointment.Id,
                DepositAmount = isStaffOrAdmin ? 0 : deposit,
                DepositDate = isStaffOrAdmin ? DateOnly.FromDateTime(DateTime.Now) : null,
                RemainingAmount = isStaffOrAdmin ? service.TotalPrice : service.TotalPrice - deposit,
                Amount = service.TotalPrice,
                Status = isStaffOrAdmin ? Domain.Payments.PaymentStatus.Incomplete : Domain.Payments.PaymentStatus.Waiting,
            }).Entity;
            await _db.SaveChangesAsync(cancellationToken);
            var result = new PayAppointmentRequest
            {
                Key = isStaffOrAdmin ? null : _jobService.Schedule(
                    () => DeleteUnpaidBooking(request.PatientId!, appointment.Id, calendar.Id, pay.Id, cancellationToken),
                    TimeSpan.FromMinutes(11)),
                AppointmentId = appointment.Id,
                PaymentID = pay.Id,
                PatientCode = patient.PatientCode,
                Amount = deposit,
                Time = isStaffOrAdmin ? TimeSpan.FromMinutes(0) : TimeSpan.FromMinutes(10),
                IsPay = isStaffOrAdmin,
                IsVerify = true,
            };

            if (!isStaffOrAdmin)
            {
                await _cacheService.SetAsync(
                        patient.PatientCode!,
                        result,
                        TimeSpan.FromMinutes(11),
                        cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new ApplicationException("An error occurred while creating the appointment", ex);
        }
    }

    public async Task VerifyAndFinishBooking(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool check = _jobService.Delete(request.Key!);
            if (!check)
            {
                throw new KeyNotFoundException("Key job not found");
            }
            await _cacheService.RemoveAsync(request.PatientCode!);
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentId);
            var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentID);
            var patientId = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == appointment.PatientId);

            appointment.Status = AppointmentStatus.Confirmed;
            calendar.Status = Domain.Identity.CalendarStatus.Booked;
            payment.DepositDate = DateOnly.FromDateTime(DateTime.Now);
            payment.Status = Domain.Payments.PaymentStatus.Incomplete;

            await _db.SaveChangesAsync(cancellationToken);
            if (appointment.DentistId != default)
            {
                _jobService.Schedule(
               () => SendAppointmentActionNotification(appointment.PatientId,
               appointment.DentistId,
               appointment.AppointmentDate,
               TypeRequest.Verify, cancellationToken), TimeSpan.FromSeconds(5));
            }

            var notification = new BasicNotification
            {
                Message = "Lịch hẹn của bạn đã được xác nhận!",
                Label = BasicNotification.LabelType.Success,
                LargeBody = "Lịch hẹn nha khoa của bạn đã được xác nhận thành công. Chi tiết lịch hẹn đã được gửi vào email đăng ký.",
                SummaryText = "Đặt lịch khám nha khoa thành công",
                Title = "Đặt lịch thành công!",
                Url = "/appointment",
            };
            await _notificationService.SendPaymentNotificationToUser(patientId.UserId, notification, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task DeleteUnpaidBooking(string userID, Guid appointmentId, Guid calendarID, Guid paymentID, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
            if (appointment.Status.Equals(AppointmentStatus.Confirmed))
            {
                return;
            }
            var user = await _db.Users.FirstOrDefaultAsync(p => p.Id == _currentUserService.GetUserId().ToString());
            if (user.AccessFailedCount == 3)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(7));
                // send mail to notify block action
                RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
                {
                    Email = user.Email,
                    UserName = $"{user.FirstName} {user.LastName}",
                    BanReason = "Tài khoản của bạn gần đây có hành động spam booking nên chúng tôi tạm khóa tài khoản của bạn trong 7 ngày"
                };
                var mailRequest = new MailRequest(
                            new List<string> { user.Email },
                            "Tài khoản tạm khóa",
                            _templateService.GenerateEmailTemplate("email-ban-user", eMailModel));
                _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
            }
            else
            {
                user.AccessFailedCount += 1;
            }

            var pay = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentID);
            var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.Id == calendarID);

            calendar.Status = Domain.Identity.CalendarStatus.Failed;
            _db.AppointmentCalendars.Remove(calendar);
            pay.Status = Domain.Payments.PaymentStatus.Failed;
            appointment.Status = AppointmentStatus.Failed;
            _db.Appointments.Remove(appointment);
            _db.Payments.Remove(pay);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    public async Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<AppointmentResponse>();
            var spec = new EntitiesByPaginationFilterSpec<Appointment>(filter);
            var appointmentsQuery = _db.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking();

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.AppointmentDate == date);
            }

            appointmentsQuery = appointmentsQuery.Where(p => p.DentistId != Guid.Empty);
            if (currentUser == FSHRoles.Dentist)
            {
                var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == _currentUserService.GetUserId().ToString());
                appointmentsQuery = appointmentsQuery.Where(p => p.DentistId == dProfile.Id && p.Status == AppointmentStatus.Come);
            }
            else if (currentUser == FSHRoles.Patient)
            {
                var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                appointmentsQuery = appointmentsQuery.Where(p => p.PatientId == patientProfile.Id);
            }
            if (currentUser == FSHRoles.Staff)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Status != AppointmentStatus.Failed && w.Status != AppointmentStatus.Pending);
            }
            int count = appointmentsQuery.Count();
            appointmentsQuery = appointmentsQuery.OrderByDescending(p => p.AppointmentDate).WithSpecification(spec);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DentistId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                bool feedback = await _db.Feedbacks.AnyAsync(p => p.AppointmentId == a.Appointment.Id);
                var dUser = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                var pUser = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Patient.UserId).Result;
                var r = new AppointmentResponse
                {
                    PatientUserID = pUser.Id,
                    AppointmentId = a.Appointment.Id,
                    PatientId = a.Appointment.PatientId,
                    ServiceId = a.Appointment.ServiceId,
                    AppointmentDate = a.Appointment.AppointmentDate,
                    StartTime = a.Appointment.StartTime,
                    Duration = a.Appointment.Duration,
                    Status = a.Appointment.Status,
                    Notes = a.Appointment.Notes,
                    PatientPhone = pUser.PhoneNumber != null ? pUser.PhoneNumber : null,

                    canFeedback = a.Appointment.canFeedback,
                    isFeedback = feedback,

                    DentistId = a.Appointment.DentistId,
                    DentistName = $"{dUser.FirstName} {dUser.LastName}",
                    PatientCode = a.Patient?.PatientCode,
                    PatientName = $"{pUser.FirstName} {pUser.LastName}",
                    ServiceName = a.Service?.ServiceName,
                    ServicePrice = a.Service?.TotalPrice ?? 0,
                    PaymentStatus = a.Payment is not null ? a.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
                    Type = AppointmentType.Appointment
                };
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == a.Doctor.Id && p.Date == a.Appointment.AppointmentDate && p.Status == WorkingStatus.Accept);

                if (calendar != null)
                {
                    if (calendar.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == calendar.RoomID);
                        r.RoomID = room.Id;
                        r.RoomName = room.RoomName;
                    }
                }
                result.Add(r);
            }
            return new PaginationResponse<AppointmentResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task RescheduleAppointment(RescheduleRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Console.WriteLine(_currentUserService.GetUserId());
            string user_role = _currentUserService.GetRole();
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID) ?? throw new NotFoundException("Error when find appointment.");

            if (appointment.SpamCount < 3)
            {
                appointment.SpamCount += 1;
            }
            else if (appointment.SpamCount == 3 && user_role == FSHRoles.Patient)
            {
                var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                if (appointment.PatientId != patientProfile.Id)
                {
                    throw new UnauthorizedAccessException("Only Patient can reschedule their appointment");
                }
                var user = await _userManager.FindByIdAsync(_currentUserService.GetUserId().ToString());
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(7));
                appointment.Status = AppointmentStatus.Failed;
                var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
                calendar.Status = CalendarStatus.Failed;
                var pay = await _db.Payments.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
                pay.Status = Domain.Payments.PaymentStatus.Canceled;

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
                {
                    Email = user.Email,
                    UserName = $"{user.FirstName} {user.LastName}",
                    BanReason = "Tài khoản của bạn gần đây có hành động bất thường nên chúng tôi tạm khóa tài khoản của bạn trong 7 ngày"
                };
                var mailRequest = new MailRequest(
                            new List<string> { user.Email },
                            "Tài khoản tạm khóa",
                            _templateService.GenerateEmailTemplate("email-ban-user", eMailModel));
                _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
                return;
            }
            else if (appointment.SpamCount == 3 && user_role != FSHRoles.Patient)
            {
                throw new Exception($"Warning: User had done reschedule 3 times");
            }

            if (appointment.DentistId != default)
            {
                bool r = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == appointment.DentistId && p.Date.Value.Month == request.AppointmentDate.Month && p.Status != WorkingStatus.Off);
                if (r)
                {
                    bool check = _workingCalendarService.CheckAvailableTimeSlotToReschedule(appointment.Id,
                    request.AppointmentDate,
                    request.StartTime,
                    request.StartTime.Add(request.Duration)).Result;
                    if (!check)
                    {
                        throw new Exception("The selected time slot overlaps with an existing appointment");
                    }
                }
            }
            appointment.AppointmentDate = request.AppointmentDate;
            appointment.StartTime = request.StartTime;
            appointment.Duration = request.Duration;
            appointment.LastModifiedBy = _currentUserService.GetUserId();
            appointment.LastModifiedOn = DateTime.Now;
            var cal = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
            cal.StartTime = request.StartTime;
            cal.EndTime = request.StartTime.Add(request.Duration);
            cal.Date = request.AppointmentDate;
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            if (appointment.DentistId != default)
            {
                _jobService.Schedule(() => SendAppointmentActionNotification(appointment.PatientId,
                appointment.DentistId,
                appointment.AppointmentDate,
                TypeRequest.Reschedule, cancellationToken), TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<bool> CheckAppointmentAvailableToReschedule(Guid appointmentId)
    {
        if (!CheckAppointmentExisting(appointmentId).Result)
        {
            return false;
        }
        else
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
            if (appointment.Status != AppointmentStatus.Confirmed)
            {
                return false;
            }
        }
        return true;
    }

    public async Task CancelAppointment(CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            string user_role = _currentUserService.GetRole();
            var appoint = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID && p.PatientId == request.UserID);
            if (appoint == null)
            {
                throw new Exception("Can not found appointment of this user.");
            }
            if (user_role == FSHRoles.Patient)
            {
                var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == request.UserID);
                if (patient.UserId != _currentUserService.GetUserId().ToString())
                {
                    throw new Exception("Only Patient can cancel their appointment");
                }
            }
            if (appoint.Status == AppointmentStatus.Done ||
                appoint.Status == AppointmentStatus.Failed ||
                appoint.Status == AppointmentStatus.Pending ||
                appoint.Status == AppointmentStatus.Cancelled)
            {
                throw new BadRequestException("Appointment can not be cancel. Check Status");
            }
            if (appoint.Status == AppointmentStatus.Confirmed)
            {
                var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);

                calendar.Status = CalendarStatus.Canceled;
                var payment = await _db.Payments.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);
                payment.Status = Domain.Payments.PaymentStatus.Canceled;
                if (appoint.DentistId != default)
                {
                    _jobService.Schedule(() => SendAppointmentActionNotification(appoint.PatientId,
                        appoint.DentistId,
                        appoint.AppointmentDate,
                        TypeRequest.Cancel, cancellationToken), TimeSpan.FromSeconds(5));
                }
            }
            else if (appoint.Status == AppointmentStatus.Come)
            {
                var query = await _db.TreatmentPlanProcedures
                    .Where(p => p.AppointmentID == request.AppointmentID && p.Status == Domain.Treatment.TreatmentPlanStatus.Active).OrderByDescending(p => p.StartDate).ToListAsync();
                foreach (var item in query)
                {
                    item.Status = Domain.Treatment.TreatmentPlanStatus.Cancelled;
                    var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.PlanID == item.Id);
                    calendar.Status = CalendarStatus.Canceled;
                    _jobService.Schedule(() => SendAppointmentActionNotification(appoint.PatientId,
                        appoint.DentistId,
                        calendar.Date.Value,
                        TypeRequest.Cancel, cancellationToken), TimeSpan.FromSeconds(5));
                }
            }
            else
            {
                throw new BadRequestException("The Appointment can not be cancel");
            }
            appoint.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task ScheduleAppointment(ScheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            string currentUserRole = _currentUserService.GetRole();
            bool isStaffOrAdmin = currentUserRole == FSHRoles.Admin || currentUserRole == FSHRoles.Staff;
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID, cancellationToken);
            if (currentUserRole == FSHRoles.Patient)
            {
                var pProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                if (pProfile.Id != appointment.PatientId)
                {
                    throw new Exception("Only Patient can reschedule their appointment");
                }
            }
            else if (!isStaffOrAdmin)
            {
                throw new UnauthorizedAccessException("Only Staff or Admin can access this function.");
            }

            if (appointment.Status != AppointmentStatus.Confirmed)
            {
                throw new Exception("Appointment is not in status to schedule.");
            }
            var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DoctorID);

            bool r = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == dprofile.Id && p.Date.Value.Month == appointment.AppointmentDate.Month && p.Status != WorkingStatus.Off);
            if (r)
            {
                bool check = _workingCalendarService.CheckAvailableTimeSlot(
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.StartTime.Add(appointment.Duration),
                request.DoctorID).Result;

                if (!check)
                {
                    throw new InvalidDataException("The selected time slot overlaps with an existing appointment or doctor do not work this day");
                }
            }

            var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);
            appointment.DentistId = dprofile.Id;
            calendar.DoctorId = dprofile.Id;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task SendAppointmentActionNotification(Guid patientID, Guid DoctorID, DateOnly AppointmentDate, TypeRequest type, CancellationToken cancellationToken)
    {
        try
        {

            var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.Id == DoctorID);
            var doctor = await _userManager.FindByIdAsync(dprofile.DoctorId);
            var pprofile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == patientID);
            var patient = await _userManager.FindByIdAsync(pprofile.UserId);

            switch (type)
            {
                case TypeRequest.Verify:
                    await _notificationService.SendNotificationToUser(dprofile.DoctorId,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"You has a meet with patient {patient.FirstName} {patient.LastName} in {AppointmentDate.ToString("dd-MM-yyyy")}",
                            Title = "Booking Schedule Notification",
                            Url = null,
                        }, null, cancellationToken);
                    break;
                case TypeRequest.Reschedule:
                    await _notificationService.SendNotificationToUser(doctor.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"Patient {patient.FirstName} {patient.LastName} was reschedule to {AppointmentDate.ToString("dd-MM-yyyy")}",
                            Title = "Reschedule Appointment Notification",
                            Url = null,
                        }, null, cancellationToken);
                    await _notificationService.SendNotificationToUser(patient.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"Reschedule to {AppointmentDate.ToString("dd-MM-yyyy")} successfully",
                            Title = "Reschedule Appointment Notification",
                            Url = null,
                        }, null, cancellationToken);
                    break;
                case TypeRequest.Cancel:
                    await _notificationService.SendNotificationToUser(doctor.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"Patient {patient.FirstName} {patient.LastName} was cancel the meeting in {AppointmentDate.ToString("dd-MM-yyyy")}",
                            Title = "Cancel Appointment Notification",
                            Url = null,
                        }, null, cancellationToken);
                    await _notificationService.SendNotificationToUser(patient.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"Cancel appointment in {AppointmentDate.ToString("dd-MM-yyyy")}",
                            Title = "Cancel Appointment Notification",
                            Url = null,
                        }, null, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task<AppointmentResponse> GetAppointmentByID(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var appointments = await _db.Appointments
                .Where(p => p.Id == id)
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                })
                .FirstOrDefaultAsync(cancellationToken);

            bool isFeedback = await _db.Feedbacks.AnyAsync(p => p.AppointmentId == id);
            var patient = await _userManager.FindByIdAsync(appointments.Patient.UserId);
            var result = new AppointmentResponse
            {
                PatientUserID = patient.Id,
                AppointmentId = appointments.Appointment.Id,
                PatientId = appointments.Appointment.PatientId,
                ServiceId = appointments.Appointment.ServiceId,
                AppointmentDate = appointments.Appointment.AppointmentDate,
                StartTime = appointments.Appointment.StartTime,
                Duration = appointments.Appointment.Duration,
                Status = appointments.Appointment.Status,
                Notes = appointments.Appointment.Notes,
                PatientPhone = patient.PhoneNumber != null ? patient.PhoneNumber : null,
                canFeedback = appointments.Appointment.canFeedback,
                isFeedback = isFeedback,
                PatientCode = appointments.Patient?.PatientCode,
                PatientName = $"{patient.FirstName} {patient.LastName}",
                ServiceName = appointments.Service?.ServiceName,
                ServicePrice = appointments.Service?.TotalPrice ?? 0,
                PaymentStatus = appointments.Payment is not null ? appointments.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
            };

            if (appointments.Appointment.DentistId != Guid.Empty)
            {
                var dentist = _db.DoctorProfiles.FirstOrDefault(p => p.Id == appointments.Appointment.DentistId);
                if (dentist != null)
                {
                    var d = await _userManager.FindByIdAsync(dentist.DoctorId);
                    result.DentistId = dentist.Id;
                    result.DentistName = $"{d.FirstName} {d.LastName}";
                }
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == dentist.Id && p.Date == appointments.Appointment.AppointmentDate && p.Status == WorkingStatus.Accept);

                if (calendar != null)
                {
                    if (calendar.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == calendar.RoomID);
                        result.RoomID = room.Id;
                        result.RoomName = room.RoomName;
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<TreatmentPlanResponse>> ToggleAppointment(Guid id, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var result = new List<TreatmentPlanResponse>();
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == id) ?? throw new KeyNotFoundException("Appointment Not Found.");
            if (appointment.AppointmentDate != DateOnly.FromDateTime(DateTime.Now))
            {
                throw new Exception("The date is not the date.");
            }
            if (appointment.Status == AppointmentStatus.Confirmed)
            {
                var groupService = await _db.ServiceProcedures
                .Where(p => p.ServiceId == appointment.ServiceId)
                .GroupBy(p => p.ServiceId)
                .Select(group => new
                {
                    Procedures = group.Select(p => p.ProcedureId).Distinct().ToList(),
                }).FirstOrDefaultAsync(cancellationToken);

                var payment = _db.Payments.FirstOrDefault(p => p.AppointmentId == id);

                var dprofile = _db.DoctorProfiles.FirstOrDefault(p => p.Id == appointment.DentistId);

                var doctor = _userManager.FindByIdAsync(dprofile.DoctorId!).Result;

                appointment.Status = AppointmentStatus.Come;

                appointment.ComeAt = DateTime.Now.TimeOfDay;

                foreach (var item in groupService.Procedures)
                {
                    var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item);
                    _db.PaymentDetails.Add(new Domain.Payments.PaymentDetail
                    {
                        ProcedureID = item!.Value,
                        PaymentID = payment.Id,
                        PaymentAmount = pro.Price,
                        PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                    });
                    var sp = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == appointment.ServiceId && p.ProcedureId == item);

                    var t = new Domain.Treatment.TreatmentPlanProcedures
                    {
                        ServiceProcedureId = sp.Id,
                        AppointmentID = id,
                        DoctorID = appointment.DentistId,
                        Status = Domain.Treatment.TreatmentPlanStatus.Pending,
                        Cost = pro.Price,
                        DiscountAmount = 0,
                        FinalCost = pro.Price,
                    };

                    if (sp.StepOrder == 1)
                    {
                        t.StartDate = appointment.AppointmentDate;
                        t.StartTime = appointment.StartTime;
                        t.Status = Domain.Treatment.TreatmentPlanStatus.Active;
                    }

                    var entry = _db.TreatmentPlanProcedures.Add(t).Entity;
                    if (sp.StepOrder == 1)
                    {
                        var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == id);
                        calendar.PlanID = entry.Id;
                        entry.Status = Domain.Treatment.TreatmentPlanStatus.Active;
                        entry.StartDate = appointment.AppointmentDate;
                        entry.StartTime = appointment.StartTime;
                    }
                    var r = new TreatmentPlanResponse
                    {
                        TreatmentPlanID = entry.Id,
                        ProcedureID = item.Value,
                        ProcedureName = pro.Name,
                        Price = pro.Price,
                        DoctorID = doctor.Id,
                        DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                        PlanCost = entry.FinalCost,
                        PlanDescription = null,
                        Step = sp.StepOrder,
                        Status = entry.Status,
                        hasPrescription = false,
                    };
                    if (sp.StepOrder == 1)
                    {
                        r.StartDate = appointment.AppointmentDate;
                    }
                    result.Add(r);
                }
                await _db.SaveChangesAsync(cancellationToken);
                result = result.OrderBy(p => p.Step).ToList();
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                throw new Exception("Can not verify appointment.");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message, ex);
        }
        return result;
    }

    public async Task<PaymentDetailResponse> GetRemainingAmountOfAppointment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var query = await _db.Payments
                .Where(p => p.AppointmentId == id)
                .Select(a => new
                {
                    Payment = a,
                    pProfile = _db.PatientProfiles.FirstOrDefault(p => p.Id == a.PatientProfileId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(p => p.Id == a.ServiceId),
                    Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList()
                })
                .FirstOrDefaultAsync();

            if (query.Payment.Status != PaymentStatus.Incomplete)
            {
                throw new Exception("The appointment have no any amount to pay.");
            }

            var patient = await _userManager.FindByIdAsync(query.pProfile.UserId);

            var response = new PaymentDetailResponse
            {
                PaymentResponse = new PaymentResponse
                {
                    AppointmentId = id,
                    ServiceId = query.Service.Id,
                    ServiceName = query.Service.ServiceName,
                    PaymentId = query.Payment.Id,
                    PatientProfileId = query.pProfile.Id,
                    PatientCode = query.pProfile.PatientCode,
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    DepositAmount = query.Payment.DepositAmount!.Value,
                    DepositDate = query.Payment.DepositAmount.Value == 0 ? query.Payment.DepositDate : default,
                    RemainingAmount = query.Payment.RemainingAmount!.Value,
                    TotalAmount = query.Payment.Amount!.Value,
                    Method = Domain.Payments.PaymentMethod.None,
                    Status = query.Payment.Status,
                },
                Details = new List<Application.Payments.PaymentDetail>()
            };

            foreach (var item in query.Detail)
            {
                var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item.ProcedureID);
                response.Details.Add(new Application.Payments.PaymentDetail
                {
                    ProcedureID = item.ProcedureID,
                    ProcedureName = pro.Name,
                    PaymentAmount = item.PaymentAmount,
                    PaymentStatus = item.PaymentStatus
                });
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task HandlePaymentRequest(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.IsPay && (request.Method == Domain.Payments.PaymentMethod.Cash))
            {
                var query = await _db.Payments
                    .Where(p => p.Id == request.PaymentID)
                    .Select(a => new
                    {
                        Payment = a,
                        Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList(),
                        Profile = _db.PatientProfiles.FirstOrDefault(p => p.Id == a.PatientProfileId)
                    })
                    .FirstOrDefaultAsync();

                if (query.Payment.RemainingAmount != request.Amount)
                {
                    throw new Exception("Warning: Amount is not equal");
                }
                query.Payment.RemainingAmount -= request.Amount;
                query.Payment.Status = Domain.Payments.PaymentStatus.Completed;
                query.Payment.FinalPaymentDate = DateOnly.FromDateTime(DateTime.Now);

                foreach (var item in query.Detail)
                {
                    item.PaymentStatus = Domain.Payments.PaymentStatus.Completed;
                }

                await _db.SaveChangesAsync(cancellationToken);
                var notification = new BasicNotification
                {
                    Message = "Thanh toán thành công toàn bộ dịch vụ!",
                    Label = BasicNotification.LabelType.Success,
                    LargeBody = "Cảm ơn bạn đã hoàn tất thanh toán cho toàn bộ dịch vụ và liệu trình khám. Khoản thanh toán của bạn đã được xác nhận thành công. Vui lòng kiểm tra email để nhận biên lai và các thông tin chi tiết về lịch hẹn và dịch vụ đã thanh toán.",
                    SummaryText = "Thanh toán thành công toàn bộ liệu trình khám và dịch vụ.",
                    Title = "Thanh toán thành công!",
                    Url = "/appointment",
                };

                var user = await _userManager.FindByIdAsync(query.Profile.UserId);
                await _notificationService.SendPaymentNotificationToUser(user.Id, notification, null, cancellationToken);
                await _notificationService.SendPaymentNotificationToUser(_currentUserService.GetUserId().ToString(), notification, null, cancellationToken);
            }
            else if (!request.IsPay && (request.Method == Domain.Payments.PaymentMethod.BankTransfer))
            {
                var query = await _db.Payments
                    .Where(p => p.Id == request.PaymentID)
                    .Select(a => new
                    {
                        Payment = a,
                        Patient = _db.PatientProfiles.FirstOrDefault(e => e.Id == a.PatientProfileId),
                        Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList(),
                    })
                    .FirstOrDefaultAsync();

                var result = new PayAppointmentRequest
                {
                    AppointmentId = request.AppointmentId,
                    PaymentID = request.PaymentID,
                    PatientCode = query.Patient.PatientCode,
                    Amount = query.Payment.RemainingAmount.Value,
                    IsVerify = false,
                    UserId = _currentUserService.GetUserId().ToString(),
                };
                await _cacheService.SetAsync(query.Patient.PatientCode, result, request.Time);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task DoPaymentForAppointment(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var query = await _db.Payments
                    .Where(p => p.Id == request.PaymentID)
                    .Select(a => new
                    {
                        Payment = a,
                        Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList(),
                        Patient = _db.PatientProfiles.FirstOrDefault(e => e.Id == a.PatientProfileId),
                    })
                    .FirstOrDefaultAsync();

            if (query.Payment.RemainingAmount != request.Amount)
            {
                throw new Exception("Warning: Amount is not equal");
            }

            query.Payment.Status = Domain.Payments.PaymentStatus.Completed;
            query.Payment.FinalPaymentDate = DateOnly.FromDateTime(DateTime.Now);
            query.Payment.RemainingAmount -= request.Amount;
            foreach (var item in query.Detail)
            {
                item.PaymentStatus = Domain.Payments.PaymentStatus.Completed;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _cacheService.Remove(request.PatientCode);

            var notification = new BasicNotification
            {
                Message = "Thanh toán thành công toàn bộ dịch vụ!",
                Label = BasicNotification.LabelType.Success,
                LargeBody = "Cảm ơn bạn đã hoàn tất thanh toán cho toàn bộ dịch vụ và liệu trình khám. Khoản thanh toán của bạn đã được xác nhận thành công. Vui lòng kiểm tra email để nhận biên lai và các thông tin chi tiết về lịch hẹn và dịch vụ đã thanh toán.",
                SummaryText = "Thanh toán thành công toàn bộ liệu trình khám và dịch vụ.",
                Title = "Thanh toán thành công!",
                Url = "/appointment",
            };

            var user = await _userManager.FindByIdAsync(query.Patient.UserId);
            await _notificationService.SendPaymentNotificationToUser(user.Id, notification, null, cancellationToken);
            await _notificationService.SendPaymentNotificationToUser(request.UserId, notification, null, cancellationToken);
            await DeleteRedisCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> CancelPayment(string code, CancellationToken cancellationToken)
    {
        try
        {
            var c = _cacheService.Get<PayAppointmentRequest>(code);
            if (c != null)
            {
                await _cacheService.RemoveAsync(code);
                return _t["Success"];
            }
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<AppointmentResponse>> GetNonDoctorAppointments(PaginationFilter filter, DateOnly date, TimeSpan time, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<AppointmentResponse>();
            var spec = new EntitiesByPaginationFilterSpec<Appointment>(filter);
            var appointmentsQuery = _db.Appointments
                .AsNoTracking().Where(p => p.DentistId == default);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.AppointmentDate == date);
            }
            if (time != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.StartTime == time);
            }

            int count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.OrderBy(p => p.AppointmentDate).WithSpecification(spec);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                var patient = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Patient.UserId).Result;
                result.Add(new AppointmentResponse
                {
                    PatientUserID = patient.Id,
                    AppointmentId = a.Appointment.Id,
                    PatientId = a.Appointment.PatientId,
                    ServiceId = a.Appointment.ServiceId,
                    AppointmentDate = a.Appointment.AppointmentDate,
                    StartTime = a.Appointment.StartTime,
                    Duration = a.Appointment.Duration,
                    Status = a.Appointment.Status,
                    Notes = a.Appointment.Notes,
                    PatientPhone = patient.PhoneNumber != null ? patient.PhoneNumber : null,
                    PatientCode = a.Patient?.PatientCode,
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    ServiceName = a.Service?.ServiceName,
                    ServicePrice = a.Service?.TotalPrice ?? 0,
                    PaymentStatus = a.Payment is not null ? a.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
                });
            }
            return new PaginationResponse<AppointmentResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> AddDoctorToAppointments(AddDoctorToAppointment request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (request.DoctorID == default || request.AppointmentID == default)
            {
                throw new Exception("All Information should be include.");
            }
            var doctor = await _db.DoctorProfiles
                .Where(p => p.Id == request.DoctorID)
                .FirstOrDefaultAsync();
            if (doctor == null)
            {
                throw new Exception("Doctor can not be found.");
            }
            var user = await _userManager.FindByIdAsync(doctor.DoctorId);
            if (!user.IsActive)
            {
                throw new Exception("Doctor has been deactive.");
            }
            var appoitment = await _db.Appointments
                .Where(p => p.Id == request.AppointmentID && p.Status == AppointmentStatus.Confirmed)
                .Select(c => new
                {
                    Appointment = c,
                    Calendar = _db.AppointmentCalendars.FirstOrDefault(p => p.AppointmentId == c.Id)
                })
                .FirstOrDefaultAsync();

            if (appoitment == null)
            {
                throw new Exception("Appointment can not be found or be cancel.");
            }
            if (appoitment.Appointment.Status != AppointmentStatus.Confirmed)
            {
                throw new Exception("Appointment is unavailable to reschedule.");
            }

            bool check = await _workingCalendarService.CheckAvailableTimeSlot(
                appoitment.Appointment.AppointmentDate,
                appoitment.Appointment.StartTime,
                appoitment.Appointment.StartTime.Add(appoitment.Appointment.Duration),
                request.DoctorID);
            if (!check)
            {
                throw new Exception("Doctor has a meeting in this time or not work today");
            }
            appoitment.Appointment.DentistId = request.DoctorID;
            var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);

            calendar.DoctorId = request.DoctorID;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _jobService.Schedule(
                () => SendAppointmentActionNotification(appoitment.Appointment.PatientId, request.DoctorID, appoitment.Appointment.AppointmentDate, TypeRequest.Verify, cancellationToken),
                TimeSpan.FromSeconds(5));
            return _t["Success"];
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetFollowUpAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<GetWorkingDetailResponse>();
            var spec = new EntitiesByPaginationFilterSpec<AppointmentCalendar>(filter);
            var appointmentsQuery = _db.AppointmentCalendars
                .AsNoTracking()
                .Where(p => p.Type == AppointmentType.FollowUp);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Date == date);
            }

            if (currentUser == FSHRoles.Dentist)
            {
                var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == _currentUserService.GetUserId().ToString());
                appointmentsQuery = appointmentsQuery.Where(p => p.DoctorId == dProfile.Id && p.Status == CalendarStatus.Checkin);
            }
            else if (currentUser == FSHRoles.Patient)
            {
                var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                appointmentsQuery = appointmentsQuery.Where(p => p.PatientId == patientProfile.Id);
            }
            if (currentUser == FSHRoles.Staff || currentUser == FSHRoles.Dentist)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Status == CalendarStatus.Booked);
            }

            int count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.OrderByDescending(p => p.Date).WithSpecification(spec);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DoctorId),
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    TreatmentPlan = _db.TreatmentPlanProcedures.FirstOrDefault(s => s.Id == appointment.PlanID),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                var doctor = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                var patient = await _userManager.FindByIdAsync(a.Patient.UserId);
                var sp = await _db.ServiceProcedures.Where(p => p.Id == a.TreatmentPlan.ServiceProcedureId)
                    .Select(s => new
                    {
                        Service = _db.Services.FirstOrDefault(p => p.Id == s.ServiceId),
                        Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                        Step = s.StepOrder
                    }).FirstOrDefaultAsync();
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == a.Doctor.Id && p.Date == a.Appointment.Date && p.Status == WorkingStatus.Accept);
                var r = new GetWorkingDetailResponse
                {
                    TreatmentID = a.TreatmentPlan.Id,
                    AppointmentId = a.Appointment.AppointmentId.Value,
                    AppointmentType = a.Appointment.Type,
                    CalendarID = a.Appointment.Id,
                    Date = a.Appointment.Date.Value,
                    DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                    DoctorProfileID = a.Appointment.DoctorId.Value,
                    EndTime = a.Appointment.EndTime.Value,
                    PatientPhone = patient.PhoneNumber != null ? patient.PhoneNumber : null,
                    Note = a.Appointment.Note,
                    PatientCode = a.Patient.PatientCode,
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    PatientProfileID = a.Patient.Id,
                    ProcedureID = sp.Procedure.Id,
                    ProcedureName = sp.Procedure.Name,
                    ServiceID = sp.Service.Id,
                    ServiceName = sp.Service.ServiceName,
                    StartTime = a.Appointment.StartTime.Value,
                    Status = a.Appointment.Status,
                    Step = sp.Step,
                };
                if (calendar != null)
                {
                    if (calendar.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == calendar.RoomID);
                        r.RoomID = room.Id;
                        r.RoomName = room.RoomName;
                    }
                }
                result.Add(r);
            }
            return new PaginationResponse<GetWorkingDetailResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetReExamAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<GetWorkingDetailResponse>();
            var spec = new EntitiesByPaginationFilterSpec<AppointmentCalendar>(filter);
            var appointmentsQuery = _db.AppointmentCalendars
                .AsNoTracking()
                .Where(p => p.Type == AppointmentType.ReExamination);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Date == date);
            }

            if (currentUser == FSHRoles.Dentist)
            {
                var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == _currentUserService.GetUserId().ToString());
                appointmentsQuery = appointmentsQuery.Where(p => p.DoctorId == dProfile.Id);
            }
            else if (currentUser == FSHRoles.Patient)
            {
                var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                appointmentsQuery = appointmentsQuery.Where(p => p.PatientId == patientProfile.Id);
            }
            if (currentUser == FSHRoles.Staff || currentUser == FSHRoles.Dentist)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Status == CalendarStatus.Booked);
            }

            int count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.OrderByDescending(p => p.Date).WithSpecification(spec);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DoctorId),
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                var doctor = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                var patient = await _userManager.FindByIdAsync(a.Patient.UserId);

                var r = new GetWorkingDetailResponse
                {
                    AppointmentId = a.Appointment.AppointmentId.Value,
                    AppointmentType = a.Appointment.Type,
                    CalendarID = a.Appointment.Id,
                    Date = a.Appointment.Date.Value,
                    DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                    PatientPhone = patient.PhoneNumber != null ? patient.PhoneNumber : null,
                    DoctorProfileID = a.Appointment.DoctorId.Value,
                    EndTime = a.Appointment.EndTime.Value,
                    Note = a.Appointment.Note,
                    PatientCode = a.Patient.PatientCode,
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    PatientProfileID = a.Patient.Id,
                    StartTime = a.Appointment.StartTime.Value,
                    Status = a.Appointment.Status,
                };

                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == a.Doctor.Id && p.Date == a.Appointment.Date && p.Status == WorkingStatus.Accept);

                if (calendar != null)
                {
                    if (calendar.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == calendar.RoomID);
                        r.RoomID = room.Id;
                        r.RoomName = room.RoomName;
                    }
                }

                //if (a.Appointment.PlanID != null)
                //{
                //    var plan = await _db.TreatmentPlanProcedures.FirstOrDefaultAsync(p => p.Id == a.Appointment.PlanID);
                //    var sp = await _db.ServiceProcedures.Where(p => p.Id == plan.ServiceProcedureId)
                //    .Select(s => new
                //    {
                //        Service = _db.Services.FirstOrDefault(p => p.Id == s.ServiceId),
                //        Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                //        Step = s.StepOrder
                //    }).FirstOrDefaultAsync();
                //    r.ServiceID = sp.Service.Id;
                //    r.ProcedureID = sp.Procedure.Id;
                //    r.ServiceName = sp.Service.ServiceName;
                //    r.ProcedureName = sp.Procedure.Name;
                //    r.Step = sp.Step;
                //}
                result.Add(r);
            }
            return new PaginationResponse<GetWorkingDetailResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> CreateReExamination(AddReExamination request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.DentistId == request.DoctorId && p.PatientId == request.PatientId && p.Id == request.AppointmentId && p.Status == AppointmentStatus.Done);
            if (appointment == null)
            {
                throw new Exception("Warning: Error when find appointment");
            }

            if (!await CheckAppointmentDateValid(request.Date.Value))
            {
                throw new Exception("Warning: The Day is not available");
            }

            var check = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == request.DoctorId && p.Date == request.Date && p.Status == WorkingStatus.Accept);
            if (check != null)
            {
                bool t = await _db.TimeWorkings.AnyAsync(c => c.CalendarID == check.Id && (
                    c.StartTime <= request.StartTime && c.EndTime >= request.EndTime
                ));
                if (!t)
                {
                    throw new Exception("Warning: The time line is not in doctor's time working");
                }
            }
            else
            {
                bool r = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == request.DoctorId && p.Date.Value.Month == request.Date.Value.Month && p.Status != WorkingStatus.Off);
                if (r)
                {
                    throw new Exception("Warning: You should choose the day you work!!!");
                }
            }

            _db.AppointmentCalendars.Add(new AppointmentCalendar
            {
                DoctorId = request.DoctorId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                PatientId = request.PatientId,
                Date = request.Date,
                AppointmentId = request.AppointmentId,
                Note = request.Note,
                Status = CalendarStatus.Booked,
                Type = AppointmentType.ReExamination,
            });
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<GetDoctorResponse>> GetAvailableDoctorAsync(GetAvailableDoctor request, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<GetDoctorResponse>();

            var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);

            if (service == null)
            {
                throw new Exception("Warning: Service Not Found");
            }
            if (request.Date == default)
            {
                throw new Exception("Warning: The Date should be available");
            }

            var doctors = await _db.DoctorProfiles.Where(p => p.TypeServiceID == service.TypeServiceID).ToListAsync();

            foreach (var doctor in doctors)
            {
                bool check = await _workingCalendarService.CheckAvailableTimeSlot(request.Date, request.StartTime, request.EndTime, doctor.DoctorId);

                if (check)
                {
                    var user = await _userManager.FindByIdAsync(doctor.DoctorId);
                    result.Add(new GetDoctorResponse
                    {
                        DoctorProfile = doctor,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        Gender = user.Gender,
                        Id = user.Id,
                        ImageUrl = user.ImageUrl,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        UserName = user.UserName,
                        isWorked = check
                    });
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> ToggleFollowAppointment(Guid id, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var calendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.Id == id) ?? throw new KeyNotFoundException("Calendar Not Found.");
            if (calendar.Date != DateOnly.FromDateTime(DateTime.Now))
            {
                throw new Exception("The date is not follow up date.");
            }
            if (calendar.Status != CalendarStatus.Booked)
            {
                throw new Exception("Can not verify the follow up appointment.");
            }
            calendar.Status = CalendarStatus.Checkin;
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message, ex);
        }
    }
    public Task DeleteRedisCode()
    {
        try
        {
            var key1a = _cacheService.Get<HashSet<string>>(APPOINTMENT);
            if (key1a != null)
            {
                foreach (string key in key1a)
                {
                    _cacheService.Remove(key);
                }
                _cacheService.Remove(APPOINTMENT);
            }
            var key2a = _cacheService.Get<HashSet<string>>(NON);
            if (key2a != null)
            {
                foreach (string key in key2a)
                {
                    _cacheService.Remove(key);
                }
                _cacheService.Remove(NON);
            }
            var key3a = _cacheService.Get<HashSet<string>>(FOLLOW);
            if (key3a != null)
            {
                foreach (string key in key3a)
                {
                    _cacheService.Remove(key);
                }
                _cacheService.Remove(FOLLOW);
            }
            var key4a = _cacheService.Get<HashSet<string>>(REEXAM);
            if (key4a != null)
            {
                foreach (string key in key4a)
                {
                    _cacheService.Remove(key);
                }
                _cacheService.Remove(REEXAM);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> RevertPayment(DefaultIdType id)
    {
        try
        {
            var query = await _db.Payments
                    .Where(p => p.AppointmentId == id)
                    .FirstOrDefaultAsync();

            query.Status = Domain.Payments.PaymentStatus.Incomplete;
            query.FinalPaymentDate = default;
            query.RemainingAmount = query.Amount - query.DepositAmount;
            query.Method = PaymentMethod.None;
            var detail = await _db.PaymentDetails
                    .Where(p => p.PaymentID == id)
                    .ToListAsync();

            foreach(var item in detail)
            {
                item.PaymentStatus = PaymentStatus.Incomplete;
            }

            await _db.SaveChangesAsync();
            return "Success";

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }
}
