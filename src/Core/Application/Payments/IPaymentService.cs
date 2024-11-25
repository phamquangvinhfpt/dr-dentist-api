
using FSH.WebApi.Domain.Payments;
using System.Threading;

namespace FSH.WebApi.Application.Payments;
public interface IPaymentService : ITransientService
{
    public Task CheckNewTransactions(CancellationToken cancellationToken);
    public Task CheckTransactionsAsync(CancellationToken cancellationToken);
    public Task<bool> CheckPaymentExisting(Guid id);
    Task<PaginationResponse<PaymentResponse>> GetALlPayment(PaginationFilter filter, DateOnly date, DateOnly eDate, CancellationToken cancellationToken);
    Task<PaymentDetailResponse> GetPaymentDetail(Guid id, CancellationToken cancellationToken);
    Task<PaginationResponse<Transaction>> GetAllTransactions(PaginationFilter filter, CancellationToken cancellationToken);
}