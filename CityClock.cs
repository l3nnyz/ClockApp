using System.Globalization;
using ClockApp.ServiceAPI;

namespace ClockApp
{
    public sealed class CityClock : Notif
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        private DateTime _clockChangeCheckedAt = DateTime.MinValue;

        public CityClock(CityInfo info)
        {
            Info = info;
            try { Zone = TimeZoneInfo.FindSystemTimeZoneById(info.TimeZoneId); }
            catch (TimeZoneNotFoundException) { }
            ApplyFallbackOffset();
        }

        public TimeZoneInfo? Zone { get; }

        public ClockChange? NextClockChange { get; private set; }

        private string _clockChangeText = "";
        public string ClockChangeText { get => _clockChangeText; private set => SetField(ref _clockChangeText, value); }

        public void RefreshClockChange(DateTime utcNow)
        {
            if (Zone is null) return;
            if (utcNow >= _clockChangeCheckedAt && utcNow - _clockChangeCheckedAt < TimeSpan.FromMinutes(10)) return;
            _clockChangeCheckedAt = utcNow;
            NextClockChange = ClockChanges.Next(Zone, utcNow, ClockChanges.CardHorizon);
            ClockChangeText = ClockChanges.CardText(Zone, utcNow);
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
        public string SourceText
        {
            get
            {
                if (IsSynced) return "Synchronisé via timeapi.io";
                return "Heure système (hors ligne)";
            }
        }

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

        private DateOnly _sunDate;
        private TimeSpan? _sunOffset;
        private (DateTime Rise, DateTime Set)? _sun;
        private DateOnly _sunApiDate;
        private DateTime _sunApiRetryAt = DateTime.MinValue;
        private bool _sunApiBusy;

        private string _sunText = "";
        public string SunText { get => _sunText; private set => SetField(ref _sunText, value); }

        private string _dayLengthText = "";
        public string DayLengthText { get => _dayLengthText; private set => SetField(ref _dayLengthText, value); }

        public void SetApiOffset(TimeSpan offset)
        {
            Offset = offset;
            IsSynced = true;
        }

        public void ApplyFallbackOffset()
        {
            Offset = Zone?.GetUtcOffset(DateTime.UtcNow) ?? TimeSpan.Zero;
            IsSynced = false;
        }

        public void Update(DateTime utcNow)
        {
            Now = utcNow + Offset;
            RefreshClockChange(utcNow);
            TimeText = Now.ToString("HH:mm", Fr);
            SecondsText = Now.ToString("ss", Fr);
            var date = Now.ToString("dddd d MMMM yyyy", Fr);
            DateText = char.ToUpper(date[0], Fr) + date[1..];
            UpdateSun(utcNow);

            var zoneOffset = TimeSpan.FromMinutes(Math.Round(Offset.TotalMinutes / 15) * 15);
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);
            UtcText = "UTC";
            if (zoneOffset != TimeSpan.Zero) UtcText += FormatOffset(zoneOffset, ":");

            var localDate = (utcNow + localOffset).Date;
            var day = (Now.Date - localDate).Days switch
            {
                > 0 => "Demain",
                < 0 => "Hier",
                _ => "Aujourd’hui",
            };
            var diff = zoneOffset - localOffset;
            if (diff == TimeSpan.Zero) RelativeText = $"{day}, même heure";
            else RelativeText = $"{day}, {FormatOffset(diff, " h ")}";
        }

        private void UpdateSun(DateTime utcNow)
        {
            var date = DateOnly.FromDateTime(Now);
            var changed = date != _sunDate || Offset != _sunOffset;
            if (date != _sunDate)
            {
                _sunDate = date;
                _sun = null;
            }
            _sunOffset = Offset;
            if (date != _sunApiDate && !_sunApiBusy && utcNow >= _sunApiRetryAt)
                _ = FetchSunAsync(date, utcNow);

            if (_sun is not { } sun)
            {
                IsDay = Now.Hour is >= 7 and < 19;
                SunText = "";
                DayLengthText = "";
                return;
            }

            IsDay = utcNow >= sun.Rise && utcNow < sun.Set;
            if (!changed) return;
            SunText = $"Lever {(sun.Rise + Offset).ToString("HH:mm", Fr)} · Coucher {(sun.Set + Offset).ToString("HH:mm", Fr)}";
            var length = sun.Set - sun.Rise;
            DayLengthText = $"{(int)length.TotalHours} h {length.Minutes:00} de jour";
        }

        private async Task FetchSunAsync(DateOnly date, DateTime utcNow)
        {
            _sunApiBusy = true;
            var (ok, sun) = await SunApiService.GetAsync(date, Info.Latitude, Info.Longitude);
            _sunApiBusy = false;
            if (!ok)
            {
                _sunApiRetryAt = utcNow + TimeSpan.FromMinutes(15);
                return;
            }
            _sunApiDate = date;
            if (date != _sunDate) return;
            _sun = sun;
            _sunOffset = null;
        }

        internal static string FormatOffset(TimeSpan t, string sep)
        {
            var sign = "+";
            if (t < TimeSpan.Zero) sign = "−";

            var a = t.Duration();
            var text = $"{sign}{(int)a.TotalHours}";
            if (sep == ":")
            {
                if (a.Minutes != 0) text += $":{a.Minutes:00}";
                return text;
            }

            text += " h";
            if (a.Minutes != 0) text += $" {a.Minutes:00}";
            return text;
        }
    }
}
