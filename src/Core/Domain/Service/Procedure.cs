using FSH.WebApi.Domain.Payments;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Service;

public class Procedure : AuditableEntity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }

    //Navigation
    [JsonIgnore]
    public List<PaymentDetail> PaymentDetails { get; set; }
    public Procedure()
    {
    }

    public Procedure(string name, string description, double price)
    {
        Name = name;
        Description = description;
        Price = price;
    }
}
