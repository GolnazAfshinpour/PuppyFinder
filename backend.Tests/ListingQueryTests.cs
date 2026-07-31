using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class ListingQueryTests
{
    private static Listing Dog(string name, string? age = null, string? size = null, string breed = "Mixed Breed") =>
        AgeParserTests.Sample(name, age, size, breed);

    private static readonly Listing[] Shelter =
    [
        Dog("Thunder", age: "4 Months", size: "Medium"),
        Dog("Puparotti", age: "9 Months", size: "Small"),
        Dog("Bruno", age: "5 Years", size: "Large"),
        Dog("Nana", age: "10 Years", size: "Large"),
        Dog("Mystery", age: null, size: null), // shelter filled in nothing
    ];

    [Fact]
    public void FiltersByAgeGroup()
    {
        var puppies = ListingQuery.Filter(Shelter, new ListingFilter(AgeGroup: AgeParser.Puppy)).ToList();

        Assert.Equal(["Thunder", "Puparotti", "Mystery"], puppies.Select(l => l.Name));
    }

    [Fact]
    public void KeepsListingsWithNoAgeSoASparseFeedDoesNotLookEmpty()
    {
        var seniors = ListingQuery.Filter(Shelter, new ListingFilter(AgeGroup: AgeParser.Senior)).ToList();

        Assert.Contains("Nana", seniors.Select(l => l.Name));
        Assert.Contains("Mystery", seniors.Select(l => l.Name));
        // ...but never a dog whose age contradicts the filter.
        Assert.DoesNotContain("Thunder", seniors.Select(l => l.Name));
    }

    [Fact]
    public void IncludeUnlistedFalseDemandsAHardMatch()
    {
        var strict = ListingQuery
            .Filter(Shelter, new ListingFilter(AgeGroup: AgeParser.Senior, IncludeUnlisted: false))
            .ToList();

        Assert.Equal(["Nana"], strict.Select(l => l.Name));
    }

    [Fact]
    public void SizeTreatsMissingDataAsUnknownToo()
    {
        var small = ListingQuery.Filter(Shelter, new ListingFilter(Size: "Small")).ToList();

        Assert.Equal(["Puparotti", "Mystery"], small.Select(l => l.Name));
        Assert.Single(ListingQuery.Filter(Shelter, new ListingFilter(Size: "Small", IncludeUnlisted: false)));
    }

    [Fact]
    public void FlagsTheListingsThatOnlySurvivedOnMissingData()
    {
        var filter = new ListingFilter(Size: "Small");

        Assert.True(ListingQuery.Unconfirmed(Dog("Mystery"), filter));
        Assert.False(ListingQuery.Unconfirmed(Dog("Puparotti", size: "Small"), filter));
        // No size filter set → nothing is unconfirmed on size grounds.
        Assert.False(ListingQuery.Unconfirmed(Dog("Mystery"), new ListingFilter()));
    }

    [Fact]
    public void SortRanksConfirmedMatchesAboveUnknowns()
    {
        var filter = new ListingFilter(AgeGroup: AgeParser.Puppy);
        var matches = ListingQuery.Filter(Shelter, filter);

        var sorted = ListingQuery.Sort(matches, sort: null, filter).Select(l => l.Name).ToList();

        Assert.Equal("Mystery", sorted[^1]);
    }

    [Fact]
    public void SortsByAgeInBothDirections()
    {
        var youngest = ListingQuery.Sort(Shelter, "youngest", new ListingFilter()).Select(l => l.Name);
        Assert.Equal(["Thunder", "Puparotti", "Bruno", "Nana", "Mystery"], youngest);

        var oldest = ListingQuery.Sort(Shelter, "oldest", new ListingFilter()).Select(l => l.Name);
        Assert.Equal(["Nana", "Bruno", "Puparotti", "Thunder", "Mystery"], oldest);
    }

    [Fact]
    public void UnknownSortFallsBackToNameRatherThanThrowing()
    {
        var sorted = ListingQuery.Sort(Shelter, "nearest", new ListingFilter()).Select(l => l.Name);

        Assert.Equal(["Bruno", "Mystery", "Nana", "Puparotti", "Thunder"], sorted);
    }

    [Fact]
    public void BreedMatchIsSubstringAndCaseInsensitive()
    {
        Listing[] listings = [Dog("Ace", breed: "Labrador Retriever / Mix"), Dog("Bo", breed: "Beagle")];

        var labs = ListingQuery.Filter(listings, new ListingFilter(BreedSearchText: "labrador retriever"));

        Assert.Equal(["Ace"], labs.Select(l => l.Name));
    }
}
