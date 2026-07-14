using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class BreedMatcherTests
{
    private static QuizAnswers Valid(
        string home = "house", string activity = "medium", string kids = "no",
        string grooming = "high", string size = "any", string budget = "any") =>
        new(home, activity, kids, grooming, size, budget);

    [Fact]
    public void Validate_AcceptsValidAnswers() =>
        Assert.Null(BreedMatcher.Validate(Valid()));

    [Theory]
    [InlineData("home", "condo")]
    [InlineData("activity", "extreme")]
    [InlineData("kids", "maybe")]
    [InlineData("grooming", "medium")]
    [InlineData("size", "giant")]
    [InlineData("budget", "under9000")]
    public void Validate_RejectsUnknownValues(string field, string bad)
    {
        var answers = field switch
        {
            "home" => Valid(home: bad),
            "activity" => Valid(activity: bad),
            "kids" => Valid(kids: bad),
            "grooming" => Valid(grooming: bad),
            "size" => Valid(size: bad),
            _ => Valid(budget: bad),
        };
        var problem = BreedMatcher.Validate(answers);
        Assert.NotNull(problem);
        Assert.StartsWith(field, problem);
    }

    [Fact]
    public void TopMatches_ReturnsThree_SortedByScore()
    {
        var matches = BreedMatcher.TopMatches(Valid());
        Assert.Equal(3, matches.Count);
        Assert.True(matches[0].MatchPercent >= matches[1].MatchPercent);
        Assert.True(matches[1].MatchPercent >= matches[2].MatchPercent);
    }

    [Fact]
    public void TopMatches_NeverIncludesTeacupAliases()
    {
        // Teacup entries mirror their parent breeds and are flagged IncludeInQuiz=false.
        var matches = BreedMatcher.TopMatches(Valid(), count: 100);
        Assert.DoesNotContain(matches, m => m.Slug.StartsWith("teacup-"));
    }

    [Fact]
    public void ApartmentLowActivity_PrefersApartmentFriendlyCalmSmallBreeds()
    {
        // Cavalier, Chihuahua, French Bulldog, and Shih Tzu tie for this profile
        // (small, apartment 5, low energy); alphabetical tiebreak puts Cavalier first.
        var matches = BreedMatcher.TopMatches(Valid(home: "apartment", activity: "low", size: "small"));
        Assert.Equal("cavalier-king-charles-spaniel", matches[0].Slug);
        Assert.All(matches, m => Assert.Equal(98, m.MatchPercent));
    }

    [Fact]
    public void ActiveHouseWithKids_PrefersHighEnergyKidFriendlyBreeds()
    {
        var top = BreedMatcher.TopMatches(Valid(activity: "high", kids: "yes", size: "large"));
        // The classic active-family dogs should dominate this profile.
        Assert.Contains(top, m => m.Slug is "labrador-retriever" or "golden-retriever" or "german-shepherd");
    }

    [Fact]
    public void MatchPercent_AlwaysWithinBounds()
    {
        string[][] combos =
        [
            ["apartment", "low", "yes", "low", "small", "under1500"],
            ["house", "high", "no", "high", "large", "over1500"],
            ["apartment", "medium", "no", "low", "any", "any"],
        ];
        foreach (var c in combos)
            foreach (var m in BreedMatcher.TopMatches(new QuizAnswers(c[0], c[1], c[2], c[3], c[4], c[5]), count: 100))
                Assert.InRange(m.MatchPercent, 0, 100);
    }

    [Fact]
    public void Reasons_AreCappedAtThree()
    {
        foreach (var m in BreedMatcher.TopMatches(Valid(home: "apartment", kids: "yes", grooming: "low", budget: "under1500"), count: 100))
            Assert.InRange(m.Reasons.Count, 0, 3);
    }
}
