using System.Text.Json;
using System.Text.Json.Serialization;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpClient("rescuegroups", client =>
{
    // 40s, not 15: this provider walks three pages of 100 animals with pictures, locations and
    // orgs included, and 15s was set when it made a single request. Exceeding it used to drop
    // every dog from this source for a full cache period. It is a background fetch behind a
    // 10-minute cache, so a slow response costs latency nobody waits on.
    client.Timeout = TimeSpan.FromSeconds(40);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PuppyFinder/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.api+json");
});
builder.Services.AddHttpClient("socrata", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PuppyFinder/1.0");
});
builder.Services.AddHttpClient("dogceo", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PuppyFinder/1.0");
});
// Marketplace listing prices. Identifies itself honestly rather than impersonating a
// browser: if the operator wants to block or rate-limit us they should be able to, and a
// spoofed user-agent would be working around that decision. See ListingSources for the
// terms caveat.
builder.Services.AddHttpClient("listings", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "PuppyFinder/1.0 (+https://github.com/GolnazAfshinpour/PuppyFinder)");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
});
// Breed prices live in SQLite with their provenance, not in source: every figure
// carries a source URL, a verbatim quote, and a retrieval date. See docs/SOURCES.md.
builder.Services.AddSingleton<PriceDb>();
builder.Services.AddSingleton<PriceStore>();
builder.Services.AddSingleton<BreedCatalogService>();
// The research job gathers cited figures; it never decides confidence. Aggregation is a
// pure function over stored observations, so thresholds can be re-tuned for free.
builder.Services.AddSingleton<PriceResearchService>();
builder.Services.AddSingleton<ListingPriceProvider>();
builder.Services.AddSingleton<PriceRefreshJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceRefreshJob>());

// Government open-data feeds — public JSON, no API key needed.
var montgomeryCounty = new SocrataDataset(
    SourceName: "Montgomery County Animal Services",
    SourceUrl: "https://www.montgomerycountymd.gov/animalservices/",
    Endpoint: "https://data.montgomerycountymd.gov/resource/e54u-qx42.json?$limit=100",
    NameField: "petname", BreedField: "breed", AgeField: "petage", SexField: "sex",
    ImageField: "url", LinkField: null, CityField: null,
    DefaultCity: "Derwood", State: "MD",
    AnimalTypeField: "animaltype",
    // Their old adoption/adoptdog.html path 404s after a site restructure (July 2026).
    FallbackListingUrl: "https://www.montgomerycountymd.gov/animal-services-adoption-center/adopt-pet",
    SizeField: "petsize",
    // Verified July 2026 against their PetHarbor pages.
    // No emoji and no city: the card renders a Heroicon, and the city already appears on
    // its own line directly above — "Derwood, MD" was printed twice, two lines apart.
    ContactInfo: "(240) 773-5900",
    // 7315 Muncaster Mill Rd, Derwood MD — the shelter these listings are all held at.
    Latitude: 39.1013,
    Longitude: -77.1500);

var kingCounty = new SocrataDataset(
    SourceName: "King County Pet Adoption",
    SourceUrl: "https://kingcounty.gov/en/dept/executive-services/animals-pets-pests/regional-animal-services/adopt-a-pet",
    Endpoint: "https://data.kingcounty.gov/resource/yaai-7frk.json?record_type=ADOPTABLE&animal_type=Dog&$limit=100",
    NameField: "animal_name", BreedField: "animal_breed", AgeField: "age", SexField: "animal_gender",
    ImageField: "image", LinkField: "link", CityField: "city",
    DefaultCity: "Kent", State: "WA",
    AnimalTypeField: null,
    FallbackListingUrl: "https://kingcounty.gov/en/dept/executive-services/animals-pets-pests/regional-animal-services/adopt-a-pet",
    MemoField: "memo",
    // Verified July 2026 against their PetHarbor pages (visit hours: noon-5 weekdays, noon-4 weekends).
    // Street kept (it is genuinely extra), city dropped as duplicated by the line above.
    ContactInfo: "(206) 296-3936 · 21615 64th Ave S",
    // 21615 64th Ave S, Kent WA — the regional shelter this feed publishes from.
    Latitude: 47.3931,
    Longitude: -122.2589);

builder.Services.AddSingleton<IListingProvider>(sp => new SocrataProvider(
    montgomeryCounty, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<ILogger<SocrataProvider>>()));
builder.Services.AddSingleton<IListingProvider>(sp => new SocrataProvider(
    kingCounty, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<ILogger<SocrataProvider>>()));
