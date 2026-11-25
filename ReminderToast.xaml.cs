using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClockApp.Reminders;

namespace ClockApp
{
    /// <summary>
    /// Petite notification affichée en bas à droite de l'écran, empilée avec les autres,
    /// qui se ferme seule (sauf si la souris est dessus).
    /// </summary>
    public partial class ReminderToast : Window
    {
        private static readonly List<ReminderToast> Open = new();
        private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(9) };
        private bool _closing;

        private ReminderToast(Reminder reminder)
        {
            InitializeComponent();
            TitleText.Text = reminder.Title;
            BodyText.Text = reminder.Body;
            (KindIcon.Text, KindIcon.Foreground) = reminder.Kind switch
            {
                ReminderKind.Morning => ("", Brush("#FFB900")),   // soleil
                ReminderKind.Lunch => ("", Brush("#F7630C")),     // tasse
                ReminderKind.Evening => ("", Brush("#C239B3")),   // départ
                _ => ("", Brush("#8AB4F8")),                      // lune
            };

            _closeTimer.Tick += (_, _) => FadeOut();
            MouseEnter += (_, _) => _closeTimer.Stop();
            MouseLeave += (_, _) => _closeTimer.Start();
            Loaded += (_, _) =>
            {
                Reposition();
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
                _closeTimer.Start();
            };
            Closed += (_, _) => { Open.Remove(this); Reposition(); };
        }

        public static void Show(Reminder reminder)
        {
            var toast = new ReminderToast(reminder);
            Open.Add(toast);
            if (Open.Count > 4) Open[0].FadeOut();
            toast.Show();
        }

        /// <summary>Empile les notifications ouvertes depuis le bas de la zone de travail.</summary>
        private static void Reposition()
        {
            var area = SystemParameters.WorkArea;
            var bottom = area.Bottom;
            for (var i = Open.Count - 1; i >= 0; i--)
            {
                var t = Open[i];
                if (!t.IsLoaded) continue;
                t.Left = area.Right - t.ActualWidth;
                t.Top = bottom - t.ActualHeight;
                bottom = t.Top + 12; // les marges de l'ombre se chevauchent
            }
        }

        private void FadeOut()
        {
            if (_closing) return;
            _closing = true;
            _closeTimer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => FadeOut();

        private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    }
}
