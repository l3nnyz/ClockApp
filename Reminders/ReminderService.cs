namespace ClockApp.Reminders
{
    /// <summary>
    /// Surveille l'heure des villes suivies et déclenche un rappel aux moments clés
    /// (9 h, 12 h, 18 h, 22 h heure locale de la ville). Les villes qui partagent la même
    /// heure sont regroupées dans un seul rappel.
    /// </summary>
    public sealed class ReminderService(AppSettings settings)
    {
        private readonly ReminderMessages _messages = new();
        private readonly HashSet<string> _fired = new();

        public event Action<Reminder>? Raised;

        public void Check(IEnumerable<CityClock> cities, DateTime utcNow)
        {
            if (!settings.RemindersEnabled) return;

            var localOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);
            var localNow = utcNow + localOffset;
            if (settings.QuietHours && IsQuietHour(localNow)) return;

            var due = new List<(ReminderKind Kind, DateTime CityTime, string City)>();
            foreach (var city in cities)
            {
                // Une ville à la même heure que le PC n'apporte rien : on l'ignore.
                if (Math.Abs((city.Offset - localOffset).TotalMinutes) < 1) continue;

                // On recalcule l'heure ici plutôt que d'utiliser city.Now, qui n'est plus
                // mis à jour quand la fenêtre est réduite.
                var cityTime = utcNow + city.Offset;
                foreach (var kind in Enum.GetValues<ReminderKind>())
                {
                    if (!settings.IsEnabled(kind)) continue;
                    var at = ReminderMessages.TimeOf(kind);
                    if (cityTime.Hour != at.Hour || cityTime.Minute != at.Minute) continue;

                    if (_fired.Add($"{city.TimeZoneId}|{kind}|{cityTime:yyyy-MM-dd}"))
                        due.Add((kind, cityTime, city.City));
                }
            }

            if (_fired.Count > 500) _fired.Clear();

            foreach (var group in due.GroupBy(d => (d.Kind, Minute: d.CityTime.ToString("yyyyMMddHHmm"))))
            {
                var first = group.First();
                var names = group.Select(g => g.City).ToList();
                if (_messages.Compose(first.Kind, names, first.CityTime, localNow) is { } reminder)
                    Raised?.Invoke(reminder);
            }
        }

        /// <summary>Affiche un exemple de rappel pour la ville donnée, comme s'il était l'heure.</summary>
        public void Test(CityClock city)
        {
            var utcNow = DateTime.UtcNow;
            var cityNow = utcNow + city.Offset;
            var kinds = Enum.GetValues<ReminderKind>().Where(k => _messages.HasTemplate(k, cityNow.DayOfWeek)).ToArray();
            var kind = kinds[Random.Shared.Next(kinds.Length)];
            var cityTime = cityNow.Date + ReminderMessages.TimeOf(kind).ToTimeSpan();
            // Heure qu'il serait chez l'utilisateur à ce moment-là, pour que l'aperçu reste cohérent.
            var localTime = cityTime - city.Offset + TimeZoneInfo.Local.GetUtcOffset(utcNow);

            if (_messages.Compose(kind, [city.City], cityTime, localTime) is { } reminder)
                Raised?.Invoke(reminder);
        }

        private static bool IsQuietHour(DateTime local) => local.Hour is >= 23 or < 7;
    }
}
