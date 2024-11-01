using FSH.WebApi.Domain.Payments;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Service;

public class Service : AuditableEntity, IAggregateRoot
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceDescription { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public double TotalPrice { get; set; } = 0;

    [JsonIgnore]
    public ICollection<ServiceProcedures> ServiceProcedures { get; set; } = new List<ServiceProcedures>();
    [JsonIgnore]
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public Service()
    {
    }

    public Service(string serviceName, string serviceDescription, bool isActive, double totalPrice)
    {
        ServiceName = serviceName;
        ServiceDescription = serviceDescription;
        IsActive = isActive;
        TotalPrice = totalPrice;
    }
}