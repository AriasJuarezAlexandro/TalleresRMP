using System.ComponentModel.DataAnnotations;

namespace TalleresRMP.Models;

public class MantenimientoProducto
{
    public int IdMantenimientoProducto { get; set; }

    public int IdMantenimiento { get; set; }

    [Required(ErrorMessage = "El item es obligatorio.")]
    [MaxLength(2)]
    public string Item { get; set; } = string.Empty;

    [Required]
    [Range(1, 9999, ErrorMessage = "La cantidad debe estar entre 1 y 9999.")]
    public int Cantidad { get; set; } = 1;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [MaxLength(4000)]
    public string Descripcion { get; set; } = string.Empty;

    public decimal PrecioUnitario { get; set; }

    public decimal Importe { get; set; }

    // 1 = generado desde Mantenimiento.Descripcion, 0 = ingresado manualmente
    public int EsServicio { get; set; }
}
