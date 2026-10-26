using System.Text.Json.Serialization;

namespace Test.VerifyWebook.Models;

public class PushPaymentRequest
{
    [JsonPropertyName("merchantAccountNumber")]
    public int MerchantAccountNumber { get; set; }

    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }

    [JsonPropertyName("payerAccountNumber")]
    public string PayerAccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("sourceReferenceNumber")]
    public string SourceReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("transactionDescription")]
    public string TransactionDescription { get; set; } = string.Empty;

    [JsonPropertyName("transactionAmount")]
    public decimal TransactionAmount { get; set; }

    [JsonPropertyName("capturedBy")]
    public string CapturedBy { get; set; } = string.Empty;
}
