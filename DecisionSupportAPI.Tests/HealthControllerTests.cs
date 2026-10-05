using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de HealthController (chequeo de disponibilidad usado
/// por el proxy inverso / orquestador de contenedores — RNF11).</summary>
public class HealthControllerTests
{
    [Fact(DisplayName = "Get devuelve 200 con estado 'API is running'")]
    public void Get_Devuelve200ConEstado()
    {
        var controller = new HealthController();

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var status = (string)ok.Value!.GetType().GetProperty("status")!.GetValue(ok.Value)!;
        Assert.Equal("API is running", status);
    }
}
