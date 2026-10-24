using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Nelknet.LibSQL.Data;
using TalleresRMP.Filters;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers;

[RequiereSesion]
public class MantenimientoController : Controller
{
    private const string FechaFormato = "yyyy-MM-dd HH:mm:ss";

    private readonly TursoService _turso;
    private readonly MantenimientoCacheService _cacheService;
    private readonly ProformaPdfService _pdfService;
    private readonly IConfiguration _configuration;
    private readonly ExcelImportService _importService;
    private readonly ExcelExportService _exportService;
    private readonly PdfImportService _pdfImportService;

    public MantenimientoController(
        TursoService turso,
        MantenimientoCacheService cacheService,
        ProformaPdfService pdfService,
        IConfiguration configuration,
        ExcelImportService importService,
        ExcelExportService exportService,
        PdfImportService pdfImportService)
    {
        _turso = turso;
        _cacheService = cacheService;
        _pdfService = pdfService;
        _configuration = configuration;
        _importService = importService;
        _exportService = exportService;
        _pdfImportService = pdfImportService;
    }

    // GET /Mantenimiento
    public async Task<IActionResult> Index(string? placa, string? desde, string? hasta, int pagina = 1)
    {
        const int porPagina = 10;
        pagina = Math.Max(1, pagina);

        // Valores por defecto: últimos 3 días hasta hoy (hora de Perú)
        var hoy = FechaHelper.Ahora;
        if (string.IsNullOrWhiteSpace(desde))
            desde = hoy.AddDays(-3).ToString("yyyy-MM-dd");
        if (string.IsNullOrWhiteSpace(hasta))
            hasta = hoy.ToString("yyyy-MM-dd");

        var where = ConstruirWhere(placa, desde, hasta);

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        int total;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM Mantenimiento {where}";
            AgregarParametrosFiltro(cmd, placa, desde, hasta);
            total = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        var lista = new List<Mantenimiento>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                $"SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, FechaCreacion, Descripcion " +
                $"FROM Mantenimiento {where} ORDER BY FechaCreacion DESC LIMIT @Limite OFFSET @Offset";
            AgregarParametrosFiltro(cmd, placa, desde, hasta);
            cmd.Parameters.AddWithValue("@Limite", porPagina);
            cmd.Parameters.AddWithValue("@Offset", (pagina - 1) * porPagina);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(LeerCabecera(reader));
        }

        ViewBag.Placa = placa ?? string.Empty;
        ViewBag.Desde = desde ?? string.Empty;
        ViewBag.Hasta = hasta ?? string.Empty;
        ViewBag.Pagina = pagina;
        ViewBag.TotalPaginas = (int)Math.Ceiling(total / (double)porPagina);
        ViewBag.Total = total;

