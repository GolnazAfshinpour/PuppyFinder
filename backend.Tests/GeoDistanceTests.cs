using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// Distance is the filter adopters use most — DESIGN.md cites Adopt-a-Pet's 6.5M searches putting
/// it above breed, and Petfinder opens on a 50-mile radius rather than offering distance as a
/// refinement. It is also the filter whose failure is invisible: a mile count that is quietly 30%
/// wrong still looks like a mile count, so the arithmetic is pinned against known real distances.
/// </summary>
public class GeoDistanceTests
{
    // Real city-centre coordinates, with published great-circle distances to check against.
    private const double DcLat = 38.9072, DcLon = -77.0369;
    private const double BaltimoreLat = 39.2904, BaltimoreLon = -76.6122;
    private const double SeattleLat = 47.6062, SeattleLon = -122.3321;

    [Fact]
    public void MeasuresAShortHopCorrectly()
    {
        // DC to Baltimore is a little under 35 miles as the crow flies.
        var miles = GeoDistance.Miles(DcLat, DcLon, BaltimoreLat, BaltimoreLon);

        Assert.NotNull(miles);
        Assert.InRange(miles!.Value, 33, 37);
    }

    [Fact]
    public void MeasuresACrossCountryDistanceCorrectly()
    {
        // DC to Seattle is roughly 2,300 miles great-circle.
        var miles = GeoDistance.Miles(DcLat, DcLon, SeattleLat, SeattleLon);

        Assert.NotNull(miles);
        Assert.InRange(miles!.Value, 2250, 2350);
    }

    [Fact]
    public void DoesNotUseAFlatEarthApproximation()
    {
        // The reason haversine is here rather than scaled Pythagoras. One degree of longitude at
        // Washington's latitude is ~54 miles, against ~69 for a degree of latitude. Treating them
        // as equal overstates east-west distance by about 28% — enough to push a dog outside a
        // 50-mile radius that is comfortably inside it.
        var eastWest = GeoDistance.Miles(DcLat, DcLon, DcLat, DcLon + 1)!.Value;
        var northSouth = GeoDistance.Miles(DcLat, DcLon, DcLat + 1, DcLon)!.Value;

        Assert.InRange(eastWest, 51, 56);
        Assert.InRange(northSouth, 68, 70);
        Assert.True(eastWest < northSouth * 0.85,
            $"east-west ({eastWest:F1}mi) should be materially shorter than north-south ({northSouth:F1}mi)");
    }

    [Fact]
    public void IsSymmetricAndZeroAtThePoint()
    {
        Assert.Equal(0, GeoDistance.Miles(DcLat, DcLon, DcLat, DcLon)!.Value, precision: 6);
        Assert.Equal(
            GeoDistance.Miles(DcLat, DcLon, SeattleLat, SeattleLon)!.Value,
            GeoDistance.Miles(SeattleLat, SeattleLon, DcLat, DcLon)!.Value,
            precision: 6);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(38.9, null)]
    [InlineData(null, -77.0)]
    public void ReturnsNullRatherThanZeroWhenCoordinatesAreMissing(double? lat, double? lon)
    {
        // Zero would mean "at your doorstep" and would sort first. Null means "we don't know",
        // which the filter and sort both handle explicitly.
        Assert.Null(GeoDistance.Miles(DcLat, DcLon, lat, lon));
    }

    [Fact]
    public void RejectsTheNullIslandThatFeedsSendForMissingData()
    {
        // 0,0 is in the Gulf of Guinea. Feeds use it for "not recorded", and taking it literally
        // would rank those animals as the nearest thing on earth to anyone searching from Africa.
        Assert.Null(GeoDistance.Miles(DcLat, DcLon, 0, 0));
        Assert.False(GeoDistance.IsPlausible(0, 0));
        Assert.True(GeoDistance.IsPlausible(DcLat, DcLon));
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    public void RejectsImpossibleCoordinates(double lat, double lon) =>
        Assert.False(GeoDistance.IsPlausible(lat, lon));

    // ------------------------------------------------------ filter and sort

    private static Listing Dog(string id, double? lat = null, double? lon = null) => new(
        Id: id, Name: id, Breed: "Mixed Breed", Age: "Adult", Sex: "Female", Description: "",
        City: "Somewhere", State: "MD", ImageUrl: null, ListingUrl: "https://example.test",
        Source: "Test", SourceUrl: "https://example.test", Latitude: lat, Longitude: lon);

    [Fact]
    public void ARadiusKeepsWhatIsInsideItAndDropsWhatIsNot()
    {
        Listing[] dogs = [
            Dog("baltimore", BaltimoreLat, BaltimoreLon),
            Dog("seattle", SeattleLat, SeattleLon),
        ];
        var filter = new ListingFilter(
            Latitude: DcLat, Longitude: DcLon, RadiusMiles: 50, IncludeUnlisted: false);

        var kept = ListingQuery.Filter(dogs, filter).ToList();

        Assert.Equal("baltimore", Assert.Single(kept).Id);
    }

    [Fact]
    public void ADogWithNoCoordinatesIsUnknownRatherThanFarAway()
    {
        // The same rule size and age already follow. Rescues leave location off constantly, and
        // dropping those dogs would hide real animals over a blank field.
        Listing[] dogs = [Dog("nowhere"), Dog("seattle", SeattleLat, SeattleLon)];
        var origin = new ListingFilter(Latitude: DcLat, Longitude: DcLon, RadiusMiles: 50);

        var lenient = ListingQuery.Filter(dogs, origin with { IncludeUnlisted = true }).ToList();
        var strict = ListingQuery.Filter(dogs, origin with { IncludeUnlisted = false }).ToList();

        Assert.Equal("nowhere", Assert.Single(lenient).Id);
        Assert.Empty(strict);
    }

    [Fact]
    public void NearestOrdersByDistanceAndPutsUnknownsLast()
    {
        Listing[] dogs = [
            Dog("seattle", SeattleLat, SeattleLon),
            Dog("nowhere"),
            Dog("baltimore", BaltimoreLat, BaltimoreLon),
        ];
        var filter = new ListingFilter(Latitude: DcLat, Longitude: DcLon);

        var sorted = ListingQuery.Sort(dogs, "nearest", filter).Select(l => l.Id).ToList();

        Assert.Equal(["baltimore", "seattle", "nowhere"], sorted);
    }

    [Fact]
    public void NearestDoesNotPretendToSortWithNowhereToMeasureFrom()
    {
        // A distance sort that silently isn't one is worse than not offering it, which is why this
        // sort waited for coordinates in the first place.
        Listing[] dogs = [Dog("a", SeattleLat, SeattleLon), Dog("b", BaltimoreLat, BaltimoreLon)];

        var sorted = ListingQuery.Sort(dogs, "nearest", new ListingFilter()).Select(l => l.Id);

        Assert.Equal(["a", "b"], sorted);  // input order, untouched
    }

    [Fact]
    public void ARadiusWithoutAnOriginDoesNotFilter()
    {
        Listing[] dogs = [Dog("seattle", SeattleLat, SeattleLon)];

        var kept = ListingQuery.Filter(dogs, new ListingFilter(RadiusMiles: 5));

        Assert.Single(kept);
    }
}
