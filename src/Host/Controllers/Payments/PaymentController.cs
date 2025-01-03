﻿using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Payments;

namespace FSH.WebApi.Host.Controllers.Payment;
public class PaymentController : VersionedApiController
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // [HttpGet("check-new-transactions")]
    // [AllowAnonymous]
    // [OpenApiOperation("Create a job to check new transactions.", "")]
    // public Task CreateJobAsync(CancellationToken cancellationToken)
    // {
    //     return _paymentService.CheckNewTransactions(cancellationToken);
    // }

    // [HttpGet("check-transactions")]
    // [TenantIdHeader]
    // [OpenApiOperation("Check patient transactions is successful or not.", "")]
    // public async Task CheckTransactionsAsync(CancellationToken cancellationToken)
    // {
    //     await _paymentService.CheckTransactionsAsync(cancellationToken);
    // }

    [HttpPost("get-all")]
    [OpenApiOperation("Get All payment", "")]
    public Task<PaginationResponse<PaymentResponse>> GetAllPayment(PaginationFilter filter, DateOnly Sdate, DateOnly EDate, CancellationToken cancellationToken)
    {
        return _paymentService.GetALlPayment(filter, Sdate, EDate, cancellationToken);
    }

    [HttpGet("get/{id}")]
    [OpenApiOperation("Get payment detail", "")]
    public Task<PaymentDetailResponse> GetDetailPayment(Guid id, CancellationToken cancellationToken)
    {
        return _paymentService.GetPaymentDetail(id, cancellationToken);
    }

    [HttpPost("transaction/get-all")]
    [OpenApiOperation("Get transactions", "")]
    public Task<PaginationResponse<Transaction>> GetTransactions(PaginationFilter filter, CancellationToken cancellationToken)
    {
        return _paymentService.GetAllTransactions(filter, cancellationToken);
    }

    // [HttpPost("transaction/seed")]
    // [OpenApiOperation("Seed a transaction", "")]
    // [AllowAnonymous]
    // [TenantIdHeader]
    // public Task SeedTransaction(List<TransactionDto> list, CancellationToken cancellationToken)
    // {
    //     return _paymentService.SeedTransactions(list, cancellationToken);
    // }


    [HttpPost("export-payment")]
    [OpenApiOperation("Export payment history logs.", "")]
    [MustHavePermission(FSHAction.Export, FSHResource.Files)]
    public async Task<FileResult> ExportPaymentAsync(ExportPaymentRequest request)
    {
        var stream = await _paymentService.ExportPaymentAsync(request);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"payment_export.xlsx");
    }


    [HttpPost("webhook")]
    [OpenApiOperation("Post webhook", "")]
    [AllowAnonymous]
    [TenantIdHeader]
    public async Task<IActionResult> GetTransactionWebhook(
        [FromBody] TransactionAPIResponse data,
        CancellationToken cancellationToken)
    {
        if (data == null)
        {
            return BadRequest("Invalid payload");
        }

        try
        {
            await _paymentService.GetTransactionFromWebhook(data, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred processing the webhook");
        }
    }
}