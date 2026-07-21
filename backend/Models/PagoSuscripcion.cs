namespace DecisionSupportAPI.Models;

public class PagoSuscripcion
{
    public int      Id                      { get; set; }
    public int      IdSuscripcion           { get; set; }
    public int      IdPlan                  { get; set; }
    public decimal  Monto                   { get; set; }
    public string   Estado                  { get; set; } = "pendiente";
    // pendiente | aprobado | rechazado | revertido

    // Datos Pagopar
    public string?  PagoparIdPedidoComercio { get; set; }  // nuestro ID único
    public string?  PagoparHashPedido       { get; set; }  // hash retornado por Pagopar
    public string?  PagoparNumeroPedido     { get; set; }  // número de pedido Pagopar
    public string?  PagoparRespuesta        { get; set; }  // JSON completo

    public DateTime? FechaPago              { get; set; }
    public DateTime  FechaCreacion          { get; set; }

    // Navegación
    public Suscripcion?     Suscripcion     { get; set; }
    public PlanSuscripcion? Plan            { get; set; }
}
