using System.Text.Json.Serialization;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpClient("petfinder", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PuppyFinder/1.0");
});
builder.Services.AddHttpClient("rescuegroups", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PuppyFinder/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.api+json");
});

builder.Services.AddSingleton<IListingProvider, PetfinderProvider>();
builder.Services.AddSingleton<IListingProvider, RescueGroupsProvider>();
builder.Services.AddSingleton<ListingAggregator>();

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

app.MapGet("/api/listings", async (string? breed, string? state, ListingAggregator aggregator, CancellationToken ct) =>
{
    var listings = (await aggregator.GetListingsAsync(ct)).AsEnumerable();

    if (!string.IsNullOrWhiteSpace(breed))
    {
        listings = listings.Where(l => l.Breed.Contains(breed, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(state))
    {
        listings = listings.Where(l => l.State.Equals(state, StringComparison.OrdinalIgnoreCase));
    }

    return Results.Ok(listings);
})
.WithName("GetListings");

app.MapGet("/api/sources", (ListingAggregator aggregator) =>
    Results.Ok(aggregator.GetSourceStatuses()))
.WithName("GetSources");

app.MapGet("/api/breeds", () =>
    Results.Ok(SiteCatalog.Breeds.Select(b => new { b.Slug, b.DisplayName })))
.WithName("GetBreeds");

app.MapGet("/api/sites", (string? breed, string? state) =>
{
    Breed? selected = null;
    if (!string.IsNullOrWhiteSpace(breed))
    {
        selected = SiteCatalog.Breeds.FirstOrDefault(b =>
            b.Slug.Equals(breed, StringComparison.OrdinalIgnoreCase));
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
        LinkUrl = SiteCatalog.BuildLink(site, selected, state),
        LinkLabel = SiteCatalog.BuildLinkLabel(site, selected),
    });

    return Results.Ok(cards);
})
.WithName("GetSites");

app.Run();
