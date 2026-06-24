using System.Data.Common;
using System.Globalization;
using TalleresRMP.Models;

namespace TalleresRMP.Data;

/// <summary>
/// Acceso a datos de Mantenimiento usando ADO.NET puro sobre Nelknet.LibSQL.Data
/// (mismo patrón que el proyecto ServicioMensual): conexión por operación,
/// comandos parametrizados con AddWithValue y lectura manual con DataReader.
/// </summary>
public class MantenimientoRepository
{
    private const string FechaFormato = "yyyy-MM-dd HH:mm:ss";

    private readonly TursoConnection _turso;

    public MantenimientoRepository(TursoConnection turso)
    {
        _turso = turso;
    }

    /// <summary>
    /// Registros cuya FechaCreacion cae entre hace 125 y hace 120 días,
    /// ordenados del más antiguo al más reciente. Solo campos de listado.
    /// </summary>
    public async Task<IEnumerable<Mantenimiento>> GetUltimos120a125DiasAsync()
    {
        var ahora = DateTime.Now;
        var desde = ahora.AddDays(-125).ToString(FechaFormato);
        var hasta = ahora.AddDays(-120).ToString(FechaFormato);

        var lista = new List<Mantenimiento>();

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdMantenimiento, Cliente, Numero, Telefono, FechaCreacion " +
            "FROM Mantenimiento " +
            "WHERE FechaCreacion BETWEEN @Desde AND @Hasta " +
            "ORDER BY FechaCreacion ASC";
        cmd.Parameters.AddWithValue("@Desde", desde);
        cmd.Parameters.AddWithValue("@Hasta", hasta);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new Mantenimiento
            {
                IdMantenimiento = reader.GetInt32(0),
                Cliente = reader.GetString(1),
                Numero = reader.GetString(2),
                Telefono = reader.GetString(3),
                FechaCreacion = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
            });
        }
        return lista;
    }

    public async Task<IEnumerable<Mantenimiento>> GetAllAsync()
    {
        var lista = new List<Mantenimiento>();

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, Estado, FechaCreacion " +
            "FROM Mantenimiento ORDER BY FechaCreacion DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add(LeerCabecera(reader));

        return lista;
    }

    public async Task<Mantenimiento?> GetByIdAsync(int id)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        Mantenimiento? mantenimiento = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IdMantenimiento, Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, Estado, FechaCreacion " +
                "FROM Mantenimiento WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                mantenimiento = LeerCabecera(reader);
        }

        if (mantenimiento is null)
            return null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IdMantenimientoProducto, IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe " +
                "FROM MantenimientoProducto WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                mantenimiento.Productos.Add(LeerProducto(reader));
        }

        return mantenimiento;
    }

    public async Task<int> InsertAsync(Mantenimiento m)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        int id;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Mantenimiento (Numero, Cliente, Telefono, Marca, Modelo, Placa, KM, Total, Estado, FechaCreacion) " +
                "VALUES (@Numero, @Cliente, @Telefono, @Marca, @Modelo, @Placa, @KM, @Total, @Estado, @FechaCreacion)";
            AgregarParametrosCabecera(cmd, m);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT last_insert_rowid()";
            id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        await InsertarProductosAsync(conn, id, m.Productos);

        return id;
    }

    public async Task UpdateAsync(Mantenimiento m)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE Mantenimiento SET " +
                "Numero=@Numero, Cliente=@Cliente, Telefono=@Telefono, Marca=@Marca, Modelo=@Modelo, " +
                "Placa=@Placa, KM=@KM, Total=@Total, Estado=@Estado " +
                "WHERE IdMantenimiento=@IdMantenimiento";
            AgregarParametrosCabecera(cmd, m);
            cmd.Parameters.AddWithValue("@IdMantenimiento", m.IdMantenimiento);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM MantenimientoProducto WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", m.IdMantenimiento);
            await cmd.ExecuteNonQueryAsync();
        }

        await InsertarProductosAsync(conn, m.IdMantenimiento, m.Productos);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _turso.GetConnection();
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM MantenimientoProducto WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Mantenimiento WHERE IdMantenimiento=@Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ----- Helpers -----

    private static async Task InsertarProductosAsync(
        Nelknet.LibSQL.Data.LibSQLConnection conn, int idMantenimiento, IEnumerable<MantenimientoProducto> productos)
    {
        foreach (var p in productos)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO MantenimientoProducto (IdMantenimiento, Item, Cantidad, Descripcion, PrecioUnitario, Importe) " +
                "VALUES (@IdMantenimiento, @Item, @Cantidad, @Descripcion, @PrecioUnitario, @Importe)";
            cmd.Parameters.AddWithValue("@IdMantenimiento", idMantenimiento);
            cmd.Parameters.AddWithValue("@Item", p.Item);
            cmd.Parameters.AddWithValue("@Cantidad", p.Cantidad);
            cmd.Parameters.AddWithValue("@Descripcion", p.Descripcion);
            cmd.Parameters.AddWithValue("@PrecioUnitario", (double)p.PrecioUnitario);
            cmd.Parameters.AddWithValue("@Importe", (double)p.Importe);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static void AgregarParametrosCabecera(Nelknet.LibSQL.Data.LibSQLCommand cmd, Mantenimiento m)
    {
        cmd.Parameters.AddWithValue("@Numero", m.Numero);
        cmd.Parameters.AddWithValue("@Cliente", m.Cliente);
        cmd.Parameters.AddWithValue("@Telefono", m.Telefono);
        cmd.Parameters.AddWithValue("@Marca", m.Marca);
        cmd.Parameters.AddWithValue("@Modelo", m.Modelo);
        cmd.Parameters.AddWithValue("@Placa", m.Placa);
        cmd.Parameters.AddWithValue("@KM", m.KM);
        cmd.Parameters.AddWithValue("@Total", (double)m.Total);
        cmd.Parameters.AddWithValue("@Estado", m.Estado);
        cmd.Parameters.AddWithValue("@FechaCreacion", m.FechaCreacion.ToString(FechaFormato));
    }

    private static Mantenimiento LeerCabecera(DbDataReader r) => new()
    {
        IdMantenimiento = r.GetInt32(0),
        Numero = r.GetString(1),
        Cliente = r.GetString(2),
        Telefono = r.GetString(3),
        Marca = r.GetString(4),
        Modelo = r.GetString(5),
        Placa = r.GetString(6),
        KM = r.GetString(7),
        Total = LeerDecimal(r, 8),
        Estado = r.GetString(9),
        FechaCreacion = DateTime.Parse(r.GetString(10), CultureInfo.InvariantCulture)
    };

    private static MantenimientoProducto LeerProducto(DbDataReader r) => new()
    {
        IdMantenimientoProducto = r.GetInt32(0),
        IdMantenimiento = r.GetInt32(1),
        Item = r.GetString(2),
        Cantidad = r.GetInt32(3),
        Descripcion = r.GetString(4),
        PrecioUnitario = LeerDecimal(r, 5),
        Importe = LeerDecimal(r, 6)
    };

    private static decimal LeerDecimal(DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(r.GetValue(ordinal), CultureInfo.InvariantCulture);
}
