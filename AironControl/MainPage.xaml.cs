using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;

namespace AironControl
{
    public partial class MainPage : ContentPage
    {
        private double _lastWidth = 0;
        private double _lastHeight = 0;
        private Connection _connection;
        private bool _isLandscape;

        public MainPage()
        {
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

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                _connection ??= new Connection();
                bool connected = await _connection.ConnectToDevice();
                StatusIndicator.Source = connected ? "green_light.svg" : "red_light.svg";

                // Обновляем текст статуса
                if (connected)
                {
                    statusLabel.Text = "Подключено";
                }
                else
                {
                    statusLabel.Text = "Нет подключения";
                }
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

                // Рассчитываем максимально возможный размер элемента
                double maxItemSize;
                double margin = 15;
                double spacing = 8;

                if (_isLandscape)
                {
                    // В ландшафте - 4 элемента в ряд
                    maxItemSize = (pageWidth - (margin * 2) - (spacing * 3)) / 4;
                    maxItemSize = Math.Min(maxItemSize, pageHeight * 0.4); // Не больше 40% высоты
                }
                else
                {
                    // В портрете - тоже 4 элемента в ряд, но проверяем помещаются ли
                    maxItemSize = (pageWidth - (margin * 2) - (spacing * 3)) / 4;

                    // Если элементы получаются слишком маленькими (< 120px), меняем на 2 ряда
                    if (maxItemSize < 120 && pageWidth < 600)
                    {
                        // Для узких экранов делаем 2x2
                        AdjustToTwoRowsLayout(pageWidth, pageHeight, margin, spacing);
                        return;
                    }
                }

                // Ограничиваем минимальный и максимальный размер
                maxItemSize = Math.Max(140, Math.Min(maxItemSize, 200));

                // Рассчитываем остальные размеры пропорционально
                double fontSize = maxItemSize * 0.09; // 9% от размера
                double imageSize = maxItemSize * 0.45; // 45% от размера
                double labelHeight = maxItemSize * 0.33; // 33% от размера

                // Минимальные значения
                fontSize = Math.Max(14, Math.Min(fontSize, 18));
                imageSize = Math.Max(70, Math.Min(imageSize, 90));
                labelHeight = Math.Max(50, Math.Min(labelHeight, 70));

                // Применяем размеры
                ApplySizesToAllElements(maxItemSize, fontSize, imageSize, labelHeight, margin, spacing);

                // Настраиваем текст
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
            // Рассчитываем размер для 2x2 layout
            double itemSize = (pageWidth - (margin * 2) - spacing) / 2;
            itemSize = Math.Max(140, Math.Min(itemSize, 180));

            // Рассчитываем остальные размеры
            double fontSize = itemSize * 0.09;
            double imageSize = itemSize * 0.45;
            double labelHeight = itemSize * 0.33;

            fontSize = Math.Max(14, Math.Min(fontSize, 16));
            imageSize = Math.Max(70, Math.Min(imageSize, 85));
            labelHeight = Math.Max(50, Math.Min(labelHeight, 65));

            // Меняем структуру Grid на 2 строки, 2 колонки
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

            // Перераспределяем элементы
            Grid.SetRow(joystickBorder, 0);
            Grid.SetColumn(joystickBorder, 0);
            Grid.SetRow(settingsBorder, 0);
            Grid.SetColumn(settingsBorder, 1);
            Grid.SetRow(resetBorder, 1);
            Grid.SetColumn(resetBorder, 0);
            Grid.SetRow(statusConnectBorder, 1);
            Grid.SetColumn(statusConnectBorder, 1);

            // Применяем размеры
            ApplySizesToAllElements(itemSize, fontSize, imageSize, labelHeight, margin, spacing);

            // Вертикальный скролл для очень маленьких экранов
            if (pageHeight < 800)
            {
                mainScrollView.Orientation = ScrollOrientation.Vertical;
            }
            else
            {
                mainScrollView.Orientation = ScrollOrientation.Horizontal;
            }
        }

        private void ApplySizesToAllElements(double itemSize, double fontSize, double imageSize, double labelHeight, double margin, double spacing)
        {
            // Обновляем Border элементы
            UpdateBorder(joystickBorder, itemSize);
            UpdateBorder(settingsBorder, itemSize);
            UpdateBorder(resetBorder, itemSize);
            UpdateBorder(statusConnectBorder, itemSize);

            // Обновляем изображения
            UpdateImage(joystickImage, imageSize);
            UpdateImage(settingsImage, imageSize);
            UpdateImage(resetImage, imageSize);
            UpdateImage(StatusIndicator, imageSize);

            // Обновляем Label
            UpdateLabel(joystickLabel, fontSize, labelHeight);
            UpdateLabel(settingsLabel, fontSize, labelHeight);
            UpdateLabel(resetLabel, fontSize, labelHeight);
            UpdateLabel(statusLabel, fontSize, labelHeight);

            // Обновляем Grid
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

                // Адаптивные скругления
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
                // Пропорциональные отступы
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

                // Пропорциональные отступы
                var horizontalMargin = fontSize;
                var verticalMargin = fontSize * 0.5;
                label.Margin = new Thickness(horizontalMargin, verticalMargin, horizontalMargin, verticalMargin * 2);

                // Гарантируем многострочный режим
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
                    // Для узких элементов сокращаем текст
                    if (elementWidth < 150)
                    {
                        if (i == 1) // Настройки подключения
                        {
                            labels[i].Text = "Настройки";
                        }
                        else if (i == 3) // Нет подключения
                        {
                            labels[i].Text = elementWidth < 130 ? "Подключить" : "Нет подключения";
                        }
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
                // Визуальная обратная связь
                if (statusConnectBorder != null)
                {
                    await statusConnectBorder.ScaleTo(0.95, 100);
                    await statusConnectBorder.ScaleTo(1.0, 100);
                }

                if (_connection == null)
                {
                    StatusIndicator.Source = "red_light.svg";
                    statusLabel.Text = "Нет подключения";
                    return;
                }

                bool isConnected = await _connection.ConnectToDevice();
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