using System.Text.Json.Serialization;

namespace Test.VerifyWebook.Models;

public class GetAccessTokenRequest
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("apiSecret")]
    public string ApiSecret { get; set; } = string.Empty;

    [JsonPropertyName("organisationId")]
    public string OrganisationId { get; set; } = string.Empty;

    [JsonPropertyName("merchantAccountNumber")]
    public int MerchantAccountNumber { get; set; }
}
