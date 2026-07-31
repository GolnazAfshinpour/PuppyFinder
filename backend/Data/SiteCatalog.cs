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
    bool IncludeInQuiz = true,
    // dog.ceo photo API path ("breed" or "breed/subbreed"), verified July 2026;
    // null = no photos available for this breed.
    string? DogCeoPath = null)
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
    string BestFor,
    string? Caution = null); // honest one-line warning for weak-vetting sites

/// <summary>
/// Static catalog of legitimate US puppy/dog sites and the URL patterns for
/// deep-linking into their breed-filtered listing pages. No API calls — the
/// UI links straight to each site; all navigation happens in the visitor's browser.
/// URL patterns were verified July 2026 (see docs/SOURCES.md).
/// </summary>
public static class SiteCatalog
{
    /// <summary>
    /// Slugs of the hand-curated breeds — the ones with real trait scores for the
    /// quiz and filters. Callers previously inferred this from "has a price", which
    /// stopped being equivalent once prices moved to the database.
    /// </summary>
    public static bool IsCurated(string slug) => CuratedSlugs.Value.Contains(slug);

    // Lazy, not a plain static initializer: static fields initialize in declaration
    // order, and Breeds is declared below this — eager initialization would read it
    // as null and throw during type init.
    private static readonly Lazy<HashSet<string>> CuratedSlugs = new(() =>
        new HashSet<string>(Breeds.Select(b => b.Slug), StringComparer.OrdinalIgnoreCase));

