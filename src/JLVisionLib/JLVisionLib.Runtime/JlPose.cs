using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents a rigid 3D transformation with 7 parameters (3 for the rotation, 3 for the translation, 1 for the representation type).</summary>
[Serializable]
public class JlPose : JlData, ISerializable, ICloneable
{
	private const int FIXEDSIZE = 7;

	/// <summary>Create an uninitialized instance.</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlPose：创建 未初始化 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlPose 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlPose obj = new JlPose();
	///   </code>
	/// </remarks>
	public JlPose()
	{
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlPose 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlPose 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlPose obj = new JlPose();
	///   </code>
	/// </remarks>
	public JlPose(JlTuple tuple)
		: base(tuple)
	{
	}

	internal JlPose(JlData data)
		: base(data)
	{
	}

	internal static int LoadNew(IntPtr proc, int parIndex, JlTupleType type, int err, out JlPose obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var t);
		obj = new JlPose(new JlData(t));
		return err;
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlPose obj)
	{
		return LoadNew(proc, parIndex, JlTupleType.MIXED, err, out obj);
	}

	internal static JlPose[] SplitArray(JlTuple data)
	{
		int num = data.Length / 7;
		JlPose[] array = new JlPose[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = new JlPose(new JlData(data.TupleSelectRange(i * 7, (i + 1) * 7 - 1)));
		}
		return array;
	}

