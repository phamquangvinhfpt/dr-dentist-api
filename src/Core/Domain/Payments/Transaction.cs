namespace FSH.WebApi.Domain.Payments;

public class Transaction : BaseEntity, IAggregateRoot
{
    public string TransactionID { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public DateOnly TransactionDate { get; set; }
    public TransactionType Type { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum TransactionType
{
    OUT,
    IN
}