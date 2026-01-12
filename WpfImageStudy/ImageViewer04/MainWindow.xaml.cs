using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageViewer04
{
    public partial class MainWindow : Window
    {
        private List<BitmapFrame> _frames = new();
        private int _pageIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            UpdateUi();
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
                LoadAsFrames(dlg.FileName);
                TxtPath.Text = dlg.FileName;

                _pageIndex = 0;
                ShowPage(_pageIndex);
                UpdateUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로딩 실패\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_frames.Count == 0 || _pageIndex <= 0) return;
            _pageIndex--;
            ShowPage(_pageIndex);
            UpdateUi();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_frames.Count == 0 || _pageIndex >= _frames.Count - 1) return;
            _pageIndex++;
            ShowPage(_pageIndex);
            UpdateUi();
        }

        private void LoadAsFrames(string path)
        {
            _frames.Clear();

            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);

            // 어떤 포맷이든 Decoder로 통일하면 TIFF 멀티페이지도 깔끔하게 처리 가능
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            foreach (var f in decoder.Frames)
            {
                if (f.CanFreeze) f.Freeze();
                _frames.Add(f);
            }
        }

        private void ShowPage(int index)
        {
            if (_frames.Count == 0) return;
            if (index < 0 || index >= _frames.Count) return;

            var frame = _frames[index];
            ImgMain.Source = frame;

            TxtPage.Text = $"Page: {index + 1} / {_frames.Count}";
            TxtMeta.Text = BuildMetadataText(frame);
        }

        private void UpdateUi()
        {
            bool has = _frames.Count > 0;
            bool multi = _frames.Count > 1;

            BtnPrev.IsEnabled = has && multi && _pageIndex > 0;
            BtnNext.IsEnabled = has && multi && _pageIndex < _frames.Count - 1;

            if (!has) TxtMeta.Text = "이미지를 열어주세요.";
        }

        private static string BuildMetadataText(BitmapFrame frame)
        {
            var sb = new StringBuilder();

            // 기본 정보(항상 신뢰 가능)
            sb.AppendLine("[기본]");
            sb.AppendLine($"Pixel: {frame.PixelWidth} x {frame.PixelHeight}");
            sb.AppendLine($"DPI  : {frame.DpiX:0.##} x {frame.DpiY:0.##}");
            sb.AppendLine($"Format: {frame.Format}");
            sb.AppendLine($"BitsPerPixel: {frame.Format.BitsPerPixel}");
            sb.AppendLine();

            // 메타데이터(있으면)
            if (frame.Metadata is BitmapMetadata meta)
            {
                sb.AppendLine("[메타데이터(가능한 항목만)]");

                // EXIF / TIFF tag: GetQuery로 시도 (없으면 예외/빈값)
                // - 카메라/스마트폰 JPG에서 잘 나오는 편
                AppendQuery(sb, meta, "DateTimeOriginal", "/app1/ifd/exif:{uint=36867}");
                AppendQuery(sb, meta, "DateTimeDigitized", "/app1/ifd/exif:{uint=36868}");
                AppendQuery(sb, meta, "Make", "/app1/ifd/{ushort=271}");
                AppendQuery(sb, meta, "Model", "/app1/ifd/{ushort=272}");
                AppendQuery(sb, meta, "Orientation", "/app1/ifd/{ushort=274}");
                AppendQuery(sb, meta, "Software", "/app1/ifd/{ushort=305}");

                // PNG (tEXt / iTXt)
                AppendQuery(sb, meta, "PNG Software", "/tEXt/Software");
                AppendQuery(sb, meta, "PNG Comment", "/tEXt/Comment");

                // TIFF
                AppendQuery(sb, meta, "TIFF Artist", "/ifd/{ushort=315}");
                AppendQuery(sb, meta, "TIFF Description", "/ifd/{ushort=270}");

                // GPS (있으면)
                AppendQuery(sb, meta, "GPS Latitude", "/app1/ifd/gps:{ushort=2}");
                AppendQuery(sb, meta, "GPS Longitude", "/app1/ifd/gps:{ushort=4}");

                sb.AppendLine();
                sb.AppendLine("※ 주의: PNG/TIFF/JPG마다 메타 구조가 달라서, 없는 태그는 표시되지 않습니다.");
            }
            else
            {
                sb.AppendLine("[메타데이터]");
                sb.AppendLine("이 이미지 프레임에서 읽을 수 있는 메타데이터가 없습니다.");
            }

            return sb.ToString();
        }

        private static string? Join(System.Collections.Generic.IEnumerable<string>? items)
        {
            if (items == null) return null;

            var list = new List<string>();
            foreach (var s in items)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }

            return list.Count == 0 ? null : string.Join(", ", list);
        }

        private static void AppendIfNotEmpty(StringBuilder sb, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.AppendLine($"{key}: {value}");
        }

        private static void AppendQuery(StringBuilder sb, BitmapMetadata meta, string label, string query)
        {
            try
            {
                object? v = meta.GetQuery(query);
                if (v == null) return;

                // byte[] 같은 경우 보기 좋게
                if (v is byte[] bytes)
                {
                    sb.AppendLine($"{label}: (byte[{bytes.Length}])");
                    return;
                }

                sb.AppendLine($"{label}: {v}");
            }
            catch
            {
                // 없는 태그면 그냥 무시
            }
        }
    }
}