	/// <summary>
	///   Create a 3D pose.
	/// </summary>
	/// <param name="transX">Translation along the x-axis (in [m]). Default: 0.1</param>
	/// <param name="transY">Translation along the y-axis (in [m]). Default: 0.1</param>
	/// <param name="transZ">Translation along the z-axis (in [m]). Default: 0.1</param>
	/// <param name="rotX">Rotation around x-axis or x component of the Rodriguez vector (in [ deg] or without unit). Default: 90.0</param>
	/// <param name="rotY">Rotation around y-axis or y component of the Rodriguez vector (in [ deg] or without unit). Default: 90.0</param>
	/// <param name="rotZ">Rotation around z-axis or z component of the Rodriguez vector (in [ deg] or without unit). Default: 90.0</param>
	/// <param name="orderOfTransform">Order of rotation and translation. Default: "Rp+T"</param>
	/// <param name="orderOfRotation">Meaning of the rotation values. Default: "gba"</param>
	/// <param name="viewOfTransform">View of transformation. Default: "point"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlPose：创建 3D 位姿。</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlPose 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlPose obj = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   </code>
	/// </remarks>
	public JlPose(double transX, double transY, double transZ, double rotX, double rotY, double rotZ, string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1816);
		JlNativeApi.StoreD(proc, 0, transX);
		JlNativeApi.StoreD(proc, 1, transY);
		JlNativeApi.StoreD(proc, 2, transZ);
		JlNativeApi.StoreD(proc, 3, rotX);
		JlNativeApi.StoreD(proc, 4, rotY);
		JlNativeApi.StoreD(proc, 5, rotZ);
		JlNativeApi.StoreS(proc, 6, orderOfTransform);
		JlNativeApi.StoreS(proc, 7, orderOfRotation);
		JlNativeApi.StoreS(proc, 8, viewOfTransform);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializePose();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlPose(SerializationInfo info, StreamingContext context)
	{
		DeserializePose((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把位姿按库自有二进制格式写入流。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现 = <c>SerializePose()</c>（原生 id 1834，取字节缓冲）+ <c>JlSerializationBuffer.WriteToStream</c> 落流；位姿的 7 个分量（3 旋转 + 3 平移 + 1 表示类型码）如何编码进字节由原生侧决定。</para>
	///   <para><b>约束或前提</b>JlPose 底层是 7 元数据（3 旋转 + 3 平移 + 1 表示类型码），序列化是否连表示形式（orderOfTransform/orderOfRotation/viewOfTransform）一起带走、读回后要不要重新 ConvertPoseType，无法由本文件代码判定 [待实测]。JlPose 系（JlData）不实现 IDisposable，没有句柄释放问题。</para>
	///   <para><b>与相邻算子的取舍</b>只要内存字节（如塞进自定义报文）用 <c>SerializePose</c>/<c>DeserializePose</c> 一对；落盘文本给人读用 <c>WritePose</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
	///   {
	///       pose.Serialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>流的生命周期由调用方管理，本方法不关闭传入的流；读回用静态 <c>JlPose.Deserialize(Stream)</c>，它返回新实例。</para>
	/// </remarks>
	public void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializePose(), stream);
	}

	/// <summary>从 <c>Serialize(Stream)</c> 写出的流读出一个新位姿。</summary>
	/// <returns>承载流内容的新 JlPose 实例（非原地改写；JlPose 不实现 IDisposable，无需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现 = 先 <c>new JlPose()</c>（未初始化实例），再 <c>DeserializePose(byte[])</c>（原生 id 1833）覆写自身，读流偏移由 <c>JlSerializationBuffer.ReadFromStream</c> 决定。</para>
	///   <para><b>约束或前提</b>字节必须是本库 <c>Serialize(Stream)</c>/<c>SerializePose()</c> 产出的格式；内容不合法时报错来自原生层 [待实测]。表示形式是否随流内类型码原样还原 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose src = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   JlPose back;
	///   using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
	///   {
	///       src.Serialize(ms);
	///       ms.Position = 0;
	///       back = JlPose.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>调用方负责流的打开与关闭；位置游标要对准 <c>Serialize</c> 写入的起点。</para>
	/// </remarks>
	public static JlPose Deserialize(Stream stream)
	{
		JlPose hPose = new JlPose();
		hPose.DeserializePose(JlSerializationBuffer.ReadFromStream(stream));
		return hPose;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>序列化/反序列化往返得到的独立位姿副本。</summary>
	/// <returns>新 JlPose 实例；与原对象数据完全解耦（JlPose 不实现 IDisposable，无需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现 = <c>SerializePose()</c> 取字节 → <c>new JlPose()</c> → <c>DeserializePose(byte[])</c> 覆写，走两次原生调用（id 1834/1833），比引用赋值贵。</para>
	///   <para><b>约束或前提</b>JlPose 内部是共享 JlTuple 包装：直接赋值只是多一个引用，改动会互相可见 [待实测：JlData 语义]，需要冻结现场值时才 Clone。</para>
	///   <para><b>与相邻算子的取舍</b>只是想"在旧值基础上继续复合、保留本对象"，用 <c>PoseCompose</c>/<c>SetOriginPose</c> 这类本就返回新实例的运算即可，不必 Clone。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose a = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   JlPose b = a.Clone();
	///   </code>
	///   <para><b>资源与坑</b>Clone 经过托管字节数组中转，无句柄泄漏点；ICloneable 显式实现同源。</para>
	/// </remarks>
	public JlPose Clone()
	{
		byte[] data = SerializePose();
		JlPose obj = new JlPose();
		obj.DeserializePose(data);
		return obj;
	}


	/// <summary>对一组位姿求（加权）平均，返回单个新位姿。</summary>
	/// <param name="poses">参与平均的位姿数组（每个自动压平为 7 分量）。</param>
	/// <param name="weights">空元组=等权；否则每个位姿一个权重。Default: []</param>
	/// <param name="mode">平均模式。Default: "iterative"</param>
	/// <param name="sigmaT">平移权重或 "auto"。Default: "auto"</param>
	/// <param name="sigmaR">旋转权重或 "auto"。Default: "auto"</param>
	/// <param name="quality">平均位姿对输入点集的偏差（DOUBLE 装载）。</param>
	/// <returns>加权平均后的新 JlPose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 220。数组先由 <c>JlData.ConcatArray</c> 压平为 7n 元组再传入；weights 为空元组时各点等权，否则须与位姿数一一对应。mode/sigmaT/sigmaR 以元组钉住方式传入（Store+UnpinTuple）；quality 按 DOUBLE 装载，含义是"平均位姿相对输入的点集偏差"，单位与平移分量一致（米）[待实测]。</para>
	///   <para><b>约束或前提</b>输入的每个 JlPose 已是满 7 分量（构造/读入即可）；<c>mode</c> 除默认 <c>"iterative"</c> 外的合法值与 sigmaT/sigmaR <c>"auto"</c> 的具体策略无法由代码判定 [待实测]。平移与旋转尺度差异大时（如毫米级平移+弧度级旋转），auto 权重会失衡，需手给 sigmaT/sigmaR。</para>
	///   <para><b>与相邻算子的取舍</b>要"两个位姿复合"用 PoseCompose，平均≠复合；位姿数=1 时结果即其本身，白白多一次原生调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p1 = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose p2 = new JlPose(0.2, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose mean = JlPose.PoseAverage(new JlPose[] { p1, p2 }, new JlTuple(), "iterative", "auto", "auto", out JlTuple quality);
	///   </code>
	///   <para><b>资源与坑</b>返回新 JlPose（JlData 系，不实现 IDisposable，无需释放）；单实例版差异见 double 重载。</para>
	/// </remarks>
	public static JlPose PoseAverage(JlPose[] poses, JlTuple weights, string mode, JlTuple sigmaT, JlTuple sigmaR, out JlTuple quality)
	{
		JlTuple tupleValue = JlData.ConcatArray(poses);
		IntPtr proc = JlNativeApi.PreCall(220);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.Store(proc, 1, weights);
		JlNativeApi.StoreS(proc, 2, mode);
		JlNativeApi.Store(proc, 3, sigmaT);
		JlNativeApi.Store(proc, 4, sigmaR);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(weights);
		JlNativeApi.UnpinTuple(sigmaT);
		JlNativeApi.UnpinTuple(sigmaR);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out quality);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}

	/// <summary>对一组位姿求（加权）平均（标量 sigma 版）。</summary>
	/// <param name="poses">参与平均的位姿数组。</param>
	/// <param name="weights">空元组=等权；否则每个位姿一个权重。Default: []</param>
	/// <param name="mode">平均模式。Default: "iterative"</param>
	/// <param name="sigmaT">平移权重（数值，不能填 "auto"）。Default: "auto"</param>
	/// <param name="sigmaR">旋转权重（数值，不能填 "auto"）。Default: "auto"</param>
	/// <param name="quality">平均位姿对输入点集的偏差（DOUBLE 装载）。</param>
	/// <returns>加权平均后的新 JlPose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 220；差别在 sigmaT/sigmaR 用 <c>StoreD</c> 数值直写，无钉固定元组的开销，也因此无法表达 "auto"——想自动定权必须用元组重载（Default 值 "auto" 只是文档性说明，本重载传非数值语义的字符串会失败 [待实测]）。</para>
	///   <para><b>约束或前提</b>weights 仍为 JlTuple；quality 按 DOUBLE 装载。平移/旋转尺度悬殊时手工给两个 sigma 是常态，单位约定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p1 = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose p2 = new JlPose(0.2, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose mean = JlPose.PoseAverage(new JlPose[] { p1, p2 }, new JlTuple(), "iterative", 1.0, 1.0, out JlTuple quality);
	///   </code>
	///   <para><b>资源与坑</b>返回新 JlPose（不实现 IDisposable）。</para>
	/// </remarks>
	public static JlPose PoseAverage(JlPose[] poses, JlTuple weights, string mode, double sigmaT, double sigmaR, out JlTuple quality)
	{
		JlTuple tupleValue = JlData.ConcatArray(poses);
		IntPtr proc = JlNativeApi.PreCall(220);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.Store(proc, 1, weights);
		JlNativeApi.StoreS(proc, 2, mode);
		JlNativeApi.StoreD(proc, 3, sigmaT);
		JlNativeApi.StoreD(proc, 4, sigmaR);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(weights);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out quality);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}