builder.Services.AddSingleton<IListingProvider, RescueGroupsProvider>();
builder.Services.AddSingleton<ListingAggregator>();

// Saved-search alerts: JSON-file store + in-process checker. Emails go out via
// SMTP when configured (Smtp:Host); otherwise to data/outbox for inspection.
builder.Services.AddSingleton<AlertStore>();
builder.Services.AddSingleton<IEmailSender>(sp =>
    builder.Configuration["Smtp:Host"] is { Length: > 0 }
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp)
        : ActivatorUtilities.CreateInstance<OutboxEmailSender>(sp));
builder.Services.AddSingleton<AlertChecker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AlertChecker>());

const string FrontendCors = "frontend";
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(FrontendCors);

// First run imports the previously-hardcoded ranges as 'unverified' — they keep
// working but stop claiming a provenance they never had.
await app.Services.GetRequiredService<PriceStore>().SeedFromCatalogAsync(CancellationToken.None);

// lat/lon/radius: distance is *reported* whenever an origin is supplied and only *filters* when a
// radius comes with it. Both are useful separately — "nearest first across the state" is a real
// request, and a radius that quietly dropped dogs would be the worse default.
app.MapGet("/api/listings", async (string? breed, string? state, string? city, string? size,
    string? age, string? sort, bool? includeUnlisted, string? goodWith,
    double? lat, double? lon, int? radius,
    ListingAggregator aggregator, BreedCatalogService catalog, CancellationToken ct) =>
{
    var listings = (await aggregator.GetListingsAsync(ct)).AsEnumerable();

    // "kids,dogs,cats" — a list rather than three flags, so the query string reads as one filter.
    var wants = (goodWith ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var filter = new ListingFilter(
        BreedSearchText: await ResolveBreedTextAsync(breed, catalog, ct),
        State: state,
        City: city,
        Size: size,
        AgeGroup: age,
        IncludeUnlisted: includeUnlisted ?? true,
        Latitude: lat,
        Longitude: lon,
        RadiusMiles: radius,
        GoodWithKids: wants.Contains("kids"),
        GoodWithDogs: wants.Contains("dogs"),
        GoodWithCats: wants.Contains("cats"));

    var matches = ListingQuery.Filter(listings, filter)
        .Select(l => l with
        {
            Unconfirmed = ListingQuery.Unconfirmed(l, filter),
            // Rounded to a whole mile: the coordinates are the rescue's office, so a decimal
            // place would imply precision this data does not have.
            DistanceMiles = ListingQuery.DistanceFor(l, filter) is { } miles
                ? Math.Round(miles)
                : null,
        });

    return Results.Ok(ListingQuery.Sort(matches, sort, filter));
})
.WithName("GetListings");

// One dog by id, so a shared or bookmarked ?dog= link opens its detail view even
// when the visitor's filters would exclude it (or they have none set at all).
app.MapGet("/api/listings/{id}", async (string id, ListingAggregator aggregator, CancellationToken ct) =>
    (await aggregator.GetListingsAsync(ct))
        .FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } listing
        ? Results.Ok(listing)
        // Dogs leave the feeds when they're adopted — that's a success, and the UI
        // says so rather than showing a generic error.
        : Results.NotFound(new { Message = "This dog is no longer listed — they may have found a home." }))
.WithName("GetListing");

app.MapGet("/api/sources", (ListingAggregator aggregator) =>
    Results.Ok(aggregator.GetSourceStatuses()))
.WithName("GetSources");

// Where we actually have live dogs right now, with counts — derived from the data
// itself so it stays correct as feeds are added (or RescueGroups arrives). The UI
// states this plainly instead of showing an empty grid in uncovered areas.
app.MapGet("/api/coverage", async (ListingAggregator aggregator, CancellationToken ct) =>
    Results.Ok((await aggregator.GetListingsAsync(ct))
        .GroupBy(l => l.State.ToUpperInvariant())
        .Select(g => new
        {
            State = g.Key,
            Count = g.Count(),
            Cities = g.Select(l => l.City).Distinct().Order().ToList(),
        })
        .OrderByDescending(c => c.Count)
        .ThenBy(c => c.State)
        .ToList()))
.WithName("GetCoverage");

