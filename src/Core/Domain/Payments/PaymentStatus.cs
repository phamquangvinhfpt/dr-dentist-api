namespace FSH.WebApi.Domain.Payments;

public enum PaymentStatus
{
    Waiting,
    Incomplete,
    Completed,
    Canceled,
    Failed
}