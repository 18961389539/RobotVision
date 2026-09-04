using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of a XLD distance transformation.</summary>
[Serializable]
public class JlXLDDistTrans : JlHandle, ISerializable, ICloneable
{
	/// <summary>构造持有 UNDEF（空）句柄的未初始化实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDDistTrans()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDDistTrans(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>从 <see cref="JlHandle"/> 句柄包装构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDDistTrans(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("xld_dist_trans");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDDistTrans obj)
	{
		obj = new JlXLDDistTrans(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDDistTrans[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlXLDDistTrans[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlXLDDistTrans(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   Read an XLD distance transform from a file.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>read_xld_disttrans</c>（算子 id 1292）：从 <see cref="WriteDistanceTransformXld(string)"/> 写出的文件
	///   装载一个 XLD 距离变换对象；句柄经 <c>InitOCT</c> 以<b>新句柄</b>装载（<c>Load</c> 到本实例），由本实例负责释放。文件中保存了构建时的全部状态：参考轮廓、mode、max_distance。</para>
	///   <para><b>约束或前提</b>文件必须是本库 write/serialize 族写出的专有格式，通用图像或轮廓 <c>.dat</c> 读不进来；文件不存在或内容损坏时原生调用在 <c>PostCall</c> 处抛
	///   <c>JlOperatorException</c>，此时构造失败、本实例没有有效句柄。</para>
	///   <para><b>与相邻成员的取舍</b>想保留现有实例、把文件内容覆盖到它身上，用 <see cref="ReadDistanceTransformXld(string)"/>（它会先 <c>Dispose()</c> 旧句柄再原地重建）；
	///   本构造器用于造一个新对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont test = new JlXLDCont(new double[] { 20.0, 40.0, 40.0 }, new double[] { 20.0, 20.0, 40.0 });
	///   using (JlXLDDistTrans dt = new JlXLDDistTrans(@"C:\vision\ref_disttrans.dat"))
	///   using (JlXLDCont withDist = dt.ApplyDistanceTransformXld(test))
	///   {
	///       JlTuple dist = withDist.GetContourAttribXld("distance");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>装载进来的变换与 <see cref="ApplyDistanceTransformXld(JlXLDCont)"/> 的返回轮廓都是原生句柄、需要释放（示例用 <c>using</c> 包住）；
	///   <c>GetContourAttribXld</c> 返回的是纯数值元组，可不处理。</para>
	/// </remarks>
	public JlXLDDistTrans(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1292);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create the XLD distance transform.
	/// </summary>
	/// <param name="contour">Reference contour(s).</param>
	/// <param name="mode">Compute the distance to points ('point_to_point') or entire segments ('point_to_segment'). Default: "point_to_point"</param>
	/// <param name="maxDistance">Maximum distance of interest. Default: 20.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>gen_xld_disttrans</c>（算子 id 1299）：把 <paramref name="contour"/> 存成参考轮廓，预构建一个可复用的"轮廓距离查询上下文"；
	///   之后对同一参考形状反复量距离用 <see cref="ApplyDistanceTransformXld(JlXLDCont)"/>，构建成本一次付清。点坐标约定 row=y（向下）、column=x（向右），单位像素。</para>
	///   <para><b>约束或前提</b><paramref name="contour"/> 可含多条轮廓，它们合起来构成参考点集；空轮廓构建失败抛 <c>JlOperatorException</c>。
	///   <paramref name="mode"/> 取 <c>point_to_point</c> 时查询点到的是<b>最近参考点</b>（对参考点采样密度敏感）；取 <c>point_to_segment</c> 时到的是相邻参考点连线段
	///   （更平滑、推荐用于采样稀疏的轮廓）。超过 <paramref name="maxDistance"/> 的距离数值是否可靠 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>只想一次性算两条轮廓的逐点距离，用 <c>JlXLDCont.DistanceContoursXld</c> 或直接 <c>DistanceCcMin</c> 拿标量最小值，不必建本对象；
	///   要的是<b>区域</b>（像素域）距离变换而不是轮廓域时，本算子族不适用。</para>
	///   <para><b>参数取向</b>本重载把 <paramref name="maxDistance"/> 按 JlTuple 传：调用期间被 <c>Store</c> 钉固、结束后 <c>UnpinTuple</c> 解除（见 <see cref="JlXLDDistTrans(JlXLDCont, string, double)"/> 的免钉固直写版）；
	///   元组形式允许携带多个值，是否按参考轮廓逐条一一对应生效 [待实测]。输出为 <c>InitOCT</c> 装载的新句柄，无返回值、由构造器给出。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 10.0, 50.0 }, new double[] { 10.0, 50.0, 50.0 });
	///   JlTuple maxDist = new double[] { 30.0 };
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_segment", maxDist);
	///   </code>
	///   <para><b>资源与坑</b>构造体内 <c>GC.KeepAlive(contour)</c> 保证原生调用返回前参考轮廓句柄存活；构造返回后 C# 侧 contour 即可 Dispose——
	///   变换对象在原生侧自持一份拷贝（<see cref="GetDistanceTransformXldContour()"/> 能取回）。本对象用完须 Dispose。</para>
	/// </remarks>
	public JlXLDDistTrans(JlXLDCont contour, string mode, JlTuple maxDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(1299);
		JlNativeApi.Store(proc, 1, contour);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.Store(proc, 1, maxDistance);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxDistance);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
	}

	/// <summary>
	///   Create the XLD distance transform.
	/// </summary>
	/// <param name="contour">Reference contour(s).</param>
	/// <param name="mode">Compute the distance to points ('point_to_point') or entire segments ('point_to_segment'). Default: "point_to_point"</param>
	/// <param name="maxDistance">Maximum distance of interest. Default: 20.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="JlXLDDistTrans(JlXLDCont, string, JlTuple)"/> 是同一个原生算子 <c>gen_xld_disttrans</c>（id 1299）的标量便捷重载：
	///   以 contour 为参考轮廓建立可复用的距离变换上下文，语义、坐标与 mode 约定见该重载说明。</para>
	///   <para><b>约束或前提</b>本重载以 <c>StoreD</c> 把 <paramref name="maxDistance"/> 直接写成单个 double 控制参数，无钉固/解钉开销，也只表达一个上限值；
	///   需要逐轮廓给不同上限时改用 JlTuple 重载。距离单位与轮廓坐标一致（像素）。</para>
	///   <para><b>与相邻成员的取舍</b>高频重建场合（每帧换一次参考轮廓）可考虑 <see cref="CreateDistanceTransformXld(JlXLDCont, string, double)"/> 复用同一实例，少一次对象分配，效果等价（同为 id 1299、先 Dispose 再重建）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 10.0, 50.0 }, new double[] { 10.0, 50.0, 50.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   using JlXLDCont held = dt.GetDistanceTransformXldContour();
	///   </code>
	///   <para><b>资源与坑</b><c>GC.KeepAlive(contour)</c> 表明参考轮廓句柄须存活到原生调用结束，构造返回后即可释放 C# 侧 contour；
	///   <c>GetDistanceTransformXldContour</c> 返回的是新句柄拷贝（示例已 <c>using</c>），可据此验证原生侧确有副本。</para>
	/// </remarks>
	public JlXLDDistTrans(JlXLDCont contour, string mode, double maxDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(1299);
		JlNativeApi.Store(proc, 1, contour);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, maxDistance);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeDistanceTransformXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDDistTrans(SerializationInfo info, StreamingContext context)
	{
		DeserializeDistanceTransformXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>Serialize object to binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b>把整个距离变换（参考轮廓 + mode + max_distance）按库内二进制格式写入流：实现上先调 <see cref="SerializeDistanceTransformXld()"/>
	///   拿原生序列化字节，再经 <c>JlSerializationBuffer.WriteToStream</c> 落流。<c>new</c> 修饰隐藏基类的通用实现，换成本类型专属的序列化算子。</para>
	///   <para><b>约束或前提</b>本对象须持有有效句柄（未 Dispose、未 Clear），流必须可写。</para>
	///   <para><b>与相邻成员的取舍</b>存成磁盘文件用 <see cref="WriteDistanceTransformXld(string)"/>；把对象塞进自己的缓存/网络通道用本方法 + 静态
	///   <see cref="Deserialize(Stream)"/> 配对还原。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   using (Stream fs = File.Create(@"C:\vision\dt.bin"))
	///   {
	///       dt.Serialize(fs);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>序列化只读不改：本对象句柄仍在，仍由外层 <c>using</c> 负责释放；流写入失败抛原生层异常，半截流不可再用。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeDistanceTransformXld(), stream);
	}

	/// <summary>Deserialize object from binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b>静态工厂：从库内二进制流还原一个距离变换，内部先建空壳实例，再用 <see cref="DeserializeDistanceTransformXld(byte[])"/>
	///   把流中字节装载进<b>新句柄</b>并返回。</para>
	///   <para><b>约束或前提</b>流的当前位置必须对准一个完整序列化块头部；字节必须由本类型的 <see cref="Serialize(Stream)"/> 产生——其他 Jl 对象族的流格式不通用，
	///   混用会在原生反序列化层报错。</para>
	///   <para><b>参数取向</b>返回新实例，不改动任何现有对象；想把流内容覆盖进现有实例请走实例方法 <see cref="DeserializeDistanceTransformXld(byte[])"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (Stream fs = File.OpenRead(@"C:\vision\dt.bin"))
	///   using (JlXLDDistTrans dt = JlXLDDistTrans.Deserialize(fs))
	///   {
	///       JlTuple maxDist = dt.GetDistanceTransformXldParam("max_distance");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回对象是原生句柄、须释放（示例已 <c>using</c>）；读取失败返回前不会泄漏半初始化对象，但调用方拿不到可用实例，需自行兜底。</para>
	/// </remarks>
	public new static JlXLDDistTrans Deserialize(Stream stream)
	{
		JlXLDDistTrans hXLDDistTrans = new JlXLDDistTrans();
		hXLDDistTrans.DeserializeDistanceTransformXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLDDistTrans;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>深拷贝本距离变换：返回内容相同、句柄独立的副本。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>用序列化→反序列化往返实现（等价于 <see cref="SerializeDistanceTransformXld()"/> + 对新实例调
	///   <see cref="DeserializeDistanceTransformXld(byte[])"/>）：副本与原件各持一个原生句柄，改动一方（如 Set 参数、Create 重建）不影响另一方。</para>
	///   <para><b>约束或前提</b>原件须持有有效句柄。</para>
	///   <para><b>与相邻成员的取舍</b>只想让另一个变量指向同一份变换时直接引用赋值即可（共享句柄、零拷贝），不要用 Clone；
	///   需要"改参数但保留原件"时才用本方法。</para>
	///   <para><b>参数取向</b>返回新实例，本对象不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   using JlXLDDistTrans copy = dt.Clone();
	///   copy.SetDistanceTransformXldParam("max_distance", 50.0);
	///   </code>
	///   <para><b>资源与坑</b>副本须自行 Dispose；序列化往返成本随参考轮廓点数增长，不要在逐点/逐轮廓循环里 Clone。</para>
	/// </remarks>
	public new JlXLDDistTrans Clone()
	{
		byte[] data = SerializeDistanceTransformXld();
		JlXLDDistTrans obj = new JlXLDDistTrans();
		obj.DeserializeDistanceTransformXld(data);
		return obj;
	}

	/// <summary>
	///   Clear a XLD distance transform.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>clear_xld_disttrans</c>（算子 id 1290）：显式释放本对象持有的变换句柄（<c>Store</c> 送进本句柄后调用，无输出），调用后对象变空壳。</para>
	///   <para><b>约束或前提</b>对已经是空句柄的对象再调一次，行为 [待实测]；清空后调用 <see cref="ApplyDistanceTransformXld(JlXLDCont)"/> 等任何成员都会在原生层失败抛
	///   <c>JlOperatorException</c>，想复用请走 <see cref="CreateDistanceTransformXld(JlXLDCont, string, double)"/> 重建。</para>
	///   <para><b>与相邻成员的取舍</b>它是 <c>Dispose()</c> 的"算子视角"版本：两者都释放原生句柄，但本方法多走一次完整的 HDevelop 调用；
	///   普通释放直接 <c>using</c> 或 Dispose 即可，只有需要在原生侧留算子轨迹时才用它。</para>
	///   <para><b>参数取向</b>无参数无返回值，原地生效。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   dt.ClearDistanceTransformXld();
	///   </code>
	///   <para><b>资源与坑</b>Clear 之后对象离开作用域时基类还会再 Dispose 一次；重复释放是否安全 [待实测]，稳妥做法是二选一而不是叠加。</para>
	/// </remarks>
	public void ClearDistanceTransformXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1290);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Determine the pointwise distance of two contours using an XLD distance transform.
	/// </summary>
	/// <param name="contour">Contour(s) for whose points the distances are calculated.</param>
	/// <returns>Copy of Contour containing the distances as an attribute.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>apply_xld_disttrans</c>（算子 id 1291）：把 <paramref name="contour"/> 的每个点对本对象内存的参考轮廓逐一量最近距离（像素）；
	///   距离模式与上限取本对象当前的 mode / max_distance（构建后可用 <see cref="SetDistanceTransformXldParam(string, string)"/> 改）。</para>
	///   <para><b>约束或前提</b>本对象须持有效句柄。距离是挂在轮廓上的<b>逐点属性</b>，不改动点坐标——数值要用
	///   <c>JlXLDCont.GetContourAttribXld</c> 读点属性（属性名 "distance" [待实测]），用 <c>GetContourXld</c> 只能拿到坐标。</para>
	///   <para><b>与相邻算子的取舍</b>只要两条轮廓间一个总体标量距离用 <c>JlXLDCont.DistanceCcMin</c>；不想预建对象、一次性算两轮廓逐点距离用
	///   <c>JlXLDCont.DistanceContoursXld</c>；本算子族的卖点是"参考形状固定、被测轮廓来一批算一批"——构建费一次，Apply 多次，单帧多轮廓时比每次现算省。</para>
	///   <para><b>参数取向</b>返回值是带距离属性的<b>新轮廓副本</b>（原生侧输出与新句柄经 <c>InitOCT</c>/<c>LoadNew</c> 装载）；入参 contour 对象本身不被改动，
	///   返回轮廓的点数、点序与入参一一对应。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 50.0, 50.0, 150.0 }, new double[] { 50.0, 150.0, 150.0 });
	///   JlXLDCont measured = new JlXLDCont(new double[] { 40.0, 60.0 }, new double[] { 55.0, 140.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_segment", 100.0);
	///   using JlXLDCont withDist = dt.ApplyDistanceTransformXld(measured);
	///   JlTuple dist = withDist.GetContourAttribXld("distance");
	///   double firstPointDist = dist[0].D;
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄、必须释放；<c>GC.KeepAlive</c> 保住 this 与入参到原生调用结束，入参轮廓调用后即可 Dispose。
	///   落在参考轮廓上的点距离为 0；超出 max_distance 的点数值是否可靠 [待实测]。</para>
	/// </remarks>
	public JlXLDCont ApplyDistanceTransformXld(JlXLDCont contour)
	{
		IntPtr proc = JlNativeApi.PreCall(1291);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, contour);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
		return obj;
	}

	/// <summary>
	///   Read an XLD distance transform from a file.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>read_xld_disttrans</c>（算子 id 1292，与 <see cref="JlXLDDistTrans(string)"/> 构造器同一算子）：
	///   读取本库写出的变换文件并<b>原地重建</b>本实例的句柄——方法体第一步就 <c>Dispose()</c> 旧句柄，随后 <c>InitOCT</c>+<c>Load</c> 装入新句柄。</para>
	///   <para><b>约束或前提</b>文件必须存在且为 <see cref="WriteDistanceTransformXld(string)"/> 的产物；文件缺失或损坏抛 <c>JlOperatorException</c>。</para>
	///   <para><b>与相邻成员的取舍</b>想保住当前变量引用不换、只换内容时用它；想留旧对象另得一个新对象时用构造器或 <see cref="Deserialize(Stream)"/>。</para>
	///   <para><b>参数取向</b>void，原地改写，无输出。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDDistTrans dt = new JlXLDDistTrans();
	///   dt.ReadDistanceTransformXld(@"C:\vision\ref_disttrans.dat");
	///   </code>
	///   <para><b>资源与坑</b>真坑：Dispose 在原生调用<b>之前</b>执行，读取一旦失败，旧内容也回不来了（对象已成空壳）；
	///   需要"要么换成新文件、要么保持原样"的语义时，先造临时新对象读文件、成功后再交换引用。</para>
	/// </remarks>
	public void ReadDistanceTransformXld(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1292);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Deserialize an XLD distance transform.
	/// </summary>
	/// <param name="serializedItemHandle">Handle of the serialized XLD distance transform.</param>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>deserialize_xld_disttrans</c>（算子 id 1293）：从 <see cref="SerializeDistanceTransformXld()"/> 产出的字节数组
	///   <b>原地恢复</b>本对象的变换句柄（先 <c>Dispose()</c> 旧句柄，再把 <c>JlSerializationBuffer</c> 包装的字节送进原生、<c>InitOCT</c> 装新句柄）。
	///   序列化的 <c>ISerializable</c> 构造器与静态 <see cref="Deserialize(Stream)"/> 最终都汇到这个方法。</para>
	///   <para><b>约束或前提</b>字节必须由本类型的序列化算子产生且完整；截断、篡改或其他对象族的字节在原生层失败抛 <c>JlOperatorException</c>。</para>
	///   <para><b>参数取向</b>void，本对象是修改目标；形参虽是 byte[]，原生侧概念上是"序列化句柄"，这里已用缓冲区对象桥接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans src = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   byte[] data = src.SerializeDistanceTransformXld();
	///   JlXLDDistTrans dst = new JlXLDDistTrans();
	///   dst.DeserializeDistanceTransformXld(data);
	///   </code>
	///   <para><b>资源与坑</b>与 Read 同坑：反序列化前旧内容已被 Dispose，失败即空壳；缓冲区是 using 短命对象，
	///   体内 <c>GC.KeepAlive(buffer)</c> 保证它活到原生调用返回。dst 用完自行 Dispose。</para>
	/// </remarks>
	public void DeserializeDistanceTransformXld(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1293);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   Serialize an XLD distance transform.
	/// </summary>
	/// <returns>Handle of the serialized XLD distance transform.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>serialize_xld_disttrans</c>（算子 id 1294）：把参考轮廓连同 mode、max_distance 全部参数打成一个自描述
	///   <c>byte[]</c>，供跨进程传输、内存缓存或喂给 <see cref="DeserializeDistanceTransformXld(byte[])"/>。</para>
	///   <para><b>约束或前提</b>本对象须持有效句柄；返回的字节不依赖任何外部文件，单机内拷贝即可还原。</para>
	///   <para><b>参数取向</b>返回纯托管 byte[]（经 <c>JlSerializationBuffer.LoadBytes</c> 拷出），不是句柄、无需释放；本对象不变。</para>
	///   <para><b>与相邻成员的取舍</b>直接落文件用 <see cref="Serialize(Stream)"/> 或 <see cref="WriteDistanceTransformXld(string)"/>；
	///   要自己控制字节去向（数据库 BLOB、网络帧）才用本方法。类型内部也靠它实现 <c>ISerializable</c> 与 <see cref="Clone()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   byte[] data = dt.SerializeDistanceTransformXld();
	///   File.WriteAllBytes(@"C:\vision\dt.snapshot", data);
	///   </code>
	///   <para><b>资源与坑</b>字节大小随参考轮廓点数线性增长，别把上百 MB 的快照塞进消息队列 [待实测具体量级]。</para>
	/// </remarks>
	public byte[] SerializeDistanceTransformXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1294);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   Write an XLD distance transform into a file.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>write_xld_disttrans</c>（算子 id 1295）：把本变换（参考轮廓 + 全部参数）写成 Vision 格式文件，
	///   之后由 <see cref="ReadDistanceTransformXld(string)"/> 或 <see cref="JlXLDDistTrans(string)"/> 构造器还原——读写严格成对。</para>
	///   <para><b>约束或前提</b>本对象须持有效句柄；目标目录须存在且可写；路径非法或磁盘满在原生层抛 <c>JlOperatorException</c>。
	///   对已存在文件是覆盖还是报错 [待实测]。</para>
	///   <para><b>参数取向</b>void、无输出；本对象只被读取（<c>Store</c> 进 0 号参数位），调用后句柄照旧。</para>
	///   <para><b>与相邻成员的取舍</b>把字节流交给自己的存储层用 <see cref="SerializeDistanceTransformXld()"/>/<see cref="Serialize(Stream)"/>；
	///   把"参考形状模板"作为资产落盘复用才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   dt.WriteDistanceTransformXld(@"C:\vision\ref_disttrans.dat");
	///   </code>
	///   <para><b>资源与坑</b>文件名后缀只是约定（习惯 .dat），格式识别靠内容不靠扩展名；写出的文件含全部参考轮廓点，属"资产文件"，别当临时缓存随手删。</para>
	/// </remarks>
	public void WriteDistanceTransformXld(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1295);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set new parameters for an XLD distance transform.
	/// </summary>
	/// <param name="genParamName">Names of the generic parameters. Default: "mode"</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: "point_to_point"</param>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>set_xld_disttrans_param</c>（算子 id 1296）：不重建变换就改掉构建参数。支持的参数名见通用参数表：
	///   <c>mode</c>（point_to_point / point_to_segment）与 <c>max_distance</c>（像素值）；传其他名字在原生层报错。</para>
	///   <para><b>约束或前提</b>改动只作用于<b>之后的</b> <see cref="ApplyDistanceTransformXld(JlXLDCont)"/> 调用，不回改历史结果；
	///   值类型要与参数语义匹配（max_distance 给数值、mode 给字符串），不匹配时原生层失败。</para>
	///   <para><b>参数取向</b>本元组重载可一次批量设多项：name 数组与 value 数组按下标一一对应；两组都用 <c>Store</c> 钉固、调用后 <c>UnpinTuple</c>
	///   （单项场景用免钉固的 <see cref="SetDistanceTransformXldParam(string, string)"/> 更轻）。void、原地生效、无输出。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   dt.SetDistanceTransformXldParam(new string[] { "mode" }, new string[] { "point_to_segment" });
	///   </code>
	///   <para><b>资源与坑</b>钉固意味着调用期间原生侧直接引用该元组内存——传入的 JlTuple 在方法返回前别并发改写内容。</para>
	/// </remarks>
	public void SetDistanceTransformXldParam(JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(1296);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set new parameters for an XLD distance transform.
	/// </summary>
	/// <param name="genParamName">Names of the generic parameters. Default: "mode"</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: "point_to_point"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="SetDistanceTransformXldParam(JlTuple, JlTuple)"/> 同一原生算子 <c>set_xld_disttrans_param</c>（id 1296）的单项字符串版：
	///   名字与值都以 <c>StoreS</c> 直写控制参数，无钉固/解钉开销；支持的参数名与取值域见元组重载说明。</para>
	///   <para><b>约束或前提</b>一次只能改一项；改 mode 立即影响后续 <see cref="ApplyDistanceTransformXld(JlXLDCont)"/> 的距离定义，
	///   已产出的结果轮廓不受影响。数值参数（max_distance）以字符串形式送出，由原生层转换 [待实测：数值型是否也要走元组重载才稳妥]。</para>
	///   <para><b>参数取向</b>void、原地生效、无输出。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   dt.SetDistanceTransformXldParam("mode", "point_to_segment");
	///   using JlXLDCont measured = new JlXLDCont(new double[] { 40.0, 60.0 }, new double[] { 55.0, 140.0 });
	///   using JlXLDCont withDist = dt.ApplyDistanceTransformXld(measured);
	///   </code>
	///   <para><b>资源与坑</b>想同时改 mode 与 max_distance 时别连调两次本方法（两次完整原生调用），用元组重载一次批量完成。</para>
	/// </remarks>
	public void SetDistanceTransformXldParam(string genParamName, string genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(1296);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.StoreS(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Get the parameters used to build an XLD distance transform.
	/// </summary>
	/// <param name="genParamName">Names of the generic parameters. Default: "mode"</param>
	/// <returns>Values of the generic parameters.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对应原生 <c>get_xld_disttrans_param</c>（算子 id 1297）：读回构建时（或后来 Set 过）的通用参数值，
	///   常查的名字是 <c>mode</c> 与 <c>max_distance</c>。本元组重载支持一次查多项。</para>
	///   <para><b>约束或前提</b>本对象须持有效句柄；查询不存在的参数名在原生层报错。返回元组的元素类型随参数而变：
	///   mode 是字符串（用 <c>.S</c> 读）、max_distance 是数值（用 <c>.D</c> 读）。</para>
	///   <para><b>参数取向</b>入参名字元组被 <c>Store</c> 钉固、调用后 <c>UnpinTuple</c>；返回经 <c>JlTuple.LoadNew</c> 的<b>新元组</b>。
	///   多个名字时返回值在结果元组里如何排列 [待实测]，单项请优先用 <see cref="GetDistanceTransformXldParam(string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont refContour = new JlXLDCont(new double[] { 10.0, 90.0 }, new double[] { 10.0, 90.0 });
	///   using JlXLDDistTrans dt = new JlXLDDistTrans(refContour, "point_to_point", 20.0);
	///   JlTuple values = dt.GetDistanceTransformXldParam(new string[] { "mode" });
	///   string mode = values[0].S;
	///   </code>
	///   <para><b>资源与坑</b>返回的是纯数值/字符串元组，不必 Dispose；若查询结果含句柄型元素才需要处理。</para>
	/// </remarks>
	public JlTuple GetDistanceTransformXldParam(JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(1297);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get the parameters used to build an XLD distance transform.
	/// </summary>
	/// <param name="genParamName">Names of the generic parameters. Default: "mode"</param>
	/// <returns>Values of the generic parameters.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取 参数 使用的 build XLD 距离变换。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLDDistTrans obj = ...;
	///   var result = obj.GetDistanceTransformXldParam("mode");
	///   </code>
	/// </remarks>
	public JlTuple GetDistanceTransformXldParam(string genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(1297);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get the reference contour used to build the XLD distance transform.
	/// </summary>
	/// <returns>Reference contour.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取 reference 轮廓 使用的 build XLD 距离变换。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLDDistTrans obj = ...;
	///   var result = obj.GetDistanceTransformXldContour();
	///   </code>
	/// </remarks>
	public JlXLDCont GetDistanceTransformXldContour()
	{
		IntPtr proc = JlNativeApi.PreCall(1298);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create the XLD distance transform.
	/// </summary>
	/// <param name="contour">Reference contour(s).</param>
	/// <param name="mode">Compute the distance to points ('point_to_point') or entire segments ('point_to_segment'). Default: "point_to_point"</param>
	/// <param name="maxDistance">Maximum distance of interest. Default: 20.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 XLD 距离变换。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLDCont contour = ...;
	///   JlXLDDistTrans obj = ...;
	///   obj.CreateDistanceTransformXld(contour, "point_to_point", 20.0);
	///   </code>
	/// </remarks>
	public void CreateDistanceTransformXld(JlXLDCont contour, string mode, JlTuple maxDistance)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1299);
		JlNativeApi.Store(proc, 1, contour);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.Store(proc, 1, maxDistance);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxDistance);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
	}

	/// <summary>
	///   Create the XLD distance transform.
	/// </summary>
	/// <param name="contour">Reference contour(s).</param>
	/// <param name="mode">Compute the distance to points ('point_to_point') or entire segments ('point_to_segment'). Default: "point_to_point"</param>
	/// <param name="maxDistance">Maximum distance of interest. Default: 20.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 XLD 距离变换。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLDCont contour = ...;
	///   JlXLDDistTrans obj = ...;
	///   obj.CreateDistanceTransformXld(contour, "point_to_point", 20.0);
	///   </code>
	/// </remarks>
	public void CreateDistanceTransformXld(JlXLDCont contour, string mode, double maxDistance)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1299);
		JlNativeApi.Store(proc, 1, contour);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, maxDistance);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
	}
}
