using System.Text.Json.Serialization;

namespace Test.VerifyWebook.Models;

public class GetAccessTokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresOn")]
    public DateTime ExpiresOn { get; set; }

    [JsonPropertyName("expiryInMinutes")]
    public int ExpiryInMinutes { get; set; }
}