	/// <summary>逐元素求逆位姿（数组版），返回同样长度的新数组。</summary>
	/// <param name="pose">待求逆的位姿数组（内部压平为 7n 元组再传入）。</param>
	/// <returns>新的 JlPose[]，第 i 项为输入第 i 项的逆变换。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 226；结果元组按每 7 个分量 SplitArray 切回数组（切分只取整数倍，尾部残值静默丢弃——由输入结构保证不会出现）。</para>
	///   <para><b>约束或前提</b>逆的是"变换本身"：若原位姿按 Rp+T 表示，求逆后平移/旋转的耦合关系随之改变，但复合 P∘P⁻¹ 应回到单位变换 [待实测：单位元的 7 分量取值]。</para>
	///   <para><b>与相邻算子的取舍</b>单个位姿求逆用实例方法 <c>PoseInvert()</c>，少一次数组压平/切分开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p1 = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose[] inverses = JlPose.PoseInvert(new JlPose[] { p1 });
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable，返回数组由 GC 管理。</para>
	/// </remarks>
	public static JlPose[] PoseInvert(JlPose[] pose)
	{
		JlTuple tupleValue = JlData.ConcatArray(pose);
		IntPtr proc = JlNativeApi.PreCall(226);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		err = JlTuple.LoadNew(proc, 0, err, out var data);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data);
	}

	/// <summary>
	///   求本位姿的逆，返回新实例（this 不被修改）。
	/// </summary>
	/// <returns>逆位姿的新 JlPose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 226；this 以 Store 钉入，结果经 <c>LoadNew</c> 装入新对象返回（代码层面可见非原地改写），调用结束有 GC.KeepAlive(this)。</para>
	///   <para><b>与相邻算子的取舍</b>批量求逆用静态数组重载；想连复合一起省一次调用，可直接用 <c>PoseCompose</c> 手工构造。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose inv = p.PoseInvert();
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public JlPose PoseInvert()
	{
		IntPtr proc = JlNativeApi.PreCall(226);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐对复合两组位姿（数组版），返回复合结果数组。</summary>
	/// <param name="poseLeft">左操作数位姿数组。</param>
	/// <param name="poseRight">右操作数位姿数组。</param>
	/// <returns>新的 JlPose[]，第 i 项 = 第 i 项左 ∘ 第 i 项右（先施加右、再施加左 [待实测：左右施加顺序]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 227；两组数组各自压平为 7n 元组后一起传入，结果再按每 7 分量切回数组。两侧长度不等时如何配对（截断/广播）无法由代码判定 [待实测]，应保证等长。</para>
	///   <para><b>约束或前提</b>本库已不提供 JlHomMat3D，复合只能在 JlPose 之间做（内部表示类型码不同也可以复合，结果落在哪种表示 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>一对一复合用实例版 <c>PoseCompose(JlPose)</c> 更直观；求平均用 PoseAverage，别拿复合代替。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose a = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose b = new JlPose(0.0, 0.2, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose[] composed = JlPose.PoseCompose(new JlPose[] { a }, new JlPose[] { b });
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable，返回数组由 GC 管理。</para>
	/// </remarks>
	public static JlPose[] PoseCompose(JlPose[] poseLeft, JlPose[] poseRight)
	{
		JlData[] data = poseLeft;
		JlTuple tupleValue = JlData.ConcatArray(data);
		data = poseRight;
		JlTuple tupleValue2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(227);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.Store(proc, 1, tupleValue2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(tupleValue2);
		err = JlTuple.LoadNew(proc, 0, err, out var data2);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data2);
	}

	/// <summary>
	///   把右侧位姿复合到本位姿（this 为左操作数），返回新实例。
	/// </summary>
	/// <param name="poseRight">右操作数位姿（元组钉住传入，调用结束解钉）。</param>
	/// <returns>复合结果的新 JlPose；this 不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 227：this 以 <c>Store(proc,0)</c> 入左槽、poseRight 入右槽，结果 <c>LoadNew</c> 新对象返回，尾部 GC.KeepAlive(this)。</para>
	///   <para><b>约束或前提</b>左右施加顺序（先右后左 or 先左后右）无法由本文件代码判定 [待实测]；复合链每多一环就是一次原生调用，热路径请缓存复合结果而不是层层套嵌。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose left = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose right = new JlPose(0.0, 0.2, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose combined = left.PoseCompose(right);
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public JlPose PoseCompose(JlPose poseRight)
	{
		IntPtr proc = JlNativeApi.PreCall(227);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, poseRight);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(poseRight);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}































	/// <summary>由相机位置与注视点构造"对准该点"的 3D 相机位姿（批量）。</summary>
	/// <param name="camPosX">光心 x 坐标元组（单位与场景一致，通常米）。</param>
	/// <param name="camPosY">光心 y 坐标元组。</param>
	/// <param name="camPosZ">光心 z 坐标元组。</param>
	/// <param name="lookAtX">注视点 x 坐标元组。</param>
	/// <param name="lookAtY">注视点 y 坐标元组。</param>
	/// <param name="lookAtZ">注视点 z 坐标元组。</param>
	/// <param name="refPlaneNormal">参考面法向量（"朝上"方向），轴名带符号串元组。Default: "-y"</param>
	/// <param name="camRoll">相机滚转角，弧度/角度约定 [待实测]。Default: 0</param>
	/// <returns>新 JlPose[]，第 i 项对应第 i 组 (camPos,lookAt)。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 995：八个输入全部以元组钉住传入（Store+UnpinTuple），结果按每 7 分量 SplitArray 切回数组。视线方向 = 光心指向注视点，绕视线的滚转由 camRoll 决定。</para>
	///   <para><b>约束或前提</b>本库已不提供 JlCamPar/3D 类型族，此接口是纯"位置+方向→位姿"的几何构造，不依赖它们。camPos 与 lookAt 重合时视线退化，结果未定义 [待实测]。refPlaneNormal 与视线平行的退化 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要原地改写一个已存在的 JlPose，用 double 版实例重载；本静态版适合一次算一批。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose[] poses = JlPose.CreateCamPoseLookAtPoint(0.0, 0.0, -1.0, 0.0, 0.0, 0.0, "-y", 0.0);
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlPose[] CreateCamPoseLookAtPoint(JlTuple camPosX, JlTuple camPosY, JlTuple camPosZ, JlTuple lookAtX, JlTuple lookAtY, JlTuple lookAtZ, JlTuple refPlaneNormal, JlTuple camRoll)
	{
		IntPtr proc = JlNativeApi.PreCall(995);
		JlNativeApi.Store(proc, 0, camPosX);
		JlNativeApi.Store(proc, 1, camPosY);
		JlNativeApi.Store(proc, 2, camPosZ);
		JlNativeApi.Store(proc, 3, lookAtX);
		JlNativeApi.Store(proc, 4, lookAtY);
		JlNativeApi.Store(proc, 5, lookAtZ);
		JlNativeApi.Store(proc, 6, refPlaneNormal);
		JlNativeApi.Store(proc, 7, camRoll);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(camPosX);
		JlNativeApi.UnpinTuple(camPosY);
		JlNativeApi.UnpinTuple(camPosZ);
		JlNativeApi.UnpinTuple(lookAtX);
		JlNativeApi.UnpinTuple(lookAtY);
		JlNativeApi.UnpinTuple(lookAtZ);
		JlNativeApi.UnpinTuple(refPlaneNormal);
		JlNativeApi.UnpinTuple(camRoll);
		err = JlTuple.LoadNew(proc, 0, err, out var data);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data);
	}

	/// <summary>
	///   由相机位置与注视点原地改写本位姿（double 版）。
	/// </summary>
	/// <param name="camPosX">光心 x 坐标（米 [待实测]）。</param>
	/// <param name="camPosY">光心 y 坐标。</param>
	/// <param name="camPosZ">光心 z 坐标。</param>
	/// <param name="lookAtX">注视点 x 坐标。</param>
	/// <param name="lookAtY">注视点 y 坐标。</param>
	/// <param name="lookAtZ">注视点 z 坐标。</param>
	/// <param name="refPlaneNormal">参考面法向（轴名带符号串元组，如 "-y"）——注意此参数在本重载仍是 JlTuple 而非 string。Default: "-y"</param>
	/// <param name="camRoll">相机滚转角，弧度/角度约定 [待实测]。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 995；六个坐标用 <c>StoreD</c> 直写（无钉固开销），结果 <c>Load(proc,0)</c> 写回自身——void 返回、原地改写，无新对象产生。</para>
	///   <para><b>约束或前提</b>本方法把相机对准指定方向，但不含任何投影/显示能力（本库显示族与 JlCamPar 类型均已删除，"相机位姿"只是几何约定）；camPos 与 lookAt 重合时方向退化 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>不想破坏现值时先 <c>Clone()</c> 再调用；批量计算用静态元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose camPose = new JlPose();
	///   camPose.CreateCamPoseLookAtPoint(0.0, 0.0, -1.0, 0.0, 0.0, 0.0, "-y", 0.0);
	///   </code>
	///   <para><b>资源与坑</b>refPlaneNormal 以字符串字面量传入时依赖 string→JlTuple 隐式转换；本库该参数族没有 string 版重载，勿与 <c>SetCurrentDir(string)</c> 之类接口想当然类比。JlPose 不实现 IDisposable。</para>
	/// </remarks>
	public void CreateCamPoseLookAtPoint(double camPosX, double camPosY, double camPosZ, double lookAtX, double lookAtY, double lookAtZ, JlTuple refPlaneNormal, double camRoll)
	{
		IntPtr proc = JlNativeApi.PreCall(995);
		JlNativeApi.StoreD(proc, 0, camPosX);
		JlNativeApi.StoreD(proc, 1, camPosY);
		JlNativeApi.StoreD(proc, 2, camPosZ);
		JlNativeApi.StoreD(proc, 3, lookAtX);
		JlNativeApi.StoreD(proc, 4, lookAtY);
		JlNativeApi.StoreD(proc, 5, lookAtZ);
		JlNativeApi.Store(proc, 6, refPlaneNormal);
		JlNativeApi.StoreD(proc, 7, camRoll);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(refPlaneNormal);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}








	/// <summary>
	///   结合相机参数与本位姿（世界位姿），把图像坐标系的 XLD 轮廓变换到世界系 z=0 平面上。
	/// </summary>
	/// <param name="contours">待变换的 XLD 轮廓（输入控制参数）。</param>
	/// <param name="cameraParam">内部相机参数，按库约定的数值元组传入（JlCamPar 类型本库已删除，无类型化入口；分量布局 [待实测]）。</param>
	/// <param name="scale">世界单位/尺度串，如 "m"。Default: "m"</param>
	/// <returns>世界坐标下的新 JlXLDCont 句柄（非原地改写，用毕须 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1810。this 作为世界位姿（WorldPose 槽位）参与投影反解，轮廓点经相机模型反投影到世界 z=0 平面。</para>
	///   <para><b>约束或前提</b>输入是 XLD 轮廓（亚像素点列）而非 Region；世界系必须满足"目标点都在 z=0 平面"的前提，高度不为 0 的物体投影到该平面会产生系统性偏移。cameraParam 与位姿的单位要一致（scale 决定米/毫米）。本库不提供显示与 3D 类型族，本方法仍可用，因为全部输入都是 JlTuple/JlXLDCont。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose worldPose = new JlPose(0.0, 0.0, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlXLDCont contours = new JlXLDCont(new double[] { 100.0, 200.0 }, new double[] { 100.0, 200.0 });
	///   JlTuple cameraParam = new double[] { 0.0, 0.0, 0.008, 0.0, 0.0, 0.0, 0.0, 0.0, 800.0, 800.0, 0.0, 0.0 };
	///   JlTuple scale = "m";
	///   JlXLDCont world = worldPose.ContourToWorldPlaneXld(contours, cameraParam, scale);
	///   world.Dispose();
	///   contours.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>包装体内 this 先写入索引 1 槽、contours 随后又写入同一索引 1——两者对槽位的先后覆盖关系是否影响结果 [待实测]；返回值与 contours 都是 JlXLDCont 句柄（实现 IDisposable），都要释放；cameraParam 的 12 个分量顺序是按库文档约定示例的 [待实测]。</para>
	/// </remarks>
	public JlXLDCont ContourToWorldPlaneXld(JlXLDCont contours, JlTuple cameraParam, JlTuple scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.Store(proc, 0, cameraParam);
		JlNativeApi.Store(proc, 2, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(cameraParam);
		JlNativeApi.UnpinTuple(scale);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
		return obj;
	}

	/// <summary>
	///   结合相机参数与本位姿，把 XLD 轮廓变换到世界系 z=0 平面（scale 字符串版）。
	/// </summary>
	/// <param name="contours">待变换的 XLD 轮廓。</param>
	/// <param name="cameraParam">内部相机参数数值元组（JlCamPar 类型本库已删除；布局 [待实测]）。</param>
	/// <param name="scale">世界单位/尺度串。Default: "m"</param>
	/// <returns>世界坐标下的新 JlXLDCont 句柄（用毕须 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 1810，scale 以 <c>StoreS</c> 直写、不钉固定元组；其余与元组重载一致。</para>
	///   <para><b>约束或前提</b>以字符串字面量传 scale 时，重载解析选中本重载（恒等转换优先于 string→JlTuple 隐式转换）。z=0 平面假设、单位一致性等约束见元组重载的说明。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose worldPose = new JlPose(0.0, 0.0, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlXLDCont contours = new JlXLDCont(new double[] { 100.0, 200.0 }, new double[] { 100.0, 200.0 });
	///   JlTuple cameraParam = new double[] { 0.0, 0.0, 0.008, 0.0, 0.0, 0.0, 0.0, 0.0, 800.0, 800.0, 0.0, 0.0 };
	///   JlXLDCont world = worldPose.ContourToWorldPlaneXld(contours, cameraParam, "m");
	///   world.Dispose();
	///   contours.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与 contours 均为 JlXLDCont 句柄（实现 IDisposable）；槽位覆盖疑点同元组重载 [待实测]。</para>
	/// </remarks>
	public JlXLDCont ContourToWorldPlaneXld(JlXLDCont contours, JlTuple cameraParam, string scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.Store(proc, 0, cameraParam);
		JlNativeApi.StoreS(proc, 2, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(cameraParam);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
		return obj;
	}

	/// <summary>
	///   平移本位姿的原点，返回新位姿。
	/// </summary>
	/// <param name="DX">x 方向平移量（单位与位姿平移分量一致，通常米）。Default: 0</param>
	/// <param name="DY">y 方向平移量。Default: 0</param>
	/// <param name="DZ">z 方向平移量。Default: 0</param>
	/// <returns>平移后的新 JlPose；this 不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1812：this 钉入后用 <c>LoadNew</c> 取回新实例；三个量以 <c>StoreD</c> 直写。</para>
	///   <para><b>约束或前提</b>偏移施加在物体坐标系还是世界坐标系、与 orderOfTransform（"Rp+T"/"T+Rp"）是否耦合，无法由代码判定 [待实测]——换一种表示形式重复平移可能得到不同结果，务必实测确认。</para>
	///   <para><b>与相邻算子的取舍</b>沿固定向量做链式平移时，连续调用每次都过一次原生调用；能合并成一次 (DX,DY,DZ) 就合并。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose moved = pose.SetOriginPose(0.05, 0.0, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable；参数名 DX/DY/DZ 大写是签名的一部分，示例保持一致。</para>
	/// </remarks>
	public JlPose SetOriginPose(double DX, double DY, double DZ)
	{
		IntPtr proc = JlNativeApi.PreCall(1812);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, DX);
		JlNativeApi.StoreD(proc, 2, DY);
		JlNativeApi.StoreD(proc, 3, DZ);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   读取本位姿当前的表示形式三元组（只查询，不修改）。
	/// </summary>
	/// <param name="orderOfRotation">旋转值的含义（如 "gba"/"abg"，欧拉序细节 [待实测]）。</param>
	/// <param name="viewOfTransform">变换视角（"point"/"object" 之一 [待实测]）。</param>
	/// <returns>旋转与平移的先后次序串（如 "Rp+T"/"T+Rp"）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1814：三个字符串都经 <c>LoadS</c> 读出（返回值=orderOfTransform，另两个走 out）。</para>
	///   <para><b>约束或前提</b>7 分量中的第 7 个就是这里的类型码的数值形式，三者合起来才构成完整表示约定；本方法不改变位姿。</para>
	///   <para><b>与相邻算子的取舍</b>想改成别的表示形式用 <c>ConvertPoseType</c>（它会返回新实例），别拿本方法的返回值手工拼数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   string orderOfTransform = pose.GetPoseType(out string orderOfRotation, out string viewOfTransform);
	///   </code>
	///   <para><b>资源与坑</b>忽略返回值只看 out 会丢掉次序信息；无句柄资源。</para>
	/// </remarks>
	public string GetPoseType(out string orderOfRotation, out string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1814);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		err = JlNativeApi.LoadS(proc, 1, err, out orderOfRotation);
		err = JlNativeApi.LoadS(proc, 2, err, out viewOfTransform);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}

	/// <summary>
	///   换一种表示形式描述同一刚体变换，返回新位姿。
	/// </summary>
	/// <param name="orderOfTransform">目标旋转/平移次序。Default: "Rp+T"</param>
	/// <param name="orderOfRotation">目标旋转值含义（欧拉序等）。Default: "gba"</param>
	/// <param name="viewOfTransform">目标视角。Default: "point"</param>
	/// <returns>目标表示形式下的新 JlPose；this 不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1815：三个串以 <c>StoreS</c> 直写，this 钉入，结果 <c>LoadNew</c> 新对象。只换参数化方式，不换变换本身。</para>
	///   <para><b>约束或前提</b>HALCON 里与 JlHomMat3D/JlQuaternion 互转的接口在本库已随类型删除，不存在同名入口；这里只处理 pose↔pose。非法串组合或奇异欧拉构型下的换算行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想"知道当前是什么形式"用 <c>GetPoseType</c>（不生成新对象）；要数值分量对照用 <c>RawData</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   JlPose converted = pose.ConvertPoseType("T+Rp", "gba", "point");
	///   </code>
	///   <para><b>资源与坑</b>JlPose 不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public JlPose ConvertPoseType(string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1815);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, orderOfTransform);
		JlNativeApi.StoreS(proc, 2, orderOfRotation);
		JlNativeApi.StoreS(proc, 3, viewOfTransform);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用给定的平移/旋转分量原地构造（覆写）本位姿。
	/// </summary>
	/// <param name="transX">x 方向平移，单位米。Default: 0.1</param>
	/// <param name="transY">y 方向平移，单位米。Default: 0.1</param>
	/// <param name="transZ">z 方向平移，单位米。Default: 0.1</param>
	/// <param name="rotX">绕 x 轴转角（度）或 Rodriguez 向量 x 分量（无量纲），随 orderOfRotation 而定。Default: 90.0</param>
	/// <param name="rotY">绕 y 轴转角或 Rodriguez y 分量。Default: 90.0</param>
	/// <param name="rotZ">绕 z 轴转角或 Rodriguez z 分量。Default: 90.0</param>
	/// <param name="orderOfTransform">旋转/平移次序串。Default: "Rp+T"</param>
	/// <param name="orderOfRotation">旋转值含义串（欧拉序）。Default: "gba"</param>
	/// <param name="viewOfTransform">视角串。Default: "point"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 9 参构造器同一原生 id 1816，区别只在结果落地方式：构造器初始化新对象，本方法 <c>Load(proc,0)</c> 原地覆写已有实例。</para>
	///   <para><b>约束或前提</b>欧拉序 "gba"/"abg" 的具体轴序与旋转正方向（右手系假定）无法由本文件代码判定 [待实测]；三个 rot 分量的含义完全取决于 orderOfRotation——把它当"绕固定三轴的角度"直接喂 Rodriguez 值会静默出错。</para>
	///   <para><b>与相邻算子的取舍</b>新位姿优先直接 <c>new JlPose(同九个参数)</c>；本方法适合复用实例避免小对象分配。表示形式要换用 ConvertPoseType，不要重喂数值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose();
	///   pose.CreatePose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   </code>
	///   <para><b>资源与坑</b>void 返回、无新对象；JlPose 不实现 IDisposable。</para>
	/// </remarks>
	public void CreatePose(double transX, double transY, double transZ, double rotX, double rotY, double rotZ, string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1816);
		JlNativeApi.StoreD(proc, 0, transX);
		JlNativeApi.StoreD(proc, 1, transY);
		JlNativeApi.StoreD(proc, 2, transZ);
		JlNativeApi.StoreD(proc, 3, rotX);
		JlNativeApi.StoreD(proc, 4, rotY);
		JlNativeApi.StoreD(proc, 5, rotZ);
		JlNativeApi.StoreS(proc, 6, orderOfTransform);
		JlNativeApi.StoreS(proc, 7, orderOfRotation);
		JlNativeApi.StoreS(proc, 8, viewOfTransform);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}



	/// <summary>用 <c>SerializePose()</c> 得到的字节覆写本位姿（原地改写）。</summary>
	/// <param name="serializedItemHandle">库自有二进制格式的位姿字节（不是句柄数值，是完整负载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1833；字节先包进 <c>JlSerializationBuffer</c>（using 释放），调用后用 <c>Load(proc,0)</c> 把结果写回自身——这是原地改写而非返回新对象。</para>
	///   <para><b>约束或前提</b>本方法来自 <c>ISerializable</c> 反序列化路径（.NET 二进制序列化构造器也调它），字节来源不合法时报错出自原生层 [待实测]。JlPose 不实现 IDisposable。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose src = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   byte[] data = src.SerializePose();
	///   JlPose dst = new JlPose();
	///   dst.DeserializePose(data);
	///   </code>
	///   <para><b>资源与坑</b>buffer 在方法内 using 释放且调用处有 GC.KeepAlive，原生调用期间不会被回收；调用方只需管 byte[] 本身（GC 自动）。</para>
	/// </remarks>
	public void DeserializePose(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		IntPtr proc = JlNativeApi.PreCall(1833);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把本位姿导出为库自有二进制格式的字节数组。</summary>
	/// <returns>序列化负载 byte[]（每次调用新建；配套 <c>DeserializePose(byte[])</c> 读回）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1834：本位姿钉住传入（Store+UnpinTuple，调用结束有 GC.KeepAlive），结果用 <c>JlSerializationBuffer.LoadBytes</c> 拷成托管数组。</para>
	///   <para><b>约束或前提</b>字节布局属库私有格式，不要与 <c>WritePose</c> 的文本格式混用；跨语言/跨版本兼容性 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要塞进流用实例方法 <c>Serialize(Stream)</c>；要人可读的文本文件用 <c>WritePose(string)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   byte[] data = pose.SerializePose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是纯托管字节，GC 管理，无句柄。</para>
	/// </remarks>
	public byte[] SerializePose()
	{
		IntPtr proc = JlNativeApi.PreCall(1834);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   从文本文件读入位姿并原地覆写本实例。
	/// </summary>
	/// <param name="poseFile">位姿文本文件路径。Default: "campose.dat"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1835：文件名 <c>StoreS</c> 传入，<c>InitOCT</c> 预留输出后 <c>Load(proc,0)</c> 原地写入自身。</para>
	///   <para><b>约束或前提</b>文件格式（几行、角度制与否、是否含表示形式行）无法由本文件代码判定 [待实测]，须与 <c>WritePose</c> 产物或库文档约定的样例文件配套；文件不存在/格式错时报错来自原生层。原参数注释写作 "File name of the external camera parameters"，说明它同时可承担外部相机位姿文件的读入。</para>
	///   <para><b>与相邻算子的取舍</b>程序间传大位姿数组用 <see cref="Serialize(Stream)"/> 二进制族；本方法面向"人可编辑"的单个位姿文件。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose();
	///   pose.ReadPose("campose.dat");
	///   </code>
	///   <para><b>资源与坑</b>读失败时实例可能停留在原值或未初始化态 [待实测]，失败路径建议读入到 <c>Clone()</c> 副本先验证。</para>
	/// </remarks>
	public void ReadPose(string poseFile)
	{
		IntPtr proc = JlNativeApi.PreCall(1835);
		JlNativeApi.StoreS(proc, 0, poseFile);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把本位姿写出为文本文件。
	/// </summary>
	/// <param name="poseFile">目标文件路径（覆盖写入）。Default: "campose.dat"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1836：this 以 <c>Store(proc,0)</c> 钉入（调用后解钉、尾部 GC.KeepAlive），文件名 <c>StoreS</c> 入第 1 槽；无输出参数。</para>
	///   <para><b>约束或前提</b>输出文本的格式/角度制与 <c>ReadPose</c> 配套 [待实测]；位姿以哪种表示形式落盘（当前表示形式还是固定形式）无法由代码判定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要无损、机器可读的负载用 <c>SerializePose()</c> 字节族；给人核对/手改才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   pose.WritePose("campose.dat");
	///   </code>
	///   <para><b>资源与坑</b>目录不可写时抛原生错误；无句柄资源。</para>
	/// </remarks>
	public void WritePose(string poseFile)
	{
		IntPtr proc = JlNativeApi.PreCall(1836);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, poseFile);
		int procResult = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}






}
