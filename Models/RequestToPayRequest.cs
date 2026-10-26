using System.Text.Json.Serialization;

namespace Test.VerifyWebook.Models;

public class RequestToPayRequest
{
    [JsonPropertyName("merchantAccountNumber")]
    public int MerchantAccountNumber { get; set; }

    [JsonPropertyName("transactionAmount")]
    public decimal TransactionAmount { get; set; }

    [JsonPropertyName("transactionDescription")]
    public string TransactionDescription { get; set; } = string.Empty;

    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("capturedBy")]
    public string CapturedBy { get; set; } = string.Empty;
}
