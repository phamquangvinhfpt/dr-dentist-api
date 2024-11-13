using FSH.WebApi.Application.Payments;

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
}