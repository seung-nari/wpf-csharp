using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageViewer01
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "이미지 선택",
                Filter =
                    "Image Files (*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|" +
                    "JPG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                    "PNG (*.png)|*.png|" +
                    "TIFF (*.tif;*.tiff)|*.tif;*.tiff|" +
                    "All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                LoadImage(dlg.FileName);
                TxtPath.Text = dlg.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"이미지 로딩 실패\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadImage(string path)
        {
            // 파일을 읽는 동안 잠금 문제를 피하려고, 메모리로 한번 복사해서 로딩
            // (나중에 메타정보/티프페이지 등에서도 안정적으로 쓰기 좋음)
            byte[] bytes = File.ReadAllBytes(path);

            using var ms = new MemoryStream(bytes);
            ms.Position = 0;

            // 확장자 기준으로 처리 (TIFF는 BitmapDecoder 사용)
            string ext = Path.GetExtension(path).ToLowerInvariant();

            BitmapSource bitmap;

            if (ext == ".tif" || ext == ".tiff")
            {
                // TIFF: 디코더로 로딩 (현재 단계는 첫 페이지(첫 프레임)만 표시)
                var decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                bitmap = decoder.Frames[0];
            }
            else
            {
                // JPG/PNG: BitmapImage로 로딩
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad; // 스트림 닫아도 이미지 유지
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();

                bitmap = bi;
            }

            // Freeze하면 UI 스레드 안전 + 성능에 유리
            if (bitmap.CanFreeze) bitmap.Freeze();

            ImgMain.Source = bitmap;
        }
    }
}
