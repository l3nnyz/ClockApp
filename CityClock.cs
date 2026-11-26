using System.Globalization;

namespace ClockApp
{
    public sealed class CityClock : Notif
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        public CityClock(CityInfo info)
        {
            Info = info;
            ApplyFallbackOffset();
        }

        public CityInfo Info { get; }
        public string City => Info.City;
        public string Country => Info.Country;
        public string TimeZoneId => Info.TimeZoneId;

        public TimeSpan Offset { get; private set; }

        private bool _isSynced;
        public bool IsSynced
        {
            get => _isSynced;
            private set { if (SetField(ref _isSynced, value)) OnPropertyChanged(nameof(SourceText)); }
        }
        public string SourceText => IsSynced ? "Synchronisé via timeapi.io" : "Heure système (hors ligne)";

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        private bool _isDragging;
        public bool IsDragging { get => _isDragging; set => SetField(ref _isDragging, value); }

        public DateTime Now { get; private set; }

        private string _timeText = "";
        public string TimeText { get => _timeText; private set => SetField(ref _timeText, value); }

        private string _secondsText = "";
        public string SecondsText { get => _secondsText; private set => SetField(ref _secondsText, value); }

        private string _dateText = "";
        public string DateText { get => _dateText; private set => SetField(ref _dateText, value); }

        private string _relativeText = "";
        public string RelativeText { get => _relativeText; private set => SetField(ref _relativeText, value); }

        private string _utcText = "";
        public string UtcText { get => _utcText; private set => SetField(ref _utcText, value); }

        private bool _isDay;
        public bool IsDay { get => _isDay; private set => SetField(ref _isDay, value); }

        public void SetApiOffset(TimeSpan offset)
        {
            Offset = offset;
            IsSynced = true;
        }

        public void ApplyFallbackOffset()
        {
            try
            {
                Offset = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId).GetUtcOffset(DateTime.UtcNow);
            }
            catch (TimeZoneNotFoundException)
            {
                Offset = TimeSpan.Zero;
            }
            IsSynced = false;
        }

        public void Update(DateTime utcNow)
        {
            Now = utcNow + Offset;
            TimeText = Now.ToString("HH:mm", Fr);
            SecondsText = Now.ToString("ss", Fr);
            var date = Now.ToString("dddd d MMMM yyyy", Fr);
            DateText = char.ToUpper(date[0], Fr) + date[1..];
            IsDay = Now.Hour is >= 7 and < 19;

            var zoneOffset = TimeSpan.FromMinutes(Math.Round(Offset.TotalMinutes / 15) * 15);
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);
            UtcText = zoneOffset == TimeSpan.Zero ? "UTC" : "UTC" + FormatOffset(zoneOffset, ":");

            var localDate = (utcNow + localOffset).Date;
            var day = (Now.Date - localDate).Days switch
            {
                > 0 => "Demain",
                < 0 => "Hier",
                _ => "Aujourd’hui",
            };
            var diff = zoneOffset - localOffset;
            RelativeText = diff == TimeSpan.Zero ? $"{day}, même heure" : $"{day}, {FormatOffset(diff, " h ")}";
        }

        private static string FormatOffset(TimeSpan t, string sep)
        {
            var sign = t < TimeSpan.Zero ? "−" : "+";
            var a = t.Duration();
            var hours = (int)a.TotalHours;
            if (sep == ":")
                return a.Minutes == 0 ? $"{sign}{hours}" : $"{sign}{hours}:{a.Minutes:00}";
            return a.Minutes == 0 ? $"{sign}{hours} h" : $"{sign}{hours} h {a.Minutes:00}";
        }
    }
}
