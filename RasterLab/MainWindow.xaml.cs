using Microsoft.Win32;              // OpenFileDialog
using OSGeo.GDAL;                   // GDAL C# 바인딩
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace RasterLab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 1) 파일을 열면 생기는 "영상 전체" 객체
        private Dataset _ds;

        // 2) 픽셀을 읽을 "밴드" 객체 (일단 1번 밴드만 테스트)
        private Band _band1;

        // 3) 좌표 변환용 GeoTransform (나중에 좌표 표시할 때 사용)
        private double[] _gt = new double[6];

        // 전역으로 빼버려 그냥
        private WriteableBitmap bmp;

        // 실시간 픽셀 읽기
        private int _lastCol = -1;
        private int _lastRow = -1;

        // 스포틀 (ms)
        private const int HoverIntervalMs = 40;
        private long _lastHoverTick = 0;

        // 비동기 취소
        private CancellationTokenSource? _hoverCts;

        // 좌표 복사
        private int _curCol = -1;
        private int _curRow = -1;
        private double _curGeoX = 0;
        private double _curGeoY = 0;
        private string _curValue = "";
        private bool _hasCoord = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Open 버튼 클릭: 파일 선택 -> Dataset 열기 -> 메타데이터 표시

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            // GDAL 드라이버 등록 (중요)
            // 어떤 포맷을 열 수 있는지 GDAL이 준비하는 단계
            // Gdal.AllRegister();
            // Gdal.AllRegister() 대신 InitGdal() 함수를 만들어서 씀
            InitGdal();

            // 초기 상태 표시 (XAML에서 TxtStatus가 없으면 이 줄은 지워도 됨...)
            // TxtStatus.Text = "Ready"; // 이거 왜 필요함...?

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Raster files|*.tif;*.tiff;*.img;*.vrt|All files|*.*";

            bool? ok = dlg.ShowDialog();
            if(ok != true)
            {
                return;
            }

            string path = dlg.FileName;

            // 기존에 열려있던 Dataset 해제
            CloseDatasetIfAny();

            // 파일 열기
            _ds = Gdal.Open(path, Access.GA_ReadOnly);
            if(_ds == null)
            {
                MessageBox.Show("파일 열기 실패 (Dataset이 null)");
                return;
            }

            // 1번 밴드 가져오기
            // 이 부분 관련해서는 summation.md에 추가 설명
            _band1 = _ds.GetRasterBand(1);
            if( _band1 == null)
            {
                MessageBox.Show("1번 밴드 가져오기 실패 (Band가 null)");
                return;
            }

            // GeoTransform 읽기 (없으면 0 배열로 남을 수 있음)
            _ds.GetGeoTransform(_gt);

            // 메타데이터 텍스트 만들기 (좌측 TextBox에 표시하는 용도)
            string meta = BuildMetadataText(path, _ds, _gt);
            TxtMeta.Text = meta;

            ShowRasterPreview();

            
            ImgRaster.Source = bmp;
            // 켰을때 어느정도 자동 맞춤
            FitToWindow();
            // 상태바가 있으면 표시 (없으면 지워도 됨)
            // TxtStatus.Text = "Opened: " + path;
        }

        private void CloseDatasetIfAny()
        {
            // Band는 Dataset이 Dispose되면 같이 의미가 없어지므로 같이 null로
            _band1 = null;

            if(_ds != null)
            {
                _ds.Dispose();
                _ds = null;
            }
        }

        private string BuildMetadataText(string path, Dataset ds, double[] gt)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("File : ");
            sb.AppendLine("    " + path);
            sb.AppendLine();

            sb.AppendLine("Driver : ");
            sb.AppendLine("    " + ds.GetDriver().LongName);
            sb.AppendLine();

            sb.AppendLine("Size : ");
            sb.AppendLine("    " + ds.RasterXSize + " x " + ds.RasterYSize);
            sb.AppendLine();

            sb.AppendLine("Bands : ");
            sb.AppendLine("    " + ds.RasterCount);
            sb.AppendLine();

            sb.AppendLine("Band1 DataType : ");
            sb.AppendLine("    " + _band1.DataType);
            sb.AppendLine();

            sb.AppendLine("GeoTransform : ");
            sb.AppendLine("  [" + gt[0] + ", " + gt[1] + ", " + gt[2] + ", " + gt[3] + ", " + gt[4] + ", " + gt[5] + "]");
            sb.AppendLine();

            string proj = ds.GetProjection();
            sb.AppendLine("Projection : ");
            if (String.IsNullOrEmpty(proj))
            {
                sb.AppendLine("  (empty)");
            }
            else
            {
                sb.AppendLine("  (exists, length = " + proj.Length + ")");
            }

            return sb.ToString();
        }

        // 픽셀 테스트 버튼 클릭: (100,50) 픽셀 한 점 읽기
        private void BtnPixelTest_Click(object sender, RoutedEventArgs e)
        {   
            // 파일을 먼저 열어야 픽셀을 읽을 수 있음
            if(_ds == null || _band1 == null)
            {
                MessageBox.Show("먼저 Open으로 tif 파일을 여세요.");
                return;
            }

            int col = 100;
            int row = 50;

            // 범위체크 (안 해도 되지만 실습할 땐 안전)
            if (col < 0 || col >= _ds.RasterXSize || row < 0 || row >= _ds.RasterYSize)
            {
                MessageBox.Show("col/row가 영상 범위를 벗어났습니다.");
                return;
            }

            // 데이터 타입에 따라 버퍼 타입을 달리해야 갑이 안 깨짐
            // (실습은 Byte / UInt16 두 가지만 먼저)
            if(_band1.DataType == DataType.GDT_Byte)
            {
                byte[] buffer = new byte[1];

                _band1.ReadRaster(col, row, 1, 1, buffer, 1, 1, 0, 0);

                byte value = buffer[0];

                MessageBox.Show("Pixel (" + col + ", " + row + ") = " + value);
            }
            else if (_band1.DataType == DataType.GDT_UInt16)
            {
                // UInt16 1픽셀 = 2바이트
                byte[] raw = new byte[2];

                _band1.ReadRaster(col, row, 1, 1, raw, 1, 1, 0, 0);

                // Windows(x86/x64)는 Little Endian이라 보통 아래가 맞음
                ushort value = BitConverter.ToUInt16(raw, 0);

                MessageBox.Show("Pixel (" + col + ", " + row + ") = " + value);
            }
            else
            {
                // 그 외 타입(Float32 등)은 다음 단계에서 처리
                MessageBox.Show("현재 실습은 Byte/UInt16만 지원. 현재 타입: " + _band1.DataType);
            }
        }

        // 창 닫힐 때 리소스 정리
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            CloseDatasetIfAny();
        }

        // Gdal.Allregister 오류 잡기 위해
        private static bool _gdalInited = false;

        private static void InitGdal()
        {
            if (_gdalInited) return;

            // exe가 있는 폴더
            // workspace\RasterLab\bin\Debug\net8.0-windows
            string baseDir = AppContext.BaseDirectory;

            // 내 프로젝트 파일 구조 기준 : baseDir\gdal\...
            // workspace\RasterLab\bin\Debug\net8.0-windows\gdal\data
            string gdalRoot = Path.Combine(baseDir, "gdal");

            string gdalData = Path.Combine(gdalRoot, "data");
            string projLib = Path.Combine(gdalRoot, "share");

            // 네이티브 dll 폴더 후보들(패키지마다 다름) - 있는 걸로 선택
            string[] nativeCandidates =
            {
                Path.Combine(gdalRoot, "x64"),
                Path.Combine(gdalRoot, "bin"),
                Path.Combine(baseDir, "runtimes", "win-x64", "native"),
                Path.Combine(gdalRoot, "runtimes", "win-x64", "native"),
            };

            string nativeDir = null;
            foreach (var c in nativeCandidates)
            {
                if (Directory.Exists(c))
                {
                    nativeDir = c;
                    break;
                }
            }

            if (nativeDir == null)
                throw new DirectoryNotFoundException("nativeDir NOT FOUND: gdal native dll folder not found.");

            if (!Directory.Exists(gdalData))
                throw new DirectoryNotFoundException("GDAL_DATA folder NOT FOUND: " + gdalData);

            if (!Directory.Exists(projLib))
                throw new DirectoryNotFoundException("PROJ_LIB folder NOT FOUND: " + projLib);

            // 1) PATH에 네이티브 dll 폴더 추가(앞에!)
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", nativeDir + ";" + path);

            // 2) GDAL/PROJ 경로 지정 (환경변수 + GDAL ConfigOption 둘 다 박으면 더 안전)
            Environment.SetEnvironmentVariable("GDAL_DATA", gdalData);
            Environment.SetEnvironmentVariable("PROJ_LIB", projLib);

            Gdal.SetConfigOption("GDAL_DATA", gdalData);
            Gdal.SetConfigOption("PROJ_LIB", projLib);

            // 3) 등록
            Gdal.AllRegister();

            _gdalInited = true;
        }

        // 이미지 그려주기
        private void ShowRasterPreview()
        {
            if (_ds == null || _band1 == null) return;

            int w = _ds.RasterXSize;
            int h = _ds.RasterYSize;

            if (_band1.DataType != DataType.GDT_Byte)
            {
                MessageBox.Show("지금 미리보기는 GDT_Byte(8bit)만 지원합니다. 현재: " + _band1.DataType);
                return;
            }

            // 1) 픽셀 전체 읽기
            byte[] src = new byte[w * h];
            _band1.ReadRaster(0, 0, w, h, src, w, h, 0, 0);

            // 2) 너무 어두우면 자동 스트레치(선택)
            //    네 XAML에 체크박스가 없으니 일단 항상 스트레치로 가자.
            byte[] dst = StretchToByte(src);

            // 3) WPF 비트맵 생성 후 복사
            bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Gray8, null);
            int stride = w; // Gray8: 1 byte per pixel
            bmp.WritePixels(new Int32Rect(0, 0, w, h), dst, stride, 0);

            ImgRaster.Source = bmp;

            System.Diagnostics.Debug.WriteLine($"ImgRaster.Source is null? {ImgRaster.Source == null}");
        }

        private byte[] StretchToByte(byte[] src)
        {
            int min = 255, max = 0;

            for (int i = 0; i < src.Length; i++)
            {
                int v = src[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            if (max <= min) return src;

            int range = max - min;
            byte[] dst = new byte[src.Length];

            for (int i = 0; i < src.Length; i++)
            {
                int scaled = (src[i] - min) * 255 / range;
                if (scaled < 0) scaled = 0;
                if (scaled > 255) scaled = 255;
                dst[i] = (byte)scaled;
            }

            return dst;
        }


        // 마우스 휠로 확대/축소 (스크롤바 기반으로 이동 유지)
        private void SvImage_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl 없이 휠: 기본 스크롤 동작 유지
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            e.Handled = true; // ScrollViewer 기본 휠 스크롤 막기

            double oldScale = ZoomTf.ScaleX;
            double zoomFactor = (e.Delta > 0) ? 1.1 : 1.0 / 1.1;

            double newScale = oldScale * zoomFactor;

            // 너무 작아/커지는 거 방지
            if (newScale < 0.05) newScale = 0.05;
            if (newScale > 50) newScale = 50;

            // 마우스가 가리키는 "현재 화면상의 위치"를 기준으로 줌 유지
            Point mouseOnContent = e.GetPosition(ImgHost);

            double offsetX = SvImage.HorizontalOffset;
            double offsetY = SvImage.VerticalOffset;

            // 줌 전: 마우스가 가리키는 콘텐츠 좌표(스크롤 포함)
            double absX = mouseOnContent.X + offsetX;
            double absY = mouseOnContent.Y + offsetY;

            // 스케일 적용
            ZoomTf.ScaleX = newScale;
            ZoomTf.ScaleY = newScale;

            // 레이아웃 갱신이 필요(Extent 계산 반영)
            SvImage.UpdateLayout();

            // 줌 후에도 같은 abs 좌표가 마우스 아래 오도록 offset 재계산
            SvImage.ScrollToHorizontalOffset(absX * (newScale / oldScale) - mouseOnContent.X);
            SvImage.ScrollToVerticalOffset(absY * (newScale / oldScale) - mouseOnContent.Y);
        }

        private double _zoom = 1.0;

        // “처음 열면 화면에 맞춰서 보이기” (Fit)
        private void FitToWindow()
        {
            if (ImgRaster.Source == null) return;

            // ScrollViewer의 실제 표시 영역(스크롤바/테두리 제외)
            double vw = SvImage.ViewportWidth;
            double vh = SvImage.ViewportHeight;

            if (vw <= 0 || vh <= 0) return; // 아직 레이아웃 전이면 0일 수 있음

            double iw = ImgRaster.Source.Width;
            double ih = ImgRaster.Source.Height;

            double scaleX = vw / iw;
            double scaleY = vh / ih;

            _zoom = Math.Min(scaleX, scaleY);

            ZoomTf.ScaleX = _zoom;
            ZoomTf.ScaleY = _zoom;
        }

        // 실시간 픽셀 읽기
        private async void ImgHost_MouseMove(object sender, MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("MOVE 1");
            if (_ds == null || _band1 == null || ImgRaster.Source == null) return;

            // 스로틀
            long now = Environment.TickCount64;
            if (now - _lastHoverTick < HoverIntervalMs) return;
            _lastHoverTick = now;

            // 좌표 계산
            Point p = e.GetPosition(ImgHost);

            //System.Diagnostics.Debug.WriteLine($"MOVE 3: p=({p.X},{p.Y}) zoom=({ZoomTf.ScaleX},{ZoomTf.ScaleY})");

            int col = (int)p.X;
            int row = (int)p.Y;

            // 범위 체크
            if (col < 0 || row < 0 || col >= _ds.RasterXSize || row >= _ds.RasterYSize)
            {
                TxtPixel.Text = "Pixel: -";
                _lastCol = -1;
                _lastRow = -1;
                if (_hoverCts != null) _hoverCts.Cancel();
                return;
            }

            // 같은 픽셀 반복이면 ReadRaster 생략
            if (col == _lastCol && row == _lastRow) return;
            _lastCol = col;
            _lastRow = row;

            // 픽셀 값 읽기(Byte 기준)
            // value 값이 0 ~ 9 까지 나올텐데 0은 검정 9는 흰색 정도이다.
            //byte[] buffer = new byte[1];
            //_band1.ReadRaster(col, row, 1, 1, buffer, 1, 1, 0, 0);

            var (geoX, geoY) = PixelCenterToGeo(col, row);
            TxtPixel.Text = $"Pixel: col={col}, row={row}\nGeo : X={geoX:0.###}, Y={geoY:0.###}\nValue: ...";

            // 좌표 복사를 위해 Hover 즉시 (UI 스레드)
            _curCol = col;
            _curRow = row;
            _curGeoX = geoX;
            _curGeoY = geoY;
            _hasCoord = true;

            // 이전 작업 취소
            if (_hoverCts != null) _hoverCts.Cancel();
            _hoverCts = new CancellationTokenSource();
            CancellationToken token = _hoverCts.Token;

            try
            {
                // 백그라운드에서 value 읽기
                string valueStr = await Task.Run(delegate
                {
                    token.ThrowIfCancellationRequested();
                    return ReadPixelAsString(_band1, col, row);
                }, token);

                if(token.IsCancellationRequested) return;

                // 최신 좌표인지 확인(늦게 끝난 작업이 덮어쓰는 것 방지)
                if (col != _lastCol || row != _lastRow) return;

                TxtPixel.Text = $"Pixel: col={col}, row={row}\nGeo : X={geoX:0.###}, Y={geoY:0.###}\nValue: {valueStr}";

                // 좌표 복사 ReadRaster 완료 후 (비동기 결과)
                _curValue = valueStr;
            }
            catch (OperationCanceledException)
            {
                // 무시 (마우스가 움직이면 취소되는게 정상)
            }
            catch (Exception ex)
            {
                TxtPixel.Text = $"Pixel: col={col}, row={row}\nValue read error: {ex.Message}";
            }
        }

        private void ImgHost_MouseLeave(object sender, EventArgs e)
        {
            TxtPixel.Text = "Pixel: -";
            _lastCol = -1;
            _lastRow = -1;

            if(_hoverCts != null) _hoverCts.Cancel();

            // _hasCoord / _curXXX 는 건드리지 않음
        }

        private string ReadPixelAsString(Band band, int col, int row)
        {
            if (band.DataType == DataType.GDT_Byte)
            {
                byte[] buffer = new byte[1];
                band.ReadRaster(col, row, 1, 1, buffer, 1, 1, 0, 0);
                return buffer[0].ToString();
            }
            else if (band.DataType == DataType.GDT_UInt16)
            {
                byte[] raw = new byte[2];
                band.ReadRaster(col, row, 1, 1, raw, 1, 1, 0, 0);
                ushort value = BitConverter.ToUInt16(raw, 0);
                return value.ToString();
            }
            else if (band.DataType == DataType.GDT_CFloat32)
            {
                byte[] raw = new byte[4];
                band.ReadRaster(col, row, 1, 1, raw, 1, 1, 0, 0);
                float value = BitConverter.ToSingle(raw, 0);
                return value.ToString("0.###");
            }

            return band.DataType.ToString();
        }

        // GeoTransform / CRS / Raster 크기 로그로 찍기
        private void ImgHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_ds == null) return;

            Point p = e.GetPosition(ImgHost);

            int col = (int)p.X;
            int row = (int)p.Y;

            double[] gt = new double[6];
            _ds.GetGeoTransform(gt);

            // 픽셀 중심 기준
            double px = col + 0.5;
            double py = row + 0.5;

            double geoX = gt[0] + px * gt[1] + py * gt[2];
            double geoY = gt[3] + px * gt[4] + py * gt[5];

            System.Diagnostics.Debug.WriteLine($"[CLICK] p=({p.X:0.###},{p.Y:0.###}) -> col,row=({col},{row}) -> geo=({geoX:0.###},{geoY:0.###})");

        }

        // 픽셀 -> geo(중심)
        private (double x, double y) PixelCenterToGeo(int col, int row)
        {
            double[] gt = new double[6];
            _ds.GetGeoTransform(gt);

            double px = col + 0.5;
            double py = row + 0.5;

            double geoX = gt[0] + px * gt[1] + py * gt[2];
            double geoY = gt[3] + px * gt[4] + py * gt[5];

            return (geoX, geoY);
        }

        // 좌표 복사 함수
        private void BtnCopyPixel_Click(object sender, EventArgs e)
        {
            if (!_hasCoord) return;
            Clipboard.SetText(_curCol + "," + _curRow);
        }

        private void BtnCopyGeo_Click(object sender, EventArgs e)
        {
            if (!_hasCoord) return;
            Clipboard.SetText(_curGeoX.ToString("0.###") + "," + _curGeoY.ToString("0.###"));
        }

        private void BtnCopyAll_Click(object sender, EventArgs e)
        {
            if (!_hasCoord) return;

            // col, row, x, y, value (value 없으면 빈칸)
            string x = _curGeoX.ToString("0.###");
            string y = _curGeoY.ToString("0.###");
            string v = _curValue ?? "";

            Clipboard.SetText(_curCol + "," + _curRow + "," + x + "," + y + "," + v);
        }
    }
}