using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

public record FilaImportada(
    DateTime Fecha,
    string Placa,
    string Marca,
    string Modelo,
    string KM,
    string Descripcion,
    string Numero,
    string Cliente,
    string Telefono,
    string? Fotos,
    decimal? Precio,
    List<MantenimientoProducto> Productos
);

// Filas de HIST. ACTIVIDADES (cada una con sus productos manuales) y cuántas filas
// de PRODUCTOS se descartaron por inválidas o por no tener su mantenimiento en el archivo.
public record ArchivoImportado(List<FilaImportada> Filas, int ProductosOmitidos);

/// <summary>
/// Parsea la hoja "HIST. ACTIVIDADES" del Excel de historial de mantenimientos
/// del taller. Reglas acordadas: se ignora Observaciones;
/// de "Prof. y/o cot." (ej. "P.001016") solo se usan los dígitos; filas sin
/// Placa/Fecha pero con "Trabajo realizado" son continuación de la fila
/// anterior (se concatenan con un espacio); Kilometro vacío -> "0".
/// "Enlace de imágenes" ("En lace" en la hoja del taller) va a Fotos; Cliente,
/// Telefono y Precio solo vienen en el Excel que exporta la app. Si la columna
/// no existe o la celda está vacía, se ignoran.
/// La hoja "PRODUCTOS" (solo en el Excel exportado) trae los productos manuales;
/// el de servicio no viaja ahí: se regenera desde "Trabajo realizado" + Precio.
/// </summary>
public class ExcelImportService
{
    private const string NombreHoja = ExcelExportService.HojaHistorial;
    private const string NombreHojaProductos = ExcelExportService.HojaProductos;

