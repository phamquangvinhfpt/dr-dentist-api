
using FSH.WebApi.Domain.Payments;
using System.Threading;

namespace FSH.WebApi.Application.Payments;
public interface IPaymentService : ITransientService
{
    public Task CheckNewTransactions(CancellationToken cancellationToken);
    public Task CheckTransactionsAsync(CancellationToken cancellationToken);
    public Task<bool> CheckPaymentExisting(Guid id);
}