using Microsoft.Data.Sqlite;

namespace PuppyFinder.Api.Data;

/// <summary>
/// Owns the SQLite file that holds breed prices and their provenance, and the
/// schema that lives in it.
///
/// A database rather than another JSON file (the pattern <see cref="Services.AlertStore"/>
/// uses) for one reason: the value here isn't the current price, it's the audit
/// trail behind it. "Which page said this, on what date, in which run, and what
/// did it replace?" is a relational question, and prices feed a fraud check, so
/// being able to answer it — and roll back — matters more than saving a dependency.
/// </summary>
public sealed class PriceDb
{
    private readonly string _connectionString;
    private readonly ILogger<PriceDb> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _ready;

    public PriceDb(IConfiguration configuration, IHostEnvironment environment, ILogger<PriceDb> logger)
    {
        var path = configuration["Prices:DbPath"]
            ?? Path.Combine(environment.ContentRootPath, "data", "prices.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        _logger = logger;
    }

    /// <summary>An open connection with the schema guaranteed to exist.</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>
    /// Applies any missing migrations. Idempotent and safe to call on every open —
    /// after the first success it's a bool check.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_ready)
        {
            return;
        }

        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_ready)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            // WAL lets the API keep serving reads while a research run writes.
            await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", ct);
            await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", ct);
            await ExecuteAsync(connection,
                "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);", ct);

            var current = await CurrentVersionAsync(connection, ct);
            for (var next = current; next < Migrations.Length; next++)
            {
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await ExecuteAsync(connection, Migrations[next], ct, transaction);
                await ExecuteAsync(connection, "DELETE FROM schema_version;", ct, transaction);
                await ExecuteAsync(connection,
                    $"INSERT INTO schema_version (version) VALUES ({next + 1});", ct, transaction);
                await transaction.CommitAsync(ct);
                _logger.LogInformation("Price DB migrated to schema version {Version}", next + 1);
            }

            _ready = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static async Task<int> CurrentVersionAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection, string sql, CancellationToken ct, System.Data.Common.DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction as SqliteTransaction;
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Append-only migration list — index + 1 is the schema version. Never edit a
    /// shipped entry; add a new one.
    /// </summary>
    private static readonly string[] Migrations =
    [
        // v1 — prices, their provenance, and the runs that produced them.
        """
        CREATE TABLE breed_price (
            breed_slug   TEXT    PRIMARY KEY,
            price_low    INTEGER NOT NULL,
            price_high   INTEGER NOT NULL,
            confidence   TEXT    NOT NULL,
            source_count INTEGER NOT NULL DEFAULT 0,
            spread_ratio REAL,
            updated_at   TEXT    NOT NULL
        );

        CREATE TABLE price_observation (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            breed_slug     TEXT    NOT NULL,
            price_low      INTEGER NOT NULL,
            price_high     INTEGER NOT NULL,
            scope          TEXT    NOT NULL,
            source_url     TEXT    NOT NULL,
            publisher      TEXT    NOT NULL,
            publisher_tier TEXT    NOT NULL,
            quote          TEXT    NOT NULL,
            published_at   TEXT,
            red_flag_quote TEXT,
            retrieved_at   TEXT    NOT NULL,
            run_id         TEXT    NOT NULL,
            model          TEXT    NOT NULL,
            status         TEXT    NOT NULL,
            reject_reason  TEXT
        );

        CREATE INDEX ix_observation_breed  ON price_observation (breed_slug, status);
        CREATE INDEX ix_observation_run    ON price_observation (run_id);
        CREATE INDEX ix_observation_status ON price_observation (status);

        CREATE TABLE price_run (
            id             TEXT PRIMARY KEY,
            started_at     TEXT NOT NULL,
            finished_at    TEXT,
            breeds_checked INTEGER NOT NULL DEFAULT 0,
            accepted       INTEGER NOT NULL DEFAULT 0,
            pending        INTEGER NOT NULL DEFAULT 0,
            rejected       INTEGER NOT NULL DEFAULT 0,
            error          TEXT
        );
        """,

        // v2 — Tier A publishers often give an average rather than a band, and
        // discarding those lost good data. Existing rows are bands by definition.
        """
        ALTER TABLE price_observation ADD COLUMN kind TEXT NOT NULL DEFAULT 'range';
        """,
    ];
}
