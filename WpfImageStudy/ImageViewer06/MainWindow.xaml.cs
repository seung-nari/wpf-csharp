using Microsoft.Win32;
using OSGeo.GDAL;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageViewer06
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Raster|*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp|All|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            LoadRaster(dlg.FileName);
        }

        private void LoadRaster(string path)
        {
            using var ds = Gdal.Open(path, Access.GA_ReadOnly);
            if (ds == null)
            {
                MessageBox.Show("GDAL.Open failed");
                return;
            }

            // ---- Info ----
            var sb = new StringBuilder();
            sb.AppendLine($"Path: {path}");
            sb.AppendLine($"Size: {ds.RasterXSize} x {ds.RasterYSize}");
            sb.AppendLine($"Bands: {ds.RasterCount}");
            sb.AppendLine($"Driver: {ds.GetDriver().ShortName} / {ds.GetDriver().LongName}");

            double[] gt = new double[6];
            ds.GetGeoTransform(gt);
            sb.AppendLine($"GeoTransform: [{string.Join(", ", gt.Select(v => v.ToString("G")))}]");

            var proj = ds.GetProjectionRef();
            sb.AppendLine($"Projection: {(string.IsNullOrWhiteSpace(proj) ? "(none)" : "(exists)")}");
            TxtInfo.Text = sb.ToString();

            // ---- Render (simple) ----
            var bmp = RenderDatasetToBitmap(ds, autoStretch: ChkAutoStretch.IsChecked == true);
            Img.Source = bmp;
        }

        private static WriteableBitmap RenderDatasetToBitmap(Dataset ds, bool autoStretch)
        {
            int w = ds.RasterXSize;
            int h = ds.RasterYSize;

            // WPF는 BGRA32가 다루기 가장 편함
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            int stride = w * 4;
            byte[] bgra = new byte[h * stride];

            if (ds.RasterCount >= 3)
            {
                // RGB (밴드 1,2,3을 R,G,B로 가정)
                FillRgb(ds, bgra, w, h, autoStretch);
            }
            else
            {
                // Single band grayscale
                FillGray(ds, bgra, w, h, autoStretch);
            }

            wb.WritePixels(new Int32Rect(0, 0, w, h), bgra, stride, 0);
            return wb;
        }

        private static void FillGray(Dataset ds, byte[] bgra, int w, int h, bool autoStretch)
        {
            Band b = ds.GetRasterBand(1);

            // 일단 float로 읽고 0~255로 매핑 (데이터 타입이 UInt16/Float32여도 대응)
            float[] buf = new float[w * h];
            b.ReadRaster(0, 0, w, h, buf, w, h, 0, 0);

            (float min, float max) = autoStretch ? GetMinMax(b, buf) : (0f, 255f);
            float scale = (max - min) == 0 ? 1 : 255f / (max - min);

            int p = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                byte g = ToByte((buf[i] - min) * scale);
                bgra[p++] = g;   // B
                bgra[p++] = g;   // G
                bgra[p++] = g;   // R
                bgra[p++] = 255; // A
            }
        }

        private static void FillRgb(Dataset ds, byte[] bgra, int w, int h, bool autoStretch)
        {
            Band rBand = ds.GetRasterBand(1);
            Band gBand = ds.GetRasterBand(2);
            Band bBand = ds.GetRasterBand(3);

            float[] r = new float[w * h];
            float[] g = new float[w * h];
            float[] b = new float[w * h];

            rBand.ReadRaster(0, 0, w, h, r, w, h, 0, 0);
            gBand.ReadRaster(0, 0, w, h, g, w, h, 0, 0);
            bBand.ReadRaster(0, 0, w, h, b, w, h, 0, 0);

            (float rMin, float rMax) = autoStretch ? GetMinMax(rBand, r) : (0f, 255f);
            (float gMin, float gMax) = autoStretch ? GetMinMax(gBand, g) : (0f, 255f);
            (float bMin, float bMax) = autoStretch ? GetMinMax(bBand, b) : (0f, 255f);

            float rScale = (rMax - rMin) == 0 ? 1 : 255f / (rMax - rMin);
            float gScale = (gMax - gMin) == 0 ? 1 : 255f / (gMax - gMin);
            float bScale = (bMax - bMin) == 0 ? 1 : 255f / (bMax - bMin);

            int p = 0;
            for (int i = 0; i < r.Length; i++)
            {
                byte rr = ToByte((r[i] - rMin) * rScale);
                byte gg = ToByte((g[i] - gMin) * gScale);
                byte bb = ToByte((b[i] - bMin) * bScale);

                bgra[p++] = bb;  // B
                bgra[p++] = gg;  // G
                bgra[p++] = rr;  // R
                bgra[p++] = 255; // A
            }
        }

        private static (float min, float max) GetMinMax(Band band, float[] fallback)
        {
            // band.GetMinimum/GetMaximum가 없는 경우도 있어서 통계로 처리
            // GDAL 통계 계산이 무거우면 배열로 min/max 계산
            try
            {
                band.GetStatistics(0, 1, out double min, out double max, out _, out _);
                return ((float)min, (float)max);
            }
            catch
            {
                float min = fallback.Min();
                float max = fallback.Max();
                return (min, max);
            }
        }

        private static byte ToByte(float v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }
    }
}