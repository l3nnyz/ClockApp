namespace ClockApp
{
    public sealed record CityInfo(string City, string Country, string TimeZoneId, double Latitude, double Longitude)
    {
        public string Display => $"{City} — {Country}";

        public static readonly IReadOnlyList<CityInfo> Catalog = new List<CityInfo>
        {
            new("Paris", "France", "Europe/Paris", 48.8566, 2.3522),
            new("Londres", "Royaume-Uni", "Europe/London", 51.5074, -0.1278),
            new("Berlin", "Allemagne", "Europe/Berlin", 52.52, 13.405),
            new("Moscou", "Russie", "Europe/Moscow", 55.7558, 37.6173),
            new("New York", "États-Unis", "America/New_York", 40.7128, -74.006),
            new("Los Angeles", "États-Unis", "America/Los_Angeles", 34.0522, -118.2437),
            new("Montréal", "Canada", "America/Toronto", 45.5017, -73.5673),
            new("Mexico", "Mexique", "America/Mexico_City", 19.4326, -99.1332),
            new("São Paulo", "Brésil", "America/Sao_Paulo", -23.5505, -46.6333),
            new("Honolulu", "États-Unis", "Pacific/Honolulu", 21.3069, -157.8583),
            new("Tokyo", "Japon", "Asia/Tokyo", 35.6762, 139.6503),
            new("Séoul", "Corée du Sud", "Asia/Seoul", 37.5665, 126.978),
            new("Pékin", "Chine", "Asia/Shanghai", 39.9042, 116.4074),
            new("Singapour", "Singapour", "Asia/Singapore", 1.3521, 103.8198),
            new("Mumbai", "Inde", "Asia/Kolkata", 19.076, 72.8777),
            new("Dubaï", "Émirats arabes unis", "Asia/Dubai", 25.2048, 55.2708),
            new("Le Caire", "Égypte", "Africa/Cairo", 30.0444, 31.2357),
            new("Casablanca", "Maroc", "Africa/Casablanca", 33.5731, -7.5898),
            new("Alger", "Algérie", "Africa/Algiers", 36.7538, 3.0588),
            new("Dakar", "Sénégal", "Africa/Dakar", 14.7167, -17.4677),
            new("Johannesburg", "Afrique du Sud", "Africa/Johannesburg", -26.2041, 28.0473),
            new("Sydney", "Australie", "Australia/Sydney", -33.8688, 151.2093),
            new("Auckland", "Nouvelle-Zélande", "Pacific/Auckland", -36.8485, 174.7633),
        }.OrderBy(c => c.City).ToList();

        public static readonly string[] Defaults =
            { "Europe/Paris", "Asia/Tokyo", "America/New_York", "Europe/London", "Australia/Sydney", "Asia/Dubai" };
    }
}
