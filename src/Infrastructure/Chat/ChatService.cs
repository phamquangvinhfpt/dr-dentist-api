using System.Security.Claims;
using DocumentFormat.OpenXml.InkML;
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
    private readonly IJobService _jobService;

    public ChatService(
        IHubContext<ChatHub> chatHubContext,
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager,
        IJobService jobService)
    {
        _chatHubContext = chatHubContext;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userManager = userManager;
        _jobService = jobService;
    }

    public async Task<List<ListUserDto>> GetListUserDtoAsync()
    {
        var senderIds = await _dbContext.PatientMessages
            .Where(pm => !pm.isStaffSender)
            .Select(pm => pm.SenderId)
            .Distinct()
            .ToListAsync();

        var lastMessages = new List<ListUserDto>();

        foreach (var senderId in senderIds)
        {
            var latestMessage = await _dbContext.PatientMessages
                .Where(pm => pm.SenderId == senderId && !pm.isStaffSender)
                .OrderByDescending(pm => pm.CreatedOn)
                .Select(pm => new ListUserDto
                {
                    Id = pm.Id,
                    SenderId = pm.SenderId ?? string.Empty,
                    LatestMessage = pm.Message ?? string.Empty,
                    IsRead = pm.IsRead,
                    CreatedOn = pm.CreatedOn
                })
                .FirstOrDefaultAsync();

            var user = await _userManager.FindByIdAsync(senderId);
            if (user != null)
            {
                latestMessage.SenderName = $"{user.FirstName} {user.LastName}" ?? "Unknown User";
                latestMessage.ImageUrl = user.ImageUrl;
            }

            lastMessages.Add(latestMessage);
        }

        return lastMessages;
    }

    public async Task<ListMessageDto> SendMessageAsync(string? receiverId, string message, CancellationToken cancellationToken)
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

        var sentMessage = new ListMessageDto
        {
            Id = patientMessage.Id,
            SenderId = patientMessage.SenderId,
            Message = patientMessage.Message,
            CreatedOn = patientMessage.CreatedOn
        };

        var user = await _userManager.FindByIdAsync(senderId);
        if (user != null)
        {
            sentMessage.SenderName = $"{user.FirstName} {user.LastName}" ?? "Unknown User";
            sentMessage.ImageUrl = user.ImageUrl;
        }

        return sentMessage;
    }

    public async Task<IEnumerable<ListMessageDto>> GetConversationAsync(string? conversionId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<PatientMessages>()
            .Where(pm => pm.SenderId == conversionId || pm.receiverId == conversionId);

        var messages = await query
            .OrderBy(pm => pm.CreatedOn)
            .Select(pm => new ListMessageDto
            {
                Id = pm.Id,
                SenderId = pm.SenderId,
                Message = pm.Message,
                CreatedOn = pm.CreatedOn
            })
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            var user = await _userManager.FindByIdAsync(message.SenderId);
            if (user != null)
            {
                message.SenderName = $"{user.FirstName} {user.LastName}" ?? "Unknown User";
                message.ImageUrl = user.ImageUrl;
            }
        }
        return messages;
    }

    public void SetCurrentUser(ClaimsPrincipal user)
    {
        _currentUser.SetCurrentUser(user);
    }
}