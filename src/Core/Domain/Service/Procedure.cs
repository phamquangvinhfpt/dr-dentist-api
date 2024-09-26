namespace FSH.WebApi.Domain.Service;

public class Procedure : AuditableEntity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public Procedure()
    {
    }

    public Procedure(string name, string description, decimal price)
    {
        Name = name;
        Description = description;
        Price = price;
    }
}
