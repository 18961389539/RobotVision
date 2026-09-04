using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an XLD polygon object(-array).</summary>
[Serializable]
public class JlXLDPoly : JlXLD, ISerializable, ICloneable
{
	/// <summary>按原生 1 基序号取本多边形元组中的单个多边形，等价于直接调用 SelectObj。</summary>
	/// <param name="index">要取出的元素序号（单元素 JlTuple）。Default: 1</param>
	/// <returns>只含该元素的新 JlXLDPoly 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>索引器不做任何换算，把 index 原样交给 SelectObj(JlTuple)（原生算子 572），因此序号是原生的 1 基语义：a[1] 取第一个多边形。</para>
	///   <para><b>约束或前提</b>index 应大于等于 1；0 或负序号在原生层的表现未验证 [待实测]。JlTuple 与 int 间有隐式转换，传整数字面量即可。</para>
	///   <para><b>与相邻算子的取舍</b>与 C# 集合的 0 基习惯相反，按 0 基思维用会整体错位一个元素。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly first = a[1];
	///   first.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>每次 get 都走一次原生调用并返回新句柄，必须逐个 Dispose；在循环里取元素又丢弃返回值，句柄会持续累积。</para>
	/// </remarks>
	public new JlXLDPoly this[JlTuple index] => SelectObj(index);

