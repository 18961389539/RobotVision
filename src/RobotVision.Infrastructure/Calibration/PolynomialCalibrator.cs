using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 多项式标定（VisionPro 式单图模式）：一张棋盘格图 + 2 个同行参考角点的机器人坐标。
/// 流程：检测全部内角点（像素）→ 由参考点解棋盘平面刚体位姿（网格间距已知，推导全部
/// 角点的机器人坐标）→ SVD 最小二乘拟合"像素→机器人"多项式（二/三阶，归一化坐标）。
/// 一个模型整体吸收畸变/透视/安装角/像素当量，替代"内参去畸变 + 外参仿射"两步。
/// 棋盘行方向（ey）的两个候选符号 ±perp(ex) 都拟合，取 RMS 小者——用户无需声明棋盘朝向。
/// 角点像素坐标取检测值（亚像素），参考点在图上点选时由向导吸附到最近角点。
/// 另有 CalibrateImageSpace：免示教版本，目标坐标 = 棋盘平面毫米系（原点=首个角点，
/// 轴=棋盘行列方向），只解"像素→毫米"不做机器人系锚定。
/// </summary>
public static class PolynomialCalibrator
{
    /// <summary>最少网格点数（=3×3 棋盘；二阶 6 系数、三阶 10 系数均要求远多于此）。</summary>
    public const int MinPointCount = 9;

    /// <summary>
    /// 免示教单图标定（像素 → 棋盘平面毫米坐标）：
    /// 目标坐标直接由网格几何生成（索引×格距，原点=检测序号 0 的角点，x=列方向、y=行方向），
    /// 不需要任何机器人示教点。适合"只要像素转毫米、不锚定机器人系"的场景——
    /// 径向畸变/透视/安装旋转/像素当量/各向异性全部被多项式吸收，输出带 RMS 残差自检。
    /// 两个行方向候选（y 轴 ±）都拟合取 RMS 小者：角点检测序号的行走向与棋盘物理摆放无关，
    /// 用户仍无需声明朝向。
    /// 坐标系约定：x 沿角点序号增加方向（列），y 沿行方向——输出即"图像系毫米"语义
    /// （上/下方向可能翻转，由使用方按需取反，毫米量值不受影响）。
    /// </summary>
    public static PolynomialProfile CalibrateImageSpace(
        string stationId, string cameraId,
        Point2f[] cornerPixels, OpenCvSharp.Size patternSize, double squareMm,
        int width, int height, int order = 2)
    {
        ValidateCommon(cornerPixels, patternSize, squareMm, width, height, order);

        // 两种行方向候选（+grid / -grid）都生成目标网格并拟合，RMS 小者胜
        PolynomialProfile? best = null;
        for (var sign = 0; sign < 2; sign++)
        {
            var s = sign == 0 ? 1.0 : -1.0;
            var targets = new Point2f[cornerPixels.Length];
            var k = 0;
            for (var j = 0; j < patternSize.Height; j++)
                for (var i = 0; i < patternSize.Width; i++)
                    targets[k++] = new Point2f((float)(i * squareMm), (float)(s * j * squareMm));

            var candidate = Fit(stationId, cameraId, cornerPixels, targets, width, height, order)
                with { CoordinateSpace = PolynomialCoordinateSpace.Image };
            if (best is null || candidate.Rms < best.Rms)
                best = candidate;
        }

        return best!;
    }

