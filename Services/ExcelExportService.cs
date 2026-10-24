using ClosedXML.Excel;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

public class ExcelExportService
{
    // Diseño copiado de la hoja "HIST. ACTIVIDADES" del taller: banda verde con
    // el título, encabezados celestes y filas con bordes punteados (sin filtros).
    private static readonly XLColor VerdeBanda = XLColor.FromHtml("#99FF66");
    private static readonly XLColor CelesteEncabezado = XLColor.FromHtml("#B7DEE8");

    // Nombres de hoja que busca ExcelImportService.
    public const string HojaHistorial = "HIST. ACTIVIDADES";
    public const string HojaProductos = "PRODUCTOS";

    // La tabla arranca en B5 (columna A y fila 1 quedan de margen, como en el original).
    private const int ColInicio = 2;
    private const int FilaBanda = 2;
    private const int FilaEncabezado = 5;

    // Mismos encabezados y orden que la hoja del taller, para que Importar los reconozca
    // (busca FECHA/PLACA/KILOMETRO/TRABAJO/PROF); los campos que esa hoja no tiene van al final.
    private const string ColIt = "It";
    private const string ColFecha = "Fecha de Ejecución";
    private const string ColKm = "Kilometro";
    private const string ColTrabajo = "Trabajo realizado";
    private const string ColProforma = "Prof. y/o cot.";
    private const string ColObservaciones = "Observaciones / fallas Ocultas";
    private const string ColEnlace = "Enlace de imágenes";

    private static readonly (string Nombre, double Ancho)[] Columnas =
    {
        (ColIt, 8), (ColFecha, 14), ("Placa", 12), ("Marca", 16), ("Modelo", 16),
        (ColKm, 12), (ColTrabajo, 90), (ColProforma, 12), (ColObservaciones, 30), (ColEnlace, 40),
        ("Cliente", 25), ("Telefono", 12), ("Total", 12), ("Precio", 12)
    };

    // Hoja PRODUCTOS: solo los productos manuales; el de servicio ya está en "Trabajo realizado".
    private const string ColDescripcion = "Descripción";
    private static readonly (string Nombre, double Ancho)[] ColumnasProductos =
    {
        (ColProforma, 12), ("Item", 8), ("Cant.", 8), (ColDescripcion, 70), ("P. Unit", 12), ("Importe", 12)
    };

