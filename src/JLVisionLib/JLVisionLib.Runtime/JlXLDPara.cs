using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>
///   XLD "平行线对" 对象数组容器：元素的原生对象类固定为 <c>xld_para</c>，由 <c>JlXLDPoly.GenParallelsXld</c> 产出，
///   每个元素承载一对互相平行的线段（row1/col1/length1/phi1 与 row2/col2/length2/phi2 八个属性），
///   只能经继承自 <see cref="JlXLD"/> 的 <c>GetParallelsXld</c> 读出，本库不提供写回这些属性的对应方法。
/// </summary>
[Serializable]
public class JlXLDPara : JlXLD, ISerializable, ICloneable
{
	/// <summary>按 1 起始的序号取出本容器里的一条或几条平行线对，逐字转发给 <see cref="SelectObj(JlTuple)"/>。</summary>
	/// <param name="index">元素序号（1 起始）。Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>托管侧不发额外原生调用，只做 <c>SelectObj(index)</c> 转发，返回的是一个新容器，取出的元素此后与原容器各走各的。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号基数是 1（与 <c>SelectObj</c>、<c>CopyObj</c> 文档里的 "Starting index … 1" 一致），<c>parallels[0]</c> 取不到第一条 [待实测：0、负数与越界序号是抛 JlOperatorException 还是返回未初始化容器]。传入的是 <c>JlTuple</c>，因此想一次拿多条要写成元组而不是数组下标区间。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要单条用本索引器；已知连续一段用 <c>CopyObj(index,numObj)</c> 少造一次元组；按平行对的长度、间距等特征筛用 <c>SelectShapeXld</c>；按"过不过某个点"筛用 <c>SelectXldPoint</c>。逐条取出再 <c>ConcatObj</c> 拼回比上述任一做法都多一次句柄搬运。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara first = parallels[1];
	///   using JlXLDPara two = parallels[new JlTuple(1, 2)];
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>每次索引都返回新容器，要各自 <c>Dispose()</c>；本类经 <c>JlXLD</c>→<c>JlObject</c>→<c>JlObjectBase</c> 实现 <c>IDisposable</c>。索引取出的元素只保证声明类型是 <c>JlXLDPara</c>，<c>LoadNew</c> 走的是未初始化句柄，不做对象类断言，原生真给了别的类也不会在此处报错。</para>
	/// </remarks>
	public new JlXLDPara this[JlTuple index] => SelectObj(index);

