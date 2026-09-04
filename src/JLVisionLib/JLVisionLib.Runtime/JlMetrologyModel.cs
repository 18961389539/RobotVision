using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of a metrology model.</summary>
[Serializable]
public class JlMetrologyModel : JlHandle, ISerializable, ICloneable
{
	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMetrologyModel(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>从 <see cref="JlHandle"/> 句柄包装构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMetrologyModel(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("metrology_model");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMetrologyModel obj)
	{
		obj = new JlMetrologyModel(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMetrologyModel[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlMetrologyModel[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlMetrologyModel(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   Read a metrology model from a file.
	/// </summary>
	/// <param name="fileName">File name.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从文件读取计量模型（原生 id 777）并让新对象持有其句柄。读入的模型包含已 add 的各 metrology object 及其参数。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要在既有对象上换内容用 ReadMetrologyModel（同为 id 777，但先 Dispose 再原地装入）；本构造器产生独立新对象。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("boremm.dat");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>文件不存在时的异常形态 [待实测]；用毕 Dispose。</para>
	/// </remarks>
	public JlMetrologyModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(777);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create the data structure that is needed to measure geometric shapes.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建空的 2D 亚像素几何计量模型（原生 id 798）。模型内可含多条测量对象（线/圆/椭圆/矩形），ApplyMetrologyModel 时对每条对象布置一排排微卡尺取边并整体拟合。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>建好后的第一步应是 SetMetrologyModelImageSize：测量区域的裁剪与布置基准都依赖图像尺寸，不设就 Add 对象的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>单个/成对边界快速定位用 JlMeasure 1D 卡尺；要拟合完整几何（圆心、半径、直线端点）并利用多边缘点平均降噪时用本类。在既有对象上重建空模型用 CreateMetrologyModel。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>句柄非引用类型语义：model 传给别处只传引用；独立副本用 Clone 或 CopyMetrologyModel。用毕 Dispose 或 ClearMetrologyModel（后者不清托管句柄，见其注释）。</para>
	/// </remarks>
	public JlMetrologyModel()
	{
		IntPtr proc = JlNativeApi.PreCall(798);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeMetrologyModel();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMetrologyModel(SerializationInfo info, StreamingContext context)
	{
		DeserializeMetrologyModel((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>Serialize object to binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把计量模型序列化后的字节流写入 Stream：内部先 SerializeMetrologyModel（id 774）取 byte[]，再落流。不改动本对象句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>落盘用 WriteMetrologyModel；只要字节数组用 SerializeMetrologyModel；跨进程配静态 Deserialize 使用。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       model.Serialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>传入流的开关由调用方负责。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeMetrologyModel(), stream);
	}

	/// <summary>Deserialize object from binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>静态方法：从流读出数据并反序列化（id 773），返回持有新句柄的 JlMetrologyModel；不修改任何既有对象。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>流位置须在数据起点（写后读回常需 Position = 0）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   JlMetrologyModel restored;
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       model.Serialize(ms);
	///       ms.Position = 0;
	///       restored = JlMetrologyModel.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>两份对象各自 Dispose。</para>
	/// </remarks>
	public new static JlMetrologyModel Deserialize(Stream stream)
	{
		JlMetrologyModel hMetrologyModel = new JlMetrologyModel();
		hMetrologyModel.DeserializeMetrologyModel(JlSerializationBuffer.ReadFromStream(stream));
		return hMetrologyModel;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>深拷贝整个模型（含所有 metrology object 与参数）：走 SerializeMetrologyModel→DeserializeMetrologyModel 的字节数组往返，返回独立新句柄的新对象。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只想在模型内复制几条对象用 CopyMetrologyObject（同模型内、返回新 index）；跨线程各用一份配置：Clone 后互不干扰。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlMetrologyModel spare = model.Clone();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>两份句柄须各自 Dispose；序列化往返耗时，热路径慎用。</para>
	/// </remarks>
	public new JlMetrologyModel Clone()
	{
		byte[] data = SerializeMetrologyModel();
		JlMetrologyModel obj = new JlMetrologyModel();
		obj.DeserializeMetrologyModel(data);
		return obj;
	}



	/// <summary>
	///   Query the result contour of a metrology object.
	/// </summary>
	/// <param name="index">Index of the metrology object. Default: 0</param>
	/// <param name="instance">Instance of the metrology object. Default: "all"</param>
	/// <param name="resolution">Distance between neighboring contour points. Default: 1.5</param>
	/// <returns>Result contour for the given metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>GetMetrologyObjectResultContour 的 JlTuple 重载：同原生 id 768，index/instance 可传多个值一次取多条拟合轮廓（合并进同一个 JlXLDCont [合并方式待实测]）；语义详见 int 重载注释。</para>
	///   <para><b>重载二义性</b><c>JlTuple</c> 与 <c>int</c>、<c>string</c> 之间是双向隐式转换，所以
	///   <c>GetMetrologyObjectResultContour(tupleIndex, "all", 1.5)</c> 会同时匹配本重载与
	///   <c>(int, string, double)</c> 重载并报 CS0121。走本重载时<b>index 与 instance 都要是 JlTuple</b>
	///   （身份转换优于用户定义转换，二义即消）；走 int 重载时两个参数都用 CLR 原生类型，不要混着传。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple idx = new int[] { 0, 1 };
	///   JlTuple inst = new JlTuple("all");
	///   JlXLDCont contours = model.GetMetrologyObjectResultContour(idx, inst, 1.5);
	///   </code>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectResultContour(JlTuple index, JlTuple instance, double resolution)
	{
		IntPtr proc = JlNativeApi.PreCall(768);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, instance);
		JlNativeApi.StoreD(proc, 3, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(instance);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Query the result contour of a metrology object.
	/// </summary>
	/// <param name="index">Index of the metrology object. Default: 0</param>
	/// <param name="instance">Instance of the metrology object. Default: "all"</param>
	/// <param name="resolution">Distance between neighboring contour points. Default: 1.5</param>
	/// <returns>Result contour for the given metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>取某条 metrology object 上次 Apply 后的拟合结果轮廓（原生 id 768）：按 resolution 给定的相邻点间距把拟合出的直线/圆/椭圆/矩形边界重采样成 XLD 轮廓，返回新 JlXLDCont（iconic 输出，单独句柄）。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>必须先对该模型执行过 ApplyMetrologyModel，否则无结果可言 [未 apply 时行为待实测]；index 是 AddMetrologyObject* 的返回值。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要参数化结果（圆心/半径/端点、不确定度）用 GetMetrologyObjectResult；要看实际采到的边缘点用 GetMetrologyObjectMeasures；本算子用于把拟合形状转成轮廓做后续区域运算。</para>
	///   <para><b>参数取向</b></para>
	///   <para>instance 传 "all" 取全部实例，或传实例号字符串 [编号从几开始待实测]；resolution 越小点越密，与像素尺寸相当或略小即可。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlXLDCont fitted = model.GetMetrologyObjectResultContour(circle, "all", 1.5);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回的 JlXLDCont 是新句柄，用毕 Dispose；JlTuple 重载可批量取多 index。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectResultContour(int index, string instance, double resolution)
	{
		IntPtr proc = JlNativeApi.PreCall(768);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, instance);
		JlNativeApi.StoreD(proc, 3, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Alignment of a metrology model.
	/// </summary>
	/// <param name="row">Row coordinate of the alignment. Default: 0</param>
	/// <param name="column">Column coordinate of the alignment. Default: 0</param>
	/// <param name="angle">Rotation angle of the alignment. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>为整个模型设定对齐位姿（原生 id 769）：后续 ApplyMetrologyModel 时，各 metrology object 的测量区域按 (row, column, angle) 平移+旋转后再布置，模型内存储的名义几何本身不变。原地修改，不换句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>TransformMetrologyObject 永久改写对象几何；Align 只在取边时临时挪动测量区域，可反复设定/清除 [取消方式待实测]，适合"先定位后计量"的流水。多个 index 需不同对齐量时传 JlTuple 数组 [逐对象对齐是否支持待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple row = 0.0;
	///   JlTuple column = 0.0;
	///   JlTuple angle = 0.0;
	///   model.AlignMetrologyModel(row, column, angle);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>angle 单位为弧度、绕何点旋转 [绕 (row,column) 还是图像中心，待实测]；double 重载同 id，仅 StoreD 传参。</para>
	/// </remarks>
	public void AlignMetrologyModel(JlTuple row, JlTuple column, JlTuple angle)
	{
		IntPtr proc = JlNativeApi.PreCall(769);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, angle);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(angle);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Alignment of a metrology model.
	/// </summary>
	/// <param name="row">Row coordinate of the alignment. Default: 0</param>
	/// <param name="column">Column coordinate of the alignment. Default: 0</param>
	/// <param name="angle">Rotation angle of the alignment. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>AlignMetrologyModel 的 double 重载：同原生 id 769、原地设定模型对齐位姿；差异仅在标量经 StoreD 传参。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.AlignMetrologyModel(242.0, 318.0, 0.02);
	///   </code>
	/// </remarks>
	public void AlignMetrologyModel(double row, double column, double angle)
	{
		IntPtr proc = JlNativeApi.PreCall(769);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, angle);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Add a metrology object to a metrology model.
	/// </summary>
	/// <param name="shape">Type of the metrology object to be added. Default: "circle"</param>
	/// <param name="shapeParam">Parameters of the metrology object to be added.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>通用入口：向模型添加任意类型的 metrology object（原生 id 770），返回其 index（int）。shape 指定类型（默认 "circle"，可选集合 [待实测]），shapeParam 为该类型展平后的几何参数序列——圆为 (row, column, radius)、直线为两点坐标等，与各自专用 Add 算子的参数对应。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>类型在编译期已知时用 AddMetrologyObjectCircleMeasure / Line / Ellipse / Rectangle2（参数拆开、不易错）；类型由配置文件驱动的循环里用本算子。返回的 index 语义一致：之后 Set/GetMetrologyObjectParam、取结果、ClearMetrologyObject 都按该 index 寻址，"all" 表示全部对象。</para>
	///   <para><b>参数取向</b></para>
	///   <para>measureLength1 是垂直于边界方向的半长（微卡尺搜索行程）、measureLength2 是沿边界切向的半长（平均带），measureSigma 平滑、measureThreshold 最小边缘幅值；genParamName/genParamValue 成对给附加参数（如 num_instances、max_deviation、num_measurements，名称集合 [待实测]）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple shape = "circle";
	///   JlTuple shapeParam = new double[] { 240.0, 320.0, 100.0 };
	///   JlTuple len1 = 20.0;
	///   JlTuple len2 = 5.0;
	///   JlTuple sigma = 1.0;
	///   JlTuple thresh = 30.0;
	///   int idx = model.AddMetrologyObjectGeneric(shape, shapeParam, len1, len2, sigma, thresh, new JlTuple(), new JlTuple());
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本库无 prepare_metrology_model / find_metrology（Grep 确认）：添加后直接 ApplyMetrologyModel 即可生效，重复建模用 ClearMetrologyObject("all") 再重加。</para>
	/// </remarks>
	public int AddMetrologyObjectGeneric(JlTuple shape, JlTuple shapeParam, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(770);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, shape);
		JlNativeApi.Store(proc, 2, shapeParam);
		JlNativeApi.Store(proc, 3, measureLength1);
		JlNativeApi.Store(proc, 4, measureLength2);
		JlNativeApi.Store(proc, 5, measureSigma);
		JlNativeApi.Store(proc, 6, measureThreshold);
		JlNativeApi.Store(proc, 7, genParamName);
		JlNativeApi.Store(proc, 8, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(shape);
		JlNativeApi.UnpinTuple(shapeParam);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a metrology object to a metrology model.
	/// </summary>
	/// <param name="shape">Type of the metrology object to be added. Default: "circle"</param>
	/// <param name="shapeParam">Parameters of the metrology object to be added.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>AddMetrologyObjectGeneric 的标量重载：同原生 id 770、返回新对象 index；shape 为 string、四个 measure 参数为 double（StoreS/StoreD 传参），仅 shapeParam 与 genParam 仍为 JlTuple。index 语义与参数取向详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int idx = model.AddMetrologyObjectGeneric("circle", new double[] { 240.0, 320.0, 100.0 },
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public int AddMetrologyObjectGeneric(string shape, JlTuple shapeParam, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(770);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, shape);
		JlNativeApi.Store(proc, 2, shapeParam);
		JlNativeApi.StoreD(proc, 3, measureLength1);
		JlNativeApi.StoreD(proc, 4, measureLength2);
		JlNativeApi.StoreD(proc, 5, measureSigma);
		JlNativeApi.StoreD(proc, 6, measureThreshold);
		JlNativeApi.Store(proc, 7, genParamName);
		JlNativeApi.Store(proc, 8, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(shapeParam);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Get parameters that are valid for the entire metrology model.
	/// </summary>
	/// <param name="genParamName">Name of the generic parameter. Default: "camera_param"</param>
	/// <returns>Value of the generic parameter.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取作用于整个模型的参数（原生 id 771），只读、不改句柄。已知名："camera_param"（默认值，来自英文参数说明）、"image_size"（对应 SetMetrologyModelImageSize 的 [width, height]），其余可用名 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>本类未提供 JlMetrologyModel.GetMetrologyModelParam 之外的模型级查询；逐对象参数在 GetMetrologyObjectParam。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple size = model.GetMetrologyModelParam("image_size");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>无 string[]/JlTuple 批量重载，取多个名需多次调用。</para>
	/// </remarks>
	public JlTuple GetMetrologyModelParam(string genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(771);
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
	///   Set parameters that are valid for the entire metrology model.
	/// </summary>
	/// <param name="genParamName">Name of the generic parameter. Default: "camera_param"</param>
	/// <param name="genParamValue">Value of the generic parameter. Default: []</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设定模型级通用参数（原生 id 772），原地修改，不换句柄。genParamName 已知约定含 "image_size"（值 [width, height]）与 "camera_param"（默认名）等，完整集合 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>图像尺寸推荐用专用 SetMetrologyModelImageSize（id 797，意图更明确）；本算子用于其覆盖不到的模型参数。string 值重载（同 id）适合单值字符串参数，其余同本重载。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   JlTuple size = new int[] { 640, 480 };
	///   model.SetMetrologyModelParam("image_size", size);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>image_size 影响后续测量区域在图像内的裁剪基准与距离计算基准，应在 Add 对象与 Apply 之前设定。</para>
	/// </remarks>
	public void SetMetrologyModelParam(string genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(772);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set parameters that are valid for the entire metrology model.
	/// </summary>
	/// <param name="genParamName">Name of the generic parameter. Default: "camera_param"</param>
	/// <param name="genParamValue">Value of the generic parameter. Default: []</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>SetMetrologyModelParam 的 string 值重载：同原生 id 772、原地生效，仅值以 StoreS 传单字符串（适合取值为字符串的参数）；参数名集合见 JlTuple 值重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelParam("color_type", "byte");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>"color_type" 是否为合法模型参数 [待实测]。</para>
	/// </remarks>
	public void SetMetrologyModelParam(string genParamName, string genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(772);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.StoreS(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Deserialize a serialized metrology model.
	/// </summary>
	/// <param name="serializedItemHandle">Handle of the serialized item.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 SerializeMetrologyModel 的 byte[] 还原模型（原生 id 773）。方法体先 Dispose() 旧句柄再把新句柄装入本对象——原地替换；缓冲区 JlSerializationBuffer 在调用结束即释放。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   byte[] data = model.SerializeMetrologyModel();
	///   JlMetrologyModel restored = new JlMetrologyModel();
	///   restored.DeserializeMetrologyModel(data);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>Clone / Deserialize(Stream) / ISerializable 构造都经由本方法；数据非法时旧句柄已释放 [失败后状态待实测]。</para>
	/// </remarks>
	public void DeserializeMetrologyModel(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(773);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   Serialize a metrology model.
	/// </summary>
	/// <returns>Handle of the serialized item.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把模型（含全部 metrology object 与已拟合结果 [是否含结果待实测]）序列化为 byte[]（原生 id 774）。只读，不动句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>落盘 WriteMetrologyModel；写流 Serialize；还原用 DeserializeMetrologyModel（注意它是原地替换）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   byte[] data = model.SerializeMetrologyModel();
	///   </code>
	/// </remarks>
	public byte[] SerializeMetrologyModel()
	{
		IntPtr proc = JlNativeApi.PreCall(774);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   Transform metrology objects of a metrology model, e.g., for alignment.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="row">Translation in row direction.</param>
	/// <param name="column">Translation in column direction.</param>
	/// <param name="phi">Rotation angle.</param>
	/// <param name="mode">Mode of the transformation. Default: "absolute"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>对指定 metrology object 的几何做平移+旋转变换（原生 id 775），原地生效：mode 为 "absolute" 时以给定值作为对象新位姿，"relative" 时在现值上叠加 [叠加基准待实测]。与 AlignMetrologyModel 不同，变换改的是对象的名义几何本身。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>临时挪动测量区域用 Align（每帧可重设）；本算子用于把示教好的模型整体搬到位姿基准处、或按标定结果固化布局。批量：index/row/column/phi 传等长数组可按对象逐个给值 [并行语义待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple index = "all";
	///   JlTuple row = 0.0;
	///   JlTuple column = 10.0;
	///   JlTuple phi = 0.0;
	///   JlTuple mode = "relative";
	///   model.TransformMetrologyObject(index, row, column, phi, mode);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>phi 为弧度；变换后原 apply 结果是否失效 [待实测]；string 重载同 id，仅标量传参路径不同。</para>
	/// </remarks>
	public void TransformMetrologyObject(JlTuple index, JlTuple row, JlTuple column, JlTuple phi, JlTuple mode)
	{
		IntPtr proc = JlNativeApi.PreCall(775);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, row);
		JlNativeApi.Store(proc, 3, column);
		JlNativeApi.Store(proc, 4, phi);
		JlNativeApi.Store(proc, 5, mode);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(mode);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Transform metrology objects of a metrology model, e.g., for alignment.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="row">Translation in row direction.</param>
	/// <param name="column">Translation in column direction.</param>
	/// <param name="phi">Rotation angle.</param>
	/// <param name="mode">Mode of the transformation. Default: "absolute"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>TransformMetrologyObject 的标量重载：同原生 id 775、原地变换对象几何；参数一次给一个标量（StoreS/StoreD），批量语义见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.TransformMetrologyObject("all", 0.0, 10.0, 0.0, "relative");
	///   </code>
	/// </remarks>
	public void TransformMetrologyObject(string index, double row, double column, double phi, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(775);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.StoreD(proc, 2, row);
		JlNativeApi.StoreD(proc, 3, column);
		JlNativeApi.StoreD(proc, 4, phi);
		JlNativeApi.StoreS(proc, 5, mode);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Write a metrology model to a file.
	/// </summary>
	/// <param name="fileName">File name.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把模型以 Vision 二进制格式写入文件（原生 id 776）。只读，不动句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>读回：构造器 JlMetrologyModel(fileName)（新对象）或 ReadMetrologyModel（原地替换）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.WriteMetrologyModel("boremm.dat");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>路径/目录错误的表现 [待实测]。</para>
	/// </remarks>
	public void WriteMetrologyModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(776);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read a metrology model from a file.
	/// </summary>
	/// <param name="fileName">File name.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从文件读模型（原生 id 777）：先 Dispose() 旧句柄、再装入文件内容的新句柄——原地替换本对象；要独立新对象用构造器 JlMetrologyModel(fileName)。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.ReadMetrologyModel("boremm.dat");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>读取失败时本对象已成空壳 [失败后状态待实测]。</para>
	/// </remarks>
	public void ReadMetrologyModel(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(777);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Copy a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <returns>Handle of the copied metrology model.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>复制模型（原生 id 778）：index 选出被复制的 metrology object 子集（"all" 或 index 数组），生成一个新模型。注意返回类型是 int——方法体用 LoadI 把输出按整数读出，即返回的是新模型的原生句柄值而不是 JlMetrologyModel 对象，且复制出的新模型不会自动释放 [资源归属待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>整模型对象级深拷贝也可用 Clone（序列化往返、返回包装好的对象）；本算子直接调原生复制，能按 index 挑选子集。CopyMetrologyObject 则是在同一模型内复制对象、返回新 index。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple index = new int[] { circle };
	///   int copyHandle = model.CopyMetrologyModel(index);
	///   JlMetrologyModel copy = new JlMetrologyModel(new IntPtr(copyHandle));
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>手工包装出的 copy 须自行 Dispose；新模型的 image_size 是否随复制保留 [待实测]。</para>
	/// </remarks>
	public int CopyMetrologyModel(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(778);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Copy a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <returns>Handle of the copied metrology model.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CopyMetrologyModel 的 string 重载：同原生 id 778、返回新模型句柄的 int 值；index 传单对象索引或 "all" 的字符串形式（StoreS）。详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int copyHandle = model.CopyMetrologyModel("all");
	///   JlMetrologyModel copy = new JlMetrologyModel(new IntPtr(copyHandle));
	///   </code>
	/// </remarks>
	public int CopyMetrologyModel(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(778);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Copy metrology metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <returns>Indices of the copied metrology objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在同一模型内复制 metrology object（原生 id 779）：把 index 指定的对象再复制一份，返回新对象 index 的 INTEGER JlTuple（LoadNew 读出，可含多值）。原对象与新对象并存、互不共享参数。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要"独立模型"用 CopyMetrologyModel（返回新模型句柄的 int 值）；本算子适合按同一基准形状派生多条测量对象再 TransformMetrologyObject 挪位。string 重载同 id 但用 LoadI 只回一个 int——复制多条时可能只取首值 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple newIndices = model.CopyMetrologyObject(new int[] { circle });
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>不用的新对象用 ClearMetrologyObject(index) 删除。</para>
	/// </remarks>
	public JlTuple CopyMetrologyObject(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(779);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Copy metrology metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <returns>Indices of the copied metrology objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CopyMetrologyObject 的 string 重载：同原生 id 779，但输出经 LoadI 读成 int——复制单个对象时给新 index，复制多个 [结果如何折叠待实测]。批量取值请用 JlTuple 重载。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   int newIndex = model.CopyMetrologyObject("all"); // 多个新 index 时该重载只回一个 int [待实测]
	///   </code>
	/// </remarks>
	public int CopyMetrologyObject(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(779);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Get the number of instances of the metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: 0</param>
	/// <returns>Number of Instances of the metrology objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>GetMetrologyObjectNumInstances 的 JlTuple 重载：同原生 id 780，可传多个 index，输出经无类型 LoadNew 得 JlTuple——每对象一个计数的元组。语义详见 int 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple counts = model.GetMetrologyObjectNumInstances(new int[] { 0, 1 });
	///   </code>
	/// </remarks>
	public JlTuple GetMetrologyObjectNumInstances(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(780);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get the number of instances of the metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: 0</param>
	/// <returns>Number of Instances of the metrology objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>取某条 metrology object 上次 ApplyMetrologyModel 后找到的实例数（原生 id 780，输出经 LoadD 读成 double——计数以浮点返回是生成绑定的特点）。实例数由对象的 num_instances 参数与图像中实际可拟合的几何个数决定。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>本方法是"结果数组长度"的换算基准：GetMetrologyObjectResult 把 index × instance × 参数 的结果压进同一条 JlTuple，取第 i 个实例的第 j 个参数必须先用这里的计数核对越界 [结果排布顺序待实测]。JlTuple 重载可批量取多 index 的计数。</para>
	///   <para><b>参数取向</b></para>
	///   <para>index 传 AddMetrologyObject* 返回的整数；未 Apply 前调用返回 0 还是报错 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   double num = model.GetMetrologyObjectNumInstances(circle);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>计数与 Apply 使用同一模型状态：再次 Apply 后结果整体刷新。</para>
	/// </remarks>
	public double GetMetrologyObjectNumInstances(int index)
	{
		IntPtr proc = JlNativeApi.PreCall(780);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Get the results of the measurement of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology object. Default: 0</param>
	/// <param name="instance">Instance of the metrology object. Default: "all"</param>
	/// <param name="genParamName">Name of the generic parameter. Default: "result_type"</param>
	/// <param name="genParamValue">Value of the generic parameter. Default: "all_param"</param>
	/// <returns>Result values.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>GetMetrologyObjectResult 的全 JlTuple 重载：同原生 id 781，index 与 instance 均可传多值做批量查询（Store+UnpinTuple 透传）。结果内容与排布详见 (int, string, JlTuple, JlTuple) 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple idx = 0;
	///   JlTuple inst = "all";
	///   JlTuple names = "result_type";
	///   JlTuple vals = "all_param";
	///   JlTuple values = model.GetMetrologyObjectResult(idx, inst, names, vals);
	///   </code>
	/// </remarks>
	public JlTuple GetMetrologyObjectResult(JlTuple index, JlTuple instance, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(781);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, instance);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(instance);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get the results of the measurement of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology object. Default: 0</param>
	/// <param name="instance">Instance of the metrology object. Default: "all"</param>
	/// <param name="genParamName">Name of the generic parameter. Default: "result_type"</param>
	/// <param name="genParamValue">Value of the generic parameter. Default: "all_param"</param>
	/// <returns>Result values.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按 index+instance 取某条 metrology object 上次 ApplyMetrologyModel 的拟合结果（原生 id 781）：genParamName "result_type" 配 genParamValue 指定要哪类量（默认 "all_param"，如不确定度等其它取值集合 [待实测]）。圆给 (row, column, radius[, 起止角])、直线给两端点等，与各自 Add 算子的几何参数量一致。</para>
	///   <para><b>约束或前提</b>必须先 Apply 过；index 是 AddMetrologyObject* 的返回值，instance 用 "all" 或实例号 [编号起点待实测]。多实例/多参数时全部压进同一条返回 JlTuple，排布顺序 [待实测]——先用 GetMetrologyObjectNumInstances 核对数量再按下标取，防越界错位。</para>
	///   <para><b>与相邻算子的取舍</b>要参数化数值用本方法；要实际采到的边缘点用 GetMetrologyObjectMeasures；要把拟合形状转成轮廓做区域运算用 GetMetrologyObjectResultContour。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlTuple values = model.GetMetrologyObjectResult(circle, "all", "result_type", "all_param");
	///   double radiusGuess = values.D; // 首值仅为示意，真实排布 [待实测]
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本重载 index/instance 经 StoreI/StoreS 直写、genParam 对仍钉传（Store+UnpinTuple）；全 JlTuple 重载可批量多 index，见其注释。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectResult(int index, string instance, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(781);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, instance);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get the measure regions and the results of the edge location for the metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="transition">Select light/dark or dark/light edges. Default: "all"</param>
	/// <param name="row">Row coordinates of the measured edges.</param>
	/// <param name="column">Column coordinates of the measured edges.</param>
	/// <returns>Rectangular XLD Contours of measure regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>取上次 ApplyMetrologyModel 实际采到的边缘点及其测量区域（原生 id 782）：out row/column 为所有被选边缘点的亚像素坐标（DOUBLE 装载、逐点一项），返回的 JlXLDCont 是每个测量区域的矩形轮廓（iconic 输出）。这是"拟合之前"的原始观测，用于诊断边缘质量。</para>
	///   <para><b>约束或前提</b>必须先 Apply；transition 按极性筛点（"all"/"positive"/"negative"，极性定义 [待实测]）。index 传 JlTuple 可一次汇总多条对象 [多对象的拼接顺序待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要最终几何量用 GetMetrologyObjectResult；要拟合轮廓用 GetMetrologyObjectResultContour；本算子回答"卡尺到底采到了哪些点"——剔除离群点后不自洽、圆度超差等判定都以它为原料。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int line = model.AddMetrologyObjectLineMeasure(100.0, 100.0, 100.0, 500.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlXLDCont regions = model.GetMetrologyObjectMeasures(line, "all", out JlTuple row, out JlTuple column);
	///   int nEdges = row.Length; // 与 column 等长
	///   regions.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 JlXLDCont 是新句柄，用毕 Dispose；未采到点时各 out 为空元组还是报错 [待实测]。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectMeasures(JlTuple index, string transition, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(782);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, transition);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Get the measure regions and the results of the edge location for the metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="transition">Select light/dark or dark/light edges. Default: "all"</param>
	/// <param name="row">Row coordinates of the measured edges.</param>
	/// <param name="column">Column coordinates of the measured edges.</param>
	/// <returns>Rectangular XLD Contours of measure regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>GetMetrologyObjectMeasures 的 string-index 重载（原生 id 782）：语义、输出契约（返回测量区域 JlXLDCont + 等长的边缘点 row/column 元组）详见 JlTuple 重载注释；差异仅 index 经 StoreS 传 "all" 或单索引字符串。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlXLDCont regions = model.GetMetrologyObjectMeasures("all", "all", out JlTuple row, out JlTuple column);
	///   regions.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 JlXLDCont 用毕 Dispose；对空模型（无对象或从未 Apply）调用 [待实测]。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectMeasures(string index, string transition, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(782);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, transition);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Measure and fit the geometric shapes of all metrology objects of a metrology model.
	/// </summary>
	/// <param name="image">Input image.</param>
	/// <remarks>
	///   <para><b>功能说明</b>2D 计量的执行入口（原生 id 783）：对模型内每条 metrology object 沿名义几何布一排排微卡尺、在 image 上取边、再做整体拟合（圆/直线/椭圆/矩形），结果暂存于模型句柄内，经 GetMetrologyObject* 系列取回。每帧调一次，重复 Apply 会整体刷新旧结果。</para>
	///   <para><b>约束或前提</b>建模顺序硬约束：SetMetrologyModelImageSize 必须先设（裁剪与布置基准），再 Add 对象、可选 SetMetrologyObjectParam（num_instances、max_deviation、num_measurements 等 [名称集合待实测]），最后 Apply。HALCON 的 find_metrology / prepare_metrology_model 在本库无绑定 [Grep 确认]：取边与拟合并成一步，没有"只布卡尺不拟合"的中间态。</para>
	///   <para><b>与相邻算子的取舍</b>本方法无返回值，成败要靠 GetMetrologyObjectNumInstances / 结果数组长度判断；只想拿单点/成对边界不上几何拟合时用 JlMeasure 1D 卡尺更快。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   double n = model.GetMetrologyObjectNumInstances(circle);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>image 尺寸与建模 image_size 不符时的行为（裁剪/报错）[待实测]；AlignMetrologyModel 设定的位姿在每次 Apply 生效，忘重设会沿用上帧对齐量。</para>
	/// </remarks>
	public void ApplyMetrologyModel(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(783);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   Get the indices of the metrology objects of a metrology model.
	/// </summary>
	/// <returns>Indices of the metrology objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>列出模型里现存 metrology object 的 index（原生 id 784，输出按 INTEGER 装载成 JlTuple）。Add 返回的 index 与这里回读的集合是同一套编号——ClearMetrologyObject 删掉某条后本方法立即少一项，是寻址是否错位的唯一事实来源。</para>
	///   <para><b>与相邻算子的取舍</b>硬编码 index 常量易在示教增删对象后悄悄错位；从配置文件重建模型或运行期删过对象时，先调本方法核对再逐个 GetMetrologyObjectParam/Result。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int line = model.AddMetrologyObjectLineMeasure(100.0, 100.0, 100.0, 500.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple indices = model.GetMetrologyObjectIndices();
	///   bool present = indices.Length == 1; // 应为 { line }
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>空模型返回空元组；index 是否随 Clear 后复用编号 [待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectIndices()
	{
		IntPtr proc = JlNativeApi.PreCall(784);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Reset all fuzzy parameters and fuzzy functions of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>把指定 metrology object 的模糊参数与模糊隶属函数整体复位为默认（原生 id 785），原地生效、不换句柄。影响 Apply 时对采边结果的模糊加权过滤。</para>
	///   <para><b>与相邻算子的取舍</b>只动模糊这一族参数用本方法；要连 num_instances、measure 参数等全部回默认用 ResetMetrologyObjectParam（id 786）；想保留默认再看当前值用 GetMetrologyObjectFuzzyParam。复位对象级设置不影响 Add 时传入的名义几何。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple idx = new int[] { circle };
	///   model.ResetMetrologyObjectFuzzyParam(idx);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>index 传 JlTuple 可批量（Store+UnpinTuple 钉传）；string 重载同 id。</para>
	/// </remarks>
	public void ResetMetrologyObjectFuzzyParam(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(785);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Reset all fuzzy parameters and fuzzy functions of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>ResetMetrologyObjectFuzzyParam 的 string 重载（原生 id 785）：index 经 StoreS 传 "all" 或单索引字符串，复位该范围对象的模糊参数与隶属函数为默认。语义与批量入参差异详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.ResetMetrologyObjectFuzzyParam("all");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型里一条对象都没有时的行为 [待实测]。</para>
	/// </remarks>
	public void ResetMetrologyObjectFuzzyParam(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(785);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Reset all parameters of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>把指定 metrology object 的全部参数复位为默认（原生 id 786），原地生效：num_instances、max_deviation、测量区域尺寸等运行期用 SetMetrologyObjectParam 改过的东西一并回到出厂值。</para>
	///   <para><b>与相邻算子的取舍</b>只想复位模糊一族用 ResetMetrologyObjectFuzzyParam（id 785）；复位不等于删除——对象还在、index 不变；要彻底移除用 ClearMetrologyObject。名义几何（Add 时给的圆心/半径）是否也在复位范围 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple idx = new int[] { circle };
	///   model.ResetMetrologyObjectParam(idx);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>复位后需重新 Apply 才能让结果反映新默认；string 重载同 id。</para>
	/// </remarks>
	public void ResetMetrologyObjectParam(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(786);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Reset all parameters of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>ResetMetrologyObjectParam 的 string 重载（原生 id 786）：index 经 StoreS 传 "all" 或单索引字符串，把该范围对象的全部参数复位默认。复位范围与后续需重新 Apply 的提醒见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.ResetMetrologyObjectParam("all");
	///   model.Dispose();
	///   </code>
	/// </remarks>
	public void ResetMetrologyObjectParam(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(786);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Get a fuzzy parameter of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "fuzzy_thresh"</param>
	/// <returns>Values of the generic parameters.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>读回 metrology object 的模糊参数当前值（原生 id 787），只读不改句柄。已知名 "fuzzy_thresh"（隶属度过滤阈值的默认约定），完整可用名集合 [待实测]；index 与 genParamName 均为 JlTuple 时可批量交叉取值，返回与请求的对应序 [排布待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>普通（非模糊）参数在 GetMetrologyObjectParam（id 788）；写回用 SetMetrologyObjectFuzzyParam（id 789）；一键回默认用 ResetMetrologyObjectFuzzyParam（id 785）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple idx = new int[] { circle };
	///   JlTuple names = new string[] { "fuzzy_thresh" };
	///   JlTuple vals = model.GetMetrologyObjectFuzzyParam(idx, names);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个入参都走 Store+UnpinTuple 钉传；非法参数名的报错形态 [待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectFuzzyParam(JlTuple index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(787);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get a fuzzy parameter of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "fuzzy_thresh"</param>
	/// <returns>Values of the generic parameters.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>GetMetrologyObjectFuzzyParam 的 string-index 重载（原生 id 787）：index 经 StoreS 传 "all" 或单索引字符串，genParamName 仍为 JlTuple 可批量取名；取值语义详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple vals = model.GetMetrologyObjectFuzzyParam("all", "fuzzy_thresh");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>"all" 时多对象同名的取值如何平铺 [待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectFuzzyParam(string index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(787);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get one or several parameters of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "num_measures"</param>
	/// <returns>Values of the generic parameters.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>读回 metrology object 的通用参数当前值（原生 id 788），只读。英文说明给出的默认名 "num_measures"（每条对象布置的测量区域数）；可取量还包括 num_instances、measure_length_1/2、measure_sigma、measure_threshold、max_deviation 一类 [名集合以实测为准]。index 与名都为 JlTuple 时批量交叉查询，返回平铺序 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>模型级参数（image_size 等）走 GetMetrologyModelParam（id 771）；模糊一族走 GetMetrologyObjectFuzzyParam（id 787）；测量结果数值走 GetMetrologyObjectResult（id 781）。写回用 SetMetrologyObjectParam（id 790）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   JlTuple vals = model.GetMetrologyObjectParam(new int[] { circle }, new string[] { "num_measures" });
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>入参 Store+UnpinTuple 钉传；Apply 之后读 num_measures 才能反映实际参与拟合的量 [查询时点差异待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectParam(JlTuple index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(788);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Get one or several parameters of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "num_measures"</param>
	/// <returns>Values of the generic parameters.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>GetMetrologyObjectParam 的 string-index 重载（原生 id 788）：index 经 StoreS 传 "all" 或单索引字符串，genParamName 保持 JlTuple 可批量取名；参数名集合与返回平布语义详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   JlTuple vals = model.GetMetrologyObjectParam("all", "num_measures");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>"all" 时多对象同名的取值顺序 [待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectParam(string index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(788);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Set fuzzy parameters or fuzzy functions for a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "fuzzy_thresh"</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: 0.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>设定 metrology object 的模糊参数/隶属函数（原生 id 789），原地生效：影响 Apply 时采边结果的模糊加权（默认名 "fuzzy_thresh"，值域 0~1）。genParamName 与 genParamValue 成对给，index 传 JlTuple 可批量。</para>
	///   <para><b>与相邻算子的取舍</b>普通参数（num_instances 等）用 SetMetrologyObjectParam（id 790）；查询当前值 GetMetrologyObjectFuzzyParam（id 787）；回默认 ResetMetrologyObjectFuzzyParam（id 785）。模糊过滤治的是"伪边缘混进拟合"，用 fuzzy_thresh 收紧比放大 measure_threshold 更不容易丢掉真边 [两者效果对比待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.SetMetrologyObjectFuzzyParam(new int[] { circle }, "fuzzy_thresh", 0.5);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三个 JlTuple 入参均 Store+UnpinTuple 钉传；改动后需重新 Apply 才反映到结果。</para>
	/// </remarks>
	public void SetMetrologyObjectFuzzyParam(JlTuple index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(789);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set fuzzy parameters or fuzzy functions for a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "fuzzy_thresh"</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: 0.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>SetMetrologyObjectFuzzyParam 的 string-index 重载（原生 id 789）：index 经 StoreS 传 "all" 或单索引字符串，名与值仍为 JlTuple；对全部对象设同一参数时用本重载最省事（"all" + 单值）。语义详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.SetMetrologyObjectFuzzyParam("all", "fuzzy_thresh", 0.5);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>"all" + 多值名的广播语义 [待实测]。</para>
	/// </remarks>
	public void SetMetrologyObjectFuzzyParam(string index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(789);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set parameters for the metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "num_instances"</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b>设定 metrology object 的通用参数（原生 id 790），原地生效、Apply 时体现。英文默认名 "num_instances"（同一几何允许找几个实例，如视场内多个同心圆）；max_deviation（采边点离名义几何的最大容差，超差的点在拟合中被剔除）与 num_measurements/num_measures 一类名 [支持集合待实测]——本库无 find_metrology，这些调参全部作用在 ApplyMetrologyModel 上。</para>
	///   <para><b>与相邻算子的取舍</b>Add 时 genParamName/genParamValue 已能带同样的参数；本方法用于示教后运行期调参。模型级参数（image_size）在 SetMetrologyModelParam（id 772）；模糊一族在 SetMetrologyObjectFuzzyParam（id 789）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.SetMetrologyObjectParam(new int[] { circle }, "num_instances", 3);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>index/名/值都是 JlTuple（Store+UnpinTuple 钉传），名值成对、index 可多值 [广播语义待实测]；调 max_deviation 过小会导致 Apply 后实例数为 0 [待实测]。</para>
	/// </remarks>
	public void SetMetrologyObjectParam(JlTuple index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(790);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set parameters for the metrology objects of a metrology model.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: "num_instances"</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b>SetMetrologyObjectParam 的 string-index 重载（原生 id 790）：index 经 StoreS 传 "all" 或单索引字符串，对全体对象广播同一参数时最方便；参数含义与批量语义详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.SetMetrologyObjectParam("all", "num_instances", 1);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>改动后需重新 Apply 生效。</para>
	/// </remarks>
	public void SetMetrologyObjectParam(string index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(790);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Add a rectangle to a metrology model.
	/// </summary>
	/// <param name="row">Row (or Y) coordinate of the center of the rectangle.</param>
	/// <param name="column">Column (or X) coordinate of the center of the rectangle.</param>
	/// <param name="phi">Orientation of the main axis [rad].</param>
	/// <param name="length1">Length of the larger half edge of the rectangle.</param>
	/// <param name="length2">Length of the smaller half edge of the rectangle.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>向模型添加矩形测量对象（原生 id 791），返回新对象 index（int）：以 (row, column) 为中心、phi（弧度）为长轴方向、length1/length2 为半边的名义矩形，沿四条边各布一排微卡尺，Apply 时对四边联合拟合出一个整体矩形。几何量与取边参数（measureLength1 垂直边界的搜索半长、measureLength2 沿边切向平均半长、sigma 平滑、measureThreshold 最小边缘幅值）可传 JlTuple 多值。</para>
	///   <para><b>约束或前提</b>须先 SetMetrologyModelImageSize；genParamName/genParamValue 成对附加参数（num_instances、max_deviation 等 [名集合待实测]）。返回的 index 是后续 Set/GetMetrologyObjectParam、GetMetrologyObjectResult*、ClearMetrologyObject 的唯一寻址凭据。</para>
	///   <para><b>与相邻算子的取舍</b>只测一条边用 Line；要整体矩形（中心+角度+两边长）用本算子——四边信息互相约束，比 4 条独立线卡尺抗噪。结果含义随形状：矩形给 (row, column, phi, length1, length2) [排布待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int rect = model.AddMetrologyObjectRectangle2Measure(240.0, 320.0, 0.0, 80.0, 40.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本重载十个参数全 JlTuple（Store+UnpinTuple 钉传）；JlTuple 多值是否按对象展开为多个矩形 [待实测]，double 重载（同 id）无此歧义。</para>
	/// </remarks>
	public int AddMetrologyObjectRectangle2Measure(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(791);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, phi);
		JlNativeApi.Store(proc, 4, length1);
		JlNativeApi.Store(proc, 5, length2);
		JlNativeApi.Store(proc, 6, measureLength1);
		JlNativeApi.Store(proc, 7, measureLength2);
		JlNativeApi.Store(proc, 8, measureSigma);
		JlNativeApi.Store(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a rectangle to a metrology model.
	/// </summary>
	/// <param name="row">Row (or Y) coordinate of the center of the rectangle.</param>
	/// <param name="column">Column (or X) coordinate of the center of the rectangle.</param>
	/// <param name="phi">Orientation of the main axis [rad].</param>
	/// <param name="length1">Length of the larger half edge of the rectangle.</param>
	/// <param name="length2">Length of the smaller half edge of the rectangle.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>AddMetrologyObjectRectangle2Measure 的标量重载（原生 id 791）：几何与取边参数经 StoreD 直写单个标量，仅 genParamName/genParamValue 保持 JlTuple；返回新对象 index。四边联合拟合、index 寻址、参数取向详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int rect = model.AddMetrologyObjectRectangle2Measure(240.0, 320.0, 0.0, 80.0, 40.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlTuple fit = model.GetMetrologyObjectResult(rect, "all", "result_type", "all_param");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>退化矩形（length1 或 length2 为 0）的拟合行为 [待实测]。</para>
	/// </remarks>
	public int AddMetrologyObjectRectangle2Measure(double row, double column, double phi, double length1, double length2, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(791);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, length1);
		JlNativeApi.StoreD(proc, 5, length2);
		JlNativeApi.StoreD(proc, 6, measureLength1);
		JlNativeApi.StoreD(proc, 7, measureLength2);
		JlNativeApi.StoreD(proc, 8, measureSigma);
		JlNativeApi.StoreD(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a line to a metrology model.
	/// </summary>
	/// <param name="rowBegin">Row (or Y) coordinate of the start of the line.</param>
	/// <param name="columnBegin">Column (or X) coordinate of the start of the line.</param>
	/// <param name="rowEnd">Row (or Y) coordinate of the end of the line.</param>
	/// <param name="columnEnd">Column (or X) coordinate of the end of the line.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>向模型添加直线测量对象（原生 id 792），返回新对象 index：以两点 (rowBegin, columnBegin)-(rowEnd, columnEnd) 为名义线段，沿线段在其法向两侧布微卡尺取边，Apply 时把所有边缘点最小二乘拟合成一条直线（结果给端点与法向偏差量 [结果参数字段待实测]）。</para>
	///   <para><b>约束或前提</b>须先 SetMetrologyModelImageSize；measureLength1 是垂直线段方向的搜索半长（工件位置偏差要落在这里面）、measureLength2 沿线段切向的平均半长。index 之后用于 Set/GetMetrologyObjectParam 与取结果，语义同其它 Add 算子。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个边缘坐标（如测件左边界位置）用 JlMeasure 1D 卡尺单点即可；要整条边的位置+角度、并对整条边几十个边缘点做平均降噪，用本算子。两条边宽度/厚度成对输出用 JlMeasure.MeasurePairs。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int line = model.AddMetrologyObjectLineMeasure(100.0, 120.0, 380.0, 120.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 多值是否展开多条直线对象 [待实测]；两点重合的退化线段行为 [待实测]。</para>
	/// </remarks>
	public int AddMetrologyObjectLineMeasure(JlTuple rowBegin, JlTuple columnBegin, JlTuple rowEnd, JlTuple columnEnd, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(792);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, rowBegin);
		JlNativeApi.Store(proc, 2, columnBegin);
		JlNativeApi.Store(proc, 3, rowEnd);
		JlNativeApi.Store(proc, 4, columnEnd);
		JlNativeApi.Store(proc, 5, measureLength1);
		JlNativeApi.Store(proc, 6, measureLength2);
		JlNativeApi.Store(proc, 7, measureSigma);
		JlNativeApi.Store(proc, 8, measureThreshold);
		JlNativeApi.Store(proc, 9, genParamName);
		JlNativeApi.Store(proc, 10, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBegin);
		JlNativeApi.UnpinTuple(columnBegin);
		JlNativeApi.UnpinTuple(rowEnd);
		JlNativeApi.UnpinTuple(columnEnd);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a line to a metrology model.
	/// </summary>
	/// <param name="rowBegin">Row (or Y) coordinate of the start of the line.</param>
	/// <param name="columnBegin">Column (or X) coordinate of the start of the line.</param>
	/// <param name="rowEnd">Row (or Y) coordinate of the end of the line.</param>
	/// <param name="columnEnd">Column (or X) coordinate of the end of the line.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>AddMetrologyObjectLineMeasure 的标量重载（原生 id 792）：端点与取边参数经 StoreD 直写单条线，仅 genParam 保持 JlTuple；返回新对象 index。拟合语义与参数取向详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int line = model.AddMetrologyObjectLineMeasure(100.0, 120.0, 380.0, 120.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   double n = model.GetMetrologyObjectNumInstances(line);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>被测边与名义线段交角过大时整批卡尺取不到边，表现为实例数 0 [待实测]。</para>
	/// </remarks>
	public int AddMetrologyObjectLineMeasure(double rowBegin, double columnBegin, double rowEnd, double columnEnd, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(792);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, rowBegin);
		JlNativeApi.StoreD(proc, 2, columnBegin);
		JlNativeApi.StoreD(proc, 3, rowEnd);
		JlNativeApi.StoreD(proc, 4, columnEnd);
		JlNativeApi.StoreD(proc, 5, measureLength1);
		JlNativeApi.StoreD(proc, 6, measureLength2);
		JlNativeApi.StoreD(proc, 7, measureSigma);
		JlNativeApi.StoreD(proc, 8, measureThreshold);
		JlNativeApi.Store(proc, 9, genParamName);
		JlNativeApi.Store(proc, 10, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add an ellipse or an elliptic arc to a metrology model.
	/// </summary>
	/// <param name="row">Row (or Y) coordinate of the center of the ellipse.</param>
	/// <param name="column">Column (or X) coordinate of the center of the ellipse.</param>
	/// <param name="phi">Orientation of the main axis [rad].</param>
	/// <param name="radius1">Length of the larger half axis.</param>
	/// <param name="radius2">Length of the smaller half axis.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>向模型添加椭圆/椭圆弧测量对象（原生 id 793），返回新对象 index：中心 (row, column)、长轴方向 phi（弧度）、半轴 radius1（长）/radius2（短）。Apply 时沿椭圆周布微卡尺取边并整体拟合。radius1 应不小于 radius2，两者相等退化为圆 [违反时行为待实测]。</para>
	///   <para><b>约束或前提</b>须先 SetMetrologyModelImageSize。只测部分弧段需把测量区域限制在起止角内——通过 genParamName/genParamValue 给 start/end angle 一类参数的路径与名称 [待实测]。index 后续寻址语义同其它 Add 算子。</para>
	///   <para><b>与相邻算子的取舍</b>目标是圆就用 AddMetrologyObjectCircleMeasure（参数少、拟合更稳）；椭圆度/长宽比本身是被测量时才用本算子。结果给 (row, column, phi, radius1, radius2[, 起止角]) [排布待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int ell = model.AddMetrologyObjectEllipseMeasure(240.0, 320.0, 0.0, 120.0, 60.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>弧段过小（可见弧不足约 1/4 周）时椭圆五参数欠约束，拟合易发散 [待实测]——这是椭圆版相对圆版的主要坑。</para>
	/// </remarks>
	public int AddMetrologyObjectEllipseMeasure(JlTuple row, JlTuple column, JlTuple phi, JlTuple radius1, JlTuple radius2, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(793);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, phi);
		JlNativeApi.Store(proc, 4, radius1);
		JlNativeApi.Store(proc, 5, radius2);
		JlNativeApi.Store(proc, 6, measureLength1);
		JlNativeApi.Store(proc, 7, measureLength2);
		JlNativeApi.Store(proc, 8, measureSigma);
		JlNativeApi.Store(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(radius1);
		JlNativeApi.UnpinTuple(radius2);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add an ellipse or an elliptic arc to a metrology model.
	/// </summary>
	/// <param name="row">Row (or Y) coordinate of the center of the ellipse.</param>
	/// <param name="column">Column (or X) coordinate of the center of the ellipse.</param>
	/// <param name="phi">Orientation of the main axis [rad].</param>
	/// <param name="radius1">Length of the larger half axis.</param>
	/// <param name="radius2">Length of the smaller half axis.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>AddMetrologyObjectEllipseMeasure 的标量重载（原生 id 793）：五个几何量与四个取边参数经 StoreD 直写单个椭圆，仅 genParam 保持 JlTuple；返回新对象 index。弧段限制、半轴次序等约束详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int ell = model.AddMetrologyObjectEllipseMeasure(240.0, 320.0, 0.0, 120.0, 60.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlXLDCont fitted = model.GetMetrologyObjectResultContour(ell, "all", 1.5);
	///   fitted.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>GetMetrologyObjectResultContour 返回新句柄须 Dispose。</para>
	/// </remarks>
	public int AddMetrologyObjectEllipseMeasure(double row, double column, double phi, double radius1, double radius2, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(793);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, radius1);
		JlNativeApi.StoreD(proc, 5, radius2);
		JlNativeApi.StoreD(proc, 6, measureLength1);
		JlNativeApi.StoreD(proc, 7, measureLength2);
		JlNativeApi.StoreD(proc, 8, measureSigma);
		JlNativeApi.StoreD(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a circle or a circular arc to a metrology model.
	/// </summary>
	/// <param name="row">Row coordinate (or Y) of the center of the circle or circular arc.</param>
	/// <param name="column">Column (or X) coordinate of the center of the circle or circular arc.</param>
	/// <param name="radius">Radius of the circle or circular arc.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>向模型添加圆/圆弧测量对象（原生 id 794），返回新对象 index：圆心 (row, column)、名义半径 radius（像素）。Apply 时沿圆周等间隔布微卡尺径向取边，把所有边缘点联合最小二乘拟合出圆心与半径——几十个点的平均使半径重复精度远高于单点卡尺。</para>
	///   <para><b>约束或前提</b>须先 SetMetrologyModelImageSize；圆心半径是名义值，真实位置偏差由 measureLength1（径向搜索半长）兜住。只见部分圆弧时通过 genParamName/genParamValue 限定起止角 [参数名待实测]，且弧段太短时圆心半径欠约束、抖动放大 [待实测]。index 是后续按对象设参/取结果的凭据。</para>
	///   <para><b>与相邻算子的取舍</b>只测孔壁上一点到基准的距离用 JlMeasure 圆弧卡尺（GenMeasureArc）；要完整拟合圆心+半径并输出不确定度类结果用本算子。结果量排布 (row, column, radius[, start, end]) [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 重载的多值是否展开多个圆对象 [待实测]；radius 为 0 的退化行为 [待实测]。</para>
	/// </remarks>
	public int AddMetrologyObjectCircleMeasure(JlTuple row, JlTuple column, JlTuple radius, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(794);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, radius);
		JlNativeApi.Store(proc, 4, measureLength1);
		JlNativeApi.Store(proc, 5, measureLength2);
		JlNativeApi.Store(proc, 6, measureSigma);
		JlNativeApi.Store(proc, 7, measureThreshold);
		JlNativeApi.Store(proc, 8, genParamName);
		JlNativeApi.Store(proc, 9, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a circle or a circular arc to a metrology model.
	/// </summary>
	/// <param name="row">Row coordinate (or Y) of the center of the circle or circular arc.</param>
	/// <param name="column">Column (or X) coordinate of the center of the circle or circular arc.</param>
	/// <param name="radius">Radius of the circle or circular arc.</param>
	/// <param name="measureLength1">Half length of the measure regions perpendicular to the boundary. Default: 20.0</param>
	/// <param name="measureLength2">Half length of the measure regions tangential to the boundary. Default: 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">Minimum edge amplitude. Default: 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>AddMetrologyObjectCircleMeasure 的标量重载（原生 id 794）：几何与取边参数经 StoreD 直写单个圆，仅 genParam 保持 JlTuple；返回新对象 index。布卡尺拟合、名义圆约束详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   using (JlImage image = new JlImage("byte", 640, 480))
	///   {
	///       model.ApplyMetrologyModel(image);
	///   }
	///   JlTuple fit = model.GetMetrologyObjectResult(circle, "all", "result_type", "all_param");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>热路径上每帧重建圆对象是浪费：圆心随定位移动时优先 AlignMetrologyModel 或 TransformMetrologyObject。</para>
	/// </remarks>
	public int AddMetrologyObjectCircleMeasure(double row, double column, double radius, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(794);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, radius);
		JlNativeApi.StoreD(proc, 4, measureLength1);
		JlNativeApi.StoreD(proc, 5, measureLength2);
		JlNativeApi.StoreD(proc, 6, measureSigma);
		JlNativeApi.StoreD(proc, 7, measureThreshold);
		JlNativeApi.Store(proc, 8, genParamName);
		JlNativeApi.Store(proc, 9, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Delete a metrology model and free the allocated memory.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>释放原生侧计量模型内存（原生 id 795，clear_metrology_model）。方法体只调原生删除，不清空托管侧句柄字段。</para>
	///   <para><b>与相邻算子的取舍</b>常规释放用 Dispose() 或 using；只想删对象保模型用 ClearMetrologyObject("all")（id 796）；要在同一对象上得到全新空模型用 CreateMetrologyModel（id 798，先 Dispose 再原地重建）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.ClearMetrologyModel(); // 调用后不得再用该对象
	///   </code>
	///   <para><b>资源与坑</b>与 CloseMeasure 同款坑：调用后变量仍指向已删除对象，再 Apply 行为未定义 [待实测]；其后若再触发 Dispose 是否二次释放 [待实测]。</para>
	/// </remarks>
	public void ClearMetrologyModel()
	{
		IntPtr proc = JlNativeApi.PreCall(795);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Delete metrology objects and free the allocated memory.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>从模型中删除指定 metrology object 并释放其内存（原生 id 796），模型本身保留。index 传 JlTuple 数组一次删多条，"all" 语义可用 GetMetrologyObjectIndices 取全量后传入。</para>
	///   <para><b>与相邻算子的取舍</b>删对象留模型用本方法；连模型一起弃用 ClearMetrologyModel/Dispose；示教重来但保留对象编号时也可 ResetMetrologyObjectParam（复位非删除）。这是"重复建模"的正路：ClearMetrologyObject 旧对象再 Add 新对象，替代整模型重建。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   int circle = model.AddMetrologyObjectCircleMeasure(240.0, 320.0, 100.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.ClearMetrologyObject(new int[] { circle });
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>删除后该 index 立即从 GetMetrologyObjectIndices 消失，但缓存的旧 index 变量不会报错——继续用它取结果的行为 [待实测]；编号是否被后续 Add 复用 [待实测]。</para>
	/// </remarks>
	public void ClearMetrologyObject(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(796);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Delete metrology objects and free the allocated memory.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>ClearMetrologyObject 的 string 重载（原生 id 796）：index 经 StoreS 传 "all"（删全部对象、留模型）或单索引字符串。与 JlTuple 重载的选择、删后编号失效的坑详见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.ClearMetrologyObject("all");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>"all" 清对象后 image_size 等模型级参数是否保留 [待实测]。</para>
	/// </remarks>
	public void ClearMetrologyObject(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(796);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set the size of the image of metrology objects.
	/// </summary>
	/// <param name="width">Width of the image to be processed. Default: 640</param>
	/// <param name="height">Height of the image to be processed. Default: 480</param>
	/// <remarks>
	///   <para><b>功能说明</b>声明模型工作的图像幅面（原生 id 797，width/height 为像素整数）。这是所有后续 Add/Apply 的几何基准：测量区域的图内裁剪、距离计算都以此为准，等价于 set_metrology_model_param 的 "image_size"（id 772）专用快捷口。</para>
	///   <para><b>约束或前提</b>顺序硬约束——必须在 AddMetrologyObject* 与 ApplyMetrologyModel 之前设定；不设就 Add 的行为 [待实测]，基准不对会导致测量区域被静默裁掉、采边数骤减。换相机分辨率后须重设并复核各对象位置。</para>
	///   <para><b>与相邻算子的取舍</b>意图直观用本方法（id 797）；批量设其它模型参数才用 SetMetrologyModelParam("image_size", ...)。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>width/height 与实图不符时原生层不校验报错 [待实测]——这是"结果莫名漂移"的头号来源，排查从这一行开始。</para>
	/// </remarks>
	public void SetMetrologyModelImageSize(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(797);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create the data structure that is needed to measure geometric shapes.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>在既有对象上重建一个空计量模型（原生 id 798，与无参构造同一算子）。方法体先 Dispose() 旧句柄再把新空模型句柄装入本对象——原地换柄：所有 metrology object、已存结果、模型级参数（含 image_size）随之清零。</para>
	///   <para><b>与相邻算子的取舍</b>只想删对象保留模型级设置用 ClearMetrologyObject("all")；只想回退个别对象参数用 ResetMetrologyObjectParam；需要"另一个模型"（旧引用还要用）则 new JlMetrologyModel()。示教换型时本方法是重复建模的一键入口。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.SetMetrologyModelImageSize(640, 480);
	///   model.CreateMetrologyModel(); // 回到未设 image_size 的空模型
	///   model.SetMetrologyModelImageSize(1024, 768); // 重建后必须重设基准
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>旧句柄若被 JlMetrologyModel(handle) 包装共享，释放后的共享行为 [待实测]。</para>
	/// </remarks>
	public void CreateMetrologyModel()
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(798);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}
}
