
using FSH.WebApi.Domain.Payments;
using System.Threading;

namespace FSH.WebApi.Application.Payments;
public interface IPaymentService : ITransientService
{
    public Task CheckNewTransactions(CancellationToken cancellationToken);
}