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

            MainMap.PinClicked += OnMapPinClicked;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeDataAndStartTracking();
        }

        // ================= HÀM BỌC THÉP TỌA ĐỘ (CHỐNG CRASH) =================
        private (double Lat, double Lng) GetSafeCoordinates(double[] coordinates)
        {
            if (coordinates == null || coordinates.Length < 2) return (0, 0);

            double val0 = coordinates[0];
            double val1 = coordinates[1];

            // Nếu val0 > 90, nó chắc chắn là Kinh độ (Longitude), ta phải đảo lại
            if (val0 > 90 || val0 < -90)
            {
                return (val1, val0); // Trả về (Vĩ độ, Kinh độ)
            }

            // Ngược lại thì val0 đúng là Vĩ độ (Latitude)
            return (val0, val1);
        }
        // =====================================================================

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
                            var (lat, lng) = GetSafeCoordinates(poi.Location.Coordinates); // [ĐÃ BỌC THÉP]

                            var pin = new Mapsui.UI.Maui.Pin()
                            {
                                Label = poi.Title,
                                Position = new Mapsui.UI.Maui.Position(lat, lng),
                                Type = Mapsui.UI.Maui.PinType.Pin,
                                Color = Microsoft.Maui.Graphics.Colors.DarkOrange,
                                Scale = 0.8f
                            };
                            MainMap.Pins.Add(pin);
                        }
                    }

                    if (pois[0].Location?.Coordinates != null)
                    {
                        var (firstLat, firstLng) = GetSafeCoordinates(pois[0].Location.Coordinates); // [ĐÃ BỌC THÉP]
                        MoveMapToLocation(firstLat, firstLng, 2);
                    }
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
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                MainThread.BeginInvokeOnMainThread(async () => {
                    await DisplayAlert("Chế độ ngoại tuyến",
                        "Bạn đang xem dữ liệu đã lưu trong máy. Vui lòng kết nối mạng để cập nhật thông tin mới nhất.",
                        "Đã hiểu");
                });
            }

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
                            var (lat, lng) = GetSafeCoordinates(poi.Location.Coordinates); // [ĐÃ BỌC THÉP]

                            var pin = new Mapsui.UI.Maui.Pin()
                            {
                                Label = poi.Title,
                                Position = new Mapsui.UI.Maui.Position(lat, lng),
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
                            if (poi.Location?.Coordinates != null)
                            {
                                var (lat, lng) = GetSafeCoordinates(poi.Location.Coordinates); // [ĐÃ BỌC THÉP]
                                var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(lat, lng);
                                double distKm = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(_myCurrentLocation, poiLoc, DistanceUnits.Kilometers);
                                poi.DistanceText = $"📍 {Math.Round(distKm, 2)} km";
                            }
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
                            var (lat, lng) = GetSafeCoordinates(poi.Location.Coordinates); // [ĐÃ BỌC THÉP]
                            var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(lat, lng);
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

                var (lat, lng) = GetSafeCoordinates(poi.Location.Coordinates); // [ĐÃ BỌC THÉP]
                var poiLoc = new Microsoft.Maui.Devices.Sensors.Location(lat, lng);
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
        private void OnMapPinClicked(object sender, PinClickedEventArgs e)
        {
            if (e.Pin == null || e.Pin.Label == "Bạn đang ở đây") return;

            var clickedPoi = _allPois.FirstOrDefault(p => p.Title == e.Pin.Label);
            if (clickedPoi != null)
            {
                OpenPoiDetail(clickedPoi);
                PoiCollectionView.SelectedItem = clickedPoi;
            }

            e.Handled = true;
        }

        private void OnPoiSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PoiModel poi)
            {
                OpenPoiDetail(poi);
            }
        }

        private void OpenPoiDetail(PoiModel poi)
        {
            _selectedPoi = poi;
            DetailTitle.Text = poi.Title;
            DetailDescription.Text = poi.Description;
            DetailDistance.Text = poi.DistanceText;

            if (!string.IsNullOrEmpty(poi.CoverImageUrl))
                DetailImage.Source = poi.CoverImageUrl;

            foreach (var pin in MainMap.Pins)
            {
                if (pin.Label == "Bạn đang ở đây") continue;
                pin.Color = (pin.Label == poi.Title) ? Microsoft.Maui.Graphics.Colors.Red : Microsoft.Maui.Graphics.Colors.DarkOrange;
                pin.Scale = (pin.Label == poi.Title) ? 1.2f : 0.8f;
            }

            DetailPanel.IsVisible = true;

            if (poi.Location?.Coordinates != null)
            {
                var (lat, lng) = GetSafeCoordinates(poi.Location.Coordinates); // [ĐÃ BỌC THÉP]
                MoveMapToLocation(lat, lng, 1);
            }
        }

        private void OnCloseDetailClicked(object sender, EventArgs e)
        {
            DetailPanel.IsVisible = false;
            PoiCollectionView.SelectedItem = null;

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
                var (lat, lon) = GetSafeCoordinates(_selectedPoi.Location.Coordinates); // [ĐÃ BỌC THÉP]

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
                string qrContent = !string.IsNullOrEmpty(_selectedPoi.Id) ? _selectedPoi.Id : _selectedPoi.Title;
                QrCodeGenerator.Value = qrContent;
                QrPopupPanel.IsVisible = true;
            }
        }

        private void OnCloseQRPopupClicked(object sender, EventArgs e)
        {
            QrPopupPanel.IsVisible = false;
        }
    }
}