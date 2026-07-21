using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DecisionSupportAPI.Services;

// ── DTOs de Pagopar ────────────────────────────────────────────────────────

public record PagoparIniciarTransaccionRequest(
    string  IdPedidoComercio,
    decimal MontoTotal,
    string  NombreComprador,
    string  EmailComprador,
    string  DocumentoComprador,
    string  DescripcionProducto,
    int     IdProducto,
    string  FechaMaximaPago    // "yyyy-MM-dd HH:mm:ss"
);

public record PagoparTransaccionResult(bool Respuesta, string? HashPedido, string? NumeroPedido, string? Error);

public record PagoparRecurrenteResult(bool Respuesta, string? Resultado, string? Error);

public record PagoparTarjeta(
    string Tarjeta,
    string TarjetaNumero,
    string Marca,
    string Emisor,
    string AliasToken,
    string Proveedor,
    string TipoTarjeta,
    string? UrlLogo
);

public record PagoparListarTarjetasResult(bool Respuesta, List<PagoparTarjeta>? Tarjetas, string? Error);

// ── Interfaz ───────────────────────────────────────────────────────────────

public interface IPagoparService
{
    /// <summary>Genera el token para iniciar-transaccion: sha1(private + idPedido + monto)</summary>
    string GenerarTokenTransaccion(string idPedido, decimal monto);

    /// <summary>Genera el token para pago-recurrente: sha1(private + "PAGO-RECURRENTE")</summary>
    string GenerarTokenRecurrente();

    /// <summary>Genera el token para consultar un pedido: sha1(private + "CONSULTA")</summary>
    string GenerarTokenConsulta();

    /// <summary>Valida el token de notificacion webhook: sha1(private + hashPedido) == tokenRecibido</summary>
    bool ValidarTokenWebhook(string hashPedido, string tokenRecibido);

    /// <summary>Paso 1 del checkout: crea el pedido en Pagopar y retorna el hash.</summary>
    Task<PagoparTransaccionResult> IniciarTransaccionAsync(PagoparIniciarTransaccionRequest request);

    /// <summary>URL de checkout a la que redirigir al usuario.</summary>
    string GetCheckoutUrl(string hashPedido);

    /// <summary>Recurrentes: registra un cliente en Pagopar.</summary>
    Task<PagoparRecurrenteResult> AgregarClienteAsync(int identificador, string nombreApellido, string email, string celular);

    /// <summary>Recurrentes: solicita la creación de una tarjeta (retorna token para iframe).</summary>
    Task<PagoparRecurrenteResult> AgregarTarjetaAsync(int identificador, string urlRetorno, string proveedor = "uPay");

    /// <summary>Recurrentes: confirma la tarjeta (obligatorio tras el iframe).</summary>
    Task<PagoparRecurrenteResult> ConfirmarTarjetaAsync(int identificador, string urlRetorno);

    /// <summary>Recurrentes: lista las tarjetas catastradas de un cliente.</summary>
    Task<PagoparListarTarjetasResult> ListarTarjetasAsync(int identificador);

    /// <summary>Recurrentes: debita con la tarjeta catastrada usando un hash de pedido ya creado.</summary>
    Task<PagoparRecurrenteResult> PagarRecurrenteAsync(string hashPedido, string aliasToken, int identificador);
}

// ── Implementación ─────────────────────────────────────────────────────────

public class PagoparService : IPagoparService
{
    private readonly HttpClient _http;
    private readonly string     _privateKey;
    private readonly string     _publicKey;
    private readonly string     _baseUrl;
    private readonly string     _checkoutUrl;
    private readonly ILogger<PagoparService> _logger;

