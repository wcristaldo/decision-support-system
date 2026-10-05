using System.Globalization;
using System.Text.Json;

namespace DecisionSupportAPI.Services;

/// <summary>Métricas extraídas de un output.json nativo de Robot Framework 7+.</summary>
public record MetricasRobotFramework(
    int TotalPruebas,
    int PruebasExitosas,
    int PruebasFallidas,
    int PruebasOmitidas,
    decimal TiempoEjecucion,
    string Herramienta,
    string NombreSuite,
    string EstadoSuite)
{
    /// <summary>Cobertura del plan de pruebas: (exitosas + fallidas) / total × 100.</summary>
    public decimal Cobertura => Math.Round((PruebasExitosas + PruebasFallidas) / (decimal)TotalPruebas * 100, 2);
}

public class FormatoReporteInvalidoException(string mensaje) : Exception(mensaje);

/// <summary>
/// Valida y procesa el output.json de Robot Framework en el servidor, con las mismas reglas que
/// aplica el frontend en la carga manual (CargarResultados.jsx), para que la ingesta automatizada
/// (POST /api/reports) reciba el archivo real y no métricas ya calculadas por el cliente.
/// </summary>
public static class RobotFrameworkParser
{
    public static MetricasRobotFramework Parsear(string contenido)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(contenido);
        }
        catch (JsonException)
        {
            throw new FormatoReporteInvalidoException("El archivo no contiene un JSON válido.");
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
                throw new FormatoReporteInvalidoException("El archivo no contiene un objeto JSON.");

            if (!raiz.TryGetProperty("generator", out var generator) || generator.ValueKind != JsonValueKind.String
                || !generator.GetString()!.Contains("robot", StringComparison.OrdinalIgnoreCase))
                throw new FormatoReporteInvalidoException("El archivo no fue generado por Robot Framework. El campo \"generator\" debe contener \"Robot\".");

            if (!raiz.TryGetProperty("generated", out var generated) || generated.ValueKind != JsonValueKind.String)
                throw new FormatoReporteInvalidoException("Falta el campo \"generated\" con la fecha/hora de generación del reporte.");

            if (!raiz.TryGetProperty("suite", out var suite) || suite.ValueKind != JsonValueKind.Object)
                throw new FormatoReporteInvalidoException("Falta el objeto \"suite\" en el archivo.");

            var estado = suite.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String ? status.GetString() : null;
            if (estado is not ("PASS" or "FAIL"))
                throw new FormatoReporteInvalidoException("El campo \"suite.status\" debe ser \"PASS\" o \"FAIL\".");

            if (!suite.TryGetProperty("elapsed_time", out var elapsed) || elapsed.ValueKind != JsonValueKind.Number
                || !elapsed.TryGetDecimal(out var tiempo) || tiempo < 0)
                throw new FormatoReporteInvalidoException("El campo \"suite.elapsed_time\" debe ser un número no negativo (segundos).");

            if (!raiz.TryGetProperty("statistics", out var statistics) || statistics.ValueKind != JsonValueKind.Object
                || !statistics.TryGetProperty("total", out var total) || total.ValueKind != JsonValueKind.Object)
                throw new FormatoReporteInvalidoException("Falta el objeto \"statistics.total\" con los contadores de pruebas.");

            int Contador(string campo)
            {
                if (!total.TryGetProperty(campo, out var valor) || valor.ValueKind != JsonValueKind.Number
                    || !valor.TryGetInt32(out var n) || n < 0)
                    throw new FormatoReporteInvalidoException($"El campo \"statistics.total.{campo}\" debe ser un entero no negativo.");
                return n;
            }
            int pass = Contador("pass"), fail = Contador("fail"), skip = Contador("skip");
            if (pass + fail + skip < 1)
                throw new FormatoReporteInvalidoException("El total de pruebas (pass + fail + skip) debe ser al menos 1.");

            // En un output.json real, suite.status es FAIL si y solo si hubo al menos una prueba fallida.
            if (estado == "FAIL" && fail == 0)
                throw new FormatoReporteInvalidoException("Inconsistencia: \"suite.status\" es \"FAIL\" pero \"statistics.total.fail\" es 0. El archivo no es un output.json válido de Robot Framework.");
            if (estado == "PASS" && fail > 0)
                throw new FormatoReporteInvalidoException($"Inconsistencia: \"suite.status\" es \"PASS\" pero \"statistics.total.fail\" es {fail.ToString(CultureInfo.InvariantCulture)}. El archivo no es un output.json válido de Robot Framework.");

            var herramienta = generator.GetString()!;
            var parentesis = herramienta.IndexOf('(');
            if (parentesis > 0)
                herramienta = herramienta[..parentesis].Trim();
            var nombreSuite = suite.TryGetProperty("name", out var nombre) && nombre.ValueKind == JsonValueKind.String
                ? nombre.GetString()! : "(sin nombre)";

            return new MetricasRobotFramework(pass + fail + skip, pass, fail, skip, tiempo, herramienta, nombreSuite, estado);
        }
    }
}