// What a quoted puppy price says about the seller. Buying is the app's main path,
// and a below-market price is the highest-signal scam check a buyer can run.
app.MapGet("/api/price-check", async (string? breed, int price,
    BreedCatalogService catalog, PriceStore prices, CancellationToken ct) =>
{
    var selected = string.IsNullOrWhiteSpace(breed) ? null : await catalog.FindAsync(breed, ct);
    var backing = selected is null ? null : await prices.FindAsync(selected.Slug, ct);
    return Results.Ok(PriceCheck.Evaluate(selected, price, backing));
})
.WithName("CheckPrice");

// The other half of that question, and the half nothing covered: not "is this quote plausible"
// but "they are asking me for $350, should I send it".
//
// It needs no price range, which is why it exists as its own endpoint rather than a branch of
// the one above. Price screening is live for 50 of 175 breeds and silent for the rest; an
// invented crate fee is the same invented crate fee whatever the breed. It is also the only
// check here aimed at someone who has already paid — BBB's "multi-tiered setup" means most of
// the loss lands on payments two, three and four, and every other check in this app fires
// before payment one.
// `asker` is the third input and often the decisive one: a transport company that contacted the
// buyer on its own is the scam's second act, whatever the fee is called.
app.MapGet("/api/fee-check", (string? fee, bool? paid, string? asker) =>
    Results.Ok(FeeCheck.Evaluate(fee, paid ?? false, FeeCheck.ParseAsker(asker))))
.WithName("CheckFee");

// Whether this seller is legally required to hold a USDA licence, and what their answer means.
// The only check here that ends in a public database rather than in advice: under the Animal
// Welfare Act a breeder needs one when they keep more than four breeding females AND sell
// sight-unseen, and a puppy shipped to a buyer is not a face-to-face sale.
app.MapGet("/api/seller-check", (string? delivery, string? licence) =>
    Results.Ok(SellerCheck.Evaluate(
        SellerCheck.ParseDelivery(delivery),
        SellerCheck.ParseLicence(licence))))
.WithName("CheckSeller");

// The sources behind a breed's range — what the UI cites instead of asserting
// "verified" on its own.
app.MapGet("/api/price-sources", async (string breed, PriceStore prices, IConfiguration config, CancellationToken ct) =>
{
    var live = await prices.FindAsync(breed, ct);
    var observations = await prices.GetObservationsAsync(breed, ObservationStatus.Accepted, ct);

    // Report the evidence that actually produced the live range, not whatever evidence
    // happens to exist for this breed.
    //
    // Before this, a range derived from 49 live listings was returned with a source list of
    // editorial articles: the count said 49 and the citations said Canine Bible, whose
    // article gives a different band entirely. That is misattribution — a number presented
    // as backed by something that didn't produce it — which is precisely the fault this
    // whole feature exists to correct. Citing the wrong source is not much better than
    // citing none.
    var fromListings = live?.Basis == PriceBasis.Listings;
    var thresholds = PriceThresholds.FromConfiguration(config);
    var sample = fromListings
        ? await prices.GetListingPricesAsync(breed, thresholds.ListingWindowDays, ct)
        : [];

    // Re-derive rather than recount, so the figures shown are the ones the band came from.
    var listingView = fromListings && sample.Count > 0
        ? ListingPriceAggregator.Aggregate(breed, sample, thresholds, live)
        : null;
    var counted = fromListings
        ? sample
            .GroupBy(l => l.ListingRef, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(l => l.RetrievedAt).First().Price)
            .Where(p => p is >= 100 and <= 25_000)
            .ToList()
        : [];

    return Results.Ok(new
    {
        Breed = breed,
        live?.Confidence,
        SourceCount = live?.SourceCount ?? 0,
        live?.SpreadRatio,
        UpdatedAt = live?.UpdatedAt,
        Basis = live?.Basis ?? PriceBasis.Editorial,
        // Live asking prices: there is no quote to show and no publisher to credit, so the
        // provenance is the sample itself — how many, from where, when, and its shape.
        //
        // Counts come from the aggregation, not from the raw rows. Reporting sample.Count here
        // put "the middle half of 69 puppies" directly above "From 80 live listings" on the
        // same card: 69 was the counted sample after de-duplicating re-listings and dropping
        // implausible prices, 80 was everything stored. Two numbers for one sample, and a
        // reader can only conclude one of them is wrong.
        Listings = fromListings && listingView?.Price is not null
            ? new
            {
                Host = sample[0].SourceHost,
                Count = listingView.SampleSize,
                listingView.Median,
                Cheapest = counted.Count > 0 ? counted.Min() : 0,
                Dearest = counted.Count > 0 ? counted.Max() : 0,
                RetrievedAt = sample.Max(l => l.RetrievedAt),
            }
            : null,
        // Published figures still travel with their quote. Shown alongside a listing-derived
        // range as context rather than as its source — and the UI must not conflate them.
        Sources = observations.Select(o => new
        {
            o.Publisher,
            o.PublisherTier,
            o.SourceUrl,
            o.Quote,
            o.Scope,
            o.PriceLow,
            o.PriceHigh,
            o.PublishedAt,
            o.RedFlagQuote,
            o.RetrievedAt,
        }),
    });
})
.WithName("GetPriceSources");

