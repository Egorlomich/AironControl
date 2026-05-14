using AironControl.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using System.Diagnostics;
using System.Net.Sockets;

namespace AironControl
{
    public partial class JoystickPageWithVideo : ContentPage
    {
        private double _screenWidth;
        private double _screenHeight;
        private bool _isLandscape;

        private const double MIN_SCREEN_WIDTH = 320;
        private const double MAX_SCREEN_WIDTH = 2000;
        private const double MIN_SCREEN_HEIGHT = 480;
        private const double STUCK_TIMEOUT_MS = 180; 
        private const int MIN_SEND_INTERVAL_MS = 60; 
        private const int INTERACTION_TIMEOUT_MS = 400;
        private PointF _center;
        private PointF _knobPosition; 
        private PointF _startKnobPosition;
        private const float BaseRadius = 100f;
        private const float KnobRadius = 40f;
        private JoystickVM _vm;
        private double _currentX = 0, _currentY = 0;
        private DateTime _lastSentTime = DateTime.MinValue;
        private bool _leftActive = false;
        private bool _rightActive = false;

        private SKBitmap _videoFrame;
        private UdpClient _udpClient;
        private bool _receiving = true; 
        private bool _forceZeroSend = false;

        private DateTime _lastInputTime = DateTime.MinValue; 
        private const int AUTO_RESET_MS = 350;

        private bool _isActive;

        private DateTime _lastInvalidate = DateTime.MinValue;
        private const int INVALIDATE_THROTTLE_MS = 33;

        private const double DEFAULT_ASPECT_RATIO = 16.0 / 9.0; // Значение по умолчанию

        private SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private (double x, double y, int rotate) _lastSentData;

        private SKPaint _videoPlaceholderPaint;
        private SKPaint _videoTextPaint;

        public JoystickPageWithVideo()
        {
            InitializeComponent();
            SetupJoystick();

            InitializePaints();

            _center = new PointF(125, 125);

            SetupButtonEvents();
            InitializeModeList();
            InitializeViewList();
            MainGrid.SizeChanged += OnMainGridSizeChanged;
            _vm = IPlatformApplication.Current!.Services.GetRequiredService<JoystickVM>();
            BindingContext = _vm;
        }

        private async void HeartbeatTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;

            if ((now - _lastSentTime).TotalMilliseconds < 60) return;

            // _isActive ставится в true в Started/Running, в false в Completed/Canceled
            // Аварийный сброс: если _isActive завис (gesture потерялся без Completed события)
            if (_isActive && (now - _lastInputTime).TotalMilliseconds > 2000)
            {
                _isActive = false;
                _currentX = _currentY = 0;
                _knobPosition = _center;
                JoystickGraphics.Invalidate();
                Debug.WriteLine("[HB] Аварийный сброс — gesture потерян");
            }

            double sendX = _isActive ? _currentX : 0;
            double sendY = _isActive ? _currentY : 0;
            int rotate = GetCurrentRotate();

            await SendDataBackgroundAsync(sendX, sendY, rotate);
            _lastSentTime = now;
        }
        private void SetupButtonEvents()
        {
            // ЛЕВАЯ кнопка - включение при нажатии, выключение при отпускании
            LeftButton.Pressed += (s, e) =>
            {
                Debug.WriteLine("LEFT PRESSED");
                _lastInputTime = DateTime.Now;
                _leftActive = true;
                _rightActive = false;
                UpdateButtonColors();
                _ = SendDataBackgroundAsync(_currentX, _currentY, GetCurrentRotate());
            };

            LeftButton.Released += async (s, e) =>
            {
                Debug.WriteLine("LEFT RELEASED");
                _leftActive = false;
                UpdateButtonColors();
                _ = SendDataBackgroundAsync(_currentX, _currentY, GetCurrentRotate());
            };

            // ПРАВАЯ кнопка - включение при нажатии, выключение при отпускании
            RightButton.Pressed += async (s, e) =>
            {
                Debug.WriteLine("RIGHT PRESSED");
                _lastInputTime = DateTime.Now;
                _rightActive = true;
                _leftActive = false; // Выключаем левую кнопку
                UpdateButtonColors();
                _ = SendDataBackgroundAsync(_currentX, _currentY, GetCurrentRotate());
            };

            RightButton.Released += async (s, e) =>
            {
                Debug.WriteLine("RIGHT RELEASED");
                _rightActive = false;
                UpdateButtonColors();
                _ = SendDataBackgroundAsync(_currentX, _currentY, GetCurrentRotate());
            };

            // STOP кнопка - сброс всего при нажатии, отпускании ничего не меняем
            StopButton.Pressed += async (s, e) =>
            {
                Debug.WriteLine("STOP PRESSED");
                _lastInputTime = DateTime.Now;
                StopButton.BackgroundColor = Colors.Red; // Визуальная обратная связь

                // Немедленный сброс
                _leftActive = false;
                _rightActive = false;
                UpdateButtonColors();
                await ResetJoystickAsync();
            };

            StopButton.Released += (s, e) =>
            {
                Debug.WriteLine("STOP RELEASED");
                StopButton.BackgroundColor = Color.FromArgb("#E0E0E0"); // Возвращаем цвет
            };
        }
       
