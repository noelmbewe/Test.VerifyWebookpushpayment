using System.Text;
using System.Text.Json;
using Test.VerifyWebook.Models;
using Test.VerifyWebook.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OneKhusaSettings>(builder.Configuration.GetSection("OneKhusa"));
builder.Services.AddHttpClient<OneKhusaService>();
builder.Services.AddSingleton<WebhookVerifier>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var webhookEvents = new List<WebhookEventLog>();

async Task<IResult> HandleWebhookAsync(HttpContext context, WebhookVerifier verifier)
{
    using var ms = new MemoryStream();
    await context.Request.Body.CopyToAsync(ms);
    var bodyBytes = ms.ToArray();
    var body = Encoding.UTF8.GetString(bodyBytes);

    var webhookEvent = context.Request.Headers["X-OneKhusa-Webhook-Event"].FirstOrDefault();
    var signature = context.Request.Headers["X-OneKhusa-Webhook-Signature"].FirstOrDefault();

    var allHeaders = context.Request.Headers
        .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));

    app.Logger.LogInformation("WEBHOOK RECEIVED | Headers: {Headers}", JsonSerializer.Serialize(allHeaders));
    app.Logger.LogInformation("RAW WEBHOOK BODY ({Bytes} bytes): {Body}", bodyBytes.Length, body);

    var verification = verifier.VerifySignature(body, bodyBytes, signature);

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    var payload = JsonSerializer.Deserialize<WebhookPayload>(body, jsonOptions);
    payload?.Normalize();

    var metaDataStr = payload?.MetaDataRawJson ?? "null";
    if (payload?.MetaData != null && string.IsNullOrEmpty(payload.MetaDataRawJson))
        metaDataStr = JsonSerializer.Serialize(payload.MetaData);
    app.Logger.LogInformation("METADATA (raw): {MetaData}", metaDataStr);

    if (payload?.ExtensionData != null && payload.ExtensionData.Count > 0)
        app.Logger.LogInformation("EXTENSION DATA (unknown fields): {Ext}", JsonSerializer.Serialize(payload.ExtensionData));

    var log = new WebhookEventLog
    {
        ReceivedAt = DateTime.UtcNow,
        Event = webhookEvent ?? "unknown",
        Signature = signature ?? "",
        Payload = body,
        ParsedPayload = payload,
        Headers = allHeaders,
        Verified = verification.Verified,
        VerificationReason = verification.Reason,
        SignatureProvided = verification.SignatureProvided,
        ComputedSignature = verification.ComputedSignature,
        SignatureMatch = verification.SignatureMatch
    };
    webhookEvents.Add(log);

    app.Logger.LogInformation("Webhook {Verification}: {Event} | TAN: {TAN} | Amount: {Amount}",
        verification.Verified ? "VERIFIED" : "REJECTED",
        webhookEvent, payload?.MetaData?.TimedAccountNumber, payload?.TransactionAmount);

    return Results.Ok(new
    {
        status = "received",
        verification = new
        {
            verified = verification.Verified,
            reason = verification.Reason
        }
    });
}

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapPost("/api/access-token", async (OneKhusaService oneKhusa) =>
{
    var (result, error) = await oneKhusa.GetAccessTokenAsync();
    if (error != null)
        return Results.Json(new { success = false, error }, statusCode: 400);
    return Results.Json(new { success = true, data = result });
});

app.MapPost("/api/request-to-pay", async (OneKhusaService oneKhusa, IConfiguration config) =>
{
    var request = new RequestToPayRequest
    {
        MerchantAccountNumber = config.GetValue<int>("OneKhusa:MerchantAccountNumber"),
        TransactionAmount = 100.00m,
        TransactionDescription = "Test payment via OneKhusa API",
        ReferenceNumber = $"REF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
        CapturedBy = "test@example.com"
    };

    var (result, error) = await oneKhusa.RequestToPayAsync(request);
    if (error != null)
        return Results.Json(new { success = false, error }, statusCode: 400);
    return Results.Json(new { success = true, data = result });
});

app.MapPost("/api/request-to-pay/custom", async (RequestToPayRequest request, OneKhusaService oneKhusa, IConfiguration config) =>
{
    if (request.MerchantAccountNumber == 0)
        request.MerchantAccountNumber = config.GetValue<int>("OneKhusa:MerchantAccountNumber");

    if (string.IsNullOrEmpty(request.ReferenceNumber))
        request.ReferenceNumber = $"REF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";

    var (result, error) = await oneKhusa.RequestToPayAsync(request);
    if (error != null)
        return Results.Json(new { success = false, error }, statusCode: 400);
    return Results.Json(new { success = true, data = result });
});

