using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageViewer03
{
    public partial class MainWindow : Window
    {
        // 현재 열린 이미지(페이지들)
        private List<BitmapSource> _frames = new();
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
                LoadImageAsFrames(dlg.FileName);
                TxtPath.Text = dlg.FileName;

                _pageIndex = 0;
                ShowPage(_pageIndex);
                UpdateUi();
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

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_frames.Count == 0) return;
            if (_pageIndex <= 0) return;

            _pageIndex--;
            ShowPage(_pageIndex);
            UpdateUi();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_frames.Count == 0) return;
            if (_pageIndex >= _frames.Count - 1) return;

            _pageIndex++;
            ShowPage(_pageIndex);
            UpdateUi();
        }

        private void LoadImageAsFrames(string path)
        {
            _frames.Clear();

            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);

            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".tif" || ext == ".tiff")
            {
                // TIFF: 멀티페이지는 Frames에 전부 들어있음
                var decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                foreach (var frame in decoder.Frames)
                {
                    if (frame.CanFreeze) frame.Freeze();
                    _frames.Add(frame);
                }
            }
            else
            {
                // JPG/PNG: 1장짜리로 frames 구성
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();

                _frames.Add(bi);
            }
        }

        private void ShowPage(int index)
        {
            if (_frames.Count == 0) return;
            if (index < 0 || index >= _frames.Count) return;

            ImgMain.Source = _frames[index];
            TxtPage.Text = $"Page: {index + 1} / {_frames.Count}";
        }

        private void UpdateUi()
        {
            bool hasImage = _frames.Count > 0;
            bool isMultipage = _frames.Count > 1;

            BtnPrev.IsEnabled = hasImage && isMultipage && _pageIndex > 0;
            BtnNext.IsEnabled = hasImage && isMultipage && _pageIndex < _frames.Count - 1;

            if (!hasImage)
                TxtPage.Text = "Page: -";
            else if (_frames.Count == 1)
                TxtPage.Text = "Page: 1 / 1";
        }
    }
}
