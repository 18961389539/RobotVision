using RobotVision.Core.Assets;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方钉扎状态文案；哈希核对在 <see cref="Compute"/> 中显式执行，不放在绑定 getter。</summary>
internal static class AssetPinStatusText
{
    internal const string Unpinned =
        "未钉扎：拷错同名 ONNX 或覆盖标定档案时不会被拦截。验证通过后请钉死哈希。";

    internal static string Compute(AssetIntegrityChecker assets, RecipeConfig editor)
    {
        var pinnedModels = editor.ModelSha256.Count(h => !string.IsNullOrWhiteSpace(h));
        var pinnedStation = !string.IsNullOrWhiteSpace(editor.StationSha256);
        if (pinnedModels == 0 && !pinnedStation)
            return Unpinned;

        try
        {
            var (hashes, station) = assets.Snapshot(editor);
            var modelOk = true;
            for (var i = 0; i < editor.ModelSha256.Count; i++)
            {
                var pin = editor.ModelSha256[i];
                if (string.IsNullOrWhiteSpace(pin))
                    continue;
                var actual = i < hashes.Count ? hashes[i] : "";
                if (!FileSha256.EqualsHex(pin, actual))
                {
                    modelOk = false;
                    break;
                }
            }

            var stationOk = !pinnedStation ||
                FileSha256.EqualsHex(editor.StationSha256, station);
            if (modelOk && stationOk)
                return pinnedStation
                    ? $"已钉扎 {pinnedModels} 个模型 + 工位，与当前文件一致"
                    : $"已钉扎 {pinnedModels} 个模型，与当前文件一致";
            return "钉扎与当前文件不一致：TRIGGER 将返回 1017。请核对文件或重新钉扎后保存。";
        }
        catch (Exception ex)
        {
            return $"无法核对当前哈希：{ex.Message}";
        }
    }
}
