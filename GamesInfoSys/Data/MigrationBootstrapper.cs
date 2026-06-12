using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GamesInfoSys.Data;

public static class MigrationBootstrapper
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var hasTrackedGames = await TableExistsAsync(connection, "TrackedGames", cancellationToken);
        var hasMigrationHistory = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken);

        if (hasTrackedGames && !hasMigrationHistory)
        {
            await CreateMigrationHistoryTableAsync(connection, cancellationToken);
        }

        if (hasTrackedGames)
        {
            var hasInitialMigration = await MigrationExistsAsync(connection, "20260612041502_InitialCreate", cancellationToken);
            if (!hasInitialMigration)
                await SeedInitialMigrationAsync(connection, cancellationToken);
        }

        await db.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE name = $name AND type = 'table';
            """;
        command.Parameters.AddWithValue("$name", tableName);

        var result = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return result > 0;
    }

    private static async Task CreateMigrationHistoryTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> MigrationExistsAsync(SqliteConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = $migrationId;
            """;
        command.Parameters.AddWithValue("$migrationId", migrationId);

        var result = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return result > 0;
    }

    private static async Task SeedInitialMigrationAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20260612041502_InitialCreate', '10.0.9');
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
