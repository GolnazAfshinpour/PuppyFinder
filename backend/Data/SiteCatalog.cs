using System.Net;

namespace PuppyFinder.Api.Data;

public record Breed(string Slug, string DisplayName, string AkcSlug);

public enum SiteCategory
{
    BreederMarketplace,
    AdoptionPlatform,
    Rescue,
    Shelter
}

public record Site(
    string Id,
    string Name,
    SiteCategory Category,
    string Description,
    string HomeUrl);

/// <summary>
/// Static catalog of legitimate US puppy/dog sites and the URL patterns for
/// deep-linking into their breed-filtered listing pages. No API calls — the
/// UI links straight to each site; all navigation happens in the visitor's browser.
/// URL patterns were verified July 2026 (see docs/SOURCES.md).
/// </summary>
public static class SiteCatalog
{
    public static readonly IReadOnlyList<Breed> Breeds =
    [
        new("australian-shepherd", "Australian Shepherd", "australian-shepherd"),
        new("beagle", "Beagle", "beagle"),
        new("bernese-mountain-dog", "Bernese Mountain Dog", "bernese-mountain-dog"),
        new("boxer", "Boxer", "boxer"),
        new("bulldog", "Bulldog", "bulldog"),
        new("cavalier-king-charles-spaniel", "Cavalier King Charles Spaniel", "cavalier-king-charles-spaniel"),
        new("chihuahua", "Chihuahua", "chihuahua"),
        new("dachshund", "Dachshund", "dachshund"),
        new("french-bulldog", "French Bulldog", "french-bulldog"),
        new("german-shepherd", "German Shepherd", "german-shepherd-dog"),
        new("golden-retriever", "Golden Retriever", "golden-retriever"),
        new("great-dane", "Great Dane", "great-dane"),
        new("labrador-retriever", "Labrador Retriever", "labrador-retriever"),
        new("pembroke-welsh-corgi", "Pembroke Welsh Corgi", "pembroke-welsh-corgi"),
        new("pomeranian", "Pomeranian", "pomeranian"),
        new("poodle", "Poodle", "poodle-standard"),
        new("rottweiler", "Rottweiler", "rottweiler"),
        new("shih-tzu", "Shih Tzu", "shih-tzu"),
        new("siberian-husky", "Siberian Husky", "siberian-husky"),
        new("yorkshire-terrier", "Yorkshire Terrier", "yorkshire-terrier"),
    ];

    public static readonly IReadOnlyList<Site> Sites =
    [
        new("akc", "AKC Marketplace", SiteCategory.BreederMarketplace,
            "Puppies from AKC-registered litters, listed by the American Kennel Club.",
            "https://marketplace.akc.org/puppies"),
        new("gooddog", "Good Dog", SiteCategory.BreederMarketplace,
            "Breeders screened against community health and care standards.",
            "https://www.gooddog.com"),
        new("puppyspot", "PuppySpot", SiteCategory.BreederMarketplace,
            "Nationwide puppy placement with a vetted, USDA-inspected breeder network.",
            "https://www.puppyspot.com"),
        new("petfinder", "Petfinder", SiteCategory.AdoptionPlatform,
            "The largest US adoption search engine — thousands of shelters and rescues.",
            "https://www.petfinder.com/search/dogs-for-adoption/us/"),
        new("adoptapet", "Adopt a Pet", SiteCategory.AdoptionPlatform,
            "North America's largest non-profit pet adoption website.",
            "https://www.adoptapet.com/dog-adoption"),
        new("aspca", "ASPCA Adoption Center", SiteCategory.Shelter,
            "Adoption programs from the American Society for the Prevention of Cruelty to Animals.",
            "https://www.aspca.org/adopt-pet"),
        new("bestfriends", "Best Friends Animal Society", SiteCategory.Rescue,
            "The nation's largest no-kill sanctuary with a nationwide adoption network.",
            "https://bestfriends.org/adopt"),
        new("akcrescue", "AKC Rescue Network", SiteCategory.Rescue,
            "Directory of 450+ breed-specific rescue groups recognized by the AKC.",
            "https://www.akc.org/akc-rescue-network"),
    ];

    /// <summary>Builds the deepest listing link each site supports for the given breed/state.</summary>
    public static string BuildLink(Site site, Breed? breed, string? state)
    {
        var stateSegment = string.IsNullOrWhiteSpace(state) ? null : state.ToLowerInvariant();

        return site.Id switch
        {
            "akc" when breed is not null =>
                $"https://marketplace.akc.org/puppies/{breed.AkcSlug}",
            // PuppySpot intentionally has no breed deep link: their URL pattern can't be
            // verified behind Cloudflare, so it falls through to the homepage.
            "gooddog" when breed is not null =>
                $"https://www.gooddog.com/breeds/{breed.Slug}",
            "petfinder" =>
                $"https://www.petfinder.com/search/dogs-for-adoption/us/{(stateSegment is null ? "" : stateSegment + "/")}"
                + (breed is null ? "" : $"?breed%5B0%5D={WebUtility.UrlEncode(breed.DisplayName)}"),
            "adoptapet" when breed is not null =>
                $"https://www.adoptapet.com/s/adopt-a-{breed.Slug}",
            _ => site.HomeUrl,
        };
    }

    public static string BuildLinkLabel(Site site, Breed? breed) =>
        breed is null
            ? $"Browse dogs on {site.Name}"
            : site.Id switch
            {
                "aspca" or "bestfriends" => $"Browse dogs on {site.Name}",
                "puppyspot" => $"Browse puppies on {site.Name}",
                "akcrescue" => $"Find {breed.DisplayName} rescues on {site.Name}",
                _ => $"See {breed.DisplayName}s on {site.Name}",
            };
}
