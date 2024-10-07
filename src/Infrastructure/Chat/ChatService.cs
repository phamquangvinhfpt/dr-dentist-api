using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FSH.WebApi.Infrastructure.Chat;
public class ChatService : IChatService
{
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatService(
        IHubContext<ChatHub> chatHubContext,
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager)
    {
        _chatHubContext = chatHubContext;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task<List<ListMessageDto>> GetListMessageDtoAsync()
    {
        // get all messages
        var messages = await _dbContext.Set<PatientMessages>()
            .Where(pm => pm.isStaffSender == false)
            .Select(g => new ListMessageDto
            {
                Id = g.Id,
                SenderId = g.SenderId,
                LatestMessage = g.Message,
                IsRead = g.IsRead,
                CreatedOn = g.CreatedOn
            })
            .ToListAsync();

        // get image url
        foreach (var message in messages)
        {
            var user = await _userManager.FindByIdAsync(message.SenderId);
            message.ImageUrl = user?.ImageUrl;
        }
    }

    public async Task<PatientMessages> SendMessageAsync(string? receiverId, string message, CancellationToken cancellationToken)
    {
        string senderId = _currentUser.GetUserId().ToString();
        bool isStaffSender = _currentUser.IsInRole(FSHRoles.Staff);
        var patientMessage = new PatientMessages
        {
            SenderId = senderId,
            receiverId = isStaffSender ? receiverId : null, // Staff can only send messages to patients
            Message = message,
            isStaffSender = isStaffSender ? true : false,
            IsRead = false
        };

        _dbContext.Set<PatientMessages>().Add(patientMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return patientMessage;
    }

    public async Task<IEnumerable<PatientMessages>> GetConversationAsync(string? conversionId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<PatientMessages>()
            .Where(pm => pm.PatientId == conversionId);

        return await query
            .OrderByDescending(pm => pm.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadMessageCountAsync(string patientId, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<PatientMessages>()
            .CountAsync(pm => pm.PatientId == patientId && !pm.IsRead, cancellationToken);
    }

    public async Task MarkMessagesAsReadAsync(string patientId, string staffId, CancellationToken cancellationToken)
    {
        var unreadMessages = await _dbContext.Set<PatientMessages>()
            .Where(pm => pm.PatientId == patientId && pm.StaffId == staffId && !pm.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ListMessageDto>> GetListMessageDtosAsync(string senderId)
    {
        var query = _dbContext.Set<PatientMessages>()
            .Where(pm => pm.PatientId == senderId || pm.StaffId == senderId)
            .GroupBy(pm => pm.PatientId)
            .Select(g => new ListMessageDto
            {
                Id = g.Max(pm => pm.Id),
                SenderId = g.Key,
                LatestMessage = g.Max(pm => pm.Message),
                IsRead = g.All(pm => pm.IsRead),
                ImageUrl = g.Max(pm => pm.StaffId == senderId ? pm.Patient.ImageUrl : pm.Staff.ImageUrl),
                CreatedOn = g.Max(pm => pm.CreatedOn)
            });

        return await query
            .OrderByDescending(pm => pm.CreatedOn)
            .ToListAsync();
    }

    private string GetPatientGroupName(string patientId) => $"Patient-{patientId}";
}