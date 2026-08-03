using System.Text.Json;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// The instructions and output contract given to the model, and the parsing of what
/// comes back.
///
/// Separated from <see cref="PriceResearchService"/> on purpose: the prompt is the least
/// testable part of an LLM pipeline, so everything about it that *can* be tested offline
/// — the schema shape, the rules text, and the mapping of a response into observations —
/// lives here with no network dependency. The service is then a thin wrapper around one
/// HTTP call.
/// </summary>
public static class PriceResearchPrompt
{
    /// <summary>
    /// Rules the model works under. Kept in one constant so it can be sent as a cached
    /// prefix — identical across all 179 breed calls, and Opus 5's 512-token cache
    /// minimum means it pays for itself immediately.
    /// </summary>
    public const string SystemRules = """
        You research published price ranges for dog breeds. You are an extractor, not an
        estimator.

        ## The one rule that matters
        Report only figures a source actually publishes, with the exact words that support
        them. Never estimate, never average sources together, never fill a gap from your
        own knowledge of dog prices. If you cannot find a published figure with a quote,
        return `unverifiable: true` and an empty list. **An empty result is a correct,
        useful answer** — a breed with no published pricing must stay unpriced rather than
        receive a plausible-looking guess.

        ## One observation per source
        Each entry is one figure from one page. If a page publishes several figures for
        different things (pet-quality vs rare colour vs adoption), emit one entry per
        figure with the right `scope`. Do not merge them.

        ## scope — the most important field
        Most apparent disagreement between price sources is conflated scope rather than
        real disagreement: a $5,000 merle French Bulldog and a $2,000 pet-quality one are
        answers to different questions. Tag what the figure actually covers:

        - `pet_standard` — pet-quality, standard/recognised colour, reputable breeder,
          national. This is the only scope used to publish a range.
        - `show_or_pedigree` — show prospect, champion lines, breeding rights.
        - `rare_colour` — merle, lilac, blue, "fluffy", or any colour the source frames as
          rare or premium.
        - `regional` — the source explicitly scopes the figure to a region or metro
          ("in the Northeast, expect $1,200–2,500").
        - `rescue` — adoption or rehoming fee, not a purchase from a breeder.
        - `unscoped` — the source gives a number without saying which of the above it
          covers, or blends several together.

        If a source does not say, use `unscoped`. Do **not** guess `pet_standard` because
        it seems likely — guessing here silently corrupts the published range, which is the
        specific failure this whole pipeline exists to prevent.

        ## figureKind
        - `range` — the source gives a low and a high. Set both.
        - `average` — the source gives a single number ("about $5,000", "average $3,000").
          Set `priceLow` and `priceHigh` to that same value.

        ## quote
        Copy the sentence containing the figure verbatim from the page. Do not paraphrase,
        summarise, or reconstruct it. If you cannot quote it, do not report it.

        ## redFlagQuote
        If the source states a price below which a quote is likely a scam ("anything under
        $1,500 is a red flag", "$400–800 is a common bait tactic"), copy that sentence into
        `redFlagQuote` on the same entry. This is the most consistent claim across sources
        and is worth capturing wherever it appears.

        ## Sources
        Only use the domains you are allowed to search. Do not report a figure from a
        breeder, kennel, broker, or puppy classifieds site even if you encounter one:
        sellers price their own stock, and classifieds listings are the very thing this
        data is used to screen against.

        Prefer sources that state a publication or update date, and prefer recent ones.
        """;

    /// <summary>The per-breed instruction appended after the cached rules.</summary>
    public static string UserPrompt(Breed breed) => $"""
        Find published US price figures for the **{breed.DisplayName}** breed.

        Search the allowed sources, then report every distinct published figure you can
        quote, each with its scope. Aim for at least three independent `pet_standard`
        figures if they exist, but report only what you can actually cite — returning two
        well-sourced figures is better than three with one invented.
        """;

