
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Payments;
using System.Text.Json;

namespace FSH.WebApi.Host.Controllers.Payments;

public class WebHookController : VersionedApiController
{
    private string accessToken = "2a82b9fac355fa8c192b28e7f47fbdb8";
    private readonly ILogger<WebHookController> _logger;
    private readonly IPaymentService _paymentService;

    public WebHookController(ILogger<WebHookController> logger, IPaymentService paymentService)
    {
        _logger = logger;
        _paymentService = paymentService;
    }

    [HttpPost]
    [AllowAnonymous]
    [OpenApiOperation("Webhook api for received transaction from bank!", "")]
    public IActionResult Post([FromBody] Transaction transactionInfo, CancellationToken cancellationToken)
    {
        try
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Unauthorized("Access Token không được cung cấp hoặc không hợp lệ.");
            }

            // Validate the Bearer token
            if (!authorizationHeader.ToString().StartsWith("Bearer "))
            {
                return Unauthorized("Access Token không hợp lệ.");
            }

            var bearerToken = authorizationHeader.ToString().Substring("Bearer ".Length).Trim();

            if (accessToken != bearerToken)
            {
                return Unauthorized("Chữ ký không hợp lệ.");
            }

            _paymentService.SaveTransactions(transactionInfo.data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing transaction data");
            return StatusCode(500, "Error while processing transaction data");
        }

        // If valid, process the received data
        var response = new { status = true, message = "Dữ liệu đã được lưu trữ." };
        _logger.LogInformation("Received transaction data: {0}", JsonSerializer.Serialize(transactionInfo));
        return Ok(response);
    }
}