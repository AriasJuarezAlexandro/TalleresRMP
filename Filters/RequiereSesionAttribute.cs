using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TalleresRMP.Services;

namespace TalleresRMP.Filters;

/// <summary>
/// Exige que haya un usuario logueado (sesión con "IdUsuario"). Si no,
/// redirige a /Account/Login conservando la URL original en ReturnUrl.
/// </summary>
public class RequiereSesionAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var idUsuario = context.HttpContext.Session.GetInt32(SesionClaves.IdUsuario);
        if (idUsuario is null)
        {
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        base.OnActionExecuting(context);
    }
}
