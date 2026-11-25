using System.ComponentModel;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClockApp.Reminders;

namespace ClockApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private readonly DispatcherTimer _syncTimer = new() { Interval = TimeSpan.FromMinutes(15) };
        private bool _allowClose;

        public MainViewModel ViewModel => _vm;

        public event Action? HiddenToTray;

        public event Action? ExitRequested;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            DrawDialTicks();

            IsVisibleChanged += OnIsVisibleChanged;

            _syncTimer.Tick += async (_, _) => await _vm.SyncAsync();
            _syncTimer.Start();
            Dispatcher.InvokeAsync(_vm.SyncAsync);
        }

        public void Open()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        public void StartBackgroundWork()
        {
            var reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            reminderTimer.Tick += (_, _) => _vm.CheckReminders();
            reminderTimer.Start();
            _vm.Reminders.Raised += OnReminderRaised;
        }

        public void AllowClose() => _allowClose = true;

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                CompositionTarget.Rendering += OnRendering;
                StartBackgroundWork();
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object? sender, EventArgs e) => _vm.Tick();

        private void OnReminderRaised(Reminder reminder)
        {
            ReminderToast.Show(reminder);
            if (_vm.Settings.PlaySound) SystemSounds.Asterisk.Play();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose && _vm.Settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                HiddenToTray?.Invoke();
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_allowClose) ExitRequested?.Invoke();
        }

        private void DrawDialTicks()
        {
            const double c = 100;
            for (var i = 0; i < 60; i++)
            {
                var major = i % 5 == 0;
                var angle = i * 6 * Math.PI / 180;
                double outer = 90, inner = major ? 80 : 86;
                var tick = new Line
                {
                    X1 = c + inner * Math.Sin(angle), Y1 = c - inner * Math.Cos(angle),
                    X2 = c + outer * Math.Sin(angle), Y2 = c - outer * Math.Cos(angle),
                    StrokeThickness = major ? 3 : 1.2,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = major ? 0.9 : 0.4,
                };
                tick.SetResourceReference(Shape.StrokeProperty, "TextFillColorPrimaryBrush");
                DialTicks.Children.Add(tick);
            }
        }

        private void CityCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CityClock clock }) _vm.Selected = clock;
        }

        private void RemoveCity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CityClock clock }) _vm.RemoveCity(clock);
            e.Handled = true;
        }

        private async void AddCity_Click(object sender, RoutedEventArgs e) => await _vm.AddCityAsync();

        private void TestReminder_Click(object sender, RoutedEventArgs e) => _vm.TestReminder();

        private async void Sync_Click(object sender, RoutedEventArgs e) => await _vm.SyncAsync();
    }
}
