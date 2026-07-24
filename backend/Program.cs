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
    client.Timeout = TimeSpan.FromSeconds(15);
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
builder.Services.AddSingleton<BreedCatalogService>();

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
    SizeField: "petsize");

var kingCounty = new SocrataDataset(
    SourceName: "King County Pet Adoption",
    SourceUrl: "https://kingcounty.gov/en/dept/executive-services/animals-pets-pests/regional-animal-services/adopt-a-pet",
    Endpoint: "https://data.kingcounty.gov/resource/yaai-7frk.json?record_type=ADOPTABLE&animal_type=Dog&$limit=100",
    NameField: "animal_name", BreedField: "animal_breed", AgeField: "age", SexField: "animal_gender",
    ImageField: "image", LinkField: "link", CityField: "city",
    DefaultCity: "Kent", State: "WA",
    AnimalTypeField: null,
    FallbackListingUrl: "https://kingcounty.gov/en/dept/executive-services/animals-pets-pests/regional-animal-services/adopt-a-pet",
    MemoField: "memo");

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

app.MapGet("/api/listings", async (string? breed, string? state, string? city, string? size, ListingAggregator aggregator, BreedCatalogService catalog, CancellationToken ct) =>
{
    var listings = (await aggregator.GetListingsAsync(ct)).AsEnumerable();

    string? searchText = null;
    if (!string.IsNullOrWhiteSpace(breed))
    {
        // The UI sends catalog slugs; shelters store free-text breed names, so match
        // on the breed's search name ("labrador-retriever" → "Labrador Retriever"),
        // minus any parenthetical qualifier ("Poodle (Standard)" → "Poodle").
        searchText = (await catalog.FindAsync(breed, ct))?.SearchName ?? breed;
        searchText = searchText.Split('(')[0].Trim();
    }

    return Results.Ok(ListingQuery.Filter(listings, searchText, state, city, size));
})
.WithName("GetListings");

app.MapGet("/api/sources", (ListingAggregator aggregator) =>
    Results.Ok(aggregator.GetSourceStatuses()))
.WithName("GetSources");

// States that currently have at least one live adoptable listing — derived from
// the data itself so it stays correct as feeds are added (or RescueGroups arrives).
app.MapGet("/api/coverage", async (ListingAggregator aggregator, CancellationToken ct) =>
    Results.Ok((await aggregator.GetListingsAsync(ct))
        .Select(l => l.State.ToUpperInvariant())
        .Distinct()
        .Order()
        .ToList()))
.WithName("GetCoverage");

app.MapGet("/api/breeds", async (BreedCatalogService catalog, CancellationToken ct) =>
    Results.Ok((await catalog.GetBreedsAsync(ct)).Select(b => new
    {
        b.Slug,
        b.DisplayName,
        // Size and traits are only meaningful for curated breeds; external
        // catalog entries default to neutral values without real data, so they're
        // reported as unknown for filtering.
        Size = b.PriceHigh > 0 ? b.Size : null,
        KidFriendly = b.PriceHigh > 0 ? b.KidFriendly : (int?)null,
        ApartmentFriendly = b.PriceHigh > 0 ? b.ApartmentFriendly : (int?)null,
        Shedding = b.PriceHigh > 0 ? b.Shedding : (int?)null,
        ImagePath = b.DogCeoPath,
    })))
.WithName("GetBreeds");

app.MapPost("/api/quiz", (QuizAnswers answers) =>
    BreedMatcher.Validate(answers) is { } problem
        ? Results.BadRequest(problem)
        : Results.Ok(BreedMatcher.TopMatches(answers)))
.WithName("MatchBreeds");

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

    var subscription = new AlertSubscription(
        Id: Guid.NewGuid().ToString("N")[..12],
        Email: request.Email.Trim(),
        Breed: NullIfBlank(request.Breed),
        State: NullIfBlank(request.State)?.ToUpperInvariant(),
        City: NullIfBlank(request.City),
        Size: NullIfBlank(request.Size),
        CreatedAt: DateTimeOffset.UtcNow);

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
    return Results.Ok(new { saved.Id, saved.Email, saved.Breed, saved.State, saved.City, saved.Size });
})
.WithName("CreateAlert");

app.MapGet("/api/alerts", async (string email, AlertStore store, CancellationToken ct) =>
    Results.Ok((await store.GetAllAsync(ct))
        .Where(s => s.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
        .Select(s => new { s.Id, s.Email, s.Breed, s.State, s.City, s.Size, s.CreatedAt })))
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

app.Run();

static string? NullIfBlank(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

public record AlertRequest(string Email, string? Breed, string? State, string? City, string? Size);

// Exposes the implicit Program class to WebApplicationFactory in integration tests.
public partial class Program;
