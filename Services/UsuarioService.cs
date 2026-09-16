using System.Globalization;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

/// <summary>
/// Acceso a datos de Usuario (ADO.NET puro, igual que el resto del proyecto).
/// </summary>
public class UsuarioService
{
    private readonly TursoService _turso;

    public UsuarioService(TursoService turso)
    {
        _turso = turso;
    }

    // Valida login/password contra la tabla Usuario. Devuelve el usuario si
    // las credenciales son correctas y está activo; null en cualquier otro caso.
    public async Task<Usuario?> ValidarCredencialesAsync(string login, string password)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdUsuario, Login, Password, Nivel, FechaCreacion, Activo " +
            "FROM Usuario WHERE Login=@Login AND Activo=1";
        cmd.Parameters.AddWithValue("@Login", login);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var usuario = LeerUsuario(reader);
        return PasswordHasher.Verify(password, usuario.Password) ? usuario : null;
    }

    public async Task<List<Usuario>> ListarAsync()
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdUsuario, Login, Password, Nivel, FechaCreacion, Activo " +
            "FROM Usuario ORDER BY Login";

        var lista = new List<Usuario>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add(LeerUsuario(reader));

        return lista;
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int id)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdUsuario, Login, Password, Nivel, FechaCreacion, Activo " +
            "FROM Usuario WHERE IdUsuario=@Id";
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? LeerUsuario(reader) : null;
    }

    public async Task<bool> ExisteLoginAsync(string login, int? excluirId = null)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = excluirId is null
            ? "SELECT COUNT(*) FROM Usuario WHERE Login=@Login"
            : "SELECT COUNT(*) FROM Usuario WHERE Login=@Login AND IdUsuario<>@ExcluirId";
        cmd.Parameters.AddWithValue("@Login", login);
        if (excluirId is not null)
            cmd.Parameters.AddWithValue("@ExcluirId", excluirId.Value);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task CrearAsync(string login, string password, string nivel)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Usuario (Login, Password, Nivel) VALUES (@Login, @Password, @Nivel)";
        cmd.Parameters.AddWithValue("@Login", login);
        cmd.Parameters.AddWithValue("@Password", PasswordHasher.Hash(password));
        cmd.Parameters.AddWithValue("@Nivel", nivel);
        await cmd.ExecuteNonQueryAsync();
    }

    // Actualiza Nivel/Activo; si nuevaPassword viene con valor, también resetea la contraseña.
    public async Task ActualizarAsync(int id, string nivel, bool activo, string? nuevaPassword)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(nuevaPassword))
        {
            cmd.CommandText = "UPDATE Usuario SET Nivel=@Nivel, Activo=@Activo WHERE IdUsuario=@Id";
        }
        else
        {
            cmd.CommandText = "UPDATE Usuario SET Nivel=@Nivel, Activo=@Activo, Password=@Password WHERE IdUsuario=@Id";
            cmd.Parameters.AddWithValue("@Password", PasswordHasher.Hash(nuevaPassword));
        }

        cmd.Parameters.AddWithValue("@Nivel", nivel);
        cmd.Parameters.AddWithValue("@Activo", activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Usuario LeerUsuario(System.Data.Common.DbDataReader r) => new()
    {
        IdUsuario = r.GetInt32(0),
        Login = r.GetString(1),
        Password = r.GetString(2),
        Nivel = r.GetString(3),
        FechaCreacion = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture),
        Activo = r.GetInt32(5) == 1
    };
}
