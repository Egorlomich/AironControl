using AironControl.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;

namespace AironControl
{
    public partial class MainPage : ContentPage
    {
        private double _lastWidth = 0;
        private double _lastHeight = 0;
        private readonly ConnectionVM _connectionVm;
        private bool _isLandscape;

        public MainPage() : this(IPlatformApplication.Current!.Services.GetRequiredService<ConnectionVM>()) { }

        public MainPage(ConnectionVM connectionVm)
        {
            _connectionVm = connectionVm;
            try
            {
                InitializeComponent();
                NavigationPage.SetHasNavigationBar(this, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainPage constructor error: {ex}");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                _connectionVm.RefreshStatus();
                StatusIndicator.Source = _connectionVm.IsConnected ? "green_light.svg" : "red_light.svg";
                statusLabel.Text = _connectionVm.IsConnected ? "Подключено" : "Нет подключения";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAppearing error: {ex}");
                StatusIndicator.Source = "red_light.svg";
                statusLabel.Text = "Нет подключения";
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width == _lastWidth && height == _lastHeight)
                return;

            bool orientationChanged = _isLandscape != (width > height);
            _isLandscape = width > height;
            _lastWidth = width;
            _lastHeight = height;

            UpdateLayoutSizes(width, height, orientationChanged);
        }

        private void UpdateLayoutSizes(double pageWidth, double pageHeight, bool orientationChanged = false)
        {
            if (pageWidth <= 0 || pageHeight <= 0)
                return;

            try
            {
                Debug.WriteLine($"Screen: {pageWidth}x{pageHeight}, Landscape: {_isLandscape}");

                double maxItemSize;
                double margin = 15;
                double spacing = 8;

                if (_isLandscape)
                {
                    maxItemSize = (pageWidth - (margin * 2) - (spacing * 3)) / 4;
                    maxItemSize = Math.Min(maxItemSize, pageHeight * 0.4);
                }
                else
                {
                    maxItemSize = (pageWidth - (margin * 2) - (spacing * 3)) / 4;

                    if (maxItemSize < 120 && pageWidth < 600)
                    {
                        AdjustToTwoRowsLayout(pageWidth, pageHeight, margin, spacing);
                        return;
                    }
                }

                maxItemSize = Math.Max(140, Math.Min(maxItemSize, 200));

                double fontSize = maxItemSize * 0.09;
                double imageSize = maxItemSize * 0.45;
                double labelHeight = maxItemSize * 0.33;

                fontSize = Math.Max(14, Math.Min(fontSize, 18));
                imageSize = Math.Max(70, Math.Min(imageSize, 90));
                labelHeight = Math.Max(50, Math.Min(labelHeight, 70));

                ApplySizesToAllElements(maxItemSize, fontSize, imageSize, labelHeight, margin, spacing);
                AdjustTextForTwoLines(maxItemSize);

                Debug.WriteLine($"Applied sizes: Item={maxItemSize}, Font={fontSize}, Image={imageSize}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateLayoutSizes error: {ex}");
            }
        }

        private void AdjustToTwoRowsLayout(double pageWidth, double pageHeight, double margin, double spacing)
        {
            double itemSize = (pageWidth - (margin * 2) - spacing) / 2;
            itemSize = Math.Max(140, Math.Min(itemSize, 180));

            double fontSize = itemSize * 0.09;
            double imageSize = itemSize * 0.45;
            double labelHeight = itemSize * 0.33;

            fontSize = Math.Max(14, Math.Min(fontSize, 16));
            imageSize = Math.Max(70, Math.Min(imageSize, 85));
            labelHeight = Math.Max(50, Math.Min(labelHeight, 65));

            mainGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            };

            mainGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            };

            Grid.SetRow(joystickBorder, 0);
            Grid.SetColumn(joystickBorder, 0);
            Grid.SetRow(settingsBorder, 0);
            Grid.SetColumn(settingsBorder, 1);
            Grid.SetRow(resetBorder, 1);
            Grid.SetColumn(resetBorder, 0);
            Grid.SetRow(statusConnectBorder, 1);
            Grid.SetColumn(statusConnectBorder, 1);

            ApplySizesToAllElements(itemSize, fontSize, imageSize, labelHeight, margin, spacing);

