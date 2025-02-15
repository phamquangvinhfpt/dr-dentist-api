using System.Security.Claims;
using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Common.FileStorage;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Notifications;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.Chat;
public class ChatService : IChatService
{
    private readonly IHubContext<NotificationHub> _chatHubContext;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IHubContext<NotificationHub> chatHubContext,
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        ILogger<ChatService> logger,
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorageService)
    {
        _chatHubContext = chatHubContext;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
        _userManager = userManager;
        _fileStorageService = fileStorageService;
    }

    public async Task<List<ListUserDto>> GetListUserDtoAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var adminUser = await _userManager.GetUsersInRoleAsync(FSHRoles.Admin);
        var staffUser = await _userManager.GetUsersInRoleAsync(FSHRoles.Staff);
        var doctorUser = await _userManager.GetUsersInRoleAsync(FSHRoles.Dentist);
        var patientUser = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
        string currentUser = _currentUser.GetUserId().ToString();
        var current_user = await _userManager.FindByIdAsync(currentUser);

        // users = users.Where(u => u.Id != currentUser && !adminUser.Contains(u)).ToList();
        if (adminUser.Contains(current_user))
        {
            users = users.Where(u => (doctorUser.Contains(u) || staffUser.Contains(u))
                                    && u.Id != currentUser).ToList();
        }
        else if (staffUser.Contains(current_user))
        {
            users = users.Where(u => (doctorUser.Contains(u) || patientUser.Contains(u) || adminUser.Contains(u))
                                    && u.Id != currentUser).ToList();
        }
        else if (doctorUser.Contains(current_user))
        {
            users = users.Where(u => (staffUser.Contains(u) || patientUser.Contains(u) || adminUser.Contains(u))
                                    && u.Id != currentUser).ToList();
        }
        else if (patientUser.Contains(current_user))
        {
            users = users.Where(u => (doctorUser.Contains(u) || staffUser.Contains(u))
                                    && u.Id != currentUser).ToList();
        }

        var senderIds = await _dbContext.PatientMessages
            .Select(pm => pm.SenderId)
            .Distinct()
            .ToListAsync();

        var latestMessages = new List<ListUserDto>();

        foreach (var user in users)
        {
            var latestMessage = await _dbContext.PatientMessages
                .Where(pm => pm.SenderId == user.Id && pm.ReceiverId == currentUser)
                .OrderByDescending(pm => pm.CreatedOn)
                .Select(pm => new ListUserDto
                {
                    Id = pm.Id,
                    SenderId = pm.SenderId ?? user.Id,
                    LatestMessage = pm.Message ?? string.Empty,
                    IsRead = pm.IsRead,
                    CreatedOn = pm.CreatedOn,
                    SenderName = $"{user.FirstName} {user.LastName}",
                    ImageUrl = user.ImageUrl,
                })
                .FirstOrDefaultAsync();

            if (latestMessage == null)
            {
                latestMessage = new ListUserDto
                {
                    SenderId = user.Id,
                    SenderName = $"{user.FirstName} {user.LastName}",
                    ImageUrl = user.ImageUrl,
                    LatestMessage = string.Empty,
                };
            }

            latestMessages.Add(latestMessage);
        }

        return latestMessages.OrderByDescending(lm => lm.CreatedOn).ToList();
    }

    public async Task<ListMessageDto> SendMessageAsync(SendMessageDto send, CancellationToken cancellationToken)
    {
        try
        {
            string senderId = _currentUser.GetUserId().ToString();
            var patientMessage = new PatientMessages
            {
                SenderId = senderId,
                ReceiverId = send.ReceiverId ?? string.Empty,
                Message = send.Message,
                IsRead = false
            };

            if (send.Images != null)
            {
                patientMessage.Images = await _fileStorageService.SaveFilesAsync(send.Images, cancellationToken);
            }

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

            await _chatHubContext.Clients.User(send.ReceiverId).SendAsync("ReceiveMessage", sentMessage);
            await _chatHubContext.Clients.User(senderId).SendAsync("ReceiveMessage", sentMessage);

            return sentMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            throw new Exception("Error sending message", ex);
        }
    }

    public async Task<IEnumerable<ListMessageDto>> GetConversationAsync(string? conversionId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<PatientMessages>()
            .Where(pm => pm.ReceiverId == conversionId && pm.SenderId == _currentUser.GetUserId().ToString() ||
                         pm.SenderId == conversionId && pm.ReceiverId == _currentUser.GetUserId().ToString());

        var messages = await query
            .OrderBy(pm => pm.CreatedOn)
            .Select(pm => new ListMessageDto
            {
                Id = pm.Id,
                SenderId = pm.SenderId,
                Message = pm.Message,
                ImagesUrl = pm.Images,
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