    /// <summary>
    /// JSON Schema for the structured response.
    ///
    /// Note: provenance rides these required fields rather than the API's document
    /// `citations` feature, which is incompatible with `output_config.format` (400).
    /// Numerical bounds are absent because structured outputs don't support them —
    /// <see cref="PriceObservationValidator.Reject"/> enforces plausibility instead.
    /// </summary>
    public static Dictionary<string, JsonElement> ResponseSchema() =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "type": "object",
          "properties": {
            "unverifiable": {
              "type": "boolean",
              "description": "True when no citable published figure could be found."
            },
            "observations": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "publisher": { "type": "string", "description": "Publication name, e.g. MetLife Pet Insurance." },
                  "sourceUrl": { "type": "string", "description": "Absolute https URL of the page carrying the figure." },
                  "quote": { "type": "string", "description": "Verbatim sentence containing the figure." },
                  "scope": {
                    "type": "string",
                    "enum": ["{{PriceScope.PetStandard}}", "{{PriceScope.ShowOrPedigree}}", "{{PriceScope.RareColour}}", "{{PriceScope.Regional}}", "{{PriceScope.Rescue}}", "{{PriceScope.Unscoped}}"]
                  },
                  "figureKind": { "type": "string", "enum": ["{{FigureKind.Range}}", "{{FigureKind.Average}}"] },
                  "priceLow": { "type": "integer", "description": "USD. Same as priceHigh for an average." },
                  "priceHigh": { "type": "integer", "description": "USD. Same as priceLow for an average." },
                  "publishedAt": { "type": "string", "description": "ISO date the source states, if any." },
                  "redFlagQuote": { "type": "string", "description": "Verbatim scam-price warning, if the source gives one." }
                },
                "required": ["publisher", "sourceUrl", "quote", "scope", "figureKind", "priceLow", "priceHigh"],
                "additionalProperties": false
              }
            }
          },
          "required": ["unverifiable", "observations"],
          "additionalProperties": false
        }
        """)!;

    /// <summary>
    /// Maps a model response into observations. Tolerant by design: a malformed entry is
    /// skipped rather than failing the breed, because one bad row shouldn't cost the
    /// other four. Everything kept still has to survive
    /// <see cref="PriceObservationValidator"/>.
    /// </summary>
    public static List<PriceObservation> Parse(
        string json, string breedSlug, string runId, string model, DateTimeOffset now)
    {
        List<PriceObservation> results = [];
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("observations", out var observations)
            || observations.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var element in observations.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var url = Text(element, "sourceUrl");
            var quote = Text(element, "quote");
            var scope = Text(element, "scope");
            var kind = Text(element, "figureKind") ?? FigureKind.Range;
            if (url is null || quote is null || scope is null
                || !Int(element, "priceLow", out var low) || !Int(element, "priceHigh", out var high))
            {
                continue;
            }

            results.Add(new PriceObservation(
                BreedSlug: breedSlug,
                PriceLow: low,
                PriceHigh: high,
                Scope: scope,
                Kind: kind,
                SourceUrl: url,
                Publisher: Text(element, "publisher") ?? PriceSources.HostOf(url) ?? "unknown",
                // Provisional — Partition re-derives it from the reviewed source list.
                PublisherTier: PriceSources.TierFor(url) ?? PublisherTier.B,
                Quote: quote,
                RetrievedAt: now,
                RunId: runId,
                Model: model,
                Status: ObservationStatus.Accepted,
                PublishedAt: Date(element, "publishedAt"),
                RedFlagQuote: Text(element, "redFlagQuote")));
        }

        return results;
    }

    /// <summary>True when the model reported it could find nothing citable.</summary>
    public static bool IsUnverifiable(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("unverifiable", out var flag)
            && flag.ValueKind == JsonValueKind.True;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    private static bool Int(JsonElement element, string name, out int result)
    {
        result = 0;
        if (!element.TryGetProperty(name, out var value))
        {
            return false;
        }

        // Models occasionally emit "2500" or 2500.0 where the schema said integer.
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out result)
                || (value.TryGetDouble(out var d) && SetRounded(d, out result)),
            JsonValueKind.String => int.TryParse(value.GetString(), out result),
            _ => false,
        };

        static bool SetRounded(double value, out int result)
        {
            result = (int)Math.Round(value);
            return true;
        }
    }

    /// <summary>
    /// Parses a source's publication date as UTC when it carries no offset. Sources
    /// publish date-only strings ("2026-02-11"), and a plain TryParse would read those in
    /// the server's local zone — so the same page would be recorded with a different
    /// timestamp depending on which machine ran the job.
    /// </summary>
    private static DateTimeOffset? Date(JsonElement element, string name) =>
        Text(element, name) is { } text
        && DateTimeOffset.TryParse(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal
                | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
