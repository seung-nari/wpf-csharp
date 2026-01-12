using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImageViewer02
{
    public partial class MainWindow : Window
    {
        // Pan 상태
        private bool _isPanning = false;
        private Point _panStartPoint;          // 마우스 시작점 (뷰포트 좌표)
        private double _panStartX, _panStartY; // 시작 Translate 값

        // Zoom 설정
        private const double ZoomStep = 1.15;  // 휠 한 칸 확대 배율
        private const double MinScale = 0.05;
        private const double MaxScale = 40.0;

        public MainWindow()
        {
            InitializeComponent();
            UpdateInfo();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "이미지 선택",
                Filter =
                    "Image Files (*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|" +
                    "All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                LoadImage(dlg.FileName);
                TxtPath.Text = dlg.FileName;

                ResetView(); // 새 이미지 열면 뷰 초기화
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

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void LoadImage(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);

            string ext = Path.GetExtension(path).ToLowerInvariant();

            BitmapSource bitmap;

            if (ext == ".tif" || ext == ".tiff")
            {
                var decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                bitmap = decoder.Frames[0]; // Step2에서는 첫 페이지
            }
            else
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();

                bitmap = bi;
            }

            if (bitmap.CanFreeze) bitmap.Freeze();
            ImgMain.Source = bitmap;
        }

        // -------- Zoom (Wheel) --------
        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ImgMain.Source == null) return;

            // 휠 방향에 따라 확대/축소 배율 결정
            double scale = ScaleTf.ScaleX;
            double factor = (e.Delta > 0) ? ZoomStep : (1.0 / ZoomStep);

            double newScale = scale * factor;
            newScale = Math.Clamp(newScale, MinScale, MaxScale);

            // 실제 적용될 factor 재계산 (Clamp로 인해 달라질 수 있음)
            factor = newScale / scale;

            // 커서 위치(뷰포트 좌표)
            Point mouse = e.GetPosition(Viewport);

            // 현재 Translate/Scale 기준에서, 마우스 기준 줌을 위해 이동 보정
            // 원리: (mouse - translate) 가 스케일되면서 mouse가 고정되도록 translate 조정
            double tx = TranslateTf.X;
            double ty = TranslateTf.Y;

            double newTx = mouse.X - (mouse.X - tx) * factor;
            double newTy = mouse.Y - (mouse.Y - ty) * factor;

            ScaleTf.ScaleX = newScale;
            ScaleTf.ScaleY = newScale;
            TranslateTf.X = newTx;
            TranslateTf.Y = newTy;

            UpdateInfo();
        }

        // -------- Pan (Drag) --------
        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ImgMain.Source == null) return;

            _isPanning = true;
            _panStartPoint = e.GetPosition(Viewport);
            _panStartX = TranslateTf.X;
            _panStartY = TranslateTf.Y;

            Viewport.CaptureMouse();
            Mouse.OverrideCursor = Cursors.Hand;
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;

            Point cur = e.GetPosition(Viewport);
            Vector delta = cur - _panStartPoint;

            TranslateTf.X = _panStartX + delta.X;
            TranslateTf.Y = _panStartY + delta.Y;
        }

        private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;

            _isPanning = false;
            Viewport.ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
        }

        // -------- Helpers --------
        private void ResetView()
        {
            ScaleTf.ScaleX = 1;
            ScaleTf.ScaleY = 1;
            TranslateTf.X = 0;
            TranslateTf.Y = 0;
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            TxtInfo.Text = $"Scale: {ScaleTf.ScaleX:0.###}  (Wheel: Zoom, Drag: Pan)";
        }
    }
}