app.MapGet("/api/breeds", async (BreedCatalogService catalog, PriceStore prices, CancellationToken ct) =>
{
    var live = await prices.GetAllAsync(ct);
    return Results.Ok((await catalog.GetBreedsAsync(ct)).Select(b => new
    {
        b.Slug,
        b.DisplayName,
        // Buying is the primary path, so the price range travels with every breed.
        // Zero means "no verified range" (the dog.ceo catalog entries) — null, not 0,
        // so the UI can't render "$0".
        PriceLow = b.PriceHigh > 0 ? b.PriceLow : (int?)null,
        PriceHigh = b.PriceHigh > 0 ? b.PriceHigh : (int?)null,
        TypicalPrice = b.PriceHigh > 0 ? b.TypicalPrice : null,
        // How well the range is backed, so the UI can label it honestly instead of
        // asserting "verified" on its own.
        Confidence = live.GetValueOrDefault(b.Slug)?.Confidence ?? PriceConfidence.Unverified,
        SourceCount = live.GetValueOrDefault(b.Slug)?.SourceCount ?? 0,
        SpreadRatio = live.GetValueOrDefault(b.Slug)?.SpreadRatio,
        PriceUpdatedAt = live.GetValueOrDefault(b.Slug)?.UpdatedAt,
        // Which kind of evidence produced the range: live asking prices or published
        // articles. "49 sources" means something very different in each case, so the UI
        // can't phrase the provenance line without knowing.
        Basis = live.GetValueOrDefault(b.Slug)?.Basis ?? PriceBasis.Editorial,
        Blurb = b.Blurb.Length > 0 ? b.Blurb : null,
        Energy = SiteCatalog.IsCurated(b.Slug) ? b.Energy : (int?)null,
        Grooming = SiteCatalog.IsCurated(b.Slug) ? b.Grooming : (int?)null,
        // Size and traits are only meaningful for curated breeds; external
        // catalog entries default to neutral values without real data, so they're
        // reported as unknown for filtering.
        Size = SiteCatalog.IsCurated(b.Slug) ? b.Size : null,
        KidFriendly = SiteCatalog.IsCurated(b.Slug) ? b.KidFriendly : (int?)null,
        ApartmentFriendly = SiteCatalog.IsCurated(b.Slug) ? b.ApartmentFriendly : (int?)null,
        Shedding = SiteCatalog.IsCurated(b.Slug) ? b.Shedding : (int?)null,
        ImagePath = b.DogCeoPath,
    }));
})
.WithName("GetBreeds");

app.MapPost("/api/quiz", (QuizAnswers answers) =>
    BreedMatcher.Validate(answers) is { } problem
        ? Results.BadRequest(problem)
        : Results.Ok(BreedMatcher.TopMatches(answers)))
.WithName("MatchBreeds");

// Every quiz breed scored — the saved adopter profile uses these to re-rank
// live listings by fit (listing breed text matched against SearchName).
app.MapPost("/api/quiz/scores", (QuizAnswers answers) =>
    BreedMatcher.Validate(answers) is { } problem
        ? Results.BadRequest(problem)
        : Results.Ok(BreedMatcher.TopMatches(answers, count: int.MaxValue)
            .Select(m => new { m.Slug, m.SearchName, m.MatchPercent })))
.WithName("QuizScores");

app.MapGet("/api/sites", async (string? breed, string? state, string? city, BreedCatalogService catalog, CancellationToken ct) =>
{
    Breed? selected = null;
    if (!string.IsNullOrWhiteSpace(breed))
    {
        selected = await catalog.FindAsync(breed, ct);
        if (selected is null)
        {
            return Results.BadRequest($"Unknown breed '{breed}'.");
        }
    }

    var cards = SiteCatalog.Sites.Select(site => new
    {
        site.Id,
        site.Name,
        site.Category,
        site.Description,
        site.Kind,
        site.Vetting,
        site.PriceNote,
        site.Delivery,
        site.BestFor,
        site.Caution,
        LinkUrl = SiteCatalog.BuildLink(site, selected, state, city),
        LinkLabel = SiteCatalog.BuildLinkLabel(site, selected),
        AppliedFilters = SiteCatalog.AppliedFilters(site, selected, state, city),
    });

    return Results.Ok(cards);
})
.WithName("GetSites");

