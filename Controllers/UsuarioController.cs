using Microsoft.AspNetCore.Mvc;
using TalleresRMP.Filters;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers;

[RequiereSesion]
[RequiereNivel("A")]
public class UsuarioController : Controller
{
    private readonly UsuarioService _usuarioService;

    public UsuarioController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    // GET /Usuario
    public async Task<IActionResult> Index()
    {
        var usuarios = await _usuarioService.ListarAsync();
        return View(usuarios);
    }

    // GET /Usuario/Create
    public IActionResult Create() => View(new UsuarioCreateViewModel());

    // POST /Usuario/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UsuarioCreateViewModel vm)
    {
        if (await _usuarioService.ExisteLoginAsync(vm.Login))
            ModelState.AddModelError(nameof(vm.Login), "Ese login ya existe.");

        if (!ModelState.IsValid)
            return View(vm);

        await _usuarioService.CrearAsync(vm.Login, vm.Password, vm.Nivel);
        return RedirectToAction(nameof(Index));
    }

    // GET /Usuario/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario is null)
            return NotFound();

        var vm = new UsuarioEditViewModel
        {
            IdUsuario = usuario.IdUsuario,
            Login = usuario.Login,
            Nivel = usuario.Nivel,
            Activo = usuario.Activo
        };
        return View(vm);
    }

    // POST /Usuario/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UsuarioEditViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        await _usuarioService.ActualizarAsync(vm.IdUsuario, vm.Nivel, vm.Activo, vm.NuevaPassword);
        return RedirectToAction(nameof(Index));
    }
}
