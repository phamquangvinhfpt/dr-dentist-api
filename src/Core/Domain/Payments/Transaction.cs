namespace FSH.WebApi.Domain.Payments;

public class Transaction
{
    public bool status { get; set; }
    public List<TransactionInfo> data { get; set; }
}

public class TransactionInfo : BaseEntity, IAggregateRoot
{
    public int Id { get; set; }
    public string Type { get; set; }
    public string TransactionId { get; set; }
    public string Amount { get; set; }
    public string Description { get; set; }
    public string Date { get; set; }
    public string Bank { get; set; }
}