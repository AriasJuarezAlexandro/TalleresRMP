using ClosedXML.Excel;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

public class ExcelExportService
{
    // Respaldo completo: todas las columnas de Mantenimiento (sin MantenimientoProducto).
    public byte[] ExportarCompleto(IEnumerable<Mantenimiento> mantenimientos)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Mantenimiento");

        string[] encabezados =
        {
            "IdMantenimiento", "Numero", "Cliente", "Telefono", "Marca", "Modelo",
            "Placa", "KM", "Total", "FechaCreacion", "Fotos", "Descripcion", "Precio"
        };

        for (int c = 0; c < encabezados.Length; c++)
            ws.Cell(1, c + 1).Value = encabezados[c];
        ws.Row(1).Style.Font.Bold = true;

        int fila = 2;
        foreach (var m in mantenimientos)
        {
            ws.Cell(fila, 1).Value = m.IdMantenimiento;
            ws.Cell(fila, 2).Value = m.Numero;
            ws.Cell(fila, 3).Value = m.Cliente;
            ws.Cell(fila, 4).Value = m.Telefono;
            ws.Cell(fila, 5).Value = m.Marca;
            ws.Cell(fila, 6).Value = m.Modelo;
            ws.Cell(fila, 7).Value = m.Placa;
            ws.Cell(fila, 8).Value = m.KM;
            ws.Cell(fila, 9).Value = (double)m.Total;
            ws.Cell(fila, 10).Value = m.FechaCreacion;
            ws.Cell(fila, 10).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
            ws.Cell(fila, 11).Value = m.Fotos ?? string.Empty;
            ws.Cell(fila, 12).Value = m.Descripcion ?? string.Empty;
            if (m.Precio.HasValue)
                ws.Cell(fila, 13).Value = (double)m.Precio.Value;
            fila++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
