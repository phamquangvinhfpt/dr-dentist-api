using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.CustomerServices;

namespace FSH.WebApi.Infrastructure.Chat;
public interface IChatService : ITransientService
{
    // Task<PatientMessages> SendMessageAsync(string senderId, string receiverId, string message, bool isStaffSender, CancellationToken cancellationToken);
    // Task<IEnumerable<PatientMessages>> GetConversationAsync(string? conversionId, CancellationToken cancellationToken);
    // Task<int> GetUnreadMessageCountAsync(string patientId, CancellationToken cancellationToken);
    // Task MarkMessagesAsReadAsync(string patientId, string staffId, CancellationToken cancellationToken);
}