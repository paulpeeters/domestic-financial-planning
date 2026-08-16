using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class MailSettingsRepository(
    IDbConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> databaseOptions) : IMailSettingsRepository
{
    private bool IsSqlite => ProviderDbConnectionFactory.NormalizeProvider(databaseOptions.Value.Provider) == DatabaseProviders.Sqlite;

    public async Task<MailSettings> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            id,
            tenant_id AS TenantId,
            scope_key AS ScopeKey,
            is_enabled AS IsEnabled,
            provider AS Provider,
            from_name AS FromName,
            from_email AS FromEmail,
            base_url AS BaseUrl,
            api_key AS ApiKey,
            smtp_host AS SmtpHost,
            smtp_port AS SmtpPort,
            smtp_username AS SmtpUsername,
            smtp_password AS SmtpPassword,
            smtp_use_ssl AS SmtpUseSsl,
            updated_utc AS UpdatedUtc
        FROM mail_settings
        WHERE scope_key = 'global'
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<MailSettings>(sql) ?? new MailSettings();
    }

    public async Task<bool> SaveGlobalAsync(MailSettings settings, CancellationToken cancellationToken = default)
    {
        var sql = IsSqlite
            ? """
              INSERT INTO mail_settings(
                  scope_key,
                  tenant_id,
                  is_enabled,
                  provider,
                  from_name,
                  from_email,
                  base_url,
                  api_key,
                  smtp_host,
                  smtp_port,
                  smtp_username,
                  smtp_password,
                  smtp_use_ssl,
                  updated_utc)
              VALUES(
                  'global',
                  NULL,
                  @IsEnabled,
                  @Provider,
                  @FromName,
                  @FromEmail,
                  @BaseUrl,
                  @ApiKey,
                  @SmtpHost,
                  @SmtpPort,
                  @SmtpUsername,
                  @SmtpPassword,
                  @SmtpUseSsl,
                  STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'))
              ON CONFLICT(scope_key) DO UPDATE SET
                  tenant_id = NULL,
                  is_enabled = excluded.is_enabled,
                  provider = excluded.provider,
                  from_name = excluded.from_name,
                  from_email = excluded.from_email,
                  base_url = excluded.base_url,
                  api_key = excluded.api_key,
                  smtp_host = excluded.smtp_host,
                  smtp_port = excluded.smtp_port,
                  smtp_username = excluded.smtp_username,
                  smtp_password = excluded.smtp_password,
                  smtp_use_ssl = excluded.smtp_use_ssl,
                  updated_utc = STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now');
              """
            : """
              INSERT INTO mail_settings(
                  scope_key,
                  tenant_id,
                  is_enabled,
                  provider,
                  from_name,
                  from_email,
                  base_url,
                  api_key,
                  smtp_host,
                  smtp_port,
                  smtp_username,
                  smtp_password,
                  smtp_use_ssl,
                  updated_utc)
              VALUES(
                  'global',
                  NULL,
                  @IsEnabled,
                  @Provider,
                  @FromName,
                  @FromEmail,
                  @BaseUrl,
                  @ApiKey,
                  @SmtpHost,
                  @SmtpPort,
                  @SmtpUsername,
                  @SmtpPassword,
                  @SmtpUseSsl,
                  UTC_TIMESTAMP(6))
              ON DUPLICATE KEY UPDATE
                  tenant_id = NULL,
                  is_enabled = VALUES(is_enabled),
                  provider = VALUES(provider),
                  from_name = VALUES(from_name),
                  from_email = VALUES(from_email),
                  base_url = VALUES(base_url),
                  api_key = VALUES(api_key),
                  smtp_host = VALUES(smtp_host),
                  smtp_port = VALUES(smtp_port),
                  smtp_username = VALUES(smtp_username),
                  smtp_password = VALUES(smtp_password),
                  smtp_use_ssl = VALUES(smtp_use_ssl),
                  updated_utc = UTC_TIMESTAMP(6);
              """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, settings) > 0;
    }
}
