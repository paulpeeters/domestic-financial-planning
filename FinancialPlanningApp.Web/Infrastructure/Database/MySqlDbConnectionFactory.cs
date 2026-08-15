using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Data;

namespace FinancialPlanningApp.Web.Infrastructure.Database;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class MySqlDbConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
