using System.Security.Claims;
using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.CustomerServices;

namespace FSH.WebApi.Infrastructure.Chat;
public interface IChatService : ITransientService
{
    Task<List<ListUserDto>> GetListUserDtoAsync();
    Task<ListMessageDto> SendMessageAsync(SendMessageDto send, CancellationToken cancellationToken);
    void SetCurrentUser(ClaimsPrincipal user);
    Task<IEnumerable<ListMessageDto>> GetConversationAsync(string? conversionId, CancellationToken cancellationToken);
}