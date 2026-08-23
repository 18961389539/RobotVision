using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Controls
{
    public partial class VolumeViewer : UserControl, IDisposable, IAsyncDisposable
    {
        private VolumeData? _volume;
        private bool _isDisposed;

        // 修复：相邻切片小缓存，滚轮连续翻片时避免每帧重新生成同一张轴位片位图。
        private readonly Dictionary<int, BitmapSource> _sliceCache = new(4);
        private const int SliceCacheCapacity = 4;

        public VolumeViewer()
        {
            InitializeComponent();
            sliceViewer = new ImageViewer();
            sliceViewerHost.Content = sliceViewer;
            UpdateSliceState();
        }

        private readonly ImageViewer sliceViewer;

        public VolumeData? Volume
        {
            get => _volume;
            set
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                _volume = value;
                // 修复：切换体数据时清空切片缓存，避免残留旧体积的位图。
                _sliceCache.Clear();
                sliceSlider.Maximum = Math.Max(0, (value?.Depth ?? 1) - 1);
                sliceSlider.Value = 0;
                UpdateSliceState();
            }
        }

        public int CurrentSliceIndex => _volume == null ? -1 : (int)sliceSlider.Value;

        public event EventHandler? CurrentSliceChanged;

        public ImageViewer SliceViewer => sliceViewer;

        public void SelectSlice(int sliceIndex)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_volume == null)
            {
                throw new InvalidOperationException("A volume must be assigned before selecting a slice.");
            }

            if ((uint)sliceIndex >= (uint)_volume.Depth)
            {
                throw new ArgumentOutOfRangeException(nameof(sliceIndex));
            }

            sliceSlider.Value = sliceIndex;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            sliceViewer.Dispose();
            _volume = null;
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        private void OnSliceValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isDisposed)
            {
                UpdateSliceState();
                CurrentSliceChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_volume == null || e.Delta == 0)
            {
                return;
            }

            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 5 : 1;
            int direction = e.Delta > 0 ? 1 : -1;
            int target = Math.Clamp(CurrentSliceIndex + direction * step, 0, _volume.Depth - 1);
            if (target != CurrentSliceIndex)
            {
                SelectSlice(target);
            }

            e.Handled = true;
        }

        private void UpdateSliceState()
        {
            if (_volume == null)
            {
                sliceViewer.ImageSource = null;
                sliceStatusText.Text = "No volume";
                return;
            }

            int sliceIndex = CurrentSliceIndex;
            // 修复：翻片时复用相邻切片缓存，避免每帧重新生成位图。
            sliceViewer.SetImage(GetCachedSlice(sliceIndex));
            sliceStatusText.Text = $"Axial slice {sliceIndex + 1}/{_volume.Depth}";
        }

        /// <summary>
        /// 取轴位切片，带小容量缓存（满时清空，相邻翻片场景命中率高）。
        /// </summary>
        private BitmapSource GetCachedSlice(int sliceIndex)
        {
            if (_sliceCache.TryGetValue(sliceIndex, out BitmapSource? cached))
            {
                return cached;
            }

            BitmapSource slice = VolumeSliceService.GetSlice(_volume!, VolumeSliceOrientation.Axial, sliceIndex);
            if (_sliceCache.Count >= SliceCacheCapacity)
            {
                _sliceCache.Clear();
            }

            _sliceCache[sliceIndex] = slice;
            return slice;
        }
    }
}
