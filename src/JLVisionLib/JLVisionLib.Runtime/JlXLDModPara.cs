using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an XLD modified parallel object(-array).</summary>
[Serializable]
public class JlXLDModPara : JlXLD, ISerializable, ICloneable
{
	/// <summary>按序号取出本容器中的一条或几条元素，逐字转发给 <see cref="SelectObj(JlTuple)"/>，返回新容器。</summary>
	/// <param name="index">元素序号。Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b>：托管侧只发一次 SelectObj(JlTuple)（原生 id 572），原容器不改动；元素是 ModParallelsXld 产出的 xld_mod_para 平行线对，不是普通轮廓。</para>
	///   <para><b>约束或前提</b>：形参是 JlTuple，写 mods[1] 时靠 int 隐式转换；一次取多条要显式 new JlTuple(1, 2)。包装层不做序号换算，基数与越界行为同 SelectObj [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：连续区段用 CopyObj(index, numObj) 少造一次元组；按特征筛用 SelectShapeXld、按过点筛用 SelectXldPoint，二者不依赖上游顺序。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara first = mods[1];
	///   using JlXLDModPara two = mods[new JlTuple(1, 2)];
	///   </code>
	///   <para><b>资源与坑</b>：每次索引都返回新容器，需各自 Dispose；out 出来的 ext 也是新句柄别漏释放。SelectObj 走 LoadNew，不做 xld_mod_para 对象类断言。</para>
	/// </remarks>
	public new JlXLDModPara this[JlTuple index] => SelectObj(index);

