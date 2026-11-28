using System.Globalization;
using ClockApp.Reminders;

namespace ClockApp
{
    public sealed record ClockChange(DateTime Utc, TimeSpan Before, TimeSpan After)
    {
        public bool IsSummerTime => After > Before;
        public string Season
        {
            get
            {
                if (IsSummerTime) return "l’heure d’été";
                return "l’heure d’hiver";
            }
        }
    }

    public static class ClockChanges
    {
        public static readonly TimeSpan CardHorizon = TimeSpan.FromDays(7);
        public static readonly TimeSpan AlertHorizon = TimeSpan.FromDays(2);
        private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        public static ClockChange? Next(TimeZoneInfo zone, DateTime utcNow, TimeSpan horizon)
        {
            var start = new DateTime(utcNow.Ticks - utcNow.Ticks % Step.Ticks, DateTimeKind.Utc);
            var end = start + horizon;
            var before = zone.GetUtcOffset(start);
            for (var t = start + Step; t <= end; t += Step)
            {
                var offset = zone.GetUtcOffset(t);
                if (offset != before) return new ClockChange(t, before, offset);
            }
            return null;
        }

        public static TimeSpan GapBefore(TimeZoneInfo zone, DateTime utc) =>
            zone.GetUtcOffset(utc.AddTicks(-1)) - TimeZoneInfo.Local.GetUtcOffset(utc.AddTicks(-1));

        public static TimeSpan GapAfter(TimeZoneInfo zone, DateTime utc) =>
            zone.GetUtcOffset(utc) - TimeZoneInfo.Local.GetUtcOffset(utc);

        public static string Gap(TimeSpan gap)
        {
            if (gap == TimeSpan.Zero) return "même heure";
            return CityClock.FormatOffset(gap, " h ");
        }

        public static string Day(DateTime utc, DateTime utcNow, TimeZoneInfo zone)
        {
            var date = TimeZoneInfo.ConvertTimeFromUtc(utc, zone).Date;
            var today = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone).Date;
            return (date - today).Days switch
            {
                <= 0 => "aujourd’hui",
                1 => "demain",
                _ => date.ToString("dddd", Fr),
            };
        }

        public static string CardText(TimeZoneInfo zone, DateTime utcNow)
        {
            var city = Next(zone, utcNow, CardHorizon);
            var local = Next(TimeZoneInfo.Local, utcNow, CardHorizon);
            if (city is not null && (local is null || city.Utc <= local.Utc))
            {
                var cityBefore = GapBefore(zone, city.Utc);
                var cityAfter = GapAfter(zone, city.Utc);
                var cityDay = Day(city.Utc, utcNow, zone);
                if (cityBefore == cityAfter) return $"Passe à {city.Season} {cityDay}, comme vous";
                return $"Passe à {city.Season} {cityDay} · écart {Gap(cityBefore)} → {Gap(cityAfter)}";
            }

            if (local is null) return "";

            var before = GapBefore(zone, local.Utc);
            var after = GapAfter(zone, local.Utc);
            if (before == after) return "";

            var day = Day(local.Utc, utcNow, TimeZoneInfo.Local);
            return $"{ReminderMessages.Capitalize(day)}, l’écart passe de {Gap(before)} à {Gap(after)}";
        }

        public static Reminder CityReminder(IReadOnlyList<string> cities, TimeZoneInfo zone, ClockChange change, DateTime utcNow)
        {
            var names = ReminderMessages.JoinCities(cities);
            var verb = "passe";
            if (cities.Count > 1) verb = "passent";

            var before = GapBefore(zone, change.Utc);
            var after = GapAfter(zone, change.Utc);
            var day = ReminderMessages.Capitalize(Day(change.Utc, utcNow, zone));

            var body = $"{day}, {names} {verb} à {change.Season} : l’écart avec vous passe de {Gap(before)} à {Gap(after)}.";
            if (before == after) body = $"{day}, {names} {verb} à {change.Season}, comme vous : l’écart ne change pas.";
            return new Reminder(ReminderKind.ClockChange, $"Changement d’heure : {names}", body);
        }

        public static Reminder LocalReminder(ClockChange change, DateTime utcNow)
        {
            var day = ReminderMessages.Capitalize(Day(change.Utc, utcNow, TimeZoneInfo.Local));
            return new Reminder(ReminderKind.ClockChange, "Changement d’heure chez vous",
                $"{day}, vous passez à {change.Season}. L’écart avec certaines de vos villes va changer.");
        }
    }
}
