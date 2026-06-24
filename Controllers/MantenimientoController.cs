using Microsoft.AspNetCore.Mvc;
using TalleresRMP.Data;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers;

public class MantenimientoController : Controller
{
    private readonly MantenimientoRepository _repository;
    private readonly MantenimientoCacheService _cacheService;
    private readonly ProformaPdfService _pdfService;

    public MantenimientoController(
        MantenimientoRepository repository,
        MantenimientoCacheService cacheService,
        ProformaPdfService pdfService)
    {
        _repository = repository;
        _cacheService = cacheService;
        _pdfService = pdfService;
    }

    // GET /Mantenimiento
    public async Task<IActionResult> Index()
    {
        var lista = await _repository.GetAllAsync();
        return View(lista);
    }

    // GET /Mantenimiento/Create
    public IActionResult Create()
    {
        var vm = new MantenimientoViewModel
        {
            Mantenimiento = new Mantenimiento
            {
                FechaCreacion = DateTime.Now,
                Estado = "Pendiente"
            },
            Productos = new List<MantenimientoProducto>()
        };
        return View(vm);
    }

    // POST /Mantenimiento/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MantenimientoViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var m = vm.Mantenimiento;
        m.Productos = vm.Productos ?? new List<MantenimientoProducto>();
        RecalcularTotales(m);

        var id = await _repository.InsertAsync(m);
        _cacheService.Invalidate();

        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Mantenimiento/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var m = await _repository.GetByIdAsync(id);
        if (m is null)
            return NotFound();

        var vm = new MantenimientoViewModel
        {
            Mantenimiento = m,
            Productos = m.Productos
        };
        return View(vm);
    }

    // POST /Mantenimiento/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MantenimientoViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var m = vm.Mantenimiento;
        m.Productos = vm.Productos ?? new List<MantenimientoProducto>();
        RecalcularTotales(m);

        await _repository.UpdateAsync(m);
        _cacheService.Invalidate();

        return RedirectToAction(nameof(Details), new { id = m.IdMantenimiento });
    }

    // GET /Mantenimiento/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var m = await _repository.GetByIdAsync(id);
        if (m is null)
            return NotFound();

        return View(m);
    }

    // POST /Mantenimiento/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _repository.DeleteAsync(id);
        _cacheService.Invalidate();

        return RedirectToAction(nameof(Index));
    }

    // GET /Mantenimiento/Pdf/{id}
    public async Task<IActionResult> Pdf(int id)
    {
        var m = await _repository.GetByIdAsync(id);
        if (m is null)
            return NotFound();

        var bytes = _pdfService.GenerarProforma(m);
        return File(bytes, "application/pdf", $"Proforma_{m.Numero}.pdf");
    }

    /// <summary>
    /// Recalcula el importe de cada producto (Cantidad × PrecioUnitario) y el
    /// Total del mantenimiento como suma de los importes.
    /// </summary>
    private static void RecalcularTotales(Mantenimiento m)
    {
        foreach (var p in m.Productos)
            p.Importe = p.Cantidad * p.PrecioUnitario;

        m.Total = m.Productos.Sum(p => p.Importe);
    }
}
