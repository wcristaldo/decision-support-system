using System.Text.Json;
using System.Text.RegularExpressions;

namespace DecisionSupportAPI.Services;

public record PruebaFallida(string Modulo, string Nombre, string Mensaje, decimal Segundos);
public record PruebaLenta(string Modulo, string Nombre, decimal Segundos);
public record ModuloResumen(string Nombre, int Exitosas, int Fallidas, int Omitidas);

/// <summary>
/// Resumen técnico de un output.json de Robot Framework: qué pruebas fallaron y por qué, cuáles tardaron más y cómo
/// se reparten los resultados por módulo (suite de primer nivel). Es lo que permite que el reporte ejecutivo diga
/// "fallaron estas 3 pruebas del módulo Login" en vez de solo "fallaron 3 pruebas". Es un resumen compacto: se guarda
/// en lugar del archivo completo, que puede pesar varios MB por el detalle de cada paso.
/// </summary>
public record DetalleRobotFramework(
    List<ModuloResumen> Modulos,
    List<PruebaFallida> Fallidas,
    int TotalFallidas,
    List<PruebaLenta> MasLentas,
    int ErroresGlobales);

public static class RobotFrameworkDetalle
{
    public const int MaxFallidas = 20;
    public const int MaxLentas = 5;
    private const int MaxMensaje = 240;

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Serializar(DetalleRobotFramework d) => JsonSerializer.Serialize(d, Opciones);

    public static DetalleRobotFramework? Deserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DetalleRobotFramework>(json, Opciones); }
        catch (JsonException) { return null; }
    }

    private static string Limpiar(string? texto)
    {
        var t = Regex.Replace(texto ?? "", @"\s+", " ").Trim();
        return t.Length <= MaxMensaje ? t : t[..(MaxMensaje - 1)] + "…";
    }

    private static string Texto(JsonElement e, string propiedad) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>Mensaje del primer paso que falló (o del propio test), buscando en el árbol de pasos del test.</summary>
    private static string MensajeDeFallo(JsonElement elemento, int profundidad = 0)
    {
        if (profundidad > 10 || elemento.ValueKind != JsonValueKind.Object) return "";

        if (Texto(elemento, "type") == "MESSAGE")
        {
            var nivel = Texto(elemento, "level").ToUpperInvariant();
            return nivel is "FAIL" or "ERROR" ? Texto(elemento, "message") : "";
        }

        if (elemento.TryGetProperty("body", out var cuerpo) && cuerpo.ValueKind == JsonValueKind.Array)
            foreach (var hijo in cuerpo.EnumerateArray())
            {
                if (hijo.ValueKind != JsonValueKind.Object) continue;
                var esMensaje = Texto(hijo, "type") == "MESSAGE";
                if (!esMensaje && Texto(hijo, "status").ToUpperInvariant() != "FAIL") continue;
                var m = MensajeDeFallo(hijo, profundidad + 1);
                if (m.Length > 0) return m;
            }
        return "";
    }

    /// <summary>
    /// Extrae el detalle de un output.json. No lanza excepciones: si el archivo no se puede leer o no trae pruebas a
    /// nivel de test (por ejemplo, un resumen con solo las estadísticas), devuelve null y el sistema sigue
    /// funcionando con las métricas agregadas.
    /// </summary>
    public static DetalleRobotFramework? Extraer(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return null;
        try
        {
            using var documento = JsonDocument.Parse(contenido);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("suite", out var suiteRaiz) || suiteRaiz.ValueKind != JsonValueKind.Object)
                return null;

            var modulos = new List<(string Nombre, int P, int F, int S)>();
            var fallidas = new List<PruebaFallida>();
            var ejecutadas = new List<PruebaLenta>();
            var totalFallidas = 0;
            var totalPruebas = 0;

            void Contar(string modulo, string estado)
            {
                var i = modulos.FindIndex(m => m.Nombre == modulo);
                if (i < 0) { modulos.Add((modulo, 0, 0, 0)); i = modulos.Count - 1; }
                var (n, p, f, s) = modulos[i];
                modulos[i] = estado switch { "PASS" => (n, p + 1, f, s), "FAIL" => (n, p, f + 1, s), _ => (n, p, f, s + 1) };
            }

            void Recorrer(JsonElement suite, string modulo, bool esRaiz)
            {
                if (suite.TryGetProperty("tests", out var pruebas) && pruebas.ValueKind == JsonValueKind.Array)
                    foreach (var t in pruebas.EnumerateArray())
                    {
                        if (t.ValueKind != JsonValueKind.Object) continue;
                        var estado = Texto(t, "status").ToUpperInvariant();
                        if (estado is not ("PASS" or "FAIL" or "SKIP" or "NOT RUN")) continue;
                        totalPruebas++;
                        Contar(modulo, estado);
                        var nombre = Texto(t, "name");
                        var segundos = t.TryGetProperty("elapsed_time", out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var s)
                            ? Math.Round(s, 2) : 0m;
                        if (estado is "PASS" or "FAIL")
                            ejecutadas.Add(new PruebaLenta(modulo, nombre, segundos));
                        if (estado == "FAIL")
                        {
                            totalFallidas++;
                            if (fallidas.Count < MaxFallidas)
                            {
                                var mensaje = Texto(t, "message");
                                if (mensaje.Length == 0) mensaje = MensajeDeFallo(t);
                                fallidas.Add(new PruebaFallida(modulo, nombre, Limpiar(mensaje), segundos));
                            }
                        }
                    }

                if (suite.TryGetProperty("suites", out var hijas) && hijas.ValueKind == JsonValueKind.Array)
                    foreach (var h in hijas.EnumerateArray())
                        if (h.ValueKind == JsonValueKind.Object)
                            Recorrer(h, esRaiz ? Texto(h, "name") : modulo, false);
            }

            var nombreRaiz = Texto(suiteRaiz, "name");
            Recorrer(suiteRaiz, nombreRaiz.Length > 0 ? nombreRaiz : "(sin nombre)", true);
            if (totalPruebas == 0) return null;

            var errores = raiz.TryGetProperty("errors", out var er) && er.ValueKind == JsonValueKind.Array ? er.GetArrayLength() : 0;
            return new DetalleRobotFramework(
                Modulos: modulos.Select(m => new ModuloResumen(m.Nombre, m.P, m.F, m.S)).OrderByDescending(m => m.Fallidas).ThenBy(m => m.Nombre).ToList(),
                Fallidas: fallidas,
                TotalFallidas: totalFallidas,
                MasLentas: ejecutadas.OrderByDescending(x => x.Segundos).Take(MaxLentas).ToList(),
                ErroresGlobales: errores);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
