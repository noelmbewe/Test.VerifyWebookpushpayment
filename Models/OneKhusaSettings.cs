namespace Test.VerifyWebook.Models;

public class OneKhusaSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string OrganisationId { get; set; } = string.Empty;
    public int MerchantAccountNumber { get; set; }
    public string WebhookSecret { get; set; } = string.Empty;
}
