using Nelknet.LibSQL.Data;

namespace TalleresRMP.Data;

/// <summary>
/// Construye conexiones a la base de datos Turso/LibSQL a partir de la
/// configuración (sección "Turso" en appsettings.json).
/// </summary>
public class TursoConnection
{
    private readonly string _connectionString;

    public TursoConnection(IConfiguration config)
    {
        var url = config["Turso:Url"]!;
        var token = config["Turso:AuthToken"]!;
        _connectionString = $"Data Source={url};Auth Token={token}";
    }

    /// <summary>
    /// Crea (sin abrir) una nueva conexión a Turso. El llamador es responsable
    /// de abrirla (OpenAsync) y liberarla (se recomienda usar 'using').
    /// </summary>
    public LibSQLConnection GetConnection() => new LibSQLConnection(_connectionString);
}
