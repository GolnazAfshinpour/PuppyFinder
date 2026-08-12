namespace PuppyFinder.Api.Services;

/// <summary>
/// Great-circle distance in miles. Pure and I/O-free, because "how far away is this dog" is the
/// filter adopters use most and a wrong answer is invisible — a mile-count that is quietly 30%
/// off still looks like a mile-count.
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusMiles = 3958.7613;

    /// <summary>
    /// Miles between two points, or null when either is missing.
    ///
    /// <para>
    /// Haversine rather than a flat-earth approximation: at US latitudes a degree of longitude is
    /// ~53 miles against ~69 for latitude, so treating the two as equal overstates east-west
    /// distance by about 30% — enough to put a dog outside a 50-mile radius that is inside it.
    /// </para>
    /// </summary>
    public static double? Miles(double? fromLat, double? fromLon, double? toLat, double? toLon)
    {
        if (fromLat is not { } lat1 || fromLon is not { } lon1
            || toLat is not { } lat2 || toLon is not { } lon2)
        {
            return null;
        }

        if (!IsPlausible(lat1, lon1) || !IsPlausible(lat2, lon2))
        {
            return null;
        }

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMiles * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Whether a coordinate pair is usable at all. Feeds send 0/0 for "not recorded" often enough
    /// that accepting it would place those animals in the Gulf of Guinea and rank them as the
    /// nearest thing to anyone searching from Africa — and as absurdly distant for everyone else.
    /// </summary>
    public static bool IsPlausible(double lat, double lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180
        && (Math.Abs(lat) > 0.0001 || Math.Abs(lon) > 0.0001);
}
