using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImageViewer05
{
    public partial class MainWindow : Window
    {
        // Pan
        private bool _isPanning = false;
        private Point _panStartPoint;
        private double _panStartX, _panStartY;

        // Zoom
        private const double ZoomStep = 1.15;
        private const double MinScale = 0.05;
        private const double MaxScale = 40.0;

        // Loaded image
        private BitmapFrame? _frame;

        // GeoTIFF basics (ModelPixelScale + ModelTiepoint)
        private bool _hasGeo = false;

        // tiepoint: raster (i,j,k) -> model (X,Y,Z)
        private double _tieI, _tieJ, _tieX, _tieY;

        // pixel scale
        private double _scaleX, _scaleY;

        public MainWindow()
        {
            InitializeComponent();
            UpdateStatus("-");
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "이미지 선택",
                Filter = "Image Files (*.tif;*.tiff;*.jpg;*.jpeg;*.png)|*.tif;*.tiff;*.jpg;*.jpeg;*.png|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                LoadImage(dlg.FileName);
                TxtPath.Text = dlg.FileName;

                ResetView();
                UpdateStatus("이미지 로드 완료");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로딩 실패\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) => ResetView();

        private void LoadImage(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);

            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            _frame = decoder.Frames[0];
            if (_frame.CanFreeze) _frame.Freeze();

            ImgMain.Source = _frame;

            // Geo parse (if TIFF/GeoTIFF)
            _hasGeo = TryReadGeoTiffBasics(_frame, out _tieI, out _tieJ, out _tieX, out _tieY, out _scaleX, out _scaleY);

            if (_hasGeo)
            {
                // 표시용 메시지
                UpdateStatus($"GeoTIFF 감지: Tiepoint(raster=({_tieI:0.###},{_tieJ:0.###}) -> model=({_tieX:0.###},{_tieY:0.###})), Scale=({_scaleX:0.###},{_scaleY:0.###})");
            }
            else
            {
                UpdateStatus("GeoTIFF 정보 없음(또는 이 방식으로 해석 불가) → 픽셀 좌표만 표시");
            }
        }

        // ------------------ Mouse: Pixel + Geo ------------------
        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            // Pan 처리
            if (_isPanning)
            {
                Point cur = e.GetPosition(Viewport);
                Vector delta = cur - _panStartPoint;
                TranslateTf.X = _panStartX + delta.X;
                TranslateTf.Y = _panStartY + delta.Y;
            }

            // 좌표 표시
            if (_frame == null) return;

            Point mouse = e.GetPosition(Viewport);

            // 현재 Transform(Scale+Translate)을 역으로 적용해서 "이미지 좌표" 구함
            double s = ScaleTf.ScaleX;
            if (s == 0) return;

            double xImg = (mouse.X - TranslateTf.X) / s;
            double yImg = (mouse.Y - TranslateTf.Y) / s;

            // 이미지 픽셀 범위 내일 때만 표시
            int px = (int)Math.Floor(xImg);
            int py = (int)Math.Floor(yImg);

            if (px < 0 || py < 0 || px >= _frame.PixelWidth || py >= _frame.PixelHeight)
            {
                TxtStatus.Text = $"Pixel: (out)   Scale:{ScaleTf.ScaleX:0.###}";
                return;
            }

            if (_hasGeo)
            {
                // 기본 북업(north-up) 가정:
                // Xgeo = TieX + (px - TieI) * ScaleX
                // Ygeo = TieY - (py - TieJ) * ScaleY   (이미지 y는 아래로 증가하므로 보통 반대부호)
                double xGeo = _tieX + (px - _tieI) * _scaleX;
                double yGeo = _tieY - (py - _tieJ) * _scaleY;

                TxtStatus.Text =
                    $"Pixel: ({px}, {py})   Geo(Model): ({xGeo:0.###}, {yGeo:0.###})   Scale:{ScaleTf.ScaleX:0.###}";
            }
            else
            {
                TxtStatus.Text = $"Pixel: ({px}, {py})   Scale:{ScaleTf.ScaleX:0.###}";
            }
        }

        // ------------------ Zoom / Pan ------------------
        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_frame == null) return;

            double scale = ScaleTf.ScaleX;
            double factor = (e.Delta > 0) ? ZoomStep : (1.0 / ZoomStep);

            double newScale = Math.Clamp(scale * factor, MinScale, MaxScale);
            factor = newScale / scale;

            Point mouse = e.GetPosition(Viewport);

            double tx = TranslateTf.X;
            double ty = TranslateTf.Y;

            double newTx = mouse.X - (mouse.X - tx) * factor;
            double newTy = mouse.Y - (mouse.Y - ty) * factor;

            ScaleTf.ScaleX = newScale;
            ScaleTf.ScaleY = newScale;
            TranslateTf.X = newTx;
            TranslateTf.Y = newTy;
        }

        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_frame == null) return;

            _isPanning = true;
            _panStartPoint = e.GetPosition(Viewport);
            _panStartX = TranslateTf.X;
            _panStartY = TranslateTf.Y;

            Viewport.CaptureMouse();
            Mouse.OverrideCursor = Cursors.Hand;
        }

        private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;

            _isPanning = false;
            Viewport.ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
        }

        private void ResetView()
        {
            ScaleTf.ScaleX = 1;
            ScaleTf.ScaleY = 1;
            TranslateTf.X = 0;
            TranslateTf.Y = 0;
        }

        private void UpdateStatus(string msg) => TxtStatus.Text = msg;

        // ------------------ GeoTIFF basics parsing ------------------
        // Reads:
        // - ModelPixelScaleTag (33550): [ScaleX, ScaleY, ScaleZ]
        // - ModelTiepointTag   (33922): [i,j,k,X,Y,Z] repeated
        //
        // If present, we use the FIRST tiepoint entry.
        private static bool TryReadGeoTiffBasics(
            BitmapFrame frame,
            out double tieI, out double tieJ, out double tieX, out double tieY,
            out double scaleX, out double scaleY)
        {
            tieI = tieJ = tieX = tieY = 0;
            scaleX = scaleY = 0;

            if (frame.Metadata is not BitmapMetadata meta)
                return false;

            // TIFF tags are typically under "/ifd/{ushort=TAG}"
            // 33550: ModelPixelScaleTag
            // 33922: ModelTiepointTag
            double[]? scales = GetDoubleArray(meta, "/ifd/{ushort=33550}");
            double[]? ties = GetDoubleArray(meta, "/ifd/{ushort=33922}");

            if (scales == null || scales.Length < 2) return false;
            if (ties == null || ties.Length < 6) return false;

            scaleX = scales[0];
            scaleY = scales[1];

            // First tiepoint
            tieI = ties[0];
            tieJ = ties[1];
            tieX = ties[3];
            tieY = ties[4];

            // scaleX/scaleY가 0이거나 비정상인 경우 방어
            if (scaleX == 0 || scaleY == 0) return false;

            return true;
        }

        private static double[]? GetDoubleArray(BitmapMetadata meta, string query)
        {
            try
            {
                object? v = meta.GetQuery(query);
                if (v == null) return null;

                // WPF가 반환하는 타입이 케이스별로 다를 수 있어서 최대한 유연하게 처리
                if (v is double[] dArr) return dArr;
                if (v is float[] fArr) return fArr.Select(x => (double)x).ToArray();
                if (v is Array arr)
                {
                    var tmp = new double[arr.Length];
                    for (int i = 0; i < arr.Length; i++)
                        tmp[i] = Convert.ToDouble(arr.GetValue(i));
                    return tmp;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
