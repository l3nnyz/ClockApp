using Microsoft.Win32;

namespace ClockApp
{
    public static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ClockApp";

        public const string TrayArgument = "--tray";

        public static void Apply(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled && Environment.ProcessPath is { } exe)
                    key.SetValue(ValueName, $"\"{exe}\" {TrayArgument}");
                else
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
        }
    }
}
