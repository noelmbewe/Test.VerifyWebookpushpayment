using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Test.VerifyWebook.Models;

namespace Test.VerifyWebook.Services;

public class WebhookVerifier
{
    private readonly OneKhusaSettings _settings;
    private readonly ILogger<WebhookVerifier> _logger;

    public WebhookVerifier(IOptions<OneKhusaSettings> settings, ILogger<WebhookVerifier> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public static string ComputeSignature(string payload, string secret)
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA512(secretBytes);
        byte[] hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public WebhookVerificationResult VerifySignature(string payloadBody, byte[] rawBodyBytes, string? signatureHeader)
    {
        var result = new WebhookVerificationResult
        {
            SignatureProvided = signatureHeader ?? "",
            PayloadLength = payloadBody.Length,
            Timestamp = DateTime.UtcNow
        };

        if (string.IsNullOrEmpty(signatureHeader))
        {
            result.Verified = false;
            result.Reason = "Signature header (X-OneKhusa-Webhook-Signature) is missing";
            _logger.LogWarning("Webhook signature header is missing");
            return result;
        }

        if (string.IsNullOrEmpty(_settings.WebhookSecret))
        {
            result.Verified = true;
            result.Reason = "WebhookSecret not configured - verification skipped";
            result.ComputedSignature = "N/A";
            _logger.LogWarning("Webhook secret is not configured - skipping verification");
            return result;
        }

        var secret = _settings.WebhookSecret;

        var sigFromDocs = ComputeSignature(payloadBody, secret);

        var rawString = Encoding.UTF8.GetString(rawBodyBytes);
        var sigFromRawBytes = ComputeSignature(rawString, secret);

        var sigFromRawDirect = ComputeDirectHmacSha512(secret, rawBodyBytes);

        _logger.LogInformation("BODY: {Len} chars, {ByteLen} bytes, rawBytes={RawLen} bytes", payloadBody.Length, Encoding.UTF8.GetBytes(payloadBody).Length, rawBodyBytes.Length);

        _logger.LogInformation("Received sig : {Sig}", signatureHeader);
        _logger.LogInformation("Computed docs : {Sig}", sigFromDocs);
        _logger.LogInformation("Computed rawS : {Sig}", sigFromRawBytes);
        _logger.LogInformation("Computed rawB : {Sig}", sigFromRawDirect);

        if (sigFromDocs != sigFromRawBytes)
            _logger.LogWarning("MISMATCH: docs-computed vs raw-bytes-computed differ - body bytes may have been modified in transit");

        var headerBytes = Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant());

        var match1 = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sigFromDocs), headerBytes);
        var match2 = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sigFromRawBytes), headerBytes);
        var match3 = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sigFromRawDirect), headerBytes);

        if (match1 || match2 || match3)
        {
            result.Verified = true;
            result.SignatureMatch = true;
            result.ComputedSignature = sigFromDocs;
            result.Reason = "HMAC-SHA512 signature matches";
            _logger.LogInformation("Webhook VERIFIED");
        }
        else
        {
            result.Verified = false;
            result.SignatureMatch = false;
            result.ComputedSignature = sigFromDocs;
            result.Reason = "HMAC-SHA512 signature does not match. Verify WebhookSecret in appsettings.json matches the one in the OneKhusa dashboard.";
            _logger.LogWarning("Webhook REJECTED - HMAC-SHA512 did not match. Check that WebhookSecret matches the OneKhusa dashboard.");
        }

        return result;
    }

    private static string ComputeDirectHmacSha512(string secret, byte[] payloadBytes)
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA512(secretBytes);
        byte[] hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

public class WebhookVerificationResult
{
    public bool Verified { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SignatureProvided { get; set; } = string.Empty;
    public string ComputedSignature { get; set; } = string.Empty;
    public bool SignatureMatch { get; set; }
    public int PayloadLength { get; set; }
    public DateTime Timestamp { get; set; }
}
