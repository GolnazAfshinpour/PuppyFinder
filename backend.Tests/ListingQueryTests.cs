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

    // ---- good with kids / dogs / cats ----
    //
    // Only 21-41% of listings record these, so the unknown-is-not-no rule matters more here than
    // anywhere else — and the explicit "no" matters more than anywhere else too.

    private static Listing GoodWith(string name, bool? kids = null, bool? dogs = null, bool? cats = null) =>
        Dog(name) with { GoodWithKids = kids, GoodWithDogs = dogs, GoodWithCats = cats };

    [Fact]
    public void GoodWithKeepsConfirmedMatchesAndUnrecordedDogs()
    {
        var listings = new[]
        {
            GoodWith("Yes", kids: true),
            GoodWith("Unknown"),
            GoodWith("No", kids: false),
        };

        var matches = ListingQuery.Filter(listings, new ListingFilter(GoodWithKids: true)).ToList();

        Assert.Equal(["Yes", "Unknown"], matches.Select(l => l.Name));
    }

    [Fact]
    public void AnExplicitNoIsNeverShownEvenWhenUnlistedDogsAreIncluded()
    {
        // The one asymmetry in the whole filter. Someone asking for "good with kids" has a child
        // in the house; a rescue that wrote down "no" is the single fact here that a convenience
        // toggle must not be able to override.
        var listings = new[] { GoodWith("No", kids: false) };

        Assert.Empty(ListingQuery.Filter(listings, new ListingFilter(GoodWithKids: true, IncludeUnlisted: true)));
        Assert.Empty(ListingQuery.Filter(listings, new ListingFilter(GoodWithKids: true, IncludeUnlisted: false)));
    }

    [Fact]
    public void StrictMatchDropsTheUnrecordedOnesOnly()
    {
        var listings = new[] { GoodWith("Yes", cats: true), GoodWith("Unknown") };

        var strict = ListingQuery.Filter(
            listings, new ListingFilter(GoodWithCats: true, IncludeUnlisted: false)).ToList();

        Assert.Equal(["Yes"], strict.Select(l => l.Name));
    }

    [Fact]
    public void EachGoodWithFieldIsIndependent()
    {
        // A dog good with dogs but not cats must survive a dogs search and fail a cats one.
        var dog = GoodWith("Rex", dogs: true, cats: false);

        Assert.Single(ListingQuery.Filter([dog], new ListingFilter(GoodWithDogs: true)));
        Assert.Empty(ListingQuery.Filter([dog], new ListingFilter(GoodWithCats: true)));
    }

    [Fact]
    public void AskingForSeveralRequiresAllOfThem()
    {
        var listings = new[]
        {
            GoodWith("Both", kids: true, dogs: true),
            GoodWith("OnlyKids", kids: true, dogs: false),
        };

        var matches = ListingQuery.Filter(
            listings, new ListingFilter(GoodWithKids: true, GoodWithDogs: true)).ToList();

        Assert.Equal(["Both"], matches.Select(l => l.Name));
    }

    [Fact]
    public void NotAskingAboutGoodWithChangesNothing()
    {
        var listings = new[] { GoodWith("No", kids: false, dogs: false, cats: false) };

        Assert.Single(ListingQuery.Filter(listings, new ListingFilter()));
    }

    [Fact]
    public void ADogKeptOnlyByABlankFieldIsMarkedUnconfirmed()
    {
        var filter = new ListingFilter(GoodWithKids: true);

        Assert.True(ListingQuery.Unconfirmed(GoodWith("Unknown"), filter));
        Assert.False(ListingQuery.Unconfirmed(GoodWith("Yes", kids: true), filter));
        // Not asked about, so a blank is not a caveat.
        Assert.False(ListingQuery.Unconfirmed(GoodWith("Unknown"), new ListingFilter()));
    }

    // ---- sex ----

    [Fact]
    public void SexMatchesOnThePrefixSoAlteredDogsAreNotHidden()
    {
        // The county feeds publish "Male (neutered)" / "Female (spayed)"; an equality test
        // would hide every altered dog from the filter most adopters combine with it.
        Listing[] dogs =
        [
            Dog("Rex") with { Sex = "Male" },
            Dog("Buddy") with { Sex = "Male (neutered)" },
            Dog("Daisy") with { Sex = "Female (spayed)" },
        ];

        var males = ListingQuery.Filter(dogs, new ListingFilter(Sex: "Male")).Select(l => l.Name);
        Assert.Equal(["Rex", "Buddy"], males);

        var females = ListingQuery.Filter(dogs, new ListingFilter(Sex: "Female")).Select(l => l.Name);
        Assert.Equal(["Daisy"], females);
    }

    [Fact]
    public void SexTreatsMissingDataAsUnknownNotNo()
    {
        Listing[] dogs =
        [
            Dog("Daisy") with { Sex = "Female (spayed)" },
            Dog("Mystery") with { Sex = null },
        ];
        var filter = new ListingFilter(Sex: "Female");

        // Kept and labelled, the same rule size and age follow...
        Assert.Equal(["Daisy", "Mystery"],
            ListingQuery.Filter(dogs, filter).Select(l => l.Name));
        Assert.True(ListingQuery.Unconfirmed(Dog("Mystery") with { Sex = null }, filter));
        Assert.False(ListingQuery.Unconfirmed(dogs[0], filter));

        // ...and strict mode is the opt-out.
        Assert.Equal(["Daisy"],
            ListingQuery.Filter(dogs, filter with { IncludeUnlisted = false }).Select(l => l.Name));
    }

    [Fact]
    public void ConfirmedGoodWithMatchesSortAboveUnrecordedOnes()
    {
        var listings = new[] { GoodWith("Aaa"), GoodWith("Zzz", kids: true) };
        var filter = new ListingFilter(GoodWithKids: true);

        var sorted = ListingQuery.Sort(
            ListingQuery.Filter(listings, filter)
                .Select(l => l with { Unconfirmed = ListingQuery.Unconfirmed(l, filter) }),
            null,
            filter).ToList();

        // Alphabetically "Aaa" wins; the confirmed match still comes first.
        Assert.Equal(["Zzz", "Aaa"], sorted.Select(l => l.Name));
    }
}
