using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>Outcome of researching one breed.</summary>
public record PriceResearchResult(
    string BreedSlug,
    IReadOnlyList<PriceObservation> Accepted,
    IReadOnlyList<RejectedObservation> Rejected,
    bool Unverifiable,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Asks Claude to find published price figures for a breed, with web search restricted
/// to a reviewed source list.
///
/// Deliberately thin: it gathers observations and never decides confidence. Aggregation
/// is a separate pure function (<see cref="PriceObservationValidator.Aggregate"/>) over
/// the stored rows, which is what makes re-tuning the trust thresholds free — no
/// re-research, no API spend.
/// </summary>
public sealed class PriceResearchService(IConfiguration configuration, ILogger<PriceResearchService> logger)
{
    /// <summary>Opus 5: thinking is on by default; sampling parameters are rejected.</summary>
    private const string Model = "claude-opus-5";

    private const long MaxTokens = 8_000;

    private readonly string? _apiKey = configuration["Anthropic:ApiKey"]
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    /// <summary>
    /// False when no key is configured. Callers check this rather than catching — the
    /// refresh job skips itself entirely and the admin endpoint returns a clear message,
    /// mirroring how <see cref="RescueGroupsProvider"/> gates on its own key.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<PriceResearchResult> ResearchAsync(Breed breed, string runId, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new PriceResearchResult(breed.Slug, [], [], false,
                "No Anthropic API key configured (set Anthropic:ApiKey or ANTHROPIC_API_KEY).");
        }

        try
        {
            var client = new AnthropicClient { ApiKey = _apiKey };
            var response = await client.Messages.Create(BuildRequest(breed), ct);

            // Opus 5's classifiers can decline; Content is empty or partial when they do.
            // Checking first, because indexing Content[0] would throw here.
            if (response.StopReason == "refusal")
            {
                logger.LogWarning("Price research for {Breed} was refused by safety classifiers", breed.Slug);
                return new PriceResearchResult(breed.Slug, [], [], false, "Request was refused.");
            }

            var json = FirstText(response);
            if (json is null)
            {
                return new PriceResearchResult(breed.Slug, [], [], false, "Model returned no text content.");
            }

            var parsed = PriceResearchPrompt.Parse(json, breed.Slug, runId, Model, DateTimeOffset.UtcNow);
            var (kept, rejected) = PriceObservationValidator.Partition(parsed);

            logger.LogInformation(
                "Price research {Breed}: {Kept} accepted, {Rejected} rejected, unverifiable={Unverifiable}",
                breed.Slug, kept.Count, rejected.Count, PriceResearchPrompt.IsUnverifiable(json));

            return new PriceResearchResult(
                breed.Slug, kept, rejected, PriceResearchPrompt.IsUnverifiable(json));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One breed failing must not abort a 179-breed run.
            logger.LogWarning("Price research for {Breed} failed: {Message}", breed.Slug, ex.Message);
            return new PriceResearchResult(breed.Slug, [], [], false, ex.Message);
        }
    }

    private static MessageCreateParams BuildRequest(Breed breed) => new()
    {
        Model = Model,
        MaxTokens = MaxTokens,

        // The rules block is byte-identical across all 179 breed calls, so caching it
        // turns 178 of them into cache reads.
        System = new List<TextBlockParam>
        {
            new()
            {
                Text = PriceResearchPrompt.SystemRules,
                CacheControl = new CacheControlEphemeral(),
            },
        },

        // Search is confined to the reviewed list. Blocked domains are enforced again in
        // PriceObservationValidator.Reject, because an allowlist on the tool is a
        // request-shaping hint and the validator is the actual guarantee.
        Tools =
        [
            new ToolUnion(new WebSearchTool20260209
            {
                MaxUses = 8,
                AllowedDomains = [.. PriceSources.AllowedDomains],
            }),
        ],

        // Provenance comes from required schema fields; the document `citations` feature
        // is incompatible with output_config.format and returns 400.
        OutputConfig = new OutputConfig
        {
            Format = new JsonOutputFormat { Schema = PriceResearchPrompt.ResponseSchema() },
        },

        Messages = [new() { Role = Role.User, Content = PriceResearchPrompt.UserPrompt(breed) }],
    };

    private static string? FirstText(Message response)
    {
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out TextBlock? text) && !string.IsNullOrWhiteSpace(text.Text))
            {
                return text.Text;
            }
        }

        return null;
    }
}
