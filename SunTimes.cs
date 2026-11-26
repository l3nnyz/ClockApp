namespace ClockApp
{
    public static class SunTimes
    {
        private const double Rad = Math.PI / 180;

        public static (DateTime Rise, DateTime Set)? For(DateOnly date, double latitude, double longitude)
        {
            var n = date.ToDateTime(new TimeOnly(12, 0)).ToOADate() + 2415018.5 - 2451545.0;
            var meanNoon = n - longitude / 360;
            var m = (357.5291 + 0.98560028 * meanNoon) % 360;
            var center = 1.9148 * Math.Sin(m * Rad) + 0.02 * Math.Sin(2 * m * Rad) + 0.0003 * Math.Sin(3 * m * Rad);
            var lambda = (m + center + 180 + 102.9372) % 360;
            var transit = 2451545.0 + meanNoon + 0.0053 * Math.Sin(m * Rad) - 0.0069 * Math.Sin(2 * lambda * Rad);
            var declination = Math.Asin(Math.Sin(lambda * Rad) * Math.Sin(23.4397 * Rad));

            var cosOmega = (Math.Sin(-0.833 * Rad) - Math.Sin(latitude * Rad) * Math.Sin(declination))
                / (Math.Cos(latitude * Rad) * Math.Cos(declination));
            if (cosOmega is < -1 or > 1) return null;

            var omega = Math.Acos(cosOmega) / Rad;
            return (ToUtc(transit - omega / 360), ToUtc(transit + omega / 360));
        }

        private static DateTime ToUtc(double julian) =>
            DateTime.SpecifyKind(DateTime.FromOADate(julian - 2415018.5), DateTimeKind.Utc);
    }
}
