using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an XLD object-(array).</summary>
[Serializable]
public class JlXLD : JlObject, ISerializable, ICloneable
{
	/// <summary>Returns the iconic object(s) at the specified index</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：按索引从当前轮廓容器中取出一条或多条轮廓，返回新容器；实现即 SelectObj(JlTuple) 的属性语法糖，索引不做换算、直接透传原生。</para>
	///   <para><b>约束与前提</b>：索引按默认值 1 判为 1 基 [待实测]；在空容器或未初始化容器上调用会抛 JlOperatorException。</para>
	///   <para><b>与相邻算子的取舍</b>：按形状特征筛选改用 SelectShapeXld，不依赖轮廓顺序。</para>
	///   <para><b>参数取向</b>：index 的每个元素是要取的轮廓序号，序号指上游 GenContourRegionXld/Connection 类算子的输出顺序，顺序变化会静默取错轮廓。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD first = contours[1];
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，取出的轮廓与原容器共享底层数据；JlXLD 与 JlXLDCont 均需 Dispose。</para>
	/// </remarks>
	public new JlXLD this[JlTuple index] => SelectObj(index);

	/// <summary>Create an uninitialized iconic object</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：创建未初始化的 JlXLD 容器（内部句柄为 UNDEF），作为 Deserialize、Clone 等装载句柄前的占位；本类没有接受布尔参数的构造器。</para>
	///   <para><b>约束与前提</b>：未初始化容器不能作为算子的图标输入参与调用，直接调用算子会抛 JlOperatorException。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLD pending = new JlXLD();
	///   </code>
	///   <para><b>资源与坑</b>：JlXLD 实现 IDisposable，用完需 Dispose。</para>
	/// </remarks>
	public JlXLD()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLD(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLD(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>由同族 <see cref="JlObject"/> 对象复制构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLD(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "xld");
	}

	/// <summary>内部工厂：从算子的输出对象槽装载本类新实例；<paramref name="err"/> 为调用错误码并原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLD obj)
	{
		obj = new JlXLD(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLD(SerializationInfo info, StreamingContext context)
	{
		DeserializeXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>Serialize object to binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：把当前容器序列化为二进制流（内部先经 SerializeXld 得到字节缓冲，再写入流）。</para>
	///   <para><b>与相邻算子的取舍</b>：只需内存中字节数组时用 SerializeXld，落盘或跨进程传输时用本方法配 Deserialize。</para>
	///   <para><b>参数取向</b>：stream 需可写；本方法不关闭流传入的句柄。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using System.IO.MemoryStream ms = new System.IO.MemoryStream();
	///   c.Serialize(ms);
	///   </code>
	///   <para><b>资源与坑</b>：容器需已初始化；JlXLD/JlXLDCont 均需 Dispose。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeXld(), stream);
	}

	/// <summary>Deserialize object from binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：静态方法，从 Serialize 写出的二进制流读回一个新的 JlXLD 容器。</para>
	///   <para><b>与相邻算子的取舍</b>：容器已有实例、想原地覆盖时用 DeserializeXld；从流新建用本方法。</para>
	///   <para><b>参数取向</b>：stream 需可读且位置停在写入时的起点附近，通常先 `stream.Position = 0;` 复位。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using System.IO.MemoryStream ms = new System.IO.MemoryStream();
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   c.Serialize(ms);
	///   ms.Position = 0;
	///   using JlXLD b = JlXLD.Deserialize(ms);
	///   </code>
	///   <para><b>资源与坑</b>：返回新容器，需 Dispose；流格式与版本绑定，跨版本读取的兼容性 [待实测]。</para>
	/// </remarks>
	public new static JlXLD Deserialize(Stream stream)
	{
		JlXLD hXLD = new JlXLD();
		hXLD.DeserializeXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLD;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b>：Clone 的实现是 SerializeXld + DeserializeXld 的往返，得到与原容器完全独立的深拷贝，不是句柄别名。</para>
	///   <para><b>与相邻算子的取舍</b>：只想取子集或复制部分元素用 CopyObj/SelectObj，整容器备份用本方法。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlXLD backup = c.Clone();
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；序列化往返有额外拷贝开销，大容器慎用。</para>
	/// </remarks>
	public new JlXLD Clone()
	{
		byte[] data = SerializeXld();
		JlXLD obj = new JlXLD();
		obj.DeserializeXld(data);
		return obj;
	}

	/// <summary>
	///   Return an XLD parallel's data (as lines).
	/// </summary>
	/// <param name="row1">Row coordinates of the points on polygon P1.</param>
	/// <param name="col1">Column coordinates of the points on polygon P1.</param>
	/// <param name="length1">Lengths of the line segments on polygon P1.</param>
	/// <param name="phi1">Angles to the normal vectors of the line segments on polygon P1.</param>
	/// <param name="row2">Row coordinates of the points on polygon P2.</param>
	/// <param name="col2">Column coordinates of the points on polygon P2.</param>
	/// <param name="length2">Lengths of the line segments on polygon P2.</param>
	/// <param name="phi2">Angles to the normal vectors of the line segments on polygon P2.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：把容器中的轮廓按"平行线段对"读出：返回 P1/P2 两侧线段的端点行、列、长度与法向角，共 8 组 DOUBLE 元组。</para>
	///   <para><b>约束与前提</b>：仅对成对平行线段成立的轮廓（如经分割得到的矩形类轮廓）有实际意义；结果长度是找到的平行对数，不一定等于轮廓数 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要整体外接矩形用 SmallestRectangle2Xld，需要逐段平行关系（对边检测、卡尺标定）才用本方法。</para>
	///   <para><b>参数取向</b>：全部为 out JlTuple，每条轮廓可能贡献多组值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont rect = new JlXLDCont(new double[] { 10, 10, 50, 50, 10 }, new double[] { 10, 60, 60, 10, 10 });
	///   rect.GetParallelsXld(out JlTuple row1, out JlTuple col1, out JlTuple length1, out JlTuple phi1,
	///     out JlTuple row2, out JlTuple col2, out JlTuple length2, out JlTuple phi2);
	///   </code>
	///   <para><b>资源与坑</b>：本方法原地读取、不产生新句柄；入参容器仍需 Dispose。</para>
	/// </remarks>
	public void GetParallelsXld(out JlTuple row1, out JlTuple col1, out JlTuple length1, out JlTuple phi1, out JlTuple row2, out JlTuple col2, out JlTuple length2, out JlTuple phi2)
	{
		IntPtr proc = JlNativeApi.PreCall(41);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row1);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out col1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out length1);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out phi1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out row2);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out col2);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out length2);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out phi2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Calculate the difference of two object tuples.
	/// </summary>
	/// <param name="objectsSub">Object tuple 2.</param>
	/// <returns>Objects from Objects that are not part of ObjectsSub.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：从本容器中剔除与 objectsSub 内容相同的轮廓，返回剩余元素组成的新容器。</para>
	///   <para><b>约束与前提</b>：按轮廓内容逐对判等（epsilon 取默认 0，即精确比较 [待实测]），与容器内顺序无关。</para>
	///   <para><b>与相邻算子的取舍</b>：按序号剔除用 RemoveObj，按内容剔除用本方法。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont all = r.GenContourRegionXld("border");
	///   using JlXLD one = all.SelectObj(1);
	///   using JlXLD rest = all.ObjDiff(one);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄；本容器与 objectsSub 均不改动，各自需 Dispose。</para>
	/// </remarks>
	public JlXLD ObjDiff(JlXLD objectsSub)
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
	///   Paint XLD objects into an image.
	/// </summary>
	/// <param name="image">Image in which the xld objects are to be painted.</param>
	/// <param name="grayval">Desired gray value of the xld object. Default: 255.0</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把容器中的轮廓按给定灰度描画到 image 的副本上，返回新图像；输入图像不被改动。</para>
	///   <para><b>约束与前提</b>：轮廓经亚像素折线光栅化后落位，与区域填充算子的覆盖规则不同 [待实测]；多条轮廓覆盖同一像素时的先后次序按容器顺序。</para>
	///   <para><b>与相邻算子的取舍</b>：要像素区域用 GenRegionContourXld 转 JlRegion 再做区域运算；只要像素化轮廓本身用本方法。</para>
	///   <para><b>参数取向</b>：元组版 grayval 可按轮廓逐个给灰度（主实现，内部 Store 后 UnpinTuple）；每条轮廓一个值，长度不匹配时的行为 [待实测]。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlImage painted = c.PaintXld(img, new double[] { 255 });
	///   </code>
	///   <para><b>资源与坑</b>：返回新图像句柄需 Dispose；本容器与 image 也各自需 Dispose。</para>
	/// </remarks>
	public JlImage PaintXld(JlImage image, JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(560);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   Paint XLD objects into an image.
	/// </summary>
	/// <param name="image">Image in which the xld objects are to be painted.</param>
	/// <param name="grayval">Desired gray value of the xld object. Default: 255.0</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：PaintXld(JlImage, JlTuple) 的标量重载，同一灰度用于全部轮廓。</para>
	///   <para><b>参数取向</b>：与元组版同一原生调用（id 560），差异仅是 grayval 以 StoreD 标量直传、无 UnpinTuple 步骤；语义与坑见主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlImage painted = c.PaintXld(img, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>：返回新图像句柄需 Dispose。</para>
	/// </remarks>
	public JlImage PaintXld(JlImage image, double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(560);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   Copy an iconic object in the Vision database.
	/// </summary>
	/// <param name="index">Starting index of the objects to be copied. Default: 1</param>
	/// <param name="numObj">Number of objects to be copied or -1. Default: 1</param>
	/// <returns>Copied objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：复制本容器中从 index 起的 numObj 条轮廓为新容器；原容器不改动。</para>
	///   <para><b>约束与前提</b>：index 为 1 基（参数默认值为 1，包装层不换算）[待实测]，越界与 numObj=-1 的确切行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：CopyObj 取连续区段；任意/重复索引改用 SelectObj。</para>
	///   <para><b>参数取向</b>：index 起点序号，numObj 个数。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD head = contours.CopyObj(1, 1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，两容器共享底层轮廓；都要 Dispose。</para>
	/// </remarks>
	public new JlXLD CopyObj(int index, int numObj)
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
	///   Concatenate two iconic object tuples.
	/// </summary>
	/// <param name="objects2">Object tuple 2.</param>
	/// <returns>Concatenated objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把 objects2 的元素接到本容器之后，返回新容器；顺序固定为 this 在前、objects2 在后。</para>
	///   <para><b>约束与前提</b>：两个输入容器都不改动；元素类型须同为 xld 对象。</para>
	///   <para><b>与相邻算子的取舍</b>：只在末尾拼接用本方法；要插到中间位置用 InsertObj，按索引替换用 ReplaceObj。</para>
	///   <para><b>参数取向</b>：objects2 可以是 JlXLD 或其派生 JlXLDCont。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c1 = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlXLDCont c2 = new JlXLDCont(new double[] { 60, 70, 80 }, new double[] { 60, 75, 62 });
	///   using JlXLD pair = c1.ConcatObj(c2);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，c1/c2 与 pair 共享底层轮廓；三者各自 Dispose。</para>
	/// </remarks>
	public JlXLD ConcatObj(JlXLD objects2)
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
	///   Select objects from an object tuple.
	/// </summary>
	/// <param name="index">Indices of the objects to be selected. Default: 1</param>
	/// <returns>Selected objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：按 1 基索引把选中的轮廓装入新容器；本容器不改动。</para>
	///   <para><b>约束与前提</b>：参数默认值为 1，包装层 Store 不做索引换算，判为 1 基 [待实测]；越界索引的行为 [待实测]，调用前先用 CountObj 核对元素数。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"第几条"依赖上游 Connection/GenContourRegionXld 的输出顺序，顺序不稳定时用 SelectShapeXld 按特征选取更稳。</para>
	///   <para><b>参数取向</b>：主实现（元组版），index 可含多个序号、可重复；调用后 UnpinTuple 解除钉住。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD picked = contours.SelectObj(new int[] { 1, 2 });
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，元素与容器共享底层轮廓；JlXLD/JlXLDCont 均需 Dispose。</para>
	/// </remarks>
	public new JlXLD SelectObj(JlTuple index)
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
	///   Select objects from an object tuple.
	/// </summary>
	/// <param name="index">Indices of the objects to be selected. Default: 1</param>
	/// <returns>Selected objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：SelectObj(JlTuple) 的标量重载，只取一条轮廓。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 572），index 以 StoreI 整数直传、无 UnpinTuple；语义与坑见主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD first = contours.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose。</para>
	/// </remarks>
	public new JlXLD SelectObj(int index)
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
	///   Compare iconic objects regarding equality.
	/// </summary>
	/// <param name="objects2">Test objects.</param>
	/// <param name="epsilon">Maximum allowed difference between two gray values or coordinates etc. Default: 0.0</param>
	/// <returns>Boolean result value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：带容差地逐对比较两个容器的轮廓内容，返回布尔判定（int）。</para>
	///   <para><b>约束与前提</b>：要求两容器元素数一致，不一致时的返回值 [待实测]；epsilon=0 为逐坐标精确比较。</para>
	///   <para><b>与相邻算子的取舍</b>：无需容差的快速判等可用 TestEqualObj；需要"坐标差 ≤ epsilon 即算相等"用本方法。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：epsilon 可按对象对逐个给值，Store 后 UnpinTuple；结果经 LoadI 以 int 取回。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c1 = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlXLDCont c2 = c1.Clone();
	///   int same = c1.CompareObj(c2, 1e-6);
	///   </code>
	///   <para><b>资源与坑</b>：纯比较、不产生新句柄；两输入容器仍需各自 Dispose。</para>
	/// </remarks>
	public int CompareObj(JlXLD objects2, JlTuple epsilon)
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
	///   Compare iconic objects regarding equality.
	/// </summary>
	/// <param name="objects2">Test objects.</param>
	/// <param name="epsilon">Maximum allowed difference between two gray values or coordinates etc. Default: 0.0</param>
	/// <returns>Boolean result value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：CompareObj(JlXLD, JlTuple) 的标量重载，整个比较使用同一容差。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 573），epsilon 以 StoreD 标量直传；语义与坑见主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c1 = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlXLDCont c2 = c1.Clone();
	///   int same = c1.CompareObj(c2, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>：不产生新句柄；两输入容器需 Dispose。</para>
	/// </remarks>
	public int CompareObj(JlXLD objects2, double epsilon)
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
	///   Compare image objects regarding equality.
	/// </summary>
	/// <param name="objects2">Comparative objects.</param>
	/// <returns>Boolean result value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：不带容差地判断两个轮廓容器是否逐元素相等，返回 int 判定。</para>
	///   <para><b>约束与前提</b>：对 xld 内容的相等判据（坐标逐点一致才算等）与元素数不同时的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：拟合/近似结果比对必须用 CompareObj 带 epsilon；只有同源拷贝的核对适合本方法。</para>
	///   <para><b>参数取向</b>：无控制参数；结果经 LoadI 以 int 取回。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c1 = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlXLDCont c2 = c1.Clone();
	///   int equal = c1.TestEqualObj(c2);
	///   </code>
	///   <para><b>资源与坑</b>：不产生新句柄；两输入容器需 Dispose。</para>
	/// </remarks>
	public int TestEqualObj(JlXLD objects2)
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
	///   Compute the mapping between the distorted image and the rectified image based upon the points of a regular grid.
	/// </summary>
	/// <param name="image">Input image.</param>
	/// <param name="meshes">Output contours.</param>
	/// <param name="gridSpacing">Distance of the grid points in the rectified image.</param>
	/// <param name="rotation">Rotation to be applied to the point grid. Default: "auto"</param>
	/// <param name="row">Row coordinates of the grid points.</param>
	/// <param name="column">Column coordinates of the grid points.</param>
	/// <param name="mapType">Type of mapping. Default: "bilinear"</param>
	/// <returns>Image containing the mapping data.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：基于规则网格点位计算畸变图与校正图之间的映射：返回映射图像（JlImage），并把网格线轮廓输出到 meshes。</para>
	///   <para><b>约束与前提</b>：image 与网格点（row/column）必须同源同标定；本容器作为图标输入 2 随 image（输入 1）一起传给原生，其对结果的作用 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：已有解析标定参数时直接用投影变换族，不必由网格反推映射。</para>
	///   <para><b>参数取向</b>：主实现（rotation 为 JlTuple，Store 后 UnpinTuple）；gridSpacing 以 StoreI 整数直传；mapType 以 StoreS 字符串直传；row/column 是网格点坐标，长度须配对。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont grid = new JlXLDCont(new double[] { 10, 10, 90 }, new double[] { 10, 50, 90 });
	///   JlTuple rot = "auto";
	///   using JlImage map = grid.GenGridRectificationMap(img, out JlXLD meshes, 8, rot,
	///     new double[] { 10, 20 }, new double[] { 10, 20 }, "bilinear");
	///   meshes.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>：返回新映射图像、out 出新网格容器，均需 Dispose；map 供图像校正算子消费后再释放。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLD meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
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
	///   Compute the mapping between the distorted image and the rectified image based upon the points of a regular grid.
	/// </summary>
	/// <param name="image">Input image.</param>
	/// <param name="meshes">Output contours.</param>
	/// <param name="gridSpacing">Distance of the grid points in the rectified image.</param>
	/// <param name="rotation">Rotation to be applied to the point grid. Default: "auto"</param>
	/// <param name="row">Row coordinates of the grid points.</param>
	/// <param name="column">Column coordinates of the grid points.</param>
	/// <param name="mapType">Type of mapping. Default: "bilinear"</param>
	/// <returns>Image containing the mapping data.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：GenGridRectificationMap 主实现的字符串重载，rotation 作为单一控制串传入。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1107），差异仅是 rotation 以 StoreS 写入、无该参数的 UnpinTuple；其余语义与坑见 (JlTuple rotation) 主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont grid = new JlXLDCont(new double[] { 10, 10, 90 }, new double[] { 10, 50, 90 });
	///   using JlImage map = grid.GenGridRectificationMap(img, out JlXLD meshes, 8, "auto",
	///     new double[] { 10, 20 }, new double[] { 10, 20 }, "bilinear");
	///   meshes.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>：返回值与 out meshes 均需 Dispose。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLD meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
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
	///   Deserialize a serialized XLD object.
	/// </summary>
	/// <param name="serializedItemHandle">Handle of the serialized item.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：从 SerializeXld 得到的字节缓冲恢复轮廓容器；原地装载——实现先 Dispose() 本实例旧句柄再 Load，本容器内容被直接覆盖。</para>
	///   <para><b>约束与前提</b>：调用前本实例原有的轮廓一定被丢弃；要保留旧结果必须先 Clone()。</para>
	///   <para><b>与相邻算子的取舍</b>：从流读取用静态 Deserialize；手里是字节数组用本方法。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont src = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   byte[] data = src.SerializeXld();
	///   using JlXLD restored = new JlXLD();
	///   restored.DeserializeXld(data);
	///   </code>
	///   <para><b>资源与坑</b>：不产生新句柄（返回值 void）；缓冲内容非法时抛 JlOperatorException。</para>
	/// </remarks>
	public void DeserializeXld(byte[] serializedItemHandle)
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
	///   Serialize an XLD object.
	/// </summary>
	/// <returns>Handle of the serialized item.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把本容器序列化为字节数组（DeserializeXld 的逆操作），返回序列化项的托管拷贝。</para>
	///   <para><b>与相邻算子的取舍</b>：写流用 Serialize(Stream)，只需字节数组（如跨线程/落库）用本方法。</para>
	///   <para><b>参数取向</b>：无参；内部 InitOCT 申请 1 个控制输出，经 JlSerializationBuffer.LoadBytes 取回 byte[]。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   byte[] data = c.SerializeXld();
	///   </code>
	///   <para><b>资源与坑</b>：返回的是托管 byte[]，非句柄，不受 Dispose 影响；本容器需 Dispose。</para>
	/// </remarks>
	public byte[] SerializeXld()
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
	///   Test whether contours or polygons are closed.
	/// </summary>
	/// <returns>Tuple with Boolean numbers.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条判断轮廓/多边形是否闭合，返回 INTEGER 元组（1 闭合、0 开放），长度等于容器内轮廓数。</para>
	///   <para><b>与相邻算子的取舍</b>：要按"是否闭合"筛掉开放轮廓，配合本结果用 SelectObj 取子集；本方法只给判定不筛容器。</para>
	///   <para><b>参数取向</b>：无参；结果按 JlTupleType.INTEGER 装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   JlTuple closed = contours.TestClosedXld();
	///   </code>
	///   <para><b>资源与坑</b>：闭合轮廓才能可靠参与 AreaCenterXld、CircularityXld 等面积口径算子。</para>
	/// </remarks>
	public JlTuple TestClosedXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1586);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Arbitrary geometric moments of contours or polygons treated as point clouds.
	/// </summary>
	/// <param name="mode">Computation mode. Default: "unnormalized"</param>
	/// <param name="area">Area enclosed by the contour or polygon.</param>
	/// <param name="centerRow">Row coordinate of the centroid.</param>
	/// <param name="centerCol">Column coordinate of the centroid.</param>
	/// <param name="p">First index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <param name="q">Second index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <returns>The computed moments.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把轮廓/多边形当作点集计算任意阶几何矩 M(p,q)，点集口径与轮廓方向无关。</para>
	///   <para><b>约束与前提</b>：mode、area、centerRow、centerCol 按本容器 AreaCenterPointsXld/AreaCenterXld 的输出对应提供，作为矩的参考量 [待实测：三者的具体用法]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要固定二阶矩用 MomentsPointsXld；按区域口径（考虑方向）用 MomentsAnyXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：area/centerRow/centerCol 每条轮廓一组值，p/q 为矩阶次，各元组长度须与轮廓数匹配 [待实测：不匹配时行为]；返回 DOUBLE 元组。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   JlTuple area, cr, cc, p, q;
	///   c.AreaCenterPointsXld(out cr, out cc);
	///   area = 3.0; p = 1; q = 1;
	///   JlTuple m = c.MomentsAnyPointsXld("unnormalized", area, cr, cc, p, q);
	///   </code>
	///   <para><b>资源与坑</b>：只产生 JlTuple 结果，无句柄需 Dispose；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple MomentsAnyPointsXld(string mode, JlTuple area, JlTuple centerRow, JlTuple centerCol, JlTuple p, JlTuple q)
	{
		IntPtr proc = JlNativeApi.PreCall(1588);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.Store(proc, 1, area);
		JlNativeApi.Store(proc, 2, centerRow);
		JlNativeApi.Store(proc, 3, centerCol);
		JlNativeApi.Store(proc, 4, p);
		JlNativeApi.Store(proc, 5, q);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(area);
		JlNativeApi.UnpinTuple(centerRow);
		JlNativeApi.UnpinTuple(centerCol);
		JlNativeApi.UnpinTuple(p);
		JlNativeApi.UnpinTuple(q);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Arbitrary geometric moments of contours or polygons treated as point clouds.
	/// </summary>
	/// <param name="mode">Computation mode. Default: "unnormalized"</param>
	/// <param name="area">Area enclosed by the contour or polygon.</param>
	/// <param name="centerRow">Row coordinate of the centroid.</param>
	/// <param name="centerCol">Column coordinate of the centroid.</param>
	/// <param name="p">First index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <param name="q">Second index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <returns>The computed moments.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：MomentsAnyPointsXld(JlTuple...) 的标量重载，单条轮廓求单值矩。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1588），area/centerRow/centerCol 以 StoreD、p/q 以 StoreI 直传，结果经 LoadD 以 double 取回；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   double m11 = c.MomentsAnyPointsXld("unnormalized", 3.0, 20.0, 16.0, 1, 1);
	///   </code>
	///   <para><b>资源与坑</b>：返回标量；本容器需 Dispose。</para>
	/// </remarks>
	public double MomentsAnyPointsXld(string mode, double area, double centerRow, double centerCol, int p, int q)
	{
		IntPtr proc = JlNativeApi.PreCall(1588);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, area);
		JlNativeApi.StoreD(proc, 2, centerRow);
		JlNativeApi.StoreD(proc, 3, centerCol);
		JlNativeApi.StoreI(proc, 4, p);
		JlNativeApi.StoreI(proc, 5, q);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Anisometry of contours or polygons treated as point clouds.
	/// </summary>
	/// <returns>Anisometry of the contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：点集口径的长细度（anisometry，等价椭圆长半轴/短半轴 ra/rb），每条轮廓一个 DOUBLE 值。</para>
	///   <para><b>约束与前提</b>：轮廓点近似各向同性（ra≈rb）时该比值不稳定 [待实测：rb→0 时的返回]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要 ra、rb、phi 全套参数用 EllipticAxisPointsXld；区域口径用 EccentricityXld。</para>
	///   <para><b>参数取向</b>：无参。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30, 40 }, new double[] { 10, 25, 12, 30 });
	///   JlTuple anisometry = c.EccentricityPointsXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple EccentricityPointsXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1589);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Parameters of the equivalent ellipse of contours or polygons treated as point clouds.
	/// </summary>
	/// <param name="rb">Minor radius.</param>
	/// <param name="phi">Angle between the major axis and the column axis (radians).</param>
	/// <returns>Major radius.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：点集口径的等价椭圆参数——返回长半轴 ra，out 出短半轴 rb 与主轴方向 phi（弧度）。</para>
	///   <para><b>约束与前提</b>：由点集二阶矩导出，开口轮廓同样按点集处理；ra/rb/phi 三者对每条轮廓各给一组值。</para>
	///   <para><b>与相邻算子的取舍</b>：只要长细度用 EccentricityPointsXld；区域口径用 EllipticAxisXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：返回与 out 均为 DOUBLE 元组。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30, 40 }, new double[] { 10, 25, 12, 30 });
	///   JlTuple ra = c.EllipticAxisPointsXld(out JlTuple rb, out JlTuple phi);
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple EllipticAxisPointsXld(out JlTuple rb, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1590);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rb);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Parameters of the equivalent ellipse of contours or polygons treated as point clouds.
	/// </summary>
	/// <param name="rb">Minor radius.</param>
	/// <param name="phi">Angle between the major axis and the column axis (radians).</param>
	/// <returns>Major radius.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：EllipticAxisPointsXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1590），差异仅是结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30, 40 }, new double[] { 10, 25, 12, 30 });
	///   double ra = c.EllipticAxisPointsXld(out double rb, out double phi);
	///   </code>
	///   <para><b>资源与坑</b>：返回标量；本容器需 Dispose。</para>
	/// </remarks>
	public double EllipticAxisPointsXld(out double rb, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1590);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out rb);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Calculate the orientation of contours or polygons treated as point clouds.
	/// </summary>
	/// <returns>Orientation of the contours or polygons (radians).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：点集口径的主方向角（由二阶矩导出，弧度），每条轮廓一个 DOUBLE 值。</para>
	///   <para><b>约束与前提</b>：各向同性的点集方向不定，值抖动大 [待实测：退化时是否返回 0]。</para>
	///   <para><b>与相邻算子的取舍</b>：区域口径方向用 OrientationXld；要主轴+半轴一起取用 EllipticAxisPointsXld。</para>
	///   <para><b>参数取向</b>：无参。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30, 40 }, new double[] { 10, 25, 12, 30 });
	///   JlTuple phi = c.OrientationPointsXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple OrientationPointsXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1591);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Geometric moments M20@f$M_{20}$, M02@f$M_{02}$, and M11@f$M_{11}$ of contours or polygons treated as point clouds.
	/// </summary>
	/// <param name="m20">Second order moment along the row axis.</param>
	/// <param name="m02">Second order moment along the column axis.</param>
	/// <returns>Mixed second order moment.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：点集口径的二阶矩：返回混合矩 m11，out 出行向 m20 与列向 m02（均 DOUBLE 元组）。</para>
	///   <para><b>约束与前提</b>：与轮廓方向无关，只取决于点的分布；每条轮廓给一组值。</para>
	///   <para><b>与相邻算子的取舍</b>：任意阶用 MomentsAnyPointsXld；区域口径用 MomentsXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30, 40 }, new double[] { 10, 25, 12, 30 });
	///   JlTuple m11 = c.MomentsPointsXld(out JlTuple m20, out JlTuple m02);
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple MomentsPointsXld(out JlTuple m20, out JlTuple m02)
	{
		IntPtr proc = JlNativeApi.PreCall(1592);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out m20);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out m02);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Geometric moments M20@f$M_{20}$, M02@f$M_{02}$, and M11@f$M_{11}$ of contours or polygons treated as point clouds.
	/// </summary>
	/// <param name="m20">Second order moment along the row axis.</param>
	/// <param name="m02">Second order moment along the column axis.</param>
	/// <returns>Mixed second order moment.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：MomentsPointsXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1592），差异仅是结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30, 40 }, new double[] { 10, 25, 12, 30 });
	///   double m11 = c.MomentsPointsXld(out double m20, out double m02);
	///   </code>
	///   <para><b>资源与坑</b>：返回标量；本容器需 Dispose。</para>
	/// </remarks>
	public double MomentsPointsXld(out double m20, out double m02)
	{
		IntPtr proc = JlNativeApi.PreCall(1592);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out m20);
		err = JlNativeApi.LoadD(proc, 2, err, out m02);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Area and center of gravity (centroid) of contours and polygons treated as point clouds.
	/// </summary>
	/// <param name="row">Row coordinate of the centroid.</param>
	/// <param name="column">Column coordinate of the centroid.</param>
	/// <returns>Area of the point cloud.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把轮廓当作点集求"面积与质心"——这里的 area 是点集的"面积"，按点云口径等于每条轮廓的点数 [待实测]，不是闭合区域几何面积；row/column 为点集的算术质心。</para>
	///   <para><b>约束与前提</b>：对开放轮廓同样成立（这正是它与 AreaCenterXld 的关键差异，后者需闭合并按方向求带符号面积）。</para>
	///   <para><b>与相邻算子的取舍</b>：要闭合区域的几何面积用 AreaCenterXld；要按点数排序/筛选用本方法。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：返回 DOUBLE 元组（每条轮廓一个点数），out row/column 为 DOUBLE 元组。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   JlTuple nPoints = c.AreaCenterPointsXld(out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple AreaCenterPointsXld(out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1593);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Area and center of gravity (centroid) of contours and polygons treated as point clouds.
	/// </summary>
	/// <param name="row">Row coordinate of the centroid.</param>
	/// <param name="column">Column coordinate of the centroid.</param>
	/// <returns>Area of the point cloud.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：AreaCenterPointsXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1593），差异仅是结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   double nPoints = c.AreaCenterPointsXld(out double row, out double column);
	///   </code>
	///   <para><b>资源与坑</b>：返回标量；本容器需 Dispose。</para>
	/// </remarks>
	public double AreaCenterPointsXld(out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1593);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row);
		err = JlNativeApi.LoadD(proc, 2, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Test XLD contours or polygons for self intersection.
	/// </summary>
	/// <param name="closeXLD">Should the input contours or polygons be closed first? Default: "true"</param>
	/// <returns>1 for contours or polygons with self intersection and 0 otherwise.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条判断轮廓/多边形是否自相交，返回 INTEGER 元组（1 自交、0 否）。closeXLD 指定检测前是否先虚拟闭合开放轮廓。</para>
	///   <para><b>约束与前提</b>："true" 会把首末点视为相连再判交，结果对开放轮廓更保守；取值 "true"/"false"。</para>
	///   <para><b>与相邻算子的取舍</b>：判闭合用 TestClosedXld；本方法判的是几何自交（如打结轮廓）。</para>
	///   <para><b>参数取向</b>：closeXLD 以 StoreS 传入；结果按 INTEGER 装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 30, 10, 30 }, new double[] { 10, 30, 30, 10 });
	///   JlTuple selfInt = c.TestSelfIntersectionXld("true");
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple TestSelfIntersectionXld(string closeXLD)
	{
		IntPtr proc = JlNativeApi.PreCall(1594);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, closeXLD);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Choose all contours or polygons containing a given point.
	/// </summary>
	/// <param name="row">Line coordinate of the test point. Default: 100.0</param>
	/// <param name="column">Column coordinate of the test point. Default: 100.0</param>
	/// <returns>All contours or polygons containing the test point.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：从容器中选出"经过给定点"的轮廓子集，返回新容器；不改动本容器。</para>
	///   <para><b>约束与前提</b>：这里的 row/column 是每个轮廓各给一组测试点坐标（元组长度一般应与轮廓数一致，逐条配对）[待实测：长度不匹配时截断或报错]。</para>
	///   <para><b>与相邻算子的取舍</b>：判"点是否被闭合轮廓包含"用 TestXldPoint（内外关系）；本方法是"轮廓是否恰好经过该点"（命中轮廓上的点）。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：row/column 以 Store 传入、调用后 UnpinTuple。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD hit = contours.SelectXldPoint(new double[] { 100 }, new double[] { 100 });
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，选中的轮廓与本容器共享底层数据；都要 Dispose。</para>
	/// </remarks>
	public JlXLD SelectXldPoint(JlTuple row, JlTuple column)
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
	///   Choose all contours or polygons containing a given point.
	/// </summary>
	/// <param name="row">Line coordinate of the test point. Default: 100.0</param>
	/// <param name="column">Column coordinate of the test point. Default: 100.0</param>
	/// <returns>All contours or polygons containing the test point.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：SelectXldPoint(JlTuple, JlTuple) 的标量重载，全容器使用同一个测试点。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1595），差异仅是 row/column 以 StoreD 标量直传；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD hit = contours.SelectXldPoint(100.0, 100.0);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose。</para>
	/// </remarks>
	public JlXLD SelectXldPoint(double row, double column)
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
	///   Test whether one or more contours or polygons enclose the given point(s).
	/// </summary>
	/// <param name="row">Row coordinates of the points to be tested.</param>
	/// <param name="column">Column coordinates of the points to be tested.</param>
	/// <returns>Tuple with Boolean numbers.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：判断给定点是否被闭合轮廓/多边形"包围"（内外关系），返回 INTEGER 布尔元组（1 在内、0 在外）。</para>
	///   <para><b>约束与前提</b>：只对闭合轮廓有意义，开放轮廓的包围判定不可靠；结果与"轮廓-点"配对顺序相关，元组排列规则 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：判"轮廓是否经过该点"用 SelectXldPoint；本方法判"点在轮廓内/外"。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：可一次给多个测试点 row/column，Store 后 UnpinTuple。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont poly = new JlXLDCont(new double[] { 0, 0, 50, 50, 0 }, new double[] { 0, 50, 50, 0, 0 });
	///   JlTuple inside = poly.TestXldPoint(new double[] { 25, 100 }, new double[] { 25, 100 });
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple TestXldPoint(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1596);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Test whether one or more contours or polygons enclose the given point(s).
	/// </summary>
	/// <param name="row">Row coordinates of the points to be tested.</param>
	/// <param name="column">Column coordinates of the points to be tested.</param>
	/// <returns>Tuple with Boolean numbers.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：TestXldPoint(JlTuple, JlTuple) 的标量重载，单个测试点、返回单个 int 判定。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1596），差异仅是 row/column 以 StoreD 直传、结果经 LoadI 以 int 取回；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont poly = new JlXLDCont(new double[] { 0, 0, 50, 50, 0 }, new double[] { 0, 50, 50, 0, 0 });
	///   int inside = poly.TestXldPoint(25.0, 25.0);
	///   </code>
	///   <para><b>资源与坑</b>：返回标量；本容器需 Dispose。</para>
	/// </remarks>
	public int TestXldPoint(double row, double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1596);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Select contours or polygons using shape features.
	/// </summary>
	/// <param name="features">Shape features to be checked. Default: "area"</param>
	/// <param name="operation">Operation type between the individual features. Default: "and"</param>
	/// <param name="min">Lower limits of the features or 'min'. Default: 150.0</param>
	/// <param name="max">Upper limits of the features or 'max'. Default: 99999.0</param>
	/// <returns>Contours or polygons fulfilling the condition(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：按形状特征值区间筛选轮廓，返回满足条件的新容器；不改动本容器。</para>
	///   <para><b>约束与前提</b>：可用特征名清单与 open 轮廓下各特征的口径 [待实测]；min/max 可用字符串 'min'/'max' 表示不设下界/上界。</para>
	///   <para><b>与相邻算子的取舍</b>：按序号选取用 SelectObj，受上游输出顺序影响；本方法按特征值选取，顺序变化不致配方静默错位，优先用于稳定筛选。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：features 与 min/max 逐特征配对，operation 取 "and"/"or" 组合多特征条件；各元组 Store 后 UnpinTuple。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD big = contours.SelectShapeXld(new string[] { "area" }, "and",
	///     new double[] { 150 }, new double[] { 99999 });
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，选中轮廓共享底层数据；都要 Dispose。</para>
	/// </remarks>
	public JlXLD SelectShapeXld(JlTuple features, string operation, JlTuple min, JlTuple max)
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
	///   Select contours or polygons using shape features.
	/// </summary>
	/// <param name="features">Shape features to be checked. Default: "area"</param>
	/// <param name="operation">Operation type between the individual features. Default: "and"</param>
	/// <param name="min">Lower limits of the features or 'min'. Default: 150.0</param>
	/// <param name="max">Upper limits of the features or 'max'. Default: 99999.0</param>
	/// <returns>Contours or polygons fulfilling the condition(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：SelectShapeXld 主实现的标量重载，单特征、单一区间。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1597），差异仅是 features/min/max 以 StoreS/StoreD 标量直传；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD big = contours.SelectShapeXld("area", "and", 150.0, 99999.0);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose。</para>
	/// </remarks>
	public JlXLD SelectShapeXld(string features, string operation, double min, double max)
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
	///   Calculate the orientation of contours or polygons.
	/// </summary>
	/// <returns>Orientation of the contours or polygons (radians).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：按闭合轮廓所围区域求主方向角（弧度，DOUBLE 元组，每条轮廓一个值）。</para>
	///   <para><b>约束与前提</b>：区域口径由所围面积的矩导出，需闭合轮廓；开放轮廓按首末点相连闭合处理 [待实测]。各向同性区域方向不定。</para>
	///   <para><b>与相邻算子的取舍</b>：想按"点集"口径（与是否闭合无关）求方向用 OrientationPointsXld。</para>
	///   <para><b>参数取向</b>：无参。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont poly = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple phi = poly.OrientationXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅元组结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple OrientationXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1598);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape features derived from the ellipse parameters of contours or polygons.
	/// </summary>
	/// <param name="bulkiness">Bulkiness of the contours or polygons.</param>
	/// <param name="structureFactor">Structure factor of the contours or polygons.</param>
	/// <returns>Anisometry of the contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：由等价椭圆参数导出的形状因子组（原生 id 1599）：返回 anisometry（长细度），out 给出 bulkiness（丰度）与 structureFactor（结构因子），每条轮廓各一组 DOUBLE 值。</para>
	///   <para><b>约束与前提</b>：三个量由等价椭圆半轴 ra/rb 导出（见 EllipticAxisXld，id 1604）；三者的确切公式与 rb 趋于 0 时的取值从包装层不可判 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要长细度一个量用 EccentricityPointsXld（id 1589，点集口径）；要 ra/rb/phi 原始椭圆参数用 EllipticAxisXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：返回与两个 out 均按 DOUBLE 装载，长度等于容器内轮廓数。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple anisometry = c.EccentricityXld(out JlTuple bulkiness, out JlTuple structureFactor);
	///   </code>
	///   <para><b>资源与坑</b>：仅产生 JlTuple 结果（不实现 IDisposable）；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple EccentricityXld(out JlTuple bulkiness, out JlTuple structureFactor)
	{
		IntPtr proc = JlNativeApi.PreCall(1599);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out bulkiness);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out structureFactor);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape features derived from the ellipse parameters of contours or polygons.
	/// </summary>
	/// <param name="bulkiness">Bulkiness of the contours or polygons.</param>
	/// <param name="structureFactor">Structure factor of the contours or polygons.</param>
	/// <returns>Anisometry of the contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：EccentricityXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1599），差异仅是三结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   double anisometry = c.EccentricityXld(out double bulkiness, out double structureFactor);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public double EccentricityXld(out double bulkiness, out double structureFactor)
	{
		IntPtr proc = JlNativeApi.PreCall(1599);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out bulkiness);
		err = JlNativeApi.LoadD(proc, 2, err, out structureFactor);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Shape factor for the compactness of contours or polygons.
	/// </summary>
	/// <returns>Compactness of the input contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：紧凑度因子（原生 id 1600），衡量轮廓接近圆程度的纯数，每条轮廓一个 DOUBLE 值。</para>
	///   <para><b>约束与前提</b>：由所围面积与边界长度导出，通常越接近 1 越圆 [待实测：精确公式、定义域及开放轮廓下的行为]。</para>
	///   <para><b>与相邻算子的取舍</b>：判"接近圆"还可看 CircularityXld（id 1603，另一种圆度口径，具体公式 [待实测]）与 ConvexityXld（凹陷程度）；三者口径不同，按检测目标选用，不要互换阈值。</para>
	///   <para><b>参数取向</b>：无参；结果按 DOUBLE 元组装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple compactness = c.CompactnessXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple CompactnessXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1600);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Maximum distance between two contour or polygon points.
	/// </summary>
	/// <param name="row1">Row coordinate of the first extreme point of the contours or polygons.</param>
	/// <param name="column1">Column coordinate of the first extreme point of the contours or polygons.</param>
	/// <param name="row2">Row coordinate of the second extreme point of the contour or polygons.</param>
	/// <param name="column2">Column coordinate of the second extreme point of the contours or polygons.</param>
	/// <param name="diameter">Distance of the two extreme points of the contours or polygons.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条轮廓求点集最大间距（原生 id 1601）：out 出两个极值点的 row/column 与二者距离 diameter，共 5 组 DOUBLE 元组，即轮廓的"直径"及取到直径的端点。</para>
	///   <para><b>约束与前提</b>：极值在轮廓顶点集中搜索，顶点稀疏采样的长轮廓会低估真实跨度；对称/圆形轮廓存在多组等价极值点对，具体返回哪一对不保证稳定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只用最大跨度的数值做筛选时用 SelectShapeXld 按特征过滤，不必回读极值点坐标；要轴向包围盒尺寸用 HeightWidthRatioXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：每条轮廓给一组 5 值，装载顺序 row1、column1、row2、column2、diameter。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 30 }, new double[] { 0, 40 });
	///   c.DiameterXld(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2, out JlTuple diameter);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public void DiameterXld(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2, out JlTuple diameter)
	{
		IntPtr proc = JlNativeApi.PreCall(1601);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row1);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out row2);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out column2);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out diameter);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Maximum distance between two contour or polygon points.
	/// </summary>
	/// <param name="row1">Row coordinate of the first extreme point of the contours or polygons.</param>
	/// <param name="column1">Column coordinate of the first extreme point of the contours or polygons.</param>
	/// <param name="row2">Row coordinate of the second extreme point of the contour or polygons.</param>
	/// <param name="column2">Column coordinate of the second extreme point of the contours or polygons.</param>
	/// <param name="diameter">Distance of the two extreme points of the contours or polygons.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：DiameterXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1601），差异仅是 5 个结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 30 }, new double[] { 0, 40 });
	///   c.DiameterXld(out double row1, out double column1, out double row2, out double column2, out double diameter);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public void DiameterXld(out double row1, out double column1, out double row2, out double column2, out double diameter)
	{
		IntPtr proc = JlNativeApi.PreCall(1601);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row1);
		err = JlNativeApi.LoadD(proc, 1, err, out column1);
		err = JlNativeApi.LoadD(proc, 2, err, out row2);
		err = JlNativeApi.LoadD(proc, 3, err, out column2);
		err = JlNativeApi.LoadD(proc, 4, err, out diameter);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Shape factor for the convexity of contours or polygons.
	/// </summary>
	/// <returns>Convexity of the input contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：凸度因子（原生 id 1602），衡量轮廓偏离凸形的程度，每条轮廓一个 DOUBLE 值；无凹陷时最大，凹口越深值越小 [待实测：精确公式（是否面积比）与值域]。</para>
	///   <para><b>约束与前提</b>：依赖所围面积与凸包面积之比口径，开放轮廓的处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：要凸包轮廓本身（后续继续做几何运算）用 ShapeTransXld("convex")；只要"凸不凸"这个数用本方法。</para>
	///   <para><b>参数取向</b>：无参；结果按 DOUBLE 元组装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple convexity = c.ConvexityXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple ConvexityXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1602);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape factor for the circularity (similarity to a circle) of contours or polygons.
	/// </summary>
	/// <returns>Roundness of the input contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：圆度因子（原生 id 1603），衡量轮廓与圆的相似程度，每条轮廓一个 DOUBLE 值；轮廓越圆值越大 [待实测：精确公式与值域]。</para>
	///   <para><b>约束与前提</b>：对单个噪声/缺失顶点敏感，轮廓断续会显著拉低圆度值；开放轮廓口径 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：区分"长细"用 EccentricityXld 的 anisometry（椭圆轴比），区分"凹凸"用 ConvexityXld；圆度只反映与圆的偏离，三者不要混用阈值。</para>
	///   <para><b>参数取向</b>：无参；结果按 DOUBLE 元组装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple circularity = c.CircularityXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple CircularityXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1603);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Parameters of the equivalent ellipse of contours or polygons.
	/// </summary>
	/// <param name="rb">Minor radius.</param>
	/// <param name="phi">Angle between the major axis and the x axis (radians).</param>
	/// <returns>Major radius.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：等价椭圆参数（原生 id 1604）：返回长半轴 ra（DOUBLE 元组），out 短半轴 rb 与主轴方向 phi——phi 为弧度制，是主轴与 x 轴（column 轴）的夹角。</para>
	///   <para><b>约束与前提</b>：各向同性轮廓（近圆）时主轴方向不定，phi 抖动大；ra/rb 与 phi 每条轮廓各一组值。phi 的取值区间（如 (-π,π]）[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：点集口径的同名算子是 EllipticAxisPointsXld（id 1590，明确按 point cloud 计算）；只要轴比导出的形状因子用 EccentricityXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：返回与 out 均按 DOUBLE 装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple ra = c.EllipticAxisXld(out JlTuple rb, out JlTuple phi);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple EllipticAxisXld(out JlTuple rb, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1604);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rb);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Parameters of the equivalent ellipse of contours or polygons.
	/// </summary>
	/// <param name="rb">Minor radius.</param>
	/// <param name="phi">Angle between the major axis and the x axis (radians).</param>
	/// <returns>Major radius.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：EllipticAxisXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1604），差异仅是三结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   double ra = c.EllipticAxisXld(out double rb, out double phi);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public double EllipticAxisXld(out double rb, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1604);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out rb);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Smallest enclosing rectangle with arbitrary orientation of contours or polygons.
	/// </summary>
	/// <param name="row">Row coordinate of the center point of the enclosing rectangle.</param>
	/// <param name="column">Column coordinate of the center point of the enclosing rectangle.</param>
	/// <param name="phi">Orientation of the enclosing rectangle (arc measure)</param>
	/// <param name="length1">First radius (half length) of the enclosing rectangle.</param>
	/// <param name="length2">Second radius (half width) of the enclosing rectangle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条轮廓求任意方向最小面积外接矩形（原生 id 1605）：out 中心 row/column、方向 phi（弧度）、两条半边长 length1/length2——注意是半长半宽，整边长要乘 2。</para>
	///   <para><b>约束与前提</b>：拟合对象是轮廓的 double 顶点点集（本库轮廓即 (row,col) 浮点点列，见 JlXLDCont(JlTuple, JlTuple)），开放轮廓同样按顶点集处理；phi 角度起点与 length1/length2 谁长谁短的约定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：JlRegion.SmallestRectangle2 拟合的是像素区域，结果带像素边界量化；需要亚像素尺寸（如测量零件长宽）用本方法，别拿区域拟合结果当亚像素精度。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：5 个 out 均按 DOUBLE 装载，每条轮廓一组值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   c.SmallestRectangle2Xld(out JlTuple row, out JlTuple column, out JlTuple phi, out JlTuple length1, out JlTuple length2);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public void SmallestRectangle2Xld(out JlTuple row, out JlTuple column, out JlTuple phi, out JlTuple length1, out JlTuple length2)
	{
		IntPtr proc = JlNativeApi.PreCall(1605);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out length1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out length2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Smallest enclosing rectangle with arbitrary orientation of contours or polygons.
	/// </summary>
	/// <param name="row">Row coordinate of the center point of the enclosing rectangle.</param>
	/// <param name="column">Column coordinate of the center point of the enclosing rectangle.</param>
	/// <param name="phi">Orientation of the enclosing rectangle (arc measure)</param>
	/// <param name="length1">First radius (half length) of the enclosing rectangle.</param>
	/// <param name="length2">Second radius (half width) of the enclosing rectangle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：SmallestRectangle2Xld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1605），差异仅是 5 个结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   c.SmallestRectangle2Xld(out double row, out double column, out double phi, out double length1, out double length2);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public void SmallestRectangle2Xld(out double row, out double column, out double phi, out double length1, out double length2)
	{
		IntPtr proc = JlNativeApi.PreCall(1605);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		err = JlNativeApi.LoadD(proc, 3, err, out length1);
		err = JlNativeApi.LoadD(proc, 4, err, out length2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Enclosing rectangle parallel to the coordinate axes of contours or polygons.
	/// </summary>
	/// <param name="row1">Row coordinate of upper left corner point of the enclosing rectangle.</param>
	/// <param name="column1">Column coordinate of upper left corner point of the enclosing rectangle.</param>
	/// <param name="row2">Row coordinate of lower right corner point of the enclosing rectangle.</param>
	/// <param name="column2">Column coordinate of lower right corner point of the enclosing rectangle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条轮廓求轴向（平行于坐标轴）外接矩形（原生 id 1606）：out 左上角 (row1, column1) 与右下角 (row2, column2)，4 组 DOUBLE 元组，坐标即顶点集的行列极值。</para>
	///   <para><b>约束与前提</b>：row = y 向下为正、column = x 向右为正，故恒有 row1≤row2、column1≤column2 [待实测：退化轮廓（单点/空）时是否仍满足]；不含旋转信息，斜放物体请用 SmallestRectangle2Xld。</para>
	///   <para><b>与相邻算子的取舍</b>：JlRegion.SmallestRectangle1 给的是像素边界（其 out int 重载，整数行列极值），本方法给 double 顶点极值——做亚像素测量用本方法；两套口径的数值有系统性差异，不要互相换算。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：4 个 out 均按 DOUBLE 装载，每条轮廓一组值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   c.SmallestRectangle1Xld(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。由 row2-row1、column2-column1 直接得高宽，无须再调 HeightWidthRatioXld。</para>
	/// </remarks>
	public void SmallestRectangle1Xld(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1606);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row1);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out row2);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Enclosing rectangle parallel to the coordinate axes of contours or polygons.
	/// </summary>
	/// <param name="row1">Row coordinate of upper left corner point of the enclosing rectangle.</param>
	/// <param name="column1">Column coordinate of upper left corner point of the enclosing rectangle.</param>
	/// <param name="row2">Row coordinate of lower right corner point of the enclosing rectangle.</param>
	/// <param name="column2">Column coordinate of lower right corner point of the enclosing rectangle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：SmallestRectangle1Xld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1606），差异仅是 4 个结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   c.SmallestRectangle1Xld(out double row1, out double column1, out double row2, out double column2);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public void SmallestRectangle1Xld(out double row1, out double column1, out double row2, out double column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1606);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row1);
		err = JlNativeApi.LoadD(proc, 1, err, out column1);
		err = JlNativeApi.LoadD(proc, 2, err, out row2);
		err = JlNativeApi.LoadD(proc, 3, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Smallest enclosing circle of contours or polygons.
	/// </summary>
	/// <param name="row">Row coordinate of the center of the enclosing circle.</param>
	/// <param name="column">Column coordinate of the center of the enclosing circle.</param>
	/// <param name="radius">Radius of the enclosing circle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条轮廓求最小外接圆（原生 id 1607）：out 圆心 row/column 与半径 radius（像素），3 组 DOUBLE 元组，圆须覆盖该轮廓的全部顶点。</para>
	///   <para><b>约束与前提</b>：由顶点集决定，半径对个别离群顶点非常敏感——轮廓上多出一个飞点会直接撑大半径；先剔噪再拟合。</para>
	///   <para><b>与相邻算子的取舍</b>：JlRegion.SmallestCircle 拟合的是像素区域的外接圆，口径含像素边界；亚像素顶点集的外接圆用本方法。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：3 个 out 均按 DOUBLE 装载，每条轮廓一组值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   c.SmallestCircleXld(out JlTuple row, out JlTuple column, out JlTuple radius);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public void SmallestCircleXld(out JlTuple row, out JlTuple column, out JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1607);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out radius);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Smallest enclosing circle of contours or polygons.
	/// </summary>
	/// <param name="row">Row coordinate of the center of the enclosing circle.</param>
	/// <param name="column">Column coordinate of the center of the enclosing circle.</param>
	/// <param name="radius">Radius of the enclosing circle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>：SmallestCircleXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1607），差异仅是 3 个结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   c.SmallestCircleXld(out double row, out double column, out double radius);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public void SmallestCircleXld(out double row, out double column, out double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1607);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		err = JlNativeApi.LoadD(proc, 2, err, out radius);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Transform the shape of contours or polygons.
	/// </summary>
	/// <param name="type">Type of transformation. Default: "convex"</param>
	/// <returns>Transformed contours respectively polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：形状变换生成新轮廓容器（原生 id 1608）：type="convex" 时对每条输入轮廓生成凸包轮廓，返回新 JlXLD，本容器不改动。</para>
	///   <para><b>约束与前提</b>：type 确证的合法值目前只有 "convex"（凸包），是否还有其它取值 [待实测]；凸包由顶点集导出，开放轮廓的凸包是否自动闭合 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要凸度数值用 ConvexityXld（id 1602），要能继续做几何运算的凸包轮廓用本方法；凸包轮廓面积/矩等须对结果重新计算，原轮廓的量不继承。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   using JlXLD hull = c.ShapeTransXld("convex");
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose，与本容器共享底层数据与否 [待实测]；本容器仍需 Dispose。</para>
	/// </remarks>
	public JlXLD ShapeTransXld(string type)
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
	///   Length of contours or polygons.
	/// </summary>
	/// <returns>Length of the contour or polygon.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：逐条轮廓的折线长度（原生 id 1609），单位像素：相邻顶点欧氏距离之和，返回 DOUBLE 元组。</para>
	///   <para><b>约束与前提</b>：闭合轮廓是否把末点回首点的封口段计入、开放轮廓首尾是否相连 [待实测]；长度取决于顶点密度，经 SegmentContoursXld 直线化后的轮廓长度会小于原采样轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>：区域像素边界长度用 JlRegion.Contlength()，与折线长度口径不同、数值不可互换；要按长度筛选用 SelectShapeXld 或先取本结果再 SelectObj。</para>
	///   <para><b>参数取向</b>：无参；结果按 DOUBLE 元组装载，长度等于轮廓数。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple lengths = c.LengthXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple LengthXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1609);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Arbitrary geometric moments of contours or polygons.
	/// </summary>
	/// <param name="mode">Computation mode. Default: "unnormalized"</param>
	/// <param name="pointOrder">Point order along the boundary. Default: "positive"</param>
	/// <param name="area">Area enclosed by the contour or polygon.</param>
	/// <param name="centerRow">Row coordinate of the centroid.</param>
	/// <param name="centerCol">Column coordinate of the centroid.</param>
	/// <param name="p">First index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <param name="q">Second index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <returns>The computed moments.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：按"所围区域"口径计算轮廓/多边形的任意阶几何矩 M(p,q)（原生 id 1610），返回 DOUBLE 元组；与点集口径的 MomentsAnyPointsXld（id 1588）不同，本算子由闭合边界的面积分导出。</para>
	///   <para><b>约束与前提</b>：area/centerRow/centerCol 应传入本容器 AreaCenterXld 对应输出的值作参考量（三者与 p/q 均为每条轮廓一组值，长度须与轮廓数配对，不匹配时行为 [待实测]）；pointOrder 声明各轮廓顶点序（"positive"/"negative"），方向给错会得到符号错误的矩 [待实测：传错方向是纠正还是报错]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要二阶矩 M20/M02/M11 用 MomentsXld（id 1611，免配参考量）；点集口径用 MomentsAnyPointsXld。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：pointOrder/area/centerRow/centerCol/p/q 逐轮廓成组，Store 钉固定后 UnpinTuple；mode 为标量串。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple area = c.AreaCenterXld(out JlTuple cr, out JlTuple cc, out JlTuple pointOrder);
	///   JlTuple m = c.MomentsAnyXld("unnormalized", pointOrder, area, cr, cc, new int[] { 1 }, new int[] { 1 });
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；AreaCenterXld 与矩计算要作用同一容器，中途改了轮廓必须重算。本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple MomentsAnyXld(string mode, JlTuple pointOrder, JlTuple area, JlTuple centerRow, JlTuple centerCol, JlTuple p, JlTuple q)
	{
		IntPtr proc = JlNativeApi.PreCall(1610);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.Store(proc, 1, pointOrder);
		JlNativeApi.Store(proc, 2, area);
		JlNativeApi.Store(proc, 3, centerRow);
		JlNativeApi.Store(proc, 4, centerCol);
		JlNativeApi.Store(proc, 5, p);
		JlNativeApi.Store(proc, 6, q);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(pointOrder);
		JlNativeApi.UnpinTuple(area);
		JlNativeApi.UnpinTuple(centerRow);
		JlNativeApi.UnpinTuple(centerCol);
		JlNativeApi.UnpinTuple(p);
		JlNativeApi.UnpinTuple(q);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Arbitrary geometric moments of contours or polygons.
	/// </summary>
	/// <param name="mode">Computation mode. Default: "unnormalized"</param>
	/// <param name="pointOrder">Point order along the boundary. Default: "positive"</param>
	/// <param name="area">Area enclosed by the contour or polygon.</param>
	/// <param name="centerRow">Row coordinate of the centroid.</param>
	/// <param name="centerCol">Column coordinate of the centroid.</param>
	/// <param name="p">First index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <param name="q">Second index of the desired moments M[P,Q]@f$M_{p,q}$. Default: 1</param>
	/// <returns>The computed moments.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：MomentsAnyXld(JlTuple...) 的标量重载，单条轮廓求单值矩。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1610），area/centerRow/centerCol 以 StoreD、p/q 以 StoreI、pointOrder 以 StoreS 标量直传，结果经 LoadD 以 double 取回；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   double area = c.AreaCenterXld(out double cr, out double cc, out string pointOrder);
	///   double m11 = c.MomentsAnyXld("unnormalized", pointOrder, area, cr, cc, 1, 1);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public double MomentsAnyXld(string mode, string pointOrder, double area, double centerRow, double centerCol, int p, int q)
	{
		IntPtr proc = JlNativeApi.PreCall(1610);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreS(proc, 1, pointOrder);
		JlNativeApi.StoreD(proc, 2, area);
		JlNativeApi.StoreD(proc, 3, centerRow);
		JlNativeApi.StoreD(proc, 4, centerCol);
		JlNativeApi.StoreI(proc, 5, p);
		JlNativeApi.StoreI(proc, 6, q);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Geometric moments M20@f$M_{20}$, M02@f$M_{02}$, and M11@f$M_{11}$ of contours or polygons.
	/// </summary>
	/// <param name="m20">Second order moment along the row axis.</param>
	/// <param name="m02">Second order moment along the column axis.</param>
	/// <returns>Mixed second order moment.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：所围区域口径的二阶几何矩（原生 id 1611）：返回混合矩 m11，out 行向 m20、列向 m02，均为 DOUBLE 元组、每条轮廓一组；等价椭圆参数（EllipticAxisXld）即由这三个二阶矩换算而来 [待实测]。</para>
	///   <para><b>约束与前提</b>：矩按闭合边界面积分计算，开放轮廓的处理 [待实测]；无需外部参考量（区别于 MomentsAnyXld 要传 area/质心/pointOrder）。</para>
	///   <para><b>与相邻算子的取舍</b>：任意阶 (p,q) 用 MomentsAnyXld；点集口径（与闭合与否无关）用 MomentsPointsXld（id 1592）。两者数值对同一轮廓不相等，别混用阈值。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：3 个结果均按 DOUBLE 装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple m11 = c.MomentsXld(out JlTuple m20, out JlTuple m02);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple MomentsXld(out JlTuple m20, out JlTuple m02)
	{
		IntPtr proc = JlNativeApi.PreCall(1611);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out m20);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out m02);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Geometric moments M20@f$M_{20}$, M02@f$M_{02}$, and M11@f$M_{11}$ of contours or polygons.
	/// </summary>
	/// <param name="m20">Second order moment along the row axis.</param>
	/// <param name="m02">Second order moment along the column axis.</param>
	/// <returns>Mixed second order moment.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：MomentsXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1611），差异仅是三结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   double m11 = c.MomentsXld(out double m20, out double m02);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public double MomentsXld(out double m20, out double m02)
	{
		IntPtr proc = JlNativeApi.PreCall(1611);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out m20);
		err = JlNativeApi.LoadD(proc, 2, err, out m02);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Area and center of gravity (centroid) of contours and polygons.
	/// </summary>
	/// <param name="row">Row coordinate of the centroid.</param>
	/// <param name="column">Column coordinate of the centroid.</param>
	/// <param name="pointOrder">point order along the boundary ('positive'/'negative').</param>
	/// <returns>Area enclosed by the contour or polygon.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：闭合轮廓所围面积与质心（原生 id 1612）：返回 area（DOUBLE 元组），out 质心 row/column 与 pointOrder（字符串元组，"positive"/"negative"，报告该轮廓实际的顶点绕行方向）。</para>
	///   <para><b>约束与前提</b>：面积按闭合边界的积分口径计算 [待实测：具体公式]；area 是否随绕行方向带符号、还是恒为正而把方向单列在 pointOrder，从装载代码判不了 [待实测]——把 area 用于面积差/叠加运算前务必先实测。开放轮廓的处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：点集口径的"面积"是点数（AreaCenterPointsXld，id 1593），像素区域面积（JlRegion.AreaCenter）又是另一套口径；三者定义互不相同，不可互换。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：前 3 个输出按 DOUBLE 装载，pointOrder 经无类型标注的 LoadNew 按字符串元组取回；pointOrder 可直接回传给 MomentsAnyXld。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple area = c.AreaCenterXld(out JlTuple row, out JlTuple column, out JlTuple pointOrder);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。形态学/变换之后轮廓已变，面积质心必须重取。</para>
	/// </remarks>
	public JlTuple AreaCenterXld(out JlTuple row, out JlTuple column, out JlTuple pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(1612);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 3, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Area and center of gravity (centroid) of contours and polygons.
	/// </summary>
	/// <param name="row">Row coordinate of the centroid.</param>
	/// <param name="column">Column coordinate of the centroid.</param>
	/// <param name="pointOrder">point order along the boundary ('positive'/'negative').</param>
	/// <returns>Area enclosed by the contour or polygon.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：AreaCenterXld(out JlTuple,...) 的标量重载，单条轮廓取单值；pointOrder 经 LoadS 以 string 取回。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 1612），差异仅是三数值经 LoadD、pointOrder 经 LoadS 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   double area = c.AreaCenterXld(out double row, out double column, out string pointOrder);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD/LoadS 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public double AreaCenterXld(out double row, out double column, out string pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(1612);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row);
		err = JlNativeApi.LoadD(proc, 2, err, out column);
		err = JlNativeApi.LoadS(proc, 3, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}





	/// <summary>
	///   Compute the width, height, and aspect ratio of the enclosing rectangle parallel to the coordinate axes of contours or polygons.
	/// </summary>
	/// <param name="width">Width of the enclosing rectangle.</param>
	/// <param name="ratio">Aspect ratio of the enclosing rectangle.</param>
	/// <returns>Height of the enclosing rectangle.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：轴向外接矩形的高、宽与长宽比（原生 id 2002）：返回 height（row 方向边长），out width（column 方向边长）与 ratio（长宽比 [待实测：是 width/height 还是 height/width]），均 DOUBLE 元组。</para>
	///   <para><b>约束与前提</b>：量的是平行于坐标轴的包围盒——物体斜放时高宽被外扩的包围盒放大，此时应改用 SmallestRectangle2Xld 的 length1/length2。</para>
	///   <para><b>与相邻算子的取舍</b>：SmallestRectangle1Xld（id 1606）给角点坐标，本方法直接给尺寸与比值，省一次减法；区域口径的同名算子 JlRegion.HeightWidthRatio 按像素数计且其标量重载高/宽为 int，精度与定义都不同。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：3 个结果均按 DOUBLE 装载，每条轮廓一组值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple height = c.HeightWidthRatioXld(out JlTuple width, out JlTuple ratio);
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple HeightWidthRatioXld(out JlTuple width, out JlTuple ratio)
	{
		IntPtr proc = JlNativeApi.PreCall(2002);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out width);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out ratio);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Compute the width, height, and aspect ratio of the enclosing rectangle parallel to the coordinate axes of contours or polygons.
	/// </summary>
	/// <param name="width">Width of the enclosing rectangle.</param>
	/// <param name="ratio">Aspect ratio of the enclosing rectangle.</param>
	/// <returns>Height of the enclosing rectangle.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：HeightWidthRatioXld(out JlTuple,...) 的标量重载，单条轮廓取单值。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 2002），差异仅是三结果经 LoadD 以 double 装载；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   double height = c.HeightWidthRatioXld(out double width, out double ratio);
	///   </code>
	///   <para><b>资源与坑</b>：LoadD 只读回第一个值——容器含多条轮廓时本重载只取第 1 条的结果，其余静默丢弃；多轮廓请改用元组版。本容器需 Dispose。</para>
	/// </remarks>
	public double HeightWidthRatioXld(out double width, out double ratio)
	{
		IntPtr proc = JlNativeApi.PreCall(2002);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out width);
		err = JlNativeApi.LoadD(proc, 2, err, out ratio);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Insert objects into an iconic object tuple.
	/// </summary>
	/// <param name="objectsInsert">Object tuple to insert.</param>
	/// <param name="index">Index to insert objects.</param>
	/// <returns>Extended object tuple.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把 objectsInsert 的全部轮廓插入本容器第 index 个位置之前，返回扩展后的新容器（原生 id 2003）；本容器与 objectsInsert 均不改动。</para>
	///   <para><b>约束与前提</b>：index 直接透传原生、包装层不做基数换算，基数（1 基或 0 基）与越界行为 [待实测]——用 0 试插入时先在实测环境验证，别按 C# 数组习惯假定。两容器元素类型须同为 xld 对象。</para>
	///   <para><b>与相邻算子的取舍</b>：只往末尾接用 ConcatObj（id 569）；按位置覆盖用 ReplaceObj（id 2006）；本方法是"挤开插入"，原元素整体后移。</para>
	///   <para><b>参数取向</b>：index 以 StoreI 写入原生控制参数 0（原生序在图标参数 2 之前）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c1 = new JlXLDCont(new double[] { 10, 20, 30 }, new double[] { 10, 25, 12 });
	///   using JlXLDCont c2 = new JlXLDCont(new double[] { 60, 70, 80 }, new double[] { 60, 75, 62 });
	///   using JlXLD merged = c1.InsertObj(c2, 1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，元素与两输入共享底层轮廓；三者各自 Dispose。</para>
	/// </remarks>
	public JlXLD InsertObj(JlXLD objectsInsert, int index)
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
	///   Remove objects from an iconic object tuple.
	/// </summary>
	/// <param name="index">Indices of the objects to be removed.</param>
	/// <returns>Remaining object tuple.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把 index 指定的轮廓从本容器中剔除，其余按原顺序装入新容器返回（原生 id 2005）；本容器不改动。</para>
	///   <para><b>约束与前提</b>：index 透传原生不换算基数，与 SelectObj（id 572）同族、1 基与否 [待实测]；重复索引与越界索引的行为 [待实测]，调用前用 CountObj 核对轮廓数。</para>
	///   <para><b>与相邻算子的取舍</b>：按内容剔除用 ObjDiff（id 558），不受顺序影响；按序号剔除只在顺序稳定（同一上游、同参数）时可靠，上游 Connection/SegmentContoursXld 输出顺序变化会静默删错轮廓。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：index 可含多个序号，Store 钉固定后 UnpinTuple。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD kept = contours.RemoveObj(new int[] { 1 });
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，保留元素与本容器共享底层轮廓；都要 Dispose。</para>
	/// </remarks>
	public new JlXLD RemoveObj(JlTuple index)
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
	///   Remove objects from an iconic object tuple.
	/// </summary>
	/// <param name="index">Indices of the objects to be removed.</param>
	/// <returns>Remaining object tuple.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：RemoveObj(JlTuple) 的标量重载，只剔除一条轮廓。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 2005），index 以 StoreI 整数直传、无 UnpinTuple；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLD kept = contours.RemoveObj(1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；剔除后剩余轮廓序号整体前移，后续按序号操作要重新对表。</para>
	/// </remarks>
	public new JlXLD RemoveObj(int index)
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
	///   Replaces one or more elements of an iconic object tuple.
	/// </summary>
	/// <param name="objectsReplace">Element(s) to replace.</param>
	/// <param name="index">Index/Indices of elements to be replaced.</param>
	/// <returns>Tuple with replaced elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：用 objectsReplace 中的轮廓按位替换本容器第 index 个元素，返回替换后的新容器（原生 id 2006）；本容器与 objectsReplace 均不改动，元素总数不变。</para>
	///   <para><b>约束与前提</b>：index 透传原生不换算基数，与 SelectObj/RemoveObj 同族，1 基与否 [待实测]；objectsReplace 元素数与 index 数不等时的配对行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：ReplaceObj 是"原位覆盖"，容器长度不变——想在中间加元素用 InsertObj（会挤后移），想删用 RemoveObj；三者的 index 语义互不相同，不要照抄参数。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：index 可含多个序号，Store 钉固定后 UnpinTuple；原生控制参数序把 index 放在图标参数之前。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLDCont patch = new JlXLDCont(new double[] { 60, 70, 80 }, new double[] { 60, 75, 62 });
	///   using JlXLD replaced = contours.ReplaceObj(patch, new int[] { 1 });
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄，未被替换的元素仍与本容器共享底层轮廓；三者各自 Dispose。</para>
	/// </remarks>
	public JlXLD ReplaceObj(JlXLD objectsReplace, JlTuple index)
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
	///   Replaces one or more elements of an iconic object tuple.
	/// </summary>
	/// <param name="objectsReplace">Element(s) to replace.</param>
	/// <param name="index">Index/Indices of elements to be replaced.</param>
	/// <returns>Tuple with replaced elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：ReplaceObj(JlXLD, JlTuple) 的标量重载，按单个序号替换一处。</para>
	///   <para><b>参数取向</b>：同一原生调用（id 2006），index 以 StoreI 整数直传、无 UnpinTuple；语义与坑见元组主重载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(100.0, 200.0);
	///   using JlXLDCont contours = r.GenContourRegionXld("border");
	///   using JlXLDCont patch = new JlXLDCont(new double[] { 60, 70, 80 }, new double[] { 60, 75, 62 });
	///   using JlXLD replaced = contours.ReplaceObj(patch, 1);
	///   </code>
	///   <para><b>资源与坑</b>：返回新句柄需 Dispose；patch 与 contours 也各自 Dispose。</para>
	/// </remarks>
	public JlXLD ReplaceObj(JlXLD objectsReplace, int index)
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

	/// <summary>
	///   Shape factor for the rectangularity of contours or polygons.
	/// </summary>
	/// <returns>Rectangularity of the input contours or polygons.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：矩形度因子（原生 id 2063），衡量轮廓与矩形的相似程度，每条轮廓一个 DOUBLE 值；越接近规则矩形值越大 [待实测：精确公式（与旋转外接矩形面积之比？）与值域]。</para>
	///   <para><b>约束与前提</b>：开放轮廓与噪声顶点下的口径 [待实测]；阈值筛选用的特征名是否即 "rectangularity" 需对照 SelectShapeXld 支持清单 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：要矩形几何参数（中心/方向/边长）用 SmallestRectangle2Xld；要"像不像矩形"这一个数用本方法，省一次拟合再比值的代码。</para>
	///   <para><b>参数取向</b>：无参；结果按 DOUBLE 元组装载。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlXLDCont c = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   JlTuple rectangularity = c.RectangularityXld();
	///   </code>
	///   <para><b>资源与坑</b>：仅 JlTuple 结果；本容器需 Dispose。</para>
	/// </remarks>
	public JlTuple RectangularityXld()
	{
		IntPtr proc = JlNativeApi.PreCall(2063);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}
}