    public static readonly IReadOnlyList<Breed> Breeds =
    [
        new("australian-shepherd", "Australian Shepherd", "australian-shepherd",
            "Medium", 5, 3, 4, 4, 2, 800, 2000,
            "Brilliant herding dog that needs a job — happiest with active owners and space to run.", DogCeoPath: "australian/shepherd"),
        new("beagle", "Beagle", "beagle",
            "Medium", 4, 1, 3, 5, 3, 500, 1200,
            "Merry, nose-driven family dog; easygoing coat, loves company and kids.", DogCeoPath: "beagle"),
        new("bernese-mountain-dog", "Bernese Mountain Dog", "bernese-mountain-dog",
            "Large", 3, 4, 5, 5, 1, 1500, 3500,
            "Gentle giant from the Swiss Alps; wonderful with kids, sheds heavily, needs room.", DogCeoPath: "mountain/bernese"),
        new("boxer", "Boxer", "boxer",
            "Large", 4, 1, 2, 4, 2, 1000, 2500,
            "Playful, patient, and protective — a clownish athlete that adores its family.", DogCeoPath: "boxer"),
        new("bulldog", "Bulldog", "bulldog",
            "Medium", 1, 2, 2, 4, 5, 1500, 4000,
            "Calm, low-key companion; happy with short strolls and a good couch.", DogCeoPath: "bulldog/english"),
        new("cavalier-king-charles-spaniel", "Cavalier King Charles Spaniel", "cavalier-king-charles-spaniel",
            "Small", 2, 3, 3, 5, 5, 1800, 3500,
            "Affectionate lap dog that gets along with everyone — kids, cats, city life.", DogCeoPath: "spaniel/blenheim"),
        new("chihuahua", "Chihuahua", "chihuahua",
            "Small", 2, 1, 2, 2, 5, 500, 1500,
            "Tiny, devoted, and portable; best with gentle older kids and adults.", DogCeoPath: "chihuahua"),
        new("dachshund", "Dachshund", "dachshund",
            "Small", 2, 2, 2, 3, 4, 500, 1500,
            "Curious, brave little hound; apartment-sized with a big personality.", DogCeoPath: "dachshund"),
        new("french-bulldog", "French Bulldog", "french-bulldog",
            "Small", 2, 1, 2, 4, 5, 2500, 5000,
            "The quintessential city dog — compact, low-exercise, and endlessly charming.", DogCeoPath: "bulldog/french"),
        new("german-shepherd", "German Shepherd", "german-shepherd-dog",
            "Large", 5, 3, 5, 4, 1, 1000, 3000,
            "Loyal, trainable working dog; thrives with structure, exercise, and space.", DogCeoPath: "german/shepherd"),
        new("golden-retriever", "Golden Retriever", "golden-retriever",
            "Large", 4, 3, 4, 5, 2, 1000, 3000,
            "The classic family dog — gentle, eager to please, and great with kids.", DogCeoPath: "retriever/golden"),
        new("great-dane", "Great Dane", "great-dane",
            "Large", 2, 1, 3, 4, 2, 1000, 3000,
            "Surprisingly mellow giant; short walks, big couch, bigger heart.", DogCeoPath: "dane/great"),
        new("labrador-retriever", "Labrador Retriever", "labrador-retriever",
            "Large", 4, 2, 4, 5, 2, 800, 2500,
            "America's favorite for a reason — friendly, sturdy, and up for anything.", DogCeoPath: "labrador"),
        new("pembroke-welsh-corgi", "Pembroke Welsh Corgi", "pembroke-welsh-corgi",
            "Small", 4, 2, 5, 4, 4, 1000, 2500,
            "Big-dog brain on short legs; smart, vocal, and sheds more than you'd think.", DogCeoPath: "pembroke"),
        new("pomeranian", "Pomeranian", "pomeranian",
            "Small", 3, 4, 3, 2, 5, 1000, 3000,
            "Bold little fluffball; thrives in apartments with owners who enjoy grooming.", DogCeoPath: "pomeranian"),
        new("poodle", "Poodle (Standard)", "poodle-standard",
            "Large", 4, 5, 1, 4, 3, 1500, 3500,
            "Whip-smart and nearly non-shedding — needs regular grooming and mental exercise.", DogCeoPath: "poodle/standard"),
        new("rottweiler", "Rottweiler", "rottweiler",
            "Large", 4, 1, 3, 3, 1, 1000, 3000,
            "Confident guardian; devoted to family, best with experienced owners and space.", DogCeoPath: "rottweiler"),
        new("shih-tzu", "Shih Tzu", "shih-tzu",
            "Small", 1, 5, 1, 4, 5, 800, 2000,
            "Bred purely for companionship — minimal exercise, maximal lap time, daily brushing.", DogCeoPath: "shihtzu"),
        new("siberian-husky", "Siberian Husky", "siberian-husky",
            "Medium", 5, 3, 5, 4, 1, 800, 2000,
            "Beautiful escape artist with marathon energy; needs cold-weather-level exercise.", DogCeoPath: "husky"),
        new("yorkshire-terrier", "Yorkshire Terrier", "yorkshire-terrier",
            "Small", 3, 4, 1, 2, 5, 1200, 3000,
            "Feisty toy terrier with a silky, low-shed coat; ideal for compact homes.", DogCeoPath: "terrier/yorkshire"),

        // Teacup searches — not a recognized breed; sites list these under the parent
        // breed, so links target the parent. Kept out of the quiz (traits mirror parents).
        new("teacup-poodle", "Teacup Poodle", "poodle-toy",
            "Teacup", 2, 5, 1, 2, 5, 2000, 5000,
            "Extra-small Toy Poodle. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "poodle", SearchNameOverride: "Poodle", IncludeInQuiz: false, DogCeoPath: "poodle/toy"),
        new("teacup-yorkie", "Teacup Yorkie", "yorkshire-terrier",
            "Teacup", 2, 4, 1, 1, 5, 1500, 4500,
            "Extra-small Yorkshire Terrier. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "yorkshire-terrier", SearchNameOverride: "Yorkshire Terrier", IncludeInQuiz: false, DogCeoPath: "terrier/yorkshire"),
        new("teacup-chihuahua", "Teacup Chihuahua", "chihuahua",
            "Teacup", 2, 1, 2, 1, 5, 1200, 3500,
            "Extra-small Chihuahua. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "chihuahua", SearchNameOverride: "Chihuahua", IncludeInQuiz: false, DogCeoPath: "chihuahua"),
        new("teacup-pomeranian", "Teacup Pomeranian", "pomeranian",
            "Teacup", 2, 4, 3, 1, 5, 1500, 5000,
            "Extra-small Pomeranian. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "pomeranian", SearchNameOverride: "Pomeranian", IncludeInQuiz: false, DogCeoPath: "pomeranian"),
        new("teacup-maltese", "Teacup Maltese", "maltese",
            "Teacup", 2, 4, 1, 1, 5, 1500, 4500,
            "Extra-small Maltese. \"Teacup\" is a size label, not a breed — vet the breeder's health practices extra carefully.",
            LinkSlugOverride: "maltese", SearchNameOverride: "Maltese", IncludeInQuiz: false, DogCeoPath: "maltese"),
    ];

    private static readonly IReadOnlyList<Site> AllSites =
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
        new("puppies", "Puppies.com", SiteCategory.BreederMarketplace,
            "One of the oldest and largest puppy classifieds sites (since 2003), with thousands of listings nationwide.",
            "https://www.puppies.com",
            Kind: "Buy from breeders",
            Vetting: "Seller reviews and optional ID badges — listings themselves are not screened",
            PriceNote: "Wide range — classifieds pricing set by each seller",
            Delivery: "Arranged directly with the seller",
            BestFor: "The biggest raw selection, if you're prepared to vet sellers yourself",
            Caution: "Open classifieds with minimal vetting — screen any seller yourself, insist on health records, and never wire money."),
        new("pawrade", "Pawrade", SiteCategory.BreederMarketplace,
            "Broker marketplace with a nationwide breeder network, health guarantee, and door delivery.",
            "https://www.pawrade.com",
            Kind: "Buy from breeders",
            Vetting: "Broker model: Pawrade says it background-checks breeders; you buy through Pawrade, not the breeder directly",
            PriceNote: "$$$ — broker pricing, typically from ~$2,000",
            Delivery: "Nationwide delivery coordinated by Pawrade",
            BestFor: "Concierge-style buying with a scam guarantee"),
        new("lancaster", "Lancaster Puppies", SiteCategory.BreederMarketplace,
            "Very high-traffic classifieds site centered on Pennsylvania/Ohio breeders.",
            "https://www.lancasterpuppies.com",
            Kind: "Buy from breeders",
            Vetting: "Classifieds — sellers are not screened by the site",
            PriceNote: "Often below-market prices — a signal to inspect carefully",
            Delivery: "Arranged directly with the seller; many expect pickup",
            BestFor: "East-coast buyers willing to visit breeders in person",
            Caution: "Reviews include sick-puppy and puppy-mill complaints — visit in person, meet the parents, and verify vet records before paying anything."),
        new("greenfield", "Greenfield Puppies", SiteCategory.BreederMarketplace,
            "Long-running Pennsylvania-based puppy classifieds covering the East Coast.",
            "https://www.greenfieldpuppies.com",
            Kind: "Buy from breeders",
            Vetting: "Classifieds — breeder claims (vet checks, guarantees) are per-listing, not site-verified",
            PriceNote: "Often below-market prices — a signal to inspect carefully",
            Delivery: "Arranged directly with the seller",
            BestFor: "East-coast buyers willing to visit breeders in person",
            Caution: "Same Amish-country classifieds model as Lancaster — do your own vetting, in person, before paying anything."),
        new("craigslist", "Craigslist Pets", SiteCategory.AdoptionPlatform,
            "Local classifieds rehoming section. Included because people use it — read the caution first.",
            "https://geo.craigslist.org/iso/us",
            Kind: "Adopt",
            Vetting: "None — anonymous classifieds. Pet sales violate Craigslist's own rules (rehoming with a small adoption fee only)",
            PriceNote: "Rehoming fees only; a priced 'sale' listing is already breaking site rules",
            Delivery: "Local only — meet in person",
            BestFor: "Local rehoming finds, if you exercise maximum caution",
            Caution: "The single most-reported source of puppy scams (68% of reports). Never wire money, never pay a deposit sight-unseen, and only hand over anything in person after meeting the puppy."),
        new("rescueme", "Rescue Me!", SiteCategory.Rescue,
            "Volunteer-run network with over 1 million animals adopted; browse rescues breed-by-breed and state-by-state.",
            "https://www.rescueme.org",
            Kind: "Adopt",
            Vetting: "Free postings from shelters and individuals — meet the animal and poster yourself",
            PriceNote: "Adoption fees vary; some fully sponsored",
            Delivery: "Local — you contact the poster directly",
            BestFor: "Breed-specific rescue searches right down to your state"),
    ];

    // Cards render in this order: buying sites first, then adoption sites;
    // within each group strongest vetting first, caution-labeled classifieds last.
    private static readonly string[] TrustOrder =
    [
        "gooddog", "akc", "puppyspot", "pawrade", "puppies", "greenfield", "lancaster",
        "petfinder", "adoptapet", "rescueme", "akcrescue", "bestfriends", "aspca", "craigslist",
    ];

    public static readonly IReadOnlyList<Site> Sites =
        AllSites.OrderBy(s => Array.IndexOf(TrustOrder, s.Id)).ToList();

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

    // Rescue Me! uses nickname subdomains (yorkie, lab, corgi…) that can't be derived
    // mechanically — verified entries only; unknown breeds fall back to dog.rescueme.org.
    private static readonly Dictionary<string, string> RescueMeSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        ["australian-shepherd"] = "australianshepherd",
        ["beagle"] = "beagle",
        ["bernese-mountain-dog"] = "bernesemountaindog",
        ["boxer"] = "boxer",
        ["bulldog"] = "bulldog",
        ["cavalier-king-charles-spaniel"] = "cavalier",
        ["chihuahua"] = "chihuahua",
        ["dachshund"] = "dachshund",
        ["french-bulldog"] = "frenchbulldog",
        ["german-shepherd"] = "germanshepherd",
        ["golden-retriever"] = "goldenretriever",
        ["great-dane"] = "greatdane",
        ["labrador-retriever"] = "lab",
        ["pembroke-welsh-corgi"] = "corgi",
        ["pomeranian"] = "pomeranian",
        ["poodle"] = "poodle",
        ["rottweiler"] = "rottweiler",
        ["shih-tzu"] = "shihtzu",
        ["siberian-husky"] = "husky",
        ["yorkshire-terrier"] = "yorkie",
    };

    // Craigslist metro subdomains, verified July 2026 against craigslist.org/about/sites.
    // Keyed by normalized city name; the state guards against same-name cities in other
    // states (Portland ME must not land on Oregon's site). Unknown cities fall back to
    // the state chooser page. Verified entries only — same policy as RescueMeSubdomains.
    private static readonly Dictionary<string, (string Subdomain, string State)> CraigslistMetros = new(StringComparer.OrdinalIgnoreCase)
    {
        ["houston"] = ("houston", "TX"), ["dallas"] = ("dallas", "TX"), ["fortworth"] = ("dallas", "TX"),
        ["austin"] = ("austin", "TX"), ["sanantonio"] = ("sanantonio", "TX"),
        ["newyork"] = ("newyork", "NY"), ["newyorkcity"] = ("newyork", "NY"),
        ["losangeles"] = ("losangeles", "CA"), ["sanfrancisco"] = ("sfbay", "CA"), ["sanjose"] = ("sfbay", "CA"),
        ["sandiego"] = ("sandiego", "CA"), ["sacramento"] = ("sacramento", "CA"), ["orangecounty"] = ("orangecounty", "CA"),
        ["chicago"] = ("chicago", "IL"), ["seattle"] = ("seattle", "WA"), ["tacoma"] = ("seattle", "WA"),
        ["denver"] = ("denver", "CO"), ["phoenix"] = ("phoenix", "AZ"), ["miami"] = ("miami", "FL"),
        ["tampa"] = ("tampa", "FL"), ["orlando"] = ("orlando", "FL"), ["atlanta"] = ("atlanta", "GA"),
        ["boston"] = ("boston", "MA"), ["philadelphia"] = ("philadelphia", "PA"), ["pittsburgh"] = ("pittsburgh", "PA"),
        ["minneapolis"] = ("minneapolis", "MN"), ["stpaul"] = ("minneapolis", "MN"),
        ["detroit"] = ("detroit", "MI"), ["portland"] = ("portland", "OR"), ["lasvegas"] = ("lasvegas", "NV"),
        ["stlouis"] = ("stlouis", "MO"), ["kansascity"] = ("kansascity", "MO"), ["baltimore"] = ("baltimore", "MD"),
        ["charlotte"] = ("charlotte", "NC"), ["raleigh"] = ("raleigh", "NC"), ["durham"] = ("raleigh", "NC"),
        ["cleveland"] = ("cleveland", "OH"), ["columbus"] = ("columbus", "OH"),
        ["nashville"] = ("nashville", "TN"), ["indianapolis"] = ("indianapolis", "IN"),
        ["saltlakecity"] = ("saltlakecity", "UT"),
    };

    /// <summary>
    /// Builds the deepest listing link each site supports for the given breed/state/city.
    /// Only these near-universal filters are offered: per-site filters like sex or price
    /// carry to so few sites that they read as broken everywhere else (and unknown query
    /// params actively break some sites — Pawrade degrades to an empty search).
    /// </summary>
    public static string BuildLink(Site site, Breed? breed, string? state, string? city = null)
    {
        var stateSegment = string.IsNullOrWhiteSpace(state) ? null : state.ToLowerInvariant();
        var stateName = state is not null && StateNames.TryGetValue(state, out var name) ? name : null;

        // City-level pages only exist under a state, so a city without a state is ignored.
        var citySlug = stateName is not null && !string.IsNullOrWhiteSpace(city)
            ? string.Join('-', city.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            : null;

        return site.Id switch
        {
            "akc" when breed is not null =>
                $"https://marketplace.akc.org/puppies/{breed.AkcSlug}{(stateName is null ? "" : $"/{stateName}")}{(citySlug is null ? "" : $"/{citySlug}")}",
            // Good Dog and PuppySpot URL patterns discovered via search-engine indexes
            // (their bot protection blocks direct verification): gooddog.com/{breed}[/{state}]
            // is the listings page; /breeds/{breed} is only a profile page.
            "gooddog" when breed is not null && citySlug is not null =>
                $"https://www.gooddog.com/{breed.LinkSlug}/{citySlug}-{stateSegment}",
            // Good Dog has real toy-size listing pages for Poodles; size and location
            // can't combine (/{breed}/toy/{city} 404s), so location wins when set.
            "gooddog" when breed is { Size: "Teacup", LinkSlug: "poodle" } && stateSegment is null =>
                "https://www.gooddog.com/poodle/size/toy",
            "gooddog" when breed is not null =>
                $"https://www.gooddog.com/{breed.LinkSlug}{(stateSegment is null ? "" : $"/{stateSegment}")}",
            "puppyspot" when breed is not null && stateName is not null =>
                $"https://www.puppyspot.com/find-puppies/{breed.LinkSlug}/{stateName}",
            "puppyspot" when breed is not null =>
                $"https://www.puppyspot.com/puppies-for-sale-by-breeders/breed/{breed.LinkSlug}",
            "puppyspot" when stateName is not null =>
                $"https://www.puppyspot.com/find-puppies/{stateName}",
            // Petfinder's Dec-2025 rebuild dropped URL-driven search filters (any search URL
            // renders "0 results" until the visitor sets a location), so breed searches land
            // on their breed adoption page instead.
            "petfinder" when breed is not null =>
                $"https://www.petfinder.com/dogs-and-puppies/breeds/{breed.LinkSlug}/",
            "petfinder" =>
                "https://www.petfinder.com/search/dogs-for-adoption/us/",
            "adoptapet" when breed is not null =>
                $"https://www.adoptapet.com/s/adopt-a-{breed.LinkSlug}{(stateName is null ? "" : $"/{stateName}")}{(citySlug is null ? "" : $"/{citySlug}")}",
            "puppies" when breed is not null =>
                $"https://www.puppies.com/find-a-puppy/{breed.LinkSlug}{(stateName is null ? "" : $"/{stateName}")}{(citySlug is null ? "" : $"/{citySlug}")}",
            "lancaster" when breed is not null =>
                $"https://www.lancasterpuppies.com/sale/puppies/{breed.LinkSlug}/{(stateName is null ? "" : $"united-states/{stateName}/")}",
            "lancaster" when stateName is not null =>
                $"https://www.lancasterpuppies.com/sale/puppies/near-me/united-states/{stateName}/",
            "greenfield" when breed is not null =>
                $"https://www.greenfieldpuppies.com/{breed.LinkSlug}-puppies-for-sale/",
            // Pawrade state slugs concatenate words ("newyork") — verified via their sitemap.
            "pawrade" when breed is not null && stateName is not null =>
                $"https://www.pawrade.com/puppies-for-sale/{stateName.Replace("-", "")}/{breed.LinkSlug}/",
            "pawrade" when breed is not null =>
                $"https://www.pawrade.com/puppies/{breed.LinkSlug}/",
            "pawrade" when stateName is not null =>
                $"https://www.pawrade.com/puppies-for-sale/{stateName.Replace("-", "")}/",
            // Craigslist search only exists per metro; a breed query carries only when the
            // typed city maps to a verified metro in the selected state. Otherwise the
            // state chooser page is the deepest safe landing (no nationwide search exists).
            "craigslist" when city is not null && state is not null
                && CraigslistMetros.TryGetValue(NormalizeCityKey(city), out var metro)
                && metro.State.Equals(state, StringComparison.OrdinalIgnoreCase) =>
                $"https://www.craigslist.org/search/area/{metro.Subdomain}?cat=pet{(breed is null ? "" : $"&query={Uri.EscapeDataString(breed.SearchName.ToLowerInvariant())}")}",
            "craigslist" when stateSegment is not null =>
                $"https://geo.craigslist.org/iso/us/{stateSegment}",
            "rescueme" when breed is not null && RescueMeSubdomains.TryGetValue(breed.LinkSlug, out var subdomain) =>
                $"https://{subdomain}.rescueme.org/{(stateName is null ? "" : stateName.Replace("-", ""))}",
            "rescueme" when stateName is not null =>
                $"https://dog.rescueme.org/{stateName.Replace("-", "")}",
            _ => site.HomeUrl,
        };
    }

    // "St. Louis" / "Fort Worth" → "stlouis" / "fortworth", matching Craigslist's naming.
    private static string NormalizeCityKey(string city) =>
        string.Concat(city.Where(char.IsLetter)).ToLowerInvariant();

    /// <summary>
    /// Which of the requested filters actually changed this site's link — shown per
    /// card so visitors know what carries through before they click. Derived by
    /// rebuilding the link without each filter, so it can never drift from
    /// BuildLink's per-site rules.
    /// </summary>
    public static IReadOnlyList<string> AppliedFilters(Site site, Breed? breed, string? state, string? city = null)
    {
        var full = BuildLink(site, breed, state, city);
        var applied = new List<string>(3);
        if (breed is not null && full != BuildLink(site, null, state, city)) applied.Add("breed");
        if (state is not null && full != BuildLink(site, breed, null)) applied.Add("state");
        if (city is not null && full != BuildLink(site, breed, state)) applied.Add("city");
        return applied;
    }

    public static string BuildLinkLabel(Site site, Breed? breed) =>
        breed is null
            ? $"Browse dogs on {site.Name}"
            : site.Id switch
            {
                "aspca" or "bestfriends" => $"Browse dogs on {site.Name}",
                "craigslist" => "Browse local rehoming posts on Craigslist",
                "akcrescue" or "rescueme" => $"Find {breed.DisplayName} rescues on {site.Name}",
                _ => $"See {breed.DisplayName}s on {site.Name}",
            };
}
