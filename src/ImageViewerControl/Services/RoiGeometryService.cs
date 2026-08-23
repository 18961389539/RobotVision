using System;
using System.Collections.Generic;
using System.Windows;
using ImageViewer.Models;
using ImageViewer.Utils;

namespace ImageViewer.Services
{
    public static class RoiGeometryService
    {
        public static bool IsRingDrawable(double outerRadius, double minimumDrawableSize)
        {
            return outerRadius > minimumDrawableSize;
        }

        public static double GetAdjustedRingInnerRadius(double outerRadius, double innerRadius, double minimumDrawableSize)
        {
            // 修复：与 ClampRingInnerRadius 逻辑完全相同，统一委托到后者避免重复实现。
            return ClampRingInnerRadius(innerRadius, outerRadius, minimumDrawableSize);
        }

        public static double ClampRingInnerRadius(double candidateInnerRadius, double outerRadius, double minimumDrawableSize)
        {
            return Math.Min(candidateInnerRadius, Math.Max(0, outerRadius - minimumDrawableSize));
        }

        public static bool ShouldClosePolygon(Point currentPos, Point startPos, double hitTestTolerance, double scale)
        {
            return GeometryUtils.Distance(currentPos, startPos) < (hitTestTolerance * 2) / scale;
        }

        public static bool ShouldAppendFreehandPolylinePoint(IReadOnlyList<Point> points, Point currentPos, double minimumDistance = 1)
        {
            return points.Count == 0 || GeometryUtils.Distance(points[points.Count - 1], currentPos) >= minimumDistance;
        }

        public static Rect GetBounds(RoiBase roi)
        {
            ArgumentNullException.ThrowIfNull(roi);

            return roi switch
            {
                CaliperMeasureRoi caliper => GeometryUtils.GetBoundingBox(BuildCaliperCorners(caliper)),
                RotatedRect rect => GeometryUtils.GetBoundingBox(new[]
                {
                    GeometryUtils.RotatePoint(new Point(rect.Center.X - rect.Width / 2, rect.Center.Y - rect.Height / 2), rect.Center, rect.Angle),
                    GeometryUtils.RotatePoint(new Point(rect.Center.X + rect.Width / 2, rect.Center.Y - rect.Height / 2), rect.Center, rect.Angle),
                    GeometryUtils.RotatePoint(new Point(rect.Center.X + rect.Width / 2, rect.Center.Y + rect.Height / 2), rect.Center, rect.Angle),
                    GeometryUtils.RotatePoint(new Point(rect.Center.X - rect.Width / 2, rect.Center.Y + rect.Height / 2), rect.Center, rect.Angle)
                }),
                EllipseRoi ellipse => GeometryUtils.GetBoundingBox(new[]
                {
                    GeometryUtils.RotatePoint(new Point(ellipse.Center.X - ellipse.RadiusX, ellipse.Center.Y - ellipse.RadiusY), ellipse.Center, ellipse.Angle),
                    GeometryUtils.RotatePoint(new Point(ellipse.Center.X + ellipse.RadiusX, ellipse.Center.Y - ellipse.RadiusY), ellipse.Center, ellipse.Angle),
                    GeometryUtils.RotatePoint(new Point(ellipse.Center.X + ellipse.RadiusX, ellipse.Center.Y + ellipse.RadiusY), ellipse.Center, ellipse.Angle),
                    GeometryUtils.RotatePoint(new Point(ellipse.Center.X - ellipse.RadiusX, ellipse.Center.Y + ellipse.RadiusY), ellipse.Center, ellipse.Angle)
                }),
                CircleRoi circle => new Rect(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Radius * 2, circle.Radius * 2),
                // 修复：补上 RingRoi 分支（此前缺失走默认分支返回 Rect.Empty，导致缩放/边界计算失效），
                // 参照 ImageAnalysisService.GetRoiBounds 的实现。
                RingRoi ring => new Rect(ring.Center.X - ring.OuterRadius, ring.Center.Y - ring.OuterRadius, ring.OuterRadius * 2, ring.OuterRadius * 2),
                PolygonRoi polygon when polygon.Points.Count > 0 => GeometryUtils.GetBoundingBox(polygon.Points),
                PolylineRoi polyline when polyline.Points.Count > 0 => GeometryUtils.GetBoundingBox(polyline.Points),
                PointAnnotationRoi point => new Rect(point.Position.X, point.Position.Y, 1, 1),
                TextAnnotationRoi text => new Rect(text.Position.X, text.Position.Y, 1, 1),
                LineMeasureRoi line => GeometryUtils.GetBoundingBox(new[] { line.P1, line.P2 }),
                AngleMeasureRoi angle => GeometryUtils.GetBoundingBox(new[] { angle.P1, angle.Vertex, angle.P2 }),
                ArcMeasureRoi arc => GeometryUtils.GetBoundingBox(new[] { arc.StartPoint, arc.EndPoint, arc.ArcPoint }),
                _ => Rect.Empty
            };
        }

        public static IEnumerable<Point> GetRepresentativePoints(RoiBase roi)
        {
            ArgumentNullException.ThrowIfNull(roi);

            return roi switch
            {
                CaliperMeasureRoi caliper => BuildCaliperCorners(caliper),
                RotatedRect rect => new[] { rect.Center },
                EllipseRoi ellipse => new[] { ellipse.Center },
                CircleRoi circle => new[] { circle.Center },
                // 修复：GetRepresentativePoints 同步补上 RingRoi，与 GetBounds 保持一致。
                RingRoi ring => new[] { ring.Center },
                PolygonRoi polygon => polygon.Points,
                PolylineRoi polyline => polyline.Points,
                PointAnnotationRoi point => new[] { point.Position },
                TextAnnotationRoi text => new[] { text.Position },
                LineMeasureRoi line => new[] { line.P1, line.P2 },
                AngleMeasureRoi angle => new[] { angle.P1, angle.Vertex, angle.P2 },
                ArcMeasureRoi arc => new[] { arc.StartPoint, arc.EndPoint, arc.ArcPoint },
                _ => Array.Empty<Point>()
            };
        }

        private static Point[] BuildCaliperCorners(CaliperMeasureRoi caliper)
        {
            caliper.EnsureCaliperRegion();
            Vector measurementDirection = caliper.GetCaliperMeasurementDirection();
            Vector caliperDirection = new(-measurementDirection.Y, measurementDirection.X);
            Point center = caliper.CaliperCenter;
            double halfSearchRange = caliper.CaliperSearchRange;
            double regionHalfLength = caliper.GetResolvedCaliperRegionLength() / 2;
            return
            [
                center - measurementDirection * halfSearchRange - caliperDirection * regionHalfLength,
                center - measurementDirection * halfSearchRange + caliperDirection * regionHalfLength,
                center + measurementDirection * halfSearchRange + caliperDirection * regionHalfLength,
                center + measurementDirection * halfSearchRange - caliperDirection * regionHalfLength
            ];
        }
    }
}
