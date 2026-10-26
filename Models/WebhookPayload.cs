using System.Text.Json;
using System.Text.Json.Serialization;

namespace Test.VerifyWebook.Models;

public class WebhookPayload
{
    [JsonPropertyName("connectorId")]
    public long ConnectorId { get; set; }

    [JsonPropertyName("sourceAccountNumber")]
    public string SourceAccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("sourceAccountName")]
    public string SourceAccountName { get; set; } = string.Empty;

    [JsonPropertyName("sourceInstitution")]
    public string SourceInstitution { get; set; } = string.Empty;

    [JsonPropertyName("sourceReferenceNumber")]
    public string SourceReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("merchantReferenceNumber")]
    public string MerchantReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("beneficiaryAccountNumber")]
    public string BeneficiaryAccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("transactionReferenceNumber")]
    public string TransactionReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("transactionDescription")]
    public string TransactionDescription { get; set; } = string.Empty;

    [JsonPropertyName("transactionAmount")]
    public decimal TransactionAmount { get; set; }

    [JsonPropertyName("transactionFee")]
    public decimal TransactionFee { get; set; }

    [JsonPropertyName("transactionDate")]
    public DateTime TransactionDate { get; set; }

    [JsonPropertyName("transactionStatusCode")]
    public string TransactionStatusCode { get; set; } = string.Empty;

    [JsonPropertyName("transactionCode")]
    public string TransactionCode { get; set; } = string.Empty;

    [JsonPropertyName("responseCode")]
    public string ResponseCode { get; set; } = string.Empty;

    [JsonPropertyName("metaData")]
    public JsonElement? MetaDataRaw { get; set; }

    [JsonIgnore]
    public WebhookMetaData? MetaData { get; set; }

    [JsonIgnore]
    public string? MetaDataRawJson { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public void Normalize()
    {
        var raw = MetaDataRaw;
        if (!raw.HasValue || raw.Value.ValueKind != JsonValueKind.Object)
        {
            if (ExtensionData != null)
            {
                if (ExtensionData.TryGetValue("metadata", out var altLower) && altLower.ValueKind == JsonValueKind.Object)
                    raw = altLower;
                else if (ExtensionData.TryGetValue("meta_data", out var altSnake) && altSnake.ValueKind == JsonValueKind.Object)
                    raw = altSnake;
            }
        }

        if (raw.HasValue && raw.Value.ValueKind == JsonValueKind.Object)
        {
            MetaDataRawJson = raw.Value.GetRawText();
            MetaData = JsonSerializer.Deserialize<WebhookMetaData>(MetaDataRawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}

public class WebhookMetaData
{
    [JsonPropertyName("timedAccountNumber")]
    public string TimedAccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("sourceDescription")]
    public string SourceDescription { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
