using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;
using ImageViewer.Plugins;

namespace ImageViewer.Services
{
    public static class RoiPersistenceService
    {
        private const int CurrentDocumentVersion = 1;
        

        public static void SaveToFile(string filePath, IEnumerable<RoiBase> rois, double pixelSize, string? physicalUnit, RoiPluginRegistry? pluginRegistry = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(rois);
            ArgumentNullException.ThrowIfNull(pluginRegistry);

            File.WriteAllText(filePath, Serialize(rois, pixelSize, physicalUnit, pluginRegistry));
        }

        public static Task SaveToFileAsync(string filePath, IEnumerable<RoiBase> rois, double pixelSize, string? physicalUnit, RoiPluginRegistry? pluginRegistry = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(rois);
            ArgumentNullException.ThrowIfNull(pluginRegistry);

            return File.WriteAllTextAsync(filePath, Serialize(rois, pixelSize, physicalUnit, pluginRegistry), cancellationToken);
        }

        public static string Serialize(IEnumerable<RoiBase> rois, double pixelSize, string? physicalUnit, RoiPluginRegistry? pluginRegistry = null)
        {
            ArgumentNullException.ThrowIfNull(rois);
            var roiPlugins = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));

            var document = new RoiDocument
            {
                Version = CurrentDocumentVersion,
                PixelSize = pixelSize,
                PhysicalUnit = string.IsNullOrWhiteSpace(physicalUnit) ? "px" : physicalUnit,
                // 修复：保存时未知类型跳过并记录日志，不再抛异常中断整批导出。
                // OfType 在过滤 null 的同时把类型收窄为非空，消除可空性警告。
                Items = rois
                    .Select(roi => CreateItem(roi, roiPlugins))
                    .OfType<RoiPersistenceData>()
                    .ToList()
            };

            return JsonSerializer.Serialize(document, ImageViewerJsonSerializationContext.Default.RoiDocument);
        }

        public static (IReadOnlyList<RoiBase> Rois, double PixelSize, string PhysicalUnit) LoadFromFile(string filePath, RoiPluginRegistry? pluginRegistry = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(pluginRegistry);

            return Deserialize(File.ReadAllText(filePath), pluginRegistry);
        }

        public static async Task<(IReadOnlyList<RoiBase> Rois, double PixelSize, string PhysicalUnit)> LoadFromFileAsync(string filePath, RoiPluginRegistry? pluginRegistry = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(pluginRegistry);

            return Deserialize(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false), pluginRegistry);
        }

        public static (IReadOnlyList<RoiBase> Rois, double PixelSize, string PhysicalUnit) Deserialize(string json, RoiPluginRegistry? pluginRegistry = null)
        {
            ArgumentNullException.ThrowIfNull(json);
            var roiPlugins = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));

            var document = JsonSerializer.Deserialize(json, ImageViewerJsonSerializationContext.Default.RoiDocument) ?? new RoiDocument();
            var rois = document.Items
                .Select(item => CreateRoi(item, roiPlugins))
                .Where(roi => roi != null)
                .Cast<RoiBase>()
                .ToList();

            return (
                rois,
                document.PixelSize <= 0 ? 1.0 : document.PixelSize,
                string.IsNullOrWhiteSpace(document.PhysicalUnit) ? "px" : document.PhysicalUnit);
        }

        private static RoiPersistenceData? CreateItem(RoiBase roi, RoiPluginRegistry roiPlugins)
        {
            var plugin = roiPlugins.FindByRoi(roi);
            if (plugin == null)
            {
                // 修复：未知类型跳过并记日志，与加载侧策略保持一致，避免整批保存因单个 ROI 失败。
                System.Diagnostics.Trace.WriteLine($"Skipping persistence for unregistered ROI type '{roi.GetType().FullName}'.");
                return null;
            }

            var item = new RoiPersistenceData();
            item.PopulateCommonState(roi, plugin.TypeKey);
            plugin.PopulatePersistenceData(roi, item);
            return item;
        }

        private static RoiBase? CreateRoi(RoiPersistenceData item, RoiPluginRegistry roiPlugins)
        {
            var plugin = ResolvePlugin(item, roiPlugins);
            if (plugin == null)
            {
                return null;
            }

            var roi = plugin.CreateRoi(item);
            roi.ApplyCommonState(item);
            return roi;
        }

        private static IRoiPlugin? ResolvePlugin(RoiPersistenceData item, RoiPluginRegistry roiPlugins)
        {
            if (string.IsNullOrWhiteSpace(item.Type))
            {
                // 修复：加载时未知类型跳过并记日志，保持与保存侧一致，不中断整批加载。
                System.Diagnostics.Trace.WriteLine("Skipping ROI persistence item with missing type key.");
                return null;
            }

            var plugin = roiPlugins.FindByTypeKey(item.Type)
                ?? roiPlugins.Plugins.FirstOrDefault(plugin => string.Equals(plugin.RoiType.Name, item.Type, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                // 修复：未知类型跳过并记日志，避免加载到一半因单个未知类型抛异常。
                System.Diagnostics.Trace.WriteLine($"Skipping ROI persistence item with unknown type '{item.Type}'.");
            }

            return plugin;
        }

    }
}
