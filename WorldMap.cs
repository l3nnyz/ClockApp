using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClockApp
{
    public sealed class WorldMap : Grid
    {
        private const double MapWidth = 360;
        private const double MapHeight = 142;
        private const double MaxLatitude = 84;

        private static readonly Geometry LandGeometry = LoadLand();
        private static readonly Brush DayBrush = Frozen("#FFB900");
        private static readonly Brush NightBrush = Frozen("#8AB4F8");

        private readonly Grid _root = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        private readonly Canvas _overlay = new() { ClipToBounds = false };
        private readonly List<Pin> _pins = new();
        private ObservableCollection<CityClock>? _cities;

        public event Action<CityClock>? CityClicked;

        public WorldMap()
        {
            var land = new Path { Data = LandGeometry, Opacity = 0.22 };
            land.SetResourceReference(Shape.FillProperty, "TextFillColorPrimaryBrush");
            var canvas = new Canvas { Width = MapWidth, Height = MapHeight };
            canvas.Children.Add(land);
            _root.Children.Add(new Viewbox { Stretch = Stretch.Fill, Child = canvas });
            _root.Children.Add(_overlay);
            Children.Add(_root);
            SizeChanged += (_, _) => Layout();
        }

        public ObservableCollection<CityClock>? Cities
        {
            get => _cities;
            set
            {
                if (_cities is not null) _cities.CollectionChanged -= OnCitiesChanged;
                _cities = value;
                if (_cities is not null) _cities.CollectionChanged += OnCitiesChanged;
                Rebuild();
            }
        }

        private void OnCitiesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

        private void Rebuild()
        {
            foreach (var pin in _pins) pin.Detach();
            _pins.Clear();
            _overlay.Children.Clear();
            if (_cities is null) return;

            foreach (var city in _cities)
            {
                var pin = new Pin(city);
                pin.Clicked += c => CityClicked?.Invoke(c);
                _overlay.Children.Add(pin.Leader);
                _overlay.Children.Add(pin.Dot);
                _overlay.Children.Add(pin.Label);
                _pins.Add(pin);
            }
            Layout();
        }

        private void Layout()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var scale = Math.Min(ActualWidth / MapWidth, ActualHeight / MapHeight);
            var w = MapWidth * scale;
            var h = MapHeight * scale;
            _root.Width = w;
            _root.Height = h;

            var taken = new List<Rect>();
            foreach (var pin in _pins)
            {
                pin.Position = new Point((pin.City.Info.Longitude + 180) * scale, (MaxLatitude - pin.City.Info.Latitude) * scale);
                taken.Add(new Rect(pin.Position.X - 7, pin.Position.Y - 7, 14, 14));
            }

            foreach (var pin in _pins.OrderBy(p => p.Position.Y))
            {
                pin.Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var size = pin.Label.DesiredSize;
                var bounds = new Rect(0, 0, w, h);
                Rect? best = null;
                foreach (var candidate in Candidates(pin.Position, size))
                {
                    if (!bounds.Contains(candidate)) continue;
                    if (taken.Any(t => t.IntersectsWith(candidate))) continue;
                    best = candidate;
                    break;
                }
                var rect = best ?? Clamp(new Rect(pin.Position.X + 12, pin.Position.Y - size.Height / 2, size.Width, size.Height), bounds);
                taken.Add(rect);
                pin.Place(rect);
            }
        }

        private static IEnumerable<Rect> Candidates(Point p, Size s)
        {
            for (var d = 12.0; d <= 120; d += 18)
            {
                yield return new Rect(p.X + d, p.Y - s.Height / 2, s.Width, s.Height);
                yield return new Rect(p.X - d - s.Width, p.Y - s.Height / 2, s.Width, s.Height);
                yield return new Rect(p.X - s.Width / 2, p.Y - d - s.Height, s.Width, s.Height);
                yield return new Rect(p.X - s.Width / 2, p.Y + d, s.Width, s.Height);
                yield return new Rect(p.X + d * 0.7, p.Y - d * 0.7 - s.Height, s.Width, s.Height);
                yield return new Rect(p.X + d * 0.7, p.Y + d * 0.7, s.Width, s.Height);
                yield return new Rect(p.X - d * 0.7 - s.Width, p.Y - d * 0.7 - s.Height, s.Width, s.Height);
                yield return new Rect(p.X - d * 0.7 - s.Width, p.Y + d * 0.7, s.Width, s.Height);
            }
        }

        private static Rect Clamp(Rect r, Rect bounds)
        {
            var x = Math.Max(bounds.Left, Math.Min(r.X, bounds.Right - r.Width));
            var y = Math.Max(bounds.Top, Math.Min(r.Y, bounds.Bottom - r.Height));
            return new Rect(x, y, r.Width, r.Height);
        }

        private static Geometry LoadLand()
        {
            using var stream = typeof(WorldMap).Assembly.GetManifestResourceStream("ClockApp.Assets.world-land.txt");
            if (stream is null) return Geometry.Empty;
            using var reader = new System.IO.StreamReader(stream);
            var geometry = Geometry.Parse(reader.ReadToEnd());
            geometry.Freeze();
            return geometry;
        }

        private static Brush Frozen(string color)
        {
            var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
            brush.Freeze();
            return brush;
        }

        private sealed class Pin
        {
            public CityClock City { get; }
            public Ellipse Dot { get; } = new() { Width = 10, Height = 10, StrokeThickness = 2, Cursor = Cursors.Hand };
            public Line Leader { get; } = new() { StrokeThickness = 1, Opacity = 0.5, IsHitTestVisible = false };
            public Border Label { get; }
            public Point Position { get; set; }

            public event Action<CityClock>? Clicked;

            public Pin(CityClock city)
            {
                City = city;
                Dot.SetResourceReference(Shape.StrokeProperty, "CardBackgroundFillColorDefaultBrush");
                Leader.SetResourceReference(Shape.StrokeProperty, "TextFillColorSecondaryBrush");

                var name = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };
                name.SetBinding(TextBlock.TextProperty, new Binding(nameof(CityClock.City)) { Source = city });
                var time = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold };
                time.SetResourceReference(TextBlock.FontFamilyProperty, "DisplayFont");
                time.SetBinding(TextBlock.TextProperty, new Binding(nameof(CityClock.TimeText)) { Source = city });
                var relative = new TextBlock { FontSize = 11 };
                relative.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                relative.SetBinding(TextBlock.TextProperty, new Binding(nameof(CityClock.RelativeText)) { Source = city });

                var stack = new StackPanel();
                stack.Children.Add(name);
                stack.Children.Add(time);
                stack.Children.Add(relative);

                Label = new Border
                {
                    Child = stack,
                    Padding = new Thickness(10, 6, 10, 7),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                };
                Label.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");

                var tooltip = new TextBlock();
                tooltip.SetBinding(TextBlock.TextProperty, new Binding(nameof(CityClock.DateText)) { Source = city });
                Label.ToolTip = tooltip;
                Dot.ToolTip = $"{city.City} — {city.Country}";

                Dot.MouseLeftButtonUp += OnClick;
                Label.MouseLeftButtonUp += OnClick;
                city.PropertyChanged += OnCityChanged;
                Refresh();
            }

            private void OnClick(object sender, MouseButtonEventArgs e) => Clicked?.Invoke(City);

            private void OnCityChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is nameof(CityClock.IsDay) or nameof(CityClock.IsSelected)) Refresh();
            }

            private void Refresh()
            {
                Dot.Fill = City.IsDay ? DayBrush : NightBrush;
                var size = City.IsSelected ? 14 : 10;
                Dot.Width = size;
                Dot.Height = size;
                Canvas.SetLeft(Dot, Position.X - size / 2.0);
                Canvas.SetTop(Dot, Position.Y - size / 2.0);
                if (City.IsSelected)
                {
                    Label.SetResourceReference(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
                    Label.BorderThickness = new Thickness(2);
                    Label.Padding = new Thickness(9, 5, 9, 6);
                }
                else
                {
                    Label.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
                    Label.BorderThickness = new Thickness(1);
                    Label.Padding = new Thickness(10, 6, 10, 7);
                }
            }

            public void Place(Rect rect)
            {
                Canvas.SetLeft(Label, rect.X);
                Canvas.SetTop(Label, rect.Y);
                Refresh();
                var target = new Point(
                    Math.Max(rect.Left, Math.Min(Position.X, rect.Right)),
                    Math.Max(rect.Top, Math.Min(Position.Y, rect.Bottom)));
                Leader.X1 = Position.X;
                Leader.Y1 = Position.Y;
                Leader.X2 = target.X;
                Leader.Y2 = target.Y;
            }

            public void Detach()
            {
                City.PropertyChanged -= OnCityChanged;
                Dot.MouseLeftButtonUp -= OnClick;
                Label.MouseLeftButtonUp -= OnClick;
            }
        }
    }
}
