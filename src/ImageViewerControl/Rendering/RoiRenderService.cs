using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;
using ImageViewer.Models;
using ImageViewer.Plugins;

namespace ImageViewer.Rendering
{
    public sealed partial class RoiRenderService
    {
        private readonly RoiPluginRegistry _pluginRegistry;

        public RoiRenderService(RoiPluginRegistry? pluginRegistry = null)
        {
            _pluginRegistry = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));
        }

        internal static IReadOnlyDictionary<Type, IRoiRenderer> CreateBuiltInRendererMap()
        {
            return new Dictionary<Type, IRoiRenderer>
            {
                [typeof(RotatedRect)] = new RotatedRectRenderer(),
                [typeof(EllipseRoi)] = new EllipseRoiRenderer(),
                [typeof(FittedEllipseRoi)] = new FittedEllipseRoiRenderer(),
                [typeof(CircleRoi)] = new CircleRoiRenderer(),
                [typeof(RingRoi)] = new RingRoiRenderer(),
                [typeof(CircularCaliperMeasureRoi)] = new CircularCaliperMeasureRenderer(),
                [typeof(ArcCaliperMeasureRoi)] = new ArcCaliperMeasureRenderer(),
                [typeof(PolygonRoi)] = new PolygonRoiRenderer(),
                [typeof(BlobAnalysisRoi)] = new BlobAnalysisRenderer(),
                [typeof(PolylineRoi)] = new PolylineRoiRenderer(),
                [typeof(PointAnnotationRoi)] = new PointAnnotationRenderer(),
                [typeof(TextAnnotationRoi)] = new TextAnnotationRenderer(),
                [typeof(ArrowAnnotationRoi)] = new ArrowAnnotationRenderer(),
                [typeof(LineMeasureRoi)] = new LineMeasureRenderer(),
                [typeof(LineCaliperMeasureRoi)] = new LineCaliperMeasureRenderer(),
                [typeof(CaliperMeasureRoi)] = new CaliperMeasureRenderer(),
                [typeof(AngleMeasureRoi)] = new AngleMeasureRenderer(),
                [typeof(ArcMeasureRoi)] = new ArcMeasureRenderer(),
                [typeof(PointToLineDistanceRoi)] = new PointToLineDistanceRenderer(),
                [typeof(PointToCircleDistanceRoi)] = new PointToCircleDistanceRenderer(),
                [typeof(ParallelismMeasureRoi)] = new ParallelismMeasureRenderer(),
                [typeof(PerpendicularityMeasureRoi)] = new PerpendicularityMeasureRenderer(),
                [typeof(ConcentricityMeasureRoi)] = new ConcentricityMeasureRenderer()
            };
        }

        public void RenderCommitted(IEnumerable<RoiBase> rois, RoiRenderContext context, RoiBase? selectedRoi)
        {
            foreach (var roi in rois)
            {
                if (ReferenceEquals(roi, selectedRoi))
                {
                    continue;
                }

                TryRender(roi, context, null, false);
            }
        }

        public void RenderSelected(RoiBase? roi, RoiRenderContext context)
        {
            if (roi == null)
            {
                return;
            }

            TryRender(roi, context, Brushes.Red, true);
        }

        public void RenderActive(IEnumerable<RoiBase> rois, RoiRenderContext context)
        {
            foreach (var roi in rois)
            {
                TryRender(roi, context, Brushes.Orange, false);
            }
        }

        public void Render(RoiBase roi, RoiRenderContext context, Brush? strokeOverride, bool isSelected)
        {
            var plugin = _pluginRegistry.FindByRoi(roi);
            if (plugin == null)
            {
                // 修复：未知类型的 ROI 不再抛异常中断整批绘制，跳过并记录日志。
                Trace.WriteLine($"No ROI plugin registered for type '{roi.GetType().FullName}'; skipping render.");
                return;
            }

            plugin.Renderer.Render(roi, context, strokeOverride, isSelected);
        }

        private void TryRender(RoiBase roi, RoiRenderContext context, Brush? strokeOverride, bool isSelected)
        {
            try
            {
                Render(roi, context, strokeOverride, isSelected);
            }
            catch (Exception exception)
            {
                // 修复：单个 ROI 渲染异常不中断整批绘制，记录日志后继续下一个。
                Trace.WriteLine($"ROI rendering failed for type '{roi.GetType().FullName}': {exception}");
            }
        }
    }
}