        return View(lista);
    }

    private static string ConstruirWhere(string? placa, string? desde, string? hasta)
    {
        var condiciones = new List<string>();
        if (!string.IsNullOrWhiteSpace(placa))
            condiciones.Add("Placa LIKE @Placa");
        if (!string.IsNullOrWhiteSpace(desde))
            condiciones.Add("FechaCreacion >= @Desde");
        if (!string.IsNullOrWhiteSpace(hasta))
            condiciones.Add("FechaCreacion <= @Hasta");

        return condiciones.Count > 0 ? "WHERE " + string.Join(" AND ", condiciones) : "";
    }

    // GET /Mantenimiento/ExportarCompleto -- todas las columnas de Mantenimiento (respaldo), sin MantenimientoProducto
    [RequiereNivel("A")]
    public async Task<IActionResult> ExportarCompleto(string? placa, string? desde, string? hasta)
    {
        var lista = await ObtenerListaParaExportarAsync(placa, desde, hasta);
        var bytes = _exportService.ExportarCompleto(lista);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Mantenimientos_Completo_{FechaHelper.Ahora:yyyyMMdd_HHmm}.xlsx");
    }

    private async Task<List<Mantenimiento>> ObtenerListaParaExportarAsync(string? placa, string? desde, string? hasta)
    {
        var where = ConstruirWhere(placa, desde, hasta);

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        var lista = new List<Mantenimiento>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, FechaCreacion, Fotos, Descripcion, Precio " +
            $"FROM Mantenimiento {where} ORDER BY FechaCreacion DESC";
        AgregarParametrosFiltro(cmd, placa, desde, hasta);

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                lista.Add(LeerCabecerCompleta(reader));
        }

        // Productos manuales de esos mismos mantenimientos (hoja PRODUCTOS); el de servicio
        // no se exporta porque ya viaja en Descripcion/Precio.
        var porId = lista.ToDictionary(m => m.IdMantenimiento);
        using var cmdProductos = conn.CreateCommand();
        cmdProductos.CommandText =
            "SELECT IdMantenimientoProducto, IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe, EsServicio " +
            "FROM MantenimientoProducto " +
            $"WHERE EsServicio = 0 AND IdMantenimiento IN (SELECT IdMantenimiento FROM Mantenimiento {where}) " +
            "ORDER BY IdMantenimiento, CAST(Item AS INTEGER)";
        AgregarParametrosFiltro(cmdProductos, placa, desde, hasta);

        using (var reader = await cmdProductos.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var producto = LeerProducto(reader);
                if (porId.TryGetValue(producto.IdMantenimiento, out var m))
                    m.Productos.Add(producto);
            }
        }

        return lista;
    }

    private static void AgregarParametrosFiltro(LibSQLCommand cmd, string? placa, string? desde, string? hasta)
    {
        if (!string.IsNullOrWhiteSpace(placa))
            cmd.Parameters.AddWithValue("@Placa", $"%{placa}%");
        if (!string.IsNullOrWhiteSpace(desde))
            cmd.Parameters.AddWithValue("@Desde", desde);
        if (!string.IsNullOrWhiteSpace(hasta))
            cmd.Parameters.AddWithValue("@Hasta", hasta + " 23:59:59");
    }

    // GET /Mantenimiento/Create
    [RequiereNivel("A")]
    public async Task<IActionResult> Create()
    {
        var vm = new MantenimientoViewModel
        {
            Mantenimiento = new Mantenimiento
            {
                Numero = await ObtenerSiguienteNumeroAsync()
            },
            Productos = new List<MantenimientoProducto>()
        };
        return View(vm);
    }

    // POST /Mantenimiento/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiereNivel("A")]
    public async Task<IActionResult> Create(MantenimientoViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var m = vm.Mantenimiento;
        m.Numero = m.Numero.PadLeft(6, '0');   // el validador ya garantizó 1 a 6 dígitos
        m.FechaCreacion = FechaHelper.Ahora;
        m.Productos = vm.Productos ?? new List<MantenimientoProducto>();
        AgregarProductoServicio(m);
        RenumerarItems(m.Productos);
        RecalcularTotales(m);

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        int id;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Mantenimiento (Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, FechaCreacion, Fotos, Descripcion, Precio) " +
                "VALUES (@Numero, @Cliente, @Telefono, @Marca, @Modelo, @Placa, @KM, @Total, @FechaCreacion, @Fotos, @Descripcion, @Precio)";
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
    [RequiereNivel("A")]
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
    [RequiereNivel("A")]
    public async Task<IActionResult> Edit(MantenimientoViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var m = vm.Mantenimiento;
        m.Numero = m.Numero.PadLeft(6, '0');   // el validador ya garantizó 1 a 6 dígitos

        // Toma solo los productos manuales del grid; el de servicio siempre se regenera desde el textarea
        var manuales = (vm.Productos ?? new List<MantenimientoProducto>())
            .Where(p => p.EsServicio == 0)
            .ToList();

        m.Productos = manuales;
        AgregarProductoServicio(m);     // agrega EsServicio=1 al final si hay descripción
        RenumerarItems(m.Productos);    // renumera 1..N con el de servicio siempre último
        RecalcularTotales(m);

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE Mantenimiento SET " +
                "Numero=@Numero, Cliente=@Cliente, Telefono=@Telefono, Marca=@Marca, Modelo=@Modelo, " +
                "Placa=@Placa, KM=@KM, Total=@Total, Fotos=@Fotos, Descripcion=@Descripcion, Precio=@Precio " +
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
    [RequiereNivel("A")]
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
    [RequiereNivel("A")]
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

    // GET /Mantenimiento/Importar
    [RequiereNivel("A")]
    public IActionResult Importar() => View();

    // POST /Mantenimiento/Importar
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiereNivel("A")]
    public async Task<IActionResult> Importar(List<IFormFile> archivos)
    {
        archivos = archivos.Where(a => a.Length > 0).ToList();
        if (archivos.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Selecciona uno o más archivos Excel (.xlsx) o PDF de proforma.");
            return View();
        }

        // Junta las filas de todos los archivos; los que no se pueden leer se listan en el resultado.
        var filas = new List<FilaImportada>();
        var noReconocidos = new List<string>();
        int productosOmitidos = 0;

        foreach (var archivo in archivos)
        {
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            try
            {
                using var stream = archivo.OpenReadStream();
                if (extension == ".xlsx")
                {
                    var datos = _importService.ParseArchivo(stream);
                    filas.AddRange(datos.Filas);
                    productosOmitidos += datos.ProductosOmitidos;
                }
                else if (extension == ".pdf" && _pdfImportService.ParseProforma(stream, out var omitidosPdf) is { } fila)
                {
                    filas.Add(fila);
                    productosOmitidos += omitidosPdf;
                }
                else
                {
                    noReconocidos.Add(archivo.FileName);
                }
            }
            catch (Exception)
            {
                // Archivo dañado o con otro formato: no frena el resto de la importación.
                noReconocidos.Add(archivo.FileName);
            }
        }

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        var numerosExistentes = new HashSet<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Numero FROM Mantenimiento";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                numerosExistentes.Add(reader.GetString(0));
        }

        int importados = 0, omitidosDuplicados = 0, omitidosSinNumero = 0, productosImportados = 0;

        foreach (var fila in filas)
        {
            if (string.IsNullOrWhiteSpace(fila.Numero))
            {
                omitidosSinNumero++;
                continue;
            }

            if (numerosExistentes.Contains(fila.Numero))
            {
                omitidosDuplicados++;
                continue;
            }

            var m = new Mantenimiento
            {
                Numero = fila.Numero,
                Cliente = fila.Cliente,
                Telefono = fila.Telefono,
                Marca = fila.Marca,
                Modelo = fila.Modelo,
                Placa = fila.Placa,
                KM = fila.KM,
                FechaCreacion = fila.Fecha,
                Fotos = fila.Fotos,
                Descripcion = fila.Descripcion,
                Precio = fila.Precio,
                // Productos manuales (hoja PRODUCTOS del Excel o tabla del PDF)
                Productos = fila.Productos.ToList()
            };
            productosImportados += m.Productos.Count;

            AgregarProductoServicio(m);
            RenumerarItems(m.Productos);
            RecalcularTotales(m);

            int id;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO Mantenimiento (Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, FechaCreacion, Fotos, Descripcion, Precio) " +
                    "VALUES (@Numero, @Cliente, @Telefono, @Marca, @Modelo, @Placa, @KM, @Total, @FechaCreacion, @Fotos, @Descripcion, @Precio)";
                AgregarParametrosCabecera(cmd, m);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT last_insert_rowid()";
                id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            await InsertarProductosAsync(conn, id, m.Productos);
            numerosExistentes.Add(fila.Numero);
            importados++;
        }

        _cacheService.Invalidate();

        ViewBag.Importados = importados;
        ViewBag.OmitidosDuplicados = omitidosDuplicados;
        ViewBag.OmitidosSinNumero = omitidosSinNumero;
        ViewBag.ProductosImportados = productosImportados;
        ViewBag.ProductosOmitidos = productosOmitidos;
        ViewBag.NoReconocidos = noReconocidos;
        return View("ImportarResultado");
    }

    // GET /Mantenimiento/Pdf/{id}
    [RequiereNivel("A")]
    public async Task<IActionResult> Pdf(int id)
    {
        var m = await ObtenerPorIdAsync(id);
        if (m is null)
            return NotFound();

        var bytes = _pdfService.GenerarProforma(m);
        return File(bytes, "application/pdf", $"Proforma_{m.Numero}.pdf");
    }

    // ----- Acceso a datos (ADO.NET puro) -----

    private async Task<Mantenimiento?> ObtenerPorIdAsync(int id)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        Mantenimiento? mantenimiento = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, FechaCreacion, Fotos, Descripcion, Precio " +
                "FROM Mantenimiento WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                mantenimiento = LeerCabecerCompleta(reader);
        }

        if (mantenimiento is null)
            return null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IdMantenimientoProducto, IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe, EsServicio " +
                "FROM MantenimientoProducto WHERE IdMantenimiento=@Id ORDER BY EsServicio ASC, Item ASC";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                mantenimiento.Productos.Add(LeerProducto(reader));
        }

        return mantenimiento;
    }

    private async Task<string> ObtenerSiguienteNumeroAsync()
    {
        int inicio = _configuration.GetValue<int?>("Numeracion:Inicio") ?? 1;

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(CAST(Numero AS INTEGER)), 0) FROM Mantenimiento";
        var maxActual = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        int siguiente = Math.Max(maxActual, inicio - 1) + 1;
        return siguiente.ToString("D6");
    }

    // Agrega al final el producto de servicio generado desde Mantenimiento.Descripcion (EsServicio=1)
    private static void AgregarProductoServicio(Mantenimiento m)
    {
        if (string.IsNullOrWhiteSpace(m.Descripcion))
            return;

        var precio = m.Precio ?? 0m;
        m.Productos.Add(new MantenimientoProducto
        {
            Item = string.Empty,   // RenumerarItems asigna el número correcto después
            Cantidad = 1,
            Descripcion = m.Descripcion,
            PrecioUnitario = precio,
            Importe = precio,
            EsServicio = 1
        });
    }

    // Renumera items 1..N garantizando que EsServicio=1 quede siempre al final
    private static void RenumerarItems(List<MantenimientoProducto> productos)
    {
        var ordenados = productos
            .OrderBy(p => p.EsServicio)
            .ToList();

        for (int i = 0; i < ordenados.Count; i++)
            ordenados[i].Item = (i + 1).ToString();

        productos.Clear();
        productos.AddRange(ordenados);
    }

    private static async Task InsertarProductosAsync(
        LibSQLConnection conn, int idMantenimiento, IEnumerable<MantenimientoProducto> productos)
    {
        foreach (var p in productos)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO MantenimientoProducto (IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe, EsServicio) " +
                "VALUES (@IdMantenimiento, @Item, @Cantidad, @Descripcion, @PrecioUnitario, @Importe, @EsServicio)";
            cmd.Parameters.AddWithValue("@IdMantenimiento", idMantenimiento);
            cmd.Parameters.AddWithValue("@Item", p.Item);
            cmd.Parameters.AddWithValue("@Cantidad", p.Cantidad);
            cmd.Parameters.AddWithValue("@Descripcion", p.Descripcion);
            cmd.Parameters.AddWithValue("@PrecioUnitario", (double)p.PrecioUnitario);
            cmd.Parameters.AddWithValue("@Importe", (double)p.Importe);
            cmd.Parameters.AddWithValue("@EsServicio", p.EsServicio);
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
        cmd.Parameters.AddWithValue("@FechaCreacion", m.FechaCreacion.ToString(FechaFormato));
        cmd.Parameters.AddWithValue("@Fotos", (object?)m.Fotos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)m.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Precio", m.Precio.HasValue ? (object)(double)m.Precio.Value : DBNull.Value);
    }

    // Lector para Index (incluye Descripcion para mostrarse en la lista)
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
        FechaCreacion = DateTime.Parse(r.GetString(9), CultureInfo.InvariantCulture),
        Descripcion = r.IsDBNull(10) ? null : r.GetString(10)
    };

    // Lector completo para Details / Edit / Pdf
    private static Mantenimiento LeerCabecerCompleta(System.Data.Common.DbDataReader r) => new()
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
        FechaCreacion = DateTime.Parse(r.GetString(9), CultureInfo.InvariantCulture),
        Fotos = r.IsDBNull(10) ? null : r.GetString(10),
        Descripcion = r.IsDBNull(11) ? null : r.GetString(11),
        Precio = r.IsDBNull(12) ? null : (decimal?)Convert.ToDecimal(r.GetValue(12), CultureInfo.InvariantCulture)
    };

    private static MantenimientoProducto LeerProducto(System.Data.Common.DbDataReader r) => new()
    {
        IdMantenimientoProducto = r.GetInt32(0),
        IdMantenimiento = r.GetInt32(1),
        Item = r.GetString(2),
        Cantidad = r.GetInt32(3),
        Descripcion = r.GetString(4),
        PrecioUnitario = LeerDecimal(r, 5),
        Importe = LeerDecimal(r, 6),
        EsServicio = r.IsDBNull(7) ? 0 : r.GetInt32(7)
    };

    private static decimal LeerDecimal(System.Data.Common.DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(r.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static void RecalcularTotales(Mantenimiento m)
    {
        foreach (var p in m.Productos)
            p.Importe = p.Cantidad * p.PrecioUnitario;

        m.Total = m.Productos.Sum(p => p.Importe);
    }
}
