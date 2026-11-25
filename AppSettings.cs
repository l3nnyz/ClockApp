using System.IO;
using System.Text.Json;
using ClockApp.Reminders;

namespace ClockApp
{
    /// <summary>
    /// Paramètres de l'application, sauvegardés automatiquement dans
    /// %AppData%\ClockApp\parametres.json à chaque modification.
    /// </summary>
    public sealed class AppSettings : Notif
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClockApp", "parametres.json");

        private bool _remindersEnabled = true;
        public bool RemindersEnabled { get => _remindersEnabled; set => SetField(ref _remindersEnabled, value); }

        private bool _morningReminder = true;
        public bool MorningReminder { get => _morningReminder; set => SetField(ref _morningReminder, value); }

        private bool _lunchReminder;
        public bool LunchReminder { get => _lunchReminder; set => SetField(ref _lunchReminder, value); }

        private bool _eveningReminder = true;
        public bool EveningReminder { get => _eveningReminder; set => SetField(ref _eveningReminder, value); }

        private bool _nightReminder;
        public bool NightReminder { get => _nightReminder; set => SetField(ref _nightReminder, value); }

        private bool _quietHours = true;
        public bool QuietHours { get => _quietHours; set => SetField(ref _quietHours, value); }

        private bool _playSound = true;
        public bool PlaySound { get => _playSound; set => SetField(ref _playSound, value); }

        public bool IsEnabled(ReminderKind kind) => kind switch
        {
            ReminderKind.Morning => MorningReminder,
            ReminderKind.Lunch => LunchReminder,
            ReminderKind.Evening => EveningReminder,
            ReminderKind.Night => NightReminder,
            _ => false,
        };

        public static AppSettings Load()
        {
            AppSettings settings;
            try
            {
                settings = File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings()
                    : new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                settings = new AppSettings();
            }

            settings.PropertyChanged += (_, _) => settings.Save();
            return settings;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
