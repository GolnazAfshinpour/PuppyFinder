using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class AgeParserTests
{
    [Theory]
    // The formats the live Socrata feeds actually publish (verified July 2026).
    [InlineData("8 Months", 8)]
    [InlineData("1 Year", 12)]
    [InlineData("1 Year 6 Months", 18)]
    [InlineData("11 Years 6 Months", 138)]
    [InlineData("14 Years", 168)]
    // Shapes other feeds use.
    [InlineData("10 months old", 10)]
    [InlineData("2 yrs 3 mo", 27)]
    [InlineData("8 weeks", 2)]
    public void ParsesTheAgeFormatsFeedsActuallySend(string age, int expected) =>
        Assert.Equal(expected, AgeParser.ToMonths(age));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("Adult")] // a word-only age has no month count...
    public void ReturnsNullWhenThereIsNoNumericAge(string? age) =>
        Assert.Null(AgeParser.ToMonths(age));

    [Fact]
    public void RoundsSubMonthAgesUpToOne() =>
        // ...because a 0 would be indistinguishable from missing data downstream.
        Assert.Equal(1, AgeParser.ToMonths("3 days"));

    [Theory]
    [InlineData("4 Months", AgeParser.Puppy)]
    [InlineData("11 Months", AgeParser.Puppy)]
    [InlineData("1 Year", AgeParser.Young)]
    [InlineData("2 Years 11 Months", AgeParser.Young)]
    [InlineData("3 Years", AgeParser.Adult)]
    [InlineData("7 Years 11 Months", AgeParser.Adult)]
    [InlineData("8 Years", AgeParser.Senior)]
    [InlineData("14 Years", AgeParser.Senior)]
    public void GroupsOnPetfinderAdoptAPetBoundaries(string age, string expected) =>
        Assert.Equal(expected, AgeParser.ToGroup(age));

    [Theory]
    // Rescue feeds often send the word instead of a number.
    [InlineData("Baby", AgeParser.Puppy)]
    [InlineData("Senior", AgeParser.Senior)]
    [InlineData("Adult", AgeParser.Adult)]
    [InlineData("young", AgeParser.Young)]
    [InlineData("Young Puppy", AgeParser.Puppy)] // "puppy" wins over "young"
    public void FallsBackToWordAges(string age, string expected) =>
        Assert.Equal(expected, AgeParser.ToGroup(age));

    [Theory]
    [InlineData(null)]
    [InlineData("Unknown")]
    [InlineData("ask the shelter")]
    public void ReportsUnknownRatherThanGuessingAGroup(string? age) =>
        Assert.Null(AgeParser.ToGroup(age));

    [Theory]
    [InlineData("Puppy", true)]
    [InlineData("puppy", true)]
    [InlineData("SENIOR", true)]
    [InlineData("Teenager", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidatesGroupNamesCaseInsensitively(string? group, bool expected) =>
        Assert.Equal(expected, AgeParser.IsGroup(group));

    [Fact]
    public void ListingExposesTheDerivedAgeToTheApi()
    {
        var listing = Sample("Rex", age: "1 Year 6 Months");

        Assert.Equal(18, listing.AgeMonths);
        Assert.Equal(AgeParser.Young, listing.AgeGroup);
    }

    internal static Listing Sample(string name, string? age = null, string? size = null, string breed = "Mixed Breed") =>
        new(
            Id: $"test-{name}",
            Name: name,
            Breed: breed,
            Age: age,
            Sex: "Male",
            Description: "",
            City: "Derwood",
            State: "MD",
            ImageUrl: null,
            ListingUrl: "https://example.test/pet",
            Source: "Test Shelter",
            SourceUrl: "https://example.test",
            Size: size);
}
