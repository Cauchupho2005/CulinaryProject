using CulinaryApp.Models;
using CulinaryApp.Services;
using Mapsui.UI.Maui;
using Mapsui.Tiling;
using Mapsui.Projections;
using System.Diagnostics;
using Microsoft.Maui.Media;
using Microsoft.Maui.Devices.Sensors;
using System.Linq;

namespace CulinaryApp
{
    public partial class MainPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<PoiModel> _allPois = new();
        private PoiModel _selectedPoi;
        private Microsoft.Maui.Devices.Sensors.Location _myCurrentLocation;

        private IDispatcherTimer _gpsTimer;
        private IDispatcherTimer _heartbeatTimer;
        private Dictionary<string, DateTime> _cooldownTracker = new();
        private string _pendingPoiId = null;
        private DateTime _stayStartTime;
        private CancellationTokenSource _speechTokenSource;

        private string _currentLang = "vi";

        public MainPage()
        {
            InitializeComponent();
            Shell.SetNavBarIsVisible(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
            _apiService = new ApiService();

            MainMap.Loaded += (s, e) => {
                try
                {
                    var map = new Mapsui.Map();
                    map.Layers.Add(OpenStreetMap.CreateTileLayer());
                    map.Widgets.Clear();
                    MainMap.Map = map;
                }
                catch (Exception ex) { Debug.WriteLine(ex.Message); }
            };

            // [MỚI THÊM] Bắt sự kiện khi người dùng nhấn vào bất kỳ Ghim nào trên bản đồ
            MainMap.PinClicked += OnMapPinClicked;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeDataAndStartTracking();
        }

        private async Task InitializeDataAndStartTracking()
        {
            var pois = await _apiService.GetAllPoisAsync(_currentLang);
            if (pois != null && pois.Count > 0)
            {
                _allPois = pois;

                MainThread.BeginInvokeOnMainThread(() => {
                    MainMap.Pins.Clear();
                    foreach (var poi in _allPois)
                    {
                        if (poi.Location?.Coordinates != null)
                        {
                            var pin = new Mapsui.UI.Maui.Pin()
                            {
                                Label = poi.Title,
                                Position = new Mapsui.UI.Maui.Position(poi.Location.Coordinates[1], poi.Location.Coordinates[0]),
                                Type = Mapsui.UI.Maui.PinType.Pin,
                                Color = Microsoft.Maui.Graphics.Colors.DarkOrange,
                                Scale = 0.8f
                            };
                            MainMap.Pins.Add(pin);
                        }
                    }
                    MoveMapToLocation(pois[0].Location.Coordinates[1], pois[0].Location.Coordinates[0], 2);
                });
            }

            StartRealtimeTracking();
        }

        // ================= XỬ LÝ MENU DROPDOWN CHỌN NGÔN NGỮ =================
        private async void OnLanguageDropdownClicked(object sender, EventArgs e)
        {
            string action = await DisplayActionSheet("Chọn ngôn ngữ thuyết minh", "Hủy", null,
                "🇻🇳 Tiếng Việt (VI)",
                "🇬🇧 English (EN)",
                "🇯🇵 日本語 (JA)",
                "🇨🇳 中文 (ZH)",
                "🇰🇷 한국어 (KO)");

            if (action == "Hủy" || string.IsNullOrEmpty(action)) return;

            string newLang = _currentLang;
            if (action.Contains("(VI)")) newLang = "vi";
            else if (action.Contains("(EN)")) newLang = "en";
            else if (action.Contains("(JA)")) newLang = "ja";
            else if (action.Contains("(ZH)")) newLang = "zh";
            else if (action.Contains("(KO)")) newLang = "ko";

            if (_currentLang != newLang)
            {
                _currentLang = newLang;
                LangSelectBtn.Text = $"🌐 {_currentLang.ToUpper()} ▼";
                DetailPanel.IsVisible = false;
                await RefreshDataWithLanguageAsync();
            }
        }

        private async Task RefreshDataWithLanguageAsync()
        {
            var pois = await _apiService.GetAllPoisAsync(_currentLang);
            if (pois != null && pois.Count > 0)
            {
                _allPois = pois;

                MainThread.BeginInvokeOnMainThread(() => {
                    var oldPins = MainMap.Pins.Where(p => p.Label != "Bạn đang ở đây").ToList();
                    foreach (var pin in oldPins) MainMap.Pins.Remove(pin);

                    foreach (var poi in _allPois)
                    {
                        if (poi.Location?.Coordinates != null)
                        {
                            var pin = new Mapsui.UI.Maui.Pin()
                            {
                                Label = poi.Title,
                                Position = new Mapsui.UI.Maui.Position(poi.Location.Coordinates[1], poi.Location.Coordinates[0]),
                                Type = Mapsui.UI.Maui.PinType.Pin,
                                Color = Microsoft.Maui.Graphics.Colors.DarkOrange,
                                Scale = 0.8f
                            };
                            MainMap.Pins.Add(pin);
                        }
                    }

                    if (_myCurrentLocation != null)
                    {
                        foreach (var poi in _allPois)
                        {
                            var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(poi.Location.Coordinates[1], poi.Location.Coordinates[0]);
                            double distKm = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(_myCurrentLocation, poiLoc, DistanceUnits.Kilometers);
                            poi.DistanceText = $"📍 {Math.Round(distKm, 2)} km";
                        }
                        _allPois = _allPois.OrderBy(p => p.DistanceText).ToList();
                    }

                    PoiCollectionView.ItemsSource = null;
                    PoiCollectionView.ItemsSource = _allPois;
                });
            }
        }

        // ================= XỬ LÝ GPS =================
        private void StartRealtimeTracking()
        {
            _gpsTimer = Dispatcher.CreateTimer();
            _gpsTimer.Interval = TimeSpan.FromSeconds(10);
            _gpsTimer.Tick += async (s, e) => await FetchGpsAndUpdateMap();
            _gpsTimer.Start();

            _ = FetchGpsAndUpdateMap();

            _heartbeatTimer = Dispatcher.CreateTimer();
            _heartbeatTimer.Interval = TimeSpan.FromSeconds(1);
            _heartbeatTimer.Tick += (s, e) => CheckGeofencesAndAudio();
            _heartbeatTimer.Start();
        }

        private async Task FetchGpsAndUpdateMap()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status == PermissionStatus.Granted)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3));
                    _myCurrentLocation = await Geolocation.Default.GetLocationAsync(request);
                }
            }
            catch (Exception)
            {
                _myCurrentLocation = new Microsoft.Maui.Devices.Sensors.Location(10.7580, 106.7011);
            }

            if (_myCurrentLocation != null)
            {
                MainThread.BeginInvokeOnMainThread(() => {
                    var oldUserPin = MainMap.Pins.FirstOrDefault(p => p.Label == "Bạn đang ở đây");
                    if (oldUserPin != null) MainMap.Pins.Remove(oldUserPin);

                    var userPin = new Mapsui.UI.Maui.Pin()
                    {
                        Label = "Bạn đang ở đây",
                        Position = new Mapsui.UI.Maui.Position(_myCurrentLocation.Latitude, _myCurrentLocation.Longitude),
                        Type = Mapsui.UI.Maui.PinType.Pin,
                        Color = Microsoft.Maui.Graphics.Colors.Blue,
                        Scale = 1.0f
                    };
                    MainMap.Pins.Add(userPin);

                    foreach (var poi in _allPois)
                    {
                        if (poi.Location?.Coordinates != null)
                        {
                            var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(poi.Location.Coordinates[1], poi.Location.Coordinates[0]);
                            double distKm = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(_myCurrentLocation, poiLoc, DistanceUnits.Kilometers);
                            poi.DistanceText = $"📍 {Math.Round(distKm, 2)} km";
                        }
                    }
                    _allPois = _allPois.OrderBy(p => p.DistanceText).ToList();
                    PoiCollectionView.ItemsSource = null;
                    PoiCollectionView.ItemsSource = _allPois;
                });
            }
        }

        // ================= ZONE DETECTION & AUDIO TTS =================
        private void CheckGeofencesAndAudio()
        {
            if (_myCurrentLocation == null || _allPois == null || _allPois.Count == 0) return;

            bool isInsideAnyZone = false;

            foreach (var poi in _allPois)
            {
                if (poi.Location?.Coordinates == null) continue;
                string poiId = poi.Title;

                var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(poi.Location.Coordinates[1], poi.Location.Coordinates[0]);
                double distanceMeters = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(_myCurrentLocation, poiLoc, DistanceUnits.Kilometers) * 1000;

                if (distanceMeters <= 30)
                {
                    isInsideAnyZone = true;

                    if (_cooldownTracker.ContainsKey(poiId) && (DateTime.Now - _cooldownTracker[poiId]).TotalMinutes < 5)
                        continue;

                    if (_pendingPoiId != poiId)
                    {
                        _pendingPoiId = poiId;
                        _stayStartTime = DateTime.Now;
                        Debug.WriteLine($"[Geofence] Đã bước vào {poi.Title}. Bắt đầu đếm 3s...");
                    }
                    else if ((DateTime.Now - _stayStartTime).TotalSeconds >= 3)
                    {
                        _cooldownTracker[poiId] = DateTime.Now;
                        _pendingPoiId = null;
                        TriggerAutoNarration(poi);
                    }
                    break;
                }
            }

            if (!isInsideAnyZone && _pendingPoiId != null)
            {
                _pendingPoiId = null;
            }
        }

        private async void TriggerAutoNarration(PoiModel poi)
        {
            _speechTokenSource?.Cancel();
            _speechTokenSource = new CancellationTokenSource();
            var token = _speechTokenSource.Token;

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var targetLocale = locales.FirstOrDefault(l => l.Language.ToLower().Contains(_currentLang.ToLower()));

            if (targetLocale == null)
            {
                MainThread.BeginInvokeOnMainThread(() => {
                    DisplayAlert("Thiếu gói Giọng Đọc",
                        $"Điện thoại chưa cài đặt bộ đọc cho ngôn ngữ '{_currentLang.ToUpper()}'. Vui lòng tải gói ngôn ngữ này trong phần Cài Đặt của điện thoại.",
                        "Đã hiểu");
                });
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => {
                DisplayAlert("Thông báo tự động", $"Đang phát thuyết minh...", "OK");
            });

            try
            {
                await TextToSpeech.Default.SpeakAsync($"{poi.Title}. {poi.Description}", new SpeechOptions
                {
                    Volume = 1.0f,
                    Locale = targetLocale
                }, cancelToken: token);
            }
            catch (OperationCanceledException) { }
        }

        // ================= XỬ LÝ SỰ KIỆN QUÉT QR =================
        private async void OnScanQRClicked(object sender, EventArgs e)
        {
            var scanPage = new ScanPage();
            scanPage.OnQrCodeDetected += (qrValue) => {
                ProcessQrResult(qrValue);
            };
            await Navigation.PushAsync(scanPage);
        }

        private void ProcessQrResult(string qrValue)
        {
            var matchedPoi = _allPois.FirstOrDefault(p =>
                p.Title.Equals(qrValue, StringComparison.OrdinalIgnoreCase) ||
                p.Id == qrValue);

            if (matchedPoi != null)
            {
                // [ĐÃ SỬA LỖI CS1729]: Gọi trực tiếp hàm dùng chung thay vì giả lập sự kiện
                OpenPoiDetail(matchedPoi);
                TriggerAutoNarration(matchedPoi);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(async () => {
                    await DisplayAlert("Thông báo", "Không tìm thấy thông tin cho mã QR này.", "OK");
                });
            }
        }

        // ================= XỬ LÝ SỰ KIỆN UI CHUNG =================

        // [MỚI] Bắt sự kiện người dùng Click thẳng vào cái Ghim trên bản đồ
        private void OnMapPinClicked(object sender, PinClickedEventArgs e)
        {
            // Bỏ qua nếu bấm nhầm vào chỗ trống hoặc ghim GPS của người dùng
            if (e.Pin == null || e.Pin.Label == "Bạn đang ở đây") return;

            // Lấy Tên quán ăn từ Label của Ghim để tìm trong danh sách Data
            var clickedPoi = _allPois.FirstOrDefault(p => p.Title == e.Pin.Label);
            if (clickedPoi != null)
            {
                // Gọi hàm dùng chung để hiển thị chi tiết và đổi màu
                OpenPoiDetail(clickedPoi);

                // Đồng bộ hóa: Tự động highlight món đó trong danh sách CollectionView bên dưới
                PoiCollectionView.SelectedItem = clickedPoi;
            }

            // Chặn cái popup mặc định của bản đồ Mapsui hiện lên (vì mình đã làm Bảng chi tiết UI xịn hơn rồi)
            e.Handled = true;
        }

        // Khi người dùng bấm vào danh sách bên dưới
        private void OnPoiSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PoiModel poi)
            {
                // [ĐÃ SỬA]: Rút gọn lại, chỉ cần gọi hàm dùng chung
                OpenPoiDetail(poi);
            }
        }

        // HÀM DÙNG CHUNG: Hiển thị bảng chi tiết, đổi màu ghim, zoom bản đồ
        private void OpenPoiDetail(PoiModel poi)
        {
            _selectedPoi = poi;
            DetailTitle.Text = poi.Title;
            DetailDescription.Text = poi.Description;
            DetailDistance.Text = poi.DistanceText;

            if (!string.IsNullOrEmpty(poi.CoverImageUrl))
                DetailImage.Source = poi.CoverImageUrl;

            // Đổi màu ghim trên bản đồ (Quán được chọn màu Đỏ to lên, quán khác màu Cam nhỏ lại)
            foreach (var pin in MainMap.Pins)
            {
                if (pin.Label == "Bạn đang ở đây") continue;
                pin.Color = (pin.Label == poi.Title) ? Microsoft.Maui.Graphics.Colors.Red : Microsoft.Maui.Graphics.Colors.DarkOrange;
                pin.Scale = (pin.Label == poi.Title) ? 1.2f : 0.8f;
            }

            // Hiện bảng UI
            DetailPanel.IsVisible = true;

            // Zoom nhẹ bản đồ tới vị trí đó
            if (poi.Location?.Coordinates != null)
                MoveMapToLocation(poi.Location.Coordinates[1], poi.Location.Coordinates[0], 1);
        }

        private void OnCloseDetailClicked(object sender, EventArgs e)
        {
            DetailPanel.IsVisible = false;
            PoiCollectionView.SelectedItem = null;

            // Khôi phục lại màu ghim gốc
            foreach (var pin in MainMap.Pins)
            {
                if (pin.Label == "Bạn đang ở đây") continue;
                pin.Color = Microsoft.Maui.Graphics.Colors.DarkOrange;
                pin.Scale = 0.8f;
            }

            if (_myCurrentLocation != null) MoveMapToLocation(_myCurrentLocation.Latitude, _myCurrentLocation.Longitude, 3);
        }

        private void OnListenClicked(object sender, EventArgs e)
        {
            if (_selectedPoi != null) TriggerAutoNarration(_selectedPoi);
        }

        private async void OnRouteClicked(object sender, EventArgs e)
        {
            if (_selectedPoi?.Location?.Coordinates != null)
            {
                double lat = _selectedPoi.Location.Coordinates[1];
                double lon = _selectedPoi.Location.Coordinates[0];

                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    string googleMapsUrl = $"https://www.google.com/maps/dir/?api=1&destination={lat},{lon}";
                    await Microsoft.Maui.ApplicationModel.Launcher.OpenAsync(googleMapsUrl);
                }
                else
                {
                    var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(lat, lon);
                    var options = new MapLaunchOptions { Name = _selectedPoi.Title, NavigationMode = NavigationMode.Driving };
                    await Microsoft.Maui.ApplicationModel.Map.OpenAsync(poiLoc, options);
                }
            }
        }

        private void OnFindMyLocationClicked(object sender, EventArgs e)
        {
            if (_myCurrentLocation != null) MoveMapToLocation(_myCurrentLocation.Latitude, _myCurrentLocation.Longitude, 1);
            else DisplayAlert("Thông báo", "Đang lấy vị trí GPS...", "OK");
        }

        private void MoveMapToLocation(double lat, double lon, double zoomLevel)
        {
            MainThread.BeginInvokeOnMainThread(() => {
                var (x, y) = SphericalMercator.FromLonLat(lon, lat);
                MainMap.Map?.Navigator?.CenterOn(new Mapsui.MPoint(x, y));
                MainMap.Map?.Navigator?.ZoomTo(zoomLevel);
            });
        }

        // ================= XỬ LÝ TẠO MÃ QR TẠI CHỖ =================
        private void OnGenerateQRClicked(object sender, EventArgs e)
        {
            if (_selectedPoi != null)
            {
                // Ưu tiên dùng Id (nếu kết nối DB), nếu không có DB thì dùng Title làm mã định danh
                string qrContent = !string.IsNullOrEmpty(_selectedPoi.Id) ? _selectedPoi.Id : _selectedPoi.Title;

                // Nạp nội dung vào bộ sinh mã QR
                QrCodeGenerator.Value = qrContent;

                // Hiển thị khung Popup lên
                QrPopupPanel.IsVisible = true;
            }
        }

        private void OnCloseQRPopupClicked(object sender, EventArgs e)
        {
            // Ẩn khung Popup đi
            QrPopupPanel.IsVisible = false;
        }

    }
}