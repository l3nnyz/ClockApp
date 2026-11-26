using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ClockApp
{
    public sealed class TrayIcon
    {
        private readonly Forms.NotifyIcon _icon;
        private readonly Forms.ToolStripMenuItem _remindersItem;
        private readonly AppSettings _settings;
        private bool _hintShown;

        public event Action? OpenRequested;
        public event Action? ExitRequested;

        public TrayIcon(AppSettings settings)
        {
            _settings = settings;

            _remindersItem = new Forms.ToolStripMenuItem("Rappels activés") { CheckOnClick = true, Checked = settings.RemindersEnabled };
            _remindersItem.CheckedChanged += (_, _) => _settings.RemindersEnabled = _remindersItem.Checked;
            settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppSettings.RemindersEnabled))
                    _remindersItem.Checked = settings.RemindersEnabled;
            };

            var open = new Forms.ToolStripMenuItem("Ouvrir l'horloge") { Font = new Font(Forms.Control.DefaultFont, FontStyle.Bold) };
            open.Click += (_, _) => OpenRequested?.Invoke();
            var exit = new Forms.ToolStripMenuItem("Quitter");
            exit.Click += (_, _) => ExitRequested?.Invoke();

            var menu = new Forms.ContextMenuStrip();
            menu.Items.AddRange([open, _remindersItem, new Forms.ToolStripSeparator(), exit]);

            _icon = new Forms.NotifyIcon
            {
                Icon = CreateClockIcon(),
                Text = "Horloge mondiale",
                ContextMenuStrip = menu,
                Visible = true,
            };
            _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke(); };
        }

        public void ShowBackgroundHint()
        {
            if (_hintShown) return;
            _hintShown = true;
            _icon.ShowBalloonTip(4000, "Toujours là, en arrière-plan",
                "Vos rappels continuent. Clic sur l'icône pour rouvrir, clic droit pour quitter.",
                Forms.ToolTipIcon.None);
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
    }
}
