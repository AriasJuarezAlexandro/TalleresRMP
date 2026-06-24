using Nelknet.LibSQL.Data;

namespace TalleresRMP.Services;

/// <summary>
/// Fábrica de conexiones a Turso/LibSQL. Lee la sección "Turso" de la
/// configuración y arma el connection string. Se registra como Singleton
/// (solo guarda una cadena) y entrega una conexión nueva por operación.
/// </summary>
public class TursoService
{
    private readonly string _connectionString;

    public TursoService(IConfiguration config)
    {
        var url = config["Turso:Url"]!;
        var token = config["Turso:AuthToken"]!;
        _connectionString = $"Data Source={url};Auth Token={token}";
    }

    public LibSQLConnection GetConnection() => new LibSQLConnection(_connectionString);
}
