using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an XLD extended parallel object(-array).</summary>
[Serializable]
public class JlXLDExtPara : JlXLD, ISerializable, ICloneable
{
	/// <summary>按 1 基索引从本扩展平行线对元组中取出子容器（等价于 <see cref="SelectObj(JlTuple)"/>）。</summary>
	/// <param name="index">要取出的元素序号，1 基；可给多值元组一次取多个。</param>
	/// <returns>仅含被选中元素的新 JlXLDExtPara 容器，需自行 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>索引器直接转调 <c>SelectObj(JlTuple)</c>，返回一个新容器而不是原地改写 <c>this</c>。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号从 1 起算（HALCON 对象元组约定），传 0 并非第一个元素；越界与负序号（HALCON 里负数从尾部倒数）的托管侧行为未校验 [待实测]。形参是 JlTuple，故 <c>ext[1]</c> 走 int→JlTuple 隐式转换。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>按位置取元素用本索引器或 <c>SelectObj</c>；按几何形状特征筛选用 <c>SelectShapeXld</c>；按某点是否落在轮廓上筛选用 <c>SelectXldPoint</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara first = ext[1];
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄，用完要释放；源容器 <c>ext</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara this[JlTuple index] => SelectObj(index);

	/// <summary>创建一个未初始化（句柄为 UNDEF）的扩展平行线对容器。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>仅分配托管壳对象，原生句柄被置为未定义，并断言其对象类为 <c>xld_ext_para</c>。它不是图像对象，也不携带任何平行线对数据。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>未初始化容器不能直接参与几何/统计算子；真正的扩展平行线对由 <c>JlXLDPara.ModParallelsXld</c> 的 out 参数产出，或由 <see cref="Deserialize(Stream)"/> 反序列化得到。本类型没有 <c>JlXLDExtPara(bool)</c> 这类构造重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlXLDExtPara empty = new JlXLDExtPara();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>派生自 JlXLD→JlObject→JlObjectBase(IDisposable)，用毕需 Dispose()。</para>
	/// </remarks>
	public JlXLDExtPara()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDExtPara(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDExtPara(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>由同族 <see cref="JlObject"/> 对象复制构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDExtPara(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "xld_ext_para");
	}

	/// <summary>内部工厂：从算子的输出对象槽装载本类新实例；<paramref name="err"/> 为调用错误码并原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDExtPara obj)
	{
		obj = new JlXLDExtPara(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDExtPara(SerializationInfo info, StreamingContext context)
	{
		DeserializeXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把本扩展平行线对容器以 Vision 二进制格式写入流。</summary>
	/// <param name="stream">目标可写流；调用后其写位置随序列化字节前进。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>内部先调 <c>SerializeXld()</c> 得到字节缓冲，再交给 <c>JlSerializationBuffer.WriteToStream</c> 落流；不产生新句柄，也不改动 <c>this</c>。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>stream</c> 必须可写；反序列化端须用配套的 <see cref="Deserialize(Stream)"/>，格式非通用图像文件，只能在本库内往返。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要在内存里拿字节数组（如自管传输/缓存）用 <c>SerializeXld()</c>；要成对做持久化/跨进程用 <c>Serialize</c>/<c>Deserialize</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using var fs = File.Create("ext.bin");
	///   ext.Serialize(fs);
	///   </code>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeXld(), stream);
	}

	/// <summary>从 Vision 二进制流读出一个新的扩展平行线对容器。</summary>
	/// <param name="stream">由 <see cref="Serialize(Stream)"/> 写出的可读流。</param>
	/// <returns>新分配的 JlXLDExtPara 句柄，调用方负责 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>先 <c>new JlXLDExtPara()</c> 建空壳，再用 <c>JlSerializationBuffer.ReadFromStream</c> 取字节、<c>DeserializeXld</c> 填充，返回的是新句柄而非复用某个已有对象。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>流内容必须是本库 <c>Serialize</c> 写出的 xld_ext_para 二进制；格式或对象类不符时由 <c>AssertObjectClass</c>/原生层报错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using var fs = File.OpenRead("ext.bin");
	///   using JlXLDExtPara ext = JlXLDExtPara.Deserialize(fs);
	///   </code>
	/// </remarks>
	public new static JlXLDExtPara Deserialize(Stream stream)
	{
		JlXLDExtPara hXLDExtPara = new JlXLDExtPara();
		hXLDExtPara.DeserializeXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLDExtPara;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>先 <c>SerializeXld()</c> 把本容器打成字节，再 <c>new JlXLDExtPara()</c> + <c>DeserializeXld</c> 还原，得到一个数据独立的新句柄；<c>this</c> 不变。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要一份可独立改动的副本用 <c>Clone()</c>；只是把同一批线对拼进别的容器用 <c>ConcatObj</c>；要截取子集用 <c>CopyObj</c>/<c>SelectObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara copy = ext.Clone();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>走了一趟原生序列化，比逐元素复制重；副本是独立句柄须单独 Dispose()。显式 <c>ICloneable.Clone()</c> 返回 <c>object</c>，本方法返回强类型 <c>JlXLDExtPara</c>。</para>
	/// </remarks>
	public new JlXLDExtPara Clone()
	{
		byte[] data = SerializeXld();
		JlXLDExtPara obj = new JlXLDExtPara();
		obj.DeserializeXld(data);
		return obj;
	}

	/// <summary>
	///   把落在同一多边形上的平行线对拼成"最长延伸"的平行线，输出为普通折线轮廓容器。
	/// </summary>
	/// <returns>拼接后的最大延伸平行线，新句柄 <c>JlXLDPoly</c>（类型已从平行线对退化为一般折线多边形），需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 38：<c>this</c> 进 iconic 槽 1，输出用 <c>InitOCT</c> 声明在槽 1 并以 <c>JlXLDPoly.LoadNew</c> 取回。它把同属一条多边形、共线的相邻平行线对首尾接成尽量长的线。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>输入须是 <c>ModParallelsXld</c> 产出的 <c>xld_ext_para</c>；能接成多长取决于线对是否"落在同一多边形上"，输入若是被灰度筛断的碎段则接合不连续。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>本方法只做"延长接合"并丢掉平行线对的成对语义（返回 <c>JlXLDPoly</c>，不再是 <c>JlXLDExtPara</c>）；要把边线多边形、扩展平行线与中心线一起综合成道路两侧线，用 <c>JlXLDModPara.CombineRoadsXld</c> 或 <c>JlXLDPoly.CombineRoadsXld</c>；只要几何明细用继承的 <c>GetParallelsXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDPoly roadsides = ext.MaxParallelsXld();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄；<c>GC.KeepAlive(this)</c> 保证原生调用结束前输入不被回收。</para>
	/// </remarks>
	public JlXLDPoly MaxParallelsXld()
	{
		IntPtr proc = JlNativeApi.PreCall(38);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDPoly.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   从本扩展平行线对容器里去掉那些同时出现在 objectsSub 中的元素，得到差集新容器。
	/// </summary>
	/// <param name="objectsSub">被减去的对象元组（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <returns>属于本容器但不属于 <c>objectsSub</c> 的元素，新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 558：<c>this</c> 进 iconic 槽 1、<c>objectsSub</c> 进槽 2，输出 <c>InitOCT</c> 槽 1 后 <c>LoadNew</c> 取回，是集合意义上的差（Objects \ ObjectsSub）。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>两侧都是同一对象类的平行线对容器；判同与否取决于原生对元素相等的定义（见 <c>CompareObj</c>/<c>TestEqualObj</c>），不是按引用。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>求差用本方法；把两个容器首尾相连用 <c>ConcatObj</c>；替换某些位置的元素用 <c>ReplaceObj</c>；按序号删元素用 <c>RemoveObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara subset = ext[1];
	///   using JlXLDExtPara rest = ext.ObjDiff(subset);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄，两个操作数都不被改动，各自须自行释放。</para>
	/// </remarks>
	public JlXLDExtPara ObjDiff(JlXLDExtPara objectsSub)
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
	///   从本扩展平行线对元组里按序号复制一段连续元素到新容器（原容器不变）。
	/// </summary>
	/// <param name="index">起始复制序号，1 基。Default: 1</param>
	/// <param name="numObj">要复制的元素个数；给 -1 表示从 index 一直复制到末尾。Default: 1</param>
	/// <returns>被复制出的元素组成的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 568：<c>this</c> 进 iconic 槽 1，控制参数 index(0)、numObj(1) 均以 <c>StoreI</c> 按 INTEGER 写入，输出 <c>InitOCT</c> 槽 1 后 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号 1 基；<c>index + numObj - 1</c> 越出实际元素数时的截断/报错行为未在托管侧校验 [待实测]。<c>numObj</c> 传 -1 是"取到结尾"的哨兵值，不是删除一个元素。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>复制连续一段用本方法；按任意序号集合取用素用 <c>SelectObj</c>；要一份完全独立的深拷贝用 <c>Clone</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara head = ext.CopyObj(1, 1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara CopyObj(int index, int numObj)
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
	///   把 objects2 接到本容器尾部，拼成一个更大的扩展平行线对元组。
	/// </summary>
	/// <param name="objects2">接在后面的一段对象元组（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <returns>拼接结果的新容器：先本容器元素、后 objects2 元素，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 569：<c>this</c> 进 iconic 槽 1、<c>objects2</c> 进槽 2，输出 <c>InitOCT</c> 槽 1 后 <c>LoadNew</c> 取回新句柄。元素顺序稳定地为"本容器在前、objects2 在后"。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>两个操作数必须同对象类；下游若按序号筛选（<c>SelectObj</c>/<c>CopyObj</c>）会依赖这个拼接次序，次序变了会静默错位。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>首尾相连用本方法；把元素插到指定位置用 <c>InsertObj</c>；从元组里删元素用 <c>RemoveObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara more = ext.Clone();
	///   using JlXLDExtPara all = ext.ConcatObj(more);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄，两个操作数均不改动，各自须释放。</para>
	/// </remarks>
	public JlXLDExtPara ConcatObj(JlXLDExtPara objects2)
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
	///   按序号元组从本扩展平行线对元组中挑选若干元素组成新容器。
	/// </summary>
	/// <param name="index">被选元素的序号集合，1 基；可多值一次取多个，重复序号会重复取。Default: 1</param>
	/// <returns>被选中元素按给定顺序组成的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 572：<c>this</c> 进 iconic 槽 1，<c>index</c> 作为控制参数 0 用 <c>Store</c> 钉固定元组写入，调用后 <c>UnpinTuple</c>，输出 <c>InitOCT</c> 槽 1 后 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号 1 基，负数是否表示从尾部倒数 [待实测]。与 <c>SelectObj(int)</c> 同 id 但走元组钉固定路径；结果顺序严格跟随 <c>index</c> 的给出顺序。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>按任意序号集合取用素用本重载；只取连续一段用 <c>CopyObj</c>；按几何特征筛用 <c>SelectShapeXld</c>。要命中本重载须传 <c>JlTuple</c>，写 <c>SelectObj(1)</c> 这类整型字面量会走 <c>int</c> 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple idx = new int[] { 1, 2 };
	///   using JlXLDExtPara picked = ext.SelectObj(idx);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara SelectObj(JlTuple index)
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
	///   按单个序号从本扩展平行线对元组中取出一个元素组成新容器（标量重载）。
	/// </summary>
	/// <param name="index">被选元素的序号，1 基。Default: 1</param>
	/// <returns>仅含该元素的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <c>SelectObj(JlTuple)</c> 同为原生 id 572：<c>this</c> 进 iconic 槽 1，<c>index</c> 用 <c>StoreI</c> 按 INTEGER 直写控制参数 0，免去钉固定/解钉开销，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号 1 基，一次只取一个元素；越界行为未校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>取一个元素用本重载；取多个任意序号用 <c>SelectObj(JlTuple)</c>；取连续一段用 <c>CopyObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara one = ext.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara SelectObj(int index)
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
	///   在给定容差下逐元素比较本容器与 objects2 是否相等，返回 1/0 整数判定。
	/// </summary>
	/// <param name="objects2">被比较对象元组（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <param name="epsilon">坐标允许的差值上限，像素单位；本重载以元组钉固定写入。Default: 0.0</param>
	/// <returns>相等返回 1、不相等返回 0（LoadI 只读第一个整数值，多值结果被丢弃）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 573：<c>this</c> 进 iconic 槽 1、<c>objects2</c> 进槽 2，<c>epsilon</c> 作控制参数 0 用 <c>Store</c> 钉固定，调用后 <c>UnpinTuple</c>，输出以 <c>InitOCT</c>/<c>LoadI</c> 按 INTEGER 取回。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>epsilon=0.0 要求坐标逐位精确相等；平行线对含行、列等坐标分量，容差按像素距离解释。给多值 epsilon 时的逐元素配对行为未校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>需要自定义容差判等用本方法；无需容差、直接判全等用 <c>TestEqualObj</c>；求集合差用 <c>ObjDiff</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple eps = 0.5;
	///   int equal = ext.CompareObj(ext.Clone(), eps);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回 int 而非 JlObject，无句柄需释放；示例里 <c>ext.Clone()</c> 产生的临时句柄应自行 Dispose。</para>
	/// </remarks>
	public int CompareObj(JlXLDExtPara objects2, JlTuple epsilon)
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
	///   在给定单一容差下比较本容器与 objects2 是否相等，返回 1/0 整数判定（标量重载）。
	/// </summary>
	/// <param name="objects2">被比较对象元组（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <param name="epsilon">坐标允许的差值上限，像素单位；以 <c>StoreD</c> 直写、无钉固定开销。Default: 0.0</param>
	/// <returns>相等返回 1、不相等返回 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <c>CompareObj(JlTuple)</c> 同为原生 id 573：<c>this</c> 进 iconic 槽 1、<c>objects2</c> 进槽 2，<c>epsilon</c> 用 <c>StoreD</c> 按 DOUBLE 直写控制参数 0，输出 <c>LoadI</c> 取整。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>只接受单个容差；要按不同分量给不同容差须用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>单容差判等用本重载；无容差判全等用 <c>TestEqualObj</c>；求差集用 <c>ObjDiff</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   int equal = ext.CompareObj(ext, 0.0);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>写 <c>0.0</c> 字面量命中本 double 重载；返回 int 无句柄负担。</para>
	/// </remarks>
	public int CompareObj(JlXLDExtPara objects2, double epsilon)
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
	///   判等本容器与 objects2 的元素是否逐一相等（不容差），返回 1/0。
	/// </summary>
	/// <param name="objects2">比较对象元组（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <returns>全等返回 1、否则返回 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 576：<c>this</c> 进 iconic 槽 1、<c>objects2</c> 进槽 2，输出 <c>InitOCT</c>/<c>LoadI</c> 按 INTEGER 取回。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>不含任何容差，坐标需逐位相同才判等；因浮点误差本不完全一致的平行线对可能返回 0。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>确定两个容器应完全一致时用本方法；需容忍小差异时用 <c>CompareObj</c> 并传合适 epsilon。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   int same = ext.TestEqualObj(ext.Clone());
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回 int 无句柄需释放；示例里 <c>ext.Clone()</c> 产生的临时句柄应自行 Dispose。</para>
	/// </remarks>
	public int TestEqualObj(JlXLDExtPara objects2)
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
	///   基于规则网格点集计算畸变图到校正图的映射，返回映射图像并导出网格轮廓（rotation 为数值弧度元组的重载）。
	/// </summary>
	/// <param name="image">输入灰度图，进 iconic 槽 1。</param>
	/// <param name="meshes">输出的网格轮廓（新 <c>JlXLDExtPara</c> 句柄），原生 iconic 输出槽 2。</param>
	/// <param name="gridSpacing">校正图中网格点间距，整数像素。Default: 由调用方给定</param>
	/// <param name="rotation">施加于点网格的旋转，数值弧度，元组钉固定写入。Default: "auto"</param>
	/// <param name="row">网格点的行坐标元组。</param>
	/// <param name="column">网格点的列坐标元组。</param>
	/// <param name="mapType">映射类型字符串，StoreS 写入。Default: "bilinear"</param>
	/// <returns>含映射数据的图像新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 1107。注意槽序与 C# 形参序不一致：<c>image</c> 占 iconic 输入 1、<c>this</c> 占 iconic 输入 2；控制参数序为 gridSpacing(0)、rotation(1)、row(2)、column(3)、mapType(4)。两个 iconic 输出经 <c>InitOCT</c> 声明：槽 1 以 <c>JlImage.LoadNew</c> 取回作返回值，槽 2 以 <c>LoadNew</c> 取回赋给 <c>meshes</c>。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>row</c>/<c>column</c> 需等长成对且与网格几何自洽；本重载 rotation 是数值弧度（对应 string 重载可传 "auto"）。多通道图是否可用未校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>本重载传数值 rotation 精确控制网格转角；想让程序自动选旋转用 <c>GenGridRectificationMap(..., string rotation, ...)</c> 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple row = new double[] { 10.0, 200.0 };
	///   JlTuple col = new double[] { 10.0, 600.0 };
	///   JlTuple rot = 0.0;
	///   using JlImage mapImg = ext.GenGridRectificationMap(img, out JlXLDExtPara meshes, 64, rot, row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值与 <c>meshes</c> 都是新句柄，须各自 Dispose；<c>GC.KeepAlive(this)</c>、<c>GC.KeepAlive(image)</c> 保证原生调用期间输入不被回收。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDExtPara meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
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
	///   基于规则网格点集计算畸变图到校正图的映射，返回映射图像并导出网格轮廓（rotation 为字符串的重载）。
	/// </summary>
	/// <param name="image">输入灰度图，进 iconic 槽 1。</param>
	/// <param name="meshes">输出的网格轮廓（新 <c>JlXLDExtPara</c> 句柄），原生 iconic 输出槽 2。</param>
	/// <param name="gridSpacing">校正图中网格点间距，整数像素。Default: 由调用方给定</param>
	/// <param name="rotation">点网格旋转选项字符串（如 "auto"），以 <c>StoreS</c> 写入。Default: "auto"</param>
	/// <param name="row">网格点的行坐标元组。</param>
	/// <param name="column">网格点的列坐标元组。</param>
	/// <param name="mapType">映射类型字符串，StoreS 写入。Default: "bilinear"</param>
	/// <returns>含映射数据的图像新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 1107，槽序与形参序不一致：<c>image</c> 占 iconic 输入 1、<c>this</c> 占 iconic 输入 2；控制参数 gridSpacing(0 StoreI)、rotation(1 StoreS)、row(2)、column(3)、mapType(4 StoreS)。输出 <c>InitOCT</c> 槽 1 作返回值（<c>JlImage.LoadNew</c>），槽 2 赋给 <c>meshes</c>。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>本重载 rotation 走字符串（StoreS，无需 UnpinTuple）；<c>"auto"</c> 由原生侧决定网格旋转。给数值弧度请改用 JlTuple rotation 重载。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>想让程序自动选旋转用本重载；需精确指定旋转角用 <c>GenGridRectificationMap(..., JlTuple rotation, ...)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple row = new double[] { 10.0, 200.0 };
	///   JlTuple col = new double[] { 10.0, 600.0 };
	///   string rot = "auto";
	///   using JlImage mapImg = ext.GenGridRectificationMap(img, out JlXLDExtPara meshes, 64, rot, row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值与 <c>meshes</c> 都是新句柄，须各自 Dispose。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDExtPara meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
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
	///   用序列化字节原地重建本容器的内容（原生 id 1552）。
	/// </summary>
	/// <param name="serializedItemHandle">由 <c>SerializeXld()</c> 得到的序列化字节数组。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>先 <c>Dispose()</c> 释放当前句柄，再用 <c>JlSerializationBuffer</c> 承载字节存入控制参数 0，输出经 <c>InitOCT</c> 后由 <c>Load(proc, 1, err)</c> 原地写回 <c>this</c>——返回的不是新句柄，而是本实例本身被填充。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>入参必须是配对的 xld 序列化字节；调用会先销毁原有内容，失败时 <c>this</c> 可能处于未初始化状态。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>原地填充已有对象用本方法；直接从一个 <c>Stream</c> 造新对象用静态 <see cref="Deserialize(Stream)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   byte[] data = ext.SerializeXld();
	///   using JlXLDExtPara target = new JlXLDExtPara();
	///   target.DeserializeXld(data);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para><c>GC.KeepAlive(buffer)</c> 保证序列化缓冲在原生调用结束前不被回收；本方法原地改写，不额外产生需释放的新句柄。</para>
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
	///   把本容器序列化为 Vision 二进制字节数组（原生 id 1553）。
	/// </summary>
	/// <returns>序列化后的托管字节数组，非原生句柄，无需 Dispose；可用 <c>DeserializeXld</c> 还原。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>this</c> 进 iconic 槽 1，输出经 <c>InitOCT</c> 声明在槽 0 并由 <c>JlSerializationBuffer.LoadBytes</c> 直接拷成 <c>byte[]</c>。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要把字节自管（内存缓存/自定义传输）用本方法；要直接落 <c>Stream</c> 用 <c>Serialize</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   byte[] data = ext.SerializeXld();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回的是值类型化的托管字节，不占用原生句柄；<c>this</c> 不被改动。</para>
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
	///   挑出经过给定点的轮廓（元组重载，可批量给多组测试点）。
	/// </summary>
	/// <param name="row">测试点的行坐标，像素单位，可多值。Default: 100.0</param>
	/// <param name="column">测试点的列坐标，像素单位，与 row 配对。Default: 100.0</param>
	/// <returns>经过该点的平行线对子集，新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 1595：<c>this</c> 进 iconic 槽 1，<c>row</c>(0)、<c>column</c>(1) 作控制参数以 <c>Store</c> 钉固定写入、调用后 <c>UnpinTuple</c>，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>row</c> 与 <c>column</c> 需等长成对；点是图像像素坐标（row 向下、column 向右）。给单点更省事的写法用 <c>double</c> 重载。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>按"是否经过某点"筛用本方法；按长度/面积等形状特征筛用 <c>SelectShapeXld</c>；按位置序号取用素用 <c>SelectObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple r = 100.0;
	///   JlTuple c = 100.0;
	///   using JlXLDExtPara hit = ext.SelectXldPoint(r, c);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara SelectXldPoint(JlTuple row, JlTuple column)
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
	///   挑出经过单个给定点的轮廓（标量重载）。
	/// </summary>
	/// <param name="row">测试点行坐标，像素单位。Default: 100.0</param>
	/// <param name="column">测试点列坐标，像素单位。Default: 100.0</param>
	/// <returns>经过该点的平行线对子集，新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <c>SelectXldPoint(JlTuple,JlTuple)</c> 同 id 1595：<c>this</c> 进 iconic 槽 1，<c>row</c>(0)、<c>column</c>(1) 用 <c>StoreD</c> 按 DOUBLE 直写，无钉固定开销，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只测一个点用本重载；批量给多组测试点用元组重载；按形状特征筛用 <c>SelectShapeXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara hit = ext.SelectXldPoint(100.0, 300.0);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>写 <c>100.0</c> 这类双精度字面量命中本重载；返回新句柄须释放。</para>
	/// </remarks>
	public new JlXLDExtPara SelectXldPoint(double row, double column)
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
	///   按形状特征筛选平行线对（元组重载：可多特征、'and'/'or' 组合、'min'/'max' 边界）。
	/// </summary>
	/// <param name="features">要检查的形状特征名，可多值。Default: "area"</param>
	/// <param name="operation">多特征之间的组合方式 "and"/"or"。Default: "and"</param>
	/// <param name="min">各特征下限，或字面量 "min" 表示不设下限。Default: 150.0</param>
	/// <param name="max">各特征上限，或字面量 "max" 表示不设上限。Default: 99999.0</param>
	/// <returns>满足条件的平行线对子集，新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 1597：<c>this</c> 进 iconic 槽 1，features(0)、min(2)、max(3) 用 <c>Store</c> 钉固定、operation(1) 用 <c>StoreS</c>，调用后对各元组 <c>UnpinTuple</c>，输出 <c>LoadNew</c> 取回新句柄。元组重载可一次给多个特征并配多组上下限。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>features</c>、<c>min</c>、<c>max</c> 需按特征一一对应；min/max 是闭区间还是开区间未明确 [待实测]。可用 <c>"min"</c>/<c>"max"</c> 字符串占位表示该侧不设限。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>按形状特征筛用本方法；只要单特征单区间可写更简洁的 <c>double</c> 重载；按经过某点筛用 <c>SelectXldPoint</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple feats = "length";
	///   JlTuple lo = 150.0;
	///   JlTuple hi = 99999.0;
	///   using JlXLDExtPara sel = ext.SelectShapeXld(feats, "and", lo, hi);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara SelectShapeXld(JlTuple features, string operation, JlTuple min, JlTuple max)
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
	///   按单个形状特征与数值区间筛选平行线对（标量重载）。
	/// </summary>
	/// <param name="features">要检查的单个形状特征名。Default: "area"</param>
	/// <param name="operation">多特征之间的组合方式 "and"/"or"。Default: "and"</param>
	/// <param name="min">特征下限，DOUBLE 直写。Default: 150.0</param>
	/// <param name="max">特征上限，DOUBLE 直写。Default: 99999.0</param>
	/// <returns>满足条件的平行线对子集，新句柄，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与元组重载同 id 1597：<c>this</c> 进 iconic 槽 1，<c>features</c>(0)、<c>operation</c>(1) 用 <c>StoreS</c> 直写、<c>min</c>(2)、<c>max</c>(3) 用 <c>StoreD</c> 直写，无钉固定开销，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>一次只能给单个特征与单个上下限，且数值以 DOUBLE 表示；多特征/多区间要改用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>单特征筛选用本重载；多特征组合用元组重载；按经过某点筛用 <c>SelectXldPoint</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   string feats = "length";
	///   using JlXLDExtPara sel = ext.SelectShapeXld(feats, "and", 150.0, 99999.0);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara SelectShapeXld(string features, string operation, double min, double max)
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
	///   按形状变换把每条平行线对映射到一种规范化形状（如凸包）。
	/// </summary>
	/// <param name="type">形状变换类型字符串，StoreS 写入。Default: "convex"</param>
	/// <returns>变换后的平行线对新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 1608：<c>this</c> 进 iconic 槽 1，<c>type</c> 控制参数 0 用 <c>StoreS</c>，输出 <c>InitOCT</c>/<c>LoadNew</c> 得到新句柄。本方法会改写平行线对的形状（凸包、包络矩形等），不再是原始线对。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>对 <c>xld_ext_para</c> 这类带成对语义的对象，变换可能丢失平行线对的"配对"结构而仅保留轮廓几何；<c>"convex"</c> 之外的 type 取值与本类型是否完全兼容未实测 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>需要整体形状近似用本方法；只要"延长接合"用 <c>MaxParallelsXld</c>；按特征筛选用 <c>SelectShapeXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara hulls = ext.ShapeTransXld("convex");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara ShapeTransXld(string type)
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
	///   把 objectsInsert 插入到本元组的指定序号位置，得到延长后的新元组。
	/// </summary>
	/// <param name="objectsInsert">要插入的对象元组（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <param name="index">插入位置序号，1 基（新元素从该位起挤入，其后元素后移）。</param>
	/// <returns>插入后的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 2003：<c>this</c> 进 iconic 槽 1、<c>objectsInsert</c> 进槽 2，<c>index</c> 用 <c>StoreI</c> 写控制参数 0；输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>插入位置 1 基，<c>index</c> 超出当前元素数时按 HALCON 规则处理，具体越界行为未校验 [待实测]。与 <c>ConcatObj</c> 不同，本方法把元素放到中间而非尾部。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>插到指定位置用本方法；只往末尾追加用 <c>ConcatObj</c>；就替换指定位元素用 <c>ReplaceObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara piece = ext.Clone();
	///   using JlXLDExtPara merged = ext.InsertObj(piece, 1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄，两个操作数均不改动，各自须释放。</para>
	/// </remarks>
	public JlXLDExtPara InsertObj(JlXLDExtPara objectsInsert, int index)
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
	///   按序号元组从本元组删除若干元素，返回剩余元素组成的新元组（元组重载）。
	/// </summary>
	/// <param name="index">要删除的元素序号集合，1 基；可多值一次删多个。</param>
	/// <returns>删除后剩下的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 2005：<c>this</c> 进 iconic 槽 1，<c>index</c> 作控制参数 0 用 <c>Store</c> 钉固定、调用后 <c>UnpinTuple</c>，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号 1 基；越界或重复序号的行为未校验 [待实测]。返回的剩余元素保持原相对顺序。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>按序号删元素用本方法；反过来保留指定序号用 <c>SelectObj</c>；只删单个元素可用 <c>int</c> 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   JlTuple idx = new int[] { 1 };
	///   using JlXLDExtPara kept = ext.RemoveObj(idx);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄须释放；<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara RemoveObj(JlTuple index)
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
	///   按单个序号从本元组删除一个元素，返回剩余元素的新元组（标量重载）。
	/// </summary>
	/// <param name="index">要删除的元素序号，1 基。</param>
	/// <returns>删除后剩下的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <c>RemoveObj(JlTuple)</c> 同 id 2005：<c>this</c> 进 iconic 槽 1，<c>index</c> 用 <c>StoreI</c> 按 INTEGER 直写控制参数 0，无钉固定开销，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>一次只删一个 1 基序号；删多个要改用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>删单个元素用本重载；删多个用元组重载；反向保留指定序号用 <c>SelectObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara kept = ext.RemoveObj(1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>写整型字面量命中本重载；返回新句柄须释放，<c>this</c> 不被改动。</para>
	/// </remarks>
	public new JlXLDExtPara RemoveObj(int index)
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
	///   用 objectsReplace 替换本元组中指定序号位置的元素，得到替换后的新元组（元组重载）。
	/// </summary>
	/// <param name="objectsReplace">用于替换的元素（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <param name="index">被替换元素的序号集合，1 基；可多值一次换多个位置。</param>
	/// <returns>替换后的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 2006：<c>this</c> 进 iconic 槽 1、<c>objectsReplace</c> 进槽 2，<c>index</c> 作控制参数 0 用 <c>Store</c> 钉固定、调用后 <c>UnpinTuple</c>，输出 <c>LoadNew</c> 取回新句柄。注意 C# 形参序是 (objectsReplace, index)，而原生侧 objectsReplace 占 iconic 2、index 占控制 0。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号 1 基；<c>index</c> 个数与 <c>objectsReplace</c> 元素数如何配对未校验 [待实测]。替换不改变元组长度。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>就地覆盖指定位置用本方法；在该位置挤入新元素（增长元组）用 <c>InsertObj</c>；只删不补用 <c>RemoveObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara repl = ext.Clone();
	///   JlTuple idx = new int[] { 1 };
	///   using JlXLDExtPara done = ext.ReplaceObj(repl, idx);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄，两个操作数均不改动，各自须释放。</para>
	/// </remarks>
	public JlXLDExtPara ReplaceObj(JlXLDExtPara objectsReplace, JlTuple index)
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
	///   用 objectsReplace 替换本元组中单个序号位置的元素，得到替换后的新元组（标量重载）。
	/// </summary>
	/// <param name="objectsReplace">用于替换的元素（须同为 <c>xld_ext_para</c> 类）。</param>
	/// <param name="index">被替换元素的序号，1 基。</param>
	/// <returns>替换后的新容器，需 Dispose()。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <c>ReplaceObj(JlTuple)</c> 同 id 2006：<c>this</c> 进 iconic 槽 1、<c>objectsReplace</c> 进槽 2，<c>index</c> 用 <c>StoreI</c> 按 INTEGER 直写控制参数 0，无钉固定开销，输出 <c>LoadNew</c> 取回新句柄。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>一次只覆盖一个 1 基位置；多处替换要改用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>覆盖单个位置用本重载；覆盖多个位置用元组重载；插入新元素用 <c>InsertObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDPara parallels = img.Threshold(100.0, 200.0).GenContourRegionXld("border").GenPolygonsXld("ramer", 2.0).GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   using JlXLDExtPara repl = ext.Clone();
	///   using JlXLDExtPara done = ext.ReplaceObj(repl, 1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>写整型字面量命中本重载；返回新句柄，两个操作数均不改动，各自须释放。</para>
	/// </remarks>
	public JlXLDExtPara ReplaceObj(JlXLDExtPara objectsReplace, int index)
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
