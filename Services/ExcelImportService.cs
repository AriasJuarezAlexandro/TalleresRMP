using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace TalleresRMP.Services;

public record FilaImportada(
    DateTime Fecha,
    string Placa,
    string Marca,
    string Modelo,
    string KM,
    string Descripcion,
    string Numero
);

/// <summary>
/// Parsea la hoja "HIST. ACTIVIDADES" del Excel de historial de mantenimientos
/// del taller. Reglas acordadas: se ignoran Observaciones y Enlace de imágenes;
/// de "Prof. y/o cot." (ej. "P.001016") solo se usan los dígitos; filas sin
/// Placa/Fecha pero con "Trabajo realizado" son continuación de la fila
/// anterior (se concatenan con un espacio); Kilometro vacío -> "0";
/// Cliente/Telefono no existen en el Excel -> "".
/// </summary>
public class ExcelImportService
{
    private const string NombreHoja = "HIST. ACTIVIDADES";

    public List<FilaImportada> ParseHistorialActividades(Stream archivo)
    {
        using var wb = new XLWorkbook(archivo);
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
                Fecha: fecha ?? DateTime.Now,
                Placa: placa,
                Marca: marca,
                Modelo: modelo,
                KM: string.IsNullOrWhiteSpace(km) ? "0" : km,
                Descripcion: trabajo,
                Numero: SoloDigitos(cotizacion)
            );
            resultado.Add(actual);
        }

        return resultado;
    }

    private record ColumnasHistorial(int Fecha, int Placa, int Marca, int Modelo, int Kilometro, int Trabajo, int Cotizacion, int FilaEncabezado);

    // Busca la fila de encabezados por el texto de las columnas (en vez de asumir
    // posiciones fijas), porque el rango usado de la hoja puede empezar en
    // columnas distintas según el archivo (A en el original, C en uno exportado).
    private static ColumnasHistorial DetectarColumnas(IXLRange range)
    {
        for (int r = 1; r <= range.RowCount(); r++)
        {
            var row = range.Row(r);
            int? fecha = null, placa = null, marca = null, modelo = null, km = null, trabajo = null, cotizacion = null;

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
            }

            if (fecha is not null && placa is not null && trabajo is not null)
                return new ColumnasHistorial(fecha.Value, placa.Value, marca ?? placa.Value + 1,
                    modelo ?? placa.Value + 2, km ?? placa.Value + 3, trabajo.Value, cotizacion ?? trabajo.Value + 1, r);
        }

        // Sin encabezados reconocibles: asume el layout original (C..I) como último recurso.
        return new ColumnasHistorial(3, 4, 5, 6, 7, 8, 9, 0);
    }

    private static string Texto(IXLCell cell) =>
        cell.IsEmpty() ? string.Empty : cell.GetFormattedString().Trim();

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
