using FSH.WebApi.Shared.Multitenancy;

namespace FSH.WebApi.Infrastructure.Payments;
public class TransactionsUtils
{
    public static void CallAPIChecking(string url)
    {
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("tenant", MultitenancyConstants.Root.Id);
        var response = client.GetAsync(url);
    }
}