    /// <summary>两种标定入口共用的入参校验。</summary>
    private static void ValidateCommon(
        Point2f[] cornerPixels, OpenCvSharp.Size patternSize, double squareMm,
        int width, int height, int order)
    {
        if (order is not (2 or 3))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"多项式阶数非法: {order}（仅支持 2 或 3）");
        if (squareMm <= 0 || !double.IsFinite(squareMm))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"棋盘方格边长非法: {squareMm}mm");
        if (width <= 0 || height <= 0)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"图像分辨率非法: {width}x{height}");
        var expected = patternSize.Width * patternSize.Height;
        if (cornerPixels.Length != expected)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"角点数量 {cornerPixels.Length} 与棋盘规格 {patternSize.Width}x{patternSize.Height} 不符");
        var coefficientCount = (order + 1) * (order + 2) / 2;
        if (cornerPixels.Length < coefficientCount + 4)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"网格点 {cornerPixels.Length} 不足以拟合 {order} 阶多项式（{coefficientCount} 系数，需 ≥{coefficientCount + 4} 点），请用更大棋盘或降阶");
    }

    public static PolynomialProfile Calibrate(
        string stationId, string cameraId,
        Point2f[] cornerPixels, OpenCvSharp.Size patternSize, double squareMm,
        Point2f refPixel1, Point2f refRobot1, Point2f refPixel2, Point2f refRobot2,
        int width, int height, int order = 2)
    {
        if (order is not (2 or 3))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"多项式阶数非法: {order}（仅支持 2 或 3）");
        if (squareMm <= 0 || !double.IsFinite(squareMm))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"棋盘方格边长非法: {squareMm}mm");
        if (width <= 0 || height <= 0)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"图像分辨率非法: {width}x{height}");
        var expected = patternSize.Width * patternSize.Height;
        if (cornerPixels.Length != expected)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"角点数量 {cornerPixels.Length} 与棋盘规格 {patternSize.Width}x{patternSize.Height} 不符");
        var coefficientCount = (order + 1) * (order + 2) / 2;
        if (cornerPixels.Length < coefficientCount + 4)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"网格点 {cornerPixels.Length} 不足以拟合 {order} 阶多项式（{coefficientCount} 系数，需 ≥{coefficientCount + 4} 点），请用更大棋盘或降阶");

        // 参考点吸附到最近检测角点（点选坐标仅用于定位，精度取角点检测值）
        var idx1 = NearestCornerIndex(cornerPixels, refPixel1);
        var idx2 = NearestCornerIndex(cornerPixels, refPixel2);
        if (idx1 == idx2)
            throw new VisionException(VisionErrorCode.NotCalibrated, "两个参考点吸附到同一角点，请选择不同角点");
        var (i1, j1) = (idx1 % patternSize.Width, idx1 / patternSize.Width);
        var (i2, j2) = (idx2 % patternSize.Width, idx2 / patternSize.Width);
        if (j1 != j2)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"两个参考角点必须同一棋盘行（当前行 {j1} / {j2}）：同行两点定 ex 方向，行方向由 RMS 自动判定");

        // ex：每列一格的机器人位移向量（参考点差 / 列差）
        var dx = (double)refRobot2.X - refRobot1.X;
        var dy = (double)refRobot2.Y - refRobot1.Y;
        var cellSpan = i2 - i1;
        if (cellSpan == 0)
            throw new VisionException(VisionErrorCode.NotCalibrated, "两个参考角点列差为 0");
        var exX = dx / cellSpan;
        var exY = dy / cellSpan;
        var cellLen = Math.Sqrt(exX * exX + exY * exY);
        if (Math.Abs(cellLen - squareMm) > 0.05 * squareMm)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"参考点间距与棋盘规格不符: 每格 {cellLen:0.000}，方格边长 {squareMm:0.000}（差 >5%）——请核对抄录的机器人坐标或边长参数");

        // 两个候选棋盘朝向（ey = ±perp(ex)）都推导全网格并拟合，取 RMS 小者
        PolynomialProfile? best = null;
        for (var sign = 0; sign < 2; sign++)
        {
            var s = sign == 0 ? 1.0 : -1.0;
            var eyX = -exY * s;
            var eyY = exX * s;

            var robots = new Point2f[cornerPixels.Length];
            for (var k = 0; k < cornerPixels.Length; k++)
            {
                var i = k % patternSize.Width;
                var j = k / patternSize.Width;
                robots[k] = new Point2f(
                    (float)(refRobot1.X + (i - i1) * exX + (j - j1) * eyX),
                    (float)(refRobot1.Y + (i - i1) * exY + (j - j1) * eyY));
            }

            var candidate = Fit(stationId, cameraId, cornerPixels, robots, width, height, order);
            if (best is null || candidate.Rms < best.Rms)
                best = candidate;
        }

        return best!;
    }

    /// <summary>SVD 最小二乘拟合：归一化像素坐标的 {order} 阶完备多项式 → 机器人 XY（两轴独立）。</summary>
    public static PolynomialProfile Fit(
        string stationId, string cameraId, Point2f[] pixels, Point2f[] robots,
        int width, int height, int order)
    {
        var n = pixels.Length;
        var m = (order + 1) * (order + 2) / 2;
        if (n < m + 2)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"拟合点 {n} 少于系数 {m}+2，多项式欠定（过拟合无意义）");

        // 设计矩阵：基函数顺序与 PolynomialProfile.Evaluate 一致（j 外层 i 内层，i+j≤order）
        using var a = new Mat(n, m, MatType.CV_64F);
        using var bx = new Mat(n, 1, MatType.CV_64F);
        using var by = new Mat(n, 1, MatType.CV_64F);
        for (var r = 0; r < n; r++)
        {
            var u = 2.0 * pixels[r].X / width - 1.0;
            var v = 2.0 * pixels[r].Y / height - 1.0;
            var c = 0;
            for (var j = 0; j <= order; j++)
                for (var i = 0; i + j <= order; i++)
                    a.Set(r, c++, Math.Pow(u, i) * Math.Pow(v, j));
            bx.Set(r, 0, (double)robots[r].X);
            by.Set(r, 0, (double)robots[r].Y);
        }

        using var coefX = new Mat();
        using var coefY = new Mat();
        if (!Cv2.Solve(a, bx, coefX, DecompTypes.SVD) || !Cv2.Solve(a, by, coefY, DecompTypes.SVD))
            throw new VisionException(VisionErrorCode.NotCalibrated, "多项式最小二乘求解失败：网格点共线或数据异常");

        var cx = new double[m];
        var cy = new double[m];
        for (var k = 0; k < m; k++)
        {
            cx[k] = coefX.At<double>(k, 0);
            cy[k] = coefY.At<double>(k, 0);
        }

        var profile = new PolynomialProfile
        {
            StationId = stationId,
            CameraId = cameraId,
            Width = width,
            Height = height,
            Order = order,
            CoefX = cx,
            CoefY = cy,
            PointCount = n,
        };

        // 残差评估（用求值路径闭环验证）
        double sumSq = 0, maxResidual = 0;
        for (var r = 0; r < n; r++)
        {
            var (mx, my) = profile.Evaluate(pixels[r].X, pixels[r].Y);
            var residual = Math.Sqrt((mx - robots[r].X) * (mx - robots[r].X) + (my - robots[r].Y) * (my - robots[r].Y));
            sumSq += residual * residual;
            maxResidual = Math.Max(maxResidual, residual);
        }

        return profile with { Rms = Math.Sqrt(sumSq / n), MaxResidual = maxResidual };
    }

    /// <summary>在检测角点集中找离目标最近的索引（参考点吸附）。</summary>
    public static int NearestCornerIndex(Point2f[] corners, Point2f target)
    {
        var best = 0;
        var bestDist = double.MaxValue;
        for (var k = 0; k < corners.Length; k++)
        {
            var d = corners[k].DistanceTo(target);
            if (d < bestDist)
            {
                bestDist = d;
                best = k;
            }
        }
        return best;
    }
}
