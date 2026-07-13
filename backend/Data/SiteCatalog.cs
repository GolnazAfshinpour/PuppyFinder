using System.Net;

namespace PuppyFinder.Api.Data;

/// <summary>
/// Breed with search slugs plus the traits the quiz scores against.
/// Trait scales are 1–5; prices are typical US ranges (approximate by design).
/// </summary>
public record Breed(
    string Slug,
    string DisplayName,
    string AkcSlug,
    string Size,            // Teacup | Small | Medium | Large
    int Energy,             // 1 couch potato … 5 needs a job
    int Grooming,           // 1 wash-and-go … 5 salon regular
    int Shedding,           // 1 minimal … 5 fur everywhere
    int KidFriendly,        // 1 poor … 5 great
    int ApartmentFriendly,  // 1 needs space … 5 thrives in apartments
    int PriceLow,
    int PriceHigh,
    string Blurb,
    // Aliases like "Teacup Poodle" aren't a recognized breed: sites list them under
    // the parent breed, so links search the parent while the display name stays as typed.
    string? LinkSlugOverride = null,
    string? SearchNameOverride = null,
    bool IncludeInQuiz = true)
{
    public string TypicalPrice => $"${PriceLow:n0}–${PriceHigh:n0}";
    public string LinkSlug => LinkSlugOverride ?? Slug;
    public string SearchName => SearchNameOverride ?? DisplayName;
}

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
    string HomeUrl,
    string Kind,        // "Buy from breeders" | "Adopt"
    string Vetting,     // what screening the site applies
    string PriceNote,
    string Delivery,    // how the dog gets to you
    string BestFor);

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
        new("australian-shepherd", "Australian Shepherd", "australian-shepherd",
            "Medium", 5, 3, 4, 4, 2, 800, 2000,
            "Brilliant herding dog that needs a job — happiest with active owners and space to run."),
        new("beagle", "Beagle", "beagle",
            "Medium", 4, 1, 3, 5, 3, 500, 1200,
            "Merry, nose-driven family dog; easygoing coat, loves company and kids."),
        new("bernese-mountain-dog", "Bernese Mountain Dog", "bernese-mountain-dog",
            "Large", 3, 4, 5, 5, 1, 1500, 3500,
            "Gentle giant from the Swiss Alps; wonderful with kids, sheds heavily, needs room."),
        new("boxer", "Boxer", "boxer",
            "Large", 4, 1, 2, 4, 2, 1000, 2500,
            "Playful, patient, and protective — a clownish athlete that adores its family."),
        new("bulldog", "Bulldog", "bulldog",
            "Medium", 1, 2, 2, 4, 5, 1500, 4000,
            "Calm, low-key companion; happy with short strolls and a good couch."),
        new("cavalier-king-charles-spaniel", "Cavalier King Charles Spaniel", "cavalier-king-charles-spaniel",
            "Small", 2, 3, 3, 5, 5, 1800, 3500,
            "Affectionate lap dog that gets along with everyone — kids, cats, city life."),
        new("chihuahua", "Chihuahua", "chihuahua",
            "Small", 2, 1, 2, 2, 5, 500, 1500,
            "Tiny, devoted, and portable; best with gentle older kids and adults."),
        new("dachshund", "Dachshund", "dachshund",
            "Small", 2, 2, 2, 3, 4, 500, 1500,
            "Curious, brave little hound; apartment-sized with a big personality."),
        new("french-bulldog", "French Bulldog", "french-bulldog",
            "Small", 2, 1, 2, 4, 5, 2500, 5000,
            "The quintessential city dog — compact, low-exercise, and endlessly charming."),
        new("german-shepherd", "German Shepherd", "german-shepherd-dog",
            "Large", 5, 3, 5, 4, 1, 1000, 3000,
            "Loyal, trainable working dog; thrives with structure, exercise, and space."),
        new("golden-retriever", "Golden Retriever", "golden-retriever",
            "Large", 4, 3, 4, 5, 2, 1000, 3000,
            "The classic family dog — gentle, eager to please, and great with kids."),
        new("great-dane", "Great Dane", "great-dane",
            "Large", 2, 1, 3, 4, 2, 1000, 3000,
            "Surprisingly mellow giant; short walks, big couch, bigger heart."),
        new("labrador-retriever", "Labrador Retriever", "labrador-retriever",
            "Large", 4, 2, 4, 5, 2, 800, 2500,
            "America's favorite for a reason — friendly, sturdy, and up for anything."),
        new("pembroke-welsh-corgi", "Pembroke Welsh Corgi", "pembroke-welsh-corgi",
            "Small", 4, 2, 5, 4, 4, 1000, 2500,
            "Big-dog brain on short legs; smart, vocal, and sheds more than you'd think."),
        new("pomeranian", "Pomeranian", "pomeranian",
            "Small", 3, 4, 3, 2, 5, 1000, 3000,
            "Bold little fluffball; thrives in apartments with owners who enjoy grooming."),
        new("poodle", "Poodle (Standard)", "poodle-standard",
            "Large", 4, 5, 1, 4, 3, 1500, 3500,
            "Whip-smart and nearly non-shedding — needs regular grooming and mental exercise."),
        new("rottweiler", "Rottweiler", "rottweiler",
            "Large", 4, 1, 3, 3, 1, 1000, 3000,
            "Confident guardian; devoted to family, best with experienced owners and space."),
        new("shih-tzu", "Shih Tzu", "shih-tzu",
            "Small", 1, 5, 1, 4, 5, 800, 2000,
            "Bred purely for companionship — minimal exercise, maximal lap time, daily brushing."),
        new("siberian-husky", "Siberian Husky", "siberian-husky",
            "Medium", 5, 3, 5, 4, 1, 800, 2000,
            "Beautiful escape artist with marathon energy; needs cold-weather-level exercise."),
        new("yorkshire-terrier", "Yorkshire Terrier", "yorkshire-terrier",
            "Small", 3, 4, 1, 2, 5, 1200, 3000,
            "Feisty toy terrier with a silky, low-shed coat; ideal for compact homes."),

        // Teacup searches — not a recognized breed; sites list these under the parent
        // breed, so links target the parent. Kept out of the quiz (traits mirror parents).
        new("teacup-poodle", "Teacup Poodle", "poodle-toy",
            "Teacup", 2, 5, 1, 2, 5, 2000, 5000,
            "Extra-small Toy Poodle. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "poodle", SearchNameOverride: "Poodle", IncludeInQuiz: false),
        new("teacup-yorkie", "Teacup Yorkie", "yorkshire-terrier",
            "Teacup", 2, 4, 1, 1, 5, 1500, 4500,
            "Extra-small Yorkshire Terrier. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "yorkshire-terrier", SearchNameOverride: "Yorkshire Terrier", IncludeInQuiz: false),
        new("teacup-chihuahua", "Teacup Chihuahua", "chihuahua",
            "Teacup", 2, 1, 2, 1, 5, 1200, 3500,
            "Extra-small Chihuahua. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "chihuahua", SearchNameOverride: "Chihuahua", IncludeInQuiz: false),
        new("teacup-pomeranian", "Teacup Pomeranian", "pomeranian",
            "Teacup", 2, 4, 3, 1, 5, 1500, 5000,
            "Extra-small Pomeranian. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "pomeranian", SearchNameOverride: "Pomeranian", IncludeInQuiz: false),
        new("teacup-maltese", "Teacup Maltese", "maltese",
            "Teacup", 2, 4, 1, 1, 5, 1500, 4500,
            "Extra-small Maltese. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "maltese", SearchNameOverride: "Maltese", IncludeInQuiz: false),
    ];

    public static readonly IReadOnlyList<Site> Sites =
    [
        new("akc", "AKC Marketplace", SiteCategory.BreederMarketplace,
            "Puppies from AKC-registered litters, listed by the American Kennel Club.",
            "https://marketplace.akc.org/puppies",
            Kind: "Buy from breeders",
            Vetting: "All litters from AKC-registered parents; breeder programs badged (Breeder of Merit, etc.)",
            PriceNote: "Breeder pricing — varies widely by breed and pedigree",
            Delivery: "Arranged with each breeder (often pickup)",
            BestFor: "Pedigreed puppies with verifiable registration"),
        new("gooddog", "Good Dog", SiteCategory.BreederMarketplace,
            "Marketplace that screens breeders against community health and care standards.",
            "https://www.gooddog.com",
            Kind: "Buy from breeders",
            Vetting: "Breeders screened against Good Dog's health & care standards; secure payments",
            PriceNote: "Breeder pricing — health-tested lines often cost more",
            Delivery: "Arranged with each breeder",
            BestFor: "Health-focused buyers who want screened breeders and safe payment"),
        new("puppyspot", "PuppySpot", SiteCategory.BreederMarketplace,
            "Nationwide puppy placement with a vetted, USDA-inspected breeder network.",
            "https://www.puppyspot.com",
            Kind: "Buy from breeders",
            Vetting: "Accepts <10% of breeder applicants; USDA-inspected network; health commitment",
            PriceNote: "$$$ — premium, all-inclusive pricing",
            Delivery: "Nationwide delivery coordinated by PuppySpot",
            BestFor: "Hands-off buying with delivery handled for you"),
        new("petfinder", "Petfinder", SiteCategory.AdoptionPlatform,
            "The largest US adoption search engine — thousands of shelters and rescues.",
            "https://www.petfinder.com/search/dogs-for-adoption/us/",
            Kind: "Adopt",
            Vetting: "Listings come from registered shelters and rescue organizations",
            PriceNote: "Adoption fees, typically $50–$500",
            Delivery: "Local — you visit the shelter or rescue",
            BestFor: "The widest adoption search in the country"),
        new("adoptapet", "Adopt a Pet", SiteCategory.AdoptionPlatform,
            "North America's largest non-profit pet adoption website.",
            "https://www.adoptapet.com/dog-adoption",
            Kind: "Adopt",
            Vetting: "Non-profit; aggregates shelters and rescue groups",
            PriceNote: "Adoption fees, typically $50–$500",
            Delivery: "Local — coordinated with the shelter or rescue",
            BestFor: "Breed-specific adoption pages that are easy to browse"),
        new("aspca", "ASPCA Adoption Center", SiteCategory.Shelter,
            "Adoption programs from the American Society for the Prevention of Cruelty to Animals.",
            "https://www.aspca.org/adopt-pet",
            Kind: "Adopt",
            Vetting: "Animals in ASPCA care with health and behavior evaluations",
            PriceNote: "Adoption fees; senior/special programs often discounted",
            Delivery: "Local (NYC & LA adoption centers)",
            BestFor: "Adopters near ASPCA centers wanting evaluated dogs"),
        new("bestfriends", "Best Friends Animal Society", SiteCategory.Rescue,
            "The nation's largest no-kill sanctuary with a nationwide adoption network.",
            "https://bestfriends.org/adopt",
            Kind: "Adopt",
            Vetting: "No-kill network; dogs in sanctuary and partner-shelter care",
            PriceNote: "Adoption fees, often modest",
            Delivery: "Local via centers and partner shelters",
            BestFor: "Mission-driven adopters supporting no-kill rescue"),
        new("akcrescue", "AKC Rescue Network", SiteCategory.Rescue,
            "Directory of 450+ breed-specific rescue groups recognized by the AKC.",
            "https://www.akc.org/akc-rescue-network",
            Kind: "Adopt",
            Vetting: "Breed clubs' own rescue groups, recognized by the AKC",
            PriceNote: "Rescue adoption fees, typically $150–$500",
            Delivery: "Varies by rescue group",
            BestFor: "Adopting a specific breed from people who know it best"),
    ];

    private static readonly Dictionary<string, string> StateNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = "alabama", ["AK"] = "alaska", ["AZ"] = "arizona", ["AR"] = "arkansas",
        ["CA"] = "california", ["CO"] = "colorado", ["CT"] = "connecticut", ["DE"] = "delaware",
        ["FL"] = "florida", ["GA"] = "georgia", ["HI"] = "hawaii", ["ID"] = "idaho",
        ["IL"] = "illinois", ["IN"] = "indiana", ["IA"] = "iowa", ["KS"] = "kansas",
        ["KY"] = "kentucky", ["LA"] = "louisiana", ["ME"] = "maine", ["MD"] = "maryland",
        ["MA"] = "massachusetts", ["MI"] = "michigan", ["MN"] = "minnesota", ["MS"] = "mississippi",
        ["MO"] = "missouri", ["MT"] = "montana", ["NE"] = "nebraska", ["NV"] = "nevada",
        ["NH"] = "new-hampshire", ["NJ"] = "new-jersey", ["NM"] = "new-mexico", ["NY"] = "new-york",
        ["NC"] = "north-carolina", ["ND"] = "north-dakota", ["OH"] = "ohio", ["OK"] = "oklahoma",
        ["OR"] = "oregon", ["PA"] = "pennsylvania", ["RI"] = "rhode-island", ["SC"] = "south-carolina",
        ["SD"] = "south-dakota", ["TN"] = "tennessee", ["TX"] = "texas", ["UT"] = "utah",
        ["VT"] = "vermont", ["VA"] = "virginia", ["WA"] = "washington", ["WV"] = "west-virginia",
        ["WI"] = "wisconsin", ["WY"] = "wyoming",
    };

    /// <summary>Builds the deepest listing link each site supports for the given breed/state.</summary>
    public static string BuildLink(Site site, Breed? breed, string? state)
    {
        var stateSegment = string.IsNullOrWhiteSpace(state) ? null : state.ToLowerInvariant();
        var stateName = state is not null && StateNames.TryGetValue(state, out var name) ? name : null;

        return site.Id switch
        {
            "akc" when breed is not null =>
                $"https://marketplace.akc.org/puppies/{breed.AkcSlug}",
            // Good Dog and PuppySpot URL patterns discovered via search-engine indexes
            // (their bot protection blocks direct verification): gooddog.com/{breed}[/{state}]
            // is the listings page; /breeds/{breed} is only a profile page.
            "gooddog" when breed is not null =>
                $"https://www.gooddog.com/{breed.LinkSlug}{(stateSegment is null ? "" : $"/{stateSegment}")}",
            "puppyspot" when breed is not null =>
                $"https://www.puppyspot.com/puppies-for-sale-by-breeders/breed/{breed.LinkSlug}",
            // Petfinder's Dec-2025 rebuild dropped URL-driven search filters (any search URL
            // renders "0 results" until the visitor sets a location), so breed searches land
            // on their breed adoption page instead.
            "petfinder" when breed is not null =>
                $"https://www.petfinder.com/dogs-and-puppies/breeds/{breed.LinkSlug}/",
            "petfinder" =>
                "https://www.petfinder.com/search/dogs-for-adoption/us/",
            "adoptapet" when breed is not null =>
                $"https://www.adoptapet.com/s/adopt-a-{breed.LinkSlug}{(stateName is null ? "" : $"/{stateName}")}",
            _ => site.HomeUrl,
        };
    }

    public static string BuildLinkLabel(Site site, Breed? breed) =>
        breed is null
            ? $"Browse dogs on {site.Name}"
            : site.Id switch
            {
                "aspca" or "bestfriends" => $"Browse dogs on {site.Name}",
                "akcrescue" => $"Find {breed.DisplayName} rescues on {site.Name}",
                _ => $"See {breed.DisplayName}s on {site.Name}",
            };
}
