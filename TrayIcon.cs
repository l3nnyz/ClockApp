using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace ClockApp
{
    public sealed class TrayIcon : IDisposable
    {
        private const int MaxCitiesInMenu = 6;

        private readonly Forms.NotifyIcon _icon;
        private readonly AppSettings _settings;
        private readonly MainViewModel _vm;
        private readonly ContextMenu _menu = new() { Placement = PlacementMode.AbsolutePoint, MinWidth = 260, StaysOpen = true };
        private readonly List<(MenuItem Item, CityClock City)> _cityItems = new();
        private readonly DispatcherTimer _menuTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private bool _hintShown;

        private static readonly Media.Brush DayBrush = Frozen("#FFB900");
        private static readonly Media.Brush NightBrush = Frozen("#8AB4F8");

        public event Action? OpenRequested;
        public event Action<int>? TabRequested;
        public event Action? ExitRequested;

        public TrayIcon(AppSettings settings, MainViewModel vm)
        {
            _settings = settings;
            _vm = vm;
            _menu.Opened += OnMenuOpened;
            _menu.Closed += (_, _) => _menuTimer.Stop();
            _menuTimer.Tick += (_, _) => RefreshCityItems();

            _icon = new Forms.NotifyIcon
            {
                Icon = CreateClockIcon(),
                Text = "Horloge mondiale",
                Visible = true,
            };
            _icon.MouseClick += OnIconClick;
            _icon.MouseMove += (_, _) => UpdateTooltip();
        }

        public void ShowBackgroundHint()
        {
            if (_hintShown) return;
            _hintShown = true;
            _icon.ShowBalloonTip(4000, "Toujours là, en arrière-plan",
                "Vos rappels continuent. Clic sur l'icône pour rouvrir, clic droit pour le menu.",
                Forms.ToolTipIcon.None);
        }

        private void OnIconClick(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke();
            else if (e.Button == Forms.MouseButtons.Right) ShowMenu();
        }


        private void ShowMenu()
        {
            if (_menu.IsOpen) _menu.IsOpen = false;
            BuildMenu();

            var cursor = Forms.Cursor.Position;
            var scale = GetDpiForSystem() / 96.0;

            var point = new System.Windows.Point(cursor.X / scale, cursor.Y / scale);
            _menu.HorizontalOffset = point.X;
            _menu.VerticalOffset = point.Y;
            _menu.IsOpen = true;
        }

        private void OnMenuOpened(object sender, RoutedEventArgs e)
        {
            if (PresentationSource.FromVisual(_menu) is not HwndSource hwnd) return;
            if (_hookedSource != hwnd)
            {
                _hookedSource?.RemoveHook(MenuHook);
                _hookedSource = hwnd;
                hwnd.AddHook(MenuHook);
            }
            SetForegroundWindow(hwnd.Handle);
            _menu.Focus();
            _menuTimer.Start();
        }

        private HwndSource? _hookedSource;

        private IntPtr MenuHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_ACTIVATE = 0x0006;
            var deactivated = (wParam.ToInt64() & 0xFFFF) == 0;
            if (msg == WM_ACTIVATE && deactivated && _menu.IsOpen)
                _menu.Dispatcher.BeginInvoke(() => _menu.IsOpen = false);
            return IntPtr.Zero;
        }

        private void BuildMenu()
        {
            _menu.Items.Clear();
            _cityItems.Clear();
            _vm.Tick();

            foreach (var city in _vm.Cities.Take(MaxCitiesInMenu))
            {
                var item = Item(city.City, "", () =>
                {
                    _vm.Selected = city;
                    OpenRequested?.Invoke();
                });
                if (city.IsSelected) item.FontWeight = FontWeights.SemiBold;
                _cityItems.Add((item, city));
                _menu.Items.Add(item);
            }
            RefreshCityItems();
            if (_vm.Cities.Count > 0) _menu.Items.Add(new Separator());

            _menu.Items.Add(Item("Ouvrir l'horloge", "", () => TabRequested?.Invoke(0), bold: true));
            _menu.Items.Add(Item("Voir la carte", "", () => TabRequested?.Invoke(1)));
            _menu.Items.Add(SyncItem());
            _menu.Items.Add(new Separator());

            _menu.Items.Add(Toggle("Rappels", "", _settings.RemindersEnabled, v => _settings.RemindersEnabled = v));
            _menu.Items.Add(Toggle("Ne pas déranger la nuit", "", _settings.QuietHours, v => _settings.QuietHours = v));
            _menu.Items.Add(Toggle("Son des rappels", "", _settings.PlaySound, v => _settings.PlaySound = v));
            _menu.Items.Add(Item("Paramètres", "", () => TabRequested?.Invoke(2)));
            _menu.Items.Add(new Separator());

            _menu.Items.Add(Item("Quitter", "", () => ExitRequested?.Invoke()));
        }

        private MenuItem SyncItem()
        {
            var label = "Synchroniser l'heure";
            if (_vm.IsSyncing) label = "Synchronisation…";

            var item = Item(label, "", async () => await _vm.SyncAsync());
            item.IsEnabled = _vm.CanSync;
            item.ToolTip = _vm.StatusText;
            return item;
        }

        private void RefreshCityItems()
        {
            _vm.Tick();
            foreach (var (item, city) in _cityItems)
            {
                item.InputGestureText = $"{city.TimeText}  ·  {city.UtcText}";
                if (item.Icon is not TextBlock icon) continue;

                if (city.IsDay)
                {
                    icon.Text = "";
                    icon.Foreground = DayBrush;
                }
                else
                {
                    icon.Text = "";
                    icon.Foreground = NightBrush;
                }
            }
        }

        private static Media.Brush Frozen(string color)
        {
            var brush = (Media.Brush)new Media.BrushConverter().ConvertFromString(color)!;
            brush.Freeze();
            return brush;
        }

        private static MenuItem Toggle(string label, string glyph, bool value, Action<bool> set)
        {
            var item = Item(label, glyph, null);
            item.IsCheckable = true;
            item.IsChecked = value;
            item.Click += (_, _) => set(item.IsChecked);
            return item;
        }

        private static MenuItem Item(string label, string glyph, Action? click, bool bold = false)
        {
            var item = new MenuItem
            {
                Header = label,
                Icon = new TextBlock
                {
                    Text = glyph,
                    FontFamily = (WpfFontFamily)Application.Current.Resources["IconFont"],
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            if (bold) item.FontWeight = FontWeights.SemiBold;
            if (click is not null) item.Click += (_, _) => click();
            return item;
        }

        private void UpdateTooltip()
        {
            _vm.Tick();
            var lines = _vm.Cities.Take(4).Select(c => $"{c.City} {c.TimeText}");
            var text = string.Join("\n", lines.Prepend("Horloge mondiale"));
            if (text.Length > 127) text = text[..127];
            _icon.Text = text;
        }

        public void Dispose()
        {
            _menuTimer.Stop();
            _menu.IsOpen = false;
            _icon.Visible = false;
            _icon.Icon?.Dispose();
            _icon.Dispose();
        }

        private static Icon CreateClockIcon()
        {
            const int size = 32;
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var face = new SolidBrush(Color.FromArgb(0, 120, 212));
                g.FillEllipse(face, 1, 1, size - 3, size - 3);

                using var hand = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var c = new PointF(size / 2f - 0.5f, size / 2f - 0.5f);
                g.DrawLine(hand, c, new PointF(c.X, c.Y - 10));
                g.DrawLine(hand, c, new PointF(c.X + 7, c.Y + 3));
            }

            var handle = bmp.GetHicon();
            try { return (Icon)Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();
    }
}
