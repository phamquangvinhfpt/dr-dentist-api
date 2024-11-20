using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Payments;

namespace FSH.WebApi.Host.Controllers.Payment;
public class PaymentController : VersionedApiController
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("check-new-transactions")]
    [AllowAnonymous]
    [OpenApiOperation("Create a job to check new transactions.", "")]
    public Task CreateJobAsync(CancellationToken cancellationToken)
    {
        return _paymentService.CheckNewTransactions(cancellationToken);
    }

    // [HttpGet("check-transactions")]
    // [TenantIdHeader]
    // [OpenApiOperation("Check patient transactions is successful or not.", "")]
    // public async Task CheckTransactionsAsync(CancellationToken cancellationToken)
    // {
    //     await _paymentService.CheckTransactionsAsync(cancellationToken);
    // }

    [HttpGet("get-all")]
    [OpenApiOperation("Get All payment", "")]
    public Task<PaginationResponse<PaymentResponse>> GetAllPayment(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
    {
        return _paymentService.GetALlPayment(filter, date, cancellationToken);
    }

    [HttpGet("get/{id}")]
    [OpenApiOperation("Get payment detail", "")]
    public Task<PaymentDetailResponse> GetDetailPayment(Guid id, CancellationToken cancellationToken)
    {
        return _paymentService.GetPaymentDetail(id, cancellationToken);
    }
}