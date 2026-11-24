namespace ClockApp
{
    public sealed record CityInfo(string City, string Country, string TimeZoneId)
    {
        public string Display => $"{City} — {Country}";

        public static readonly IReadOnlyList<CityInfo> Catalog = new List<CityInfo>
        {
            new("Paris", "France", "Europe/Paris"),
            new("Londres", "Royaume-Uni", "Europe/London"),
            new("Berlin", "Allemagne", "Europe/Berlin"),
            new("Moscou", "Russie", "Europe/Moscow"),
            new("New York", "États-Unis", "America/New_York"),
            new("Los Angeles", "États-Unis", "America/Los_Angeles"),
            new("Montréal", "Canada", "America/Toronto"),
            new("Mexico", "Mexique", "America/Mexico_City"),
            new("São Paulo", "Brésil", "America/Sao_Paulo"),
            new("Honolulu", "États-Unis", "Pacific/Honolulu"),
            new("Tokyo", "Japon", "Asia/Tokyo"),
            new("Séoul", "Corée du Sud", "Asia/Seoul"),
            new("Pékin", "Chine", "Asia/Shanghai"),
            new("Singapour", "Singapour", "Asia/Singapore"),
            new("Mumbai", "Inde", "Asia/Kolkata"),
            new("Dubaï", "Émirats arabes unis", "Asia/Dubai"),
            new("Le Caire", "Égypte", "Africa/Cairo"),
            new("Casablanca", "Maroc", "Africa/Casablanca"),
            new("Alger", "Algérie", "Africa/Algiers"),
            new("Dakar", "Sénégal", "Africa/Dakar"),
            new("Johannesburg", "Afrique du Sud", "Africa/Johannesburg"),
            new("Sydney", "Australie", "Australia/Sydney"),
            new("Auckland", "Nouvelle-Zélande", "Pacific/Auckland"),
        }.OrderBy(c => c.City).ToList();

        public static readonly string[] Defaults =
            { "Europe/Paris", "Asia/Tokyo", "America/New_York", "Europe/London", "Australia/Sydney", "Asia/Dubai" };
    }
}
