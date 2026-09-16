using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TalleresRMP.Services;

namespace TalleresRMP.Filters;

/// <summary>
/// Restringe una acción (o un controller completo) a los niveles indicados,
/// ej. [RequiereNivel("A","B")]. Asume que ya hay sesión iniciada -- se usa
/// junto con [RequiereSesion], que corre primero y garantiza la sesión.
/// Si el nivel de la sesión no está en la lista permitida, muestra
/// "Acceso denegado" (403) en vez de ejecutar la acción.
/// </summary>
public class RequiereNivelAttribute : ActionFilterAttribute
{
    private readonly string[] _nivelesPermitidos;

    public RequiereNivelAttribute(params string[] nivelesPermitidos)
    {
        _nivelesPermitidos = nivelesPermitidos;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var nivel = context.HttpContext.Session.GetString(SesionClaves.Nivel);
        if (nivel is null || !_nivelesPermitidos.Contains(nivel))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Result = new ViewResult { ViewName = "~/Views/Shared/AccesoDenegado.cshtml" };
            return;
        }

        base.OnActionExecuting(context);
    }
}
