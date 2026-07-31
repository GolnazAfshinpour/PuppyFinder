using System.Globalization;
using System.Text.RegularExpressions;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Turns the free-text age every feed publishes ("8 Months", "1 Year 6 Months",
/// "Baby") into months + one of four age groups, so "show me puppies" is a real
/// filter instead of something the user has to eyeball card by card.
///
/// Group boundaries follow the Petfinder / Adopt-a-Pet convention that adopters
/// already know: Puppy &lt; 1 yr, Young 1–2, Adult 3–7, Senior 8+.
/// </summary>
public static partial class AgeParser
{
    public const string Puppy = "Puppy";
    public const string Young = "Young";
    public const string Adult = "Adult";
    public const string Senior = "Senior";

    public static readonly string[] Groups = [Puppy, Young, Adult, Senior];

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(year|yr|month|mo|week|wk|day)", RegexOptions.IgnoreCase)]
    private static partial Regex UnitPattern();

    /// <summary>Age in months, or null when the feed gave us nothing numeric to work with.</summary>
    public static int? ToMonths(string? age)
    {
        if (string.IsNullOrWhiteSpace(age))
        {
            return null;
        }

        double months = 0;
        var matched = false;
        foreach (var match in UnitPattern().Matches(age).Cast<Match>())
        {
            if (!double.TryParse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            months += match.Groups[2].Value.ToLowerInvariant() switch
            {
                "year" or "yr" => value * 12,
                "month" or "mo" => value,
                "week" or "wk" => value / 4.345,
                _ => value / 30.44, // day
            };
            matched = true;
        }

        // "6 weeks" rounds to 1 month, not 0 — a 0 would read as missing data.
        return matched ? Math.Max(1, (int)Math.Round(months)) : null;
    }

    /// <summary>
    /// The age group for a listing. Prefers a numeric age; falls back to the
    /// word-based ages ("Baby", "Senior") that rescue feeds often send instead.
    /// Null means unknown — callers must decide whether to keep or drop those.
    /// </summary>
    public static string? ToGroup(string? age)
    {
        if (ToMonths(age) is { } months)
        {
            return FromMonths(months);
        }

        if (string.IsNullOrWhiteSpace(age))
        {
            return null;
        }

        // Word-only ages. "Puppy"/"Baby" first: "young puppy" is a puppy, not Young.
        var text = age.ToLowerInvariant();
        if (text.Contains("baby") || text.Contains("puppy") || text.Contains("pup")) return Puppy;
        if (text.Contains("senior") || text.Contains("geriatric")) return Senior;
        if (text.Contains("young") || text.Contains("juvenile")) return Young;
        if (text.Contains("adult")) return Adult;
        return null;
    }

    public static string FromMonths(int months) => months switch
    {
        < 12 => Puppy,
        < 36 => Young,
        < 96 => Adult,
        _ => Senior,
    };

    /// <summary>True when <paramref name="group"/> is one of the four known groups.</summary>
    public static bool IsGroup(string? group) =>
        group is not null && Groups.Contains(group, StringComparer.OrdinalIgnoreCase);
}
