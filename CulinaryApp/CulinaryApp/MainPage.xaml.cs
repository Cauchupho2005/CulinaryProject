using CulinaryApp.Models;
using CulinaryApp.Services;
using Mapsui.UI.Maui;
using Mapsui.Tiling;
using Mapsui.Projections;
using System.Diagnostics;
using Microsoft.Maui.Media;
using Microsoft.Maui.Devices.Sensors;

namespace CulinaryApp
{
    public partial class MainPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<PoiModel> _allPois = new();
        private PoiModel _selectedPoi;
        private Microsoft.Maui.Devices.Sensors.Location _myCurrentLocation;

        // ================= BIẾN QUẢN LÝ GEOFENCE & AUDIO =================
        private IDispatcherTimer _gpsTimer;
        private IDispatcherTimer _heartbeatTimer;
        private Dictionary<string, DateTime> _cooldownTracker = new();
        private string _pendingPoiId = null;
        private DateTime _stayStartTime;

        // [QUAN TRỌNG] Biến dùng để ngắt âm thanh đang phát dở
        private CancellationTokenSource _speechTokenSource;

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
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeDataAndStartTracking();
        }

        private async Task InitializeDataAndStartTracking()
        {
            var pois = await _apiService.GetAllPoisAsync();
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

        // ================= GIAI ĐOẠN 1: GPS COLLECTION =================
        private void StartRealtimeTracking()
        {
            _gpsTimer = Dispatcher.CreateTimer();
            _gpsTimer.Interval = TimeSpan.FromSeconds(5);
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

        // ================= GIAI ĐOẠN 2 & 3: ZONE DETECTION & AUDIO DECISION =================
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
                        Debug.WriteLine($"[Audio] Kích hoạt thuyết minh cho: {poi.Title}");

                        // [QUAN TRỌNG] Cập nhật thời gian hồi chiêu NGAY LẬP TỨC để tránh nhịp quét sau đè lên
                        _cooldownTracker[poiId] = DateTime.Now;
                        _pendingPoiId = null;

                        TriggerAutoNarration(poi);
                    }

                    break;
                }
            }

            if (!isInsideAnyZone && _pendingPoiId != null)
            {
                Debug.WriteLine($"[Geofence] Đã rời khỏi vùng trước 3s. Hủy đọc.");
                _pendingPoiId = null;
            }
        }

        private async void TriggerAutoNarration(PoiModel poi)
        {
            // [QUAN TRỌNG] Hủy lệnh đọc cũ trước khi đọc lệnh mới
            _speechTokenSource?.Cancel();
            _speechTokenSource = new CancellationTokenSource();
            var token = _speechTokenSource.Token;

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var vietnameseLocale = locales.FirstOrDefault(l => l.Language.Contains("vi"));

            MainThread.BeginInvokeOnMainThread(() => {
                DisplayAlert("Thông báo tự động", $"Bạn đang ở gần {poi.Title}. Đang phát thuyết minh...", "OK");
            });

            try
            {
                // Thêm cancelToken vào lệnh đọc
                await TextToSpeech.Default.SpeakAsync($"Chào mừng bạn đến với {poi.Title}. {poi.Description}", new SpeechOptions
                {
                    Volume = 1.0f,
                    Locale = vietnameseLocale
                }, cancelToken: token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Đã hủy lệnh đọc cũ để tránh phát lặp.");
            }
        }

        // ================= XỬ LÝ UI: CHỌN ĐỊA ĐIỂM, ĐÓNG BẢNG & NÚT BẤM =================
        private void OnPoiSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PoiModel poi)
            {
                _selectedPoi = poi;

                DetailTitle.Text = poi.Title;
                DetailDescription.Text = poi.Description;
                DetailDistance.Text = poi.DistanceText;

                if (!string.IsNullOrEmpty(poi.CoverImageUrl)) DetailImage.Source = poi.CoverImageUrl;

                foreach (var pin in MainMap.Pins)
                {
                    if (pin.Label == "Bạn đang ở đây") continue;

                    if (pin.Label == poi.Title)
                    {
                        pin.Color = Microsoft.Maui.Graphics.Colors.Red;
                        pin.Scale = 1.2f;
                    }
                    else
                    {
                        pin.Color = Microsoft.Maui.Graphics.Colors.DarkOrange;
                        pin.Scale = 0.8f;
                    }
                }

                DetailPanel.IsVisible = true;
                if (poi.Location?.Coordinates != null)
                    MoveMapToLocation(poi.Location.Coordinates[1], poi.Location.Coordinates[0], 1);
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

        private async void OnListenClicked(object sender, EventArgs e)
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
            else DisplayAlert("Thông báo", "Đang lấy vị trí GPS, vui lòng đợi...", "OK");
        }

        private void MoveMapToLocation(double lat, double lon, double zoomLevel)
        {
            MainThread.BeginInvokeOnMainThread(() => {
                var (x, y) = SphericalMercator.FromLonLat(lon, lat);
                MainMap.Map?.Navigator?.CenterOn(new Mapsui.MPoint(x, y));
                MainMap.Map?.Navigator?.ZoomTo(zoomLevel);
            });
        }
    }
}