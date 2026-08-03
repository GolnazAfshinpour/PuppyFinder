using Microsoft.Data.Sqlite;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// The only code that touches the price tables. Reads are cached in memory —
/// prices change monthly, so every /api/breeds call shouldn't hit disk — and the
/// cache is dropped on write.
/// </summary>
public sealed class PriceStore(PriceDb db, ILogger<PriceStore> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, BreedPrice>? _cache;

    /// <summary>Live prices by breed slug.</summary>
    public async Task<IReadOnlyDictionary<string, BreedPrice>> GetAllAsync(CancellationToken ct)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache is not null)
            {
                return _cache;
            }

            await using var connection = await db.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT breed_slug, price_low, price_high, confidence, source_count, spread_ratio, updated_at
                FROM breed_price;
                """;

            var prices = new Dictionary<string, BreedPrice>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var slug = reader.GetString(0);
                prices[slug] = new BreedPrice(
                    BreedSlug: slug,
                    PriceLow: reader.GetInt32(1),
                    PriceHigh: reader.GetInt32(2),
                    Confidence: reader.GetString(3),
                    SourceCount: reader.GetInt32(4),
                    UpdatedAt: DateTimeOffset.Parse(reader.GetString(6)),
                    SpreadRatio: reader.IsDBNull(5) ? null : reader.GetDouble(5));
            }

            _cache = prices;
            return prices;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BreedPrice?> FindAsync(string breedSlug, CancellationToken ct) =>
        (await GetAllAsync(ct)).GetValueOrDefault(breedSlug);

    /// <summary>Writes (or replaces) a breed's live range and drops the read cache.</summary>
    public async Task UpsertAsync(BreedPrice price, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await UpsertAsync(connection, price, transaction: null, ct);
            _cache = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Appends observations. Never updates or deletes — this is the audit trail.</summary>
    public async Task AddObservationsAsync(IReadOnlyList<PriceObservation> observations, CancellationToken ct)
    {
        if (observations.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            foreach (var observation in observations)
            {
                await InsertObservationAsync(connection, observation, transaction, ct);
            }

            await transaction.CommitAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The accepted observations backing a breed's range — what the UI cites.</summary>
    public async Task<IReadOnlyList<PriceObservation>> GetObservationsAsync(
        string breedSlug, string? status, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, breed_slug, price_low, price_high, scope, kind, source_url, publisher, publisher_tier,
                   quote, published_at, red_flag_quote, retrieved_at, run_id, model, status, reject_reason
            FROM price_observation
            WHERE breed_slug = $slug AND ($status IS NULL OR status = $status)
            ORDER BY retrieved_at DESC, id DESC;
            """;
        command.Parameters.AddWithValue("$slug", breedSlug);
        command.Parameters.AddWithValue("$status", status ?? (object)DBNull.Value);

        var results = new List<PriceObservation>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadObservation(reader));
        }

        return results;
    }

    /// <summary>Everything waiting on a human decision, newest first.</summary>
    public async Task<IReadOnlyList<PriceObservation>> GetPendingAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, breed_slug, price_low, price_high, scope, kind, source_url, publisher, publisher_tier,
                   quote, published_at, red_flag_quote, retrieved_at, run_id, model, status, reject_reason
            FROM price_observation
            WHERE status = $pending
            ORDER BY retrieved_at DESC, id DESC;
            """;
        command.Parameters.AddWithValue("$pending", ObservationStatus.Pending);

        var results = new List<PriceObservation>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadObservation(reader));
        }

        return results;
    }

    /// <summary>Records a human decision on a pending observation. The row is kept either way.</summary>
    public async Task<bool> SetObservationStatusAsync(
        long id, string status, string? reason, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE price_observation SET status = $status, reject_reason = $reason WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$reason", reason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$id", id);
            var changed = await command.ExecuteNonQueryAsync(ct) > 0;
            if (changed)
            {
                _cache = null;
            }

            return changed;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StartRunAsync(PriceRun run, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO price_run (id, started_at) VALUES ($id, $startedAt);
            """;
        command.Parameters.AddWithValue("$id", run.Id);
        command.Parameters.AddWithValue("$startedAt", Iso(run.StartedAt));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task FinishRunAsync(PriceRun run, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE price_run
            SET finished_at = $finishedAt, breeds_checked = $checked,
                accepted = $accepted, pending = $pending, rejected = $rejected, error = $error
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", run.Id);
        command.Parameters.AddWithValue("$finishedAt", Iso(run.FinishedAt ?? DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$checked", run.BreedsChecked);
        command.Parameters.AddWithValue("$accepted", run.Accepted);
        command.Parameters.AddWithValue("$pending", run.Pending);
        command.Parameters.AddWithValue("$rejected", run.Rejected);
        command.Parameters.AddWithValue("$error", run.Error ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>True when a previous run started and never finished — the refresh job refuses to overlap.</summary>
    public async Task<bool> HasUnfinishedRunAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM price_run WHERE finished_at IS NULL;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    /// <summary>
    /// One-time import of the prices that were hardcoded in <see cref="SiteCatalog"/>.
    ///
    /// They land as <see cref="PriceConfidence.Unverified"/> with an observation whose
    /// publisher is "legacy hardcoded (unsourced)" and whose URL is empty — deliberately,
    /// because the bug being fixed here is that these numbers were presented as verified
    /// when nobody can say where they came from. Grandfathering them in as trustworthy
    /// would defeat the exercise. They keep working; they stop claiming.
    /// </summary>
    public async Task SeedFromCatalogAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);

            await using (var count = connection.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM breed_price;";
                if (Convert.ToInt32(await count.ExecuteScalarAsync(ct)) > 0)
                {
                    return; // already seeded or already researched
                }
            }

            var seeded = SiteCatalog.Breeds.Where(b => b.PriceLow > 0 && b.PriceHigh > 0).ToList();
            var now = DateTimeOffset.UtcNow;
            const string runId = "seed-legacy-hardcoded";

            await using var transaction = await connection.BeginTransactionAsync(ct);

            await using (var run = connection.CreateCommand())
            {
                run.CommandText = """
                    INSERT OR IGNORE INTO price_run (id, started_at, finished_at, breeds_checked, accepted)
                    VALUES ($id, $at, $at, $checked, $checked);
                    """;
                run.Transaction = (SqliteTransaction)transaction;
                run.Parameters.AddWithValue("$id", runId);
                run.Parameters.AddWithValue("$at", Iso(now));
                run.Parameters.AddWithValue("$checked", seeded.Count);
                await run.ExecuteNonQueryAsync(ct);
            }

            foreach (var breed in seeded)
            {
                await UpsertAsync(connection, new BreedPrice(
                    BreedSlug: breed.Slug,
                    PriceLow: breed.PriceLow,
                    PriceHigh: breed.PriceHigh,
                    Confidence: PriceConfidence.Unverified,
                    SourceCount: 0,
                    UpdatedAt: now), transaction, ct);

                await InsertObservationAsync(connection, new PriceObservation(
                    BreedSlug: breed.Slug,
                    PriceLow: breed.PriceLow,
                    PriceHigh: breed.PriceHigh,
                    Scope: PriceScope.Unscoped,
                    Kind: FigureKind.Range,
                    SourceUrl: "",
                    Publisher: "legacy hardcoded (unsourced)",
                    PublisherTier: PublisherTier.B,
                    Quote: "Imported from SiteCatalog.cs; no source was ever recorded for this range.",
                    RetrievedAt: now,
                    RunId: runId,
                    Model: "none",
                    Status: ObservationStatus.Accepted), transaction, ct);
            }

            await transaction.CommitAsync(ct);
            _cache = null;
            logger.LogInformation(
                "Seeded {Count} legacy price ranges as '{Confidence}' — they carry no source", seeded.Count, PriceConfidence.Unverified);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task UpsertAsync(
        SqliteConnection connection, BreedPrice price, System.Data.Common.DbTransaction? transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction as SqliteTransaction;
        command.CommandText = """
            INSERT INTO breed_price (breed_slug, price_low, price_high, confidence, source_count, spread_ratio, updated_at)
            VALUES ($slug, $low, $high, $confidence, $sources, $spread, $updatedAt)
            ON CONFLICT (breed_slug) DO UPDATE SET
                price_low = excluded.price_low, price_high = excluded.price_high,
                confidence = excluded.confidence, source_count = excluded.source_count,
                spread_ratio = excluded.spread_ratio, updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$slug", price.BreedSlug);
        command.Parameters.AddWithValue("$low", price.PriceLow);
        command.Parameters.AddWithValue("$high", price.PriceHigh);
        command.Parameters.AddWithValue("$confidence", price.Confidence);
        command.Parameters.AddWithValue("$sources", price.SourceCount);
        command.Parameters.AddWithValue("$spread", price.SpreadRatio ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", Iso(price.UpdatedAt));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertObservationAsync(
        SqliteConnection connection, PriceObservation o, System.Data.Common.DbTransaction? transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction as SqliteTransaction;
        command.CommandText = """
            INSERT INTO price_observation
                (breed_slug, price_low, price_high, scope, kind, source_url, publisher, publisher_tier,
                 quote, published_at, red_flag_quote, retrieved_at, run_id, model, status, reject_reason)
            VALUES ($slug, $low, $high, $scope, $kind, $url, $publisher, $tier,
                    $quote, $publishedAt, $redFlag, $retrievedAt, $runId, $model, $status, $reason);
            """;
        command.Parameters.AddWithValue("$slug", o.BreedSlug);
        command.Parameters.AddWithValue("$low", o.PriceLow);
        command.Parameters.AddWithValue("$high", o.PriceHigh);
        command.Parameters.AddWithValue("$scope", o.Scope);
        command.Parameters.AddWithValue("$kind", o.Kind);
        command.Parameters.AddWithValue("$url", o.SourceUrl);
        command.Parameters.AddWithValue("$publisher", o.Publisher);
        command.Parameters.AddWithValue("$tier", o.PublisherTier);
        command.Parameters.AddWithValue("$quote", o.Quote);
        command.Parameters.AddWithValue("$publishedAt", o.PublishedAt is null ? DBNull.Value : Iso(o.PublishedAt.Value));
        command.Parameters.AddWithValue("$redFlag", o.RedFlagQuote ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$retrievedAt", Iso(o.RetrievedAt));
        command.Parameters.AddWithValue("$runId", o.RunId);
        command.Parameters.AddWithValue("$model", o.Model);
        command.Parameters.AddWithValue("$status", o.Status);
        command.Parameters.AddWithValue("$reason", o.RejectReason ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static PriceObservation ReadObservation(SqliteDataReader r) => new(
        Id: r.GetInt64(0),
        BreedSlug: r.GetString(1),
        PriceLow: r.GetInt32(2),
        PriceHigh: r.GetInt32(3),
        Scope: r.GetString(4),
        Kind: r.GetString(5),
        SourceUrl: r.GetString(6),
        Publisher: r.GetString(7),
        PublisherTier: r.GetString(8),
        Quote: r.GetString(9),
        PublishedAt: r.IsDBNull(10) ? null : DateTimeOffset.Parse(r.GetString(10)),
        RedFlagQuote: r.IsDBNull(11) ? null : r.GetString(11),
        RetrievedAt: DateTimeOffset.Parse(r.GetString(12)),
        RunId: r.GetString(13),
        Model: r.GetString(14),
        Status: r.GetString(15),
        RejectReason: r.IsDBNull(16) ? null : r.GetString(16));

    // Round-trippable and sortable as text, which is how SQLite stores it.
    private static string Iso(DateTimeOffset value) => value.ToString("o");
}