app.MapPost("/api/alerts", async (AlertRequest request, AlertStore store, AlertChecker checker,
    ListingAggregator aggregator, BreedCatalogService catalog, CancellationToken ct) =>
{
    if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
    {
        return Results.BadRequest("A valid email address is required.");
    }

    if (!string.IsNullOrWhiteSpace(request.Breed) && await catalog.FindAsync(request.Breed, ct) is null)
    {
        return Results.BadRequest($"Unknown breed '{request.Breed}'.");
    }

    if (!string.IsNullOrWhiteSpace(request.Age) && !AgeParser.IsGroup(request.Age))
    {
        return Results.BadRequest($"Unknown age group '{request.Age}' — expected one of {string.Join(", ", AgeParser.Groups)}.");
    }

    var subscription = new AlertSubscription(
        Id: Guid.NewGuid().ToString("N")[..12],
        Email: request.Email.Trim(),
        Breed: NullIfBlank(request.Breed),
        State: NullIfBlank(request.State)?.ToUpperInvariant(),
        City: NullIfBlank(request.City),
        Size: NullIfBlank(request.Size),
        CreatedAt: DateTimeOffset.UtcNow,
        Age: NullIfBlank(request.Age));

    // Seed the seen-set with everything currently listed, so signup alerts only
    // about dogs that appear from now on (the UI already shows today's matches).
    try
    {
        var current = await checker.MatchAsync(await aggregator.GetListingsAsync(ct), subscription, ct);
        subscription = subscription with { SeenListingIds = [.. current.Select(l => l.Id)] };
    }
    catch
    {
        // feeds down — an extra "new dogs" email later is acceptable
    }

    var saved = await store.AddAsync(subscription, ct);
    return Results.Ok(new { saved.Id, saved.Email, saved.Breed, saved.State, saved.City, saved.Size, saved.Age });
})
.WithName("CreateAlert");

app.MapGet("/api/alerts", async (string email, AlertStore store, CancellationToken ct) =>
    Results.Ok((await store.GetAllAsync(ct))
        .Where(s => s.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
        .Select(s => new { s.Id, s.Email, s.Breed, s.State, s.City, s.Size, s.Age, s.CreatedAt })))
.WithName("ListAlerts");

app.MapDelete("/api/alerts/{id}", async (string id, string email, AlertStore store, CancellationToken ct) =>
    await store.RemoveAsync(id, email, ct) ? Results.NoContent() : Results.NotFound())
.WithName("DeleteAlert");

// One-click link used inside the alert emails.
app.MapGet("/api/alerts/unsubscribe", async (string id, string email, AlertStore store, CancellationToken ct) =>
    await store.RemoveAsync(id, email, ct)
        ? Results.Text("You're unsubscribed — no more PuppyFinder alerts for this search. 🐾")
        : Results.Text("This alert was already removed."))
.WithName("UnsubscribeAlert");

// ---------------------------------------------------------------- admin
// These mutate the source of truth behind a fraud check, so they sit behind a shared
// secret. Absent config means the whole group is disabled rather than open.
var adminSecret = app.Configuration["Prices:AdminSecret"];

bool Authorised(HttpRequest request) =>
    !string.IsNullOrWhiteSpace(adminSecret)
    && request.Headers.TryGetValue("X-Admin-Secret", out var provided)
    && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
        System.Text.Encoding.UTF8.GetBytes(provided.ToString()),
        System.Text.Encoding.UTF8.GetBytes(adminSecret));

IResult Forbidden() => Results.Json(new
{
    Message = string.IsNullOrWhiteSpace(adminSecret)
        ? "Admin endpoints are disabled — set Prices:AdminSecret to enable them."
        : "A valid X-Admin-Secret header is required.",
}, statusCode: StatusCodes.Status403Forbidden);

