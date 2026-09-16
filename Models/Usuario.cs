using System.ComponentModel.DataAnnotations;

namespace TalleresRMP.Models;

public class Usuario
{
    public int IdUsuario { get; set; }

    [Required(ErrorMessage = "El login es obligatorio.")]
    [MaxLength(50)]
    public string Login { get; set; } = string.Empty;

    // Hash (PBKDF2), nunca texto plano. Ver Services/PasswordHasher.
    [Required]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nivel es obligatorio.")]
    [RegularExpression("^[ABC]$", ErrorMessage = "El nivel debe ser A, B o C.")]
    public string Nivel { get; set; } = "C";

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public bool Activo { get; set; } = true;
}
