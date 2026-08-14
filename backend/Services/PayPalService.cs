using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace DecisionSupportAPI.Services;

// ── Interfaz ──────────────────────────────────────────────────────────────────

public interface IPayPalService
{
    /// <summary>
    /// Crea una orden de pago en PayPal y devuelve la URL de aprobación del usuario.
    /// El monto se recibe en Guaraníes (PYG) y se convierte a USD para PayPal.
    /// </summary>
    Task<(bool Success, string? ApprovalUrl, string? OrderId, string? Error)> CreateOrderAsync(
        int planId, string planNombre, decimal montoGs, string returnUrl, string cancelUrl);

    /// <summary>
    /// Captura (completa) una orden aprobada por el usuario en PayPal.
    /// Debe llamarse luego del redirect de retorno.
    /// </summary>
    Task<(bool Success, string? TransactionId, string? Error)> CaptureOrderAsync(string orderId);
}

// ── Implementación ────────────────────────────────────────────────────────────

public class PayPalService : IPayPalService
{
    private readonly IHttpClientFactory      _httpFactory;
    private readonly IConfiguration          _config;
    private readonly ILogger<PayPalService>  _logger;
    private readonly IMemoryCache            _cache;

    public PayPalService(
        IHttpClientFactory      httpFactory,
        IConfiguration          config,
        ILogger<PayPalService>  logger,
        IMemoryCache            cache)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
        _cache       = cache;
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    private string BaseUrl =>
        _config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";

    /// <summary>
    /// Obtiene la tasa de cambio PYG/USD desde open.er-api.com.
    /// Cachea el resultado 1 hora. Si falla, usa el valor configurado (fallback).
    /// </summary>
    private async Task<decimal> ObtenerTasaPygUsdAsync()
    {
        const string cacheKey = "paypal_tasa_pyg_usd";
        if (_cache.TryGetValue(cacheKey, out decimal tasaCacheada))
            return tasaCacheada;

        // Fallback configurado
        var tasaStr = _config["PayPal:TasaPygUsd"] ?? "7500";
        if (!decimal.TryParse(tasaStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var tasaFallback) || tasaFallback <= 0)
            tasaFallback = 7500m;

        try
        {
            var client = _httpFactory.CreateClient("ExchangeRate");
            var res    = await client.GetAsync("https://open.er-api.com/v6/latest/USD");

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("ExchangeRate API devolvió {Status}. Usando fallback {Tasa}",
                    res.StatusCode, tasaFallback);
                return tasaFallback;
            }

            var body = await res.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                rates.TryGetProperty("PYG", out var pygProp))
            {
                var tasa = pygProp.GetDecimal();
                if (tasa > 0)
                {
                    _cache.Set(cacheKey, tasa, TimeSpan.FromHours(1));
                    _logger.LogInformation("Tasa USD→PYG actualizada: {Tasa} (cacheada 1h)", tasa);
                    return tasa;
                }
            }

            _logger.LogWarning("No se pudo extraer PYG de la API. Usando fallback {Tasa}", tasaFallback);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener cotización. Usando fallback {Tasa}", tasaFallback);
        }

        return tasaFallback;
    }

    private async Task<decimal> ConvertirPygAUsdAsync(decimal montoGs)
    {
        var tasa = await ObtenerTasaPygUsdAsync();
        return Math.Round(montoGs / tasa, 2);
    }

    /// <summary>Obtiene un Bearer token mediante OAuth2 client_credentials.</summary>
    private async Task<string?> ObtenerTokenAsync()
    {
        var clientId = _config["PayPal:ClientId"] ?? "";
        var secret   = _config["PayPal:Secret"]   ?? "";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("PayPal: ClientId o Secret no configurados.");
            return null;
        }

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{secret}"));
        var client      = _httpFactory.CreateClient("PayPal");

        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        req.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal token error: {Status} {Body}",
                res.StatusCode, await res.Content.ReadAsStringAsync());
            return null;
        }

        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    // ── CreateOrderAsync ──────────────────────────────────────────────────────

    public async Task<(bool Success, string? ApprovalUrl, string? OrderId, string? Error)>
        CreateOrderAsync(int planId, string planNombre, decimal montoGs, string returnUrl, string cancelUrl)
    {
        var token = await ObtenerTokenAsync();
        if (token == null)
            return (false, null, null, "No se pudo obtener token de PayPal. Verificá la configuración.");

        var montoUsd = await ConvertirPygAUsdAsync(montoGs);

        // Payload de la orden según la API v2 de PayPal
        var orderPayload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = "USD",
                        value         = montoUsd.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    },
                    description = $"Roshka DSS — Plan {planNombre} (suscripción mensual)"
                }
            },
            application_context = new
            {
                return_url           = returnUrl,
                cancel_url           = cancelUrl,
                brand_name           = "Roshka DSS",
                landing_page         = "BILLING",
                user_action          = "PAY_NOW",
                shipping_preference  = "NO_SHIPPING",
                locale               = "es-PY"
            }
        };

        var client = _httpFactory.CreateClient("PayPal");
        var req    = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(
            JsonSerializer.Serialize(orderPayload),
            Encoding.UTF8, "application/json");

        var res  = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal create order error: {Status} {Body}", res.StatusCode, body);
            return (false, null, null, $"PayPal rechazó la solicitud (HTTP {(int)res.StatusCode}).");
        }

        var doc     = JsonDocument.Parse(body);
        var orderId = doc.RootElement.GetProperty("id").GetString();

        string? approvalUrl = null;
        if (doc.RootElement.TryGetProperty("links", out var links))
        {
            foreach (var link in links.EnumerateArray())
            {
                if (link.GetProperty("rel").GetString() == "approve")
                {
                    approvalUrl = link.GetProperty("href").GetString();
                    break;
                }
            }
        }

        if (approvalUrl == null)
        {
            _logger.LogError("PayPal no retornó link 'approve'. Body: {Body}", body);
            return (false, null, null, "PayPal no retornó URL de aprobación.");
        }

        _logger.LogInformation("Orden PayPal creada: {OrderId} — USD {Monto}", orderId, montoUsd);
        return (true, approvalUrl, orderId, null);
    }

    // ── CaptureOrderAsync ─────────────────────────────────────────────────────

    public async Task<(bool Success, string? TransactionId, string? Error)>
        CaptureOrderAsync(string orderId)
    {
        var token = await ObtenerTokenAsync();
        if (token == null)
            return (false, null, "No se pudo obtener token de PayPal.");

        var client = _httpFactory.CreateClient("PayPal");
        var req    = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl}/v2/checkout/orders/{orderId}/capture");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var res  = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal capture error: {Status} {Body}", res.StatusCode, body);
            return (false, null, $"Error al capturar el pago con PayPal (HTTP {(int)res.StatusCode}).");
        }

        var doc    = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();

        if (status != "COMPLETED")
        {
            _logger.LogWarning("PayPal capture status inesperado: {Status}", status);
            return (false, null, $"El pago no fue completado (estado: {status}).");
        }

        // Extraer transaction ID de purchase_units[0].payments.captures[0].id
        string? transactionId = null;
        if (doc.RootElement.TryGetProperty("purchase_units", out var units) &&
            units.GetArrayLength() > 0)
        {
            var first = units[0];
            if (first.TryGetProperty("payments", out var payments) &&
                payments.TryGetProperty("captures", out var captures) &&
                captures.GetArrayLength() > 0)
            {
                transactionId = captures[0].GetProperty("id").GetString();
            }
        }

        _logger.LogInformation("PayPal capturado OK: orderId={OrderId} txId={TxId}",
            orderId, transactionId);
        return (true, transactionId, null);
    }
}
