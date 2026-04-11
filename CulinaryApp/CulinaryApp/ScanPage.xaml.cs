using ZXing.Net.Maui;

namespace CulinaryApp;

public partial class ScanPage : ContentPage
{
    public event Action<string> OnQrCodeDetected;

    public ScanPage()
    {
        InitializeComponent();

        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false // Chỉ bắt 1 mã duy nhất, tránh loạn
        };
    }

    // Bật camera khi trang vừa hiện lên
    protected override void OnAppearing()
    {
        base.OnAppearing();
        barcodeReader.IsDetecting = true;
    }

    // Tắt camera ngay lập tức khi trang bị ẩn (tránh nóng máy, hao pin)
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        barcodeReader.IsDetecting = false;
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var first = e.Results?.FirstOrDefault();
        if (first != null)
        {
            // Ngừng quét ngay lập tức sau khi bắt được mã
            barcodeReader.IsDetecting = false;

            Dispatcher.Dispatch(async () => {
                OnQrCodeDetected?.Invoke(first.Value); // Trả kết quả về cho MainPage
                await Navigation.PopAsync(); // Tự động đóng trang quét
            });
        }
    }

    // Xử lý khi người dùng bấm nút ❌
    private async void OnCloseClicked(object sender, EventArgs e)
    {
        barcodeReader.IsDetecting = false; // Tắt camera
        await Navigation.PopAsync(); // Trở về màn hình chính
    }
}