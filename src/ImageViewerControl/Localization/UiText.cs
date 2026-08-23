using System;
using System.Globalization;
using System.Resources;

namespace ImageViewer.Localization
{
    public static class UiText
    {
        private static readonly ResourceManager ResourceManager = new("ImageViewerControl.Resources.UiText", typeof(UiText).Assembly);

        public static string Get(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            // 修复：缺 key 时返回 key 本身而非抛异常，避免因资源缺失导致 UI 崩溃；
            // 便于在运行时直观发现缺失项（显示为 key 文本）。
            return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }

        public static string Format(string key, params object?[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args);
        }

        public static string FormatInvariant(string key, params object?[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }
    }
}