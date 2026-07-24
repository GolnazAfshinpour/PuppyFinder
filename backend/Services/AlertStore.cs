using System.Text.Json;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// JSON-file-backed store for alert subscriptions — deliberately no database:
/// the expected scale is personal/hobby (tens of subscriptions, not millions).
/// All mutations serialize through one lock and rewrite the file atomically.
/// </summary>
public sealed class AlertStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<AlertSubscription>? _cache;

    public AlertStore(IConfiguration configuration, IHostEnvironment environment)
    {
        _path = configuration["Alerts:StorePath"]
            ?? Path.Combine(environment.ContentRootPath, "data", "alerts.json");
    }

    public async Task<IReadOnlyList<AlertSubscription>> GetAllAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return [.. Load()];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AlertSubscription> AddAsync(AlertSubscription subscription, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = Load();
            // Same email + same filters = same alert; make re-submits idempotent.
            var existing = all.FirstOrDefault(s =>
                s.Email.Equals(subscription.Email, StringComparison.OrdinalIgnoreCase) &&
                s.Breed == subscription.Breed && s.State == subscription.State &&
                s.City == subscription.City && s.Size == subscription.Size);
            if (existing is not null)
            {
                return existing;
            }

            all.Add(subscription);
            Save(all);
            return subscription;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Removes a subscription; the email must match so an ID alone can't unsubscribe others.</summary>
    public async Task<bool> RemoveAsync(string id, string email, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = Load();
            var removed = all.RemoveAll(s =>
                s.Id == id && s.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                Save(all);
            }

            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSeenAsync(string id, IReadOnlyCollection<string> seenListingIds, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = Load();
            var index = all.FindIndex(s => s.Id == id);
            if (index < 0)
            {
                return; // unsubscribed while the checker was running
            }

            all[index] = all[index] with { SeenListingIds = [.. seenListingIds] };
            Save(all);
        }
        finally
        {
            _lock.Release();
        }
    }

    private List<AlertSubscription> Load()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        _cache = File.Exists(_path)
            ? JsonSerializer.Deserialize<List<AlertSubscription>>(File.ReadAllText(_path)) ?? []
            : [];
        return _cache;
    }

    private void Save(List<AlertSubscription> all)
    {
        _cache = all;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _path, overwrite: true);
    }
}
