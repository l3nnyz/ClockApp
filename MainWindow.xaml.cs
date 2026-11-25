using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ClockApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private readonly DispatcherTimer _syncTimer = new() { Interval = TimeSpan.FromMinutes(15) };
        private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            DrawDialTicks();

            CompositionTarget.Rendering += (_, _) => _vm.Tick();

            _reminderTimer.Tick += (_, _) => _vm.CheckReminders();
            _vm.Reminders.Raised += reminder =>
            {
                ReminderToast.Show(reminder);
                if (_vm.Settings.PlaySound) SystemSounds.Asterisk.Play();
            };

            _syncTimer.Tick += async (_, _) => await _vm.SyncAsync();
            Loaded += async (_, _) =>
            {
                _reminderTimer.Start();
                _syncTimer.Start();
                await _vm.SyncAsync();
            };
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
