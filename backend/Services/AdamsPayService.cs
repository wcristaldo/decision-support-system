using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DecisionSupportAPI.Services;

// ── Interfaz ──────────────────────────────────────────────────────────────────

public interface IAdamsPayService
{
    /// <summary>
    /// Crea una deuda en AdamsPay y retorna la URL de pago única.
    /// </summary>
    Task<(bool Success, string? PayUrl, string? Error)> CreateDebtAsync(
        string docId,
        string label,
        decimal amount,
        int validHours = 24);

    /// <summary>
    /// Valida el HMAC de un webhook de AdamsPay.
    /// HMAC = MD5("adams" + rawBody + apiSecret)
    /// </summary>
    bool ValidateWebhookHmac(string rawBody, string receivedHmac);
}

// ── Implementación ────────────────────────────────────────────────────────────

public class AdamsPayService : IAdamsPayService
{
    private readonly IHttpClientFactory             _httpFactory;
    private readonly IConfiguration                 _config;
    private readonly ILogger<AdamsPayService>       _logger;

    public AdamsPayService(
        IHttpClientFactory       httpFactory,
        IConfiguration           config,
        ILogger<AdamsPayService> logger)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    // ── CreateDebt ────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? PayUrl, string? Error)> CreateDebtAsync(
        string docId, string label, decimal amount, int validHours = 24)
    {
        var apiKey  = _config["AdamsPay:ApiKey"]
                        ?? throw new InvalidOperationException("AdamsPay:ApiKey no configurado.");
        var baseUrl = _config["AdamsPay:BaseUrl"]
                        ?? "https://staging.adamspay.com/api/v1";

        // Las fechas DEBEN ser UTC — requerimiento explícito de la API
        var now    = DateTime.UtcNow;
        var expiry = now.AddHours(validHours);

        var body = JsonSerializer.Serialize(new
        {
            debt = new
            {
                docId,
                label,
                amount = new { currency = "PYG", value = ((long)amount).ToString() },
                validPeriod = new
                {
                    start = now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    end   = expiry.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            }
        });

        try
        {
            var client  = _httpFactory.CreateClient("AdamsPay");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/debts")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            // Autenticación: header "apikey" (no Bearer)
            request.Headers.Add("apikey",      apiKey);
            // x-if-exists: update → si ya existe un docId igual, actualiza en lugar de fallar
            request.Headers.Add("x-if-exists", "update");

            var response = await client.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("AdamsPay CreateDebt ({DocId}) → HTTP {Status}: {Json}",
                docId, (int)response.StatusCode, json);

            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("debt", out var debt) &&
                debt.TryGetProperty("payUrl", out var payUrlProp))
            {
                return (true, payUrlProp.GetString(), null);
            }

            // Si vino "meta" con el error, extraerlo
            var errorMsg = doc.RootElement.TryGetProperty("meta", out var meta)
                ? meta.GetRawText()
                : json;

            return (false, null, errorMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al llamar a AdamsPay CreateDebt ({DocId})", docId);
            return (false, null, ex.Message);
        }
    }

    // ── Validación de webhook ─────────────────────────────────────────────────

    public bool ValidateWebhookHmac(string rawBody, string receivedHmac)
    {
        if (string.IsNullOrEmpty(receivedHmac))
            return false;

        var secret   = _config["AdamsPay:ApiSecret"] ?? "";
        var input    = "adams" + rawBody + secret;
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var computed  = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return computed == receivedHmac.ToLowerInvariant();
    }
}
