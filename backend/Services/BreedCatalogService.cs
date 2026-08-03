using System.Text.Json;
using PuppyFinder.Api.Data;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Merges the curated breed list (with quiz traits and verified slugs) with the
/// full breed catalog from the free, keyless dog.ceo API. Curated entries win on
/// conflicts; external breeds get neutral traits and are used for the dropdown
/// and deep links only (the quiz scores curated breeds exclusively).
///
/// Prices are then overlaid from <see cref="PriceStore"/>, which is authoritative:
/// the numbers in <see cref="SiteCatalog"/> are only an unsourced seed.
/// </summary>
public sealed class BreedCatalogService(
    IHttpClientFactory httpClientFactory,
    PriceStore priceStore,
    ILogger<BreedCatalogService> logger)
{
    private const string BreedsUrl = "https://dog.ceo/api/breeds/list/all";

    // dog.ceo squashes some names to one word; expand the common ones.
    private static readonly Dictionary<string, string> NameFixes = new()
    {
        ["germanshepherd"] = "German Shepherd",
        ["stbernard"] = "St. Bernard",
        ["mexicanhairless"] = "Mexican Hairless",
        ["cotondetulear"] = "Coton de Tulear",
        ["shihtzu"] = "Shih Tzu",
        ["bullterrier"] = "Bull Terrier",
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<Breed>? _cached;

    public async Task<IReadOnlyList<Breed>> GetBreedsAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var merged = SiteCatalog.Breeds.ToDictionary(b => b.Slug);
            try
            {
                var client = httpClientFactory.CreateClient("dogceo");
                var payload = await client.GetStringAsync(BreedsUrl, cancellationToken);
                using var json = JsonDocument.Parse(payload);

                foreach (var (name, dogCeoPath) in ExpandNames(json.RootElement.GetProperty("message")))
                {
                    var slug = name.ToLowerInvariant().Replace(". ", "-").Replace(' ', '-');
                    // Exact-slug matching alone let the same breed through twice under two
                    // names, which produced two different prices for one animal.
                    if (!merged.ContainsKey(slug) && !SiteCatalog.DuplicateOfCurated.Contains(slug))
                    {
                        merged[slug] = new Breed(
                            slug, name, slug,
                            Size: "Medium", Energy: 3, Grooming: 3, Shedding: 3,
                            KidFriendly: 3, ApartmentFriendly: 3,
                            PriceLow: 0, PriceHigh: 0, Blurb: "",
                            DogCeoPath: dogCeoPath);
                    }
                }

                logger.LogInformation("Breed catalog: {Count} breeds (curated + dog.ceo)", merged.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning("dog.ceo fetch failed, using curated breeds only: {Message}", ex.Message);
            }

            _cached = await OverlayPricesAsync(merged.Values, cancellationToken);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Replaces each breed's seed price with the live DB value. A breed absent from
    /// the DB keeps zeroes, which every caller already reads as "no verified range".
    /// </summary>
    private async Task<IReadOnlyList<Breed>> OverlayPricesAsync(
        IEnumerable<Breed> breeds, CancellationToken cancellationToken)
    {
        var prices = await priceStore.GetAllAsync(cancellationToken);
        return breeds
            .Select(b => prices.TryGetValue(b.Slug, out var price)
                ? b with { PriceLow = price.PriceLow, PriceHigh = price.PriceHigh }
                : b with { PriceLow = 0, PriceHigh = 0 })
            .OrderBy(b => b.DisplayName)
            .ToList();
    }

    /// <summary>Drops the merged catalog so the next read picks up refreshed prices.</summary>
    public void InvalidatePrices() => _cached = null;

    public async Task<Breed?> FindAsync(string slug, CancellationToken cancellationToken) =>
        (await GetBreedsAsync(cancellationToken))
            .FirstOrDefault(b => b.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<(string Name, string DogCeoPath)> ExpandNames(JsonElement message)
    {
        foreach (var breed in message.EnumerateObject())
        {
            var subBreeds = breed.Value.EnumerateArray().ToList();
            if (subBreeds.Count == 0)
            {
                yield return (Pretty(breed.Name), breed.Name);
            }
            else
            {
                // Convention: sub-breed comes first ("retriever"/"golden" → "Golden Retriever").
                foreach (var sub in subBreeds)
                {
                    yield return ($"{Pretty(sub.GetString()!)} {Pretty(breed.Name)}", $"{breed.Name}/{sub.GetString()}");
                }
            }
        }
    }

    private static string Pretty(string raw) =>
        NameFixes.TryGetValue(raw, out var fixedName)
            ? fixedName
            : char.ToUpperInvariant(raw[0]) + raw[1..];
}
