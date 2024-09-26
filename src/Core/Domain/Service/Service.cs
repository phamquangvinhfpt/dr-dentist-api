namespace FSH.WebApi.Domain.Service;

public class Service : AuditableEntity, IAggregateRoot
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceDescription { get; set; } = string.Empty;
    public ICollection<ServiceProcedures> ServiceProcedures { get; set; } = new List<ServiceProcedures>();

    public Service()
    {
    }

    public Service(string serviceName, string serviceDescription)
    {
        ServiceName = serviceName;
        ServiceDescription = serviceDescription;
    }
}