using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>四类 JSON 映射档案的载入/保存/删除与双档案并存编排。</summary>
internal sealed class JsonProfileCoordinator
{
    private readonly CalibrationStores _stores;

    public JsonProfileCoordinator(CalibrationStores stores) => _stores = stores;

    public void LoadExtrinsic(ExtrinsicProfile profile)
    {
        _stores.Extrinsics.Load(profile, _stores.AddQualityWarning);
        WarnIfDualMapping(profile.StationId);
    }

    public void LoadRotationCenter(RotationCenterProfile profile) =>
        _stores.RotationCenters.Load(profile, _stores.AddQualityWarning);

    public void LoadPolynomial(PolynomialProfile profile)
    {
        _stores.Polynomials.Load(profile, _stores.AddQualityWarning);
        WarnIfDualMapping(profile.StationId);
    }

    public void LoadScale(ScaleProfile profile)
    {
        _stores.Scales.Load(profile, _stores.AddQualityWarning);
        WarnIfDualMapping(profile.StationId);
    }

    public void SaveExtrinsic(ExtrinsicProfile profile)
    {
        _stores.RequireFolder();
        EnsureCanSaveMapping(profile.StationId, StationMappingMode.Extrinsic);
        _stores.Extrinsics.Save(profile, _stores.AddQualityWarning, _stores.ProfileFile, _stores.WriteJson);
        WarnIfDualMapping(profile.StationId);
    }

    public void SaveRotationCenter(RotationCenterProfile profile)
    {
        _stores.RequireFolder();
        _stores.RotationCenters.Save(profile, _stores.AddQualityWarning, _stores.ProfileFile, _stores.WriteJson);
    }

    public void SavePolynomial(PolynomialProfile profile)
    {
        _stores.RequireFolder();
        EnsureCanSaveMapping(profile.StationId, StationMappingMode.Polynomial);
        _stores.Polynomials.Save(profile, _stores.AddQualityWarning, _stores.ProfileFile, _stores.WriteJson);
        WarnIfDualMapping(profile.StationId);
    }

    public void SaveScale(ScaleProfile profile)
    {
        _stores.RequireFolder();
        EnsureCanSaveMapping(profile.StationId, StationMappingMode.Scale);
        _stores.Scales.Save(profile, _stores.AddQualityWarning, _stores.ProfileFile, _stores.WriteJson);
        WarnIfDualMapping(profile.StationId);
    }

    public bool DeleteExtrinsic(string stationId) =>
        _stores.Extrinsics.Delete(stationId, _stores.DeleteProfileFile);

    public bool DeleteRotationCenter(string stationId) =>
        _stores.RotationCenters.Delete(stationId, _stores.DeleteProfileFile);

    public bool DeletePolynomial(string stationId) =>
        _stores.Polynomials.Delete(stationId, _stores.DeleteProfileFile);

    public bool DeleteScale(string stationId) =>
        _stores.Scales.Delete(stationId, _stores.DeleteProfileFile);

    private void WarnIfDualMapping(string stationId)
    {
        if (_stores.Polynomials.Contains(stationId) && _stores.Extrinsics.Contains(stationId))
            _stores.AddQualityWarning($"工位 {stationId} 同时存在多项式与外参档案：生产优先使用多项式（原图+多项式映射），外参/去畸变被忽略。请删除不用的那份以免坐标系混淆");
        if (_stores.Scales.Contains(stationId) &&
            (_stores.Polynomials.Contains(stationId) || _stores.Extrinsics.Contains(stationId)))
            _stores.AddQualityWarning($"工位 {stationId} 同时存在比例与外参/多项式档案：管线优先使用外参/多项式（机器人系坐标），比例档案仅用于测量显示");
    }

    private void EnsureCanSaveMapping(string stationId, StationMappingMode incoming)
    {
        var hasPoly = _stores.Polynomials.Contains(stationId);
        var hasExt = _stores.Extrinsics.Contains(stationId);
        var hasScale = _stores.Scales.Contains(stationId);
        var overwriting = incoming switch
        {
            StationMappingMode.Polynomial => hasPoly,
            StationMappingMode.Extrinsic => hasExt,
            StationMappingMode.Scale => hasScale,
            _ => false,
        };
        if (overwriting)
            return;

        if (incoming == StationMappingMode.Polynomial && hasExt)
            throw new InvalidOperationException(
                $"工位 {stationId} 已有外参档案：保存多项式后生产只会用多项式，外参被忽略。请先删除外参再保存");
        if (incoming == StationMappingMode.Extrinsic && hasPoly)
            throw new InvalidOperationException(
                $"工位 {stationId} 已有多项式档案：保存外参后生产仍优先多项式。请先删除多项式再保存外参");
        if (incoming == StationMappingMode.Polynomial && hasScale)
            throw new InvalidOperationException(
                $"工位 {stationId} 已有比例档案：保存多项式后比例不参与管线。请先删除比例再保存");
        if (incoming == StationMappingMode.Extrinsic && hasScale)
            throw new InvalidOperationException(
                $"工位 {stationId} 已有比例档案：保存外参后比例不参与管线。请先删除比例再保存");
        if (incoming == StationMappingMode.Scale && hasPoly)
            throw new InvalidOperationException(
                $"工位 {stationId} 已有多项式档案：比例不参与管线。请先删除多项式再保存比例");
        if (incoming == StationMappingMode.Scale && hasExt)
            throw new InvalidOperationException(
                $"工位 {stationId} 已有外参档案：比例不参与管线。请先删除外参再保存比例");
    }
}
