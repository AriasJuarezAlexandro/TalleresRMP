namespace TalleresRMP.Services;

/// <summary>
/// Nombres de las claves de Session usadas por el login simple, para no
/// repetir strings mágicos entre AccountController, los filtros y las vistas.
/// </summary>
public static class SesionClaves
{
    public const string IdUsuario = "IdUsuario";
    public const string Login = "Login";
    public const string Nivel = "Nivel";
}
