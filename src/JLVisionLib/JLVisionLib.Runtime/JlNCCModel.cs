using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an NCC model for matching.</summary>
[Serializable]
public class JlNCCModel : JlHandle, ISerializable, ICloneable
{
	/// <summary>构造持有 UNDEF（空）句柄的未初始化实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlNCCModel()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlNCCModel(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>从 <see cref="JlHandle"/> 句柄包装构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlNCCModel(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("ncc_model");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlNCCModel obj)
	{
		obj = new JlNCCModel(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlNCCModel[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlNCCModel[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlNCCModel(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   Read an NCC model from a file.
	/// </summary>
	/// <param name="fileName">模型文件路径，须与 WriteNccModel / Serialize 写出的格式对应。File name.</param>
	/// <remarks>
	///   <para><b>功能说明</b>构造 JlNCCModel：从文件读取 NCC 灰度相关模型（原生 id 939，与 ReadNccModel 同一算子）。新对象持有读入的原生句柄；文件里存的是已训练好的模型数据（模板域灰度、金字塔角度参数、metric），读回即可直接 FindNccModel，无需重建。</para>
	///   <para><b>与相邻算子的取舍</b>要在既有对象上换内容用 ReadNccModel（同 id，先 Dispose 再原地装入）；要在内存中还原用 DeserializeNccModel 或静态 Deserialize(Stream)。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlNCCModel model = new JlNCCModel("bottle.ncm");
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄对象，用毕 Dispose；文件不存在或格式非法时的异常形态 [待实测]。</para>
	/// </remarks>
	public JlNCCModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(939);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Prepare an NCC model for matching.
	/// </summary>
	/// <param name="template">模板图像；只取其定义域（domain）内的灰度作为模型内容——先用 ReduceDomain 抠出感兴趣区域再建模，可排除背景干扰。Input image whose domain will be used to create the model.</param>
	/// <param name="numLevels">金字塔层数上限；传 "auto" 由模板尺寸决定。可传多值或字符串是本重载相对 int 重载的能力。Maximum number of pyramid levels. Default: "auto"</param>
	/// <param name="angleStart">最小旋转角，弧度（-0.39 约等于 -22.4 度）。Smallest rotation of the pattern. Default: -0.39</param>
	/// <param name="angleExtent">角度覆盖范围，弧度，相对 angleStart 的增量（0.79 约等于 45.3 度）。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="angleStep">角度离散步长（弧度，决定模型训练精度）；传 "auto" 自动选择。Step length of the angles (resolution). Default: "auto"</param>
	/// <param name="metric">匹配度量；"use_polarity" 要求模板与目标的灰度极性一致（亮对亮），取 "ignore_polarity" 等其它值时原样透传原生层，支持集合 [待实测]。Match metric. Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b>构造 JlNCCModel：训练灰度相关（NCC）匹配模板（原生 id 947，与 CreateNccModel 同一算子）。模型存的是模板域内各金字塔层的灰度图，FindNccModel 时按归一化互相关打分。</para>
	///   <para><b>约束或前提</b>模板内容来自 template 的 domain：整幅图直接传入会把背景一起编进模型。NCC 靠灰度值本身，对光照漂移、整体对比度变化敏感，且低对比度/近纹理化目标会失效——这类目标应改用形状模板（JlShapeModel，靠梯度边缘，不怕整体光照变化但怕无边缘目标）；反之，弱边缘、依赖灰度差分区分的目标形状模板也测不到，两套机制互补选型。</para>
	///   <para><b>与相邻算子的取舍</b>NCC 模型没有尺度（scale）参数：角度可调、尺度缩放不支持，尺寸会变的工件不适用 NCC。在既有对象上重建用 CreateNccModel（同 id，先释放旧句柄）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlImage template = new JlImage("template.png"))
	///   {
	///       JlNCCModel model = new JlNCCModel(template, "auto", -0.39, 0.79, "auto", "use_polarity");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本重载用 Store+UnpinTuple 钉住传入的 JlTuple 原样透传（支持 "auto" 字符串与多值），int 重载经 StoreI/StoreD 直写标量；两者同一原生 id。新对象持原生句柄，用毕 Dispose。</para>
	/// </remarks>
	public JlNCCModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(947);
		JlNativeApi.Store(proc, 1, template);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   Prepare an NCC model for matching.
	/// </summary>
	/// <param name="template">模板图像；模型内容取其 domain 内灰度。Input image whose domain will be used to create the model.</param>
	/// <param name="numLevels">金字塔层数，整数（无 "auto" 可用，需要自动取值请走 JlTuple 重载）。Maximum number of pyramid levels. Default: "auto"</param>
	/// <param name="angleStart">最小旋转角，弧度。Smallest rotation of the pattern. Default: -0.39</param>
	/// <param name="angleExtent">角度覆盖范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="angleStep">角度步长，弧度数值（不能传 "auto"）。Step length of the angles (resolution). Default: "auto"</param>
	/// <param name="metric">匹配度量，如 "use_polarity"；本重载本就是 string 形参，取值集合 [待实测]。Match metric. Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b>JlNCCModel(JlImage, JlTuple, ...) 的标量重载：同原生 id 947 训练灰度相关模板；差异仅在 numLevels/angleStep 以 StoreI/StoreD 直写、无钉固定元组开销，也失去了传 "auto" 字符串的能力。</para>
	///   <para><b>约束或前提</b>建模语义（domain 成模、角度弧度、无尺度参数、NCC 与形状模板的取舍）详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlImage template = new JlImage("template.png"))
	///   {
	///       JlNCCModel model = new JlNCCModel(template, 3, -0.39, 0.79, 0.0087, "use_polarity");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>新对象持原生句柄，用毕 Dispose；template 在原生调用结束前不得释放（方法体末尾 GC.KeepAlive），调用返回后即可 Dispose 模板。</para>
	/// </remarks>
	public JlNCCModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(947);
		JlNativeApi.Store(proc, 1, template);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeNccModel();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlNCCModel(SerializationInfo info, StreamingContext context)
	{
		DeserializeNccModel((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>Serialize object to binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b>把 NCC 模型序列化后的字节流写入 Stream：内部先调 SerializeNccModel（id 938）取 byte[]，再落流。不改动、不释放本对象句柄。</para>
	///   <para><b>与相邻算子的取舍</b>落盘用 WriteNccModel；只要内存字节数组用 SerializeNccModel；跨进程/网络传递用本方法配静态 Deserialize(Stream)。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       model.Serialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>传入流的开关由调用方负责，本类不关闭它。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeNccModel(), stream);
	}

	/// <summary>Deserialize object from binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法：从流读出数据并经 DeserializeNccModel（id 937）还原，返回持有新原生句柄的 JlNCCModel；不修改任何既有对象。</para>
	///   <para><b>约束或前提</b>流位置须在数据起点（同一 MemoryStream 写后读回常需 Position = 0）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlNCCModel restored;
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       model.Serialize(ms);
	///       ms.Position = 0;
	///       restored = JlNCCModel.Deserialize(ms);
	///   }
	///   restored.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的实例与原实例各自持独立句柄，须分别 Dispose。</para>
	/// </remarks>
	public new static JlNCCModel Deserialize(Stream stream)
	{
		JlNCCModel hNCCModel = new JlNCCModel();
		hNCCModel.DeserializeNccModel(JlSerializationBuffer.ReadFromStream(stream));
		return hNCCModel;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b>深拷贝整个 NCC 模型：走 SerializeNccModel（id 938）→ DeserializeNccModel（id 937）的字节数组往返，返回持有独立新句柄的副本，含训练好的模板数据。</para>
	///   <para><b>与相邻算子的取舍</b>多个线程各自匹配同一模型时先 Clone 再分线程持有；只要传引用共享一个对象也可匹配（FindNccModel 只读模型），Clone 仅在要各自改参数（SetNccModelParam）时才必要。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   {
	///       JlNCCModel spare = model.Clone();
	///       spare.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>ICloneable 显式实现走同一 Clone；两份句柄须各自 Dispose；序列化往返有耗时，热路径慎用。</para>
	/// </remarks>
	public new JlNCCModel Clone()
	{
		byte[] data = SerializeNccModel();
		JlNCCModel obj = new JlNCCModel();
		obj.DeserializeNccModel(data);
		return obj;
	}

	/// <summary>
	///   Free the memory of an NCC model.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>释放原生侧 NCC 模型内存（原生 id 936，clear_ncc_model）。方法体只调原生删除，不清空托管侧句柄字段。</para>
	///   <para><b>与相邻算子的取舍</b>常规释放直接用 Dispose() 或 using；本方法仅用于"立刻归还原生内存但对象暂时还在作用域里"的场合。要在原地换一个新模型，用 CreateNccModel / ReadNccModel / DeserializeNccModel（它们内部会先释放旧句柄再装入）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlNCCModel model = new JlNCCModel("bottle.ncm");
	///   model.ClearNccModel(); // 调用后不得再用该对象匹配
	///   </code>
	///   <para><b>资源与坑</b>调用后该变量仍指向已被删除的原生对象，再调 FindNccModel 等行为未定义 [待实测]；其后若再触发 Dispose 是否二次释放 [待实测]。</para>
	/// </remarks>
	public void ClearNccModel()
	{
		IntPtr proc = JlNativeApi.PreCall(936);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Deserialize an NCC model.
	/// </summary>
	/// <param name="serializedItemHandle">来自 SerializeNccModel / WriteNccModel 体系的字节数组。Handle of the serialized item.</param>
	/// <remarks>
	///   <para><b>功能说明</b>从字节数组还原 NCC 模型（原生 id 937）。方法体先 Dispose() 本对象旧句柄、再把新句柄装入本对象——原地替换，不是返回新对象。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel src = new JlNCCModel("bottle.ncm"))
	///   {
	///       byte[] data = src.SerializeNccModel();
	///       JlNCCModel dst = new JlNCCModel();
	///       dst.DeserializeNccModel(data);
	///       dst.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>Clone、静态 Deserialize(Stream)、ISerializable 构造都经由本方法；数据非法时旧句柄已被释放，对象成空壳 [失败后状态待实测]。空壳 JlNCCModel() 构造出的对象本就无有效句柄，可直接用于装载本方法。</para>
	/// </remarks>
	public void DeserializeNccModel(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(937);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   Serialize an NCC model.
	/// </summary>
	/// <returns>模型完整数据的字节数组（英文 returns 中的 "Handle of the serialized item" 是底层缓冲表述，C# 侧拿到的是纯数据）。Handle of the serialized item.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 NCC 模型序列化为 Vision 二进制格式 byte[]（原生 id 938）。只读，不动本对象句柄，调用后模型仍可继续匹配。</para>
	///   <para><b>与相邻算子的取舍</b>落盘用 WriteNccModel；写流用 Serialize(Stream)；还原用 DeserializeNccModel（注意它是原地替换句柄，想保留原模型就先 new 一个空壳再装载）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   {
	///       byte[] data = model.SerializeNccModel();
	///       int bytes = data.Length;
	///   }
	///   </code>
	/// </remarks>
	public byte[] SerializeNccModel()
	{
		IntPtr proc = JlNativeApi.PreCall(938);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   Read an NCC model from a file.
	/// </summary>
	/// <param name="fileName">模型文件路径（与 WriteNccModel 成对）。File name.</param>
	/// <remarks>
	///   <para><b>功能说明</b>从文件读 NCC 模型（原生 id 939）。方法体先 Dispose() 旧句柄、再把文件内容的新句柄装入本对象——原地替换；构造器 JlNCCModel(fileName) 是同一算子的"新对象"版本。</para>
	///   <para><b>与相邻算子的取舍</b>复用同一对象引用换模型（例如产线程序里被到处传着的模型变量）用本方法；不想动现有对象就 new。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlNCCModel model = new JlNCCModel();
	///   model.ReadNccModel("bottle.ncm");
	///   </code>
	///   <para><b>资源与坑</b>读取失败时本对象旧句柄已释放、成为空壳 [失败后状态待实测]。</para>
	///   <para><b>相关算子</b>FindNccModel、WriteNccModel</para>
	/// </remarks>
	public void ReadNccModel(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(939);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Write an NCC model to a file.
	/// </summary>
	/// <param name="fileName">目标文件路径；存的是训练完成的模型本体（非图像）。File name.</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 NCC 模型以 Vision 二进制格式写入文件（原生 id 940）。只读，不动本对象句柄。</para>
	///   <para><b>与相邻算子的取舍</b>读回：构造器 JlNCCModel(fileName)（新对象）或 ReadNccModel（原地替换）；内存中转换用 SerializeNccModel / DeserializeNccModel。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlImage template = new JlImage("template.png"))
	///   using (JlNCCModel model = new JlNCCModel(template, 3, -0.39, 0.79, 0.0087, "use_polarity"))
	///   {
	///       model.WriteNccModel("bottle.ncm");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>路径/目录错误的表现 [待实测]。</para>
	/// </remarks>
	public void WriteNccModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(940);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>Determine the parameters of an NCC model.</summary>
	/// <param name="template">用于统计特征的模板图像；只取其 domain 内灰度（与建模同一约定）。Input image whose domain will be used to create the model.</param>
	/// <param name="numLevels">传给算法的金字塔层数上限，可 "auto"。Maximum number of pyramid levels. Default: "auto"</param>
	/// <param name="angleStart">最小旋转角，弧度。Smallest rotation of the model. Default: -0.39</param>
	/// <param name="angleExtent">角度范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="metric">匹配度量。Match metric. Default: "use_polarity"</param>
	/// <param name="parameters">要自动确定的参数名，如 "all"；可选名集合 [待实测]。Parameters to be determined automatically. Default: "all"</param>
	/// <param name="parameterValue">与返回名一一对应的推荐值（DOUBLE 域数值）。Value of the automatically determined parameter.</param>
	/// <returns>被自动确定的参数名元组（按序对应 parameterValue）。Name of the automatically determined parameter.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态算子（原生 id 941）：不建模、不产生句柄，只根据模板的灰度统计推荐 num_levels / angle_step 一类参数的取值。返回的名与 out 值两个元组等长、按序对应。</para>
	///   <para><b>与相邻算子的取舍</b>拿到的推荐值用于随后 CreateNccModel/构造器的同名参数；不想自己定参就直接给 "auto"。对比 GetNccModelParams：那是对已建好的模型回读实际生效参数，本算子是在建模型之前做推荐。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlImage template = new JlImage("template.png"))
	///   {
	///       JlTuple names = JlNCCModel.DetermineNccModelParams(template, "auto", -0.39, 0.79,
	///           "use_polarity", "all", out JlTuple values);
	///       int count = names.Length; // 与 values.Length 相等
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本重载 Store+UnpinTuple 钉传 numLevels/parameters 两个 JlTuple；返回的两个 JlTuple 由调用方管理。</para>
	/// </remarks>
	public static JlTuple DetermineNccModelParams(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, string metric, JlTuple parameters, out JlTuple parameterValue)
	{
		IntPtr proc = JlNativeApi.PreCall(941);
		JlNativeApi.Store(proc, 1, template);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreS(proc, 3, metric);
		JlNativeApi.Store(proc, 4, parameters);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(parameters);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out parameterValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(template);
		return tuple;
	}

	/// <summary>Determine the parameters of an NCC model.</summary>
	/// <param name="template">模板图像（取 domain 内灰度）。Input image whose domain will be used to create the model.</param>
	/// <param name="numLevels">金字塔层数，整数标量。Maximum number of pyramid levels. Default: "auto"</param>
	/// <param name="angleStart">最小旋转角，弧度。Smallest rotation of the model. Default: -0.39</param>
	/// <param name="angleExtent">角度范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="metric">匹配度量。Match metric. Default: "use_polarity"</param>
	/// <param name="parameters">要自动确定的参数名（单个字符串，如 "all"）。Parameters to be determined automatically. Default: "all"</param>
	/// <param name="parameterValue">推荐值元组。Value of the automatically determined parameter.</param>
	/// <returns>被确定的参数名元组。Name of the automatically determined parameter.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>DetermineNccModelParams 的标量重载：同原生 id 941，numLevels 经 StoreI、parameters 经 StoreS 直写，无钉固定元组开销；语义、返回排布详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlImage template = new JlImage("template.png"))
	///   {
	///       JlTuple names = JlNCCModel.DetermineNccModelParams(template, 3, -0.39, 0.79,
	///           "use_polarity", "all", out JlTuple values);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>两个输出（返回名元组与 out 值元组）等长按序对应，这是该算子唯一的取结果契约。</para>
	/// </remarks>
	public static JlTuple DetermineNccModelParams(JlImage template, int numLevels, double angleStart, double angleExtent, string metric, string parameters, out JlTuple parameterValue)
	{
		IntPtr proc = JlNativeApi.PreCall(941);
		JlNativeApi.Store(proc, 1, template);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreS(proc, 3, metric);
		JlNativeApi.StoreS(proc, 4, parameters);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out parameterValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(template);
		return tuple;
	}

	/// <summary>
	///   Return the parameters of an NCC model.
	/// </summary>
	/// <param name="angleStart">模型训练时的最小旋转角，弧度。Smallest rotation of the pattern.</param>
	/// <param name="angleExtent">角度覆盖范围，弧度。Extent of the rotation angles.</param>
	/// <param name="angleStep">角度步长，弧度。Step length of the angles (resolution).</param>
	/// <param name="metric">建模时实际生效的匹配度量。Match metric.</param>
	/// <returns>金字塔层数（INTEGER 装载，原生输出槽 0；角度与度量在槽 1~4）。Number of pyramid levels.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>回读已建模型的实际参数（原生 id 942）。注意 numLevels 是返回值（LoadI 读整数）、其余四个经 out 装载（LoadD×3 + LoadS）——返回值不是错误码。</para>
	///   <para><b>与相邻算子的取舍</b>与 DetermineNccModelParams 相对：那是不建模的推荐，这里读的是模型里已固化的值；建模后传给 FindNccModel 的角度区间若超出这里回读的范围，是否被原生层截断 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   {
	///       int levels = model.GetNccModelParams(out double a0, out double aExt, out double aStep, out string metric);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>只读操作，不改动句柄。</para>
	/// </remarks>
	public int GetNccModelParams(out double angleStart, out double angleExtent, out double angleStep, out string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(942);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out angleStart);
		err = JlNativeApi.LoadD(proc, 2, err, out angleExtent);
		err = JlNativeApi.LoadD(proc, 3, err, out angleStep);
		err = JlNativeApi.LoadS(proc, 4, err, out metric);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Return the origin (reference point) of an NCC model.
	/// </summary>
	/// <param name="row">模型参考点行坐标，像素（row = y，向下为正）。Row coordinate of the origin of the NCC model.</param>
	/// <param name="column">模型参考点列坐标，像素（column = x，向右为正）。Column coordinate of the origin of the NCC model.</param>
	/// <remarks>
	///   <para><b>功能说明</b>取 NCC 模型的参考点（原生 id 943）——FindNccModel 输出的 row/column 正是该点在图像中的位置，不是模板中心（除非二者重合）。</para>
	///   <para><b>与相邻算子的取舍</b>想要"匹配框绕物体几何中心"就 SetNccModelOrigin 改到中心；要与其它坐标系（如手眼标定）对齐时以该点为桥梁。GetNccModelRegion 能对照看到建模域形状。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   {
	///       model.GetNccModelOrigin(out double row, out double column);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>只读；默认参考点位置由建模时模板 domain 决定 [与建模位置的关系待实测]。</para>
	/// </remarks>
	public void GetNccModelOrigin(out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(943);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set the origin (reference point) of an NCC model.
	/// </summary>
	/// <param name="row">新参考点行坐标，像素（模型内部坐标，非图像坐标）。Row coordinate of the origin of the NCC model.</param>
	/// <param name="column">新参考点列坐标，像素。Column coordinate of the origin of the NCC model.</param>
	/// <remarks>
	///   <para><b>功能说明</b>改设 NCC 模型参考点（原生 id 944），原地生效：此后 FindNccModel 输出的 row/column 立即按新参考点报告，模型灰度数据本身不变。典型用法是把参考点放到孔/字符中心等物理基准处。</para>
	///   <para><b>约束或前提</b>参考点越出建模域时的行为 [待实测]；改点后已保存的匹配结果坐标即失去可比性，需要重跑。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   {
	///       model.GetNccModelOrigin(out double oldRow, out double oldCol);
	///       model.SetNccModelOrigin(oldRow + 10.0, oldCol + 20.0);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>原地修改，不换句柄；用毕 Dispose。</para>
	/// </remarks>
	public void SetNccModelOrigin(double row, double column)
	{
		IntPtr proc = JlNativeApi.PreCall(944);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Find the best matches of an NCC model in an image.
	/// </summary>
	/// <param name="image">待搜图；单通道灰度图。灰度绝对水平漂移会直接拉低 NCC 得分（与形状模板的本质差别）。Input image in which the model should be found.</param>
	/// <param name="angleStart">本帧搜索的最小角，弧度；应落在模型训练范围内，越界是否截断 [待实测]。Smallest rotation of the model. Default: -0.39</param>
	/// <param name="angleExtent">本帧搜索角度范围，弧度（相对 angleStart）。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="minScore">接受实例的最低相关得分（0~1 域 [上限含义待实测]）。Minimum score of the instances of the model to be found. Default: 0.8</param>
	/// <param name="numMatches">要找的实例数；0 表示给出分的所有实例。Number of instances of the model to be found (or 0 for all matches). Default: 1</param>
	/// <param name="maxOverlap">两实例允许的最大重叠比例（0~1）；numMatches=0 时它控制重叠峰抑制的松紧。Maximum overlap of the instances of the model to be found. Default: 0.5</param>
	/// <param name="subPixel">是否亚像素精化，"true"/"none" 之类，取值集合 [待实测]。Subpixel accuracy. Default: "true"</param>
	/// <param name="numLevels">本帧匹配用的金字塔层数（JlTuple 可传负数二元组：按英文说明 |NumLevels| = 2 时第二值指定最低层）。Number of pyramid levels used in the matching (and lowest pyramid level to use if $|NumLevels| = 2$). Default: 0</param>
	/// <param name="row">各实例参考点行坐标（= SetNccModelOrigin 所设参考点）。Row coordinate of the found instances of the model.</param>
	/// <param name="column">各实例参考点列坐标。Column coordinate of the found instances of the model.</param>
	/// <param name="angle">各实例旋转角，弧度。Rotation angle of the found instances of the model.</param>
	/// <param name="score">各实例相关得分。Score of the found instances of the model.</param>
	/// <remarks>
	///   <para><b>功能说明</b>在图像中按归一化互相关搜 NCC 模板（原生 id 945）。四个 out 元组等长，长度 = 实际找到的实例数（可能为 0，找不到时是空元组而非报错 [待实测]），且都按 DOUBLE 装载。</para>
	///   <para><b>参数取向</b>numMatches/minScore/maxOverlap 相互作用：先按 minScore 过滤，再按得分降序 [排序待实测] 取前 numMatches 个（numMatches=0 时全取），重叠超过 maxOverlap 的候选被抑制——numMatches=0 + 高 minScore + 小 maxOverlap 会得到多而干净的实例；maxOverlap 放大则允许堆叠找同一物体。本签名无 greediness 参数，重叠淘汰策略由原生默认决定 [待实测]。angleStart/angleExtent 是每帧搜索窗，比训练范围窄可提速。</para>
	///   <para><b>与相邻算子的取舍</b>多模型一次搜用 FindNccModels；光照不稳定的现场 NCC 得分会整体下移，minScore 固定阈值会漏检——换形状模板或补光照。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   using (JlImage image = new JlImage("frame001.png"))
	///   {
	///       JlTuple levels = 1;
	///       model.FindNccModel(image, -0.39, 0.79, 0.8, 1, 0.5, "true", levels,
	///           out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///       if (row.Length &gt; 0)
	///       {
	///           double x = column.D; // 最佳实例的列坐标
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本重载 numLevels 经 Store+UnpinTuple 钉传（支持负数/多值编码），int 重载经 StoreI 直写；image 在原生调用结束前不得释放（方法体 GC.KeepAlive），故调用返回后可安全 Dispose 图像。</para>
	///   <para><b>相关算子</b>CreateNccModel、ClearNccModel</para>
	/// </remarks>
	public void FindNccModel(JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(945);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   Find the best matches of an NCC model in an image.
	/// </summary>
	/// <param name="image">待搜图（灰度绝对值敏感，光照漂移直接拉低得分）。Input image in which the model should be found.</param>
	/// <param name="angleStart">本帧搜索最小角，弧度。Smallest rotation of the model. Default: -0.39</param>
	/// <param name="angleExtent">搜索角度范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="minScore">最低接受得分。Minimum score of the instances of the model to be found. Default: 0.8</param>
	/// <param name="numMatches">实例数，0 = 全取。Number of instances of the model to be found (or 0 for all matches). Default: 1</param>
	/// <param name="maxOverlap">最大重叠比例。Maximum overlap of the instances of the model to be found. Default: 0.5</param>
	/// <param name="subPixel">亚像素开关。Subpixel accuracy. Default: "true"</param>
	/// <param name="numLevels">金字塔层数整数标量（无法表达负数二元组编码，需要时用 JlTuple 重载）。Number of pyramid levels used in the matching (and lowest pyramid level to use if $|NumLevels| = 2$). Default: 0</param>
	/// <param name="row">各实例参考点行坐标。Row coordinate of the found instances of the model.</param>
	/// <param name="column">各实例参考点列坐标。Column coordinate of the found instances of the model.</param>
	/// <param name="angle">各实例角度，弧度。Rotation angle of the found instances of the model.</param>
	/// <param name="score">各实例得分。Score of the found instances of the model.</param>
	/// <remarks>
	///   <para><b>功能说明</b>FindNccModel 的标量重载：同原生 id 945，numLevels 经 StoreI 直写；参数交互（minScore 过滤、numMatches 截断、maxOverlap 峰抑制）与四个等长 DOUBLE out 元组的约定详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   using (JlImage image = new JlImage("frame001.png"))
	///   {
	///       model.FindNccModel(image, -0.39, 0.79, 0.8, 5, 0.5, "true", 0,
	///           out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///       int found = row.Length; // 至多 5
	///   }
	///   </code>
	///   <para><b>资源与坑</b>Default 0 的 numLevels 语义（"用建模时的全部层"）以原生默认为准 [待实测]。</para>
	///   <para><b>相关算子</b>CreateNccModel、ClearNccModel</para>
	/// </remarks>
	public void FindNccModel(JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(945);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   Set selected parameters of the NCC model.
	/// </summary>
	/// <param name="genParamName">参数名数组，与 CreateNccModel 的 measure_operation 类扩展设置（如 prealignment/gradient 相关名）对应与否、完整可用名集合 [待实测]。Parameter names.</param>
	/// <param name="genParamValue">与名等长（或单值广播 [广播语义待实测]）的参数值。Parameter values.</param>
	/// <remarks>
	///   <para><b>功能说明</b>建模之后追加/修改模型的通用参数（原生 id 946），原地生效。CreateNccModel 签名里没有 gen-param 数组，故非常规参数只能从本口子进。</para>
	///   <para><b>约束或前提</b>非法名/值的报错形态 [待实测]；改动是否要求重训练才能生效 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   {
	///       JlTuple names = new string[] { "color" }; // 仅为占位示例，合法名 [待实测]
	///       JlTuple values = new string[] { "mono" };
	///       model.SetNccModelParam(names, values);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>两个入参经 Store+UnpinTuple 钉传；GetNccModelParams 只能回读建模四件套，看不到本口子设进去的参数。</para>
	/// </remarks>
	public void SetNccModelParam(JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(946);
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
	///   Prepare an NCC model for matching.
	/// </summary>
	/// <param name="template">模板图像；模型内容取其 domain 内灰度。Input image whose domain will be used to create the model.</param>
	/// <param name="numLevels">金字塔层数上限，可 "auto"。Maximum number of pyramid levels. Default: "auto"</param>
	/// <param name="angleStart">最小旋转角，弧度。Smallest rotation of the pattern. Default: -0.39</param>
	/// <param name="angleExtent">角度覆盖范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="angleStep">角度步长，弧度，可 "auto"。Step length of the angles (resolution). Default: "auto"</param>
	/// <param name="metric">匹配度量（灰度极性约定），取值集合 [待实测]。Match metric. Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b>在既有 JlNCCModel 对象上重建模型（原生 id 947，与构造器同一算子）。方法体先 Dispose() 旧句柄再把新模型句柄装入本对象——原地换柄，训练语义见构造器注释（domain 成模、弧度角、NCC 无尺度参数、怕光照漂移）。</para>
	///   <para><b>与相邻算子的取舍</b>想保留旧对象另存新模型就直接用构造器 new；本方法适合"同一个模型变量反复换内容"的示教流程。measure_operation 类可选值（use_prealignment / gradient / rotation 等）在本绑定签名中无入口，只能寄望 SetNccModelParam，支持与否 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlNCCModel model = new JlNCCModel();
	///   using (JlImage template = new JlImage("template.png"))
	///   {
	///       model.CreateNccModel(template, "auto", -0.39, 0.79, "auto", "use_polarity");
	///   }
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>调用即丢弃旧模型；若旧句柄还被其它 JlNCCModel(handle) 包装共享，行为未定义 [待实测]。本重载 Store+UnpinTuple 钉传两个 JlTuple。</para>
	///   <para><b>相关算子</b>FindNccModel、ClearNccModel</para>
	/// </remarks>
	public void CreateNccModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, string metric)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(947);
		JlNativeApi.Store(proc, 1, template);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   Prepare an NCC model for matching.
	/// </summary>
	/// <param name="template">模板图像（取 domain 内灰度）。Input image whose domain will be used to create the model.</param>
	/// <param name="numLevels">金字塔层数整数标量。Maximum number of pyramid levels. Default: "auto"</param>
	/// <param name="angleStart">最小旋转角，弧度。Smallest rotation of the pattern. Default: -0.39</param>
	/// <param name="angleExtent">角度范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="angleStep">角度步长，弧度数值。Step length of the angles (resolution). Default: "auto"</param>
	/// <param name="metric">匹配度量。Match metric. Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b>CreateNccModel 的标量重载：同原生 id 947，同样先 Dispose() 旧句柄再原地装入新模型；numLevels/angleStep 经 StoreI/StoreD 直写、无 "auto" 能力。训练语义详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlNCCModel model = new JlNCCModel();
	///   using (JlImage template = new JlImage("template.png"))
	///   {
	///       model.CreateNccModel(template, 3, -0.79, 1.57, 0.0175, "use_polarity");
	///   }
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>角度步长过小会让训练量与匹配耗时按 1/step 膨胀；先 DetermineNccModelParams 拿推荐值可免拍脑袋。</para>
	///   <para><b>相关算子</b>FindNccModel、ClearNccModel</para>
	/// </remarks>
	public void CreateNccModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, string metric)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(947);
		JlNativeApi.Store(proc, 1, template);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>Find the best matches of multiple NCC models.</summary>
	/// <param name="image">待搜图。Input image in which the model should be found.</param>
	/// <param name="modelIDs">模型数组；方法体内 ConcatArray 拼成句柄元组传给原生（id 1958），数组顺序决定 model 输出的编号基准。Handle of the models.</param>
	/// <param name="angleStart">各模型最小角（弧度）；JlTuple 可按模型给多值 [逐模型对应语义待实测]。Smallest rotation of the models. Default: -0.39</param>
	/// <param name="angleExtent">角度范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="minScore">各模型最低得分。Minimum score of the instances of the models to be found. Default: 0.8</param>
	/// <param name="numMatches">每模型要找的实例数（0 = 全部）。Number of instances of the models to be found (or 0 for all matches). Default: 1</param>
	/// <param name="maxOverlap">最大重叠比例。Maximum overlap of the instances of the models to be found. Default: 0.5</param>
	/// <param name="subPixel">亚像素开关（英文说明：非 'none' 即启用）。Subpixel accuracy if not equal to 'none'. Default: "true"</param>
	/// <param name="numLevels">匹配金字塔层数。Number of pyramid levels used in the matching (and lowest pyramid level to use if $|NumLevels| = 2$). Default: 0</param>
	/// <param name="row">各实例参考点行坐标（DOUBLE 装载）。Row coordinate of the found instances of the models.</param>
	/// <param name="column">各实例参考点列坐标。Column coordinate of the found instances of the models.</param>
	/// <param name="angle">各实例角度，弧度。Rotation angle of the found instances of the models.</param>
	/// <param name="score">各实例得分。Score of the found instances of the models.</param>
	/// <param name="model">各实例来自哪个模型（INTEGER 装载）。Index of the found instances of the models.</param>
	/// <remarks>
	///   <para><b>功能说明</b>一次调用在图内搜多个 NCC 模型（原生 id 1958）。五个 out 元组等长 = 全部模型合计找到的实例数；model 与其余四个输出按序对应，用来区分实例归属。</para>
	///   <para><b>参数取向</b>model 的编号基准是 modelIDs 数组拼接后的序 [从 0 还是从 1 起待实测]；跨模型重叠时的抑制策略与单模型版一致 [待实测]。所有 JlTuple 入参都走 Store+UnpinTuple 钉传。</para>
	///   <para><b>与相邻算子的取舍</b>只搜一个模型时用实例方法 FindNccModels(image, ...) 或 FindNccModel；多型号混线共图时本算子省一次全图扫描。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel m1 = new JlNCCModel("a.ncm"))
	///   using (JlNCCModel m2 = new JlNCCModel("b.ncm"))
	///   using (JlImage image = new JlImage("frame001.png"))
	///   {
	///       JlNCCModel.FindNccModels(image, new JlNCCModel[] { m1, m2 },
	///           -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///           out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model);
	///       int which = model.Length &gt; 0 ? (int)model.D : -1;
	///   }
	///   </code>
	///   <para><b>资源与坑</b>modelIDs 中若有已 Clear/Dispose 的句柄，整次调用的失败形态 [待实测]；ConcatArray 生成的临时句柄元组在调用后 Unpin，不需调用方处理。</para>
	///   <para><b>相关算子</b>CreateNccModel、ClearNccModel</para>
	/// </remarks>
	public static void FindNccModels(JlImage image, JlNCCModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(1958);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, minScore);
		JlNativeApi.Store(proc, 4, numMatches);
		JlNativeApi.Store(proc, 5, maxOverlap);
		JlNativeApi.Store(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(image);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>
	///   Find the best matches of multiple NCC models.
	/// </summary>
	/// <param name="image">待搜图。Input image in which the model should be found.</param>
	/// <param name="angleStart">最小角，弧度。Smallest rotation of the models. Default: -0.39</param>
	/// <param name="angleExtent">角度范围，弧度。Extent of the rotation angles. Default: 0.79</param>
	/// <param name="minScore">最低得分。Minimum score of the instances of the models to be found. Default: 0.8</param>
	/// <param name="numMatches">实例数（0 = 全部）。Number of instances of the models to be found (or 0 for all matches). Default: 1</param>
	/// <param name="maxOverlap">最大重叠比例。Maximum overlap of the instances of the models to be found. Default: 0.5</param>
	/// <param name="subPixel">亚像素开关。Subpixel accuracy if not equal to 'none'. Default: "true"</param>
	/// <param name="numLevels">金字塔层数标量。Number of pyramid levels used in the matching (and lowest pyramid level to use if $|NumLevels| = 2$). Default: 0</param>
	/// <param name="row">各实例参考点行坐标。Row coordinate of the found instances of the models.</param>
	/// <param name="column">各实例参考点列坐标。Column coordinate of the found instances of the models.</param>
	/// <param name="angle">各实例角度，弧度。Rotation angle of the found instances of the models.</param>
	/// <param name="score">各实例得分。Score of the found instances of the models.</param>
	/// <param name="model">实例归属的模型索引（INTEGER 装载；本重载只挂一个模型，取值 [应为恒 1，待实测]）。Index of the found instances of the models.</param>
	/// <remarks>
	///   <para><b>功能说明</b>find_ncc_models（原生 id 1958）的实例单模型入口：把 this 作为唯一模型句柄传入，五个标量参数经 StoreD/StoreI/StoreS 直写。五个 out 元组等长 = 找到的实例数。</para>
	///   <para><b>与相邻算子的取舍</b>与 FindNccModel（id 945）几乎同参——差别在输出多一路 model 索引且底层走批量算子；单模型场景两者可互换，批量入口未来改成多模型时改本方法调用更顺 [两者性能差异待实测]。多模型合搜用静态 FindNccModels(image, modelIDs, ...)。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   using (JlImage image = new JlImage("frame001.png"))
	///   {
	///       model.FindNccModels(image, -0.39, 0.79, 0.75, 0, 0.4, "true", 0,
	///           out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple which);
	///       int found = row.Length; // numMatches = 0：全取，个数不定
	///   }
	///   </code>
	///   <para><b>资源与坑</b>numMatches=0 时输出长度无上限，宿主循环别按定长预分配。</para>
	///   <para><b>相关算子</b>CreateNccModel、ClearNccModel</para>
	/// </remarks>
	public void FindNccModels(JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(1958);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   Return the region used to create an NCC model.
	/// </summary>
	/// <returns>建模时的模板域区域（新句柄，iconic 输出槽 1）。Model region of the NCC model.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>取回 CreateNccModel 当时由模板 domain 生成的模型区域（原生 id 1961）——即"哪些像素真的进了模板"，可直接用于核对 ReduceDomain 是否抠对了位置。</para>
	///   <para><b>与相邻算子的取舍</b>要参考点坐标用 GetNccModelOrigin；要看金字塔/角度参数用 GetNccModelParams；本算子只回答"建模域长什么样"。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   using (JlNCCModel model = new JlNCCModel("bottle.ncm"))
	///   using (JlRegion dom = model.GetNccModelRegion())
	///   {
	///       double px = dom.Area.D; // 真正进入模板的像素数
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回的 JlRegion 是新句柄，用毕 Dispose。</para>
	/// </remarks>
	public JlRegion GetNccModelRegion()
	{
		IntPtr proc = JlNativeApi.PreCall(1961);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}
}