	/// <summary>造一个句柄为 UNDEF 的空容器，只当作 <c>Deserialize</c>/<c>Clone</c>/<c>Load</c> 之前的装载位。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转调 <c>base(JlObjectBase.UNDEF, copy: false)</c>，不发任何原生调用，所以 <c>IsInitialized()</c> 为 false；本类没有 <c>JlXLDPara(bool)</c> 这种构造器。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>空容器不能当图标输入参与算子调用（<c>Store</c> 会把零句柄塞给原生层，报错形式由原生决定 [待实测]）；<c>JlObjectBase.Load</c> 要求本实例句柄必须是 UNDEF，所以对已有内容的实例重复装载会抛 JlException("Undisposed object instance when loading output parameter")。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>手里已有一个 <c>JlXLDPara</c> 只想要新副本用 <c>Clone()</c>；从流里恢复用静态 <c>Deserialize(Stream)</c>（它内部就是本构造器 + <c>DeserializeXld</c>）；只有要自己接管一个原生句柄时才用 <c>JlXLDPara(IntPtr)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlXLDPara pending = new JlXLDPara();
	///   bool ready = pending.IsInitialized();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>构造器带 <c>EditorBrowsable(Never)</c> 的句柄重载会做对象类断言，但断言在 <c>key == UNDEF</c> 时整段跳过，所以本构造器与 <c>LoadNew</c> 造出的容器都不会校验原生类，类型写错不会被托管层拦住。</para>
	/// </remarks>
	public JlXLDPara()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPara(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPara(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>由同族 <see cref="JlObject"/> 对象复制构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPara(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "xld_para");
	}

	/// <summary>内部工厂：从算子的输出对象槽装载本类新实例；<paramref name="err"/> 为调用错误码并原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDPara obj)
	{
		obj = new JlXLDPara(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDPara(SerializationInfo info, StreamingContext context)
	{
		DeserializeXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把本容器按库自有二进制格式写入流，实现是 <c>SerializeXld()</c> 取字节 + <c>JlSerializationBuffer.WriteToStream</c> 落流。</summary>
	/// <param name="stream">目标流，须可写。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 1553（<c>SerializeXld</c>）先把容器变成字节，再带库自有头部写进流；因此流里的内容不是可读文本，只能由本类的 <c>Deserialize(Stream)</c> 读回。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>本方法不关闭流、也不回绕 <c>Position</c>，落盘或跨进程传完后要自己把位置归零再读；容器若未初始化（<c>new JlXLDPara()</c> 后没装载过）能否序列化未在托管侧检查 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要内存字节（进队列、存数据库字段）用 <c>SerializeXld()</c>；要跨进程/落文件用本方法配 <c>FileStream</c>；想把几何导成别的软件能读的文本，本库的 <c>JlXLDCont</c> 侧才有 DXF 一类导出，<c>xld_para</c> 这种带平行线对属性的对象没有文本出口。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       parallels.Serialize(ms);
	///       ms.Position = 0;
	///       using JlXLDPara back = JlXLDPara.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本方法是 <c>new</c> 隐藏 <c>JlXLD.Serialize(Stream)</c>，按 <c>JlXLD</c> 静态类型调用会走到基类实现；容器里的平行线对属性是否随二进制一起走未在托管侧体现 [待实测]。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeXld(), stream);
	}

	/// <summary>从本库 <c>Serialize(Stream)</c> 写出的二进制流恢复一个容器，返回的是新对象。</summary>
	/// <param name="stream">源流，须可读且位置指向头部。</param>
	/// <returns>新分配的 JlXLDPara，内部先 <c>new JlXLDPara()</c> 再 <c>DeserializeXld</c> 装载句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>先 <c>JlSerializationBuffer.ReadFromStream</c> 取全部字节，再走原生 id 1552 反序列化；不改动调用方原有的任何容器。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>流的内容必须是同一库同一版本写出的，头部不匹配时由 <c>JlSerializationBuffer</c> 侧报错 [待实测：格式不匹配的具体异常]；静态方法按类名调用，不会像 <c>Serialize</c> 那样受隐藏影响。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要一份独立副本用 <c>Clone()</c>，不必绕流；想按元素挑子集用 <c>SelectObj</c>/<c>CopyObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using FileStream fs = new FileStream(@"C:\vision\parallels.bin", FileMode.Open, FileAccess.Read);
	///   using JlXLDPara parallels = JlXLDPara.Deserialize(fs);
	///   int n = parallels.CountObj();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>；<c>CountObj()</c> 只数元素条数，拿平行线对属性仍要用 <c>GetParallelsXld</c>。</para>
	/// </remarks>
	public new static JlXLDPara Deserialize(Stream stream)
	{
		JlXLDPara hXLDPara = new JlXLDPara();
		hXLDPara.DeserializeXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLDPara;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>实现是 <c>SerializeXld()</c> + <c>new JlXLDPara()</c> + <c>DeserializeXld(data)</c> 的往返，得到一份原生侧完全独立的新句柄，不是引用同一容器的别名。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序列化往返要求本容器已初始化；副本与原容器之后各自独立释放，改一个不影响另一个。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要子集用 <c>SelectObj</c>/<c>CopyObj</c>；要把两份容器并起来用 <c>ConcatObj</c>；整容器备份才用本方法。想省掉一次序列化开销而只要个"同一对象的第二个托管壳"，用 <c>new JlXLDPara(JlObject)</c>（内部 <c>CopyObject</c>）而不是本方法 [待实测：两者的属性共享差异]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara backup = parallels.Clone();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄需 <c>Dispose()</c>；大容器上这是两次原生调用加一份字节缓冲，代价明显高于 <c>CopyObj</c>。显式接口实现 <c>ICloneable.Clone()</c> 返回 <c>object</c>，走的是同一个方法。</para>
	/// </remarks>
	public new JlXLDPara Clone()
	{
		byte[] data = SerializeXld();
		JlXLDPara obj = new JlXLDPara();
		obj.DeserializeXld(data);
		return obj;
	}

	/// <summary>
	///   用图像灰度校验平行线对围出的区域，把它筛成"改过的平行线对"并顺带导出其延长形态。
	/// </summary>
	/// <param name="image">与轮廓同一坐标系的灰度图。</param>
	/// <param name="extParallels">同一次计算得到的扩展平行线对，原生 iconic 输出槽 2。</param>
	/// <param name="quality">平行度质量因子的下限，可给多值元组（此时本实例被钉住，调用结束才解钉）。Default: 0.4</param>
	/// <param name="minGray">线对之间区域平均灰度的下限，整数。Default: 160</param>
	/// <param name="maxGray">线对之间区域平均灰度的上限，整数。Default: 220</param>
	/// <param name="maxStandard">线对之间区域灰度标准差的上限，可给多值元组。Default: 10.0</param>
	/// <returns>通过校验的平行线对新容器；本容器不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 39：本实例进 iconic 槽 1、<c>image</c> 进 iconic 槽 2，控制参数序为 quality(0)、minGray(1)、maxGray(2)、maxStandard(3)（与 C# 形参序一致），两个 iconic 输出用 <c>InitOCT</c> 声明后分别用 <c>JlXLDModPara.LoadNew</c>、<c>JlXLDExtPara.LoadNew</c> 取回。判据是"两条平行边之间那块区域的平均灰度落在 [minGray,maxGray] 且标准差不超过 maxStandard，且平行度不低于 quality"。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>输入必须是 <c>GenParallelsXld</c> 那类 <c>xld_para</c> 容器，灰度图必须与轮廓坐标同一尺度（轮廓是从另一张图或缩放后的图来的就会静默筛错）；<c>minGray</c>/<c>maxGray</c> 在本重载里被 <c>StoreI</c> 按 INTEGER 写入，小数灰度阈值会被截断，且灰度带是单值，不能像 <c>JlOperatorSet.ModParallelsXld</c> 那样给多个灰度带。图像类型（byte/int2/int4）对灰度阈值含义有影响，多通道图是否可用未在托管侧校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只想看当前这批平行线对的灰度统计而不筛选，用 <c>InfoParallelsXld</c>；只要平行线对的几何（两端点行、列、长度、法向角）用继承的 <c>GetParallelsXld</c>；要按长度/间距筛而不看灰度，用 <c>SelectShapeXld</c>。本方法的独有价值就是"用灰度确认线对之间是同一种材质/路面"。</para>
	///   <para><b>参数取向</b></para>
	///   <para>返回值 + 一个 <c>out</c> 容器，两个都是新句柄；<c>extParallels</c> 是 <c>MaxParallelsXld</c> 与 <c>CombineRoadsXld</c> 的输入，只要返回值会把扩展信息丢掉。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, new JlTuple(0.4), 160, 220, new JlTuple(10.0));
	///   using JlXLDPoly roadsides = ext.MaxParallelsXld();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>写 <c>0.4</c>、<c>10.0</c> 这类字面量会命中 <c>double</c> 重载，必须显式 <c>new JlTuple(...)</c> 才走到本重载；元组重载多两次钉固定元组的开销，值不变时无需为此承担。三个对象（返回值、<c>ext</c>、上游容器）都要 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDModPara ModParallelsXld(JlImage image, out JlXLDExtPara extParallels, JlTuple quality, int minGray, int maxGray, JlTuple maxStandard)
	{
		IntPtr proc = JlNativeApi.PreCall(39);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 0, quality);
		JlNativeApi.StoreI(proc, 1, minGray);
		JlNativeApi.StoreI(proc, 2, maxGray);
		JlNativeApi.Store(proc, 3, maxStandard);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(quality);
		JlNativeApi.UnpinTuple(maxStandard);
		err = JlXLDModPara.LoadNew(proc, 1, err, out var obj);
		err = JlXLDExtPara.LoadNew(proc, 2, err, out extParallels);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   <c>ModParallelsXld</c> 的标量便捷重载：平行度下限与标准差上限各只给一个阈值。
	/// </summary>
	/// <param name="image">与轮廓同一坐标系的灰度图。</param>
	/// <param name="extParallels">同一次计算得到的扩展平行线对。</param>
	/// <param name="quality">平行度质量因子的下限。Default: 0.4</param>
	/// <param name="minGray">线对之间区域平均灰度的下限，整数。Default: 160</param>
	/// <param name="maxGray">线对之间区域平均灰度的上限，整数。Default: 220</param>
	/// <param name="maxStandard">线对之间区域灰度标准差的上限。Default: 10.0</param>
	/// <returns>通过校验的平行线对新容器；本容器不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <see cref="ModParallelsXld(JlImage,out JlXLDExtPara,JlTuple,int,int,JlTuple)"/> 是同一个原生 id 39、同一套槽位，唯一差别是 <c>quality</c>、<c>maxStandard</c> 用 <c>StoreD</c> 直写 DOUBLE，不走 <c>Store</c>+<c>UnpinTuple</c> 的钉固流程，省两次托管/原生交互。两个 iconic 输出仍然都要接。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>minGray</c>/<c>maxGray</c> 在两个重载里都是 <c>int</c>，写不成小数也写不成数组；本重载无法表达"不同线对用不同阈值"。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>常规单阈值流程用本重载；要给 quality/maxStandard 多值（原生是否逐元素配对使用未在托管侧体现 [待实测]）就换元组重载，要给多段灰度带则用 <c>JlOperatorSet.ModParallelsXld</c>。</para>
	///   <para><b>参数取向</b></para>
	///   <para>返回值与 <c>out JlXLDExtPara</c> 都是新句柄；<c>image</c> 只读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDModPara mod = parallels.ModParallelsXld(img, out JlXLDExtPara ext, 0.4, 160, 220, 10.0);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值、<c>ext</c> 及上游各容器都需 <c>Dispose()</c>；两个 iconic 输出若只要一个，另一个句柄仍已被 <c>InitOCT</c> 分配，漏接只是丢了引用，不是省了开销。</para>
	/// </remarks>
	public JlXLDModPara ModParallelsXld(JlImage image, out JlXLDExtPara extParallels, double quality, int minGray, int maxGray, double maxStandard)
	{
		IntPtr proc = JlNativeApi.PreCall(39);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreD(proc, 0, quality);
		JlNativeApi.StoreI(proc, 1, minGray);
		JlNativeApi.StoreI(proc, 2, maxGray);
		JlNativeApi.StoreD(proc, 3, maxStandard);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDModPara.LoadNew(proc, 1, err, out var obj);
		err = JlXLDExtPara.LoadNew(proc, 2, err, out extParallels);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   统计本容器的平行线对在给定图像上围出区域的灰度与平行度极值，纯读取、不产生新对象。
	/// </summary>
	/// <param name="image">与轮廓同一坐标系的灰度图。</param>
	/// <param name="qualityMin">平行度质量因子的最小值，DOUBLE。</param>
	/// <param name="qualityMax">平行度质量因子的最大值，DOUBLE。</param>
	/// <param name="grayMin">围出区域平均灰度的最小值，INTEGER。</param>
	/// <param name="grayMax">围出区域平均灰度的最大值，INTEGER。</param>
	/// <param name="standardMin">围出区域灰度标准差的最小值，DOUBLE。</param>
	/// <param name="standardMax">围出区域灰度标准差的最大值，DOUBLE。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 40：<c>Store(proc,1)</c> 放本实例、image 放 iconic 槽 2，随后对输出槽 0..5 依次 <c>LoadD/LoadD/LoadI/LoadI/LoadD/LoadD</c>。用来先探一遍数据范围，再把 <c>ModParallelsXld</c> 的 minGray/maxGray/maxStandard 定在合适位置。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>托管侧每个量只取一个标量：<c>LoadI</c>/<c>LoadD</c> 只读输出元组的第 0 个元素，原生若按轮廓逐条返回，这里拿到的就是第一条的值而不是全容器极值，且第 0 个元素类型不是 INTEGER/DOUBLE 时会被静默换算（INTEGER 读出后强转 int，小数灰度被截断）[待实测：原生返回的是单值还是逐轮廓元组]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要逐条的值请用 <c>JlOperatorSet.InfoParallelsXld</c>（同名算子的元组版，六个 <c>out JlTuple</c>）；要边统计边筛掉不合格的线对用 <c>ModParallelsXld</c>；只要平行线对的几何用 <c>GetParallelsXld</c>。</para>
	///   <para><b>参数取向</b></para>
	///   <para>六个 <c>out</c> 全为标量，返回 <c>void</c>；本容器与 image 都不被改动。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   parallels.InfoParallelsXld(img, out double qMin, out double qMax, out int gMin, out int gMax, out double sMin, out double sMax);
	///   bool bright = gMax &gt; 128;
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>六个装载共用同一条 <c>err</c> 链：任一步失败（例如空容器导致输出元组长度为 0）后，后面每个 <c>out</c> 都会被填成 <c>-1</c>/<c>-1.0</c>，最后在 <c>PostCall</c> 处抛 <c>JlOperatorException</c>，所以"看见 -1"不能当真实统计值用；空容器要先用 <c>CountObj()</c> 挡掉。</para>
	/// </remarks>
	public void InfoParallelsXld(JlImage image, out double qualityMin, out double qualityMax, out int grayMin, out int grayMax, out double standardMin, out double standardMax)
	{
		IntPtr proc = JlNativeApi.PreCall(40);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out qualityMin);
		err = JlNativeApi.LoadD(proc, 1, err, out qualityMax);
		err = JlNativeApi.LoadI(proc, 2, err, out grayMin);
		err = JlNativeApi.LoadI(proc, 3, err, out grayMax);
		err = JlNativeApi.LoadD(proc, 4, err, out standardMin);
		err = JlNativeApi.LoadD(proc, 5, err, out standardMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   按内容（不是按序号）从本容器里剔掉与 <c>objectsSub</c> 相同的平行线对，返回剩余元素组成的新容器。
	/// </summary>
	/// <param name="objectsSub">要减掉的对象元组。</param>
	/// <returns>差集容器；两个输入都不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 558：<c>Store(proc,1)</c> 放本实例、objectsSub 放 iconic 槽 2，一个 iconic 输出经 <c>LoadNew</c> 装成新的 <c>JlXLDPara</c>。判据是元素内容配对，与两容器的先后位置无关。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>本封装不暴露 HALCON 该算子的 epsilon 可选参，走原生默认（精确比较 [待实测：默认容差与"坐标差一点但形状等价"的判法]），亚像素流程里两条只差 1e-9 的线对可能被判成不同。形参类型固定为 <c>JlXLDPara</c>，拿 <c>JlXLD</c> 或别的容器类型传不进来，需先转类型。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>知道要删第几条就用 <c>RemoveObj(index)</c>（按序号，快且确定）；按几何位置筛掉某条用 <c>SelectXldPoint</c> 反选；只有"手上有一份要减掉的对象集合"才用本方法。</para>
	///   <para><b>参数取向</b></para>
	///   <para>单个返回值即新句柄，无 out。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara one = parallels.SelectObj(1);
	///   using JlXLDPara rest = parallels.ObjDiff(one);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>结果条数一般少于输入且下标整体前移，凡按 <c>parallels[i]</c> 缓存过 per-元素属性的数组都要重算；返回值与 <c>one</c> 都要 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDPara ObjDiff(JlXLDPara objectsSub)
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
	///   从给定序号起连续复制若干条平行线对，拼成一个新的 <c>JlXLDPara</c> 容器。
	/// </summary>
	/// <param name="index">起始序号，1 起始。Default: 1</param>
	/// <param name="numObj">要复制的条数，给 -1 表示复制到末尾。Default: 1</param>
	/// <returns>复制出的新容器；本容器不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 568，两个控制参数都用 <c>StoreI</c> 按 INTEGER 写入，所以序号与条数只能是整数；输出经 <c>LoadNew</c> 是新分配的句柄，元素本身是否与原容器共享底层数据未在托管侧体现 [待实测]。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>index</c> 是 1 起始；<c>numObj = -1</c> 是"到末尾"的约定写法，给 0 或负数（-1 除外）不属于合法输入，越界行为由原生决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>取不连续的下标用 <c>SelectObj(new JlTuple(1, 5))</c>；要一份彻底独立的备份用 <c>Clone()</c>；只是想在尾部加元素用 <c>ConcatObj</c>。本方法的定位是"整段截取"，比逐条 <c>SelectObj</c> 再 <c>ConcatObj</c> 少一整套调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara head3 = parallels.CopyObj(1, 3);
	///   using JlXLDPara tail = parallels.CopyObj(2, -1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄需 <c>Dispose()</c>；方法是 <c>new</c> 隐藏 <c>JlXLD.CopyObj</c>，用 <c>JlXLD</c> 静态类型调用拿到的是 <c>JlXLD</c> 容器而不是本类型。</para>
	/// </remarks>
	public new JlXLDPara CopyObj(int index, int numObj)
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
	///   把两个 <c>JlXLDPara</c> 容器首尾相接成一个新容器，本容器的元素在前。
	/// </summary>
	/// <param name="objects2">接在后面的对象元组。</param>
	/// <returns>拼接后的新容器；两个输入都不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 569：本实例进 iconic 槽 1、objects2 进槽 2，输出仍从槽 1 装载。结果的元素顺序是"本容器全部 + objects2 全部"，由调用方自己可控，这与 <c>ObjDiff</c>、<c>SelectShapeXld</c> 那种由原生决定顺序的输出不同。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>形参写死成 <c>JlXLDPara</c>，只能同类型容器互拼；托管侧不对原生对象类做断言，拼进去的元素类若与 <c>xld_para</c> 不同不会在这里报错 [待实测]。拼接不做去重，同一句柄拼两次就有两份。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要插到中间位置用 <c>InsertObj(objectsInsert,index)</c>；要替换某几条用 <c>ReplaceObj</c>；只是备份整容器用 <c>Clone()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara head = parallels.CopyObj(1, 1);
	///   using JlXLDPara tail = parallels.CopyObj(2, -1);
	///   using JlXLDPara again = head.ConcatObj(tail);
	///   int n = again.CountObj();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄需 <c>Dispose()</c>；拼完再按序号 <c>SelectObj</c> 时，下标含义相对原容器已整体偏移。</para>
	/// </remarks>
	public JlXLDPara ConcatObj(JlXLDPara objects2)
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
	///   按序号元组从本容器里挑出若干条平行线对，返回一个新容器。
	/// </summary>
	/// <param name="index">要选出的元素序号（1 起始）。Default: 1</param>
	/// <returns>选出的新容器；本容器不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 572。本重载用 <c>Store(proc,0,index)</c> 钉住 <c>JlTuple</c>，<c>CallProcedure</c> 之后才 <c>UnpinTuple(index)</c>，因此传多值元组可以一次挑多条、并完全决定结果的先后次序。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号 1 起始，<c>index</c> 的基数与 <c>CopyObj</c>、索引器一致；越界与 0/负序号的处理由原生决定 [待实测]。结果的元素类不受托管层校验，只保证静态类型是 <c>JlXLDPara</c>。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>连续一段用 <c>CopyObj</c> 更直观；按灰度/平行度条件筛用 <c>ModParallelsXld</c> 或 <c>SelectShapeXld</c>；按点位包含关系筛用 <c>SelectXldPoint</c>。本重载适合"序号已经由别的算子算出来（如排序后的元组）"的场景。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara picked = parallels.SelectObj(new JlTuple(3, 1));
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄需 <c>Dispose()</c>；上例特意写成 3 在前、1 在后，说明输出顺序按 <c>index</c> 给定的顺序，不是按原容器顺序。</para>
	/// </remarks>
	public new JlXLDPara SelectObj(JlTuple index)
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
	///   按单个序号挑出一条平行线对，返回一个新容器。
	/// </summary>
	/// <param name="index">要选出的元素序号（1 起始）。Default: 1</param>
	/// <returns>选出的新容器；本容器不被改动。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <see cref="SelectObj(JlTuple)"/> 同一个原生 id 572，差别只是这里用 <c>StoreI</c> 直写 INTEGER，不钉元组、也就没有 <c>UnpinTuple</c> 的收尾；只挑一条时用本重载更省。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>一次只能给一个序号；容器为空时结果是一个空容器 [待实测：越界序号是否抛 JlOperatorException]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>索引器 <c>this[JlTuple]</c> 就是本方法的元组版转发；要"除了这几条都要"用 <c>RemoveObj</c> 比先算补集再选省事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara first = parallels.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新句柄需 <c>Dispose()</c>；<c>new</c> 只影响静态类型，按 <c>JlXLD</c> 调用会退回基类版本。</para>
	/// </remarks>
	public new JlXLDPara SelectObj(int index)
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
	///   在给定容差下逐元素比较两个 <c>JlXLDPara</c> 容器是否相同，返回 0/1 整数。
	/// </summary>
	/// <param name="objects2">参与比较的容器。</param>
	/// <param name="epsilon">坐标等数值允许的差，可给多值元组（钉固定元组，调用后解钉）。Default: 0.0</param>
	/// <returns>相同返回 1，不同返回 0；不是布尔值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 573：本实例与 objects2 分别进 iconic 槽 1、2，epsilon 进控制槽 0，输出是单个 INTEGER，由 <c>JlNativeApi.LoadI</c> 从结果元组的第 0 个元素取回。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>两容器元素条数不等是判 0 还是报错，托管侧不检查 [待实测]。<c>epsilon</c> 约束的是平行线对坐标与属性的差，单位随量纲走：行列是像素、length 是像素、phi 是弧度；能否用多值元组给不同元素配不同容差未在托管侧体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>不需要容差、只想快速判"完全一样"用 <c>TestEqualObj</c>；要"差在哪几条"用 <c>ObjDiff</c>；要按条件筛子集用 <c>SelectShapeXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara backup = parallels.Clone();
	///   int same = parallels.CompareObj(backup, new JlTuple(1e-6));
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>两个入参都必须活到 <c>PostCall</c> 之后（实现里靠 <c>GC.KeepAlive</c> 兜住），调用返回前别 Dispose 任一侧；返回类型是 <c>int</c>，<c>if (CompareObj(...))</c> 这种写法编译不过。</para>
	/// </remarks>
	public int CompareObj(JlXLDPara objects2, JlTuple epsilon)
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
	///   <c>CompareObj</c> 的标量重载：整个比较只用一个容差。
	/// </summary>
	/// <param name="objects2">参与比较的容器。</param>
	/// <param name="epsilon">坐标等数值允许的差。Default: 0.0</param>
	/// <returns>相同返回 1，不同返回 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <see cref="CompareObj(JlXLDPara,JlTuple)"/> 是同一个原生 id 573、同一套槽位，只是 <c>epsilon</c> 用 <c>StoreD</c> 直写 DOUBLE，省掉钉固/解钉元组两次交互。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>给 <c>0.0</c> 就是精确比较；比较在原生侧完成，托管层不会先把坐标读出来再逐条比，因此没有中间拷贝。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>不需要容差用 <c>TestEqualObj</c>；要指出差异元素本身用 <c>ObjDiff</c>；不同数量的容器不要拿本方法当"包含关系"判断。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara other = parallels.Clone();
	///   int same = parallels.CompareObj(other, 0.0);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>结果元组为空时 <c>LoadI</c> 返回码变成 7001 并把值填成 <c>-1</c>，紧接着 <c>PostCall</c> 抛 <c>JlOperatorException</c>，所以 <c>-1</c> 不是第三种"不确定"结果，别拿它当分支条件。</para>
	/// </remarks>
	public int CompareObj(JlXLDPara objects2, double epsilon)
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
	///   不带容差地判断两个 <c>JlXLDPara</c> 容器是否等价，返回 0/1 整数。
	/// </summary>
	/// <param name="objects2">对照容器。</param>
	/// <returns>等价返回 1，否则返回 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 576，与 <c>CompareObj</c> 不是同一个算子：没有 epsilon 控制参数，两个容器进 iconic 槽 1、2，输出单个 INTEGER 取第 0 个元素。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>经过仿射变换、亚像素拟合或 <c>ShapeTransXld</c> 之后的容器坐标带浮点差，用它判等几乎必然返回 0，那种比较必须走 <c>CompareObj</c> 给容差 [待实测：本判等比较的是点集还是对象身份]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要容差用 <c>CompareObj</c>；要差集用 <c>ObjDiff</c>；只是校验 <c>Clone()</c>/<c>SerializeXld</c> 往返是否完整时用它最省事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara clone = parallels.Clone();
	///   int equal = parallels.TestEqualObj(clone);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>两个入参都要活到调用结束（实现用 <c>GC.KeepAlive</c> 保证）；它不做序列化，开销远低于把两边都 <c>SerializeXld()</c> 再比字节。</para>
	/// </remarks>
	public int TestEqualObj(JlXLDPara objects2)
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

	/// <summary>基于规则网格点求畸变图到校正图的映射（rotation 走数值元组版）：本容器提供网格线，返回映射图像并输出实际使用的网格。</summary>
	/// <param name="image">畸变输入图（提供尺寸/坐标系）。</param>
	/// <param name="meshes">本次映射实际使用的网格线对新容器（新句柄）。</param>
	/// <param name="gridSpacing">校正图中网格点间距，像素整数。</param>
	/// <param name="rotation">施加给点网格的旋转角，数值元组（按本库角度约定应为弧度）[待实测]；调用期间被钉固。Default: "auto"</param>
	/// <param name="row">网格点行坐标元组（像素）。</param>
	/// <param name="column">网格点列坐标元组（像素）。</param>
	/// <param name="mapType">映射类型串。Default: "bilinear"</param>
	/// <returns>承载映射数据的 <c>JlImage</c> 新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1107，与 string 重载同一算子：image 进 iconic 输入槽 1、本实例进 iconic 输入槽 2；控制槽 0..4 为 gridSpacing/rotation/row/column/mapType；本重载把 <c>rotation</c> 连同 <c>row</c>/<c>column</c> 一起 <c>Store</c> 钉固、调用后逐个 <c>UnpinTuple</c>，string 重载则 <c>StoreS</c> 直写旋转串。</para>
	///   <para><b>约束或前提</b>数值 rotation 只能表达确定角度，拿不到 "auto"（自动定角）行为，自动对齐请走 string 重载；角度单位与正方向未在托管侧体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"auto"选 <see cref="GenGridRectificationMap(JlImage,out JlXLDPara,int,string,JlTuple,JlTuple,string)"/>；要按已知安装角固定网格朝向才用本重载；标定参数已知时优先标定族。</para>
	///   <para><b>参数取向</b>返回新 <c>JlImage</c> 句柄，<c>out meshes</c> 为新 <c>JlXLDPara</c> 句柄，两者都要 <c>Dispose()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara meshes = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   JlTuple gridRows = new JlTuple(50.0, 150.0, 250.0);
	///   JlTuple gridCols = new JlTuple(50.0, 150.0, 250.0);
	///   using JlImage map = meshes.GenGridRectificationMap(img, out JlXLDPara usedMeshes, 20, new JlTuple(0.0), gridRows, gridCols, "bilinear");
	///   usedMeshes.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本容器即使只是"被用作网格输入"也会被 <c>GC.KeepAlive</c> 保到调用结束；三个钉固元组在方法返回前不可 Dispose。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDPara meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
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

	/// <summary>基于规则网格点求畸变图到校正图的映射：本容器提供网格线（meshes 输入），返回映射图像并输出算法实际使用的网格。</summary>
	/// <param name="image">畸变输入图（提供尺寸/坐标系）。</param>
	/// <param name="meshes">本次映射实际使用的网格线对新容器（新句柄）。</param>
	/// <param name="gridSpacing">校正图中网格点间距，像素整数。</param>
	/// <param name="rotation">网格旋转模式串。Default: "auto"</param>
	/// <param name="row">网格点行坐标元组（像素）。</param>
	/// <param name="column">网格点列坐标元组（像素）。</param>
	/// <param name="mapType">映射类型串。Default: "bilinear"</param>
	/// <returns>承载映射数据的 <c>JlImage</c> 新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1107：注意槽位——image 进 iconic 输入槽 1，本实例进 iconic 输入槽 2（本容器在这里是输入不是输出）；控制槽 0..4 依次 gridSpacing/rotation/row/column/mapType；两个 iconic 输出槽 1/2 分别用 <c>JlImage.LoadNew</c> 与本类 <c>LoadNew</c> 取回。</para>
	///   <para><b>约束或前提</b><c>row</c>/<c>column</c> 是你在畸变图上量出的网格点坐标，二者配对；<c>gridSpacing</c> 是校正后图的网格步长，模板示例里的 <c>0</c> 是否为合法"自动"值未证实 [待实测]；<c>rotation</c> 用 <c>StoreS</c> 按 STRING 直写，接受 "auto" 这类控制串，本重载省掉 rotation 的钉固（元组重载还要多钉一个）。</para>
	///   <para><b>与相邻算子的取舍</b>已知相机标定参数时用标定族直接生成映射，不必量网格；网格线在图上清晰可提取（本项目里平行线对容器正好当网格线用）才轮到本算子；只要校正单个四边形区域用 find_quad+映射族更直接。</para>
	///   <para><b>参数取向</b>返回新 <c>JlImage</c> 句柄，<c>out meshes</c> 也是新 <c>JlXLDPara</c> 句柄，两个都要 <c>Dispose()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara meshes = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   JlTuple gridRows = new JlTuple(50.0, 150.0, 250.0);
	///   JlTuple gridCols = new JlTuple(50.0, 150.0, 250.0);
	///   using JlImage map = meshes.GenGridRectificationMap(img, out JlXLDPara usedMeshes, 20, "auto", gridRows, gridCols, "bilinear");
	///   usedMeshes.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>传字面量 <c>"auto"</c> 时编译器优先命中本 string 重载（标准引用转换优于隐式转 JlTuple），要按弧度数值给旋转角请显式构造 <c>JlTuple</c> 走元组重载；<c>row</c>/<c>column</c> 仍被钉固。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDPara meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
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

	/// <summary>把 <see cref="SerializeXld"/> 产出的字节装入"本实例自己"：先释放当前句柄，再把新句柄装载进来——是原地改写，不返回新对象。</summary>
	/// <param name="serializedItemHandle">serialize_xld 格式的字节数组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1552：方法体第一步就 <c>Dispose()</c> 掉本实例旧句柄（原有内容即刻作废），再 <c>Load(proc, 1)</c> 把反序列化出的句柄写进本实例；<c>GC.KeepAlive(buffer)</c> 保证字节缓冲在原生调用结束前不被回收。</para>
	///   <para><b>约束或前提</b>这是破坏性装载：想让旧数据还活着，先 <c>Clone()</c> 或干脆用静态 <c>Deserialize(Stream)</c>/<see cref="JlXLDExtPara"/> 那类"返回新对象"的路径；静态 <c>Deserialize(Stream)</c> 与 <c>Clone()</c> 内部都调的本方法；<c>JlXLDPara(SerializationInfo,StreamingContext)</c> 反序列化构造器同样走这里。</para>
	///   <para><b>与相邻算子的取舍</b>手里是 <c>Stream</c> 用 <c>Deserialize(Stream)</c>（自己建对象并读流头）；手里已是 <c>byte[]</c> 且有个现成的空壳对象才用本方法；两者都不做对象类校验，装入别的 XLD 类的字节后本实例声明仍是 <c>JlXLDPara</c> [待实测：原生是否拒绝类不匹配的流]。</para>
	///   <para><b>参数取向</b>返回 <c>void</c>；成功时本实例换芯，失败时旧句柄已被释放、实例停在半死状态——对旧数据的引用不要留到调用之后。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   byte[] data = parallels.SerializeXld();
	///   using JlXLDPara shell = new JlXLDPara();
	///   shell.DeserializeXld(data);
	///   int n = shell.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>同一实例重复调用会再次先 Dispose 再装载，不泄漏；但把同一实例当"不可变值"到处传的写法会被本方法静默掏空，跨线程共享实例调用本方法尤其危险 [待实测：并发下的句柄竞态表现]。</para>
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

	/// <summary>把本容器按库自有二进制格式打包成托管字节数组返回，不触碰任何句柄生命周期。</summary>
	/// <returns>序列化后的字节数组（含库头部），可直接喂给 <see cref="DeserializeXld"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1553：本实例进 iconic 槽 1，输出槽 0 用 <c>JlSerializationBuffer.LoadBytes</c> 拷成 <c>byte[]</c> 后即脱离原生内存，<c>GC.KeepAlive(this)</c> 保证打包完成前句柄不被回收。</para>
	///   <para><b>约束或前提</b>要求本容器已初始化；对 <c>new JlXLDPara()</c> 出来的 UNDEF 实例调用会怎样 [待实测]。字节内容非公开格式，只能由本库读回。</para>
	///   <para><b>与相邻算子的取舍</b>要落文件/走网络流用 <c>Serialize(Stream)</c>（内部就是本方法再写流）；要进程内快速复制容器用 <c>Clone()</c>（同样走一次序列化往返）；<c>ISerializable</c> 通道的 <c>GetObjectData</c> 也调本方法取字节。</para>
	///   <para><b>参数取向</b>返回的是托管数组，无需 Dispose；原生句柄不受影响，返回后原容器照常可用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   byte[] data = parallels.SerializeXld();
	///   using JlXLDPara copy = new JlXLDPara();
	///   copy.DeserializeXld(data);
	///   </code>
	///   <para><b>资源与坑</b>大容器打包出的数组会明显大于原始点数据的直觉预期（含头部与对齐），存库字段要按此预留。</para>
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

	/// <summary>用元组坐标挑出穿过给定点的平行线对，返回子集新容器；本容器不被改动。</summary>
	/// <param name="row">测试点行坐标元组（像素，y 向下为正）；调用期间被钉固。Default: 100.0</param>
	/// <param name="column">测试点列坐标元组（像素，x 向右为正）；调用期间被钉固。Default: 100.0</param>
	/// <returns>包含给定点的元素组成的新容器，可能为空容器。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1595，与标量重载同一算子；本方法 <c>Store</c> 钉住 <c>row</c>/<c>column</c> 两个元组、调用后逐个 <c>UnpinTuple</c>，比 <c>StoreD</c> 直写多四次交互。</para>
	///   <para><b>约束或前提</b>两元组长度应一致（逐位配对成点）[待实测：原生是否配对消费还是各取首值]；给单值元组与标量重载等价，但白担钉固开销。</para>
	///   <para><b>与相邻算子的取舍</b>只测一个点用 <see cref="SelectXldPoint(double,double)"/>；本重载的存在意义是给"多点/多区间"留通道，其真实多值语义未经证实，生产上按标量逐点调用更稳。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara hit = parallels.SelectXldPoint(new JlTuple(240.5), new JlTuple(320.0));
	///   </code>
	///   <para><b>资源与坑</b>注意 <c>int</c> 字面量会优先命中 <c>double</c> 重载（标准数值转换优于用户隐式转换），想显式走本重载就构造 <c>JlTuple</c>。</para>
	/// </remarks>
	public new JlXLDPara SelectXldPoint(JlTuple row, JlTuple column)
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

	/// <summary>挑出穿过给定点的平行线对，返回子集新容器；本容器不被改动。</summary>
	/// <param name="row">测试点的行坐标（像素，y 向下为正），可带小数。Default: 100.0</param>
	/// <param name="column">测试点的列坐标（像素，x 向右为正）。Default: 100.0</param>
	/// <returns>包含该点的元素组成的新容器，可能为空容器。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1595：本实例进 iconic 槽 1，两点坐标用 <c>StoreD</c> 直写 DOUBLE 控制槽 0/1，无钉固；"包含"对线段而言指点落在线段路径上（相邻像素容差内）[待实测：精确容差值]，不是指点在线对围成的带状区域内部。</para>
	///   <para><b>约束或前提</b>坐标以像素计、以图像左上角为原点，与 <c>GetParallelsXld</c> 的 row/col 同一坐标系；点在图像外不报错，只是大概率筛空。</para>
	///   <para><b>与相邻算子的取舍</b>要"围住点的那块区域"应该先转 <c>JlRegion</c> 侧再做内点测试；按几何特征筛用 <c>SelectShapeXld</c>；需要同时测多个点并取并集时用元组重载 <see cref="SelectXldPoint(JlTuple,JlTuple)"/>，但多值语义未证实 [待实测]，稳妥做法是逐点调用后各自处理结果容器。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara hit = parallels.SelectXldPoint(240.5, 320.0);
	///   </code>
	///   <para><b>资源与坑</b>给 <c>int</c> 字面量（如 <c>100</c>）会经标准数值转换命中本 <c>double</c> 重载而非元组重载；两重载行为同一算子，语义无差，仅钉固开销不同。</para>
	/// </remarks>
	public new JlXLDPara SelectXldPoint(double row, double column)
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

	/// <summary>按多个形状特征及其区间筛平行线对，特征间按 <c>operation</c> 连接，返回合格元素的新容器；本容器不被改动。</summary>
	/// <param name="features">特征名元组，可多个，与 <c>min</c>/<c>max</c> 按位置一一配对。Default: "area"</param>
	/// <param name="operation">多特征间连接方式，"and" 全满足 / "or" 任一满足。Default: "and"</param>
	/// <param name="min">各特征下限元组，元素可写字符串 'min' 表示该特征不设下限。Default: 150.0</param>
	/// <param name="max">各特征上限元组，元素可写字符串 'max' 表示该特征不设上限。Default: 99999.0</param>
	/// <returns>通过条件筛选的元素组成的新容器。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1597，与标量重载同一算子；本方法把 <c>features</c>/<c>min</c>/<c>max</c> 三个元组 <c>Store</c> 钉固、调用后逐个 <c>UnpinTuple</c>，换来多特征一次做完的能力。</para>
	///   <para><b>约束或前提</b>三个元组的长度要按特征数对齐（<c>min</c>/<c>max</c> 与 <c>features</c> 逐位配对）；混合数值与 'min'/'max' 字符串的"锯齿元组"是否被原生接受 [待实测]；特征名对本容器元素的有效性由原生判定，托管侧不校验。</para>
	///   <para><b>与相邻算子的取舍</b>单特征单区间用 <see cref="SelectShapeXld(string,string,double,double)"/> 省三次钉固；'min'/'max' 这种"只设一边"的写法只有本重载能表达（标量版 <c>double</c> 装不下字符串）；几何之外的灰度判断走 <c>ModParallelsXld</c>。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>；三个入参元组在方法返回前不得释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara big = parallels.SelectShapeXld(new JlTuple("area"), "and",
	///       new JlTuple(150.0), new JlTuple("max"));
	///   </code>
	///   <para><b>资源与坑</b>写 <c>new JlTuple("area")</c> 是字符串元组；若直接把 <c>"area"</c> 字面量传给本重载的第一个形参也合法（隐式转换），但第二、三个 <c>JlTuple</c> 形参收到数值时同样走隐式转换——两个重载同时可见时编译器按实参类型定夺，想要本重载就给显式 <c>JlTuple</c>。</para>
	/// </remarks>
	public new JlXLDPara SelectShapeXld(JlTuple features, string operation, JlTuple min, JlTuple max)
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

	/// <summary>按单个形状特征的数值区间筛平行线对，返回合格元素的新容器；本容器不被改动。</summary>
	/// <param name="features">单个特征名。Default: "area"</param>
	/// <param name="operation">多特征间的连接方式；单特征时实际不起作用。Default: "and"</param>
	/// <param name="min">该特征的下限（含），DOUBLE 直写。Default: 150.0</param>
	/// <param name="max">该特征的上限（含）。Default: 99999.0</param>
	/// <returns>特征值落在 [min,max] 内的元素组成的新容器。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1597，与元组重载同一算子；<c>features</c>/<c>operation</c> 用 <c>StoreS</c>、<c>min</c>/<c>max</c> 用 <c>StoreD</c> 直写，全程无钉固，是单特征筛选的便宜路径。</para>
	///   <para><b>约束或前提</b>特征名对 <c>xld_para</c> 元素是否可用由原生决定，托管侧不校验，传错名要么报错要么全筛空 [待实测]；形参是 <c>double</c>，文档里"下限可填字符串 'min' 表示不设下限"的写法在本重载表达不出来，单边区间要么写真实极值（0.0 / 99999.0 这类），要么换元组重载。</para>
	///   <para><b>与相邻算子的取舍</b>多个特征要同时满足或任一满足时用 <see cref="SelectShapeXld(JlTuple,string,JlTuple,JlTuple)"/>（<c>features</c> 给多值、<c>operation</c> 取 "and"/"or"）；按"过不过某点"筛用 <c>SelectXldPoint</c>；按灰度筛用 <c>ModParallelsXld</c>，本方法只看几何特征不碰图像。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara big = parallels.SelectShapeXld("area", "and", 150.0, 99999.0);
	///   </code>
	///   <para><b>资源与坑</b>筛空不是错误，返回的是 0 元素容器，后续算子拿到空容器可能才在别处炸出更难定位的错，关键路径先查 <c>CountObj()</c>。</para>
	/// </remarks>
	public new JlXLDPara SelectShapeXld(string features, string operation, double min, double max)
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

	/// <summary>按 <c>type</c> 指定的方式改写每条轮廓/多边形的几何形状，返回变换后的新容器；本容器不被改动。</summary>
	/// <param name="type">变换类型字符串。Default: "convex"</param>
	/// <returns>几何被变换后的新容器（仍是 <c>JlXLDPara</c> 声明类型）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1608：本实例进 iconic 槽 1，控制槽 0 用 <c>StoreS</c> 写类型串；<c>"convex"</c> 把每条轮廓替换为其凸包。类型串的完整取值集在托管侧无枚举校验，写错要到原生报错才可见 [待实测]。</para>
	///   <para><b>约束或前提</b>作用于多边形/闭合轮廓才有意义，开放的 XLD 轮廓做 <c>"convex"</c> 会把形状补凸；<c>xld_para</c> 元素承载的是平行线对属性（端点/长度/法向角），几何变换后 <c>GetParallelsXld</c> 读出的八个属性是否仍与变换后形状一致 [待实测]——要保住平行线对语义，改形状前先读属性、改后重建更稳妥。</para>
	///   <para><b>与相邻算子的取舍</b>本方法改的是元素内部几何（点数、形状），不增删元素；要按平行度、长度等数值筛子集用 <c>SelectShapeXld</c>；要整体挪位置（平移/旋转/缩放）应该用仿射变换族而不是本方法。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara convex = parallels.ShapeTransXld("convex");
	///   </code>
	///   <para><b>资源与坑</b>凸化后每条轮廓的点数会重排，原容器序号与新容器仍一一对应，但"第 i 条的属性"已不是第 i 条线对的原值；结果拿去喂 <c>ModParallelsXld</c> 时灰度统计区域随之改变。</para>
	/// </remarks>
	public new JlXLDPara ShapeTransXld(string type)
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

	/// <summary>把 <c>objectsInsert</c> 的全部元素整段插进本容器的指定序号位，返回加长后的新容器；两个输入容器都不被改动。</summary>
	/// <param name="objectsInsert">被插入的元素容器，其内部顺序保持原样。</param>
	/// <param name="index">插入位置序号（1 起始），落在该序号处，原有元素依次后移。</param>
	/// <returns>加长后的新容器，元素数为两输入之和。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2003：本实例进 iconic 槽 1、<c>objectsInsert</c> 进 iconic 槽 2，控制槽 0 用 <c>StoreI</c> 直写插入位；只接受标量位置，没有元组重载。</para>
	///   <para><b>约束或前提</b>序号基数是 1，<c>index</c> 写 0 的原生行为未在托管侧体现 [待实测：是报错、还是前插到最前]；<c>index</c> 大于 <c>CountObj()+1</c> 时通常等效于追加到尾部 [待实测]。插到 <c>CountObj()+1</c> 位就是尾追。</para>
	///   <para><b>与相邻算子的取舍</b>只在尾部追加用 <c>ConcatObj</c>，语义直白；要顶掉原有位置（长度不变）用 <c>ReplaceObj</c>；把两段元素交错插到多个不同位置，本方法做不到，只能拆成多次调用或换 <c>SelectObj</c>+<c>ConcatObj</c> 重组。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>；<c>objectsInsert</c> 被 <c>GC.KeepAlive</c> 保到原生调用结束。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara extra = poly.GenParallelsXld(15.0, 40.0, 0.2, "true");
	///   using JlXLDPara grown = parallels.InsertObj(extra, 1);
	///   </code>
	///   <para><b>资源与坑</b>插入的元素与原容器 <c>extra</c> 共享同一批图标对象，<c>Dispose()</c> 各自句柄互不影响，但别再对 <c>grown</c> 里这些位置做 <c>ReplaceObj</c> 之外的"就地"幻想——一切修改都是返回新容器。</para>
	/// </remarks>
	public JlXLDPara InsertObj(JlXLDPara objectsInsert, int index)
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

	/// <summary>一次删掉本容器里若干条平行线对，返回剩余元素组成的新容器；本容器不被改动。</summary>
	/// <param name="index">要删除元素的序号元组（1 起始）；调用期间被钉固，原生调用结束后解钉。</param>
	/// <returns>删除后的新容器；删空时是元素数为 0 的新容器而非 null。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2005，与标量重载同一算子；差别在托管侧：本方法用 <c>Store</c> 钉住 <c>index</c> 元组、<c>CallProcedure</c> 返回后 <c>UnpinTuple</c>，比标量版 <c>StoreI</c> 直写多两次交互，换来的是"一次删多条"。</para>
	///   <para><b>约束或前提</b>序号基数是 1；元组里出现重复序号时该条只会被删一次还是第二次报越界 [待实测]；混入 0/负数/越界序号的行为与标量重载一致，托管侧不校验。</para>
	///   <para><b>与相邻算子的取舍</b>删一条用 <see cref="RemoveObj(int)"/> 省钉固开销；要"只留这几条"（反向操作）用 <c>SelectObj(JlTuple)</c>；按特征批量剔除先 <c>SelectShapeXld</c> 挑出合格者，通常比算完序号再来 <c>RemoveObj</c> 更省事。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>；<c>index</c> 元组在方法返回前不得提前 Dispose（钉固指向它的原生数据）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara rest = parallels.RemoveObj(new JlTuple(1, 3));
	///   int n = rest.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>纯数值元组无需手动 Dispose 也不影响正确性（Dispose 只处理句柄类元素），但删除使剩余元素序号前移，原容器序号映射到本结果时要自己换算。</para>
	/// </remarks>
	public new JlXLDPara RemoveObj(JlTuple index)
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

	/// <summary>删掉本容器里指定序号的那条平行线对，返回剩余元素组成的新容器；本容器不被改动。</summary>
	/// <param name="index">要删除元素的序号（1 起始），单值。</param>
	/// <returns>删除后的新容器；删空时是元素数为 0 的新容器而非 null。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2005：本实例进 iconic 槽 1，控制槽 0 用 <c>StoreI</c> 直写 INTEGER，无钉固；与元组重载同一 id，差别仅在钉固路径。</para>
	///   <para><b>约束或前提</b>序号基数是 1（原模板示例写 <c>RemoveObj(0)</c> 删不到第一条）；0、负数或超过 <c>CountObj()</c> 的序号托管侧不拦，原生报错还是静默无删 [待实测]。删除后其余元素序号整体前移，下游按"第几条"取值依赖本容器时要重算。</para>
	///   <para><b>与相邻算子的取舍</b>反向操作"只留这几条"用 <c>SelectObj</c>；只删一条用本标量重载，删多条用 <see cref="RemoveObj(JlTuple)"/> 一次做完；要的是"被删掉的那部分"而非剩余部分时用 <c>ObjDiff</c>。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>；剩余元素与原容器各走各的，释放原容器不影响新容器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara rest = parallels.RemoveObj(1);
	///   int n = rest.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>循环逐条删要付 n 次完整算子调用，一次 <c>RemoveObj(new JlTuple(1, 2))</c> 就能替掉两次调用。</para>
	/// </remarks>
	public new JlXLDPara RemoveObj(int index)
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

	/// <summary>一次性替换本容器里多个位置的元素，返回替换后的新容器；序号与替换元素按位置逐一配对。</summary>
	/// <param name="objectsReplace">提供替换元素的容器，第 k 个元素顶给第 k 个序号位。</param>
	/// <param name="index">被替换元素的序号元组（1 起始），可含多个值；调用期间被钉固，结束才解钉。</param>
	/// <returns>替换后的新容器，元素总数与本容器一致。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2006，与标量重载同一算子：本实例进 iconic 槽 1、<c>objectsReplace</c> 进 iconic 槽 2、index 进控制槽 0；差别在托管侧——元组版 <c>Store</c> 钉固定元组、调用后 <c>UnpinTuple</c>，标量版 <c>StoreI</c> 直写无钉固。固定一个序号时本重载也能用，但白担钉固开销，应改用 <see cref="ReplaceObj(JlXLDPara,int)"/>。</para>
	///   <para><b>约束或前提</b>序号基数是 1；<c>index</c> 的长度超过 <c>objectsReplace</c> 的元素数时，多出来的序号位如何处理（保持原值还是报错）托管侧无体现 [待实测]；<c>index</c> 传空元组应等于原样拷贝一份 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>整段位置连续且要覆盖时用本方法一次做完，比循环调标量重载少 n-1 次算子调用；把容器"缩短"是 <c>RemoveObj</c> 的活，"加长"是 <c>InsertObj</c> 的活，本方法长度不变。</para>
	///   <para><b>参数取向</b>返回新句柄需 <c>Dispose()</c>；<c>index</c> 在原生调用结束前不得被回收（<c>UnpinTuple</c> 在 <c>CallProcedure</c> 之后），调用返回后元组即可弃用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara fresh = poly.GenParallelsXld(15.0, 40.0, 0.2, "true");
	///   using JlXLDPara patched = parallels.ReplaceObj(fresh, new JlTuple(1, 2));
	///   </code>
	///   <para><b>资源与坑</b>本容器不被改动，原数据仍在，可安全失败重试；<c>objectsReplace</c> 与 <c>index</c> 都要等本方法返回后才能随作用域释放。</para>
	/// </remarks>
	public JlXLDPara ReplaceObj(JlXLDPara objectsReplace, JlTuple index)
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

	/// <summary>把本容器里指定序号位置的元素换成 <c>objectsReplace</c> 的元素，返回替换后的新容器；本容器与替换源都不被改动。</summary>
	/// <param name="objectsReplace">提供替换元素的容器，其元素按自身顺序依次顶给各替换位。</param>
	/// <param name="index">被替换元素的序号（1 起始），单值。</param>
	/// <returns>替换后的新容器，元素总数与本容器一致。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2006：本实例进 iconic 槽 1、<c>objectsReplace</c> 进 iconic 槽 2，控制槽 0 只有 index；标量版走 <c>StoreI</c> 直写 INTEGER，无钉固开销，与元组重载同一 id。</para>
	///   <para><b>约束或前提</b>序号基数是 1（原模板示例写 <c>ReplaceObj(..., 0)</c> 取不到第一个位置）；写 0、负数或大于 <c>CountObj()</c> 的序号时托管侧不拦，原生是报错还是静默跳过 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>换单个位置用本标量重载；一次换多个位置用 <see cref="ReplaceObj(JlXLDPara,JlTuple)"/>；不改变原有元素、只在中间加元素用 <c>InsertObj</c>（结果会变长）；替换后总数不变是本方法与 <c>InsertObj</c>/<c>RemoveObj</c> 的根本区别。</para>
	///   <para><b>参数取向</b>返回新句柄，需 <c>Dispose()</c>；<c>objectsReplace</c> 被 <c>GC.KeepAlive</c> 保到原生调用结束，调用返回后即可释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(100.0, 200.0);
	///   using JlXLDCont cont = reg.GenContourRegionXld("border");
	///   using JlXLDPoly poly = cont.GenPolygonsXld("ramer", 2.0);
	///   using JlXLDPara parallels = poly.GenParallelsXld(10.0, 30.0, 0.15, "true");
	///   using JlXLDPara fresh = poly.GenParallelsXld(15.0, 40.0, 0.2, "true");
	///   using JlXLDPara patched = parallels.ReplaceObj(fresh, 1);
	///   </code>
	///   <para><b>资源与坑</b>替换只影响返回的新容器，原 <c>parallels</c> 该位置不变；替换元素与 <c>objectsReplace</c> 的共享/复制语义未在托管侧体现 [待实测]；本重载不校验 <c>objectsReplace</c> 元素的对象类，塞非 <c>xld_para</c> 容器能否成功 [待实测]。</para>
	/// </remarks>
	public JlXLDPara ReplaceObj(JlXLDPara objectsReplace, int index)
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