	/// <summary>创建一个句柄未初始化（UNDEF）的多边形容器占位对象。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>基类句柄被置为 JlObjectBase.UNDEF，不占用原生 xld_poly 资源；它只是"等着被填入"的空壳。</para>
	///   <para><b>约束或前提</b>在得到实际数据前不能参与任何原生算子调用（读取坐标、求交等都会报错）。能被合法填入的途径如 DeserializeXld、ReadPolygonXldArcInfo（这两个会先 Dispose 再原地 Load）或静态 Deserialize。</para>
	///   <para><b>与相邻算子的取舍</b>与 JlXLDPoly(IntPtr) 系构造器不同：后者要求句柄已存在且原生对象类必须是 xld_poly（AssertObjectClass 校验），适合包装外部句柄；无参构造器适合"输出容器"角色。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   </code>
	///   <para><b>资源与坑</b>UNDEF 句柄调用 Dispose 安全（空操作），用 using 包一层不会出错。</para>
	/// </remarks>
	public JlXLDPoly()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>用已有原生句柄包装出 JlXLDPoly（内部管道，默认按 copy=true 复制句柄）。</summary>
	/// <param name="key">原生 H_XLD_POLY 句柄值。</param>
	/// <remarks>
	///   <para><b>功能说明</b>委托给 (IntPtr, bool) 重载并 copy=true；构造后立即 AssertObjectClass，要求该句柄的原生对象类恰为 "xld_poly"。</para>
	///   <para><b>约束或前提</b>拿它包装轮廓（xld_cont）或区域句柄会在断言处直接抛错，而不是静默产生错误类型。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPoly(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>用已有原生句柄包装出 JlXLDPoly，并可控制是否复制句柄（内部管道）。</summary>
	/// <param name="key">原生 H_XLD_POLY 句柄值。</param>
	/// <param name="copy">true 时本对象持有句柄副本、Dispose 只释放副本；false 时直接接管传入句柄本身。</param>
	/// <remarks>
	///   <para><b>功能说明</b>供 Load/LoadNew 等内部装载路径使用；构造时 AssertObjectClass 校验原生对象类必须是 "xld_poly"。</para>
	///   <para><b>资源与坑</b>copy=false 时本对象与原句柄共享所有权，再对同一原生句柄手工释放会造成双重释放。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPoly(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从任意 JlObject 复制并包装为 JlXLDPoly（内部管道）。</summary>
	/// <param name="obj">源图标对象；基类构造器按 copy=true 复制其句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>复制源句柄后立即 AssertObjectClass，要求原生对象类为 "xld_poly"。</para>
	///   <para><b>约束或前提</b>传入 JlImage、JlRegion 或 JlXLDCont 等其它类句柄会直接抛错；它不是类型转换工具，只做同类接管/复制。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPoly(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "xld_poly");
	}

	/// <summary>从过程句柄的输出槽装载出一个新的 JlXLDPoly（供各算子包装代码调用的内部管道）。</summary>
	/// <param name="proc">JlNativeApi.PreCall 返回的过程句柄。</param>
	/// <param name="parIndex">iconic 输出参数序号（InitOCT 登记的槽位）。</param>
	/// <param name="err">CallProcedure 返回的错误码，失败时原样透传、不装载。</param>
	/// <param name="obj">装载结果；调用即产出的新句柄，须由调用方 Dispose。</param>
	/// <returns>透传或更新后的错误码。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 new 一个 UNDEF 占位对象，再 obj.Load(proc, parIndex, err) 把输出槽中的原生句柄填入——是"返回新句柄"而非原地改写；JlObjectBase.Load 要求接收者句柄为 UNDEF。</para>
	///   <para><b>资源与坑</b>err 已是失败码时跳过装载，obj 保持未初始化状态，但仍是可 Dispose 的托管对象。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDPoly obj)
	{
		obj = new JlXLDPoly(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>.NET 二进制反序列化专用构造器：从 SerializationInfo 中键为 "data" 的字节负载重建多边形（内部管道）。</summary>
	/// <param name="info">由 GetObjectData 写出的序列化信息，"data" 字段即 SerializeXld 的字节负载。</param>
	/// <param name="context">流上下文，本实现未使用。</param>
	/// <remarks>
	///   <para><b>功能说明</b>取出 "data" 字节数组后直接调 DeserializeXld 填充本对象；缺 "data" 键会由 GetValue 抛出异常。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPoly(SerializationInfo info, StreamingContext context)
	{
		DeserializeXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把本多边形元组按 Vision 二进制格式写入流；本对象不被改写。</summary>
	/// <param name="stream">目标流；须可写，位置与关闭时机由调用方管理。</param>
	/// <remarks>
	///   <para><b>功能说明</b>内部先 SerializeXld（原生算子 1553）得到字节负载，再经 JlSerializationBuffer.WriteToStream 落流。不消耗/不重置本对象句柄。</para>
	///   <para><b>约束或前提</b>本对象须已初始化，否则原生序列化调用报错。</para>
	///   <para><b>与相邻算子的取舍</b>成对读回用静态 Deserialize（返回新句柄）；想原地填充已有空对象则用 DeserializeXld。写文件持久化不要用本方法——那是 WritePolygonXldArcInfo/WritePolygonXldDxf 的职责。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       a.Serialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>MemoryStream 属于 System.IO；本方法不关闭传入的流。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeXld(), stream);
	}

	/// <summary>从 Vision 二进制流读出一个全新的多边形元组句柄。</summary>
	/// <param name="stream">源流；内容须为 Serialize/SerializeXld 写出的 Vision 二进制格式。</param>
	/// <returns>新建的 JlXLDPoly（内部 new 空对象后 DeserializeXld 原地填充）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法，不依赖任何已有实例；与实例方法 DeserializeXld 的区别是它自己创建并返回接收者。</para>
	///   <para><b>与相邻算子的取舍</b>想把数据灌进一个已存在的对象用 DeserializeXld（会先 Dispose 原句柄）；只要新对象用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       a.Serialize(ms);
	///       ms.Position = 0;
	///       JlXLDPoly b = JlXLDPoly.Deserialize(ms);
	///       b.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕 Dispose；流写后读回须把 Position 拨回起点；本方法不关闭传入的流。</para>
	/// </remarks>
	public new static JlXLDPoly Deserialize(Stream stream)
	{
		JlXLDPoly hXLDPoly = new JlXLDPoly();
		hXLDPoly.DeserializeXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLDPoly;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>深拷贝本多边形元组：经序列化字节负载再反序列化，产出内容相同、句柄独立的新对象。</summary>
	/// <returns>新 JlXLDPoly 句柄，与原对象互不影响。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现体为 SerializeXld → new JlXLDPoly → DeserializeXld，是字节级深拷贝；此后改副本不影响原对象，反之亦然。</para>
	///   <para><b>与相邻算子的取舍</b>只要元组中某一段用 SelectObj/索引器；取子区间用 CopyObj；把两个元组相接用 ConcatObj；本方法整份复制。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   JlXLDPoly copy = a.Clone();
	///   copy.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕 Dispose；克隆走一次完整序列化-反序列化，比句柄级共享开销大 [待实测：具体量级]。</para>
	/// </remarks>
	public new JlXLDPoly Clone()
	{
		byte[] data = SerializeXld();
		JlXLDPoly obj = new JlXLDPoly();
		obj.DeserializeXld(data);
		return obj;
	}

	/// <summary>把本元组与 polygons2 各自围成的区域求并，边界以闭合多边形给出（算子 id 5）。</summary>
	/// <param name="polygons2">第二个闭合多边形集合（本对象为第一个）。</param>
	/// <returns>包围并集区域的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>几何布尔并：this→原生输入 1、polygons2→输入 2，InitOCT 登记 1 个输出，LoadNew 返回全新句柄；两个输入都不被改写。</para>
	///   <para><b>约束或前提</b>"Closed" 指算子把每个多边形视为首末相连、围成有界区域来处理；输入应为闭合多边形。自相交多边形"内部"的定义本身有歧义，布尔结果依原生算法实现而定 [待实测]。顶点序（顺/逆时针）对并集结果的影响亦未在包装层体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要交集用 IntersectionClosedPolygonsXld（id 11），要差集用 DifferenceClosedPolygonsXld（id 9，方向是 this 减 sub），要对称差用 SymmDifferenceClosedPolygonsXld（id 7）。若是"删掉元组里某些多边形元素"，那是 ObjDiff 的容器语义，不是几何布尔。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("a.gen");
	///   JlXLDPoly b = new JlXLDPoly();
	///   b.ReadPolygonXldArcInfo("b.gen");
	///   JlXLDPoly u = a.Union2ClosedPolygonsXld(b);
	///   u.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值须 Dispose；调用结束前 a、b 都不得释放（实现靠 GC.KeepAlive 兜底），因此不能把"传完就 Dispose"写进调用表达式。</para>
	/// </remarks>
	public JlXLDPoly Union2ClosedPolygonsXld(JlXLDPoly polygons2)
	{
		IntPtr proc = JlNativeApi.PreCall(5);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, polygons2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(polygons2);
		return obj;
	}

	/// <summary>本元组与 polygons2 围成区域的对称差（只属一侧的部分），结果以闭合多边形表示（算子 id 7）。</summary>
	/// <param name="polygons2">第二个闭合多边形集合（本对象为第一个）。</param>
	/// <returns>包围对称差区域的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>几何对称差：this→原生输入 1、polygons2→输入 2；等价于 (A∪B)−(A∩B)，两两相交处会被挖掉。返回 LoadNew 新句柄，输入不改写。</para>
	///   <para><b>约束或前提</b>输入须是闭合多边形；自相交输入下"内部"定义有歧义，结果依原生实现 [待实测]。对称差具有交换性，但 DifferenceClosedPolygonsXld 的差方向不可交换，别混用。</para>
	///   <para><b>与相邻算子的取舍</b>要"A 有而 B 无"用 DifferenceClosedPolygonsXld（id 9）；只要公共部分用 IntersectionClosedPolygonsXld（id 11）；合并用 Union2ClosedPolygonsXld（id 5）。四个布尔算子都不处理开放多边形（不闭合）的语义。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("a.gen");
	///   JlXLDPoly b = new JlXLDPoly();
	///   b.ReadPolygonXldArcInfo("b.gen");
	///   JlXLDPoly sd = a.SymmDifferenceClosedPolygonsXld(b);
	///   sd.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值须 Dispose；调用期间两输入由 GC.KeepAlive 保命，不得提前释放。</para>
	/// </remarks>
	public JlXLDPoly SymmDifferenceClosedPolygonsXld(JlXLDPoly polygons2)
	{
		IntPtr proc = JlNativeApi.PreCall(7);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, polygons2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(polygons2);
		return obj;
	}

	/// <summary>用本多边形所围区域减去 sub 所围区域，差集以闭合多边形给出（算子 id 9）。</summary>
	/// <param name="sub">Polygons enclosing the region that is subtracted from the first region.</param>
	/// <returns>包围差集（this 有而 sub 无）区域的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>几何布尔差：this（被减数）→原生输入 1、sub（减数）→输入 2，InitOCT 登记 1 个输出，LoadNew 返回全新句柄，两输入都不被改写。差方向固定为 this − sub。</para>
	///   <para><b>约束或前提</b>输入须是闭合多边形才有确定内部；开口多边形如何围合 [待实测]。差方向不可交换，需要 sub − this 时得交换两路输入。自相交输入结果依原生实现而定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"公共部分"用 IntersectionClosedPolygonsXld（id 11），要"合并"用 Union2ClosedPolygonsXld（id 5），要"只属一侧"用 SymmDifferenceClosedPolygonsXld（id 7）。"从元组删掉某些多边形元素"（非几何）是 ObjDiff 的容器语义。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("a.gen");
	///   JlXLDPoly sub = new JlXLDPoly();
	///   sub.ReadPolygonXldArcInfo("sub.gen");
	///   JlXLDPoly diff = a.DifferenceClosedPolygonsXld(sub);
	///   diff.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值须 Dispose；调用期间两路由 GC.KeepAlive 保住，不得提前释放。</para>
	/// </remarks>
	public JlXLDPoly DifferenceClosedPolygonsXld(JlXLDPoly sub)
	{
		IntPtr proc = JlNativeApi.PreCall(9);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, sub);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(sub);
		return obj;
	}

	/// <summary>求本多边形与 polygons2 所围区域的公共部分（交集），以闭合多边形给出（算子 id 11）。</summary>
	/// <param name="polygons2">Polygons enclosing the second region to be intersected.</param>
	/// <returns>包围交集区域的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>几何布尔交：this→原生输入 1、polygons2→输入 2，InitOCT 登记 1 个输出，LoadNew 返回全新句柄，两输入都不被改写。交集对两路输入可交换。</para>
	///   <para><b>约束或前提</b>输入须是闭合多边形；不相交的两区域结果为空句柄 [待实测：空结果是否仍返回可 Dispose 对象]。开口多边形的围合方式 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"合并"用 Union2ClosedPolygonsXld（id 5），要"this 减 sub"用 DifferenceClosedPolygonsXld（id 9），要"只属一侧"用 SymmDifferenceClosedPolygonsXld（id 7）。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("a.gen");
	///   JlXLDPoly b = new JlXLDPoly();
	///   b.ReadPolygonXldArcInfo("b.gen");
	///   JlXLDPoly inter = a.IntersectionClosedPolygonsXld(b);
	///   inter.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值须 Dispose；调用期间两路由 GC.KeepAlive 保住，不得提前释放。</para>
	/// </remarks>
	public JlXLDPoly IntersectionClosedPolygonsXld(JlXLDPoly polygons2)
	{
		IntPtr proc = JlNativeApi.PreCall(11);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, polygons2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(polygons2);
		return obj;
	}

	/// <summary>从 ARC/INFO generate 格式文件读取多边形，原地载入本对象（算子 id 18，无返回值）。</summary>
	/// <param name="fileName">ARC/INFO generate 文件路径。</param>
	/// <remarks>
	///   <para><b>功能说明</b>实现体第一步是 Dispose()：本对象旧句柄先被释放，随后 Load(proc,1) 把读到的多边形写回本对象——原地改写，不返回新句柄。</para>
	///   <para><b>约束或前提</b>应把它用在空的新建对象上；对已有数据的对象调用等于"先清空再覆盖"，中途读取失败会留下未初始化的空对象。文件名经 StoreS 传原生，路径不存在等错误以原生错误码抛出。</para>
	///   <para><b>与相邻算子的取舍</b>DXF 来源用 ReadPolygonXldDxf（多一对状态输出与可调通用参数）；内存字节用 DeserializeXld。ARC/INFO generate 是纯文本折线格式，闭合性由文件内容决定。</para>
	///   <para><b>参数取向</b>无返回值；结果在本对象上。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   </code>
	///   <para><b>资源与坑</b>本对象用毕 Dispose；不要拿它当"返回新句柄"的算子用。</para>
	/// </remarks>
	public void ReadPolygonXldArcInfo(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(18);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把本多边形元组写出为 ARC/INFO generate 格式文本文件（算子 id 19，纯副作用，无输出参数）。</summary>
	/// <param name="fileName">目标文件路径；对已存在文件的覆盖行为依原生实现 [待实测]。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Store 本对象到输入 1、fileName 用 StoreS 到控制参数 0，CallProcedure 后不装载任何输出；本对象内容不变。</para>
	///   <para><b>约束或前提</b>本对象须已初始化；未初始化句柄的写出行为依原生实现 [待实测]。开放多边形与闭合多边形在文件里的记法差异未在包装层处理，以文件实际内容为准 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要 DXF 用 WritePolygonXldDxf；要内存流用 Serialize(Stream)。读回用成对的 ReadPolygonXldArcInfo。</para>
	///   <para><b>参数取向</b>void。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   a.WritePolygonXldArcInfo("roads_out.gen");
	///   </code>
	///   <para><b>资源与坑</b>不产生新句柄；调用期间 a 由 GC.KeepAlive 保住，调用结束前不可释放。</para>
	/// </remarks>
	public void WritePolygonXldArcInfo(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(19);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>合并两个分辨率层级得到的道路假设，输出确认后的路侧多边形（算子 id 37，元组阈值版）。</summary>
	/// <param name="modParallels">EdgePolygons 修正后的平行线对（mod parallels）。</param>
	/// <param name="extParallels">EdgePolygons 延伸后的平行线对（ext parallels）。</param>
	/// <param name="centerLines">待检验的路中心线多边形。</param>
	/// <param name="maxAngleParallel">两条平行线段间允许的最大夹角，弧度制（默认值即 π/6）。Default: 0.523598775598</param>
	/// <param name="maxAngleColinear">两条共线线段间允许的最大夹角，弧度制（默认值即 π/12）。Default: 0.261799387799</param>
	/// <param name="maxDistanceParallel">两条平行线段间允许的最大距离，像素。Default: 40</param>
	/// <param name="maxDistanceColinear">两条共线线段间允许的最大距离，像素。Default: 40</param>
	/// <returns>找到的路侧（roadsides）新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本对象是第一路图标输入（原生输入槽 1），modParallels/extParallels/centerLines 依次为槽 2/3/4；四个阈值作为控制参数钉住后 Store 到 0..3，调用后逐一 UnpinTuple。InitOCT 声明 1 个图标输出，返回 LoadNew 新句柄，所有输入不被改写。</para>
	///   <para><b>约束或前提</b>这是道路提取专用管线（配合 GenParallelsXld/EdgePolygons 等）的一环：阈值不生效与否取决于上游假设的质量；角度参数是弧度不是角度制。</para>
	///   <para><b>与相邻算子的取舍</b>四个阈值都是标量时用 double 重载（StoreD 直写，省钉固定/解钉）；本元组重载仅在需要给阈值传多值/批量配置时才值得用。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   JlXLDModPara mod = new JlXLDModPara();
	///   JlXLDExtPara ext = new JlXLDExtPara();
	///   JlXLDPoly center = new JlXLDPoly();
	///   center.ReadPolygonXldArcInfo("center.gen");
	///   JlTuple ap = 0.3;
	///   JlTuple ac = 0.2;
	///   JlTuple dp = 60.0;
	///   JlTuple dc = 50.0;
	///   JlXLDPoly roads = a.CombineRoadsXld(mod, ext, center, ap, ac, dp, dc);
	///   roads.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值须 Dispose；mod/ext/center 若是上游算子的新句柄也要各自释放；调用期间四个图标输入由 GC.KeepAlive 保住。</para>
	/// </remarks>
	public JlXLDPoly CombineRoadsXld(JlXLDModPara modParallels, JlXLDExtPara extParallels, JlXLDPoly centerLines, JlTuple maxAngleParallel, JlTuple maxAngleColinear, JlTuple maxDistanceParallel, JlTuple maxDistanceColinear)
	{
		IntPtr proc = JlNativeApi.PreCall(37);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, modParallels);
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
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modParallels);
		GC.KeepAlive(extParallels);
		GC.KeepAlive(centerLines);
		return obj;
	}

	/// <summary>合并两个分辨率层级得到的道路假设，输出确认后的路侧多边形（算子 id 37，标量阈值版）。</summary>
	/// <param name="modParallels">EdgePolygons 修正后的平行线对（mod parallels）。</param>
	/// <param name="extParallels">EdgePolygons 延伸后的平行线对（ext parallels）。</param>
	/// <param name="centerLines">待检验的路中心线多边形。</param>
	/// <param name="maxAngleParallel">两条平行线段间允许的最大夹角，弧度制。Default: 0.523598775598</param>
	/// <param name="maxAngleColinear">两条共线线段间允许的最大夹角，弧度制。Default: 0.261799387799</param>
	/// <param name="maxDistanceParallel">两条平行线段间允许的最大距离，像素。Default: 40</param>
	/// <param name="maxDistanceColinear">两条共线线段间允许的最大距离，像素。Default: 40</param>
	/// <returns>找到的路侧（roadsides）新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同算子 id 37：本对象→原生输入槽 1，modParallels/extParallels/centerLines 依次为槽 2/3/4；差别仅在四个阈值这里用 StoreD 直写单个 double（控制槽 0..3），无需钉固定/解钉。InitOCT 声明 1 个图标输出，返回 LoadNew 新句柄，输入不被改写。</para>
	///   <para><b>约束或前提</b>道路提取管线的一环，阈值效果取决于上游假设质量；角度参数是弧度不是角度制。距离阈值单位为像素。</para>
	///   <para><b>与相邻算子的取舍</b>四个阈值都是标量时用本重载（比元组版省钉固定开销）；需给某阈值传多值/批量配置时改用 JlTuple 重载。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   JlXLDModPara mod = new JlXLDModPara();
	///   JlXLDExtPara ext = new JlXLDExtPara();
	///   JlXLDPoly center = new JlXLDPoly();
	///   center.ReadPolygonXldArcInfo("center.gen");
	///   JlXLDPoly roads = a.CombineRoadsXld(mod, ext, center, 0.523598775598, 0.261799387799, 40.0, 40.0);
	///   roads.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值须 Dispose；mod/ext/center 若是上游算子新句柄也各自释放；调用期间四个图标输入由 GC.KeepAlive 保住。</para>
	/// </remarks>
	public JlXLDPoly CombineRoadsXld(JlXLDModPara modParallels, JlXLDExtPara extParallels, JlXLDPoly centerLines, double maxAngleParallel, double maxAngleColinear, double maxDistanceParallel, double maxDistanceColinear)
	{
		IntPtr proc = JlNativeApi.PreCall(37);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, modParallels);
		JlNativeApi.Store(proc, 3, extParallels);
		JlNativeApi.Store(proc, 4, centerLines);
		JlNativeApi.StoreD(proc, 0, maxAngleParallel);
		JlNativeApi.StoreD(proc, 1, maxAngleColinear);
		JlNativeApi.StoreD(proc, 2, maxDistanceParallel);
		JlNativeApi.StoreD(proc, 3, maxDistanceColinear);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modParallels);
		GC.KeepAlive(extParallels);
		GC.KeepAlive(centerLines);
		return obj;
	}

	/// <summary>在多边形集中抽取相互平行的线段对，返回描述这些平行关系的 JlXLDPara（算子 id 42，元组阈值版）。</summary>
	/// <param name="len">参与平行判定的单条线段最小长度，像素。Default: 10.0</param>
	/// <param name="dist">两条平行线段间允许的最大间距，像素。Default: 30.0</param>
	/// <param name="alpha">两条平行线段间允许的最大夹角差，弧度。Default: 0.15</param>
	/// <param name="merge">是否合并相邻的平行关系。Default: "true"</param>
	/// <returns>平行关系集合的新 JlXLDPara 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本对象（多边形）→原生输入槽 1；len/dist/alpha 作为钉住的 JlTuple 存到控制槽 0/1/2，调用后逐一 UnpinTuple；merge 用 StoreS 存到控制槽 3。InitOCT 声明 1 个输出，经 JlXLDPara.LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>输出是 JlXLDPara（平行线对），不是 JlXLDPoly，供后续 CombineRoadsXld 等道路算子消费；alpha 为弧度。本元组版可对某阈值给多值。</para>
	///   <para><b>与相邻算子的取舍</b>三个阈值都是标量时用 double 重载（StoreD 直写，省钉固定/解钉）；本元组版仅在需要多值阈值时才有收益。</para>
	///   <para><b>参数取向</b>返回 JlXLDPara.LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   JlTuple l = 10.0;
	///   JlTuple d = 30.0;
	///   JlTuple al = 0.15;
	///   JlXLDPara par = a.GenParallelsXld(l, d, al, "true");
	///   par.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新 JlXLDPara 句柄须 Dispose；len/dist/alpha 若为新建元组用完可释放；调用期间 this 由 GC.KeepAlive 保住。</para>
	/// </remarks>
	public JlXLDPara GenParallelsXld(JlTuple len, JlTuple dist, JlTuple alpha, string merge)
	{
		IntPtr proc = JlNativeApi.PreCall(42);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, len);
		JlNativeApi.Store(proc, 1, dist);
		JlNativeApi.Store(proc, 2, alpha);
		JlNativeApi.StoreS(proc, 3, merge);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(len);
		JlNativeApi.UnpinTuple(dist);
		JlNativeApi.UnpinTuple(alpha);
		err = JlXLDPara.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在本元组的多边形间找相互平行的线段对，输出平行关系对象（算子 id 42，标量参数版）。</summary>
	/// <param name="len">参与平行配对的单个多边形线段的最小长度，像素。Default: 10.0</param>
	/// <param name="dist">两线段被视为平行的最大间距，像素。Default: 30.0</param>
	/// <param name="alpha">两线段允许的最大方向差，弧度制。Default: 0.15</param>
	/// <param name="merge">是否把相邻的平行关系段合并成一个关系。Default: "true"</param>
	/// <returns>描述平行关系的 JlXLDPara 新句柄（不是多边形本身）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>len/dist/alpha 经 StoreD 直写控制槽 0..2，merge 经 StoreS 到槽 3；输出走 JlXLDPara.LoadNew。它是道路/双线路提取管线的第一步，产物是"哪些线段互为平行"的关系集，供 EdgePolygons 等后续算子加工，再喂给 CombineRoadsXld。</para>
	///   <para><b>约束或前提</b>输入应是线状多边形（长直线）；对闭合面状多边形配平行的语义未定义 [待实测]。merge 取 "true"/"false" 字符串而非布尔值。</para>
	///   <para><b>与相邻算子的取舍</b>阈值需多值 JlTuple 时用元组重载（钉固定/解钉）；标量场景用本重载。要真正得到"平行多边形"还需后续算子处理关系对象，本方法只给关系。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄，本对象不被改写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("roads.gen");
	///   JlXLDPara parallels = a.GenParallelsXld(20.0, 40.0, 0.2, "true");
	///   parallels.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlXLDPara 也是 JlXLD 系新句柄，用毕 Dispose。</para>
	/// </remarks>
	public JlXLDPara GenParallelsXld(double len, double dist, double alpha, string merge)
	{
		IntPtr proc = JlNativeApi.PreCall(42);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, len);
		JlNativeApi.StoreD(proc, 1, dist);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.StoreS(proc, 3, merge);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDPara.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把每个多边形按"线段"读出：起终点坐标、长度与法向角，六个 DOUBLE 元组一一对应（算子 id 43）。</summary>
	/// <param name="beginRow">各线段起点行坐标（row=y，向下为正）。</param>
	/// <param name="beginCol">各线段起点列坐标（column=x，向右为正）。</param>
	/// <param name="endRow">各线段终点行坐标。</param>
	/// <param name="endCol">各线段终点列坐标。</param>
	/// <param name="length">各线段长度，像素。</param>
	/// <param name="phi">各线段法向矢量的角度，弧度制。</param>
	/// <remarks>
	///   <para><b>功能说明</b>InitOCT 登记 6 个输出槽 0..5，全部用 JlTupleType.DOUBLE 装载——即便 length 这类量本可整数，也是 double 精度。六个元组等长，第 i 个分量对应第 i 个多边形，顺序即元组内元素顺序。</para>
	///   <para><b>约束或前提</b>面向两顶点的线状多边形；对多顶点/闭合多边形时"起终点"如何取 [待实测]。零长度线段（两顶点重合）会给出 length=0 而非报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要完整顶点序列用 GetPolygonXld（返回逐点 row/col）；本方法压扁成直线段参数，适合道路/线划分析。phi 是法向角不是方向角，相差 π/2 [待实测：符号约定]。</para>
	///   <para><b>参数取向</b>六个 out 全部是新 JlTuple 句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("lines.gen");
	///   a.GetLinesXld(out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol, out JlTuple length, out JlTuple phi);
	///   double firstLen = length[0];
	///   </code>
	///   <para><b>资源与坑</b>六个出参都是 JlTuple（实现 IDisposable），用完释放；漏掉任何一个只影响句柄类元素，但养成成对释放的习惯最稳。</para>
	/// </remarks>
	public void GetLinesXld(out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol, out JlTuple length, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(43);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out beginRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out beginCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out endRow);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out endCol);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out length);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>读出多边形的顶点坐标序列及每个多边形的代表长度与法向角（算子 id 44，四个 DOUBLE 元组）。</summary>
	/// <param name="row">全部多边形顶点的行坐标（row=y，向下为正），按多边形顺序串联。</param>
	/// <param name="col">全部多边形顶点的列坐标（column=x，向右为正），与 row 等长、逐点配对。</param>
	/// <param name="length">每个多边形的长度（像素），元素数 = 多边形数。</param>
	/// <param name="phi">每个多边形法向矢量的角度（弧度），元素数 = 多边形数。</param>
	/// <remarks>
	///   <para><b>功能说明</b>InitOCT 登记 4 个输出槽 0..3，均按 JlTupleType.DOUBLE 装载。注意两层级混合：row/col 是"逐顶点"串联序列，length/phi 是"逐多边形"标量，二者长度通常不同；哪个顶点对应哪个多边形需自行按顶点数切分，本算子不返回分组边界 [待实测：闭合多边形首末点是否重复计入]。</para>
	///   <para><b>约束或前提</b>顶点保持多边形原始顶点序；包装层不做顺/逆时针规范化。</para>
	///   <para><b>与相邻算子的取舍</b>只要直线段参数用 GetLinesXld；这里保留全部顶点，适合逐点重建多边形。phi 是法向角而非切向/方向角。</para>
	///   <para><b>参数取向</b>四个 out 均为新 JlTuple。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("polys.gen");
	///   a.GetPolygonXld(out JlTuple row, out JlTuple col, out JlTuple length, out JlTuple phi);
	///   int n = row.Length;
	///   </code>
	///   <para><b>资源与坑</b>四个出参用毕释放；误把 row[1] 当成"第二个多边形"是最常见的索引误用——它是第二个顶点。</para>
	/// </remarks>
	public void GetPolygonXld(out JlTuple row, out JlTuple col, out JlTuple length, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(44);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out col);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out length);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>在多边形折线的主点处把输入切成自由轮廓，输出是 JlXLDCont 而非多边形（算子 id 46）。</summary>
	/// <param name="mode">切分模式。Default: "polygon"</param>
	/// <param name="weight">灵敏度权重，越大对主点的响应越强。Default: 1</param>
	/// <param name="smooth">平滑掩码宽度（参与平滑的邻域点数）。Default: 5</param>
	/// <returns>切分后的新轮廓集 JlXLDCont 句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>mode 用 StoreS 写控制槽 0，weight/smooth 用 StoreI 写槽 1/2；输出经 JlXLDCont.LoadNew 装载。先在平滑上找主点（dominant points），再在这些点处断开，把每个输入多边形拆成若干轮廓。</para>
	///   <para><b>约束或前提</b>这是 XLD 多边形→XLD 轮廓的类转换：结果不再有多边形（线段）语义，不能再喂给只收 JlXLDPoly 的算子；平滑宽度 smooth 过小/过大都会改变主点检测，进而改变切分数量。"polygon" 之外可取值的完整清单未见包装层校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想把轮廓再变回多边形顶点表示是另一条转换路径（不在本类）；只是想去噪平滑而非切分，不要用本方法，它会改变元素个数。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄，本对象不被改写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   a.ReadPolygonXldArcInfo("polys.gen");
	///   JlXLDCont parts = a.SplitContoursXld("polygon", 1, 5);
	///   parts.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 JlXLDCont 是新句柄，用毕 Dispose；输出轮廓个数与输入多边形个数无关，按序号引用下游时须重数。</para>
	/// </remarks>
	public JlXLDCont SplitContoursXld(string mode, int weight, int smooth)
	{
		IntPtr proc = JlNativeApi.PreCall(46);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, weight);
		JlNativeApi.StoreI(proc, 2, smooth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对本多边形施加任意 2D 仿射变换（平移/旋转/缩放/剪切），返回变换后的新多边形。
	/// </summary>
	/// <param name="homMat2D">Input transformation matrix.</param>
	/// <returns>变换后的新多边形句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 48。homMat2D 以 Store 到参数 0、this 到参数 1，逐顶点乘上齐次矩阵得到新多边形；调用后 UnpinTuple(homMat2D)。矩阵按 row=y、column=x 的图像坐标约定作用。</para>
	///   <para><b>约束或前提</b>JlHomMat2D 派生自 JlData 且**不实现 IDisposable**，切勿对其调用 .Dispose()/using。奇异矩阵（行列式为 0）会把多边形压扁成点/线，退化后填充区域面积为 0 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>多边形级变换保持顶点数不变（每顶点变换）；若要对已栅格化区域做仿射请用区域族算子。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlHomMat2D hom = new JlHomMat2D();
	///   JlXLDPoly moved = poly.AffineTransPolygonXld(hom);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；hom 由托管对象自动回收，无需（也不能）手动释放。</para>
	/// </remarks>
	public JlXLDPoly AffineTransPolygonXld(JlHomMat2D homMat2D)
	{
		IntPtr proc = JlNativeApi.PreCall(48);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   返回属于本元组但不属于 objectsSub 的多边形集合（元组差集）。
	/// </summary>
	/// <param name="objectsSub">Object tuple 2.</param>
	/// <returns>差集结果的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 558。this→参数 1、objectsSub→参数 2，保留在 this 中出现、但不在 objectsSub 中的元素。这是对象元组集合层面的差，不是几何布尔差。</para>
	///   <para><b>约束或前提</b>"是否属于" 依据对象标识/相等性判定 [待实测：按引用还是按内容]，与多边形几何求交相减（DifferenceClosedPolygonsXld）不同。</para>
	///   <para><b>与相邻算子的取舍</b>想按几何区域做差用 DifferenceClosedPolygonsXld；想从一组结果里剔除某些对象用本算子。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly sub = a.Clone();
	///   JlXLDPoly diff = a.ObjDiff(sub);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose。调用期间靠 GC.KeepAlive 保住两路输入。</para>
	/// </remarks>
	public JlXLDPoly ObjDiff(JlXLDPoly objectsSub)
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
	///   从本元组第 index 个起复制 numObj 个元素到新元组（在数据库内建立副本）。
	/// </summary>
	/// <param name="index">Starting index of the objects to be copied. Default: 1</param>
	/// <param name="numObj">Number of objects to be copied or -1. Default: 1</param>
	/// <returns>复制出的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 568。index/numObj 分别 StoreI 到参数 0/1。index 从 1 起（1 基，与基类 `Default: 1` 一致）；numObj=-1 表示复制到末尾。产出为独立的对象副本，不共享句柄内部数据。</para>
	///   <para><b>约束或前提</b>index+numObj 越界触发原生错误 [待实测]。numObj 为负且非 -1 的行为 [待实测]。副本与原对象各自需独立 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>只要引用不复制用 SelectObj；需要数据库内真实副本用本算子。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly copy = a.CopyObj(1, 1);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；否则副本泄漏在原生数据库中。</para>
	/// </remarks>
	public new JlXLDPoly CopyObj(int index, int numObj)
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
	///   把 objects2 的元素接在本元组之后，返回拼接后的新元组（this 在前、objects2 在后）。
	/// </summary>
	/// <param name="objects2">Object tuple 2.</param>
	/// <returns>拼接后的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 569。this→参数 1、objects2→参数 2，结果顺序固定为 [this..., objects2...]。不修改任一输入，产出全新元组。</para>
	///   <para><b>约束或前提</b>结果元素序号即拼接后次序；若下游按序号引用，需重新计算映射。空元组参与拼接合法，仅返回另一侧。</para>
	///   <para><b>与相邻算子的取舍</b>只想在指定位置插入用 InsertObj；本算子只做尾部追加式拼接。</para>
	///   <para><b>参数取向</b>返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly b = a.Clone();
	///   JlXLDPoly all = a.ConcatObj(b);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；a、b 独立保留。调用期间靠 GC.KeepAlive 保住两路输入。</para>
	/// </remarks>
	public JlXLDPoly ConcatObj(JlXLDPoly objects2)
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
	///   从本多边形元组中按元组序号挑选元素，组成新元组（可重复、可乱序选取同一元素）。
	/// </summary>
	/// <param name="index">Indices of the objects to be selected. Default: 1</param>
	/// <returns>被选中元素组成的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 572，与 int 重载同 id。index 作为钉住的 JlTuple Store 到参数 0（调用后 UnpinTuple）。序号 1 基（与基类 `Default: 1` 一致）。结果按 index 给出的顺序排列，允许重复与任意次序，因而可用来重排或复制元素。</para>
	///   <para><b>约束或前提</b>任一序号越界触发原生错误 [待实测]。挑选后顺序即 index 顺序，若下游依赖序号对应上游检测顺序，需自行维护映射。</para>
	///   <para><b>与相邻算子的取舍</b>单元素用 int 重载省钉固定开销；批量/重排用本元组版。</para>
	///   <para><b>参数取向</b>this Store 到参数 1。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlTuple idx = new JlTuple(new int[] { 1 });
	///   JlXLDPoly picked = a.SelectObj(idx);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；index 元组用完可释放。</para>
	/// </remarks>
	public new JlXLDPoly SelectObj(JlTuple index)
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
	///   从本多边形元组中挑选单个序号对应的元素，返回新元组（标量版，StoreI 直写）。
	/// </summary>
	/// <param name="index">Indices of the objects to be selected. Default: 1</param>
	/// <returns>被选中元素组成的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 572，与元组重载同 id。index 用 StoreI 直写单个 1 基序号，无钉固定/解钉开销。返回仅含该元素的新元组。</para>
	///   <para><b>约束或前提</b>序号 1 基，越界触发原生错误 [待实测]。需要选多个或重排时用 JlTuple 重载。</para>
	///   <para><b>与相邻算子的取舍</b>只取一个元素用本重载最直接。</para>
	///   <para><b>参数取向</b>this Store 到参数 1。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly one = a.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕须 Dispose。</para>
	/// </remarks>
	public new JlXLDPoly SelectObj(int index)
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
	///   带元组容差比较本元组与 objects2 是否相等（钉住 epsilon 元组 Store，可对不同分量给不同容差）。
	/// </summary>
	/// <param name="objects2">Test objects.</param>
	/// <param name="epsilon">Maximum allowed difference between two gray values or coordinates etc. Default: 0.0</param>
	/// <returns>布尔结果值（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 573，与标量重载同 id。epsilon 作为 JlTuple 直接 Store 到参数 0（调用后 UnpinTuple 解钉），允许一次传入多个容差值分别约束不同量。</para>
	///   <para><b>约束或前提</b>若只关心单一全局容差用标量版省钉固定开销；epsilon 元素数与被比较量不匹配时按何种规则广播 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>多容差用本元组版，单容差用 double 重载。</para>
	///   <para><b>参数取向</b>this→参数 1，objects2→参数 2，epsilon→参数 0。返回 int，无新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly b = a.Clone();
	///   JlTuple eps = new JlTuple(0.0);
	///   int same = a.CompareObj(b, eps);
	///   </code>
	///   <para><b>资源与坑</b>无新句柄产生；epsilon 是 JlTuple，用完可释放。</para>
	/// </remarks>
	public int CompareObj(JlXLDPoly objects2, JlTuple epsilon)
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
	///   带单一标量容差比较本元组与 objects2 是否相等（StoreD 直写 epsilon，返回 0/1）。
	/// </summary>
	/// <param name="objects2">Test objects.</param>
	/// <param name="epsilon">Maximum allowed difference between two gray values or coordinates etc. Default: 0.0</param>
	/// <returns>布尔结果值（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 573，与元组重载同 id。epsilon 是两坐标/灰度值允许的最大差，此处用 StoreD 直写单个 double。经 LoadI 装载整数结果。</para>
	///   <para><b>约束或前提</b>epsilon=0.0 退化为精确比较（等同 TestEqualObj 语义 [待实测]）。对浮点坐标建议给非零容差以避免噪声误判不等。单位随被比较量（坐标为像素）。</para>
	///   <para><b>与相邻算子的取舍</b>单一容差用本标量版；需对不同特征用不同容差用 JlTuple 重载。</para>
	///   <para><b>参数取向</b>返回 int，无新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly b = a.Clone();
	///   int same = a.CompareObj(b, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>无新句柄产生；调用期间靠 GC.KeepAlive 保住 a 与 objects2。</para>
	/// </remarks>
	public int CompareObj(JlXLDPoly objects2, double epsilon)
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
	///   逐元素严格比较本元组与 objects2 是否相等（无容差），返回 0/1 布尔整数。
	/// </summary>
	/// <param name="objects2">Comparative objects.</param>
	/// <returns>布尔结果值（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 576，无 epsilon 参数，按精确相等比较两路图标输入（this→参数 1，objects2→参数 2）。经 LoadI 以整数装载返回值。</para>
	///   <para><b>约束或前提</b>浮点坐标的完全相等判定对噪声敏感，若需容差请改用 CompareObj(…, epsilon)。返回值是否严格为 0/1 [待实测]。返回按两元组的对应位置逐元素比较 [待实测：多元素时的语义]。</para>
	///   <para><b>与相邻算子的取舍</b>已知两对象由同一算子生成、期望逐位一致时用本算子；跨来源或有浮点抖动时用带 epsilon 的 CompareObj。</para>
	///   <para><b>参数取向</b>返回 int，无新句柄产生。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly b = a.Clone();
	///   int equal = a.TestEqualObj(b);
	///   </code>
	///   <para><b>资源与坑</b>不产生新句柄；a、b 各自独立释放。调用期间靠 GC.KeepAlive 保住两路输入。</para>
	/// </remarks>
	public int TestEqualObj(JlXLDPoly objects2)
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
	///   把每个 XLD 多边形按其边界填充成区域，返回新生成的区域句柄。
	/// </summary>
	/// <param name="mode">Fill mode of the region(s). Default: "filled"</param>
	/// <returns>生成的新区域句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 581。以本对象的多边形顶点为边界轮廓生成 JlRegion。mode="filled" 得到实心填充区域；其它模式（如按轮廓描边）具体取值 [待实测]。这是 XLD→区域 的单向转换。</para>
	///   <para><b>约束或前提</b>多边形必须闭合才有确定内部；开口/退化（共线、零面积）多边形填充结果不确定 [待实测]。顶点按 row=y、column=x 定义，区域像素以行为单位栅格化。</para>
	///   <para><b>与相邻算子的取舍</b>只要轮廓点集保持 XLD 表示就别用本算子（转区域会丢失连续顶点精度）；需要面积/矩等区域度量时才转区域。</para>
	///   <para><b>参数取向</b>mode 用 StoreS 传字符串，输出经 JlRegion.LoadNew 得到新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlRegion reg = poly.GenRegionPolygonXld("filled");
	///   </code>
	///   <para><b>资源与坑</b>返回的 JlRegion 是新句柄，用毕须 Dispose；本对象 poly 独立保留。</para>
	/// </remarks>
	public JlRegion GenRegionPolygonXld(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(581);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   依据规则网格点计算畸变图到校正图的映射，返回含映射数据的图像，并输出网格多边形（元组版 rotation）。
	/// </summary>
	/// <param name="image">Input image.</param>
	/// <param name="meshes">Output contours.</param>
	/// <param name="gridSpacing">Distance of the grid points in the rectified image.</param>
	/// <param name="rotation">Rotation to be applied to the point grid. Default: "auto"</param>
	/// <param name="row">Row coordinates of the grid points.</param>
	/// <param name="column">Column coordinates of the grid points.</param>
	/// <param name="mapType">Type of mapping. Default: "bilinear"</param>
	/// <returns>含映射数据的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1107。本对象（多边形）作为第二路图标输入 Store 到参数 2，image 输入参数 1；控制参数 gridSpacing/rotation(row/column)/mapType 分别 Store 到 0..4。产出两路输出：JlImage.LoadNew 得到映射图像（返回值），LoadNew 得到 out meshes 网格多边形。</para>
	///   <para><b>约束或前提</b>gridSpacing 为校正图中网格点间距（像素），应为正数 [待实测：0/负数行为]。rotation 走元组版直接 Store 并在调用后 UnpinTuple，"auto" 由算法自动取向；显式给角度时单位为弧度 [待实测]。row/column 是网格点坐标，须成对等长。</para>
	///   <para><b>与相邻算子的取舍</b>只传单一旋转标量时用 string rotation 重载；rotation 需多值/批量配置时用本元组重载。</para>
	///   <para><b>参数取向</b>返回新 JlImage，out 出参 meshes 也是新句柄，二者都需释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage();
	///   JlTuple row = new JlTuple(new double[] { 10.0 });
	///   JlTuple col = new JlTuple(new double[] { 10.0 });
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlXLDPoly meshes;
	///   JlImage map = poly.GenGridRectificationMap(img, out meshes, 32, new JlTuple("auto"), row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回的 map 与 out meshes 都是新句柄，用毕各自 Dispose；row/column/rotation 元组用完可释放。调用期间靠 GC.KeepAlive 保住 this 与 image。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDPoly meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
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
	///   依据规则网格点计算畸变图到校正图的映射，返回含映射数据的图像，并输出网格多边形（字符串版 rotation，用 StoreS 直写单个旋转模式）。
	/// </summary>
	/// <param name="image">Input image.</param>
	/// <param name="meshes">Output contours.</param>
	/// <param name="gridSpacing">Distance of the grid points in the rectified image.</param>
	/// <param name="rotation">Rotation to be applied to the point grid. Default: "auto"</param>
	/// <param name="row">Row coordinates of the grid points.</param>
	/// <param name="column">Column coordinates of the grid points.</param>
	/// <param name="mapType">Type of mapping. Default: "bilinear"</param>
	/// <returns>含映射数据的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1107，与元组版同 id。本重载把 rotation 作为字符串用 StoreS 直写（如 "auto"），无需钉固定与解钉；其余图标输入 this(参数 2)、image(参数 1) 与控制输入 gridSpacing(0)、row(2)、column(3)、mapType(4) 布局同元组版。双路输出：返回值 JlImage 映射图、out meshes 网格多边形。</para>
	///   <para><b>约束或前提</b>gridSpacing 应为正的像素间距 [待实测：非正值行为]；row/column 须成对等长；mapType 常用 "bilinear"，其它取值语义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>rotation 只有一个模式串时用本重载；需多值 rotation 用 JlTuple 重载。</para>
	///   <para><b>参数取向</b>返回新 JlImage，out meshes 也是新句柄，均须释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage();
	///   JlTuple row = new JlTuple(new double[] { 10.0 });
	///   JlTuple col = new JlTuple(new double[] { 10.0 });
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlXLDPoly meshes;
	///   JlImage map = poly.GenGridRectificationMap(img, out meshes, 32, "auto", row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回的 map 与 out meshes 都是新句柄，用毕各自 Dispose；row/column 元组用完可释放。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDPoly meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
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
	///   用序列化字节数组重建本多边形对象（算子 id 1552，原地改写，无返回值）。
	/// </summary>
	/// <param name="serializedItemHandle">由 SerializeXld 得到的序列化字节数组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>形参虽名为 handle，实为托管 byte[]。方法用 using 包住 JlSerializationBuffer 承载字节，先 Dispose() 释放本对象旧句柄，再 Load(proc,1) 把重建的多边形原地写回本对象。与 SerializeXld 成对使用。</para>
	///   <para><b>约束或前提</b>字节须来自同版本 SerializeXld/Read 通道，格式不符会以原生错误码抛出并令本对象处于空态 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>与 .NET Deserialize(Stream) 不同，本方法走原生反序列化通道，作用于当前实例。</para>
	///   <para><b>参数取向</b>buffer 经 using 自动释放，调用期间靠 GC.KeepAlive 保住 this 与 buffer。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly src = new JlXLDPoly();
	///   byte[] data = src.SerializeXld();
	///   JlXLDPoly dst = new JlXLDPoly();
	///   dst.DeserializeXld(data);
	///   </code>
	///   <para><b>资源与坑</b>本对象被原地改写，用毕仍须 Dispose；buffer 由 using 管理无需手动释放。</para>
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
	///   把本多边形序列化为字节数组（算子 id 1553），供跨进程/存储传输。
	/// </summary>
	/// <returns>序列化后的字节缓冲区（托管数组，非句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>Store 本对象到参数 1，InitOCT(0) 声明零个图标输出，随后由 JlSerializationBuffer.LoadBytes 把原生结果拷贝成托管 byte[]。返回值是普通数组，不涉及原生句柄。</para>
	///   <para><b>约束或前提</b>与 DeserializeXld 成对：字节格式仅对本库版本兼容 [待实测：跨版本兼容性]。空对象也会被序列化为合法字节数组 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>与 .NET 的 Serialize(Stream) 不同，本方法走原生序列化通道，专用于 XLD 句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   byte[] data = poly.SerializeXld();
	///   </code>
	///   <para><b>资源与坑</b>返回的 byte[] 是托管内存，由 GC 回收，无需手动释放；本对象不变。</para>
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
	///   从 DXF 文件读取多边形并原地载入本对象（元组版，可一次传多组通用参数名/值），返回状态元组。
	/// </summary>
	/// <param name="fileName">Name of the DXF file.</param>
	/// <param name="genParamName">Names of the generic parameters that can be adjusted for the DXF input. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters that can be adjusted for the DXF input. Default: []</param>
	/// <returns>状态信息元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1554，与标量重载同 id。方法先 Dispose() 丢弃旧句柄，再 Load(proc,1) 把读到的多边形原地写回本对象。genParamName/genParamValue 是配对的参数名与值序列（钉住后 Store，调用后 UnpinTuple），可一次配置多个 DXF 输入项。</para>
	///   <para><b>约束或前提</b>两个元组长度应一致，否则名/值错位。读取失败时本对象可能处于空/未定义状态 [待实测]。传空元组表示不做输入调整。</para>
	///   <para><b>与相邻算子的取舍</b>需要多参数用本元组版；单个参数用 string/double 重载更省。</para>
	///   <para><b>参数取向</b>状态经 JlTuple.LoadNew 以元组装载并返回；多边形原地写回本对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlTuple status = poly.ReadPolygonXldDxf("poly.dxf", new JlTuple(), new JlTuple());
	///   </code>
	///   <para><b>资源与坑</b>本对象被原地改写，用毕须 Dispose；返回的状态 JlTuple 也实现了 IDisposable，用完可释放。</para>
	/// </remarks>
	public JlTuple ReadPolygonXldDxf(string fileName, JlTuple genParamName, JlTuple genParamValue)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1554);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = Load(proc, 1, err);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   从 DXF 文件读取多边形并原地载入本对象（标量版），返回状态字符串。
	/// </summary>
	/// <param name="fileName">Name of the DXF file.</param>
	/// <param name="genParamName">Names of the generic parameters that can be adjusted for the DXF input. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters that can be adjusted for the DXF input. Default: []</param>
	/// <returns>状态信息字符串。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1554，与元组重载同 id。方法先 Dispose() 丢弃本对象旧句柄，再 Load(proc,1) 把读到的多边形原地写回本对象，故本对象被改写而非返回新句柄。genParamName 用 StoreS、genParamValue 用 StoreD 各写单个通用参数。</para>
	///   <para><b>约束或前提</b>调用前本对象旧内容被释放，读取失败时本对象可能处于空/未定义状态 [待实测]。传空的参数名意味着不做任何 DXF 输入调整。</para>
	///   <para><b>与相邻算子的取舍</b>只需设一个通用参数用本标量版；需一次传多组参数名/值用 JlTuple 重载。</para>
	///   <para><b>参数取向</b>状态经 LoadS 以字符串装载并返回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   string status = poly.ReadPolygonXldDxf("poly.dxf", "", 0.0);
	///   </code>
	///   <para><b>资源与坑</b>本对象被原地改写，用毕仍须 Dispose；返回值是字符串无句柄。</para>
	/// </remarks>
	public string ReadPolygonXldDxf(string fileName, string genParamName, double genParamValue)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1554);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.StoreD(proc, 2, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}

	/// <summary>
	///   把本多边形写出为 DXF 文件（算子 id 1555，无返回值）。
	/// </summary>
	/// <param name="fileName">Name of the DXF file.</param>
	/// <remarks>
	///   <para><b>功能说明</b>将本对象的多边形几何序列化到指定 DXF 文件。Store 本对象到参数 1，fileName 用 StoreS 到参数 0，InitOCT 未声明，纯副作用调用。</para>
	///   <para><b>约束或前提</b>路径不可写或磁盘错误会以原生错误码抛出 [待实测：是否抛异常]。多边形顶点按 row=y、column=x 映射到 DXF 的 y/x [待实测：轴向是否交换]。</para>
	///   <para><b>与相邻算子的取舍</b>与 ReadPolygonXldDxf 成对；本方法只写不读，且不改变本对象内容。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   poly.WritePolygonXldDxf("poly.dxf");
	///   </code>
	///   <para><b>资源与坑</b>无新句柄产生，本对象不受影响。</para>
	/// </remarks>
	public void WritePolygonXldDxf(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1555);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   选出覆盖了多个给定点（元组版，一次传入成对的 row/column 序列）的所有多边形。
	/// </summary>
	/// <param name="row">Line coordinate of the test point. Default: 100.0</param>
	/// <param name="column">Column coordinate of the test point. Default: 100.0</param>
	/// <returns>包含测试点的新多边形句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1595，与标量重载同 id。row 与 column 是配对的坐标序列，逐点判断覆盖关系。多边形被任一点命中即入选 [待实测：多点是"或"还是"且"语义]。坐标遵循 row=y（向下为正）、column=x（向右为正）。</para>
	///   <para><b>约束或前提</b>row 与 column 长度应一致，否则配对错位。本重载把钉住的 row/column 直接 Store，调用后各自 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>单点测试用标量重载更省；多点批量筛选用本版。</para>
	///   <para><b>参数取向</b>this Store 到参数 1。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlTuple rows = new JlTuple(new double[] { 100.0 });
	///   JlTuple cols = new JlTuple(new double[] { 100.0 });
	///   JlXLDPoly hits = poly.SelectXldPoint(rows, cols);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；传入的 row/column 元组用完可释放。</para>
	/// </remarks>
	public new JlXLDPoly SelectXldPoint(JlTuple row, JlTuple column)
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
	///   选出覆盖了给定点 (row,column) 的所有多边形（标量版，StoreD 直写单点）。
	/// </summary>
	/// <param name="row">Line coordinate of the test point. Default: 100.0</param>
	/// <param name="column">Column coordinate of the test point. Default: 100.0</param>
	/// <returns>包含该测试点的新多边形句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1595。以 (row,column) 为测试点，返回本元组中"经过/包含"该点的多边形集合。坐标遵循 row=y（向下为正）、column=x（向右为正）。</para>
	///   <para><b>约束或前提</b>点在多边形边界上时的归属 [待实测]；无任何多边形命中时返回空句柄。本重载仅测一个点，多目标点请用 JlTuple 重载。</para>
	///   <para><b>与相邻算子的取舍</b>与元组重载同 id：单点用 StoreD 直写省钉固定开销，多点批量筛选用元组版。</para>
	///   <para><b>参数取向</b>this Store 到参数 1，row/column StoreD 到 0/1。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlXLDPoly hits = poly.SelectXldPoint(100.0, 100.0);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕须 Dispose。</para>
	/// </remarks>
	public new JlXLDPoly SelectXldPoint(double row, double column)
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
	///   按形状特征区间筛选多边形，仅保留特征落在 [min,max] 内的元素（元组版，可一次传多个特征与多组阈值）。
	/// </summary>
	/// <param name="features">Shape features to be checked. Default: "area"</param>
	/// <param name="operation">Operation type between the individual features. Default: "and"</param>
	/// <param name="min">Lower limits of the features or 'min'. Default: 150.0</param>
	/// <param name="max">Upper limits of the features or 'max'. Default: 99999.0</param>
	/// <returns>满足条件的多边形新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1597。features 是要检查的形状特征名列表，min/max 为对应下/上限，operation 决定多个特征间的组合方式（"and" 全满足、"or" 任一满足 [待实测：or 是否支持]）。本元组版把钉住的 features/min/max 直接 Store，调用后各自 UnpinTuple。</para>
	///   <para><b>约束或前提</b>面积类特征以像素计；特征值需在闭区间 [min,max] 内才保留，边界是否包含 [待实测]。features 与 min/max 长度需匹配，标量阈值通常被广播到全部特征 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>与 string 重载同 id：需要多特征/多阈值时用本元组版，单特征时可用标量版避免钉固定开销。</para>
	///   <para><b>参数取向</b>this Store 到参数 1。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlTuple feats = new JlTuple(new string[] { "area" });
	///   JlXLDPoly kept = poly.SelectShapeXld(feats, "and", new JlTuple(150.0), new JlTuple(99999.0));
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；传入的三个 JlTuple 用完可各自释放。</para>
	/// </remarks>
	public new JlXLDPoly SelectShapeXld(JlTuple features, string operation, JlTuple min, JlTuple max)
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
	///   按单个形状特征的标量区间筛选多边形（标量版，用 StoreS/StoreD 直写，无钉固定元组开销）。
	/// </summary>
	/// <param name="features">Shape features to be checked. Default: "area"</param>
	/// <param name="operation">Operation type between the individual features. Default: "and"</param>
	/// <param name="min">Lower limits of the features or 'min'. Default: 150.0</param>
	/// <param name="max">Upper limits of the features or 'max'. Default: 99999.0</param>
	/// <returns>满足条件的多边形新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 与元组版同为 1597。本标量版只携带一个特征名与一对 double 阈值，用 StoreS/StoreD 直写，省去钉固定与解钉。min/max 为该特征的下/上限。</para>
	///   <para><b>约束或前提</b>面积类特征以像素计，边界是否含 [min,max] 端点 [待实测]。若要同时检查多个特征，请改用元组重载，本重载传单个字符串只会检查那一个特征。</para>
	///   <para><b>与相邻算子的取舍</b>需要多特征/多区间组合时用 JlTuple 重载；只按单一特征粗筛时用本重载更省事。</para>
	///   <para><b>参数取向</b>this Store 到参数 1。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlXLDPoly kept = poly.SelectShapeXld("area", "and", 150.0, 99999.0);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕须 Dispose。</para>
	/// </remarks>
	public new JlXLDPoly SelectShapeXld(string features, string operation, double min, double max)
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
	///   按 <paramref name="type"/> 指定的几何形状重建每个多边形（如凸包），返回形状变换后的新句柄。
	/// </summary>
	/// <param name="type">Type of transformation. Default: "convex"</param>
	/// <returns>形状变换后的新多边形句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 1608。以本对象为输入，用 type 指定的目标形状替换原轮廓几何。"convex" 生成顶点为原点集凸包的多边形；其余形状（如外接矩形/椭圆/圆等）具体名与语义 [待实测]，以本库现有英文文档与原生算子为准，不臆造。</para>
	///   <para><b>约束或前提</b>退化输入（共线点、单点、零面积）在求凸包时结果可能为线段或点，顶点数随之变化 [待实测]。顶点坐标遵循 row=y（向下为正）、column=x（向右为正）。</para>
	///   <para><b>与相邻算子的取舍</b>若只需要区域层面的形状变换应改用 JlRegion 的对应算子；本算子保持 XLD 表示，输出仍是多边形顶点集。</para>
	///   <para><b>参数取向</b>type 用 StoreS 传字符串。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly poly = new JlXLDPoly();
	///   JlXLDPoly hull = poly.ShapeTransXld("convex");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕须 Dispose；本对象 poly 独立保留。</para>
	/// </remarks>
	public new JlXLDPoly ShapeTransXld(string type)
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
	///   把 <paramref name="objectsInsert"/> 插入本多边形元组的第 <paramref name="index"/> 个位置，返回扩展后的新元组。
	/// </summary>
	/// <param name="objectsInsert">Object tuple to insert.</param>
	/// <param name="index">Index to insert objects.</param>
	/// <returns>插入后的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 2003。objectsInsert 的所有元素被整体插入到本元组 index 指定处，原元素顺序保持不变，结果是全新元组，本对象与 objectsInsert 不被改写。</para>
	///   <para><b>约束或前提</b>index 从 1 开始；index 大于当前元素数时追加到末尾 [待实测：越界是追加还是报错]。原生参数序为 index、this、objectsInsert（Store 到 0/1/2），与 C# 形参序不同。</para>
	///   <para><b>参数取向</b>int 参数用 StoreI 直写。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly add = a.Clone();
	///   JlXLDPoly result = a.InsertObj(add, 1);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose。原生调用期间靠 GC.KeepAlive 保住 a 与 objectsInsert。</para>
	/// </remarks>
	public JlXLDPoly InsertObj(JlXLDPoly objectsInsert, int index)
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
	///   从本多边形元组中删除 <paramref name="index"/> 元组所列出的多个元素，返回剩余新元组。
	/// </summary>
	/// <param name="index">Indices of the objects to be removed.</param>
	/// <returns>删除后剩余的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 2005，与 int 重载同 id。本重载把钉住的多序号 JlTuple 直接 Store，调用后 UnpinTuple 解钉；int 重载用 StoreI 单值。可一次性删除多个位置。</para>
	///   <para><b>约束或前提</b>序号从 1 开始；任一序号越界会触发原生错误 [待实测：越界行为]。删除顺序不影响结果。</para>
	///   <para><b>参数取向</b>this Store 到参数 1，index Store 到参数 0。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlTuple idx = new JlTuple(new int[] { 1 });
	///   JlXLDPoly rest = a.RemoveObj(idx);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose；index 是 JlTuple，用完可一并释放。</para>
	/// </remarks>
	public new JlXLDPoly RemoveObj(JlTuple index)
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
	///   从本多边形元组中删除第 <paramref name="index"/> 个元素，返回剩余元素组成的新元组。
	/// </summary>
	/// <param name="index">Indices of the objects to be removed.</param>
	/// <returns>删除后剩余的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算子 id 2005，标量重载用 StoreI 直写单个序号，无钉固定元组开销。本对象不被改写，产出删除后的新元组。</para>
	///   <para><b>约束或前提</b>序号从 1 开始（与基类 JlObject 的 `Default: 1`、示例 `SelectObj(1)` 一致），越界会触发原生错误 [待实测：是否静默忽略]。删除到空时返回空元组句柄 [待实测]。</para>
	///   <para><b>参数取向</b>this Store 到参数 1，index StoreI 到参数 0，InitOCT 声明 1 个输出。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly rest = a.RemoveObj(1);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕须 Dispose；本对象 a 仍保留其余元素可继续使用。</para>
	/// </remarks>
	public new JlXLDPoly RemoveObj(int index)
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
	///   用 <paramref name="objectsReplace"/> 替换本元组中由元组 <paramref name="index"/> 指定的多个位置，返回新元组。
	/// </summary>
	/// <param name="objectsReplace">Element(s) to replace.</param>
	/// <param name="index">Index/Indices of elements to be replaced.</param>
	/// <returns>替换后的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用算子 id 2006。差别在于本重载把钉住的 JlTuple 直接 Store（调用后 UnpinTuple 解钉），而 int 重载用 StoreI 直写单值。index 元组可含多个 1 基序号，objectsReplace 的元素按序逐一替换这些位置。</para>
	///   <para><b>约束或前提</b>index 各元素不得越界；index 元素个数应与 objectsReplace 元素个数匹配，否则替换错位 [待实测]。</para>
	///   <para><b>参数取向</b>this 为原元组，index 钉固定元组传入原生参数 0，objectsReplace 传入参数 2。返回 LoadNew 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly repl = a.Clone();
	///   JlTuple idx = new JlTuple(new int[] { 1 });
	///   JlXLDPoly result = a.ReplaceObj(repl, idx);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，须 Dispose。JlTuple 亦实现 IDisposable，用完也应释放。</para>
	/// </remarks>
	public JlXLDPoly ReplaceObj(JlXLDPoly objectsReplace, JlTuple index)
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
	///   用 <paramref name="objectsReplace"/> 中的多边形替换本元组第 <paramref name="index"/> 个元素，返回替换后的新元组。
	/// </summary>
	/// <param name="objectsReplace">Element(s) to replace.</param>
	/// <param name="index">Index/Indices of elements to be replaced.</param>
	/// <returns>替换后的新多边形元组句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本对象是"待修改的原元组"（原生侧 Store 到参数 1，被替换集合 Store 到参数 2，索引 Store 到参数 0，故原生参数序为 index、this、objectsReplace，与 C# 形参序不同）。用 objectsReplace 的元素替换本元组中 index 指定位置的对象，产出一个全新的元组，原对象与本对象均不被改写。</para>
	///   <para><b>约束或前提</b>index 为 1 基序号（与基类 `Default: 1` 一致），越界会触发原生错误。objectsReplace 的元素个数需与被替换位置匹配；替换后总长度可能变化。</para>
	///   <para><b>参数取向</b>本方法为 JlObject 通用容器算子的多边形重载（非 new 隐藏）。返回 LoadNew 出的新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDPoly a = new JlXLDPoly();
	///   JlXLDPoly repl = a.Clone();
	///   JlXLDPoly result = a.ReplaceObj(repl, 1);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄，用毕须 Dispose；a 与 repl 在本调用结束后仍可独立释放。原生调用期间靠 GC.KeepAlive 保住 a 与 repl，二者不可在调用中途释放。</para>
	/// </remarks>
	public JlXLDPoly ReplaceObj(JlXLDPoly objectsReplace, int index)
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
