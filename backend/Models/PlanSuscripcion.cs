namespace DecisionSupportAPI.Models;

public class PlanSuscripcion
{
    public int     Id                    { get; set; }
    public string  Nombre                { get; set; } = "";
    public decimal PrecioMensual         { get; set; }

    // Límites cuantitativos (null = ilimitado)
    public int?    MaxProyectos          { get; set; }
    public int?    MaxUsuarios           { get; set; }
    public int?    MaxEvaluacionesMes    { get; set; }
    public int?    MaxTamanoArchivoMb    { get; set; }

    // Historial (null = completo)
    public int?    HistorialDias         { get; set; }

    // Funcionalidades de reporting
    public bool    ExportarPdf           { get; set; }
    public bool    ExportarExcel         { get; set; }
    public bool    DashboardAvanzado     { get; set; }
    public bool    AuditoriaDetallada    { get; set; }

    // Notificaciones e integraciones
    public bool    NotificacionesEmail   { get; set; }
    public bool    NotificacionesSlack   { get; set; }
    public bool    ApiPublica            { get; set; }
    public bool    IntegracionCicd       { get; set; }
    public bool    Webhooks              { get; set; }

    // Soporte
    public bool    SoportePrioritario    { get; set; }

    public string  Estado                { get; set; } = "activo";
    public DateTime FechaCreacion        { get; set; }

    // Navegación
    public ICollection<Suscripcion> Suscripciones { get; set; } = [];
}
