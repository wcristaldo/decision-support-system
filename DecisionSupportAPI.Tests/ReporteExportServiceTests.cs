using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ReporteExportService (RF10/RF11 — exportación
/// del historial de análisis a PDF/Excel/CSV).</summary>
public class ReporteExportServiceTests
{
    private static List<AnalisisHistorialDto> HistorialDeEjemplo() => new()
    {
        new AnalisisHistorialDto
        {
            ProyectoId = 1, ProyectoNombre = "Proyecto Demo", ProyectoTipo = "web",
            VersionId = 1, VersionNumero = "1.0.0", ResultadoId = 1, RecomendacionId = 1,
            Recomendacion = "desplegar", FechaCarga = DateTime.UtcNow, FechaEvaluacion = DateTime.UtcNow,
            TasaExito = 96m, TasaFallo = 4m, Cobertura = 100m, TiempoEjecucion = 45m, TotalPruebas = 50m,
        }
    };

    [Fact(DisplayName = "GenerarHistorialPdf produce un PDF no vacío")]
    public void GenerarHistorialPdf_ProduceBytesNoVacios()
    {
        var service = new ReporteExportService();

        var pdf = service.GenerarHistorialPdf(HistorialDeEjemplo());

        Assert.NotEmpty(pdf);
        // Firma estándar de un PDF: %PDF
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact(DisplayName = "GenerarHistorialExcel produce un archivo XLSX no vacío")]
    public void GenerarHistorialExcel_ProduceBytesNoVacios()
    {
        var service = new ReporteExportService();

        var xlsx = service.GenerarHistorialExcel(HistorialDeEjemplo());

        Assert.NotEmpty(xlsx);
    }

    [Fact(DisplayName = "GenerarHistorialCsv incluye el nombre del proyecto en el contenido")]
    public void GenerarHistorialCsv_IncluyeDatos()
    {
        var service = new ReporteExportService();

        var csvBytes = service.GenerarHistorialCsv(HistorialDeEjemplo());
        var texto = System.Text.Encoding.UTF8.GetString(csvBytes);

        Assert.Contains("Proyecto Demo", texto);
    }

    [Fact(DisplayName = "GenerarHistorialPdf con lista vacía no lanza excepción")]
    public void GenerarHistorialPdf_ListaVacia_NoLanza()
    {
        var service = new ReporteExportService();

        var pdf = service.GenerarHistorialPdf(new List<AnalisisHistorialDto>());

        Assert.NotEmpty(pdf);
    }
}
