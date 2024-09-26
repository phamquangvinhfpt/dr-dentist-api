namespace FSH.WebApi.Infrastructure.Persistence.Configuration;

internal static class SchemaNames
{
    // TODO: figure out how to capitalize these only for Oracle
    public static string Auditing = nameof(Auditing); // "AUDITING";
    public static string Identity = nameof(Identity); // "IDENTITY";
    public static string Catalog = nameof(Catalog); // "CATALOG";
    public static string MultiTenancy = nameof(MultiTenancy); // "MULTITENANCY";
    public static string Notification = nameof(Notification); // "NOTIFICATION";
    public static string Service = nameof(Service); // "SERVICE";
    public static string Treatment = nameof(Treatment); // "TREATMENT_PLAN";
    public static string Payment = nameof(Payment); // "PAYMENT";
    public static string CustomerService = nameof(CustomerService); // "CUSTOMER_SERVICE";
}