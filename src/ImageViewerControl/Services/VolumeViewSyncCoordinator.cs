using System;
using ImageViewer.Controls;

namespace ImageViewer.Services
{
    public sealed class VolumeViewSyncCoordinator : IDisposable
    {
        private readonly VolumeViewer _sliceViewer;
        private bool _isDisposed;

        public VolumeViewSyncCoordinator(AdaptiveImageViewer adaptiveViewer)
        {
            ArgumentNullException.ThrowIfNull(adaptiveViewer);
            // 修复：SwitchToAxialSliceRequested 已由 AdaptiveImageViewer 自身在构造器中订阅并处理
            // （设置为 AxialSlice 显示模式），这里不再重复订阅，避免同一事件触发两次相同处理。
            _sliceViewer = adaptiveViewer.VolumeViewer;
        }

        public int CurrentSliceIndex => _sliceViewer.CurrentSliceIndex;

        public void SelectAxialSlice(int index)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _sliceViewer.SelectSlice(index);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
