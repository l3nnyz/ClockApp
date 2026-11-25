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

            _window = new MainWindow();
            var settings = _window.ViewModel.Settings;

            if (settings.StartWithWindows) StartupManager.Apply(true);
            settings.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AppSettings.StartWithWindows))
                    StartupManager.Apply(settings.StartWithWindows);
            };

            _tray = new TrayIcon(settings);
            _tray.OpenRequested += () => _window.Open();
            _tray.ExitRequested += Quit;
            _window.HiddenToTray += _tray.ShowBackgroundHint;
            _window.ExitRequested += Quit;

            if (e.Args.Contains(StartupManager.TrayArgument)) _window.StartBackgroundWork();
            else _window.Open();
        }

        private void Quit()
        {
            _window?.AllowClose();
            Shutdown();
        }
    }
}
