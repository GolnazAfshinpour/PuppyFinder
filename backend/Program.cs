using System.Text.Json.Serialization;
using PuppyFinder.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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
