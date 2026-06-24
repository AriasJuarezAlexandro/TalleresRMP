using System.ComponentModel.DataAnnotations;

namespace TalleresRMP.Models;

public class Mantenimiento
{
    public int IdMantenimiento { get; set; }

    [Required(ErrorMessage = "El número es obligatorio.")]
    [MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "El cliente es obligatorio.")]
    [MaxLength(50)]
    public string Cliente { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [MaxLength(10)]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [MaxLength(30)]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    [MaxLength(20)]
    public string Modelo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La placa es obligatoria.")]
    [MaxLength(10)]
    public string Placa { get; set; } = string.Empty;

    [Required(ErrorMessage = "El KM es obligatorio.")]
    [MaxLength(11)]
    public string KM { get; set; } = string.Empty;

    [Range(0, 999.99)]
    public decimal Total { get; set; }

    [MaxLength(20)]
    public string Estado { get; set; } = "Pendiente";

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public List<MantenimientoProducto> Productos { get; set; } = new();
}
