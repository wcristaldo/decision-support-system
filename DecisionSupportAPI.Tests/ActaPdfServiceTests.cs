using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ActaPdfService (RF12/RF13 — acta de decisión
/// de despliegue en PDF, consolidando proyecto/versión/métricas/recomendación/decisión).</summary>
public class ActaPdfServiceTests
{
    private static ActaData ActaDeEjemplo(bool esOverride = false) => new(
        DecisionId: 1,
        ProyectoNombre: "Proyecto Demo",
        VersionNumero: "1.0.0",
        VersionDescripcion: "Versión inicial",
        ArchivoNombre: "output.json",
        ArchivoFecha: DateTime.UtcNow,
        Metricas: new() { new ActaMetricaData("Tasa de éxito", "96%"), new ActaMetricaData("Cobertura", "100%") },
        RecomendacionTipo: "desplegar",
        RecomendacionJustificacion: "Todas las métricas superan los umbrales.",
        RecomendacionFecha: DateTime.UtcNow,
        DecisionFinal: "aprobado",
        Comentario: "Aprobado según lo esperado.",
        EsOverride: esOverride,
        FechaDecision: DateTime.UtcNow,
        UsuarioDecisorNombre: "Administrador Sistema"
    );

    [Fact(DisplayName = "GenerarActaPdf produce un PDF válido no vacío")]
    public void GenerarActaPdf_ProduceBytesNoVacios()
    {
        var service = new ActaPdfService();

        var pdf = service.GenerarActaPdf(ActaDeEjemplo());

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact(DisplayName = "GenerarActaPdf con override no lanza excepción (aviso adicional en el documento)")]
    public void GenerarActaPdf_ConOverride_NoLanza()
    {
        var service = new ActaPdfService();

        var pdf = service.GenerarActaPdf(ActaDeEjemplo(esOverride: true));

        Assert.NotEmpty(pdf);
    }

    [Fact(DisplayName = "GenerarActaPdf con lista de métricas vacía no lanza excepción")]
    public void GenerarActaPdf_SinMetricas_NoLanza()
    {
        var service = new ActaPdfService();
        var acta = ActaDeEjemplo() with { Metricas = new() };

        var pdf = service.GenerarActaPdf(acta);

        Assert.NotEmpty(pdf);
    }
}