// Research one breed on demand. This is the calibration loop: run it, read the
// observations, tune the prompt, repeat — before anything is scheduled.
app.MapPost("/api/admin/price-research", async (HttpRequest request, string? breed,
    PriceRefreshJob job, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();
    var summary = await job.RunAsync(ct, onlyBreedSlug: breed);
    return summary.Errors.Count > 0 && summary.BreedsChecked == 0
        ? Results.BadRequest(summary)
        : Results.Ok(summary);
})
.WithName("ResearchPrices");

// Record observations gathered by hand, for when no API key exists — an org account that
// can't mint one, or a breed the job keeps failing on.
//
// Deliberately not a shortcut around the rules. It takes the *same* payload shape the
// model emits and runs it through the *same* PriceResearchPrompt.Parse and
// PriceObservationValidator.Partition, so a hand-entered row faces an identical
// allowlist, quote-length and scope check, and aggregates identically. Only the
// provenance differs, and that is recorded rather than hidden: these rows carry
// model = "manual" and a manual- run id, so the audit trail always says a human gathered
// them.
//
// Body: [{ "breed": "french-bulldog", "observations": [ ...same fields as the schema... ]}]
app.MapPost("/api/admin/price-observations", async (HttpRequest request,
    PriceStore prices, PriceRefreshJob job, BreedCatalogService catalog,
    IConfiguration config, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();

    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync(ct);
    JsonDocument document;
    try
    {
        document = JsonDocument.Parse(body);
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { Message = $"Body is not valid JSON: {ex.Message}" });
    }

    using (document)
    {
        // Accept one batch or many, so a single breed doesn't need array ceremony.
        var batches = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement];

        var knownSlugs = (await catalog.GetBreedsAsync(ct)).Select(b => b.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runId = $"manual-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        var thresholds = PriceThresholds.FromConfiguration(config);
        var now = DateTimeOffset.UtcNow;
        List<object> outcomes = [];

        foreach (var batch in batches)
        {
            if (batch.ValueKind != JsonValueKind.Object
                || !batch.TryGetProperty("breed", out var slugElement)
                || slugElement.GetString() is not { Length: > 0 } slug)
            {
                return Results.BadRequest(new { Message = "Each batch needs a non-empty \"breed\" slug." });
            }

            // A typo'd slug would create rows no breed can ever read — orphaned data that
            // silently never surfaces is worse than a rejected request.
            if (!knownSlugs.Contains(slug))
            {
                return Results.BadRequest(new { Message = $"Unknown breed slug '{slug}'." });
            }

            var parsed = PriceResearchPrompt.Parse(batch.GetRawText(), slug, runId, model: "manual", now);
            var (kept, refused) = PriceObservationValidator.Partition(parsed);

            var rejectedRows = refused
                .Select(r => r.Observation with { Status = ObservationStatus.Rejected, RejectReason = r.Reason })
                .ToList();
            await prices.AddObservationsAsync([.. kept, .. rejectedRows], ct);

            var aggregation = await job.ReaggregateBreedAsync(slug, thresholds, ct);
            outcomes.Add(new
            {
                Breed = slug,
                Submitted = parsed.Count,
                Accepted = kept.Count,
                Rejected = refused.Select(r => new { r.Observation.Publisher, r.Observation.SourceUrl, r.Reason }),
                Result = aggregation?.Price,
                aggregation?.Rationale,
            });
        }

        return Results.Ok(new { RunId = runId, Thresholds = thresholds, Breeds = outcomes });
    }
})
.WithName("AddPriceObservations");

// Pull real asking prices for a breed (or every curated breed) and derive the range from
// the middle half of them, floor-guarded against the published range.
//
// Off unless Prices:ListingsEnabled — the source's terms restrict automated collection, so
// this must be a deliberate act by the operator, never something that starts on its own.
app.MapPost("/api/admin/listing-prices", async (HttpRequest request, string? breed,
    PriceRefreshJob job, IConfiguration config, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();

    // The loop itself lives on PriceRefreshJob, because the scheduled pass needs it too and
    // two copies of the vendor-dedup, run-recording and precedence rules would drift apart.
    // This endpoint is now the manual trigger for it, nothing more.
    var summary = await job.CollectListingsAsync(ct, onlyBreedSlug: breed);

    return summary.BreedsChecked == 0 && summary.Errors.Count > 0
        ? Results.BadRequest(new { Message = summary.Errors[0] })
        : Results.Ok(new
        {
            summary.RunId,
            Thresholds = PriceThresholds.FromConfiguration(config),
            summary.BreedsChecked,
            summary.Published,
            summary.Refused,
            summary.CrossbreedsDropped,
            summary.Breeds,
        });
})
.WithName("CollectListingPrices");

