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
                SELECT breed_slug, price_low, price_high, confidence, source_count, spread_ratio, updated_at, basis
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
                    SpreadRatio: reader.IsDBNull(5) ? null : reader.GetDouble(5),
                    Basis: reader.GetString(7));
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

    /// <summary>
    /// Withdraws a breed's published range, for when the evidence no longer supports one.
    ///
    /// <para>
    /// Needed because re-aggregation only ever upserted. When a range stopped qualifying and
    /// there was no seed to fall back to, nothing was written and the old row simply stayed
    /// live: Irish Wolfhound kept serving a $2,000-$2,100 band after the sample behind it was
    /// refused as one seller's litter. A rule that can only ever *raise* confidence isn't a
    /// rule. The listing and observation rows are untouched — only the derived range goes.
    /// </para>
    /// </summary>
    public async Task<bool> RemoveAsync(string breedSlug, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM breed_price WHERE breed_slug = $slug;";
            command.Parameters.AddWithValue("$slug", breedSlug);
            var removed = await command.ExecuteNonQueryAsync(ct) > 0;
            if (removed)
            {
                _cache = null;
            }

            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Records a listing sample. Idempotent per run via the unique index — re-fetching a
    /// breed within one run refreshes rather than inflating the sample, because a price
    /// counted twice moves the percentiles while looking like corroboration.
    /// </summary>
    public async Task AddListingPricesAsync(IReadOnlyList<ListingPrice> prices, CancellationToken ct)
    {
        if (prices.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            foreach (var price in prices)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)transaction;
                command.CommandText = """
                    INSERT INTO listing_price
                        (breed_slug, price, source_host, listing_ref, listing_name, retrieved_at, run_id)
                    VALUES ($slug, $price, $host, $ref, $name, $retrieved, $run)
                    ON CONFLICT (breed_slug, source_host, listing_ref, run_id) DO UPDATE SET
                        price = excluded.price,
                        listing_name = excluded.listing_name,
                        retrieved_at = excluded.retrieved_at;
                    """;
                command.Parameters.AddWithValue("$slug", price.BreedSlug);
                command.Parameters.AddWithValue("$price", price.Price);
                command.Parameters.AddWithValue("$host", price.SourceHost);
                command.Parameters.AddWithValue("$ref", price.ListingRef);
                command.Parameters.AddWithValue("$name", price.ListingName);
                command.Parameters.AddWithValue("$retrieved", price.RetrievedAt.ToString("o"));
                command.Parameters.AddWithValue("$run", price.RunId);
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            _cache = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// A breed's listing sample over a rolling window, pooled across runs.
    ///
    /// <para>
    /// Pooling is not an optimisation, it is the fix for a property of the source: two runs
    /// forty minutes apart returned <b>zero overlapping listings</b> for most breeds. The
    /// index hands out a different slice of a much larger pool each time, so any single run
    /// is a small random sample — Australian Shepherd swung from a verified $800–$1,500 to
    /// a refused $500 floor between two runs, which is not a benchmark a fraud check can
    /// rest on.
    /// </para>
    ///
    /// <para>
    /// The same property makes pooling unusually effective: with no overlap, every run adds
    /// ~40 genuinely new observations rather than re-confirming the last ones. The window
    /// bounds staleness; <see cref="ListingPriceAggregator"/> dedupes by listing so a
    /// re-listed animal counts once, at its most recent price.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ListingPrice>> GetListingPricesAsync(
        string breedSlug, int windowDays, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, breed_slug, price, source_host, listing_ref, listing_name, retrieved_at, run_id
            FROM listing_price
            WHERE breed_slug = $slug AND retrieved_at >= $since
            ORDER BY price;
            """;
        command.Parameters.AddWithValue("$slug", breedSlug);
        command.Parameters.AddWithValue("$since",
            Iso(DateTimeOffset.UtcNow.AddDays(-Math.Max(1, windowDays))));

        List<ListingPrice> results = [];
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ListingPrice(
                BreedSlug: reader.GetString(1),
                Price: reader.GetInt32(2),
                SourceHost: reader.GetString(3),
                ListingRef: reader.GetString(4),
                ListingName: reader.GetString(5),
                RetrievedAt: DateTimeOffset.Parse(reader.GetString(6)),
                RunId: reader.GetString(7),
                Id: reader.GetInt64(0)));
        }

        return results;
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

    /// <summary>
    /// Records a human decision on a pending observation. The row is kept either way — a
    /// rejection is evidence about a source, not something to erase.
    /// </summary>
    /// <returns>
    /// The affected breed slug, or null when no such observation exists. Returned rather
    /// than a bool because the caller has to re-aggregate that breed, and looking the slug
    /// up separately needs a query this one already has in hand.
    /// </returns>
    public async Task<string?> SetObservationStatusAsync(
        long id, string status, string? reason, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE price_observation SET status = $status, reject_reason = $reason
                WHERE id = $id
                RETURNING breed_slug;
                """;
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$reason", reason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$id", id);
            var slug = await command.ExecuteScalarAsync(ct) as string;
            if (slug is not null)
            {
                _cache = null;
            }

            return slug;
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
            INSERT INTO breed_price (breed_slug, price_low, price_high, confidence, source_count, spread_ratio, updated_at, basis)
            VALUES ($slug, $low, $high, $confidence, $sources, $spread, $updatedAt, $basis)
            ON CONFLICT (breed_slug) DO UPDATE SET
                price_low = excluded.price_low, price_high = excluded.price_high,
                confidence = excluded.confidence, source_count = excluded.source_count,
                spread_ratio = excluded.spread_ratio, updated_at = excluded.updated_at,
                basis = excluded.basis;
            """;
        command.Parameters.AddWithValue("$slug", price.BreedSlug);
        command.Parameters.AddWithValue("$low", price.PriceLow);
        command.Parameters.AddWithValue("$high", price.PriceHigh);
        command.Parameters.AddWithValue("$confidence", price.Confidence);
        command.Parameters.AddWithValue("$sources", price.SourceCount);
        command.Parameters.AddWithValue("$spread", price.SpreadRatio ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", Iso(price.UpdatedAt));
        command.Parameters.AddWithValue("$basis", price.Basis);
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
