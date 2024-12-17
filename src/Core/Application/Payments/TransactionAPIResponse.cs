using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace FSH.WebApi.Application.Payments;
// public class TransactionAPIResponse
// {
//     public bool Status { get; set; }
//     public string Message { get; set; }
//     public List<TransactionDto> Transactions { get; set; } = new();
// }

// public class TransactionDto
// {
//     public string TransactionID { get; set; }
//     public decimal Amount { get; set; }
//     public string Description { get; set; }
//     public string TransactionDate { get; set; }
//     public string Type { get; set; }
// }

public class TransactionAPIResponse
{
    public int Error { get; set; }
    public List<TransactionDto> data { get; set; }
}

public class TransactionDto
{
    public int Id { get; set; }
    public string Tid { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public decimal cusum_balance { get; set; }
    [JsonConverter(typeof(CustomDateTimeConverter))]
    public DateTime When { get; set; }
    public string bank_sub_acc_id { get; set; }
    public string SubAccId { get; set; }
    public string BankName { get; set; }
    public string BankAbbreviation { get; set; }
    public string? VirtualAccount { get; set; }
    public string? VirtualAccountName { get; set; }
    public string CorresponsiveName { get; set; }
    public string CorresponsiveAccount { get; set; }
    public string CorresponsiveBankId { get; set; }
    public string CorresponsiveBankName { get; set; }
}

public class CustomDateTimeConverter : JsonConverter<DateTime>
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? dateString = reader.GetString();

        if (string.IsNullOrEmpty(dateString))
        {
            return default;
        }

        return DateTime.ParseExact(dateString, DateFormat, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}