// Re-derive every breed's confidence from stored observations under the current
// thresholds. Free and idempotent — this is how a threshold change is applied.
app.MapPost("/api/admin/price-reaggregate", async (HttpRequest request,
    PriceRefreshJob job, IConfiguration config, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();
    var thresholds = PriceThresholds.FromConfiguration(config);
    var results = await job.ReaggregateAllAsync(thresholds, ct);
    return Results.Ok(new
    {
        Thresholds = thresholds,
        Breeds = results.Count,
        Distribution = results.Values
            .Where(r => r.Price is not null)
            .GroupBy(r => r.Price!.Confidence)
            .ToDictionary(g => g.Key, g => g.Count()),
    });
})
.WithName("ReaggregatePrices");

// What the verified bar should be, decided from evidence rather than guessed: the
// confidence distribution now, plus how many breeds would qualify under each
// candidate threshold. Read-only — it never writes.
app.MapGet("/api/admin/price-report", async (HttpRequest request,
    PriceStore prices, IConfiguration config, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();

    var live = await prices.GetAllAsync(ct);
    var observationsBySlug = new Dictionary<string, IReadOnlyList<PriceObservation>>();
    foreach (var slug in live.Keys)
    {
        observationsBySlug[slug] = await prices.GetObservationsAsync(slug, status: null, ct);
    }

    // Each candidate is a full re-aggregation over the same stored rows — which is
    // exactly why deferring this decision costs nothing.
    PriceThresholds[] candidates =
    [
        new(MinSources: 2, RequireTierA: false, MaxSpreadRatio: 3.0, MaxVerifiedBandRatio: 8.0),
        new(MinSources: 2, RequireTierA: true, MaxSpreadRatio: 2.0, MaxVerifiedBandRatio: 5.0),
        new(MinSources: 3, RequireTierA: true, MaxSpreadRatio: 3.0, MaxVerifiedBandRatio: 5.0),
        new(MinSources: 3, RequireTierA: true, MaxSpreadRatio: 2.0), // the strict default
        // Band width varied on its own, holding everything else at the default, because
        // it turned out to be the binding constraint in practice rather than source count.
        new(MinSources: 3, RequireTierA: true, MaxSpreadRatio: 2.0, MaxVerifiedBandRatio: 8.0),
        new(MinSources: 3, RequireTierA: true, MaxSpreadRatio: 2.0, MaxVerifiedBandRatio: 3.0),
        new(MinSources: 4, RequireTierA: true, MaxSpreadRatio: 1.5, MaxVerifiedBandRatio: 3.0),
    ];

    object Summarise(PriceThresholds t) => new
    {
        Thresholds = t,
        Distribution = observationsBySlug
            .Select(kv => PriceObservationValidator
                .Aggregate(kv.Key, kv.Value, t, live.GetValueOrDefault(kv.Key)).Price)
            .Where(p => p is not null)
            .GroupBy(p => p!.Confidence)
            .ToDictionary(g => g.Key, g => g.Count()),
    };

    return Results.Ok(new
    {
        Current = PriceThresholds.FromConfiguration(config),
        LiveDistribution = live.Values
            .GroupBy(p => p.Confidence)
            .ToDictionary(g => g.Key, g => g.Count()),
        TotalObservations = observationsBySlug.Values.Sum(o => o.Count),
        BreedsWithAnyObservation = observationsBySlug.Count(kv => kv.Value.Count > 0),
        WhatIf = candidates.Select(Summarise),
    });
})
.WithName("PriceReport");

// Price changes waiting on a person, and the previous range each would replace.
//
// A sharp move away from an already-verified range is not published automatically, because it is
// genuinely ambiguous: either the market moved or our evidence went bad, and those need opposite
// responses. Until it is decided, the old range stays live — so nothing here is urgent in the
// sense of the app being broken, but an unattended hold means that breed's range is frozen.
// The count is logged on every run for that reason.
app.MapGet("/api/admin/price-holds", async (HttpRequest request,
    PriceStore prices, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();

    var holds = await prices.GetOpenHoldsAsync(ct);
    return Results.Ok(new
    {
        Waiting = holds.Count,
        Holds = holds.Select(h => new
        {
            h.BreedSlug,
            Live = new { Low = h.FromLow, High = h.FromHigh, Confidence = h.FromConfidence },
            Proposed = new
            {
                Low = h.ProposedLow,
                High = h.ProposedHigh,
                h.ProposedConfidence,
                h.ProposedBasis,
                h.ProposedSources,
            },
            h.DriftPercent,
            h.Rationale,
            h.RaisedAt,
            Decide = $"POST /api/admin/price-holds/{h.BreedSlug}?decision=approve|dismiss",
        }),
    });
})
.WithName("PriceHolds");

