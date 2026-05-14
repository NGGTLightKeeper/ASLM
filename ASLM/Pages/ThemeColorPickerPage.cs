// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace ASLM.Pages
{
    /// <summary>
    /// Compact modal color picker centered on a dimmed backdrop. HS plane updates only while the pointer is pressed.
    /// </summary>
    public sealed class ThemeColorPickerPage : ContentPage
    {
        private readonly TaskCompletionSource<Color?> _completion = new();
        private double _hue;
        private double _saturation = 1;
        private double _value = 1;
        private double _alpha = 1;
        private bool _hsPointerDown;

        private readonly GraphicsView _hsPlane;
        private readonly Slider _valueSlider;
        private readonly Slider _alphaSlider;
        private readonly Border _preview;
        private readonly BoxView _previewFill;
        private readonly Entry _hexEntry;
        private readonly Entry _rEntry;
        private readonly Entry _gEntry;
        private readonly Entry _bEntry;
        private bool _suppressSync;

        public Task<Color?> WaitForResultAsync() => _completion.Task;

        public static async Task<Color?> PickAsync(Color initial)
        {
            var host = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (host == null)
            {
                return null;
            }

            var page = new ThemeColorPickerPage(initial);
            await host.Navigation.PushModalAsync(page);
            return await page.WaitForResultAsync().ConfigureAwait(true);
        }

        public ThemeColorPickerPage(Color initial)
        {
            NavigationPage.SetHasNavigationBar(this, false);
            BackgroundColor = Colors.Transparent;
            Padding = 0;

            RgbToHsv(initial, out _hue, out _saturation, out _value);
            _alpha = initial.Alpha;

            _hsPlane = new GraphicsView
            {
                HeightRequest = 120,
                WidthRequest = 200,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            _hsPlane.Drawable = new HsPlaneDrawable(this);

            var ptr = new PointerGestureRecognizer();
            ptr.PointerPressed += OnHsPointerPressed;
            ptr.PointerMoved += OnHsPointerMoved;
            ptr.PointerReleased += OnHsPointerReleased;
            _hsPlane.GestureRecognizers.Add(ptr);

            _valueSlider = new Slider(0, 1, _value) { HorizontalOptions = LayoutOptions.Fill };
            _valueSlider.ValueChanged += (_, _) =>
            {
                _value = _valueSlider.Value;
                _hsPlane.Invalidate();
                SyncFromHsv();
            };

            _alphaSlider = new Slider(0, 1, _alpha) { HorizontalOptions = LayoutOptions.Fill };
            _alphaSlider.ValueChanged += (_, _) =>
            {
                _alpha = _alphaSlider.Value;
                SyncFromHsv();
            };

            _previewFill = new BoxView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                CornerRadius = 6
            };
            _preview = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                StrokeThickness = 1.5,
                Padding = new Thickness(2),
                Content = _previewFill,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            _hexEntry = CreateEntry();
            _hexEntry.FontSize = 12;
            _hexEntry.TextChanged += OnHexTextChanged;

            _rEntry = CreateRgbEntry();
            _gEntry = CreateRgbEntry();
            _bEntry = CreateRgbEntry();
            _rEntry.TextChanged += OnRgbTextChanged;
            _gEntry.TextChanged += OnRgbTextChanged;
            _bEntry.TextChanged += OnRgbTextChanged;

            var done = new Button { Text = "Done", HeightRequest = 36, CornerRadius = 6, FontSize = 13 };
            ApplyFooterButtonStyle(done, isPrimary: true);
            done.Clicked += async (_, _) => await CompleteAsync(GetCurrentColor());

            var cancel = new Button { Text = "Cancel", HeightRequest = 36, CornerRadius = 6, FontSize = 13 };
            ApplyFooterButtonStyle(cancel, isPrimary: false);
            cancel.Clicked += async (_, _) => await CompleteAsync(null);

            var leftCol = new VerticalStackLayout { Spacing = 8 };
            leftCol.Children.Add(_preview);
            leftCol.Children.Add(_hsPlane);
            leftCol.Children.Add(MiniLabel("Brightness"));
            leftCol.Children.Add(_valueSlider);
            leftCol.Children.Add(MiniLabel("Opacity"));
            leftCol.Children.Add(_alphaSlider);

            var rightCol = new VerticalStackLayout { Spacing = 8 };
            rightCol.Children.Add(MiniLabel("Hex"));
            rightCol.Children.Add(_hexEntry);
            rightCol.Children.Add(MiniLabel("RGB"));
            var rgbRow = new Grid { ColumnSpacing = 6 };
            rgbRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rgbRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rgbRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var rw = WrapMini("R", _rEntry);
            var gw = WrapMini("G", _gEntry);
            var bw = WrapMini("B", _bEntry);
            rgbRow.Children.Add(rw);
            Grid.SetColumn(rw, 0);
            rgbRow.Children.Add(gw);
            Grid.SetColumn(gw, 1);
            rgbRow.Children.Add(bw);
            Grid.SetColumn(bw, 2);
            rightCol.Children.Add(rgbRow);

            var body = new Grid { ColumnSpacing = 14, Padding = new Thickness(4, 2) };
            body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            body.Children.Add(leftCol);
            Grid.SetColumn(leftCol, 0);
            body.Children.Add(rightCol);
            Grid.SetColumn(rightCol, 1);

            var footer = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Fill };
            footer.Children.Add(done);
            footer.Children.Add(cancel);

            var cardInner = new VerticalStackLayout { Spacing = 12, Children = { body, footer } };

            var card = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                StrokeThickness = 1,
                Padding = new Thickness(16, 14),
                MaximumWidthRequest = 380,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Content = cardInner
            };
            card.SetDynamicResource(Border.BackgroundColorProperty, "BackgroundSecondary");
            card.SetDynamicResource(Border.StrokeProperty, "Separator");

            var backdrop = new BoxView();
            backdrop.SetDynamicResource(BoxView.BackgroundColorProperty, "OverlayBackground");
            var dismissTap = new TapGestureRecognizer();
            dismissTap.Tapped += async (_, _) => await CompleteAsync(null);
            backdrop.GestureRecognizers.Add(dismissTap);

            var root = new Grid();
            root.Children.Add(backdrop);
            root.Children.Add(card);

            Content = root;
            SyncFromHsv();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (!_completion.Task.IsCompleted)
            {
                _completion.TrySetResult(null);
            }
        }

        private static Label MiniLabel(string text)
        {
            var lab = new Label { Text = text, FontSize = 11, Margin = new Thickness(0, 0, 0, -4) };
            lab.SetDynamicResource(Label.TextColorProperty, "LabelSecondary");
            return lab;
        }

        private static VerticalStackLayout WrapMini(string caption, Entry entry)
        {
            var s = new VerticalStackLayout { Spacing = 2 };
            s.Children.Add(MiniLabel(caption));
            s.Children.Add(entry);
            return s;
        }

        private static void ApplyFooterButtonStyle(Button button, bool isPrimary)
        {
            var key = isPrimary ? "SettingsFooterPrimaryButtonStyle" : "SettingsFooterButtonStyle";
            if (Application.Current?.Resources.TryGetValue(key, out var st) == true && st is Style style)
            {
                button.Style = style;
            }
        }

        private void OnHsPointerPressed(object? sender, PointerEventArgs e)
        {
            _hsPointerDown = true;
            ApplyHsFromPointer(sender, e);
        }

        private void OnHsPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_hsPointerDown)
            {
                return;
            }

            ApplyHsFromPointer(sender, e);
        }

        private void OnHsPointerReleased(object? sender, PointerEventArgs e)
        {
            _hsPointerDown = false;
        }

        private void ApplyHsFromPointer(object? sender, PointerEventArgs e)
        {
            if (sender is not View view)
            {
                return;
            }

            var pt = e.GetPosition(view);
            if (!pt.HasValue)
            {
                return;
            }

            var w = Math.Max(1, view.Width);
            var h = Math.Max(1, view.Height);
            _hue = Math.Clamp(pt.Value.X / w, 0, 1);
            _saturation = Math.Clamp(1 - pt.Value.Y / h, 0, 1);
            _hsPlane.Invalidate();
            SyncFromHsv();
        }

        private async Task CompleteAsync(Color? result)
        {
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            _hsPointerDown = false;
            _completion.TrySetResult(result);
            if (Navigation.ModalStack.Count > 0 && Navigation.ModalStack[^1] == this)
            {
                await Navigation.PopModalAsync();
            }
        }

        private Color GetCurrentColor() => HsvToColor(_hue, _saturation, _value, _alpha);

        private void ApplyPreviewColor(Color c)
        {
            _previewFill.BackgroundColor = c;
            _preview.Stroke = ThemePaletteResolver.SwatchContrastStroke(c);
        }

        private void SyncFromHsv()
        {
            var c = GetCurrentColor();
            _suppressSync = true;
            ApplyPreviewColor(c);
            _hexEntry.Text = ThemePaletteResolver.ToHex(c);
            _rEntry.Text = ((int)(c.Red * 255)).ToString();
            _gEntry.Text = ((int)(c.Green * 255)).ToString();
            _bEntry.Text = ((int)(c.Blue * 255)).ToString();
            _suppressSync = false;
        }

        private void OnHexTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressSync)
            {
                return;
            }

            var text = e.NewTextValue?.Trim() ?? string.Empty;
            if (!ThemePaletteResolver.TryParseHex(text, out var c))
            {
                return;
            }

            RgbToHsv(c, out _hue, out _saturation, out _value);
            _alpha = c.Alpha;
            _valueSlider.Value = _value;
            _alphaSlider.Value = _alpha;
            _hsPlane.Invalidate();
            _suppressSync = true;
            ApplyPreviewColor(c);
            _rEntry.Text = ((int)(c.Red * 255)).ToString();
            _gEntry.Text = ((int)(c.Green * 255)).ToString();
            _bEntry.Text = ((int)(c.Blue * 255)).ToString();
            _suppressSync = false;
        }

        private void OnRgbTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressSync)
            {
                return;
            }

            if (!int.TryParse(_rEntry.Text, out var ri) ||
                !int.TryParse(_gEntry.Text, out var gi) ||
                !int.TryParse(_bEntry.Text, out var bi))
            {
                return;
            }

            _alpha = _alphaSlider.Value;
            ri = Math.Clamp(ri, 0, 255);
            gi = Math.Clamp(gi, 0, 255);
            bi = Math.Clamp(bi, 0, 255);
            var ai = (int)Math.Round(_alpha * 255);
            var withAlpha = Color.FromRgba(ri, gi, bi, ai);
            RgbToHsv(withAlpha, out _hue, out _saturation, out _value);
            _valueSlider.Value = _value;
            _hsPlane.Invalidate();
            _suppressSync = true;
            ApplyPreviewColor(withAlpha);
            _hexEntry.Text = ThemePaletteResolver.ToHex(withAlpha);
            _suppressSync = false;
        }

        private static Entry CreateEntry()
        {
            var entry = new Entry { FontSize = 12, HeightRequest = 34 };
            entry.SetDynamicResource(Entry.TextColorProperty, "LabelPrimary");
            entry.SetDynamicResource(Entry.BackgroundColorProperty, "FieldBackground");
            return entry;
        }

        private static Entry CreateRgbEntry()
        {
            var entry = CreateEntry();
            entry.Keyboard = Keyboard.Numeric;
            return entry;
        }

        private static void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            var r = color.Red;
            var g = color.Green;
            var b = color.Blue;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            v = max;
            s = max < 1e-8 ? 0 : delta / max;

            if (delta < 1e-8)
            {
                h = 0;
                return;
            }

            double hh;
            if (Math.Abs(max - r) < 1e-8)
            {
                hh = ((g - b) / delta % 6 + 6) % 6;
            }
            else if (Math.Abs(max - g) < 1e-8)
            {
                hh = (b - r) / delta + 2;
            }
            else
            {
                hh = (r - g) / delta + 4;
            }

            h = hh / 6;
        }

        private static Color HsvToColor(double h, double s, double v, double a)
        {
            h = (h % 1 + 1) % 1;
            var hh = h * 6;
            var i = (int)Math.Floor(hh);
            var f = hh - i;
            var p = v * (1 - s);
            var q = v * (1 - f * s);
            var t = v * (1 - (1 - f) * s);

            double r, g, b;
            switch (i % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }

            return new Color((float)r, (float)g, (float)b, (float)a);
        }

        private sealed class HsPlaneDrawable : IDrawable
        {
            private readonly ThemeColorPickerPage _owner;

            public HsPlaneDrawable(ThemeColorPickerPage owner) => _owner = owner;

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                const int step = 3;
                var w = dirtyRect.Width;
                var h = dirtyRect.Height;
                var v = (float)_owner._value;
                for (var y = 0; y < h; y += step)
                {
                    for (var x = 0; x < w; x += step)
                    {
                        var hue = x / w;
                        var sat = 1 - y / h;
                        canvas.FillColor = HsvToColor(hue, sat, v, 1);
                        canvas.FillRectangle(x, y, step, step);
                    }
                }

                var px = (float)(_owner._hue * w);
                var py = (float)((1 - _owner._saturation) * h);
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 2;
                canvas.DrawCircle(px, py, 7);
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 1;
                canvas.DrawCircle(px, py, 7);
            }
        }
    }
}
