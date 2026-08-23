using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageViewer.Models;

namespace ImageViewer.Services
{
    public enum VolumeSliceOrientation
    {
        Axial,
        Coronal,
        Sagittal
    }

    public sealed class VolumeSliceService
    {
        public static BitmapSource GetSlice(VolumeData volume, VolumeSliceOrientation orientation, int sliceIndex)
        {
            ArgumentNullException.ThrowIfNull(volume);
            return orientation switch
            {
                VolumeSliceOrientation.Axial => volume.GetAxialSlice(sliceIndex),
                VolumeSliceOrientation.Coronal => BuildCoronalSlice(volume, sliceIndex),
                VolumeSliceOrientation.Sagittal => BuildSagittalSlice(volume, sliceIndex),
                _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Unsupported slice orientation.")
            };
        }

        private static BitmapSource BuildCoronalSlice(VolumeData volume, int sliceIndex)
        {
            if ((uint)sliceIndex >= (uint)volume.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(sliceIndex));
            }

            // 修复：整层一次 CopyPixels 到缓冲后内存重排生成目标切片，
            // 取代原先对每个像素调用 CopyPixels（每像素一次 COM 调用，512³ 体素性能灾难）。
            int outputWidth = volume.Width;
            int outputHeight = volume.Depth;
            byte[] output = new byte[outputWidth * outputHeight];

            for (int y = 0; y < volume.Depth; y++)
            {
                // 该冠状面输出行 y 取自第 y 张轴位片的第 sliceIndex 行
                byte[] slicePixels = CopySliceToGrayBuffer(volume.GetAxialSlice(y), out int rowStride);
                int sourceRowOffset = sliceIndex * rowStride;
                int destinationRowOffset = y * outputWidth;
                for (int x = 0; x < outputWidth; x++)
                {
                    output[destinationRowOffset + x] = slicePixels[sourceRowOffset + x];
                }
            }

            return CreateFrozenGray8(output, outputWidth, outputHeight);
        }

        private static BitmapSource BuildSagittalSlice(VolumeData volume, int sliceIndex)
        {
            if ((uint)sliceIndex >= (uint)volume.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(sliceIndex));
            }

            // 修复：整层一次 CopyPixels 到缓冲后内存重排，避免逐像素 COM 调用。
            int outputWidth = volume.Depth;
            int outputHeight = volume.Height;
            byte[] output = new byte[outputWidth * outputHeight];

            for (int x = 0; x < volume.Depth; x++)
            {
                // 该矢状面输出列 x 取自第 x 张轴位片的第 sliceIndex 列
                byte[] slicePixels = CopySliceToGrayBuffer(volume.GetAxialSlice(x), out int rowStride);
                int destinationColumnOffset = x * outputHeight;
                for (int y = 0; y < outputHeight; y++)
                {
                    output[destinationColumnOffset + y] = slicePixels[y * rowStride + sliceIndex];
                }
            }

            return CreateFrozenGray8(output, outputWidth, outputHeight);
        }

        private static BitmapSource CreateFrozenGray8(byte[] pixels, int width, int height)
        {
            BitmapSource result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
            result.Freeze();
            return result;
        }

        /// <summary>
        /// 将单张轴位片整层拷贝并统一为 Gray8 单字节像素缓冲，stride 即宽度。
        /// </summary>
        private static byte[] CopySliceToGrayBuffer(BitmapSource slice, out int stride)
        {
            int width = slice.PixelWidth;
            int height = slice.PixelHeight;

            if (slice.Format == PixelFormats.Gray8)
            {
                stride = width;
                byte[] gray = new byte[width * height];
                slice.CopyPixels(gray, stride, 0);
                return gray;
            }

            int bytesPerPixel = Math.Max(1, (slice.Format.BitsPerPixel + 7) / 8);
            int sourceStride = width * bytesPerPixel;
            byte[] source = new byte[height * sourceStride];
            slice.CopyPixels(source, sourceStride, 0);

            byte[] grayPixels = new byte[width * height];
            for (int index = 0; index < source.Length; index += bytesPerPixel)
            {
                int grayIndex = index / bytesPerPixel;
                grayPixels[grayIndex] = bytesPerPixel >= 3
                    ? (byte)(source[index] * 0.114 + source[index + 1] * 0.587 + source[index + 2] * 0.299)
                    : source[index];
            }

            stride = width;
            return grayPixels;
        }
    }
}
