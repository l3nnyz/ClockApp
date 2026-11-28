namespace ClockApp
{
    public static class WeatherCodes
    {
        private const string Sun = "☀";
        private const string Moon = "\U0001F319";
        private const string PartlyCloudy = "⛅";
        private const string Cloud = "☁";
        private const string Showers = "\U0001F326";
        private const string Rain = "\U0001F327";
        private const string Snow = "❄";
        private const string Storm = "⛈";

        public static string Label(int code) => code switch
        {
            0 => "Ciel dégagé",
            1 => "Plutôt dégagé",
            2 => "Partiellement nuageux",
            3 => "Couvert",
            45 or 48 => "Brouillard",
            51 or 53 or 55 => "Bruine",
            56 or 57 => "Bruine verglaçante",
            61 or 63 => "Pluie",
            65 => "Forte pluie",
            66 or 67 => "Pluie verglaçante",
            71 or 73 => "Neige",
            75 => "Forte neige",
            77 => "Grésil",
            80 or 81 or 82 => "Averses",
            85 or 86 => "Averses de neige",
            95 => "Orage",
            96 or 99 => "Orage avec grêle",
            _ => "Météo inconnue",
        };

        public static string Glyph(int code, bool isDay)
        {
            if (code is 0 or 1)
            {
                if (isDay) return Sun;
                return Moon;
            }
            if (code == 2)
            {
                if (isDay) return PartlyCloudy;
                return Cloud;
            }

            return code switch
            {
                >= 51 and <= 67 => Rain,
                >= 71 and <= 77 => Snow,
                80 or 81 or 82 => Showers,
                85 or 86 => Snow,
                >= 95 => Storm,
                _ => Cloud,
            };
        }
    }
}
