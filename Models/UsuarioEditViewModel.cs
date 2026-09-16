using System.ComponentModel.DataAnnotations;

namespace TalleresRMP.Models;

public class UsuarioEditViewModel
{
    public int IdUsuario { get; set; }

    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nivel es obligatorio.")]
    [RegularExpression("^[ABC]$", ErrorMessage = "El nivel debe ser A, B o C.")]
    public string Nivel { get; set; } = "C";

    public bool Activo { get; set; } = true;

    // Opcional: si se llena, resetea la contraseña. Si se deja vacío, no se toca.
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    public string? NuevaPassword { get; set; }
}
