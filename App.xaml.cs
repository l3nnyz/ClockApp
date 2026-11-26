using System.Windows;

namespace ClockApp
{
    public partial class App : Application
    {
        private MainWindow? _window;
        private TrayIcon? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = AppSettings.Load();
            ApplyTheme(settings.Theme);
            _window = new MainWindow(settings);

            if (settings.StartWithWindows) StartupManager.Apply(true);
            settings.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AppSettings.StartWithWindows))
                    StartupManager.Apply(settings.StartWithWindows);
                else if (args.PropertyName == nameof(AppSettings.Theme))
                    ApplyTheme(settings.Theme);
            };

            _tray = new TrayIcon(settings);
            _tray.OpenRequested += () => _window.Open();
            _tray.ExitRequested += Quit;
            _window.HiddenToTray += _tray.ShowBackgroundHint;
            _window.ExitRequested += Quit;

            if (e.Args.Contains(StartupManager.TrayArgument)) _window.StartBackgroundWork();
            else _window.Open();
        }

        private void ApplyTheme(AppTheme theme)
        {
            var mode = theme switch
            {
                AppTheme.Light => ThemeMode.Light,
                AppTheme.Dark => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
            if (ThemeMode != mode) ThemeMode = mode;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            _tray = null;
            base.OnExit(e);
        }

        private void Quit()
        {
            _window?.AllowClose();
            Shutdown();
        }
    }
}
