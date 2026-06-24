namespace TalleresRMP.Models;

public class MantenimientoViewModel
{
    public Mantenimiento Mantenimiento { get; set; } = new();

    public List<MantenimientoProducto> Productos { get; set; } = new();
}
