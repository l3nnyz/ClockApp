using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ClockApp.Reminders;
using ClockApp.ServiceAPI;

namespace ClockApp
{
    public sealed class MainViewModel : Notif
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClockApp", "villes.json");

        public ObservableCollection<CityClock> Cities { get; } = new();

        public IReadOnlyList<CityInfo> Catalog => CityInfo.Catalog;

        public AppSettings Settings { get; } = AppSettings.Load();

        public ReminderService Reminders { get; }

        private CityClock? _selected;
        public CityClock? Selected
        {
            get => _selected;
            set
            {
                if (_selected is not null) _selected.IsSelected = false;
                if (SetField(ref _selected, value) && value is not null) value.IsSelected = true;
            }
        }

        private CityInfo? _cityToAdd;
        public CityInfo? CityToAdd { get => _cityToAdd; set => SetField(ref _cityToAdd, value); }

        private string _statusText = "Synchronisation…";
        public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

        private bool _isSyncing;
        public bool IsSyncing { get => _isSyncing; private set { if (SetField(ref _isSyncing, value)) OnPropertyChanged(nameof(CanSync)); } }
        public bool CanSync => !IsSyncing;

        private double _hourAngle, _minuteAngle, _secondAngle;
        public double HourAngle { get => _hourAngle; private set => SetField(ref _hourAngle, value); }
        public double MinuteAngle { get => _minuteAngle; private set => SetField(ref _minuteAngle, value); }
        public double SecondAngle { get => _secondAngle; private set => SetField(ref _secondAngle, value); }

        public MainViewModel()
        {
            Reminders = new ReminderService(Settings);
            foreach (var id in LoadSavedIds())
            {
                var info = CityInfo.Catalog.FirstOrDefault(c => c.TimeZoneId == id);
                if (info is not null) Cities.Add(new CityClock(info));
            }
            Selected = Cities.FirstOrDefault();
            Tick();
        }

        public void Tick()
        {
            var utc = DateTime.UtcNow;
            foreach (var c in Cities) c.Update(utc);

            if (Selected is { } s)
            {
                var t = s.Now.TimeOfDay;
                SecondAngle = (t.TotalSeconds % 60) * 6;
                MinuteAngle = (t.TotalMinutes % 60) * 6;
                HourAngle = (t.TotalHours % 12) * 30;
            }
        }

        public void CheckReminders() => Reminders.Check(Cities, DateTime.UtcNow);

        public void TestReminder()
        {
            // Une ville à la même heure que le PC ne reçoit jamais de rappel : on en prend une autre si possible.
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            bool Distant(CityClock c) => Math.Abs((c.Offset - localOffset).TotalMinutes) >= 1;

            var city = Selected is { } s && Distant(s) ? s : Cities.FirstOrDefault(Distant) ?? Selected;
            if (city is not null) Reminders.Test(city);
        }

        public async Task SyncAsync()
        {
            if (IsSyncing || Cities.Count == 0) return;
            IsSyncing = true;
            StatusText = "Synchronisation avec timeapi.io…";

            var cities = Cities.ToList();
            var ok = 0;
            foreach (var city in cities)
            {
                var offset = await TimeApiService.GetOffsetAsync(city.TimeZoneId);
                if (offset is null)
                {
                    await Task.Delay(1500);
                    offset = await TimeApiService.GetOffsetAsync(city.TimeZoneId);
                }

                if (offset is { } o) { city.SetApiOffset(o); ok++; }
                else city.ApplyFallbackOffset();
            }

            var at = DateTime.Now.ToString("HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
            StatusText = ok == cities.Count
                ? $"Synchronisé via timeapi.io à {at}"
                : ok == 0
                    ? $"API injoignable — heure système utilisée ({at})"
                    : $"{ok}/{cities.Count} villes synchronisées à {at}";
            IsSyncing = false;
            Tick();
        }

        public async Task AddCityAsync()
        {
            if (CityToAdd is not { } info) return;
            var existing = Cities.FirstOrDefault(c => c.TimeZoneId == info.TimeZoneId);
            if (existing is not null) { Selected = existing; CityToAdd = null; return; }

            var clock = new CityClock(info);
            Cities.Add(clock);
            Selected = clock;
            CityToAdd = null;
            Save();
            Tick();

            if (await TimeApiService.GetOffsetAsync(info.TimeZoneId) is { } offset)
                clock.SetApiOffset(offset);
        }

        public void RemoveCity(CityClock clock)
        {
            var index = Cities.IndexOf(clock);
            Cities.Remove(clock);
            if (Selected == clock)
                Selected = Cities.Count == 0 ? null : Cities[Math.Min(index, Cities.Count - 1)];
            Save();
        }

        public void MoveCity(CityClock city, int index)
        {
            var from = Cities.IndexOf(city);
            if (from >= 0 && index >= 0 && index < Cities.Count && from != index) Cities.Move(from, index);
        }

        private static IEnumerable<string> LoadSavedIds()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var ids = JsonSerializer.Deserialize<string[]>(File.ReadAllText(SettingsPath));
                    if (ids is { Length: > 0 }) return ids;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
            return CityInfo.Defaults;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Cities.Select(c => c.TimeZoneId)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
