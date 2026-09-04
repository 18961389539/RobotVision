using OpenCvSharp;

namespace RobotVision.Vision;

public static partial class MaskShapeMatch
{
    /// <summary>单次转正窗 Chamfer 距离场/方向图，供多轮 Search 与 NCC 兜底复用。</summary>
    private sealed class ChamferField : IDisposable
    {
        internal Mat Gray { get; }
        internal Mat Dt { get; }
        internal Mat DirMap { get; }
        internal float GradPeak { get; private set; }
        internal bool CanonMapped { get; }
        internal double InvSx { get; }
        internal double InvSy { get; }
        internal double TeachSx { get; }
        internal double TeachSy { get; }
        internal ChamferScale Scale { get; }
        internal double SceneWarpDeg { get; }
        internal float[] DtFlat { get; }
        internal byte[] DirFlat { get; }
        internal MatIndexer<float>? GradIdx { get; private set; }
        internal int W => Dt.Cols;
        internal int H => Dt.Rows;

        private Mat? _gradMag;

        private ChamferField(
            Mat gray, Mat dt, Mat dirMap, float gradPeak,
            bool canonMapped, double invSx, double invSy, ChamferScale scale,
            double sceneWarpDeg, float[] dtFlat, byte[] dirFlat)
        {
            Gray = gray;
            Dt = dt;
            DirMap = dirMap;
            GradPeak = gradPeak;
            CanonMapped = canonMapped;
            InvSx = invSx;
            InvSy = invSy;
            TeachSx = canonMapped ? 1.0 / invSx : 1.0;
            TeachSy = canonMapped ? 1.0 / invSy : 1.0;
            Scale = scale;
            SceneWarpDeg = sceneWarpDeg;
            DtFlat = dtFlat;
            DirFlat = dirFlat;
        }

        internal void EnsureGrad()
        {
            if (_gradMag is not null || Math.Abs(SceneWarpDeg) < EdgeDilateWarpDeg)
                return;
            _gradMag = BuildGradientMagnitude(Gray, out var peak);
            GradPeak = peak;
            GradIdx = _gradMag.GetGenericIndexer<float>();
        }

        internal static ChamferField Create(
            Mat upright, ShapeModel model, double sceneWarpDeg,
            IReadOnlyList<Point2f>? contourInUpright = null)
        {
            Mat? gray = PrepareMatchGray(upright, model, sceneWarpDeg, out var canonMapped, out var invSx, out var invSy);
            Mat? dt = null;
            Mat? dirMap = null;
            try
            {
                var scale = ChamferScale.ForMatch(upright.Width, upright.Height, model, sceneWarpDeg, canonMapped);
                using var edges = BuildChamferEdges(gray, sceneWarpDeg);
                OrUprightContour(edges, contourInUpright, canonMapped, invSx, invSy);
                using var edgesForDt = DilateEdgesForWarp(edges, sceneWarpDeg);
                using var inv = new Mat();
                Cv2.BitwiseNot(edgesForDt, inv);
                dt = new Mat();
                Cv2.DistanceTransform(inv, dt, DistanceTypes.L2, DistanceTransformMasks.Mask3);
                if (dt.Empty() || dt.Type() != MatType.CV_32FC1)
                    throw new InvalidOperationException("Chamfer 距离场构建失败");

                dirMap = BuildDirMap(gray);

                var field = new ChamferField(
                    gray, dt, dirMap, 0f, canonMapped, invSx, invSy, scale,
                    sceneWarpDeg, FlattenDt(dt), FlattenDir(dirMap));
                gray = null;
                dt = null;
                dirMap = null;
                return field;
            }
            finally
            {
                gray?.Dispose();
                dt?.Dispose();
                dirMap?.Dispose();
            }
        }

        public void Dispose()
        {
            Gray.Dispose();
            Dt.Dispose();
            DirMap.Dispose();
            _gradMag?.Dispose();
        }
    }

    private static float[] FlattenDt(Mat dt)
    {
        var arr = new float[dt.Rows * dt.Cols];
        dt.GetArray(out float[]? raw);
        if (raw is not null && raw.Length == arr.Length)
            Array.Copy(raw, arr, arr.Length);
        else
        {
            var idx = dt.GetGenericIndexer<float>();
            var k = 0;
            for (var y = 0; y < dt.Rows; y++)
            for (var x = 0; x < dt.Cols; x++)
                arr[k++] = idx[y, x];
        }

        return arr;
    }

    private static byte[] FlattenDir(Mat dirMap)
    {
        var arr = new byte[dirMap.Rows * dirMap.Cols];
        dirMap.GetArray(out byte[]? raw);
        if (raw is not null && raw.Length == arr.Length)
            Array.Copy(raw, arr, arr.Length);
        else
        {
            var idx = dirMap.GetGenericIndexer<byte>();
            var k = 0;
            for (var y = 0; y < dirMap.Rows; y++)
            for (var x = 0; x < dirMap.Cols; x++)
                arr[k++] = idx[y, x];
        }

        return arr;
    }
}