            if (pageHeight < 800)
                mainScrollView.Orientation = ScrollOrientation.Vertical;
            else
                mainScrollView.Orientation = ScrollOrientation.Horizontal;
        }

        private void ApplySizesToAllElements(double itemSize, double fontSize, double imageSize, double labelHeight, double margin, double spacing)
        {
            UpdateBorder(joystickBorder, itemSize);
            UpdateBorder(settingsBorder, itemSize);
            UpdateBorder(resetBorder, itemSize);
            UpdateBorder(statusConnectBorder, itemSize);

            UpdateImage(joystickImage, imageSize);
            UpdateImage(settingsImage, imageSize);
            UpdateImage(resetImage, imageSize);
            UpdateImage(StatusIndicator, imageSize);

            UpdateLabel(joystickLabel, fontSize, labelHeight);
            UpdateLabel(settingsLabel, fontSize, labelHeight);
            UpdateLabel(resetLabel, fontSize, labelHeight);
            UpdateLabel(statusLabel, fontSize, labelHeight);

            mainGrid.Margin = margin;
            mainGrid.ColumnSpacing = spacing;
            mainGrid.RowSpacing = spacing;
        }

        private void UpdateBorder(Border border, double size)
        {
            if (border != null)
            {
                border.WidthRequest = size;
                border.HeightRequest = size;
                var cornerRadius = Math.Max(15, size * 0.1);
                border.StrokeShape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(cornerRadius)
                };
            }
        }

        private void UpdateImage(Image image, double size)
        {
            if (image != null)
            {
                image.HeightRequest = size;
                var imageMargin = size * 0.25;
                image.Margin = new Thickness(imageMargin, imageMargin, imageMargin, imageMargin * 0.5);
            }
        }

        private void UpdateLabel(Label label, double fontSize, double height)
        {
            if (label != null)
            {
                label.FontSize = fontSize;
                label.HeightRequest = height;
                var horizontalMargin = fontSize;
                var verticalMargin = fontSize * 0.5;
                label.Margin = new Thickness(horizontalMargin, verticalMargin, horizontalMargin, verticalMargin * 2);
                label.LineBreakMode = LineBreakMode.WordWrap;
                label.MaxLines = 2;
                label.HorizontalTextAlignment = TextAlignment.Center;
            }
        }

        private void AdjustTextForTwoLines(double elementWidth)
        {
            var labels = new[] { joystickLabel, settingsLabel, resetLabel, statusLabel };
            var texts = new[]
            {
                "Джойстик",
                "Настройки\nподключения",
                "Управление",
                "Нет подключения"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                {
                    if (elementWidth < 150)
                    {
                        if (i == 1)
                            labels[i].Text = "Настройки";
                        else if (i == 3)
                            labels[i].Text = elementWidth < 130 ? "Подключить" : "Нет подключения";
                    }
                    else
                    {
                        labels[i].Text = texts[i];
                    }
                }
            }
        }

        private async void ConnectionStatusButton(object sender, EventArgs e)
        {
            try
            {
                if (statusConnectBorder != null)
                {
                    await statusConnectBorder.ScaleTo(0.95, 100);
                    await statusConnectBorder.ScaleTo(1.0, 100);
                }

                bool isConnected = await _connectionVm.ConnectAsync();
                StatusIndicator.Source = isConnected ? "green_light.svg" : "red_light.svg";
                statusLabel.Text = isConnected ? "Подключено" : "Нет подключения";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConnectionStatusButton error: {ex}");
                StatusIndicator.Source = "red_light.svg";
                statusLabel.Text = "Нет подключения";
            }
        }

        private async void JoystickButton(object sender, EventArgs e)
        {
            await AnimateElement(joystickBorder);
            await Navigation.PushAsync(new JoystickPageWithVideo());
        }

        private async void SettingButton(object sender, EventArgs e)
        {
            await AnimateElement(settingsBorder);
            await Navigation.PushAsync(new SettingsPage());
        }

        private async void ResetButton(object sender, EventArgs e)
        {
            await AnimateElement(resetBorder);
            await Navigation.PushAsync(new LaunchPage());
        }

        private async Task AnimateElement(Border element)
        {
            if (element != null)
            {
                await element.ScaleTo(0.95, 100);
                await element.ScaleTo(1.0, 100);
            }
        }
    }
}
