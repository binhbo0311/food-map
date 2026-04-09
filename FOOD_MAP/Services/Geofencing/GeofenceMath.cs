namespace FOOD_MAP.Services.Geofencing;

public static class GeofenceMath
{
    private const double EarthRadiusMeters = 6371000d;

    public static double CalculateDistanceMeters(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude)
    {
        var deltaLatitude = ToRadians(toLatitude - fromLatitude);
        var deltaLongitude = ToRadians(toLongitude - fromLongitude);

        var fromLatitudeRadians = ToRadians(fromLatitude);
        var toLatitudeRadians = ToRadians(toLatitude);

        var haversine = Math.Pow(Math.Sin(deltaLatitude / 2d), 2d) +
                        Math.Cos(fromLatitudeRadians) *
                        Math.Cos(toLatitudeRadians) *
                        Math.Pow(Math.Sin(deltaLongitude / 2d), 2d);

        var centralAngle = 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine));
        return EarthRadiusMeters * centralAngle;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * (Math.PI / 180d);
    }
}
