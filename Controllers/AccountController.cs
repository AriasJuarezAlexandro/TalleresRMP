using Microsoft.AspNetCore.Mvc;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers;

public class AccountController : Controller
{
    private readonly UsuarioService _usuarioService;

    public AccountController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    // GET /Account/Login
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.GetInt32(SesionClaves.IdUsuario) is not null)
            return RedirectToLocal(returnUrl);

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    // POST /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return View(vm);

        var usuario = await _usuarioService.ValidarCredencialesAsync(vm.Login, vm.Password);
        if (usuario is null)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(vm);
        }

        HttpContext.Session.SetInt32(SesionClaves.IdUsuario, usuario.IdUsuario);
        HttpContext.Session.SetString(SesionClaves.Login, usuario.Login);
        HttpContext.Session.SetString(SesionClaves.Nivel, usuario.Nivel);

        return RedirectToLocal(returnUrl);
    }

    // POST /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
