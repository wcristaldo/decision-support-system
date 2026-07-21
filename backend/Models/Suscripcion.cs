namespace DecisionSupportAPI.Models;

public class Suscripcion
{
    public int      Id                  { get; set; }
    public int      IdPlan              { get; set; }
    public string   Estado              { get; set; } = "pendiente";
    // pendiente | activa | vencida | cancelada

    public DateTime? FechaInicio        { get; set; }
    public DateTime? FechaVencimiento   { get; set; }
    public DateTime  FechaCreacion      { get; set; }

    // Datos Pagopar para pagos recurrentes
    public int?     PagoparIdCliente    { get; set; }   // identificador en Pagopar
    public string?  PagoparIdTarjeta    { get; set; }   // id numérico tarjeta

    // Navegación
    public PlanSuscripcion?         Plan    { get; set; }
    public ICollection<PagoSuscripcion> Pagos { get; set; } = [];
}