    public PagoparService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<PagoparService> logger)
    {
        _privateKey  = config["Pagopar:PrivateKey"]  ?? throw new InvalidOperationException("Pagopar:PrivateKey no configurado");
        _publicKey   = config["Pagopar:PublicKey"]   ?? throw new InvalidOperationException("Pagopar:PublicKey no configurado");
        _baseUrl     = config["Pagopar:ApiBaseUrl"]  ?? "https://api.pagopar.com";
        _checkoutUrl = config["Pagopar:CheckoutUrl"] ?? "https://www.pagopar.com/pagos/";
        _http        = httpFactory.CreateClient("Pagopar");
        _logger      = logger;
    }

    // ── Token helpers ──────────────────────────────────────────────────────

    private static string Sha1Hex(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    public string GenerarTokenTransaccion(string idPedido, decimal monto)
        // sha1(private_key + idPedido + strval(floatval(monto)))
        => Sha1Hex(_privateKey + idPedido + ((double)monto).ToString("G"));

    public string GenerarTokenRecurrente()
        => Sha1Hex(_privateKey + "PAGO-RECURRENTE");

    public string GenerarTokenConsulta()
        => Sha1Hex(_privateKey + "CONSULTA");

    public bool ValidarTokenWebhook(string hashPedido, string tokenRecibido)
        => Sha1Hex(_privateKey + hashPedido) == tokenRecibido;

    public string GetCheckoutUrl(string hashPedido)
        => _checkoutUrl + hashPedido;

    // ── Checkout estándar ──────────────────────────────────────────────────

    public async Task<PagoparTransaccionResult> IniciarTransaccionAsync(PagoparIniciarTransaccionRequest req)
    {
        var payload = new
        {
            token            = GenerarTokenTransaccion(req.IdPedidoComercio, req.MontoTotal),
            public_key       = _publicKey,
            monto_total      = req.MontoTotal,
            tipo_pedido      = "VENTA-COMERCIO",
            id_pedido_comercio = req.IdPedidoComercio,
            descripcion_resumen = req.DescripcionProducto,
            fecha_maxima_pago = req.FechaMaximaPago,
            comprador = new
            {
                nombre        = req.NombreComprador,
                email         = req.EmailComprador,
                documento     = req.DocumentoComprador,
                tipo_documento = "CI",
                ciudad        = 1,
                ruc           = "",
                telefono      = "",
                direccion     = "",
                coordenadas   = "",
                razon_social  = "",
                direccion_referencia = (string?)null
            },
            compras_items = new[]
            {
                new
                {
                    ciudad           = "1",
                    nombre           = req.DescripcionProducto,
                    cantidad         = 1,
                    categoria        = "909",
                    public_key       = _publicKey,
                    url_imagen       = "",
                    descripcion      = req.DescripcionProducto,
                    id_producto      = req.IdProducto,
                    precio_total     = req.MontoTotal,
                    vendedor_telefono               = "",
                    vendedor_direccion              = "",
                    vendedor_direccion_referencia   = "",
                    vendedor_direccion_coordenadas  = ""
                }
            }
        };

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/api/comercios/2.0/iniciar-transaccion", payload);
            var body = await resp.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.GetProperty("respuesta").GetBoolean())
            {
                var data    = root.GetProperty("resultado")[0].GetProperty("data").GetString();
                var pedido  = root.GetProperty("resultado")[0].GetProperty("pedido").GetString();
                return new(true, data, pedido, null);
            }

            var err = root.GetProperty("resultado").GetString();
            return new(false, null, null, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar transacción en Pagopar");
            return new(false, null, null, ex.Message);
        }
    }

    // ── Pagos recurrentes ──────────────────────────────────────────────────

    private async Task<PagoparRecurrenteResult> PostRecurrenteAsync(string endpoint, object payload)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}{endpoint}", payload);
            var body = await resp.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var ok  = root.GetProperty("respuesta").GetBoolean();
            var res = root.GetProperty("resultado");
            var resStr = res.ValueKind == JsonValueKind.String
                ? res.GetString()
                : res.GetRawText();

            return new(ok, resStr, ok ? null : resStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Pagopar recurrente {Endpoint}", endpoint);
            return new(false, null, ex.Message);
        }
    }

    public Task<PagoparRecurrenteResult> AgregarClienteAsync(int identificador, string nombreApellido, string email, string celular)
        => PostRecurrenteAsync("/api/pago-recurrente/3.0/agregar-cliente/", new
        {
            token          = GenerarTokenRecurrente(),
            token_publico  = _publicKey,
            identificador,
            nombre_apellido = nombreApellido,
            email,
            celular
        });

    public Task<PagoparRecurrenteResult> AgregarTarjetaAsync(int identificador, string urlRetorno, string proveedor = "uPay")
        => PostRecurrenteAsync("/api/pago-recurrente/3.0/agregar-tarjeta/", new
        {
            token         = GenerarTokenRecurrente(),
            token_publico = _publicKey,
            url           = urlRetorno,
            proveedor,
            identificador
        });

    public Task<PagoparRecurrenteResult> ConfirmarTarjetaAsync(int identificador, string urlRetorno)
        => PostRecurrenteAsync("/api/pago-recurrente/3.0/confirmar-tarjeta/", new
        {
            token         = GenerarTokenRecurrente(),
            token_publico = _publicKey,
            url           = urlRetorno,
            identificador
        });

    public async Task<PagoparListarTarjetasResult> ListarTarjetasAsync(int identificador)
    {
        var payload = new
        {
            token         = GenerarTokenRecurrente(),
            token_publico = _publicKey,
            identificador
        };

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/api/pago-recurrente/3.0/listar-tarjeta/", payload);
            var body = await resp.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.GetProperty("respuesta").GetBoolean())
                return new(false, null, root.GetProperty("resultado").GetString());

            var tarjetas = root.GetProperty("resultado")
                .EnumerateArray()
                .Select(t => new PagoparTarjeta(
                    Tarjeta:      t.GetProperty("tarjeta").GetString()!,
                    TarjetaNumero: t.GetProperty("tarjeta_numero").GetString()!,
                    Marca:        t.GetProperty("marca").GetString()!,
                    Emisor:       t.GetProperty("emisor").GetString()!,
                    AliasToken:   t.GetProperty("alias_token").GetString()!,
                    Proveedor:    t.GetProperty("proveedor").GetString()!,
                    TipoTarjeta:  t.GetProperty("tipo_tarjeta").GetString()!,
                    UrlLogo:      t.TryGetProperty("url_logo", out var logo) ? logo.GetString() : null
                ))
                .ToList();

            return new(true, tarjetas, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar tarjetas en Pagopar");
            return new(false, null, ex.Message);
        }
    }

    public Task<PagoparRecurrenteResult> PagarRecurrenteAsync(string hashPedido, string aliasToken, int identificador)
        => PostRecurrenteAsync("/api/pago-recurrente/3.0/pagar/", new
        {
            token         = GenerarTokenRecurrente(),
            token_publico = _publicKey,
            hash_pedido   = hashPedido,
            tarjeta       = aliasToken,
            identificador
        });
}