	/// <summary>造一个句柄为 UNDEF 的空容器，仅作 Deserialize/DeserializeXld/Clone 装载前的接收位。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：转调 base(JlObjectBase.UNDEF, copy: false)，不发任何原生调用；本类没有 JlXLDModPara(bool) 这种重载。</para>
	///   <para><b>约束或前提</b>：空句柄上 AssertObjectClass 的 xld_mod_para 断言整体跳过，所以本构造器与 LoadNew 造出的容器都不校验原生类，装错类型不会在托管层被拦下；空容器不能当图标输入参与算子（零句柄会直接交给原生，报错形式由原生决定 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>：要真实内容走 JlXLDPara.ModParallelsXld 或本类 Deserialize(Stream)（内部就是本构造器 + DeserializeXld）；只要副本用 Clone()；自己接管原生句柄才用 IntPtr 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlXLDModPara pending = new JlXLDModPara();
	///   bool ready = pending.IsInitialized();
	///   </code>
	///   <para><b>资源与坑</b>：JlObjectBase.Load 要求实例句柄必须是 UNDEF，否则抛 JlException("Undisposed object instance when loading output parameter")——DeserializeXld 内部先 Dispose 正是为此。</para>
	/// </remarks>
	public JlXLDModPara()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDModPara(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDModPara(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>由同族 <see cref="JlObject"/> 对象复制构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDModPara(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "xld_mod_para");
	}

	/// <summary>内部工厂：从算子的输出对象槽装载本类新实例；<paramref name="err"/> 为调用错误码并原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDModPara obj)
	{
		obj = new JlXLDModPara(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDModPara(SerializationInfo info, StreamingContext context)
	{
		DeserializeXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把本容器按库自有二进制格式写入流；实现是 SerializeXld() 取字节 + JlSerializationBuffer.WriteToStream 落流。</summary>
	/// <param name="stream">目标流，须可写。</param>
	/// <remarks>
	///   <para><b>功能说明</b>：先走原生 id 1553 把容器变成字节，再带库自有头部写进流；流内不是可读文本，只能由本类 Deserialize(Stream) 读回。</para>
	///   <para><b>约束或前提</b>：不关闭流、不回绕 Position，读完要自己把位置归零；未初始化容器能否序列化托管侧未检查 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要内存字节（入队列、存数据库字段）直接用 SerializeXld()；进程内独立副本用 Clone()，不必绕流；xld_mod_para 这类带平行线对属性的对象没有文本导出出口。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       mods.Serialize(ms);
	///       ms.Position = 0;
	///       using JlXLDModPara back = JlXLDModPara.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>：本方法是 new 隐藏 JlXLD.Serialize(Stream)，按 JlXLD 静态类型调用会走基类实现；平行线对属性是否随二进制完整往返未在托管侧体现 [待实测]。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeXld(), stream);
	}

	/// <summary>从本库 Serialize(Stream) 写出的二进制流恢复一个容器，返回的是新对象。</summary>
	/// <param name="stream">源流，须可读且位置指向头部。</param>
	/// <returns>内部先 new JlXLDModPara() 再 DeserializeXld 装载句柄的新容器。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：先 JlSerializationBuffer.ReadFromStream 取全部字节，再走原生 id 1552 反序列化装载；不改动调用方已有的任何容器。</para>
	///   <para><b>约束或前提</b>：流内容必须是同库同版本写出的，头部不匹配时报错形式由 JlSerializationBuffer 侧决定 [待实测]；静态方法按类名调用，不会像实例 Serialize 那样受 new 隐藏影响走错基类。</para>
	///   <para><b>与相邻算子的取舍</b>：手里已有实例、只想就地换内容用实例方法 DeserializeXld(byte[])（原地改写、不产生新对象）；要独立副本用 Clone()。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using FileStream fs = new FileStream(@"C:\vision\mods.bin", FileMode.Open, FileAccess.Read);
	///   using JlXLDModPara mods = JlXLDModPara.Deserialize(fs);
	///   int n = mods.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>：返回值需 Dispose；CountObj 只数元素条数，读出平行线对八属性仍要用继承自 JlXLD 的 GetParallelsXld。</para>
	/// </remarks>
	public new static JlXLDModPara Deserialize(Stream stream)
	{
		JlXLDModPara hXLDModPara = new JlXLDModPara();
		hXLDModPara.DeserializeXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLDModPara;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>序列化往返（SerializeXld + DeserializeXld）出一份原生侧完全独立的新容器。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现是 id 1553 取字节、new JlXLDModPara()、id 1552 装载的往返，副本与原件此后各自释放、互不影响，不是同一容器的第二个托管壳。</para>
	///   <para><b>约束或前提</b>：要求本容器已初始化；平行线对属性随字节流完整往返这一点未在托管侧体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：要子集用 SelectObj/CopyObj；要把两份容器并起来用 ConcatObj；只想要"同一对象的另一个托管壳"用带 EditorBrowsable(Never) 的 JlXLDModPara(JlObject) 构造器（内部 CopyObject）而非本方法 [待实测：两者的属性共享差异]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara backup = mods.Clone();
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；大容器上是两次原生调用加一份字节缓冲，代价明显高于 CopyObj。显式接口实现 ICloneable.Clone() 返回 object，走同一方法。</para>
	/// </remarks>
	public new JlXLDModPara Clone()
	{
		byte[] data = SerializeXld();
		JlXLDModPara obj = new JlXLDModPara();
		obj.DeserializeXld(data);
		return obj;
	}

	/// <summary>
	///   把两分辨率层级的道路假设合并成路侧多边形；本容器在其中充当"已修正的平行线对"输入，结果装入新 JlXLDPoly 返回。
	/// </summary>
	/// <param name="edgePolygons">待检查的边缘多边形。Default: 无</param>
	/// <param name="extParallels">由 edgePolygons 求得的扩展平行线对。Default: 无</param>
	/// <param name="centerLines">待检查的道路中心线多边形。Default: 无</param>
	/// <param name="maxAngleParallel">两条平行线段允许的夹角上限（弧度）。Default: 0.523598775598</param>
	/// <param name="maxAngleColinear">两条共线线段允许的夹角上限（弧度）。Default: 0.261799387799</param>
	/// <param name="maxDistanceParallel">两条平行线段允许的间距上限（像素）。Default: 40</param>
	/// <param name="maxDistanceColinear">两条共线线段允许的间距上限（像素）。Default: 40</param>
	/// <returns>找到的路侧（Roadsides），新 JlXLDPoly 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生 id 37；图标输入四路按原生槽位存：edgePolygons→1、本容器（xld_mod_para）→2、extParallels→3、centerLines→4；四个控制参占控制槽 0–3。本容器是输入不是输出，输出走 JlXLDPoly.LoadNew（OCT 槽 1），即结果声明为 JlXLDPoly 而非 ModPara。</para>
	///   <para><b>约束或前提</b>：edgePolygons/extParallels/centerLines 必须与本容器出自同一条上游链（GenPolygonsXld→GenParallelsXld→ModParallelsXld），四者序号错位会静默合并错对象 [待实测：错配时的原生行为]；角度单位是弧度，默认值即 30° 与 15°。</para>
	///   <para><b>与相邻算子的取舍</b>：本重载四个控制参走 Store+调用后 UnpinTuple（钉固元组），double 重载走 StoreD 直写、同一 id；传字面量时用 double 重载省一次钉固定位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDPoly center = poly.CopyObj(1, 1);
	///   using JlXLDPoly roadsides = mods.CombineRoadsXld(poly, ext, center,
	///       new JlTuple(0.523598775598), new JlTuple(0.261799387799), new JlTuple(40.0), new JlTuple(40.0));
	///   </code>
	///   <para><b>资源与坑</b>：GC.KeepAlive 覆盖 this 与三路外部输入，调用结束前别提前 Dispose 它们；返回的 roadsides 与 out 的 ext 均需各自 Dispose。</para>
	/// </remarks>
	public JlXLDPoly CombineRoadsXld(JlXLDPoly edgePolygons, JlXLDExtPara extParallels, JlXLDPoly centerLines, JlTuple maxAngleParallel, JlTuple maxAngleColinear, JlTuple maxDistanceParallel, JlTuple maxDistanceColinear)
	{
		IntPtr proc = JlNativeApi.PreCall(37);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, edgePolygons);
		JlNativeApi.Store(proc, 3, extParallels);
		JlNativeApi.Store(proc, 4, centerLines);
		JlNativeApi.Store(proc, 0, maxAngleParallel);
		JlNativeApi.Store(proc, 1, maxAngleColinear);
		JlNativeApi.Store(proc, 2, maxDistanceParallel);
		JlNativeApi.Store(proc, 3, maxDistanceColinear);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxAngleParallel);
		JlNativeApi.UnpinTuple(maxAngleColinear);
		JlNativeApi.UnpinTuple(maxDistanceParallel);
		JlNativeApi.UnpinTuple(maxDistanceColinear);
		err = JlXLDPoly.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(edgePolygons);
		GC.KeepAlive(extParallels);
		GC.KeepAlive(centerLines);
		return obj;
	}

	/// <summary>
	///   把两分辨率层级的道路假设合并成路侧多边形（double 控制参重载）；本容器充当"已修正的平行线对"输入。
	/// </summary>
	/// <param name="edgePolygons">待检查的边缘多边形。Default: 无</param>
	/// <param name="extParallels">由 edgePolygons 求得的扩展平行线对。Default: 无</param>
	/// <param name="centerLines">待检查的道路中心线多边形。Default: 无</param>
	/// <param name="maxAngleParallel">两条平行线段允许的夹角上限（弧度）。Default: 0.523598775598</param>
	/// <param name="maxAngleColinear">两条共线线段允许的夹角上限（弧度）。Default: 0.261799387799</param>
	/// <param name="maxDistanceParallel">两条平行线段允许的间距上限（像素）。Default: 40</param>
	/// <param name="maxDistanceColinear">两条共线线段允许的间距上限（像素）。Default: 40</param>
	/// <returns>找到的路侧（Roadsides），新 JlXLDPoly 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：与 JlTuple 重载同为原生 id 37，图标槽位相同（edgePolygons→1、本容器→2、extParallels→3、centerLines→4）；差别仅在四个控制参用 StoreD 直写 DOUBLE，不做元组钉固/解固，单次调用更省。</para>
	///   <para><b>约束或前提</b>：角度是弧度、距离是像素；四路输入须来自同一上游链（GenPolygonsXld→GenParallelsXld→ModParallelsXld），槽位与 C# 形参序不一致（本容器在槽 2），别按形参顺序猜原生顺序 [待实测：错配时行为]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要给控制参传多元素元组（广播语义 [待实测]）只能用 JlTuple 重载；常规单阈值场景用本重载，免去钉固开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDPoly center = poly.CopyObj(1, 1);
	///   using JlXLDPoly roadsides = mods.CombineRoadsXld(poly, ext, center, 0.523598775598, 0.261799387799, 40.0, 40.0);
	///   </code>
	///   <para><b>资源与坑</b>：结果是新 JlXLDPoly 句柄需 Dispose；GC.KeepAlive 覆盖全部图标输入，原生调用返回前不得先 Dispose 它们。</para>
	/// </remarks>
	public JlXLDPoly CombineRoadsXld(JlXLDPoly edgePolygons, JlXLDExtPara extParallels, JlXLDPoly centerLines, double maxAngleParallel, double maxAngleColinear, double maxDistanceParallel, double maxDistanceColinear)
	{
		IntPtr proc = JlNativeApi.PreCall(37);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, edgePolygons);
		JlNativeApi.Store(proc, 3, extParallels);
		JlNativeApi.Store(proc, 4, centerLines);
		JlNativeApi.StoreD(proc, 0, maxAngleParallel);
		JlNativeApi.StoreD(proc, 1, maxAngleColinear);
		JlNativeApi.StoreD(proc, 2, maxDistanceParallel);
		JlNativeApi.StoreD(proc, 3, maxDistanceColinear);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDPoly.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(edgePolygons);
		GC.KeepAlive(extParallels);
		GC.KeepAlive(centerLines);
		return obj;
	}

	/// <summary>
	///   从本容器中剔除与 objectsSub 内容重合的元素，装入新容器返回（原生 id 558）。</summary>
	/// <param name="objectsSub">被减对象容器。Default: 无</param>
	/// <returns>本容器中不属于 objectsSub 的元素，新 JlXLDModPara 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：图标槽位 this→1、objectsSub→2；差集按元素内容判同（判等精度细则由原生决定 [待实测]），与本容器/被减容器的元素顺序无关。</para>
	///   <para><b>约束或前提</b>：两容器元素类应同为 xld_mod_para，否则判等结果无意义；本容器不改动。</para>
	///   <para><b>与相邻算子的取舍</b>：按"第几条"删用 RemoveObj（id 2005，依赖上游顺序）；按内容删用本方法。只判两容器是否全等用 TestEqualObj（id 576），不必搬一个新容器回来。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara keep = mods.CopyObj(1, 1);
	///   using JlXLDModPara rest = mods.ObjDiff(keep);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；托管层还有基类 JlXLD.ObjDiff(JlXLD) 重载，实参按 JlXLD 静态类型传入时会走基线并返回 JlXLD。</para>
	/// </remarks>
	public JlXLDModPara ObjDiff(JlXLDModPara objectsSub)
	{
		IntPtr proc = JlNativeApi.PreCall(558);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsSub);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsSub);
		return obj;
	}

	/// <summary>
	///   把本容器中从 index 起的 numObj 个连续元素复制成新容器（原生 id 568）。</summary>
	/// <param name="index">复制区段起始序号。Default: 1</param>
	/// <param name="numObj">复制个数，或 -1。Default: 1</param>
	/// <returns>复制出的新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：两个参数都经 StoreI 以 INTEGER 直写控制槽 0/1，无元组钉固开销；输出走本类 LoadNew（OCT 槽 1），是独立新句柄。</para>
	///   <para><b>约束或前提</b>：起始序号默认值为 1、包装层不换算，判为 1 基 [待实测]；numObj=-1 的英文文档原文只说 "or -1"，未证实即"全部" [待实测]；越界行为调用前用 CountObj() 核对元素数。</para>
	///   <para><b>与相邻算子的取舍</b>：CopyObj 只取连续区段；任意/含重复的序号用 SelectObj（id 572，传 JlTuple）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara head2 = mods.CopyObj(1, 2);
	///   </code>
	///   <para><b>资源与坑</b>：本方法是 new 隐藏 JlXLD.CopyObj(int,int)，按 JlXLD 静态类型调用得到的是 JlXLD 返回值。</para>
	/// </remarks>
	public new JlXLDModPara CopyObj(int index, int numObj)
	{
		IntPtr proc = JlNativeApi.PreCall(568);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.StoreI(proc, 1, numObj);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 objects2 整体接在本容器之后，装入新容器返回（原生 id 569）。</summary>
	/// <param name="objects2">接在后面的容器。Default: 无</param>
	/// <returns>拼接后的新 JlXLDModPara 句柄；两个源容器均不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：图标槽位 this→1、objects2→2，结果元素顺序固定为"本容器在前、objects2 在后"；新容器长度 = 两者 CountObj 之和 [待实测：空容器参与时的长度]。</para>
	///   <para><b>约束或前提</b>：拼接只做容器级合并，不改元素几何；两容器类不同能否拼上托管侧未检查 [待实测]（LoadNew 不做类断言），类不符要到后续 ModPara 专用调用才暴露。</para>
	///   <para><b>与相邻算子的取舍</b>：只往末尾接用本方法；要插到中间位置用 InsertObj（id 2003，原元素整体后移）；只取子集用 SelectObj。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara tail = mods.CopyObj(1, 1);
	///   using JlXLDModPara joined = mods.ConcatObj(tail);
	///   </code>
	///   <para><b>资源与坑</b>：GC.KeepAlive 保证两个源容器在原生调用结束前存活；返回新句柄需 Dispose。</para>
	/// </remarks>
	public JlXLDModPara ConcatObj(JlXLDModPara objects2)
	{
		IntPtr proc = JlNativeApi.PreCall(569);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return obj;
	}

	/// <summary>
	///   按序号把选中的元素装入新容器（原生 id 572，JlTuple 序号重载）。</summary>
	/// <param name="index">要选出的元素序号元组。Default: 1</param>
	/// <returns>选出的新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：index 经 Store 钉固为控制槽 0、调用后 UnpinTuple；int 重载则 StoreI 直写、无钉固开销，同一 id。输出走本类 LoadNew（OCT 槽 1）。</para>
	///   <para><b>约束或前提</b>：包装层不做序号换算，默认值 1 判为 1 基 [待实测]；重复序号、越界与空元组行为 [待实测]，调用前用 CountObj() 核对。</para>
	///   <para><b>与相邻算子的取舍</b>："第几条"依赖上游 GenParallelsXld/ModParallelsXld 的输出顺序，顺序不稳时改用 SelectShapeXld 按特征筛；连续区段用 CopyObj。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara picked = mods.SelectObj(new JlTuple(1, 3));
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；索引器 this[JlTuple] 即本方法，写 mods[1] 靠 int 隐式转 JlTuple 落到这里。</para>
	/// </remarks>
	public new JlXLDModPara SelectObj(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(572);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按单个序号把元素装入新容器（原生 id 572，int 序号重载）。</summary>
	/// <param name="index">要选出的元素序号。Default: 1</param>
	/// <returns>选出的新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：index 经 StoreI 以 INTEGER 直写控制槽 0，与 JlTuple 重载同一 id，但省掉钉固/解固两步，单序号场景优先用本重载。</para>
	///   <para><b>约束或前提</b>：基数与越界行为同 JlTuple 重载 [待实测]；只能选单个序号，选多条用 SelectObj(JlTuple) 或索引器。</para>
	///   <para><b>与相邻算子的取舍</b>：连续区段用 CopyObj(index, numObj) 一次取完，比循环 SelectObj 再 ConcatObj 少 N-1 次原生调用与容器搬运。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara third = mods.SelectObj(3);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；本方法 new 隐藏 JlXLD.SelectObj(int)，按 JlXLD 静态类型调用返回基类容器。</para>
	/// </remarks>
	public new JlXLDModPara SelectObj(int index)
	{
		IntPtr proc = JlNativeApi.PreCall(572);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   带容差比较两个元素容器是否逐条相等（原生 id 573），返回装载为 INTEGER 的比较码。</summary>
	/// <param name="objects2">被比较容器。Default: 无</param>
	/// <param name="epsilon">两坐标/灰度值间允许的最大差。Default: 0.0</param>
	/// <returns>比较结果 int（英文文档称 Boolean result value，但具体取值编码托管侧不可见 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：图标槽位 this→1、objects2→2；epsilon 钉固在控制槽 0，结果经 LoadI 从输出槽 0 装载。epsilon 单位随被比较属性而定（坐标为像素）。</para>
	///   <para><b>约束或前提</b>：epsilon=0.0 即严格逐位相等，浮点重建/变换链后建议放宽 [待实测：建议阈值]；两容器元素数不等时的返回与判等粒度 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：无容差、只判"完全一样"用 TestEqualObj（id 576）；要拿差集内容用 ObjDiff（id 558）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara copy = mods.Clone();
	///   int eq = mods.CompareObj(copy, new JlTuple(1e-9));
	///   </code>
	///   <para><b>资源与坑</b>：本重载 epsilon 走 Store+UnpinTuple；标量场景用 double 重载（StoreD）更省；别把返回当 bool 用 == true 比较（类型是 int）。</para>
	/// </remarks>
	public int CompareObj(JlXLDModPara objects2, JlTuple epsilon)
	{
		IntPtr proc = JlNativeApi.PreCall(573);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.Store(proc, 0, epsilon);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(epsilon);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return intValue;
	}

	/// <summary>
	///   带容差比较两个元素容器是否逐条相等（原生 id 573，double 容差重载）。</summary>
	/// <param name="objects2">被比较容器。Default: 无</param>
	/// <param name="epsilon">两坐标/灰度值间允许的最大差。Default: 0.0</param>
	/// <returns>装载为 INTEGER 的比较码，取值编码 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：与 JlTuple 重载同一 id/槽位，epsilon 经 StoreD 直写 DOUBLE、不钉固，单容差场景优先用本重载。</para>
	///   <para><b>约束或前提</b>：epsilon 对坐标按像素、对灰度按灰度级理解（同一标量同时作用于两类属性 [待实测]）；元素数不等时的行为同元组重载 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：零容差快速判同用 TestEqualObj；元组重载可传多元素容差（原生是否按向量处理 [待实测]）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara copy = mods.Clone();
	///   int eq = mods.CompareObj(copy, 1e-9);
	///   </code>
	///   <para><b>资源与坑</b>：返回值是 int，写判等逻辑前先核实相等时的具体值 [待实测]，别按 C# 布尔直觉直接当 true/false 用。</para>
	/// </remarks>
	public int CompareObj(JlXLDModPara objects2, double epsilon)
	{
		IntPtr proc = JlNativeApi.PreCall(573);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.StoreD(proc, 0, epsilon);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return intValue;
	}

	/// <summary>
	///   判断两容器是否完全相同（原生 id 576），结果装载为 INTEGER 返回。</summary>
	/// <param name="objects2">对照容器。Default: 无</param>
	/// <returns>布尔性结果（1/0 的具体含义与"部分相同"是否有中间值 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：图标槽位 this→1、objects2→2，无控制参；比 CompareObj 少一个 epsilon，判的是逐位严格相同。</para>
	///   <para><b>约束或前提</b>：Clone/序列化往返再比较是否仍判"相同" [待实测]；带容差需求时本方法无能为力，换 CompareObj（id 573）。</para>
	///   <para><b>与相邻算子的取舍</b>：要的是"哪些不同"而非"是否不同"时用 ObjDiff（id 558）；判等仅用于分支/回归比对，不要拿它当同步手段。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara copy = mods.Clone();
	///   int same = mods.TestEqualObj(copy);
	///   </code>
	///   <para><b>资源与坑</b>：无新句柄产生、无需释放；返回 int 与布尔直觉不同，先核实取值编码 [待实测]。</para>
	/// </remarks>
	public int TestEqualObj(JlXLDModPara objects2)
	{
		IntPtr proc = JlNativeApi.PreCall(576);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return intValue;
	}

	/// <summary>
	///   依据规则网格点计算畸变图像到校正图像的映射（原生 id 1107）：返回映射图新句柄，网格化轮廓结果经 out 以新 JlXLDModPara 容器给出。</summary>
	/// <param name="image">参考图像。Default: 无</param>
	/// <param name="meshes">输出的网格轮廓容器，新 JlXLDModPara 句柄。Default: 无</param>
	/// <param name="gridSpacing">校正图像中格点间距（像素，StoreI 按 INTEGER 装载）。Default: 0</param>
	/// <param name="rotation">施加到点网格上的旋转（元组钉固装载；"auto" 语义见 string 重载）。Default: "auto"</param>
	/// <param name="row">格点行坐标（像素）。Default: 无</param>
	/// <param name="column">格点列坐标（像素）。Default: 无</param>
	/// <param name="mapType">映射类型。Default: "bilinear"</param>
	/// <returns>含映射数据的新 JlImage 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：image 存图标槽 1、本实例存图标槽 2（本类只是两个 OCT 输出中槽 2 的装载壳，见 Store 行为）；控制槽 0–4 依次是 gridSpacing/rotation/row/column/mapType。双输出：槽 1 经 JlImage.LoadNew 得映射图，槽 2 经本类 LoadNew 得 meshes，都是新句柄。</para>
	///   <para><b>约束或前提</b>：row/column 成对给出校正后图像中的格点坐标，点数与 gridSpacing 的关系（0 即自动取网格 [待实测]）由原生决定；rotation 数值单位为弧度 [待实测：托管侧只按元组透传]。</para>
	///   <para><b>与相邻算子的取舍</b>：本重载 rotation 走 Store+UnpinTuple，固定传 "auto" 或角度字符串时用 string 重载（StoreS）更直接；只需网格不需映射图时也要接住返回值释放，别只 Dispose meshes。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple row = new JlTuple(100.0, 200.0);
	///   JlTuple column = new JlTuple(100.0, 300.0);
	///   using JlImage map = mods.GenGridRectificationMap(img, out JlXLDModPara meshes, 0, new JlTuple(0.0), row, column, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>：map 与 meshes 均需 Dispose；row/column 传引用元组，调用期间别改；GC.KeepAlive 只覆盖 this 与 image。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDModPara meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreI(proc, 0, gridSpacing);
		JlNativeApi.Store(proc, 1, rotation);
		JlNativeApi.Store(proc, 2, row);
		JlNativeApi.Store(proc, 3, column);
		JlNativeApi.StoreS(proc, 4, mapType);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rotation);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   依据规则网格点计算畸变图像到校正图像的映射（原生 id 1107，rotation 为字符串的重载）。</summary>
	/// <param name="image">参考图像。Default: 无</param>
	/// <param name="meshes">输出的网格轮廓容器，新 JlXLDModPara 句柄。Default: 无</param>
	/// <param name="gridSpacing">校正图像中格点间距（像素，StoreI 按 INTEGER 装载）。Default: 0</param>
	/// <param name="rotation">施加到点网格上的旋转，StoreS 按 STRING 装载（如 "auto"）。Default: "auto"</param>
	/// <param name="row">格点行坐标（像素）。Default: 无</param>
	/// <param name="column">格点列坐标（像素）。Default: 无</param>
	/// <param name="mapType">映射类型。Default: "bilinear"</param>
	/// <returns>含映射数据的新 JlImage 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：与 JlTuple rotation 重载同一 id/槽位；唯一区别是 rotation 经 StoreS 直写 STRING，"auto" 这类关键字只能走本重载或再包一层字符串元组。</para>
	///   <para><b>约束或前提</b>："auto" 时原生如何定网格朝向 [待实测]；row/column 与 gridSpacing 的组合合法性由原生校验 [待实测]，托管侧不做任何前置检查。</para>
	///   <para><b>与相邻算子的取舍</b>：要显式给角度数值用 JlTuple 重载传弧度 [待实测：单位]；本重载适合"自动定朝向"的标准校正流程。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple row = new JlTuple(100.0, 200.0);
	///   JlTuple column = new JlTuple(100.0, 300.0);
	///   using JlImage map = mods.GenGridRectificationMap(img, out JlXLDModPara meshes, 0, "auto", row, column, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>：map、meshes 与 out 的 ext 都要 Dispose；映射图供后续 rectify 类算子消费时，其生命周期要覆盖整个校正流程。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDModPara meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreI(proc, 0, gridSpacing);
		JlNativeApi.StoreS(proc, 1, rotation);
		JlNativeApi.Store(proc, 2, row);
		JlNativeApi.Store(proc, 3, column);
		JlNativeApi.StoreS(proc, 4, mapType);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   用序列化字节就地重建本容器内容（原生 id 1552）：先 Dispose 旧句柄，再把输出装载回本实例。</summary>
	/// <param name="serializedItemHandle">SerializeXld() 产出的序列化字节。Default: 无</param>
	/// <remarks>
	///   <para><b>功能说明</b>：方法体第一步就 Dispose() 自身旧句柄，随后 Load(proc,1,err) 原地写入新句柄——本对象引用不变、内容整体替换，不返回新对象。</para>
	///   <para><b>约束或前提</b>：字节须来自同库同版本的 SerializeXld（包一层 JlSerializationBuffer 钉住整次调用）；对同一实例并发装载不安全，旧句柄已释放而新装载失败时本容器处于未初始化态 [待实测：失败残留状态]。</para>
	///   <para><b>与相邻算子的取舍</b>：想保留旧容器、另得新容器用静态 Deserialize(Stream)（作用于流）或先 Clone 再改；从流恢复要自己配 JlSerializationBuffer.ReadFromStream 读流。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   byte[] data = mods.SerializeXld();
	///   JlXLDModPara holder = new JlXLDModPara();
	///   holder.DeserializeXld(data);
	///   </code>
	///   <para><b>资源与坑</b>：调用方若同时持有本实例的其他 JlXLDModPara 壳（JlObject 构造器路径），旧句柄 Dispose 后那些壳即悬空；序列化字节不含流的头部，别和 Serialize(Stream) 的产物混用。</para>
	/// </remarks>
	public new void DeserializeXld(byte[] serializedItemHandle)
	{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1552);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   把本容器序列化成内存字节（原生 id 1553）；英文文档写 "Handle"，托管侧实际返回 JlSerializationBuffer.LoadBytes 取回的 byte[]。</summary>
	/// <returns>序列化字节，可直接交给 DeserializeXld(byte[])。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：单次原生调用，本容器不改动；产物不含 Serialize(Stream) 那层的库头部，两族格式不可互换。</para>
	///   <para><b>约束或前提</b>：容器须已初始化；字节里是否完整保留 xld_mod_para 平行线对属性 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：落文件/跨进程用 Serialize(Stream)+Deserialize(Stream)（自动带头部）；入队列、存数据库字段用本方法配 DeserializeXld。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   byte[] data = mods.SerializeXld();
	///   using JlXLDModPara holder = new JlXLDModPara();
	///   holder.DeserializeXld(data);
	///   </code>
	///   <para><b>资源与坑</b>：byte[] 由托管层拷贝生成，原生序列化缓冲在调用内回收；new 隐藏 JlXLD.SerializeXld()，静态类型走基类时产物仍是同一字节格式。</para>
	/// </remarks>
	public new byte[] SerializeXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1553);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   选出经过给定点的元素装入新容器（原生 id 1595，点坐标为 JlTuple）。</summary>
	/// <param name="row">测试点行坐标（像素，row=y、向下为正）。Default: 100.0</param>
	/// <param name="column">测试点列坐标（像素，column=x、向右为正）。Default: 100.0</param>
	/// <returns>含测试点的元素新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：row/column 钉固在控制槽 0/1；平行线对元素按线段几何判断"含点" [待实测：判定容差与端点归属]。</para>
	///   <para><b>约束或前提</b>：元组传多测试点时的组合语义（任一点命中即选 / 逐点对齐）托管侧不可见 [待实测]；负坐标或远超图像尺寸的坐标是否报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只想知道每条轮廓是否含点、不挑子集时用基类 TestXldPoint（返回逐元素 0/1）；按面积/形状特征筛用 SelectShapeXld。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara hit = mods.SelectXldPoint(new JlTuple(120.0), new JlTuple(240.0));
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；单点场景用 double 重载可省两次元组钉固。</para>
	/// </remarks>
	public new JlXLDModPara SelectXldPoint(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1595);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   选出经过给定单点的元素装入新容器（原生 id 1595，double 点坐标重载）。</summary>
	/// <param name="row">测试点行坐标（像素，row=y、向下为正）。Default: 100.0</param>
	/// <param name="column">测试点列坐标（像素，column=x、向右为正）。Default: 100.0</param>
	/// <returns>含该测试点的元素新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：row/column 经 StoreD 直写 DOUBLE 到控制槽 0/1，与元组重载同一 id，省去钉固/解固；单点判选优先用本重载。</para>
	///   <para><b>约束或前提</b>：坐标单位为图像像素坐标，不是物理量；对 xld_mod_para 线段的"含点"判定容差 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：多个测试点一次筛只能走 JlTuple 重载；只要真值表不要子集用基类 TestXldPoint。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara hit = mods.SelectXldPoint(120.0, 240.0);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；选空时得到的是空容器而非 null，用 CountObj() 判断。</para>
	/// </remarks>
	public new JlXLDModPara SelectXldPoint(double row, double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1595);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按形状特征区间筛选元素装入新容器（原生 id 1597，特征名/上下限为 JlTuple）。</summary>
	/// <param name="features">要检查的形状特征名。Default: "area"</param>
	/// <param name="operation">多特征间的组合方式。Default: "and"</param>
	/// <param name="min">各特征下限，或字符串 'min' 表示不设下限。Default: 150.0</param>
	/// <param name="max">各特征上限，或字符串 'max' 表示不设上限。Default: 99999.0</param>
	/// <returns>满足条件的元素新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：控制槽 0–3 依次 features（钉固元组）/operation（StoreS STRING）/min/max（钉固元组）；特征名、上下限按元素逐条求值后比较。</para>
	///   <para><b>约束或前提</b>：features、min、max 三者长度需配对（多特征多区间 [待实测：不等长时广播规则]）；xld_mod_para 元素上各特征（area/length 等）的具体定义与单位 [待实测]；传 'min'/'max' 关键字只能走本元组重载（string 重载的 min/max 是 double，装不出字符串）。</para>
	///   <para><b>与相邻算子的取舍</b>：按几何过点筛用 SelectXldPoint；按序号取用 SelectObj。特征筛选不依赖容器顺序，是并行流水线里最稳的挑法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara longOnes = mods.SelectShapeXld(new JlTuple("length"), "and", 150.0, new JlTuple("max"));
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；下限>上限的区间只会选空、不报错 [待实测]。</para>
	/// </remarks>
	public new JlXLDModPara SelectShapeXld(JlTuple features, string operation, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(1597);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, features);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.Store(proc, 2, min);
		JlNativeApi.Store(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(features);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按形状特征区间筛选元素装入新容器（原生 id 1597，单特征单区间重载）。</summary>
	/// <param name="features">要检查的形状特征名。Default: "area"</param>
	/// <param name="operation">多特征间的组合方式。Default: "and"</param>
	/// <param name="min">特征下限。Default: 150.0</param>
	/// <param name="max">特征上限。Default: 99999.0</param>
	/// <returns>满足条件的元素新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：features/operation 走 StoreS、min/max 走 StoreD 直写控制槽 0–3，与元组重载同一 id，无钉固开销；单特征区间场景首选本重载。</para>
	///   <para><b>约束或前提</b>：本重载装不出 'min'/'max' 字符串关键字（要开边界请走元组重载）；operation 在单特征时是否被忽略 [待实测]；特征单位 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：多特征"与/或"组合只能传多元素 features 元组（本重载一个字符串只装一个特征名 [待实测：原生是否支持空格分隔多值]）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara big = mods.SelectShapeXld("area", "and", 150.0, 99999.0);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；Default 里的 99999.0 不是"无穷"，超过它的元素会被上限卡掉。</para>
	/// </remarks>
	public new JlXLDModPara SelectShapeXld(string features, string operation, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(1597);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, features);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.StoreD(proc, 2, min);
		JlNativeApi.StoreD(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对每条元素做形状变换（如凸包）后装入新容器（原生 id 1608）。</summary>
	/// <param name="type">变换类型。Default: "convex"</param>
	/// <returns>变换后的新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：type 经 StoreS 直写控制槽 0；输出走本类 LoadNew，托管声明仍是 JlXLDModPara，但变换后的原生对象类是否还带平行线对属性未验 [待实测]——若结果要回喂 CombineRoadsXld，先核实其类仍为 xld_mod_para [待实测]。</para>
	///   <para><b>约束或前提</b>：变换基于元素的采样点集或线段端点重建几何 [待实测]，凸包会把线段集变成环状包壳，原"平行线对"语义丢失；退化元素（两点重合的线段）的输出形态 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只筛不改用 SelectShapeXld；要量尺寸用 LengthXld/基类矩方法。ShapeTransXld 改变几何本身，下游容差判等会翻脸。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara hull = mods.ShapeTransXld("convex");
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；"convex" 之外的取值以原生算子文档为准，托管层不做白名单校验，传错在原生侧才报错 [待实测：报错形式]。</para>
	/// </remarks>
	public new JlXLDModPara ShapeTransXld(string type)
	{
		IntPtr proc = JlNativeApi.PreCall(1608);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 objectsInsert 的全部元素插入本容器第 index 个位置处，返回扩展后的新容器（原生 id 2003）。</summary>
	/// <param name="objectsInsert">要插入的元素容器。Default: 无</param>
	/// <param name="index">插入位置序号。Default: 无</param>
	/// <returns>扩展后的新 JlXLDModPara 句柄；本容器与 objectsInsert 均不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：图标槽位 this→1、objectsInsert→2；index 经 StoreI 直写控制槽 0（INTEGER，无钉固）。"挤开插入"：原第 index 个及之后的元素整体后移。</para>
	///   <para><b>约束或前提</b>：index 透传原生、基数与越界（含 0 与超长）行为 [待实测]；插到末尾请核对 index 与 CountObj() 的关系后再用，或干脆 ConcatObj。</para>
	///   <para><b>与相邻算子的取舍</b>：末尾追加用 ConcatObj（id 569，无 index）；原位覆盖用 ReplaceObj（id 2006，长度不变）；三者 index 语义互不相同，别照抄参数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara piece = mods.CopyObj(1, 1);
	///   using JlXLDModPara grown = mods.InsertObj(piece, 1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；本方法非 new 隐藏，基类 JlXLD.InsertObj(JlXLD,int) 同时存在，实参静态类型决定走哪条。</para>
	/// </remarks>
	public JlXLDModPara InsertObj(JlXLDModPara objectsInsert, int index)
	{
		IntPtr proc = JlNativeApi.PreCall(2003);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsInsert);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsInsert);
		return obj;
	}

	/// <summary>
	///   按序号把指定元素从本容器剔除，其余按原顺序装入新容器（原生 id 2005，序号为 JlTuple）。</summary>
	/// <param name="index">要剔除的元素序号元组。Default: 无</param>
	/// <returns>剩余元素组成的新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：index 钉固在控制槽 0、调用后 UnpinTuple；int 重载 StoreI 直写、同 id 更省。输出经本类 LoadNew（OCT 槽 1）。</para>
	///   <para><b>约束或前提</b>：序号基数与越界/重复序号行为 [待实测]；"第几条"依赖上游 ModParallelsXld 的输出顺序，上游参数一变就可能静默删错元素。</para>
	///   <para><b>与相邻算子的取舍</b>：按内容剔除用 ObjDiff（id 558），与顺序无关；只要留下的子集也可反过来用 SelectObj 选保留项。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara rest = mods.RemoveObj(new JlTuple(1, 2));
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；new 隐藏 JlXLD.RemoveObj(JlTuple)，按基类静态类型调用返回 JlXLD。</para>
	/// </remarks>
	public new JlXLDModPara RemoveObj(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(2005);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按单个序号剔除本容器的一个元素，其余装入新容器（原生 id 2005，int 序号重载）。</summary>
	/// <param name="index">要剔除的元素序号。Default: 无</param>
	/// <returns>剩余元素组成的新 JlXLDModPara 句柄；本容器不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：index 经 StoreI 直写控制槽 0，与元组重载同一 id，省钉固；单元素剔除首选本重载。</para>
	///   <para><b>约束或前提</b>：示例里常见的 0 基写法是模板残留——本重载默认值未标注，基数行为与元组重载一致 [待实测]；剔除依赖上游输出顺序，参数一变可能删错元素。</para>
	///   <para><b>与相邻算子的取舍</b>：批量剔除多个序号传元组重载一次完成，别循环调用本方法（每轮都新建一次容器、代价 N 倍）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara rest = mods.RemoveObj(2);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；new 隐藏 JlXLD.RemoveObj(int)。</para>
	/// </remarks>
	public new JlXLDModPara RemoveObj(int index)
	{
		IntPtr proc = JlNativeApi.PreCall(2005);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 objectsReplace 按位替换本容器指定序号的元素，装入新容器返回（原生 id 2006，序号为 JlTuple）。</summary>
	/// <param name="objectsReplace">拿来顶替的元素容器。Default: 无</param>
	/// <param name="index">被替换元素的序号元组。Default: 无</param>
	/// <returns>替换后的新 JlXLDModPara 句柄；本容器与 objectsReplace 均不改动，元素总数不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：图标槽位 this→1、objectsReplace→2；index 钉固在控制槽 0、调用后 UnpinTuple。"原位覆盖"只动被点名的位置，其余元素原样保留。</para>
	///   <para><b>约束或前提</b>：objectsReplace 元素数与 index 数不等时的配对规则（截断/广播/报错）托管侧不可见 [待实测]；序号基数行为同 RemoveObj [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：要在中间加元素用 InsertObj（会挤后移）、删元素用 RemoveObj；ReplaceObj 不改长度，三者 index 语义互不相同。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara fresh = mods.CopyObj(1, 1);
	///   using JlXLDModPara swapped = mods.ReplaceObj(fresh, new JlTuple(1));
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；GC.KeepAlive 覆盖 objectsReplace，原生调用结束前别 Dispose 它。</para>
	/// </remarks>
	public JlXLDModPara ReplaceObj(JlXLDModPara objectsReplace, JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(2006);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsReplace);
		JlNativeApi.Store(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsReplace);
		return obj;
	}

	/// <summary>
	///   用 objectsReplace 替换本容器第 index 个元素，装入新容器返回（原生 id 2006，int 单序号重载）。</summary>
	/// <param name="objectsReplace">拿来顶替的元素容器。Default: 无</param>
	/// <param name="index">被替换元素的序号。Default: 无</param>
	/// <returns>替换后的新 JlXLDModPara 句柄；两个源容器均不改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：index 经 StoreI 直写控制槽 0，与元组重载同一 id，省钉固；单位置覆盖首选本重载。</para>
	///   <para><b>约束或前提</b>：objectsReplace 含多个元素而 index 只有一个时是"整体顶一个位置"还是只取第一个 [待实测]；序号基数行为同 RemoveObj [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：多位置成批改用元组重载一次完成；想在中间"加"而非"换"用 InsertObj。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mods = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDModPara fresh = mods.CopyObj(1, 1);
	///   using JlXLDModPara swapped = mods.ReplaceObj(fresh, 1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；本方法非 new 隐藏，基类 JlXLD.ReplaceObj(JlXLD,int) 并存，按实参静态类型分派。</para>
	/// </remarks>
	public JlXLDModPara ReplaceObj(JlXLDModPara objectsReplace, int index)
	{
		IntPtr proc = JlNativeApi.PreCall(2006);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsReplace);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsReplace);
		return obj;
	}
}
