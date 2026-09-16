namespace CfiApp.Domain.Attendance;

/// <summary>
/// Great-circle distance between two points, in metres. This is what decides whether a
/// phone is actually on site before the server accepts an automatic clock event - a
/// wrong answer here is either a false clock-in from off site or a real worker turned
/// away, so it is a plain, independently testable function rather than buried in a
/// controller.
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusMetres = 6_371_000;

    public static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMetres * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
