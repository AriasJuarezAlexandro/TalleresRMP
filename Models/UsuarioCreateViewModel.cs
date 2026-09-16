using System.ComponentModel.DataAnnotations;

namespace TalleresRMP.Models;

public class UsuarioCreateViewModel
{
    [Required(ErrorMessage = "El login es obligatorio.")]
    [MaxLength(50)]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nivel es obligatorio.")]
    [RegularExpression("^[ABC]$", ErrorMessage = "El nivel debe ser A, B o C.")]
    public string Nivel { get; set; } = "C";
}
