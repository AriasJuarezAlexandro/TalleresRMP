using System.ComponentModel.DataAnnotations;

namespace TalleresRMP.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