// Approve a held change (publishes it) or dismiss it (keeps what is live).
//
// Dismissing is not "the evidence is wrong" — for that, reject the observation behind it. It
// means "I have seen this and I am keeping the current range", and it stops the same proposal
// being raised every run. A proposal with different numbers is a new question and will be.
//
// Approval publishes the stored proposal verbatim rather than re-deriving it. Re-deriving would
// compare against the still-unchanged live row, find the same sharp move, and re-raise the hold
// that was just approved.
app.MapPost("/api/admin/price-holds/{breed}", async (HttpRequest request, string breed,
    string decision, string? reason, PriceStore prices, BreedCatalogService catalog,
    CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();

    var resolved = decision.ToLowerInvariant() switch
    {
        "approve" => HoldDecision.Approved,
        "dismiss" => HoldDecision.Dismissed,
        _ => null,
    };
    if (resolved is null)
    {
        return Results.BadRequest(new { Message = "decision must be 'approve' or 'dismiss'." });
    }

    if (await prices.DecideHoldAsync(breed, resolved, reason, ct) is not { } decided)
    {
        return Results.NotFound(new { Message = $"No price change is waiting for '{breed}'." });
    }

    // An approval changes what every visitor sees, so the served catalog must not keep the
    // superseded range — the exact combination this project exists to prevent.
    catalog.InvalidatePrices();

    return Results.Ok(new
    {
        decided.BreedSlug,
        Decision = resolved,
        Published = resolved == HoldDecision.Approved
            ? new { Low = decided.ProposedLow, High = decided.ProposedHigh, decided.ProposedConfidence }
            : null,
        Live = resolved == HoldDecision.Approved
            ? null
            : new { Low = decided.FromLow, High = decided.FromHigh },
        decided.DecidedAt,
    });
})
.WithName("PriceHoldDecide");

// Reject an observation, or restore one previously rejected. The row is kept either way — a
// rejection is evidence about a source, not something to erase.
//
// Not the same thing as approving a price change — that is /api/admin/price-holds. This acts on
// one piece of evidence: "this publisher's figure is wrong, stop counting it". Aggregation is a
// pure function of the evidence, so rejecting is how you change what a range is derived *from*.
// Ids come from the price_observation table; nothing in the logs prints them.
app.MapPost("/api/admin/price-observation/{id:long}", async (HttpRequest request, long id,
    string decision, string? reason, PriceStore prices, PriceRefreshJob job,
    IConfiguration config, CancellationToken ct) =>
{
    if (!Authorised(request)) return Forbidden();

    var status = decision.ToLowerInvariant() switch
    {
        "accept" => ObservationStatus.Accepted,
        "reject" => ObservationStatus.Rejected,
        _ => null,
    };
    if (status is null)
    {
        return Results.BadRequest(new { Message = "decision must be 'accept' or 'reject'." });
    }

    if (await prices.SetObservationStatusAsync(id, status, reason, ct) is not { } slug)
    {
        return Results.NotFound(new { Message = $"No observation {id}." });
    }

    // The decision changes what aggregation sees, so re-derive that breed immediately.
    var aggregation = await job.ReaggregateBreedAsync(
        slug, PriceThresholds.FromConfiguration(config), ct);

    return Results.Ok(new { Id = id, Decision = status, Breed = slug, Result = aggregation?.Price, aggregation?.Rationale });
})
.WithName("PriceObservationDecide");

app.Run();

static string? NullIfBlank(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

// The UI sends catalog slugs; shelters store free-text breed names, so match on the
// breed's search name ("labrador-retriever" → "Labrador Retriever"), minus any
// parenthetical qualifier ("Poodle (Standard)" → "Poodle").
static async Task<string?> ResolveBreedTextAsync(string? slug, BreedCatalogService catalog, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(slug))
    {
        return null;
    }

    var searchName = (await catalog.FindAsync(slug, ct))?.SearchName ?? slug;
    return searchName.Split('(')[0].Trim();
}

public record AlertRequest(string Email, string? Breed, string? State, string? City, string? Size, string? Age = null);

// Exposes the implicit Program class to WebApplicationFactory in integration tests.
public partial class Program;
