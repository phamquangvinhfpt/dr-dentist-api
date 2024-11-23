using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Infrastructure.CustomerInformations;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Feedbacks;
internal class FeedbackService : IFeedbackService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<FeedbackService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(ApplicationDbContext db, IStringLocalizer<FeedbackService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<FeedbackService> logger)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<string> CreateFeedback(CreateFeedbackRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID);
            if (appointment == null) {
                throw new InvalidOperationException("Error when found appointment.");
            }

            if (!appointment.canFeedback) {
                throw new InvalidOperationException("Can not feedback when you have not done treatment plan.");
            }

            var feedback = await _db.Feedbacks.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);

            if (feedback != null) {
                throw new InvalidOperationException("You had done feedback for this treatment.");
            }

            _db.Feedbacks.Add(new Domain.CustomerServices.Feedback
            {
                AppointmentId = request.AppointmentID,
                PatientProfileId = appointment.PatientId,
                DoctorProfileId = appointment.DentistId,
                Message = request.Message,
                Rating = request.Rating,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex) {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> DeleteFeedback(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var feedback = await _db.Feedbacks.FirstOrDefaultAsync(p => p.Id == id);

            if (feedback == null)
            {
                throw new InvalidOperationException("Error when found feedback.");
            }

            _db.Feedbacks.Remove(feedback);
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> UpdateFeedback(CreateFeedbackRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID);
            if (appointment == null)
            {
                throw new InvalidOperationException("Error when found appointment.");
            }

            if (!appointment.canFeedback)
            {
                throw new InvalidOperationException("Can not feedback when you have not done treatment plan.");
            }

            var feedback = await _db.Feedbacks.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);

            if (feedback == null)
            {
                throw new InvalidOperationException("Error when found feedback.");
            }

            feedback.Message = request.Message;
            feedback.Rating = request.Rating;

            appointment.canFeedback = false;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }
}
