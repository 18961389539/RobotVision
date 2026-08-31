using System.Collections.Concurrent;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 标定档案种类描述符：把"每类档案与众不同的部分"收敛成一份元数据，
/// 使 载入/保存/删除/目录扫描 只实现一次。
/// <para>
/// 重构背景：原先外参、旋转中心、多项式、比例四类档案各有一份近乎复制粘贴的
/// Validate/Load/Save/Delete/Assess 实现（23 个方法体结构完全同构），
/// 改一处要同步改四处，且 LoadDirectory 的目录扫描逻辑重复了五遍。
/// </para>
/// <para>
/// 适用范围：仅纯 JSON 档案。内参（<c>IntrinsicProfile</c>）在载入时要即时构建
/// OpenCV 去畸变映射表、删除/释放时要 Dispose 非托管 Mat、全程受读写锁保护，
/// 生命周期语义与其余四类完全不同，强行塞进泛型仓会让仓储被迫了解 OpenCV 与锁，
/// 因此内参不在此列（保留在 <see cref="CalibrationManager"/> 内）。
/// </para>
/// </summary>
/// <typeparam name="TProfile">档案类型。</typeparam>
internal interface IJsonProfileKind<TProfile> where TProfile : class
{
    /// <summary>文件名中缀，落盘为 <c>{Id}.{Kind}.json</c>，如 "extrinsic"。</summary>
    string Kind { get; }

    /// <summary>档案主键：外参/旋转中心/多项式/比例均为 StationId（内参为 CameraId）。</summary>
    string IdOf(TProfile profile);

    /// <summary>值域校验：非法档案拒绝加载，抛 <see cref="VisionException"/>。</summary>
    void Validate(TProfile profile);

    /// <summary>质量评估（与 README 验收参考对齐），供 UI 与载入告警共用。</summary>
    CalibrationQuality Assess(TProfile profile);

    /// <summary>载入后的质量告警；该种类无告警时实现为空。</summary>
    void CheckQuality(TProfile profile, Action<string> warn);
}

/// <summary>
/// 纯 JSON 标定档案的泛型仓储：字典存取、值域校验、质量告警、目录扫描去重，全部只实现一次。
/// <para>
/// 线程安全：<see cref="ConcurrentDictionary{TKey, TValue}"/> 保证单个档案的存取原子性。
/// 跨档案的复合判断（如"同一工位是否并存多项式与外参"）由 <see cref="CalibrationManager"/>
/// 在调用方编排，仓储不感知其他种类。
/// </para>
/// </summary>
/// <typeparam name="TProfile">档案类型。</typeparam>
internal sealed class JsonProfileStore<TProfile> where TProfile : class
{
    private readonly IJsonProfileKind<TProfile> _kind;
    private readonly ConcurrentDictionary<string, TProfile> _items = new(StringComparer.OrdinalIgnoreCase);

    public JsonProfileStore(IJsonProfileKind<TProfile> kind) => _kind = kind;

    /// <summary>已加载档案数。</summary>
    public int Count => _items.Count;

    /// <summary>文件名中缀（转发描述符，供目录扫描与落盘拼路径）。</summary>
    public string Kind => _kind.Kind;

    /// <summary>已加载档案（无序）。需要稳定展示序的调用方自行 OrderBy。</summary>
    public IEnumerable<TProfile> Values => _items.Values;

    /// <summary>档案主键（目录扫描去重时不必把描述符暴露出去）。</summary>
    public string IdOf(TProfile profile) => _kind.IdOf(profile);

    /// <summary>是否存在该 Id 的档案（Id 为空时返回 false，与原先 HasPolynomial 等一致）。</summary>
    public bool Contains(string? id) => !string.IsNullOrEmpty(id) && _items.ContainsKey(id);

    /// <summary>按 Id 取档案，无档案返回 null。</summary>
    public TProfile? Get(string? id) =>
        string.IsNullOrEmpty(id) ? null : _items.TryGetValue(id, out var p) ? p : null;

    /// <summary>
    /// 校验 → 入字典 → 质量告警。与原先 LoadExtrinsic/LoadPolynomial/LoadScale 语义一致
    /// （Validate 在最前，非法档案不会污染内存字典）。
    /// </summary>
    public void Load(TProfile profile, Action<string> warn)
    {
        _kind.Validate(profile);
        _items[_kind.IdOf(profile)] = profile;
        _kind.CheckQuality(profile, warn);
    }

    /// <summary>
    /// 落盘 + 立即热加载（无需重启）。
    /// <para>
    /// 行为统一说明：原先 <c>SaveExtrinsic</c>/<c>SaveRotationCenter</c> 是"先写文件、再由 Load 校验"，
    /// 非法档案会先落盘成坏文件再抛异常；而 <c>SaveIntrinsic</c>/<c>SavePolynomial</c>/<c>SaveScale</c>
    /// 是"先校验再落盘"。此处统一为<b>先校验后落盘</b>——落盘坏文件是明显更糟的结果
    /// （重启后档案不可用）。该差异在生产代码与测试中都无调用点依赖，统一是安全的。
    /// </para>
    /// </summary>
    public void Save(
        TProfile profile,
        Action<string> warn,
        Func<string, string, string> profileFile,
        Action<string, object> writeJson)
    {
        _kind.Validate(profile);
        writeJson(profileFile(_kind.Kind, _kind.IdOf(profile)), profile);
        Load(profile, warn);
    }

    /// <summary>
    /// 内存与文件一并删除。返回值沿用原语义：<b>仅表示文件删除是否成功</b>，
    /// 内存字典里本来就没有该档案时不视为失败。
    /// </summary>
    public bool Delete(string id, Func<string, string, bool> deleteProfileFile)
    {
        _items.TryRemove(id, out _);
        return deleteProfileFile(id, _kind.Kind);
    }

    /// <summary>质量评估转发（供 CalibrationManager 的 public static Assess* 门面调用）。</summary>
    public CalibrationQuality Assess(TProfile profile) => _kind.Assess(profile);

    /// <summary>校验转发（供 CalibrationManager 的 public static Validate* 门面调用）。</summary>
    public void Validate(TProfile profile) => _kind.Validate(profile);
}
