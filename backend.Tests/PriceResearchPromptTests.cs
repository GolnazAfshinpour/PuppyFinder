using System.Text.Json;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// The prompt and its output contract are the least testable part of an LLM pipeline, so
/// everything about them that can be checked offline is checked here: the schema shape,
/// the rules that must be stated, and the parsing of a response into observations. No
/// network, no API key.
/// </summary>
public class PriceResearchPromptTests
{
    private static readonly Breed Frenchie = new(
        "french-bulldog", "French Bulldog", "french-bulldog",
        Size: "Small", Energy: 3, Grooming: 2, Shedding: 3, KidFriendly: 4, ApartmentFriendly: 5,
        PriceLow: 0, PriceHigh: 0, Blurb: "");

    private static List<PriceObservation> Parse(string json) =>
        PriceResearchPrompt.Parse(json, "french-bulldog", "run-1", "claude-opus-5", DateTimeOffset.UtcNow);

    // ---------------------------------------------------------------- the contract

    [Fact]
    public void SchemaRequiresProvenanceOnEveryObservation()
    {
        var schema = PriceResearchPrompt.ResponseSchema();
        var required = schema["properties"]
            .GetProperty("observations").GetProperty("items").GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()).ToList();

        // No citation, no write — enforced by the schema itself, not just the validator.
        Assert.Contains("sourceUrl", required);
        Assert.Contains("quote", required);
        Assert.Contains("scope", required);
        Assert.Contains("figureKind", required);
    }

    [Fact]
    public void SchemaClosesObjectsAsStructuredOutputsDemands()
    {
        var schema = PriceResearchPrompt.ResponseSchema();

        Assert.False(schema["additionalProperties"].GetBoolean());
        Assert.False(schema["properties"].GetProperty("observations")
            .GetProperty("items").GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void SchemaOffersExactlyTheKnownScopesAndKinds()
    {
        var items = PriceResearchPrompt.ResponseSchema()["properties"]
            .GetProperty("observations").GetProperty("items").GetProperty("properties");

        var scopes = items.GetProperty("scope").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToList();
        var kinds = items.GetProperty("figureKind").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        // A scope the validator doesn't know would be rejected on arrival, so the schema
        // and the enum must not drift apart.
        Assert.Equal(PriceScope.All.OrderBy(s => s), scopes.OrderBy(s => s));
        Assert.Equal(FigureKind.All.OrderBy(k => k), kinds.OrderBy(k => k));
    }

    [Fact]
    public void SchemaOmitsNumericBoundsBecauseStructuredOutputsRejectThem()
    {
        var priceLow = PriceResearchPrompt.ResponseSchema()["properties"]
            .GetProperty("observations").GetProperty("items")
            .GetProperty("properties").GetProperty("priceLow");

        // minimum/maximum are unsupported; plausibility is the validator's job.
        Assert.False(priceLow.TryGetProperty("minimum", out _));
        Assert.False(priceLow.TryGetProperty("maximum", out _));
    }

    [Fact]
    public void SchemaIsValidJsonSerializable() =>
        // Round-trips, so the SDK can hand it to the API without surprises.
        Assert.False(string.IsNullOrWhiteSpace(
            JsonSerializer.Serialize(PriceResearchPrompt.ResponseSchema())));

    // ---------------------------------------------------------------- the rules text

    [Theory]
    [InlineData("extractor, not an")]        // the framing that prevents estimation
    [InlineData("Never estimate")]
    [InlineData("empty result is a correct")] // finding nothing must be a valid answer
    [InlineData("verbatim")]                  // quotes may not be paraphrased
    [InlineData("Do **not** guess `pet_standard`")]
    [InlineData("sellers price their own stock")]
    public void RulesStateTheThingsThatKeepTheDataHonest(string expected) =>
        Assert.Contains(expected, PriceResearchPrompt.SystemRules);

    [Fact]
    public void RulesExplainEveryScopeTheSchemaAccepts()
    {
        // A scope offered without an explanation is a scope the model will misapply.
        Assert.All(PriceScope.All, scope =>
            Assert.Contains($"`{scope}`", PriceResearchPrompt.SystemRules));
    }

    [Fact]
    public void RulesAreLongEnoughToCacheOnOpus5() =>
        // The 512-token cache minimum is roughly 2,000 characters; below it the shared
        // prefix silently won't cache and all 179 calls pay full price.
        Assert.True(PriceResearchPrompt.SystemRules.Length > 2_000,
            $"rules are {PriceResearchPrompt.SystemRules.Length} chars, too short to cache");

    [Fact]
    public void UserPromptNamesTheBreedAndAsksForCitableFiguresOnly()
    {
        var prompt = PriceResearchPrompt.UserPrompt(Frenchie);

        Assert.Contains("French Bulldog", prompt);
        Assert.Contains("only what you can actually cite", prompt);
    }

    // ---------------------------------------------------------------- parsing

    [Fact]
    public void ParsesAWellFormedResponse()
    {
        var observations = Parse("""
            {
              "unverifiable": false,
              "observations": [
                {
                  "publisher": "MetLife Pet Insurance",
                  "sourceUrl": "https://www.metlifepetinsurance.com/blog/breed-spotlights/french-bulldog/",
                  "quote": "A standard French Bulldog puppy typically costs between $2,500 and $4,000.",
                  "scope": "pet_standard",
                  "figureKind": "range",
                  "priceLow": 2500,
                  "priceHigh": 4000,
                  "publishedAt": "2026-02-11",
                  "redFlagQuote": "Pricing a French Bulldog at $400-$800 is a common bait tactic."
                }
              ]
            }
            """);

        var o = Assert.Single(observations);
        Assert.Equal(2500, o.PriceLow);
        Assert.Equal(PriceScope.PetStandard, o.Scope);
        Assert.Equal(FigureKind.Range, o.Kind);
        Assert.Equal(PublisherTier.A, o.PublisherTier);
        Assert.Equal(new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero), o.PublishedAt);
        Assert.Contains("bait tactic", o.RedFlagQuote);
    }

    [Fact]
    public void OneMalformedEntryDoesNotCostTheOthers()
    {
        // A 179-breed run shouldn't lose a breed's good rows to one bad one.
        var observations = Parse("""
            {
              "unverifiable": false,
              "observations": [
                { "publisher": "Broken", "scope": "pet_standard", "figureKind": "range" },
                {
                  "publisher": "Insurify",
                  "sourceUrl": "https://insurify.com/pet-insurance/knowledge/how-much-is-a-french-bulldog/",
                  "quote": "You might pay around $5,000 on average for a Frenchie from a reputable breeder.",
                  "scope": "pet_standard",
                  "figureKind": "average",
                  "priceLow": 5000,
                  "priceHigh": 5000
                }
              ]
            }
            """);

        var kept = Assert.Single(observations);
        Assert.Equal("Insurify", kept.Publisher);
        Assert.Equal(FigureKind.Average, kept.Kind);
    }

    [Theory]
    // Models sometimes ignore "integer" in a schema; the figure is still recoverable.
    [InlineData("\"2500\"", "\"4000\"", 2500, 4000)]
    [InlineData("2500.0", "4000.4", 2500, 4000)]
    public void ToleratesNumbersThatArriveAsStringsOrDecimals(
        string low, string high, int expectedLow, int expectedHigh)
    {
        var observations = Parse($$"""
            {
              "unverifiable": false,
              "observations": [{
                "publisher": "MetLife", "sourceUrl": "https://www.metlifepetinsurance.com/x",
                "quote": "A standard French Bulldog puppy typically costs in this band.",
                "scope": "pet_standard", "figureKind": "range",
                "priceLow": {{low}}, "priceHigh": {{high}}
              }]
            }
            """);

        var o = Assert.Single(observations);
        Assert.Equal(expectedLow, o.PriceLow);
        Assert.Equal(expectedHigh, o.PriceHigh);
    }

    [Fact]
    public void RecognisesTheUnverifiableAnswerAsValid()
    {
        const string json = """{ "unverifiable": true, "observations": [] }""";

        Assert.True(PriceResearchPrompt.IsUnverifiable(json));
        Assert.Empty(Parse(json));
    }

    [Fact]
    public void AnEmptyOrShapelessResponseYieldsNothingRatherThanThrowing()
    {
        Assert.Empty(Parse("""{ "unverifiable": false }"""));
        Assert.Empty(Parse("""{ "observations": "not an array" }"""));
        Assert.Empty(Parse("""{ "observations": [1, 2, 3] }"""));
    }

    [Fact]
    public void ParsedRowsStillHaveToSurviveTheValidator()
    {
        // The parser is tolerant; the validator is not. A blocked domain slipping past
        // the tool's allowlist is caught here.
        var observations = Parse("""
            {
              "unverifiable": false,
              "observations": [{
                "publisher": "Lancaster Puppies",
                "sourceUrl": "https://www.lancasterpuppies.com/breeds/french-bulldog/puppy",
                "quote": "French Bulldog puppies are available starting at $1,200 today.",
                "scope": "pet_standard", "figureKind": "range",
                "priceLow": 1200, "priceHigh": 2400
              }]
            }
            """);

        var (kept, rejected) = PriceObservationValidator.Partition(observations);

        Assert.Empty(kept);
        Assert.Contains("excluded as a price authority", Assert.Single(rejected).Reason);
    }

    [Fact]
    public void AllowedDomainsAndBlockedDomainsDoNotOverlap()
    {
        // A domain on both lists would make behaviour depend on evaluation order.
        var overlap = PriceSources.AllowedDomains.Intersect(PriceSources.Blocked).ToList();

        Assert.Empty(overlap);
    }
}
