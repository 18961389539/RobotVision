using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageViewer.Controls
{
    public partial class ImageViewer
    {
        private bool _isApplyingToolbarPreference;

        internal void ApplyToolbarPreference()
        {
            _isApplyingToolbarPreference = true;
            try
            {
                IsToolbarVisible = ImageViewerToolbarPreferences.ShowToolbar;
            }
            finally
            {
                _isApplyingToolbarPreference = false;
            }
        }

        public void ToggleFloatingToolbar()
        {
            IsToolbarVisible = !IsToolbarVisible;
        }
        /// <summary>将鼠标位置换算为图像像素坐标；点在图像外返回 false。</summary>
        public bool TryHitImage(MouseEventArgs e, out Point imagePoint)
        {
            imagePoint = default;
            if (ImageSource is not BitmapSource bitmap)
                return false;

            var position = e.GetPosition(imageContainer);
            if (position.X < 0 || position.Y < 0 ||
                position.X >= bitmap.PixelWidth || position.Y >= bitmap.PixelHeight)
                return false;

            imagePoint = position;
            return true;
        }
    }
}
