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
        private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private bool _allowClose;
        private Point _dragStart;
        private CityClock? _pressedCity;

        public MainViewModel ViewModel => _vm;

        public event Action? HiddenToTray;

        public event Action? ExitRequested;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            DrawDialTicks();

            IsVisibleChanged += OnIsVisibleChanged;

            _reminderTimer.Tick += (_, _) => _vm.CheckReminders();
            _vm.Reminders.Raised += OnReminderRaised;

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
            if (!_reminderTimer.IsEnabled) _reminderTimer.Start();
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

        private void CityCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: CityClock clock }) return;
            _pressedCity = clock;
            _dragStart = e.GetPosition(this);
        }

        private void CityCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_pressedCity is not { } city || e.LeftButton != MouseButtonState.Pressed) return;

            var delta = e.GetPosition(this) - _dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            _pressedCity = null;
            var before = _vm.Cities.ToList();
            city.IsDragging = true;
            try
            {
                DragDrop.DoDragDrop((DependencyObject)sender, city, DragDropEffects.Move);
            }
            finally
            {
                city.IsDragging = false;
                if (!before.SequenceEqual(_vm.Cities)) _vm.Save();
            }
        }

        private void CityCard_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(CityClock)) is CityClock dragged
                && sender is FrameworkElement { DataContext: CityClock target })
            {
                if (dragged != target) _vm.MoveCity(dragged, _vm.Cities.IndexOf(target));
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void CityCard_Drop(object sender, DragEventArgs e) => e.Handled = true;

        private void CardsScroller_PreviewDragOver(object sender, DragEventArgs e)
        {
            const double edge = 48, step = 12;
            var y = e.GetPosition(CardsScroller).Y;
            if (y < edge) CardsScroller.ScrollToVerticalOffset(CardsScroller.VerticalOffset - step);
            else if (y > CardsScroller.ActualHeight - edge) CardsScroller.ScrollToVerticalOffset(CardsScroller.VerticalOffset + step);
        }

        private void CityCard_Click(object sender, MouseButtonEventArgs e)
        {
            _pressedCity = null;
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
