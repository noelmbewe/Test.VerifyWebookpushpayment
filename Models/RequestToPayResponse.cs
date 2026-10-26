using System.Text.Json.Serialization;

namespace Test.VerifyWebook.Models;

public class RequestToPayResponse
{
    [JsonPropertyName("merchantAccountNumber")]
    public int MerchantAccountNumber { get; set; }

    [JsonPropertyName("timedAccountNumber")]
    public string TimedAccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("expiryDate")]
    public DateTime ExpiryDate { get; set; }

    [JsonPropertyName("expiryInMinutes")]
    public int ExpiryInMinutes { get; set; }
}
