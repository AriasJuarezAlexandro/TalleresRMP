using System.Globalization;
using System.Text.RegularExpressions;
using TalleresRMP.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace TalleresRMP.Services;

/// <summary>
/// Lee una proforma PDF generada por ProformaPdfService y la convierte en una
/// FilaImportada. Cabecera: por etiquetas ("Cliente:", "Placa:"...). Tabla: entre
/// el encabezado "Item Cant. Descripción..." y "TOTAL"; una fila empieza cuando hay
/// texto en la columna Item y las líneas que solo traen Descripción son su
/// continuación. El ítem "N (S)" es el de servicio (va a Descripcion/Precio); los PDF
/// anteriores a esa marca no lo distinguen y todas sus filas entran como manuales.
/// </summary>
public class PdfImportService
{
    // Etiquetas de la columna derecha del recuadro de datos: cortan el valor de la izquierda.
    private static readonly HashSet<string> EtiquetasDerecha = new() { "Teléfono:", "Placa:", "Fecha:" };

    private record FilaTabla(string Item, string Cantidad, List<string> Descripcion, string PrecioUnitario);

    // Devuelve null si el archivo no es una proforma de la app (sin N° o sin tabla).
    // productosOmitidos: filas de la tabla con cantidad o precio ilegible.
    public FilaImportada? ParseProforma(Stream archivo, out int productosOmitidos)
    {
        productosOmitidos = 0;

        using var doc = PdfDocument.Open(archivo);
        var lineasPrimera = Lineas(doc.GetPage(1));

        string numero = SoloDigitos(ValorDespuesDe(lineasPrimera, "N°"));
        var filasTabla = LeerTabla(doc);
        if (string.IsNullOrEmpty(numero) || filasTabla is null)
            return null;

        // "Marca / Modelo: Toyota YARIS" -> la marca es la primera palabra, el modelo el resto.
        var marcaModelo = ValorDespuesDe(lineasPrimera, "Marca", "/", "Modelo:").Split(' ', 2);
        string km = ValorDespuesDe(lineasPrimera, "KM:");
        DateTime? fecha = DateTime.TryParseExact(ValorDespuesDe(lineasPrimera, "Fecha:"), "dd/MM/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

        var productos = new List<MantenimientoProducto>();
        string descripcionServicio = string.Empty;
        decimal? precioServicio = null;

        foreach (var fila in filasTabla)
        {
            bool esServicio = fila.Item.Contains("(S)");
            string descripcion = string.Join(" ", fila.Descripcion);
            bool cantidadOk = int.TryParse(fila.Cantidad, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cantidad);
            bool precioOk = decimal.TryParse(fila.PrecioUnitario, NumberStyles.Number, CultureInfo.InvariantCulture, out var precio);

            if (!cantidadOk || cantidad < 1 || !precioOk || string.IsNullOrWhiteSpace(descripcion))
            {
                productosOmitidos++;
                continue;
            }

            // El servicio se regenera al grabar desde Descripcion/Precio, como en Crear/Editar.
            if (esServicio)
            {
                descripcionServicio = descripcion;
                precioServicio = cantidad * precio;
                continue;
            }

            productos.Add(new MantenimientoProducto
            {
                Cantidad = cantidad,
                Descripcion = descripcion,
                PrecioUnitario = precio,
                EsServicio = 0
            });
        }

        return new FilaImportada(
            Fecha: fecha ?? FechaHelper.Ahora,
            Placa: ValorDespuesDe(lineasPrimera, "Placa:"),
            Marca: marcaModelo[0],
            Modelo: marcaModelo.Length > 1 ? marcaModelo[1] : string.Empty,
            KM: string.IsNullOrWhiteSpace(km) ? "0" : km,
            Descripcion: descripcionServicio,
            Numero: numero,
            Cliente: ValorDespuesDe(lineasPrimera, "Cliente:"),
            Telefono: ValorDespuesDe(lineasPrimera, "Teléfono:"),
            Fotos: null,   // la proforma no muestra el enlace de fotos
            Precio: precioServicio,
            Productos: productos
        );
    }

    // Recorre todas las páginas: el encabezado de la tabla se repite en cada una, el pie
    // "Datos para depósito" corta la página y "TOTAL" (alineado a la derecha) termina la tabla.
    // Devuelve null si nunca encontró el encabezado.
    private static List<FilaTabla>? LeerTabla(PdfDocument doc)
    {
        var filas = new List<FilaTabla>();
        bool encontrada = false, terminada = false;

        foreach (var page in doc.GetPages())
        {
            double? cantX = null, descX = null, punitX = null, importeX = null;

            foreach (var linea in Lineas(page))
            {
                var textos = linea.Select(w => w.Text).ToList();

                if (textos.Contains("Item") && textos.Contains("Cant.") && textos.Contains("Importe"))
                {
                    cantX = Inicio(linea, "Cant.");
                    descX = Inicio(linea, "Descripción");
                    punitX = Inicio(linea, "P.");
                    importeX = Inicio(linea, "Importe");
                    encontrada = true;
                    continue;
                }

                if (cantX is null || descX is null || punitX is null || importeX is null)
                    continue; // en esta página todavía no empieza la tabla

                // Una descripción también empieza en descX; "TOTAL" del pie va mucho más a la derecha.
                if (textos is ["TOTAL"] && linea[0].BoundingBox.Left > descX + 100)
                {
                    terminada = true;
                    break;
                }

                if (textos.Count >= 3 && textos[0] == "Datos" && textos[1] == "para" && textos[2].StartsWith("depósito"))
                    break;

                // Columnas por posición: los textos de celda arrancan unos puntos después del borde.
                var item = linea.Where(w => w.BoundingBox.Left < cantX - 2).Select(w => w.Text).ToList();
                var descripcion = linea
                    .Where(w => w.BoundingBox.Left >= descX - 2 && w.BoundingBox.Left < punitX - 4)
                    .Select(w => w.Text).ToList();

                if (item.Count > 0)
                {
                    filas.Add(new FilaTabla(
                        string.Join(" ", item),
                        string.Concat(linea.Where(w => w.BoundingBox.Left >= cantX - 2 && w.BoundingBox.Left < descX - 2).Select(w => w.Text)),
                        descripcion,
                        string.Concat(linea.Where(w => w.BoundingBox.Left >= punitX - 4 && w.BoundingBox.Left < importeX - 4).Select(w => w.Text))));
                }
                else if (filas.Count > 0 && descripcion.Count == linea.Count)
                {
                    filas[^1].Descripcion.AddRange(descripcion); // continuación de la descripción
                }
            }

            if (terminada)
                break;
        }

        return encontrada ? filas : null;
    }

    // Texto que sigue a la etiqueta en su renglón, hasta la siguiente etiqueta de la columna derecha.
    private static string ValorDespuesDe(List<List<Word>> lineas, params string[] etiqueta)
    {
        foreach (var linea in lineas)
        {
            var textos = linea.Select(w => w.Text).ToList();
            for (int i = 0; i + etiqueta.Length <= textos.Count; i++)
            {
                if (!textos.Skip(i).Take(etiqueta.Length).SequenceEqual(etiqueta))
                    continue;

                var valor = textos.Skip(i + etiqueta.Length).TakeWhile(t => !EtiquetasDerecha.Contains(t));
                return string.Join(" ", valor).Trim();
            }
        }

        return string.Empty;
    }

    private static double Inicio(List<Word> linea, string texto) =>
        linea.First(w => w.Text == texto).BoundingBox.Left;

    // Palabras agrupadas por renglón (misma altura, tolerancia 2 pt), de arriba hacia abajo.
    private static List<List<Word>> Lineas(Page page)
    {
        var lineas = new List<List<Word>>();
        foreach (var w in page.GetWords().OrderByDescending(w => w.BoundingBox.Bottom))
        {
            if (lineas.Count > 0 && Math.Abs(lineas[^1][0].BoundingBox.Bottom - w.BoundingBox.Bottom) <= 2)
                lineas[^1].Add(w);
            else
                lineas.Add(new List<Word> { w });
        }

        return lineas.Select(l => l.OrderBy(w => w.BoundingBox.Left).ToList()).ToList();
    }

    private static string SoloDigitos(string texto) => Regex.Match(texto, @"\d+").Value;
}