app.MapPost("/api/push-payment", async (PushPaymentRequest request, OneKhusaService oneKhusa, IConfiguration config) =>
{
    if (request.ConnectorId <= 0)
        return Results.BadRequest(new { success = false, error = "ConnectorId must be greater than zero." });
    if (string.IsNullOrWhiteSpace(request.PayerAccountNumber))
        return Results.BadRequest(new { success = false, error = "PayerAccountNumber is required." });
    if (request.TransactionAmount <= 0)
        return Results.BadRequest(new { success = false, error = "TransactionAmount must be greater than zero." });
    if (string.IsNullOrWhiteSpace(request.TransactionDescription))
        return Results.BadRequest(new { success = false, error = "TransactionDescription is required." });
    if (string.IsNullOrWhiteSpace(request.CapturedBy))
        return Results.BadRequest(new { success = false, error = "CapturedBy is required." });

    if (request.MerchantAccountNumber == 0)
        request.MerchantAccountNumber = config.GetValue<int>("OneKhusa:MerchantAccountNumber");
    if (request.MerchantAccountNumber <= 0)
        return Results.BadRequest(new { success = false, error = "MerchantAccountNumber must be configured or supplied." });
    if (string.IsNullOrWhiteSpace(request.SourceReferenceNumber))
        request.SourceReferenceNumber = $"PUSH-{Guid.NewGuid():N}";

    var (result, error) = await oneKhusa.PushPaymentAsync(request);
    if (error != null)
        return Results.Json(new { success = false, error }, statusCode: 400);
    return Results.Json(new { success = true, data = result });
});

app.MapPost("/webhook/payrequest", HandleWebhookAsync);
app.MapPost("/webhook/pushpayment", HandleWebhookAsync);

app.MapGet("/api/webhook-events", () => Results.Ok(webhookEvents.OrderByDescending(x => x.ReceivedAt)));

app.MapGet("/api/debug-hmac", (IConfiguration config) =>
{
    var secret = config.GetValue<string>("OneKhusa:WebhookSecret") ?? "";

    var payload = "{\"test\":\"hello\"}";
    var sig = WebhookVerifier.ComputeSignature(payload, secret);

    return Results.Ok(new
    {
        secretConfigured = !string.IsNullOrEmpty(secret),
        payload,
        payloadByteCount = Encoding.UTF8.GetByteCount(payload),
        computedSignature = sig,
        signatureLength = sig.Length,
        instructions = "Go to https://mkjwk.org/ or any HMAC-SHA512 tool and compute HMAC-SHA512 of the payload string using the secret. If the result matches computedSignature above, your secret is correct."
    });
});

app.MapPost("/api/test-webhook", async (HttpContext context, WebhookVerifier verifier, IConfiguration config) =>
{
    var secret = config.GetValue<string>("OneKhusa:WebhookSecret") ?? "";

    var testPayload = new WebhookPayload
    {
        ConnectorId = 892353,
        SourceAccountNumber = "74629183",
        SourceAccountName = "Test Sender Ltd",
        SourceInstitution = "Test Bank",
        SourceReferenceNumber = "SRC" + Guid.NewGuid().ToString("N")[..10].ToUpper(),
        BeneficiaryAccountNumber = config.GetValue<int>("OneKhusa:MerchantAccountNumber").ToString(),
        TransactionReferenceNumber = "TXN" + Guid.NewGuid().ToString("N")[..10].ToUpper(),
        TransactionDescription = "Test webhook verification",
        TransactionAmount = 500.00m,
        TransactionFee = 10.00m,
        TransactionDate = DateTime.UtcNow,
        TransactionStatusCode = "S",
        TransactionCode = "DMP",
        ResponseCode = "S100",
        MerchantReferenceNumber = "TEST-MERCHANT-REF",
        MetaDataRaw = JsonSerializer.SerializeToElement(new
        {
            timedAccountNumber = "11005533",
            referenceNumber = "TEST-REF-001",
            sourceDescription = "Test payment"
        })
    };

    var payloadJson = JsonSerializer.Serialize(testPayload);
    var signature = WebhookVerifier.ComputeSignature(payloadJson, secret);

    using var httpClient = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Post, $"{context.Request.Scheme}://{context.Request.Host}/webhook/pushpayment")
    {
        Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
    };
    request.Headers.TryAddWithoutValidation("X-OneKhusa-Webhook-Event", "pushpayment.success");
    request.Headers.TryAddWithoutValidation("X-OneKhusa-Webhook-Signature", signature);

    var response = await httpClient.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();

    return Results.Json(new
    {
        status = "test webhook sent",
        payloadJson,
        signature,
        response = responseBody,
        note = "If this self-test shows VERIFIED, the algorithm is correct and your WebhookSecret matches. If real OneKhusa webhooks still fail, their dashboard secret differs from your appsettings.json value."
    });
});

app.Run();

public class WebhookEventLog
{
    public DateTime ReceivedAt { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public WebhookPayload? ParsedPayload { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public bool Verified { get; set; }
    public string VerificationReason { get; set; } = string.Empty;
    public string SignatureProvided { get; set; } = string.Empty;
    public string ComputedSignature { get; set; } = string.Empty;
    public bool SignatureMatch { get; set; }
}
