using PuppyFinder.Api.Data;

namespace PuppyFinder.Api.Services;

public record QuizAnswers(
    string Home,      // apartment | house
    string Activity,  // low | medium | high
    string Kids,      // yes | no
    string Grooming,  // low | high  (tolerance for grooming effort)
    string Size,      // small | medium | large | any
    string Budget);   // any | under1500 | over1500

public record BreedMatch(
    string Slug,
    string DisplayName,
    string TypicalPrice,
    string Blurb,
    int MatchPercent,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Scores quiz answers against breed traits. Each of the six dimensions
/// contributes 0–10 points; the total maps to a match percentage.
/// </summary>
public static class BreedMatcher
{
    private static readonly string[] HomeValues = ["apartment", "house"];
    private static readonly string[] ActivityValues = ["low", "medium", "high"];
    private static readonly string[] YesNo = ["yes", "no"];
    private static readonly string[] GroomingValues = ["low", "high"];
    private static readonly string[] SizeValues = ["small", "medium", "large", "any"];
    private static readonly string[] BudgetValues = ["any", "under1500", "over1500"];

    public static string? Validate(QuizAnswers answers)
    {
        if (!HomeValues.Contains(answers.Home)) return $"home must be one of: {string.Join(", ", HomeValues)}";
        if (!ActivityValues.Contains(answers.Activity)) return $"activity must be one of: {string.Join(", ", ActivityValues)}";
        if (!YesNo.Contains(answers.Kids)) return $"kids must be one of: {string.Join(", ", YesNo)}";
        if (!GroomingValues.Contains(answers.Grooming)) return $"grooming must be one of: {string.Join(", ", GroomingValues)}";
        if (!SizeValues.Contains(answers.Size)) return $"size must be one of: {string.Join(", ", SizeValues)}";
        if (!BudgetValues.Contains(answers.Budget)) return $"budget must be one of: {string.Join(", ", BudgetValues)}";
        return null;
    }

    public static IReadOnlyList<BreedMatch> TopMatches(QuizAnswers answers, int count = 3) =>
        SiteCatalog.Breeds
            .Where(b => b.IncludeInQuiz)
            .Select(breed => Score(breed, answers))
            .OrderByDescending(m => m.MatchPercent)
            .ThenBy(m => m.DisplayName)
            .Take(count)
            .ToList();

    private static BreedMatch Score(Breed breed, QuizAnswers answers)
    {
        var reasons = new List<(double Points, string Text)>();
        double total = 0;

        // Home: apartments reward apartment-friendly breeds; houses suit everyone.
        double home = answers.Home == "apartment" ? breed.ApartmentFriendly * 2 : 10;
        total += home;
        if (answers.Home == "apartment" && breed.ApartmentFriendly >= 4)
            reasons.Add((home, "Fits apartment life"));

        // Activity: match owner's activity level to breed energy.
        double target = answers.Activity switch { "low" => 1.5, "medium" => 3.5, _ => 5.0 };
        double activity = Math.Clamp(10 - Math.Abs(breed.Energy - target) * 2.5, 0, 10);
        total += activity;
        if (activity >= 8)
            reasons.Add((activity, breed.Energy >= 4 ? "Matches your active lifestyle" : "Relaxed exercise needs"));

        // Kids
        double kids = answers.Kids == "yes" ? breed.KidFriendly * 2 : 10;
        total += kids;
        if (answers.Kids == "yes" && breed.KidFriendly == 5)
            reasons.Add((kids, "Great with kids"));

        // Grooming tolerance: low tolerance rewards easy coats.
        double grooming = answers.Grooming == "low" ? (6 - breed.Grooming) * 2 : 10;
        total += grooming;
        if (answers.Grooming == "low" && breed.Grooming <= 2)
            reasons.Add((grooming, "Easy-care coat"));
        if (answers.Grooming == "low" && breed.Shedding <= 2)
            reasons.Add((8, "Low shedding"));

        // Size preference
        double size = answers.Size == "any" ? 10
            : breed.Size.Equals(answers.Size, StringComparison.OrdinalIgnoreCase) ? 10
            : IsAdjacentSize(breed.Size, answers.Size) ? 5
            : 0;
        total += size;
        if (size == 10 && answers.Size != "any")
            reasons.Add((size, $"{breed.Size} — the size you wanted"));

        // Budget
        double budget = answers.Budget switch
        {
            "under1500" when breed.PriceLow <= 1500 => 10,
            "under1500" when breed.PriceLow <= 2000 => 5,
            "under1500" => 0,
            _ => 10,
        };
        total += budget;
        if (answers.Budget == "under1500" && budget == 10)
            reasons.Add((budget, $"Typically from ${breed.PriceLow:n0}"));

        return new BreedMatch(
            breed.Slug,
            breed.DisplayName,
            breed.TypicalPrice,
            breed.Blurb,
            MatchPercent: (int)Math.Round(total / 60 * 100),
            Reasons: reasons
                .OrderByDescending(r => r.Points)
                .Select(r => r.Text)
                .Distinct()
                .Take(3)
                .ToList());
    }

    private static bool IsAdjacentSize(string breedSize, string wanted) =>
        (breedSize, wanted) is ("Medium", "small") or ("Medium", "large")
            or ("Small", "medium") or ("Large", "medium");
}