        private void UpdateButtonColors()
        {
            // Обновляем цвета всех кнопок
            LeftButton.BackgroundColor = _leftActive ? Colors.Blue : Color.FromArgb("#E0E0E0");
            RightButton.BackgroundColor = _rightActive ? Colors.Blue : Color.FromArgb("#E0E0E0");
        }
        private void InitializePaints()
        {
            _videoPlaceholderPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill
            };

            _videoTextPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 24,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
        }
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (_videoFrame != null)
            {
                // Используем реальные пропорции кадра
                double frameAspect = (double)_videoFrame.Width / _videoFrame.Height;
                UpdateVideoLayout(width, height, frameAspect);
            }
            else
            {
                // Используем значение по умолчанию
                UpdateVideoLayout(width, height, DEFAULT_ASPECT_RATIO);
            }


        }
        private void UpdateVideoLayout(double width, double height, double aspectRatio)
        {
            // Рассчитываем размеры с сохранением пропорций
            double containerWidth = width * 0.6; // 60% ширины экрана
            double containerHeight = height * 0.8; // 80% высоты экрана

            // Рассчитываем размер видео с сохранением пропорций
            double videoWidth = containerWidth;
            double videoHeight = videoWidth / aspectRatio;

            // Если видео выше контейнера, пересчитываем
            if (videoHeight > containerHeight)
            {
                videoHeight = containerHeight;
                videoWidth = videoHeight * aspectRatio;
            }

        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            _ = Task.Run(StartReceivingVideo);
            _ = Task.Run(async () => await _vm.ConnectAsync());
            UpdateLayoutForScreenSize();
            Dispatcher.StartTimer(TimeSpan.FromMilliseconds(80), () =>
            {
                HeartbeatTimer_Tick(null, EventArgs.Empty);

                return true; 
            });
        }   
        private void OnMainGridSizeChanged(object sender, EventArgs e)
        {
            if (MainGrid.Width <= 0 || MainGrid.Height <= 0)
                return;

            if (Math.Abs(_screenWidth - MainGrid.Width) > 1 ||
                Math.Abs(_screenHeight - MainGrid.Height) > 1)
            {
                _screenWidth = MainGrid.Width;
                _screenHeight = MainGrid.Height;
                _isLandscape = _screenWidth > _screenHeight;

                Debug.WriteLine($"Screen size changed: {_screenWidth}x{_screenHeight}, Landscape: {_isLandscape}");
                UpdateLayoutForScreenSize();
            }
        }
        private void UpdateLayoutForScreenSize()
        {
            try
            {
                if (_screenWidth <= MIN_SCREEN_WIDTH || _screenHeight <= MIN_SCREEN_HEIGHT)
                    return;

                // 1. Определяем базовый масштаб (нормализуем к ширине 375 - iPhone 8/SE)
                var baseWidth = 375.0;
                var scaleFactor = _screenWidth / baseWidth;
                
                // Ограничиваем масштаб
                scaleFactor = Math.Max(0.7, Math.Min(scaleFactor, 1.8));

                // 2. Для ландшафтного режима используем другой подход
                if (_isLandscape)
                {
                    scaleFactor = Math.Min(_screenHeight / 375.0, scaleFactor);
                }

                Debug.WriteLine($"Scale factor: {scaleFactor:F2}");

                // 3. Обновляем размер джойстика (процент от меньшей стороны экрана)
                var minSide = Math.Min(_screenWidth, _screenHeight);
                var joystickSize = minSide * 0.25; // 25% от меньшей стороны
                joystickSize = Math.Max(150, Math.Min(joystickSize, 300));

                JoystickContainer.WidthRequest = joystickSize;
                JoystickContainer.HeightRequest = joystickSize;
                JoystickContainer.Margin = new Thickness(0, 0, _screenWidth * 0.05, 0);

                // 4. Обновляем размер кнопок
                var buttonSize = 70 * scaleFactor;
                buttonSize = Math.Max(50, Math.Min(buttonSize, 100));

                LeftButton.WidthRequest = buttonSize;
                LeftButton.HeightRequest = buttonSize;
                LeftButton.CornerRadius = (int)(buttonSize / 2);

                StopButton.WidthRequest = buttonSize;
                StopButton.HeightRequest = buttonSize;
                StopButton.CornerRadius = (int)(buttonSize / 2);

                RightButton.WidthRequest = buttonSize;
                RightButton.HeightRequest = buttonSize;
                RightButton.CornerRadius = (int)(buttonSize / 2);

                // 5. Обновляем расстояние между кнопками
                var buttonSpacing = 30 * scaleFactor;
                buttonSpacing = Math.Max(20, Math.Min(buttonSpacing, 50));
                ButtonsLayout.Spacing = buttonSpacing;

                // 6. Обновляем высоту контейнера кнопок
                var buttonsContainerHeight = 100 * scaleFactor;
                buttonsContainerHeight = Math.Max(80, Math.Min(buttonsContainerHeight, 150));
                ButtonsContainer.HeightRequest = buttonsContainerHeight;

                // 7. Обновляем отступы
                var bottomPadding = 30 * scaleFactor;
                bottomPadding = Math.Max(20, Math.Min(bottomPadding, 50));
                var leftPadding = 15 * scaleFactor;
                leftPadding = Math.Max(10, Math.Min(leftPadding, 30));

                ButtonsContainer.Padding = new Thickness(leftPadding, 0, 0, bottomPadding);

                // 8. Обновляем размер индикатора
                var indicatorSize = 40 * scaleFactor;
                indicatorSize = Math.Max(30, Math.Min(indicatorSize, 60));
                StatusIndicator.WidthRequest = indicatorSize;
                StatusIndicator.HeightRequest = indicatorSize;

                // 9. Обновляем шрифты
                var baseFontSize = 16.0;
                var fontSize = baseFontSize * scaleFactor;
                fontSize = Math.Max(12, Math.Min(fontSize, 24));

                CoordinatesLabel.FontSize = fontSize;
                JoystickCoordinatesLabel.FontSize = fontSize * 0.875; // 14px при 16px основном

                // 10. Обновляем отступы текста
                var textPaddingH = 16 * scaleFactor;
                var textPaddingV = 10 * scaleFactor;
                textPaddingH = Math.Max(12, Math.Min(textPaddingH, 24));
                textPaddingV = Math.Max(8, Math.Min(textPaddingV, 16));

                CoordinatesLabel.Padding = new Thickness(textPaddingH, textPaddingV);
                JoystickCoordinatesLabel.Padding = new Thickness(textPaddingH * 0.625, textPaddingV * 0.5);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateLayoutForScreenSize: {ex.Message}");
            }
        }


      
        protected override async void OnDisappearing()
        {
            _receiving = false;

            await ResetJoystickAsync();
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch 
            {
            }

            _videoFrame?.Dispose();
            _videoPlaceholderPaint?.Dispose();
            _videoTextPaint?.Dispose();
            _sendSemaphore?.Dispose();
            base.OnDisappearing();

            MainGrid.SizeChanged -= OnMainGridSizeChanged;

        }
        private int GetCurrentRotate()
        {
            if (_leftActive && !_rightActive) return -1;
            if (_rightActive && !_leftActive) return 1;
            return 0;
        }
        private async Task StartReceivingVideo()
        {
            try
            {
                _udpClient = new UdpClient(5005);

                while (_receiving)
                {
                    try
                    {
                        // Используем ReceiveAsync без блокировки
                        var result = await _udpClient.ReceiveAsync();

                        // Обработка в отдельной задаче
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            try
                            {
                                using var stream = new SKMemoryStream(result.Buffer);
                                var decoded = SKBitmap.Decode(stream);

                                if (decoded != null)
                                {
                                    // Атомарная замена кадра
                                    var oldFrame = _videoFrame;
                                    _videoFrame = decoded;
                                    oldFrame?.Dispose();

                                    // Ограничиваем частоту обновления (макс 25 FPS)
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        VideoView.InvalidateSurface();
                                    });
                                }
                            }
                            catch
                            {
                                // Игнорируем ошибки декодирования
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Video receive error: {ex.Message}");
                        await Task.Delay(100); // Пауза при ошибках
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Video setup error: {ex.Message}");
            }
            finally
            {
                _udpClient?.Close();
            }
        }
        private Border _modeDropdown;
        private Border _viewDropdown;

        private void InitializeModeList()
        {
            var modeList = new VerticalStackLayout { Spacing = 0 };

            for (int i = 0; i <= 8; i++)
            {
                int mode = i;
                var btn = new Button
                {
                    Text = $"Mode {mode}",
                    TextColor = Colors.White,
                    BackgroundColor = Colors.Transparent,
                    FontSize = 12,
                    HeightRequest = 30,
                    Padding = new Thickness(4, 0)
                };
                btn.Clicked += async (s, e) =>
                {
                    _modeDropdown.IsVisible = false;
                    ModeButton.Text = $"{mode} ▼";
                    await _vm.ChangeModeAsync(mode);
                };
                modeList.Add(btn);
            }

            _modeDropdown = new Border
            {
                IsVisible = false,
                StrokeThickness = 0.5,
                Stroke = new SolidColorBrush(Color.FromArgb("#666666")),
                BackgroundColor = Color.FromArgb("#EE000000"),
                Margin = new Thickness(0, 2, 0, 0),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) },
                Content = modeList
            };

            ModeContainer.Add(_modeDropdown);
        }

        private void OnModeButtonClicked(object sender, EventArgs e)
        {
            _viewDropdown.IsVisible = false;
            _modeDropdown.IsVisible = !_modeDropdown.IsVisible;
        }

        private void InitializeViewList()
        {
            var items = new[]
            {
                "Карта местности",
                "Текущая обстановка",
                "Видеопоток"
            };

            var list = new VerticalStackLayout { Spacing = 0 };

            foreach (var item in items)
            {
                var label = item;
                var btn = new Button
                {
                    Text = label,
                    TextColor = Colors.White,
                    BackgroundColor = Colors.Transparent,
                    FontSize = 11,
                    HeightRequest = 35,
                    Padding = new Thickness(6, 0)
                };
                btn.Clicked += (s, e) =>
                {
                    _viewDropdown.IsVisible = false;
                    ViewButton.Text = label.Length > 10 ? label[..10] + "… ▼" : label + " ▼";
                    OnViewSelected(label);
                };
                list.Add(btn);
            }

            _viewDropdown = new Border
            {
                IsVisible = false,
                StrokeThickness = 0.5,
                Stroke = new SolidColorBrush(Color.FromArgb("#666666")),
                BackgroundColor = Color.FromArgb("#EE000000"),
                Margin = new Thickness(0, 2, 0, 0),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) },
                Content = list
            };

            ViewContainer.Add(_viewDropdown);
        }

        private void OnViewButtonClicked(object sender, EventArgs e)
        {
            _modeDropdown.IsVisible = false;
            _viewDropdown.IsVisible = !_viewDropdown.IsVisible;
        }

        private void OnViewSelected(string view)
        {
            Debug.WriteLine($"[View] Selected: {view}");
            // TODO: переключить отображаемый слой
        }
        private void OnVideoPaint(object sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.Black);
            if (_videoFrame != null)
            {
                try
                {
                    // Быстрое масштабирование без сложных расчетов
                    float scale = Math.Min(
                        e.Info.Width / (float)_videoFrame.Width,
                        e.Info.Height / (float)_videoFrame.Height
                    );

                    float width = _videoFrame.Width * scale;
                    float height = _videoFrame.Height * scale;
                    float x = (e.Info.Width - width) / 2;
                    float y = (e.Info.Height - height) / 2;

                    var destRect = new SKRect(x, y, x + width, y + height);
                    canvas.DrawBitmap(_videoFrame, destRect);
                }
                catch
                {
                    canvas.DrawRect(0, 0, e.Info.Width, e.Info.Height, _videoPlaceholderPaint);
                    canvas.DrawText("Ошибка видео",
                        e.Info.Width / 2,
                        e.Info.Height / 2,
                        _videoTextPaint);
                }
            }
            else
            {
                canvas.DrawRect(0, 0, e.Info.Width, e.Info.Height, _videoPlaceholderPaint);
                canvas.DrawText("Ожидание видео...",
                    e.Info.Width / 2,
                    e.Info.Height / 2,
                    _videoTextPaint);
            }
        }
      private void SetupJoystick()
        {
            JoystickGraphics.Drawable = new JoystickDrawable(
            () => _center,
            () => _knobPosition,
            () => _isActive);
        }
      
        // Джойстик
        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            try
            {
                switch (e.StatusType)
                {
                    case GestureStatus.Started:
                        _isActive = true;
                        _lastInputTime = DateTime.Now;
                        break;

                    case GestureStatus.Running:
                        _isActive = true;
                        _lastInputTime = DateTime.Now;
                        UpdateKnobPosition(e.TotalX, e.TotalY);
                        break;

                    case GestureStatus.Completed:
                    case GestureStatus.Canceled:
                        ResetToCenterImmediately();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPanUpdated error: {ex.Message}");
                ResetToCenterImmediately();
            }
        }

        private async void OnTapped(object sender, EventArgs e) => await ResetJoystickAsync();

        private async Task ResetJoystickAsync()
        {
            _isActive = false;
            _knobPosition = _center;
            _currentX = 0;
            _currentY = 0;
            CoordinatesLabel.Text = "X: 0.00, Y: 0.00";
            JoystickGraphics.Invalidate();

            await SendDataBackgroundAsync(0, 0, 0);
        }

        private void UpdateKnobPosition(double totalX, double totalY)
        {
            float dx = (float)totalX;
            float dy = (float)totalY;

            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float maxDist = BaseRadius - KnobRadius;

            if (distance > maxDist)
            {
                float scale = maxDist / distance;
                dx *= scale;
                dy *= scale;
            }

            _knobPosition = new PointF(_center.X + dx, _center.Y + dy);

            double newX = dx / maxDist;
            double newY = -dy / maxDist;

            if (Math.Abs(newX) < 0.13) newX = 0;   // deadzone ↑
            if (Math.Abs(newY) < 0.13) newY = 0;


            _currentX = newX;
            _currentY = newY;

            CoordinatesLabel.Text = $"X: {_currentX:F2}, Y: {_currentY:F2}";
            JoystickCoordinatesLabel.Text = CoordinatesLabel.Text;

            // Throttle Invalidate
            if ((DateTime.Now - _lastInvalidate).TotalMilliseconds > INVALIDATE_THROTTLE_MS)
            {
                JoystickGraphics.Invalidate();
                _lastInvalidate = DateTime.Now;
            }

            _ = SendDataBackgroundAsync(_currentX, _currentY, GetCurrentRotate());
        }

        private async Task SendDataBackgroundAsync(double x, double y, int rotate)
        {
            // Быстрая проверка без захвата семафора
            bool isZero = Math.Abs(x) < 0.01 && Math.Abs(y) < 0.01;

            if (!isZero &&
                Math.Abs(x - _lastSentData.x) < 0.08 &&
                Math.Abs(y - _lastSentData.y) < 0.08 &&
                rotate == _lastSentData.rotate)
            {
                return; // только ненулевые значения дедуплицируем
            }

            if (! _sendSemaphore.Wait(0)) // Не блокируем, если занят
                return;

            try
            {
                _lastSentData = (x, y, rotate);
                _vm.SendJoystickValues(x, y, rotate);
                Debug.WriteLine($"Sent: X={x:F2}, Y={y:F2}, R={rotate}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Send error: {ex.Message}");
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
        private async void OnStatusClicked(object sender, EventArgs e)
        {
            StatusIndicator.IsEnabled = false;
            await _vm.ConnectAsync();
            StatusIndicator.IsEnabled = true;
        }

        private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
        private async void ResetToCenterImmediately()
        {
            _isActive = false;
            _currentX = _currentY = 0;
            _knobPosition = _center;

            CoordinatesLabel.Text = "X: 0.00, Y: 0.00";
            JoystickCoordinatesLabel.Text = CoordinatesLabel.Text;
            JoystickGraphics.Invalidate();

            Debug.WriteLine("[RESET] Палец отпущен → мгновенный 0,0,0");
            await SendDataBackgroundAsync(0, 0, GetCurrentRotate());
        }

        // Левые кнопки
        private void OnLeftPointerPressed(object sender, PointerEventArgs e)
        {
            Debug.WriteLine("=== LEFT PRESSED ===");
            _leftActive = true;
            LeftButton.BackgroundColor = Colors.Blue;
            SendCommand(_currentX, _currentY, GetCurrentRotate());
        }

        private void OnLeftPointerReleased(object sender, PointerEventArgs e)
        {
            Debug.WriteLine("=== LEFT RELEASED ===");
            _leftActive = false;
            LeftButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            SendCommand(_currentX, _currentY, GetCurrentRotate());
        }

        private void OnLeftPointerEntered(object sender, PointerEventArgs e)
        {
            // Опционально: визуальный feedback при наведении
            if (!_leftActive)
                LeftButton.BackgroundColor = Colors.LightBlue;
        }

        private void OnLeftPointerExited(object sender, PointerEventArgs e)
        {
            // Возвращаем цвет если не активна
            if (!_leftActive)
                LeftButton.BackgroundColor = Color.FromArgb("#E0E0E0");
        }

        // Правые кнопки (аналогично)
        private void OnRightPointerPressed(object sender, PointerEventArgs e)
        {
            Debug.WriteLine("=== RIGHT PRESSED ===");
            _rightActive = true;
            RightButton.BackgroundColor = Colors.Blue;
            SendCommand(_currentX, _currentY, GetCurrentRotate());
        }

        private void OnRightPointerReleased(object sender, PointerEventArgs e)
        {
            Debug.WriteLine("=== RIGHT RELEASED ===");
            _rightActive = false;
            RightButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            SendCommand(_currentX, _currentY, GetCurrentRotate());
        }

        private void OnRightPointerEntered(object sender, PointerEventArgs e)
        {
            if (!_rightActive)
                RightButton.BackgroundColor = Colors.LightBlue;
        }

        private void OnRightPointerExited(object sender, PointerEventArgs e)
        {
            if (!_rightActive)
                RightButton.BackgroundColor = Color.FromArgb("#E0E0E0");
        }

        private async void OnStopClicked(object sender, EventArgs e)
        {
            Debug.WriteLine($"=== StopClicked at {DateTime.Now:HH:mm:ss.fff} ===");

            _leftActive = false;
            _rightActive = false;

            // Сброс цветов кнопок
            LeftButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            RightButton.BackgroundColor = Color.FromArgb("#E0E0E0");

            await ResetJoystickAsync();
        }

        private void SendCommand(double x, double y, int rotate)
        {
            try
            {
                _vm.SendJoystickValues(x, y, rotate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Send error: {ex.Message}");
            }
        }
       
        private class JoystickDrawable : IDrawable
        {
            private readonly Func<PointF> _getCenter;
            private readonly Func<PointF> _getKnobPosition;
            private readonly Func<bool> _getIsPressed;

            private readonly Color _baseColor = Color.FromArgb("#D3D3D3");
            private readonly Color _knobColor = Colors.DarkBlue;
            private readonly Color _knobPressedColor = Colors.Blue;

            public JoystickDrawable(
                Func<PointF> getCenter,
                Func<PointF> getKnobPosition,
                Func<bool> getIsPressed)
            {
                _getCenter = getCenter;
                _getKnobPosition = getKnobPosition;
                _getIsPressed = getIsPressed;
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                var center = _getCenter();
                var knobPosition = _getKnobPosition();
                var isPressed = _getIsPressed();

                // Используем локальные переменные, а не _page
                if (center == PointF.Zero)
                    center = new PointF(dirtyRect.Width / 2, dirtyRect.Height / 2);

                if (knobPosition == PointF.Zero)
                    knobPosition = center;

                // Основание
                canvas.FillColor = _baseColor;
                canvas.FillCircle(center, BaseRadius); // Используем center, а не _page._center

                // Ручка
                canvas.FillColor = isPressed ? _knobPressedColor : _knobColor;
                canvas.FillCircle(knobPosition, KnobRadius); // Используем knobPosition, а не _page._knobPosition
            }
        }
    }
}