using System.Net;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Periodically diffs the live listings against each subscription's seen-set and
/// emails the new matches. Runs inside the API process — no external scheduler.
/// </summary>
public sealed class AlertChecker(
    AlertStore store,
    ListingAggregator aggregator,
    BreedCatalogService catalog,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AlertChecker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(configuration.GetValue("Alerts:CheckMinutes", 30));
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                await CheckOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    internal async Task CheckOnceAsync(CancellationToken ct)
    {
        var subscriptions = await store.GetAllAsync(ct);
        if (subscriptions.Count == 0)
        {
            return;
        }

        IReadOnlyList<Listing> listings;
        try
        {
            listings = await aggregator.GetListingsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Alert check skipped — listings unavailable: {Message}", ex.Message);
            return;
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                var matches = (await MatchAsync(listings, subscription, ct)).ToList();
                var fresh = matches.Where(l => !subscription.SeenListingIds.Contains(l.Id)).ToList();
                if (fresh.Count == 0)
                {
                    continue;
                }

                await emailSender.SendAsync(
                    subscription.Email,
                    $"🐶 {fresh.Count} new {(fresh.Count == 1 ? "dog" : "dogs")} match your PuppyFinder alert",
                    BuildEmailBody(subscription, fresh),
                    ct);
                // Seen-set = everything currently matching, so removed-then-relisted
                // dogs don't re-alert and the set can't grow without bound.
                await store.UpdateSeenAsync(subscription.Id, matches.Select(l => l.Id).ToList(), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Alert {Id} failed this cycle: {Message}", subscription.Id, ex.Message);
            }
        }
    }

    /// <summary>Applies the subscription's filters with the same rules as /api/listings.</summary>
    internal async Task<IEnumerable<Listing>> MatchAsync(
        IEnumerable<Listing> listings, AlertSubscription subscription, CancellationToken ct)
    {
        string? breedText = null;
        if (!string.IsNullOrWhiteSpace(subscription.Breed))
        {
            breedText = (await catalog.FindAsync(subscription.Breed, ct))?.SearchName ?? subscription.Breed;
            breedText = breedText.Split('(')[0].Trim();
        }

        return ListingQuery.Filter(listings, breedText, subscription.State, subscription.City, subscription.Size);
    }

    private static string BuildEmailBody(AlertSubscription subscription, IReadOnlyList<Listing> fresh)
    {
        var cards = string.Join("", fresh.Select(l => $"""
            <div style="margin:12px 0;padding:12px;border:1px solid #ddd;border-radius:8px">
              <strong>{WebUtility.HtmlEncode(l.Name)}</strong> — {WebUtility.HtmlEncode(l.Breed)}<br>
              {WebUtility.HtmlEncode(l.Sex ?? "")} {WebUtility.HtmlEncode(l.Age ?? "")} · 📍 {WebUtility.HtmlEncode(l.City)}, {WebUtility.HtmlEncode(l.State)}<br>
              {(l.ContactInfo is null ? "" : $"<strong>{WebUtility.HtmlEncode(l.ContactInfo)}</strong>{(l.AnimalRef is null ? "" : $" — ask about {WebUtility.HtmlEncode(l.AnimalRef)}")}<br>")}
              <a href="{WebUtility.HtmlEncode(l.ListingUrl)}">See {WebUtility.HtmlEncode(l.Name)}'s photos &amp; bio at {WebUtility.HtmlEncode(l.Source)} →</a>
            </div>
            """));

        var filters = string.Join(" · ", new[]
        {
            subscription.Breed, subscription.Size, subscription.City, subscription.State,
        }.Where(f => !string.IsNullOrWhiteSpace(f)));

        return $"""
            <h2>New adoptable dogs matching your search{(filters.Length > 0 ? $" ({WebUtility.HtmlEncode(filters)})" : "")}</h2>
            {cards}
            <p style="color:#777;font-size:12px">You get this because you saved an alert on PuppyFinder.
            <a href="http://localhost:5133/api/alerts/unsubscribe?id={Uri.EscapeDataString(subscription.Id)}&email={Uri.EscapeDataString(subscription.Email)}">Unsubscribe</a></p>
            """;
    }
}