    // Respaldo completo: hoja de mantenimientos + hoja con sus productos manuales.
    public byte[] ExportarCompleto(IEnumerable<Mantenimiento> mantenimientos)
    {
        var lista = mantenimientos.ToList();

        using var wb = new XLWorkbook();
        ComponerHistorial(wb.Worksheets.Add(HojaHistorial), lista);
        ComponerProductos(wb.Worksheets.Add(HojaProductos), lista);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void ComponerHistorial(IXLWorksheet ws, List<Mantenimiento> mantenimientos)
    {
        int fila = FilaEncabezado + 1;
        foreach (var m in mantenimientos)
        {
            int c = ColInicio;
            ws.Cell(fila, c++).Value = m.IdMantenimiento;
            ws.Cell(fila, c++).Value = m.FechaCreacion;
            ws.Cell(fila, c++).Value = m.Placa;
            ws.Cell(fila, c++).Value = m.Marca;
            ws.Cell(fila, c++).Value = m.Modelo;
            ws.Cell(fila, c++).Value = m.KM;
            ws.Cell(fila, c++).Value = m.Descripcion ?? string.Empty;
            ws.Cell(fila, c++).Value = "P." + m.Numero;   // como la hoja; Importar se queda solo con los dígitos
            c++;                                          // Observaciones: no existe en la base, va vacía
            ws.Cell(fila, c++).Value = m.Fotos ?? string.Empty;
            ws.Cell(fila, c++).Value = m.Cliente;
            ws.Cell(fila, c++).Value = m.Telefono;
            ws.Cell(fila, c++).Value = (double)m.Total;
            if (m.Precio.HasValue)
                ws.Cell(fila, c).Value = (double)m.Precio.Value;
            fila++;
        }

        var columna = ComponerHoja(ws, "HISTORIAL DE ACTIVIDADES EJECUTADAS", Columnas, fila - 1);

        // Texto libre a la izquierda (como "Trabajo realizado"); la descripción ajusta líneas.
        foreach (var nombre in new[] { ColKm, ColTrabajo, ColProforma, ColObservaciones, ColEnlace, "Cliente" })
            columna(nombre).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        columna(ColTrabajo).Style.Alignment.WrapText = true;

        foreach (var nombre in new[] { "Total", "Precio" })
            FormatoMonto(columna(nombre));

        // Se ve solo la fecha, como la hoja; la celda conserva la hora para reimportarla.
        columna(ColFecha).Style.DateFormat.Format = "dd/mm/yyyy";
    }

    private static void ComponerProductos(IXLWorksheet ws, List<Mantenimiento> mantenimientos)
    {
        int fila = FilaEncabezado + 1;
        foreach (var m in mantenimientos)
        {
            foreach (var p in m.Productos.Where(p => p.EsServicio == 0))
            {
                int c = ColInicio;
                ws.Cell(fila, c++).Value = "P." + m.Numero;   // une el producto con su fila de HIST. ACTIVIDADES
                ws.Cell(fila, c++).Value = p.Item;
                ws.Cell(fila, c++).Value = p.Cantidad;
                ws.Cell(fila, c++).Value = p.Descripcion;
                ws.Cell(fila, c++).Value = (double)p.PrecioUnitario;
                ws.Cell(fila, c).Value = (double)p.Importe;
                fila++;
            }
        }

        var columna = ComponerHoja(ws, "DETALLE DE PRODUCTOS", ColumnasProductos, fila - 1);

        foreach (var nombre in new[] { ColProforma, ColDescripcion })
            columna(nombre).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        columna(ColDescripcion).Style.Alignment.WrapText = true;

        foreach (var nombre in new[] { "P. Unit", "Importe" })
            FormatoMonto(columna(nombre));
    }

    // Arma banda, encabezados y cuerpo punteado; devuelve un lector de la columna
    // de datos por nombre para aplicar los formatos propios de cada hoja.
    private static Func<string, IXLRange> ComponerHoja(
        IXLWorksheet ws, string titulo, (string Nombre, double Ancho)[] columnas, int ultimaFila)
    {
        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 10;

        int colFin = ColInicio + columnas.Length - 1;
        int filaIni = FilaEncabezado + 1;
        int filaFin = Math.Max(ultimaFila, filaIni);

        ComponerBanda(ws, titulo, colFin);
        ComponerEncabezados(ws, columnas);

        var cuerpo = ws.Range(filaIni, ColInicio, filaFin, colFin);
        cuerpo.Style.Border.InsideBorder = XLBorderStyleValues.Dotted;
        cuerpo.Style.Border.OutsideBorder = XLBorderStyleValues.Dotted;
        cuerpo.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        cuerpo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Los encabezados quedan fijos al hacer scroll, como en la hoja del taller.
        ws.SheetView.FreezeRows(FilaEncabezado);

        return nombre =>
        {
            int col = ColInicio + Array.FindIndex(columnas, c => c.Nombre == nombre);
            return ws.Range(filaIni, col, filaFin, col);
        };
    }

    // Banda verde con borde grueso y el título alineado a la derecha (filas 2-3).
    private static void ComponerBanda(IXLWorksheet ws, string titulo, int colFin)
    {
        ws.Column(1).Width = 2;
        ws.Row(FilaBanda).Height = 30;
        ws.Row(FilaBanda + 1).Height = 30;
        ws.Row(FilaEncabezado - 1).Height = 6;

        var banda = ws.Range(FilaBanda, ColInicio, FilaBanda + 1, colFin).Merge();
        banda.Value = titulo;
        banda.Style.Fill.BackgroundColor = VerdeBanda;
        banda.Style.Font.Bold = true;
        banda.Style.Font.FontSize = 20;
        banda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        banda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        banda.Style.Alignment.Indent = 1;
        banda.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
    }

    // Encabezados celestes, en negrita y centrados; sin autofiltro.
    private static void ComponerEncabezados(IXLWorksheet ws, (string Nombre, double Ancho)[] columnas)
    {
        for (int i = 0; i < columnas.Length; i++)
        {
            ws.Cell(FilaEncabezado, ColInicio + i).Value = columnas[i].Nombre;
            ws.Column(ColInicio + i).Width = columnas[i].Ancho;
        }

        var encabezado = ws.Range(FilaEncabezado, ColInicio, FilaEncabezado, ColInicio + columnas.Length - 1);
        encabezado.Style.Fill.BackgroundColor = CelesteEncabezado;
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        encabezado.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        encabezado.Style.Alignment.WrapText = true;
        encabezado.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        encabezado.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        ws.Row(FilaEncabezado).Height = 30;
    }

    private static void FormatoMonto(IXLRange rango)
    {
        rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        rango.Style.NumberFormat.Format = "#,##0.00";
    }
}