    public ArchivoImportado ParseArchivo(Stream archivo)
    {
        using var wb = new XLWorkbook(archivo);

        var filas = ParseHistorialActividades(wb);
        var (productos, omitidos) = wb.Worksheets.Contains(NombreHojaProductos)
            ? ParseProductos(wb.Worksheet(NombreHojaProductos))
            : (new List<(string Numero, MantenimientoProducto Producto)>(), 0);

        // Asocia cada producto a su mantenimiento por el número de proforma.
        var porNumero = filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Numero))
            .GroupBy(f => f.Numero)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (numero, producto) in productos)
        {
            if (!porNumero.TryGetValue(numero, out var destinos))
            {
                omitidos++;
                continue;
            }

            foreach (var fila in destinos)
                fila.Productos.Add(producto);
        }

        return new ArchivoImportado(filas, omitidos);
    }

    private static List<FilaImportada> ParseHistorialActividades(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Contains(NombreHoja) ? wb.Worksheet(NombreHoja) : wb.Worksheets.First();

        var range = ws.RangeUsed();
        if (range is null)
            return new List<FilaImportada>();

        var columnas = DetectarColumnas(range);

        var resultado = new List<FilaImportada>();
        FilaImportada? actual = null;

        for (int r = 1; r <= range.RowCount(); r++)
        {
            var row = range.Row(r);

            string placa = Texto(row.Cell(columnas.Placa));
            string marca = Texto(row.Cell(columnas.Marca));
            string modelo = Texto(row.Cell(columnas.Modelo));
            string km = Texto(row.Cell(columnas.Kilometro));
            string trabajo = Texto(row.Cell(columnas.Trabajo));
            string cotizacion = Texto(row.Cell(columnas.Cotizacion));
            DateTime? fecha = FechaONull(row.Cell(columnas.Fecha));
            string cliente = TextoOpcional(row, columnas.Cliente);
            string telefono = TextoOpcional(row, columnas.Telefono);
            string fotos = TextoOpcional(row, columnas.Enlace);
            decimal? precio = columnas.Precio is null ? null : DecimalONull(row.Cell(columnas.Precio.Value));

            bool esFilaNueva = fecha is not null || !string.IsNullOrWhiteSpace(placa);
            bool esContinuacion = !esFilaNueva && !string.IsNullOrWhiteSpace(trabajo);

            if (esContinuacion && actual is not null)
            {
                actual = actual with { Descripcion = (actual.Descripcion + " " + trabajo).Trim() };
                resultado[^1] = actual;
                continue;
            }

            if (!esFilaNueva)
                continue; // fila totalmente vacía (plantilla sin usar)

            if (r == columnas.FilaEncabezado)
                continue; // es la fila de títulos, no un registro

            actual = new FilaImportada(
                Fecha: fecha ?? FechaHelper.Ahora,
                Placa: placa,
                Marca: marca,
                Modelo: modelo,
                KM: string.IsNullOrWhiteSpace(km) ? "0" : km,
                Descripcion: trabajo,
                Numero: SoloDigitos(cotizacion),
                Cliente: cliente,
                Telefono: telefono,
                Fotos: string.IsNullOrWhiteSpace(fotos) ? null : fotos,
                Precio: precio,
                Productos: new List<MantenimientoProducto>()
            );
            resultado.Add(actual);
        }

        return resultado;
    }

    // Lee los productos manuales en el orden de la hoja (Item e Importe se recalculan al grabar).
    // Fila sin número, cantidad entera >= 1, P. Unit numérico o descripción -> se omite.
    private static (List<(string Numero, MantenimientoProducto Producto)> Productos, int Omitidos) ParseProductos(IXLWorksheet ws)
    {
        var productos = new List<(string, MantenimientoProducto)>();

        var range = ws.RangeUsed();
        var columnas = range is null ? null : DetectarColumnasProductos(range);
        if (range is null || columnas is null)
            return (productos, 0); // hoja vacía o sin encabezados reconocibles: se ignora

        int omitidos = 0;
        for (int r = columnas.FilaEncabezado + 1; r <= range.RowCount(); r++)
        {
            var row = range.Row(r);

            string numero = SoloDigitos(Texto(row.Cell(columnas.Cotizacion)));
            string descripcion = Texto(row.Cell(columnas.Descripcion));
            int? cantidad = EnteroONull(row.Cell(columnas.Cantidad));
            decimal? precioUnitario = DecimalONull(row.Cell(columnas.PrecioUnitario));

            bool vacia = string.IsNullOrWhiteSpace(numero) && string.IsNullOrWhiteSpace(descripcion)
                         && row.Cell(columnas.Cantidad).IsEmpty() && row.Cell(columnas.PrecioUnitario).IsEmpty();
            if (vacia)
                continue;

            if (string.IsNullOrWhiteSpace(numero) || string.IsNullOrWhiteSpace(descripcion)
                || cantidad is null or < 1 or > 9999 || precioUnitario is null)
            {
                omitidos++;
                continue;
            }

            productos.Add((numero, new MantenimientoProducto
            {
                Cantidad = cantidad.Value,
                Descripcion = descripcion,
                PrecioUnitario = precioUnitario.Value,
                EsServicio = 0
            }));
        }

        return (productos, omitidos);
    }

    // Cliente/Telefono/Enlace/Precio son opcionales: null cuando el Excel no trae esa columna.
    private record ColumnasHistorial(int Fecha, int Placa, int Marca, int Modelo, int Kilometro, int Trabajo, int Cotizacion, int FilaEncabezado,
        int? Cliente = null, int? Telefono = null, int? Enlace = null, int? Precio = null);

    private record ColumnasProductos(int Cotizacion, int Cantidad, int Descripcion, int PrecioUnitario, int FilaEncabezado);

    // Busca la fila de encabezados por el texto de las columnas (en vez de asumir
    // posiciones fijas), porque el rango usado de la hoja puede empezar en
    // columnas distintas según el archivo (A en el original, C en uno exportado).
    private static ColumnasHistorial DetectarColumnas(IXLRange range)
    {
        for (int r = 1; r <= range.RowCount(); r++)
        {
            var row = range.Row(r);
            int? fecha = null, placa = null, marca = null, modelo = null, km = null, trabajo = null, cotizacion = null;
            int? cliente = null, telefono = null, enlace = null, precio = null;

            for (int c = 1; c <= range.ColumnCount(); c++)
            {
                var texto = Texto(row.Cell(c)).ToUpperInvariant();
                if (texto.StartsWith("FECHA")) fecha ??= c;
                else if (texto == "PLACA") placa ??= c;
                else if (texto == "MARCA") marca ??= c;
                else if (texto == "MODELO") modelo ??= c;
                else if (texto.StartsWith("KILOMETRO")) km ??= c;
                else if (texto.StartsWith("TRABAJO")) trabajo ??= c;
                else if (texto.StartsWith("PROF")) cotizacion ??= c;
                else if (texto == "CLIENTE") cliente ??= c;
                else if (texto == "TELEFONO") telefono ??= c;
                else if (texto.StartsWith("ENLACE") || texto.StartsWith("EN LACE")) enlace ??= c;   // la hoja del taller lo escribe "En lace"
                else if (texto == "PRECIO") precio ??= c;
            }

            if (fecha is not null && placa is not null && trabajo is not null)
                return new ColumnasHistorial(fecha.Value, placa.Value, marca ?? placa.Value + 1,
                    modelo ?? placa.Value + 2, km ?? placa.Value + 3, trabajo.Value, cotizacion ?? trabajo.Value + 1, r,
                    cliente, telefono, enlace, precio);
        }

        // Sin encabezados reconocibles: asume el layout original (C..I) como último recurso.
        return new ColumnasHistorial(3, 4, 5, 6, 7, 8, 9, 0);
    }

    // Igual que DetectarColumnas, pero sin layout de respaldo: sin las 4 columnas no hay productos.
    private static ColumnasProductos? DetectarColumnasProductos(IXLRange range)
    {
        for (int r = 1; r <= range.RowCount(); r++)
        {
            var row = range.Row(r);
            int? cotizacion = null, cantidad = null, descripcion = null, precioUnitario = null;

            for (int c = 1; c <= range.ColumnCount(); c++)
            {
                var texto = Texto(row.Cell(c)).ToUpperInvariant();
                if (texto.StartsWith("PROF")) cotizacion ??= c;
                else if (texto.StartsWith("CANT")) cantidad ??= c;
                else if (texto.StartsWith("DESCRIP")) descripcion ??= c;
                else if (texto.StartsWith("P. UNIT") || texto.StartsWith("P.UNIT")) precioUnitario ??= c;
            }

            if (cotizacion is not null && cantidad is not null && descripcion is not null && precioUnitario is not null)
                return new ColumnasProductos(cotizacion.Value, cantidad.Value, descripcion.Value, precioUnitario.Value, r);
        }

        return null;
    }

    private static string Texto(IXLCell cell) =>
        cell.IsEmpty() ? string.Empty : cell.GetFormattedString().Trim();

    private static string TextoOpcional(IXLRangeRow row, int? columna) =>
        columna is null ? string.Empty : Texto(row.Cell(columna.Value));

    // Precio vacío o no numérico -> null (se ignora, igual que si la columna no existiera).
    private static decimal? DecimalONull(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        return decimal.TryParse(cell.GetFormattedString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    // Cantidad: solo enteros (1.5 o texto -> null, la fila se omite).
    private static int? EnteroONull(IXLCell cell)
    {
        var valor = DecimalONull(cell);
        return valor is decimal v && v == decimal.Truncate(v) && v >= int.MinValue && v <= int.MaxValue ? (int)v : null;
    }

    private static DateTime? FechaONull(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime();

        return DateTime.TryParse(cell.GetFormattedString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f
            : null;
    }

    private static string SoloDigitos(string texto)
    {
        var digitos = Regex.Match(texto, @"\d+").Value;
        return digitos;
    }
}
