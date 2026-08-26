using System;
using System.IO;
using System.Text.Json;

namespace ImageViewer.Controls
{
    /// <summary>浮动工具栏显示偏好（跨页面、跨会话）。</summary>
    internal static class ImageViewerToolbarPreferences
    {
        private static bool _loaded;
        private static bool _showToolbar;

        public static bool ShowToolbar
        {
            get
            {
                EnsureLoaded();
                return _showToolbar;
            }
            set
            {
                EnsureLoaded();
                if (_showToolbar == value)
                    return;

                _showToolbar = value;
                Save();
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            Load();
            _loaded = true;
        }

        private static string PreferenceFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ImageViewer",
                "toolbar-prefs.json");

        private static void Load()
        {
            try
            {
                if (!File.Exists(PreferenceFilePath))
                {
                    _showToolbar = false;
                    return;
                }

                var json = File.ReadAllText(PreferenceFilePath);
                var prefs = JsonSerializer.Deserialize<ToolbarPrefsDocument>(json);
                _showToolbar = prefs?.ShowToolbar ?? false;
            }
            catch
            {
                _showToolbar = false;
            }
        }

        private static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(PreferenceFilePath)!;
                Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(new ToolbarPrefsDocument { ShowToolbar = _showToolbar });
                File.WriteAllText(PreferenceFilePath, json);
            }
            catch
            {
                // 偏好写入失败不影响查看器使用
            }
        }

        private sealed class ToolbarPrefsDocument
        {
            public bool ShowToolbar { get; set; }
        }
    }
}
