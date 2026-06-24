using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Nelknet.LibSQL.Data;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers;

public class MantenimientoController : Controller
{
    private const string FechaFormato = "yyyy-MM-dd HH:mm:ss";

    private readonly TursoService _turso;
    private readonly MantenimientoCacheService _cacheService;
    private readonly ProformaPdfService _pdfService;

    public MantenimientoController(
        TursoService turso,
        MantenimientoCacheService cacheService,
        ProformaPdfService pdfService)
    {
        _turso = turso;
        _cacheService = cacheService;
        _pdfService = pdfService;
    }

    // GET /Mantenimiento
    public async Task<IActionResult> Index()
    {
        var lista = new List<Mantenimiento>();

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, Estado, FechaCreacion " +
            "FROM Mantenimiento ORDER BY FechaCreacion DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add(LeerCabecera(reader));

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

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        int id;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Mantenimiento (Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, Estado, FechaCreacion) " +
                "VALUES (@Numero, @Cliente, @Telefono, @Marca, @Modelo, @Placa, @KM, @Total, @Estado, @FechaCreacion)";
            AgregarParametrosCabecera(cmd, m);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT last_insert_rowid()";
            id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        await InsertarProductosAsync(conn, id, m.Productos);
        _cacheService.Invalidate();

        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Mantenimiento/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var m = await ObtenerPorIdAsync(id);
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

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE Mantenimiento SET " +
                "Numero=@Numero, Cliente=@Cliente, Telefono=@Telefono, Marca=@Marca, Modelo=@Modelo, " +
                "Placa=@Placa, KM=@KM, Total=@Total, Estado=@Estado " +
                "WHERE IdMantenimiento=@IdMantenimiento";
            AgregarParametrosCabecera(cmd, m);
            cmd.Parameters.AddWithValue("@IdMantenimiento", m.IdMantenimiento);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM MantenimientoProducto WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", m.IdMantenimiento);
            await cmd.ExecuteNonQueryAsync();
        }

        await InsertarProductosAsync(conn, m.IdMantenimiento, m.Productos);
        _cacheService.Invalidate();

        return RedirectToAction(nameof(Details), new { id = m.IdMantenimiento });
    }

    // GET /Mantenimiento/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var m = await ObtenerPorIdAsync(id);
        if (m is null)
            return NotFound();

        return View(m);
    }

    // POST /Mantenimiento/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM MantenimientoProducto WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Mantenimiento WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        _cacheService.Invalidate();
        return RedirectToAction(nameof(Index));
    }

    // GET /Mantenimiento/Pdf/{id}
    public async Task<IActionResult> Pdf(int id)
    {
        var m = await ObtenerPorIdAsync(id);
        if (m is null)
            return NotFound();

        var bytes = _pdfService.GenerarProforma(m);
        return File(bytes, "application/pdf", $"Proforma_{m.Numero}.pdf");
    }

    // ----- Acceso a datos (ADO.NET puro, patrón ServicioMensual) -----

    /// <summary>Carga la cabecera con sus productos (maestro-detalle).</summary>
    private async Task<Mantenimiento?> ObtenerPorIdAsync(int id)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        Mantenimiento? mantenimiento = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, Estado, FechaCreacion " +
                "FROM Mantenimiento WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                mantenimiento = LeerCabecera(reader);
        }

        if (mantenimiento is null)
            return null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IdMantenimientoProducto, IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe " +
                "FROM MantenimientoProducto WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                mantenimiento.Productos.Add(LeerProducto(reader));
        }

        return mantenimiento;
    }

    private static async Task InsertarProductosAsync(
        LibSQLConnection conn, int idMantenimiento, IEnumerable<MantenimientoProducto> productos)
    {
        foreach (var p in productos)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO MantenimientoProducto (IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe) " +
                "VALUES (@IdMantenimiento, @Item, @Cantidad, @Descripcion, @PrecioUnitario, @Importe)";
            cmd.Parameters.AddWithValue("@IdMantenimiento", idMantenimiento);
            cmd.Parameters.AddWithValue("@Item", p.Item);
            cmd.Parameters.AddWithValue("@Cantidad", p.Cantidad);
            cmd.Parameters.AddWithValue("@Descripcion", p.Descripcion);
            cmd.Parameters.AddWithValue("@PrecioUnitario", (double)p.PrecioUnitario);
            cmd.Parameters.AddWithValue("@Importe", (double)p.Importe);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static void AgregarParametrosCabecera(LibSQLCommand cmd, Mantenimiento m)
    {
        cmd.Parameters.AddWithValue("@Numero", m.Numero);
        cmd.Parameters.AddWithValue("@Cliente", m.Cliente);
        cmd.Parameters.AddWithValue("@Telefono", m.Telefono);
        cmd.Parameters.AddWithValue("@Marca", m.Marca);
        cmd.Parameters.AddWithValue("@Modelo", m.Modelo);
        cmd.Parameters.AddWithValue("@Placa", m.Placa);
        cmd.Parameters.AddWithValue("@KM", m.KM);
        cmd.Parameters.AddWithValue("@Total", (double)m.Total);
        cmd.Parameters.AddWithValue("@Estado", m.Estado);
        cmd.Parameters.AddWithValue("@FechaCreacion", m.FechaCreacion.ToString(FechaFormato));
    }

    private static Mantenimiento LeerCabecera(System.Data.Common.DbDataReader r) => new()
    {
        IdMantenimiento = r.GetInt32(0),
        Numero = r.GetString(1),
        Cliente = r.GetString(2),
        Telefono = r.GetString(3),
        Marca = r.GetString(4),
        Modelo = r.GetString(5),
        Placa = r.GetString(6),
        KM = r.GetString(7),
        Total = LeerDecimal(r, 8),
        Estado = r.GetString(9),
        FechaCreacion = DateTime.Parse(r.GetString(10), CultureInfo.InvariantCulture)
    };

    private static MantenimientoProducto LeerProducto(System.Data.Common.DbDataReader r) => new()
    {
        IdMantenimientoProducto = r.GetInt32(0),
        IdMantenimiento = r.GetInt32(1),
        Item = r.GetString(2),
        Cantidad = r.GetInt32(3),
        Descripcion = r.GetString(4),
        PrecioUnitario = LeerDecimal(r, 5),
        Importe = LeerDecimal(r, 6)
    };

    private static decimal LeerDecimal(System.Data.Common.DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(r.GetValue(ordinal), CultureInfo.InvariantCulture);

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
