using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of a matrix.</summary>
[Serializable]
public class JlMatrix : JlHandle, ISerializable, ICloneable
{
	/// <summary>按 (row, column) 读写单个矩阵元素，元素值一律为 double。</summary>
	/// <param name="row">行号，0 基。</param>
	/// <param name="column">列号，0 基。</param>
	/// <remarks>
	///   <para><b>功能说明</b>读取转调 GetValueMatrix(int,int)（原生算子 id 871，按 DOUBLE 装载单值）；写入转调 SetValueMatrix(int,int,double)（id 870，行列为 INTEGER、值为 DOUBLE）。</para>
	///   <para><b>约束或前提</b>索引越界不在托管层预检，由原生层报错 [待实测]。矩阵元素只存 double：赋 int/long 字面量是隐式提升，超过 2^53 的整数才会悄悄丢精度。</para>
	///   <para><b>与相邻算子的取舍</b>每次 get/set 都是一次完整的原生调用，逐元素循环搬运整块矩阵极慢；批量读写改用 GetValueMatrix(JlTuple,JlTuple)/SetValueMatrix(JlTuple,JlTuple,JlTuple) 或 GetFullMatrix/SetFullMatrix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   m[1, 2] = 5.0;
	///   double v = m[1, 2];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>写入只改元素不改维度；对同一矩阵交替索引读写时无需重新创建句柄。</para>
	/// </remarks>
	public double this[int row, int column]
	{
		get
		{
			return GetValueMatrix(row, column);
		}
		set
		{
			SetValueMatrix(row, column, value);
		}
	}

	/// <summary>矩阵行数；每次读取都向原生层查询一次，托管侧不缓存。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>getter 内部调用 GetSizeMatrix(out int,out int)（原生算子 id 861），维度按 INTEGER 装载后丢弃列数只返回行数。</para>
	///   <para><b>约束或前提</b>句柄无效（未创建或已 Dispose）时由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>同时要行数和列数时，一次 GetSizeMatrix 比 NumRows+NumColumns 少一半原生调用；只关心元素总数也可直接 GetFullMatrix().Length。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 2, 0.0);
	///   int rows = m.NumRows;
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>*Mod 系列原地改写维度（如 TransposeMatrixMod）后立即再读该属性即可拿到新值，不存在陈旧缓存。</para>
	/// </remarks>
	public int NumRows
	{
		get
		{
			GetSizeMatrix(out var rows, out var _);
			return rows;
		}
	}

	/// <summary>矩阵列数；每次读取都向原生层查询一次，托管侧不缓存。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>getter 内部调用 GetSizeMatrix(out int,out int)（原生算子 id 861），维度按 INTEGER 装载后丢弃行数只返回列数。</para>
	///   <para><b>约束或前提</b>句柄无效（未创建或已 Dispose）时由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>校验乘法维度是否匹配时成对取 NumRows/NumColumns，或改用一次 GetSizeMatrix；判断是否方阵请两个都读，不能只读这一个。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 2, 0.0);
	///   int cols = m.NumColumns;
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>*Mod 系列原地改写维度后读到的即是新列数；单列矩阵（列向量）该值为 1，点积类运算要先确认这一点。</para>
	/// </remarks>
	public int NumColumns
	{
		get
		{
			GetSizeMatrix(out var _, out var columns);
			return columns;
		}
	}

	/// <summary>创建一个不占任何原生资源的空句柄（UNDEF），供后续装载输出参数用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>仅把内部句柄置为 UNDEF，不做任何原生调用；本类内部的 Deserialize/LoadNew 模式都先这样占位再装载。</para>
	///   <para><b>约束或前提</b>在装载有效矩阵之前不能参与任何运算；原生层禁止把已持有有效句柄的实例当作输出参数装载（Load 会抛 JlException "Undisposed handle instance when loading output parameter"）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix();
	///   m.CreateMatrix(2, 2, 1.0);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>标记为 EditorBrowsableState.Never：正常业务代码应使用带参数的构造器，而不是先建空句柄再 CreateMatrix。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMatrix()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>把已有的原生句柄 ID 包装为 JlMatrix，并校验其语义类型。</summary>
	/// <param name="handle">原生句柄 ID；UNDEF 表示空句柄，跳过校验。</param>
	/// <remarks>
	///   <para><b>功能说明</b>构造后立即 AssertSemType("matrix")：句柄非 UNDEF 且语义类型不是 "matrix" 时抛 JlException "Invalid handle instance passed"（见 JlHandleBase.AssertSemType）。</para>
	///   <para><b>约束或前提</b>包装后该句柄归本实例管理，Dispose 会释放它；不要把仍被别处使用的 ID 同时交给两个包装器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix src = new JlMatrix(2, 2, 1.0);
	///   IntPtr id = src;
	///   JlMatrix wrap = new JlMatrix(id);
	///   wrap.Dispose();
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlMatrix 到 IntPtr 是隐式转换（返回 Vision 句柄 ID），调用方需保证转换期间对象存活。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMatrix(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>从 <see cref="JlHandle"/> 句柄包装构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMatrix(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("matrix");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMatrix obj)
	{
		obj = new JlMatrix(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMatrix[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlMatrix[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlMatrix(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   从文件读入一个矩阵并构造新句柄。
	/// </summary>
	/// <param name="fileName">矩阵文件名（由 WriteMatrix 写出的 .dat 等格式）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 819：StoreS(0, fileName) 传文件名字符串，InitOCT(0)+Load(0) 把读到的矩阵装载进本新建实例的句柄，属"构造新句柄"。</para>
	///   <para><b>约束或前提</b>文件须是本库 WriteMatrix/序列化格式，路径不存在或格式非法时由原生层报错 [待实测]。用毕须 Dispose 释放句柄。</para>
	///   <para><b>与相邻算子的取舍</b>与实例方法 ReadMatrix(string) 用同一 id 819：本构造器给出新句柄；ReadMatrix 则先 Dispose() 再原地改写已有句柄，反复换文件读取时可复用同一对象避免新建。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix("data.dat");
	///   double v = m[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>构造返回后即持有有效原生句柄；读取失败抛异常前句柄可能处于半初始化状态，建议置于 using 中确保释放。</para>
	/// </remarks>
	public JlMatrix(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(819);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按行列数新建矩阵，并用一个元组的值填充元素。
	/// </summary>
	/// <param name="rows">矩阵行数。Default: 3</param>
	/// <param name="columns">矩阵列数。Default: 3</param>
	/// <param name="value">初始化各元素的值，按行优先铺放；单元素会被广播到全矩阵。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 873：StoreI(0, rows)、StoreI(1, columns) 定维度，Store(2, value) 钉住传入的 JlTuple 作填充值，InitOCT(0)+Load(0) 装载出本新句柄，调用后 UnpinTuple(value) 解除固定。</para>
	///   <para><b>约束或前提</b>value 元素个数须等于 rows*columns，或为单元素以广播填充，长度不匹配时由原生层报错 [待实测]。JlTuple 有 int/long/double/string 及其数组的隐式转换，可直接传数组字面量。用毕须 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>与 double 重载同 id 873：整块填充同一常数用 double 重载（StoreD 直写、无钉固定开销）；需要逐元素不同的初值才用本元组重载（Store+UnpinTuple，多一次钉固定）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, new JlTuple(1.0, 2.0, 3.0, 4.0));
	///   double a = m[1, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>value 元组在原生调用结束前不得释放；本方法已在调用后 UnpinTuple，故调用方仍持有 value 时可安全继续复用或 Dispose。</para>
	/// </remarks>
	public JlMatrix(int rows, int columns, JlTuple value)
	{
		IntPtr proc = JlNativeApi.PreCall(873);
		JlNativeApi.StoreI(proc, 0, rows);
		JlNativeApi.StoreI(proc, 1, columns);
		JlNativeApi.Store(proc, 2, value);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(value);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按行列数新建矩阵，所有元素填同一常数。
	/// </summary>
	/// <param name="rows">矩阵行数。Default: 3</param>
	/// <param name="columns">矩阵列数。Default: 3</param>
	/// <param name="value">填充到每个元素的常数。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 873：StoreI(0, rows)、StoreI(1, columns) 定维度，StoreD(2, value) 直写单一 double 填充全矩阵，InitOCT(0)+Load(0) 装载出本新句柄。</para>
	///   <para><b>约束或前提</b>元素一律 double；用 int/long 字面量是隐式提升。用毕须 Dispose 释放句柄。</para>
	///   <para><b>与相邻算子的取舍</b>与 JlTuple 重载同 id 873：整块同值走本重载（无钉固定开销，比传元组更省）；逐元素不同初值改用 JlTuple 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix zeros = new JlMatrix(3, 3, 0.0);
	///   double v = zeros[2, 2];
	///   zeros.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>rows/columns 直接是矩阵维度，构造后即可用索引器 [row, col] 访问；0 维或负维由原生层报错 [待实测]。</para>
	/// </remarks>
	public JlMatrix(int rows, int columns, double value)
	{
		IntPtr proc = JlNativeApi.PreCall(873);
		JlNativeApi.StoreI(proc, 0, rows);
		JlNativeApi.StoreI(proc, 1, columns);
		JlNativeApi.StoreD(proc, 2, value);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeMatrix();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMatrix(SerializationInfo info, StreamingContext context)
	{
		DeserializeMatrix((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把矩阵以本库二进制格式序列化写入流。</summary>
	/// <param name="stream">目标写入流。</param>
	/// <remarks>
	///   <para><b>功能说明</b>先调 SerializeMatrix() 取得字节数组，再由 JlSerializationBuffer.WriteToStream 原样写入 stream。不产生新句柄，只读不改动 this。</para>
	///   <para><b>约束或前提</b>stream 须可写；本方法不关闭/不 flush 传入的流，由调用方负责收尾。用 new 隐藏了基类 JlHandle.Serialize。</para>
	///   <para><b>与相邻算子的取舍</b>想拿到内存字节而非写流，直接用 SerializeMatrix()；想把矩阵存成可读回的文件，用 WriteMatrix(fileFormat, fileName)。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       m.Serialize(ms);
	///   }
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>序列化产物须用配套的 Deserialize(Stream) 读回；跨版本/跨本库格式不保证兼容 [待实测]。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeMatrix(), stream);
	}

	/// <summary>从本库二进制流反序列化出一个新矩阵句柄。</summary>
	/// <param name="stream">由 Serialize 写出的可读流。</param>
	/// <returns>新建的 JlMatrix 句柄（内部经 DeserializeMatrix/id 817 装载），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法：先 new JlMatrix() 占 UNDEF 空句柄，再用 JlSerializationBuffer.ReadFromStream(stream) 取字节，交给实例 DeserializeMatrix 装载成有效矩阵后返回。</para>
	///   <para><b>约束或前提</b>返回的是新句柄（非原地改写）；读回的维度与元素取决于流内容，与调用方原有矩阵无关。stream 须处于可读位置。</para>
	///   <para><b>与相邻算子的取舍</b>手上有 byte[] 而非流时，用实例方法 DeserializeMatrix(byte[]) 原地装入一个已有句柄；跨进程/网络搬运整块用本静态方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix src = new JlMatrix(2, 2, 1.0);
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       src.Serialize(ms);
	///       ms.Position = 0;
	///       JlMatrix copy = JlMatrix.Deserialize(ms);
	///       copy.Dispose();
	///   }
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄归调用方管理，须 Dispose；new 隐藏了基类 JlHandle.Deserialize。</para>
	/// </remarks>
	public new static JlMatrix Deserialize(Stream stream)
	{
		JlMatrix hMatrix = new JlMatrix();
		hMatrix.DeserializeMatrix(JlSerializationBuffer.ReadFromStream(stream));
		return hMatrix;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>深拷贝矩阵，返回一个独立的新句柄。</summary>
	/// <returns>与原矩阵内容相同、互不影响的新 JlMatrix 句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 SerializeMatrix() 序列化 this，再 new JlMatrix() + DeserializeMatrix 反序列化出副本，等价于经内存字节做一次往返的深拷贝。</para>
	///   <para><b>约束或前提</b>返回的是全新句柄，修改副本不影响原矩阵，反之亦然；原矩阵须为有效句柄才能序列化。</para>
	///   <para><b>与相邻算子的取舍</b>与 CopyMatrix() 同为返回独立新句柄，但本方法经序列化字节往返实现、元素值完全隔离；仅需把内容装入已有目标句柄时用 DeserializeMatrix(SerializeMatrix()) 免去新建。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   JlMatrix c = m.Clone();
	///   c.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>用 new 隐藏基类返回类型，返回 JlMatrix 而非 object；显式接口 ICloneable.Clone 亦转调本方法。副本须单独 Dispose。</para>
	/// </remarks>
	public new JlMatrix Clone()
	{
		byte[] data = SerializeMatrix();
		JlMatrix obj = new JlMatrix();
		obj.DeserializeMatrix(data);
		return obj;
	}

	/// <summary>矩阵取反：返回每个元素乘 -1 的新句柄。</summary>
	/// <param name="matrix">被取反的矩阵。</param>
	/// <returns>新 JlMatrix 句柄（原矩阵各元素变号），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix.ScaleMatrix(-1.0)（原生算子 id 854，LoadNew 返回新句柄），维度与原矩阵一致。</para>
	///   <para><b>约束或前提</b>不改动入参，入参仍归调用方管理；结果为独立新句柄须 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>只是变号无需另写 ScaleMatrix(-1.0)，用 -m 更直观；要就地改号请用 ScaleMatrixMod 系列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   JlMatrix neg = -m;
	///   neg.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄与原矩阵各自独立释放；漏 Dispose 会泄漏原生矩阵内存。</para>
	/// </remarks>
	public static JlMatrix operator -(JlMatrix matrix)
	{
		return matrix.ScaleMatrix(-1.0);
	}

	/// <summary>两个矩阵逐元素相加，返回新句柄。</summary>
	/// <param name="matrix1">加数 A（this 位）。</param>
	/// <param name="matrix2">加数 B。</param>
	/// <returns>新 JlMatrix 句柄（A 与 B 对应元素之和），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix1.AddMatrix(matrix2)（原生算子 id 858，Store(this)+Store(B)，LoadNew 返回新句柄）。</para>
	///   <para><b>约束或前提</b>逐元素加法要求 A、B 行列维度完全一致；维度不匹配时由原生层报错 [待实测]，不会自动广播。</para>
	///   <para><b>与相邻算子的取舍</b>这是逐元素加，不是矩阵相加意义的拼接；要就地累加到 A 用 AddMatrixMod(B)。矩阵乘法用 * 运算符（见 operator*）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   JlMatrix sum = a + b;
	///   sum.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两入参与结果各为独立句柄，均需分别 Dispose；结果不共享任一入参内存。</para>
	/// </remarks>
	public static JlMatrix operator +(JlMatrix matrix1, JlMatrix matrix2)
	{
		return matrix1.AddMatrix(matrix2);
	}

	/// <summary>两个矩阵逐元素相减，返回新句柄。</summary>
	/// <param name="matrix1">被减数 A（this 位）。</param>
	/// <param name="matrix2">减数 B。</param>
	/// <returns>新 JlMatrix 句柄（A 各元素减 B 对应元素），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix1.SubMatrix(matrix2)（原生算子 id 856，LoadNew 返回新句柄），计算 A - B。</para>
	///   <para><b>约束或前提</b>要求 A、B 维度完全一致；减法不满足交换律，A-B 与 B-A 结果相反，参数顺序不可颠倒。</para>
	///   <para><b>与相邻算子的取舍</b>就地相减到 A 用 SubMatrixMod(B)；若只需对 A 变号再相加，可用一元 - 与 +，但不如本运算符直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 3.0);
	///   JlMatrix b = new JlMatrix(2, 2, 1.0);
	///   JlMatrix diff = a - b;
	///   diff.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是新句柄，与两入参互不影响，须各自 Dispose。</para>
	/// </remarks>
	public static JlMatrix operator -(JlMatrix matrix1, JlMatrix matrix2)
	{
		return matrix1.SubMatrix(matrix2);
	}

	/// <summary>两个矩阵相乘（A×B），返回新句柄。</summary>
	/// <param name="matrix1">左矩阵 A。</param>
	/// <param name="matrix2">右矩阵 B。</param>
	/// <returns>新 JlMatrix 句柄（A×B 的矩阵积），维度为 A.rows×B.columns，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix1.MultMatrix(matrix2, "AB")（原生算子 id 860，multType 固定 "AB" 表示标准矩阵积），LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>矩阵乘法要求 A 的列数等于 B 的行数，否则由原生层报错 [待实测]；乘法不满足交换律，A×B 与 B×A 维度与值一般不同，操作数顺序即 A、B 顺序。</para>
	///   <para><b>与相邻算子的取舍</b>这是线性代数的矩阵积，不是逐元素乘——逐元素乘积本运算符族里没有；要就地乘回 A 用 MultMatrixMod(B, "AB")。与标量数乘 operator*(double,JlMatrix) 区分。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 3, 1.0);
	///   JlMatrix b = new JlMatrix(3, 2, 2.0);
	///   JlMatrix prod = a * b;
	///   prod.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是新句柄，与两入参独立，须各自 Dispose；A 列数须与 B 行数匹配才不报错。</para>
	/// </remarks>
	public static JlMatrix operator *(JlMatrix matrix1, JlMatrix matrix2)
	{
		return matrix1.MultMatrix(matrix2, "AB");
	}

	/// <summary>标量左乘矩阵：所有元素乘 factor，返回新句柄。</summary>
	/// <param name="factor">数乘标量。</param>
	/// <param name="matrix">被缩放的矩阵。</param>
	/// <returns>新 JlMatrix 句柄（各元素×factor），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix.ScaleMatrix(factor)（原生算子 id 854，StoreD 直写标量，LoadNew 返回新句柄），维度不变。</para>
	///   <para><b>约束或前提</b>与 operator*(JlMatrix,double) 完全等价，只是书写方向 (factor*m 与 m*factor) 不同；不改动入参。</para>
	///   <para><b>与相邻算子的取舍</b>就地缩放用 ScaleMatrixMod(factor)；factor 需逐元素不同则改用 ScaleMatrix(JlTuple) 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   JlMatrix s = 2.5 * m;
	///   s.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是独立新句柄，入参与结果分别 Dispose。</para>
	/// </remarks>
	public static JlMatrix operator *(double factor, JlMatrix matrix)
	{
		return matrix.ScaleMatrix(factor);
	}

	/// <summary>标量右乘矩阵：所有元素乘 factor，返回新句柄。</summary>
	/// <param name="matrix">被缩放的矩阵。</param>
	/// <param name="factor">数乘标量。</param>
	/// <returns>新 JlMatrix 句柄（各元素×factor），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix.ScaleMatrix(factor)（原生算子 id 854，StoreD 直写标量，LoadNew 返回新句柄），与 operator*(double,JlMatrix) 同一实现。</para>
	///   <para><b>约束或前提</b>标量数乘满足交换律，m*factor 与 factor*m 结果一致；不改动入参 matrix。</para>
	///   <para><b>与相邻算子的取舍</b>就地缩放用 ScaleMatrixMod(factor)；逐元素不同倍率用 ScaleMatrix(JlTuple)。factor=0 得全零矩阵但不释放原句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 4.0);
	///   JlMatrix half = m * 0.5;
	///   half.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是独立新句柄，入参与结果分别 Dispose。</para>
	/// </remarks>
	public static JlMatrix operator *(JlMatrix matrix, double factor)
	{
		return matrix.ScaleMatrix(factor);
	}

	/// <summary>解线性方程组 matrix2 × x = matrix1，返回 x（并非逐元素除法）。</summary>
	/// <param name="matrix1">右端项 B。</param>
	/// <param name="matrix2">系数矩阵 A。</param>
	/// <returns>新 JlMatrix 句柄，满足 matrix2 × result = matrix1，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 matrix2.SolveMatrix("general", 0.0, matrix1)（原生算子 id 828，LoadNew 返回新句柄）：把右操作数 matrix2 当作系数矩阵 A、左操作数 matrix1 当作右端项 B，解 A×x=B。</para>
	///   <para><b>约束或前提</b>操作数顺序与直觉相反：`B / A` 求的是 x 使 A×x=B（等价 x=A⁻¹B，左乘逆），不是 B×A⁻¹，也不是逐元素相除。A 须可解（配合维度：A 行数= B 行数），奇异/病态时由 epsilon 阈值判奇异性 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要显式控制 matrixLHSType 与 epsilon 请直接用 SolveMatrix 方法；只要 A⁻¹ 本身用 InvertMatrix。本运算符把 epsilon 固定 0.0、类型固定 general。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 1, 3.0);
	///   JlMatrix x = b / a;
	///   x.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是独立新句柄，两入参与结果分别 Dispose；奇异矩阵求解会报错而非静默返回 [待实测]。</para>
	/// </remarks>
	public static JlMatrix operator /(JlMatrix matrix1, JlMatrix matrix2)
	{
		return matrix2.SolveMatrix("general", 0.0, matrix1);
	}

	/// <summary>
	///   把序列化的字节缓冲反序列化并原地装入本句柄。
	/// </summary>
	/// <param name="serializedItemHandle">SerializeMatrix() 产出的序列化字节缓冲。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 817：先 Dispose() 释放本实例原有句柄，Store(0, buffer) 传字节，InitOCT(0)+Load(0) 把解出的矩阵原地装载回 this。属原地改写，不返回新句柄。</para>
	///   <para><b>约束或前提</b>会丢弃本对象原有的矩阵内容（先 Dispose 再装载）；buffer 须为配套的 SerializeMatrix()/Serialize() 字节，格式非法由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想一次得到全新对象（不动现有句柄）用静态 Deserialize(Stream) 或 Clone()；已持有句柄、想原地覆盖时用本方法，省去再建对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix src = new JlMatrix(2, 2, 1.0);
	///   byte[] data = src.SerializeMatrix();
	///   JlMatrix dst = new JlMatrix();
	///   dst.DeserializeMatrix(data);
	///   dst.Dispose();
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>调用内部先 Dispose()，若在装载中途抛异常本句柄可能处于无效态 [待实测]；buffer 用 JlSerializationBuffer 包装并 GC.KeepAlive 保活到调用结束。</para>
	/// </remarks>
	public void DeserializeMatrix(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(817);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   把本矩阵序列化成字节数组。
	/// </summary>
	/// <returns>本库序列化格式的 byte[]（托管内存，非原生句柄，无需 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 818：Store(0) 传本句柄，InitOCT(0)+JlSerializationBuffer.LoadBytes 取回序列化字节。不改动 this。</para>
	///   <para><b>约束或前提</b>this 须为有效句柄。返回的是纯托管 byte[]，可自由存取/传输，不必再释放原生资源。</para>
	///   <para><b>与相邻算子的取舍</b>要直接写进 Stream 用 Serialize(Stream)（内部即本方法+WriteToStream）；要读回用 DeserializeMatrix(byte[])。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   byte[] data = m.SerializeMatrix();
	///   JlMatrix r = new JlMatrix();
	///   r.DeserializeMatrix(data);
	///   r.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>字节内容含维度与全部 double 元素，大矩阵会占用可观托管内存；产物仅由本库 DeserializeMatrix 可读回 [待实测]。</para>
	/// </remarks>
	public byte[] SerializeMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(818);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   从文件读入矩阵并原地装入本句柄。
	/// </summary>
	/// <param name="fileName">矩阵文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 819：先 Dispose() 释放本实例原句柄，StoreS(0, fileName) 传文件名，InitOCT(0)+Load(0) 把读到的矩阵原地装载回 this。属原地改写，无返回值。</para>
	///   <para><b>约束或前提</b>会丢弃 this 原有内容；文件须为本库 WriteMatrix 写出的格式，不存在或格式非法由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>与构造器 JlMatrix(string) 同 id 819：构造器给新句柄，本方法复用已有句柄原地改；循环读多文件时用本法避免反复 new/Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix();
	///   m.ReadMatrix("data.dat");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>内部先 Dispose 再装载，读失败时本句柄可能变无效态 [待实测]；对象本身由调用方最后统一 Dispose。</para>
	/// </remarks>
	public void ReadMatrix(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(819);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把本矩阵写入文件。
	/// </summary>
	/// <param name="fileFormat">文件格式。Default: "binary"</param>
	/// <param name="fileName">目标文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 820：Store(0) 传本句柄，StoreS(1, fileFormat)、StoreS(2, fileName)；参数序为 [矩阵, 格式, 文件名]。只读不改动 this，无返回值。</para>
	///   <para><b>约束或前提</b>目标路径可写、目录须已存在 [待实测]；fileFormat 取值须为原生层支持的字符串（如 "binary"），拼写错误由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要存进 Stream（内存/网络）用 Serialize(Stream)；要落盘成可用 JlMatrix(string)/ReadMatrix 读回的文件用本法。读写文件与序列化流两套格式，不保证互相通用 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   m.WriteMatrix("binary", "data.dat");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>写出的文件由 JlMatrix(string fileName) 或 ReadMatrix 读回；本方法不产生新句柄。</para>
	/// </remarks>
	public void WriteMatrix(string fileFormat, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(820);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileFormat);
		JlNativeApi.StoreS(proc, 2, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   对矩阵做正交分解（如 QR），返回正交部分并输出三角部分。
	/// </summary>
	/// <param name="decompositionType">分解方法。Default: "qr"</param>
	/// <param name="outputMatricesType">输出矩阵形态。Default: "full"</param>
	/// <param name="computeOrthogonal">是否计算正交矩阵。Default: "true"</param>
	/// <param name="matrixTriangularID">输出：被分解矩阵的三角部分（新句柄）。</param>
	/// <returns>被分解矩阵的正交部分（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 821：Store(0) 传本句柄，StoreS(1,decompositionType)、StoreS(2,outputMatricesType)、StoreS(3,computeOrthogonal)，两个 InitOCT+LoadNew 分别装载正交部分（返回值）与三角部分（out）。不改动 this。</para>
	///   <para><b>约束或前提</b>返回与 out 各是独立新句柄，用完都须 Dispose；QR 分解要求输入为方阵或按 decompositionType 支持的形状 [待实测]。computeOrthogonal 置非 "true" 时正交部分可能不被实际计算 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要一般特征/谱分解用 DecomposeMatrix 或 SvdMatrix；本法专给正交-三角（QR 等）分解对。outputMatricesType 控制返回紧凑还是满尺寸矩阵。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 1.0);
	///   JlMatrix tri;
	///   JlMatrix orth = m.OrthogonalDecomposeMatrix("qr", "full", "true", out tri);
	///   orth.Dispose();
	///   tri.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个输出句柄任一漏 Dispose 都会泄漏原生矩阵内存；返回正交、out 为三角的顺序勿混。</para>
	/// </remarks>
	public JlMatrix OrthogonalDecomposeMatrix(string decompositionType, string outputMatricesType, string computeOrthogonal, out JlMatrix matrixTriangularID)
	{
		IntPtr proc = JlNativeApi.PreCall(821);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, decompositionType);
		JlNativeApi.StoreS(proc, 2, outputMatricesType);
		JlNativeApi.StoreS(proc, 3, computeOrthogonal);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		err = LoadNew(proc, 1, err, out matrixTriangularID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   分解矩阵，返回输出矩阵 1 并输出矩阵 2。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="matrix2ID">输出：分解结果第二矩阵（新句柄）。</param>
	/// <returns>分解结果第一矩阵（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 822：Store(0) 传本句柄，StoreS(1, matrixType)，两个 InitOCT+LoadNew 分别装载矩阵 1（返回值）与矩阵 2（out）。不改动 this。</para>
	///   <para><b>约束或前提</b>返回与 out 各是独立新句柄，均须 Dispose；matrixType 选 "general"/对称等影响分解算法与所需输入形状 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>本法是通用分解入口；已知要 QR/特征/SVD 时分别用 OrthogonalDecomposeMatrix、Eigenvalues*、SvdMatrix 更直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 1.0);
	///   JlMatrix mat2;
	///   JlMatrix mat1 = m.DecomposeMatrix("general", out mat2);
	///   mat1.Dispose();
	///   mat2.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个输出句柄都要单独释放；matrix1/matrix2 的含义随 matrixType 而变，勿按固定语义假设 [待实测]。</para>
	/// </remarks>
	public JlMatrix DecomposeMatrix(string matrixType, out JlMatrix matrix2ID)
	{
		IntPtr proc = JlNativeApi.PreCall(822);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		err = LoadNew(proc, 1, err, out matrix2ID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   计算奇异值分解 SVD，返回左奇异向量并输出奇异值、右奇异向量。
	/// </summary>
	/// <param name="SVDType">计算类型。Default: "full"</param>
	/// <param name="computeSingularVectors">奇异向量计算范围。Default: "both"</param>
	/// <param name="matrixSID">输出：奇异值矩阵（新句柄）。</param>
	/// <param name="matrixVID">输出：右奇异向量矩阵（新句柄）。</param>
	/// <returns>左奇异向量矩阵（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 823：Store(0) 传本句柄，StoreS(1,SVDType)、StoreS(2,computeSingularVectors)，三个 InitOCT+LoadNew 分别装载左奇异向量（返回）、奇异值（out S）、右奇异向量（out V）。不改动 this。</para>
	///   <para><b>约束或前提</b>返回与两个 out 共三个独立新句柄，全部须 Dispose；computeSingularVectors 取非 "both" 时对应奇异向量矩阵可能不被计算 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要奇异值不要向量时用更省的取法；要特征值分解用 Eigenvalues*；SVD 对任意（含非方阵）矩阵都稳定，比对称特征法适用范围广。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 1.0);
	///   JlMatrix s, v;
	///   JlMatrix u = m.SvdMatrix("full", "both", out s, out v);
	///   u.Dispose();
	///   s.Dispose();
	///   v.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三输出句柄任一漏 Dispose 都泄漏；奇异值按 SVDType 可能落在对角阵或列向量，形状随参数变 [待实测]。</para>
	/// </remarks>
	public JlMatrix SvdMatrix(string SVDType, string computeSingularVectors, out JlMatrix matrixSID, out JlMatrix matrixVID)
	{
		IntPtr proc = JlNativeApi.PreCall(823);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, SVDType);
		JlNativeApi.StoreS(proc, 2, computeSingularVectors);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		err = LoadNew(proc, 1, err, out matrixSID);
		err = LoadNew(proc, 2, err, out matrixVID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   计算广义（成对矩阵 A、B）特征值，可选广义特征向量。
	/// </summary>
	/// <param name="matrixBID">输入矩阵 B 的句柄。</param>
	/// <param name="computeEigenvectors">是否计算特征向量。Default: "none"</param>
	/// <param name="eigenvaluesRealID">输出：特征值实部（新句柄）。</param>
	/// <param name="eigenvaluesImagID">输出：特征值虚部（新句柄）。</param>
	/// <param name="eigenvectorsRealID">输出：特征向量实部（新句柄）。</param>
	/// <param name="eigenvectorsImagID">输出：特征向量虚部（新句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 824：Store(0) 传 A（this），Store(1, matrixBID) 传 B，StoreS(2, computeEigenvectors)，四个 InitOCT+LoadNew 分别装载特征值实/虚部、特征向量实/虚部。无返回值，结果全在四个 out。</para>
	///   <para><b>约束或前提</b>四个 out 各是独立新句柄，全部须 Dispose；广义特征问题针对 A、B 同维方阵，B 奇异时可能报错 [待实测]。computeEigenvectors 为 "none" 时特征向量矩阵内容可能为空 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>这是"广义"（含 B）版本；只处理单矩阵用 EigenvaluesGeneralMatrix。特征值可能为复数，须成对读实部+虚部两矩阵，只读一个会丢信息。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   JlMatrix valRe, valIm, vecRe, vecIm;
	///   a.GeneralizedEigenvaluesGeneralMatrix(b, "none", out valRe, out valIm, out vecRe, out vecIm);
	///   valRe.Dispose();
	///   valIm.Dispose();
	///   vecRe.Dispose();
	///   vecIm.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>四输出句柄任一漏 Dispose 都泄漏；实/虚部成对使用才表示完整复特征值。</para>
	/// </remarks>
	public void GeneralizedEigenvaluesGeneralMatrix(JlMatrix matrixBID, string computeEigenvectors, out JlMatrix eigenvaluesRealID, out JlMatrix eigenvaluesImagID, out JlMatrix eigenvectorsRealID, out JlMatrix eigenvectorsImagID)
	{
		IntPtr proc = JlNativeApi.PreCall(824);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.StoreS(proc, 2, computeEigenvectors);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out eigenvaluesRealID);
		err = LoadNew(proc, 1, err, out eigenvaluesImagID);
		err = LoadNew(proc, 2, err, out eigenvectorsRealID);
		err = LoadNew(proc, 3, err, out eigenvectorsImagID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
	}

	/// <summary>
	///   计算对称矩阵对的广义特征值，可选特征向量。
	/// </summary>
	/// <param name="matrixBID">对称正定输入矩阵 B 的句柄。</param>
	/// <param name="computeEigenvectors">是否计算特征向量。Default: "false"</param>
	/// <param name="eigenvectorsID">输出：特征向量矩阵（新句柄）。</param>
	/// <returns>特征值矩阵（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 825：Store(0) 传 A（this），Store(1, matrixBID) 传 B，StoreS(2, computeEigenvectors)，两个 InitOCT+LoadNew 装载特征值（返回）与特征向量（out）。不改动 this。</para>
	///   <para><b>约束或前提</b>返回与 out 各是独立新句柄，须 Dispose；要求 A 对称、B 对称正定，B 非正定时可能报错 [待实测]。对称广义问题特征值为实数，不像一般版需实/虚部成对。</para>
	///   <para><b>与相邻算子的取舍</b>这是对称版，比 GeneralizedEigenvaluesGeneralMatrix 快且只出实特征值；矩阵不对称时须改用一般版。单矩阵对称特征问题用 EigenvaluesSymmetricMatrix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   JlMatrix vec;
	///   JlMatrix val = a.GeneralizedEigenvaluesSymmetricMatrix(b, "false", out vec);
	///   val.Dispose();
	///   vec.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>computeEigenvectors 为 "false" 时特征向量句柄仍被分配但内容可能为空 [待实测]。</para>
	/// </remarks>
	public JlMatrix GeneralizedEigenvaluesSymmetricMatrix(JlMatrix matrixBID, string computeEigenvectors, out JlMatrix eigenvectorsID)
	{
		IntPtr proc = JlNativeApi.PreCall(825);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.StoreS(proc, 2, computeEigenvectors);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		err = LoadNew(proc, 1, err, out eigenvectorsID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
		return obj;
	}

	/// <summary>
	///   计算单个一般矩阵的特征值，可选特征向量。
	/// </summary>
	/// <param name="computeEigenvectors">是否计算特征向量。Default: "none"</param>
	/// <param name="eigenvaluesRealID">输出：特征值实部（新句柄）。</param>
	/// <param name="eigenvaluesImagID">输出：特征值虚部（新句柄）。</param>
	/// <param name="eigenvectorsRealID">输出：特征向量实部（新句柄）。</param>
	/// <param name="eigenvectorsImagID">输出：特征向量虚部（新句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 826：Store(0) 传本句柄，StoreS(1, computeEigenvectors)，四个 InitOCT+LoadNew 装载特征值实/虚部、特征向量实/虚部。无返回值，结果全在 out。</para>
	///   <para><b>约束或前提</b>四个 out 各是独立新句柄，须全部 Dispose；一般矩阵特征值可为复数，须成对读实部+虚部，只读一个丢信息。computeEigenvectors="none" 时向量矩阵内容可能为空 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>矩阵若对称，改用 EigenvaluesSymmetricMatrix 更快且只出实特征值；含第二矩阵 B 的广义问题用 GeneralizedEigenvaluesGeneralMatrix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   JlMatrix valRe, valIm, vecRe, vecIm;
	///   m.EigenvaluesGeneralMatrix("none", out valRe, out valIm, out vecRe, out vecIm);
	///   valRe.Dispose();
	///   valIm.Dispose();
	///   vecRe.Dispose();
	///   vecIm.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>四输出句柄任一漏 Dispose 都泄漏；实/虚部成对才表示完整复特征值。</para>
	/// </remarks>
	public void EigenvaluesGeneralMatrix(string computeEigenvectors, out JlMatrix eigenvaluesRealID, out JlMatrix eigenvaluesImagID, out JlMatrix eigenvectorsRealID, out JlMatrix eigenvectorsImagID)
	{
		IntPtr proc = JlNativeApi.PreCall(826);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, computeEigenvectors);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out eigenvaluesRealID);
		err = LoadNew(proc, 1, err, out eigenvaluesImagID);
		err = LoadNew(proc, 2, err, out eigenvectorsRealID);
		err = LoadNew(proc, 3, err, out eigenvectorsImagID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   计算对称矩阵的特征值，可选特征向量。
	/// </summary>
	/// <param name="computeEigenvectors">是否计算特征向量。Default: "false"</param>
	/// <param name="eigenvectorsID">输出：特征向量矩阵（新句柄）。</param>
	/// <returns>特征值矩阵（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 827：Store(0) 传本句柄，StoreS(1, computeEigenvectors)，两个 InitOCT+LoadNew 装载特征值（返回）与特征向量（out）。不改动 this。</para>
	///   <para><b>约束或前提</b>返回与 out 各是独立新句柄，须 Dispose；输入须为对称矩阵，非对称时结果不保证正确 [待实测]。对称矩阵特征值为实数，无需虚部。</para>
	///   <para><b>与相邻算子的取舍</b>比 EigenvaluesGeneralMatrix 快且只出实特征值；矩阵不对称务必改用一般版。含 B 的对称广义问题用 GeneralizedEigenvaluesSymmetricMatrix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   JlMatrix vec;
	///   JlMatrix val = m.EigenvaluesSymmetricMatrix("false", out vec);
	///   val.Dispose();
	///   vec.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>computeEigenvectors="false" 时特征向量句柄仍被分配但内容可能为空 [待实测]。</para>
	/// </remarks>
	public JlMatrix EigenvaluesSymmetricMatrix(string computeEigenvectors, out JlMatrix eigenvectorsID)
	{
		IntPtr proc = JlNativeApi.PreCall(827);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, computeEigenvectors);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		err = LoadNew(proc, 1, err, out eigenvectorsID);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   解方程组 A·x = B：本句柄为系数矩阵 A，右端为 B，返回解 x。
	/// </summary>
	/// <param name="matrixLHSType">左端（系数）矩阵类型。Default: "general"</param>
	/// <param name="epsilon">求解方式及把奇异值视为 0 的阈值。Default: 0.0</param>
	/// <param name="matrixRHSID">右端矩阵 B 的句柄。</param>
	/// <returns>解 x 的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 828：Store(0) 传 A（this），StoreS(1, matrixLHSType)、StoreD(2, epsilon)、Store(3, matrixRHSID) 传 B，LoadNew 返回解。A、B 均不被改动。</para>
	///   <para><b>约束或前提</b>this 是左端系数矩阵、matrixRHSID 是右端项，别写反；operator/ 即把右操作数当 A、左操作数当 B 调本法。A 须可逆/适定，维度须 A 行数=B 行数 [待实测]。epsilon&gt;0 时把小于阈值的奇异值当 0 处理（近似最小二乘/降秩解）[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要 A⁻¹ 本身用 InvertMatrix；批量同一 A 解不同 B 可复用 A。matrixLHSType 选对称/三角等可让原生层走更快的专用解法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 1, 3.0);
	///   JlMatrix x = a.SolveMatrix("general", 0.0, b);
	///   x.Dispose();
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>解是新句柄须 Dispose；奇异 A 会报错或依赖 epsilon 退化，不保证给出最小二乘解 [待实测]。</para>
	/// </remarks>
	public JlMatrix SolveMatrix(string matrixLHSType, double epsilon, JlMatrix matrixRHSID)
	{
		IntPtr proc = JlNativeApi.PreCall(828);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixLHSType);
		JlNativeApi.StoreD(proc, 2, epsilon);
		JlNativeApi.Store(proc, 3, matrixRHSID);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixRHSID);
		return obj;
	}

	/// <summary>
	///   计算行列式，返回标量。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <returns>行列式值（托管 double，非句柄，无需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 829：Store(0) 传本句柄，StoreS(1, matrixType)，InitOCT(0)+LoadD(0) 按 DOUBLE 装载单个标量结果。不改动 this。</para>
	///   <para><b>约束或前提</b>行列式只对方阵有定义，非方阵由原生层报错 [待实测]；matrixType 选对称/三角等可能走专用快速路径但数学结果应一致 [待实测]。结果仅为第一个装载值。</para>
	///   <para><b>与相邻算子的取舍</b>想知道可逆性，判行列式是否≈0 不如直接 InvertMatrix 看是否报错（病态矩阵行列式可能极小但仍非零）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   double det = m.DeterminantMatrix("general");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回纯标量无句柄泄漏风险；大矩阵行列式易溢出/下溢到 0，数值判奇异性须谨慎 [待实测]。</para>
	/// </remarks>
	public double DeterminantMatrix(string matrixType)
	{
		IntPtr proc = JlNativeApi.PreCall(829);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   就地求逆：把本句柄矩阵原地替换为其逆矩阵。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="epsilon">求逆方式/奇异阈值。Default: 0.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 830：Store(0) 传本句柄，StoreS(1, matrixType)、StoreD(2, epsilon)，无 LoadNew——直接改写 this 为逆矩阵，不返回新句柄。</para>
	///   <para><b>约束或前提</b>原地覆盖原内容，调用后 this 已是 A⁻¹，原矩阵不可恢复；要求方阵且非奇异，奇异时由原生层报错 [待实测]。epsilon&gt;0 时按阈值截断小奇异值 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要保留原矩阵、另得逆用 InvertMatrix（返回新句柄）；只为解 A x=b 时别先本法求逆再乘，直接 SolveMatrix 更稳更快。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   m.InvertMatrixMod("general", 0.0);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>句柄不变、内容被替换，仍由调用方 Dispose；对已 Dispose 的句柄调用会报错 [待实测]。</para>
	/// </remarks>
	public void InvertMatrixMod(string matrixType, double epsilon)
	{
		IntPtr proc = JlNativeApi.PreCall(830);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.StoreD(proc, 2, epsilon);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   求逆矩阵，返回新句柄，原矩阵保持不变。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="epsilon">求逆方式/奇异阈值。Default: 0.0</param>
	/// <returns>逆矩阵的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 831：Store(0) 传本句柄，StoreS(1, matrixType)、StoreD(2, epsilon)，InitOCT(0)+LoadNew 装载逆矩阵为新句柄返回。this 不被改动。</para>
	///   <para><b>约束或前提</b>要求方阵且非奇异，奇异时由原生层报错 [待实测]；epsilon&gt;0 按阈值截断小奇异值 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想覆盖原句柄用 InvertMatrixMod；只是解线性方程组不要显式求逆，用 SolveMatrix 数值更稳。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   JlMatrix inv = m.InvertMatrix("general", 0.0);
	///   inv.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须单独 Dispose；m 与 inv 各自独立释放。</para>
	/// </remarks>
	public JlMatrix InvertMatrix(string matrixType, double epsilon)
	{
		IntPtr proc = JlNativeApi.PreCall(831);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.StoreD(proc, 2, epsilon);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   就地转置：本句柄矩阵原地行列互换。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 832：仅 Store(0) 传本句柄，无参数、无 LoadNew——直接把 this 改写为其转置，维度随之互换（rows↔columns）。</para>
	///   <para><b>约束或前提</b>原地覆盖，调用后 this 即 Aᵀ，再调一次还原为 A；方阵之外维度会变，随后 NumRows/NumColumns 读到的是新维度。</para>
	///   <para><b>与相邻算子的取舍</b>要保留原矩阵、另得转置用 TransposeMatrix（返回新句柄）；本法零额外句柄分配，适合中间量可丢弃的场景。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 3, 1.0);
	///   m.TransposeMatrixMod();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>句柄不变仍归调用方 Dispose；连续 *Mod 可原地反复翻转而不增加原生对象。</para>
	/// </remarks>
	public void TransposeMatrixMod()
	{
		IntPtr proc = JlNativeApi.PreCall(832);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   转置矩阵，返回新句柄，原矩阵不变。
	/// </summary>
	/// <returns>转置矩阵的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 833：Store(0) 传本句柄，InitOCT(0)+LoadNew 装载转置为新句柄返回。this 不被改动。</para>
	///   <para><b>约束或前提</b>返回维度为原矩阵的 columns×rows；无参数，任何形状都可转置。</para>
	///   <para><b>与相邻算子的取舍</b>不需要保留原矩阵时用 TransposeMatrixMod 原地转置省一次分配。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 3, 1.0);
	///   JlMatrix t = m.TransposeMatrix();
	///   t.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须单独 Dispose。</para>
	/// </remarks>
	public JlMatrix TransposeMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(833);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按指定维度归约求最大，返回含最大值的新句柄。
	/// </summary>
	/// <param name="maxType">求最大的归约方向。Default: "columns"</param>
	/// <returns>最大值矩阵的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 834：Store(0) 传本句柄，StoreS(1, maxType)，InitOCT(0)+LoadNew 返回归约结果。this 不变。</para>
	///   <para><b>约束或前提</b>maxType 决定按列/行/全体归约，结果维度随之不同（如按列归约得单行）[待实测]。与逐元素两两求 max 不同，本算子只针对单个矩阵。</para>
	///   <para><b>与相邻算子的取舍</b>要最小用 MinMatrix（id 835，同结构）；要两个矩阵逐元素取大需要别的组合，本族未直接提供。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 3, 1.0);
	///   JlMatrix mx = m.MaxMatrix("columns");
	///   mx.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须 Dispose；结果形状依赖 maxType，读取前建议先看 NumRows/NumColumns。</para>
	/// </remarks>
	public JlMatrix MaxMatrix(string maxType)
	{
		IntPtr proc = JlNativeApi.PreCall(834);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, maxType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按指定维度归约求最小，返回含最小值的新句柄。
	/// </summary>
	/// <param name="minType">求最小的归约方向。Default: "columns"</param>
	/// <returns>最小值矩阵的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 835：Store(0) 传本句柄，StoreS(1, minType)，InitOCT(0)+LoadNew 返回归约结果。this 不变。</para>
	///   <para><b>约束或前提</b>minType 决定按列/行/全体归约，结果维度随之不同 [待实测]；只针对单个矩阵归约。</para>
	///   <para><b>与相邻算子的取舍</b>与 MaxMatrix（id 834）成对，仅归约方向相反；两者结构完全一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 3, 1.0);
	///   JlMatrix mn = m.MinMatrix("columns");
	///   mn.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须 Dispose；结果形状依赖 minType。</para>
	/// </remarks>
	public JlMatrix MinMatrix(string minType)
	{
		IntPtr proc = JlNativeApi.PreCall(835);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, minType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   就地求矩阵幂：把本句柄改写为其 power 次幂（元组指数）。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="power">指数（元组形式）。Default: 2.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 836：Store(0) 传本句柄，StoreS(1, matrixType)，Store(2, power) 钉住指数元组，调用后 UnpinTuple(power)；无 LoadNew，原地改写 this。这是矩阵幂（A^p），非逐元素幂。</para>
	///   <para><b>约束或前提</b>矩阵幂通常要求方阵 [待实测]；原地覆盖后原矩阵不可恢复。与 double 重载同 id 836，区别仅在指数以钉固定的 JlTuple 传入（多值 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>要保留原矩阵用 PowMatrix(JlTuple)（id 837 返回新句柄）；单一带量用 double 重载省钉固定；逐元素求幂用 PowScalarElementMatrixMod/PowElementMatrix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 2.0);
	///   m.PowMatrixMod("general", new JlTuple(2.0));
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>power 元组在调用结束前不得释放，本法已 UnpinTuple；this 句柄不变仍归调用方 Dispose。</para>
	/// </remarks>
	public void PowMatrixMod(string matrixType, JlTuple power)
	{
		IntPtr proc = JlNativeApi.PreCall(836);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.Store(proc, 2, power);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(power);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   就地求矩阵幂：把本句柄改写为其标量 power 次幂。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="power">指数（标量）。Default: 2.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 836：Store(0) 传本句柄，StoreS(1, matrixType)，StoreD(2, power) 直写标量指数；无 LoadNew，原地改写 this。矩阵幂 A^p，非逐元素幂。</para>
	///   <para><b>约束或前提</b>矩阵幂通常要求方阵 [待实测]；原地覆盖不可恢复。与 JlTuple 重载同 id 836，本法 StoreD 直写、无钉固定/UnpinTuple 开销。</para>
	///   <para><b>与相邻算子的取舍</b>单一整数/实数指数用本重载最快；要保留原矩阵用 PowMatrix(double)（id 837 新句柄）；逐元素幂用 PowScalarElementMatrixMod。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 2.0);
	///   m.PowMatrixMod("general", 2.0);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>this 句柄不变仍归调用方 Dispose；指数为负或分数按矩阵幂语义处理 [待实测]。</para>
	/// </remarks>
	public void PowMatrixMod(string matrixType, double power)
	{
		IntPtr proc = JlNativeApi.PreCall(836);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.StoreD(proc, 2, power);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   求矩阵幂，返回新句柄（元组指数），原矩阵不变。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="power">指数（元组形式）。Default: 2.0</param>
	/// <returns>幂结果矩阵的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 837：Store(0) 传本句柄，StoreS(1, matrixType)，Store(2, power) 钉住指数元组，InitOCT(0)+LoadNew 装载结果为新句柄，调用后 UnpinTuple(power)。this 不变。矩阵幂 A^p。</para>
	///   <para><b>约束或前提</b>矩阵幂通常要求方阵 [待实测]；与 double 重载同 id 837，本法走 Store+UnpinTuple 钉固定。返回独立新句柄须 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>要原地改写用 PowMatrixMod(JlTuple)（id 836）；单一带量用 PowMatrix(double) 省钉固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 2.0);
	///   JlMatrix p = m.PowMatrix("general", new JlTuple(2.0));
	///   p.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>power 元组在调用结束前不得释放，本法已 UnpinTuple；返回句柄与 m 各自独立 Dispose。</para>
	/// </remarks>
	public JlMatrix PowMatrix(string matrixType, JlTuple power)
	{
		IntPtr proc = JlNativeApi.PreCall(837);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.Store(proc, 2, power);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(power);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   求矩阵幂，返回新句柄（标量指数），原矩阵不变。
	/// </summary>
	/// <param name="matrixType">输入矩阵类型。Default: "general"</param>
	/// <param name="power">指数（标量）。Default: 2.0</param>
	/// <returns>幂结果矩阵的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 837：Store(0) 传本句柄，StoreS(1, matrixType)，StoreD(2, power) 直写标量指数，InitOCT(0)+LoadNew 装载结果为新句柄。this 不变。矩阵幂 A^p。</para>
	///   <para><b>约束或前提</b>矩阵幂通常要求方阵 [待实测]；与 JlTuple 重载同 id 837，本法 StoreD 直写、无钉固定。返回独立新句柄须 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>要原地改写用 PowMatrixMod(double)（id 836）；指数为向量用 JlTuple 重载。单一带量下本法最省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 2.0);
	///   JlMatrix p = m.PowMatrix("general", 3.0);
	///   p.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄与 m 各自独立 Dispose；负/分数指数按矩阵幂语义 [待实测]。</para>
	/// </remarks>
	public JlMatrix PowMatrix(string matrixType, double power)
	{
		IntPtr proc = JlNativeApi.PreCall(837);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, matrixType);
		JlNativeApi.StoreD(proc, 2, power);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   就地逐元素求幂：本句柄各元素取指数矩阵对应元素为幂，原地改写。
	/// </summary>
	/// <param name="matrixExpID">含各元素指数的矩阵句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 838：Store(0) 传底数矩阵（this），Store(1, matrixExpID) 传指数矩阵；无 LoadNew，逐元素 this[i]=this[i]^exp[i]，原地改写。</para>
	///   <para><b>约束或前提</b>底数与指数矩阵须同维，否则由原生层报错 [待实测]；这与 PowMatrixMod（矩阵幂 A^p、把整矩阵当底连乘）本质不同，别混用。原地覆盖后底数不可恢复。</para>
	///   <para><b>与相邻算子的取舍</b>要保留底数用 PowElementMatrix（id 839 返回新句柄）；全元素同指数用 PowScalarElementMatrixMod。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix base1 = new JlMatrix(2, 2, 2.0);
	///   JlMatrix expo = new JlMatrix(2, 2, 3.0);
	///   base1.PowElementMatrixMod(expo);
	///   expo.Dispose();
	///   base1.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>matrixExpID 在原生调用结束前不得释放（GC.KeepAlive 保活），且仍归调用方 Dispose。</para>
	/// </remarks>
	public void PowElementMatrixMod(JlMatrix matrixExpID)
	{
		IntPtr proc = JlNativeApi.PreCall(838);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixExpID);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixExpID);
	}

	/// <summary>
	///   逐元素求幂，返回新句柄，底数矩阵不变。
	/// </summary>
	/// <param name="matrixExpID">含各元素指数的矩阵句柄。</param>
	/// <returns>逐元素幂结果的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 839：Store(0) 传底数矩阵（this），Store(1, matrixExpID) 传指数矩阵，InitOCT(0)+LoadNew 返回逐元素 this[i]^exp[i] 的新句柄。底数不变。</para>
	///   <para><b>约束或前提</b>底数与指数矩阵须同维 [待实测]；这是逐元素幂，非 PowMatrix 的矩阵幂 A^p。返回独立新句柄须 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>要原地改写底数用 PowElementMatrixMod（id 838）；全元素同指数用 PowScalarElementMatrix 系列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix base1 = new JlMatrix(2, 2, 2.0);
	///   JlMatrix expo = new JlMatrix(2, 2, 3.0);
	///   JlMatrix r = base1.PowElementMatrix(expo);
	///   r.Dispose();
	///   expo.Dispose();
	///   base1.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>matrixExpID 与底数、结果各自独立 Dispose；结果与入参互不共享内存。</para>
	/// </remarks>
	public JlMatrix PowElementMatrix(JlMatrix matrixExpID)
	{
		IntPtr proc = JlNativeApi.PreCall(839);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixExpID);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixExpID);
		return obj;
	}

	/// <summary>
	///   就地逐元素求幂（元组指数）：本句柄各元素取 power 为幂，原地改写。
	/// </summary>
	/// <param name="power">指数（元组形式）。Default: 2.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>转原生算子 id 840：Store(0) 传底数矩阵（this），Store(1, power) 钉住指数元组，调用后 UnpinTuple(power)；无 LoadNew，逐元素把 this 各元素求幂，原地改写。base 矩阵、指数来自 power。</para>
	///   <para><b>约束或前提</b>与 double 重载同 id 840，区别是指数以钉固定的 JlTuple 传入（可多值 [待实测]）；原地覆盖不可恢复。这与 PowElementMatrix（指数是另一矩阵）不同。</para>
	///   <para><b>与相邻算子的取舍</b>单一标量指数用 PowScalarElementMatrixMod(double) 省钉固定；要保留底数另得结果则用带返回值的逐元素幂族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 3.0);
	///   m.PowScalarElementMatrixMod(new JlTuple(2.0));
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>power 元组在原生调用结束前不得释放，本法已 UnpinTuple；this 句柄不变仍归调用方 Dispose。</para>
	/// </remarks>
	public void PowScalarElementMatrixMod(JlTuple power)
	{
		IntPtr proc = JlNativeApi.PreCall(840);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, power);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(power);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>原地给每个元素取同一标量次幂（x ← x^power），不产生新句柄。</summary>
	/// <param name="power">The power. Default: 2.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 840。输入原生参数序 0=this（Store 句柄）、1=power（StoreD 直写 DOUBLE）；方法体无 InitOCT/Load，结果由原生层直接写回 this，维度不变。</para>
	///   <para><b>约束或前提</b>原值即时消失，回退只能靠事先 CopyMatrix 快照；0^负数、负底数分数指数等退化组合的结果由原生层决定 [待实测]。多次调用是幂的复合（指数相乘），不是重复同一变换。</para>
	///   <para><b>与相邻算子的取舍</b>要保留原矩阵用 PowScalarElementMatrix（id 841 返回新句柄）；指数来自别处算子的 JlTuple 输出时用本方法的 JlTuple 重载（多一次钉固定/解固定）；矩阵自乘意义的 A^n 用 PowMatrixMod（id 836）。</para>
	///   <para><b>参数取向</b>void 返回；this 是唯一被改写者。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 3.0);
	///   m.PowScalarElementMatrixMod(2.0);
	///   double v = m[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>int/long 字面量隐式提升为 double 命中本重载，静默无精度损失（2^53 以内）；开平方别用本法传 0.5，用 SqrtMatrixMod 更不易错。</para>
	/// </remarks>
	public void PowScalarElementMatrixMod(double power)
	{
		IntPtr proc = JlNativeApi.PreCall(840);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, power);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>用元组携带的指数对全矩阵逐元素取幂，返回新句柄；与标量重载同一个原生算子。</summary>
	/// <param name="power">The power. Default: 2.0</param>
	/// <returns>新 JlMatrix 句柄（各元素取 power 给出的指数次幂），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 841，与 PowScalarElementMatrix(double) 完全同一算子；唯一差别是 power 走 JlNativeApi.Store 把元组钉在原生输入 1 上、调用后 UnpinTuple 解除固定（double 重载用 StoreD 直写，无钉固定开销）。输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>传单元素元组与标量重载等价；传多条值时原生层按位置对应、广播还是只用第一条，托管层看不出来 [待实测]——需要严格的"逐元素不同指数"请改用 PowElementMatrix（指数必须是矩阵）。</para>
	///   <para><b>与相邻算子的取舍</b>指数是编译期常数时直接传 double 字面量命中标量重载，省一次钉固定；只有指数本来就是别的算子输出的 JlTuple 时才用本重载，避免先取首值再转 double。矩阵幂用 PowMatrix（id 837），别把逐元素幂当矩阵自乘。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 3.0);
	///   using JlMatrix sq = m.PowScalarElementMatrix(new double[] { 2.0 });
	///   double v = sq[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄要 Dispose。隐式转换生成的临时元组由本方法内部 UnpinTuple；自己 new 的 JlTuple 用完自行 Dispose（纯数值元组不调也不会漏原生句柄）。</para>
	/// </remarks>
	public JlMatrix PowScalarElementMatrix(JlTuple power)
	{
		IntPtr proc = JlNativeApi.PreCall(841);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, power);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(power);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>每个元素取同一标量次幂（x^power），返回同维度新句柄；是逐元素幂，不是矩阵幂。</summary>
	/// <param name="power">The power. Default: 2.0</param>
	/// <returns>新 JlMatrix 句柄（各元素的 power 次幂），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 841。输入原生参数序 0=this（Store 句柄）、1=power（StoreD 直写 DOUBLE，无元组钉固定）；输出 InitOCT(0)+LoadNew(0) 返回新句柄，this 不变。</para>
	///   <para><b>约束或前提</b>0^0、负底数配分数指数等退化组合的结果（NaN/报错）由原生层决定 [待实测]。power 用 int/long 字面量时隐式提升为 double，不会丢精度也命中不了别的重载。</para>
	///   <para><b>与相邻算子的取舍</b>要 A^n 那种矩阵自乘（矩阵幂）用 PowMatrix（id 837），两者对 n=2 结果都不同（逐元素平方 ≠ 矩阵自乘）；每个元素指数不同用 PowElementMatrix（id 839，指数须是等维度矩阵句柄）；指数来自别处算子的 JlTuple 输出时用本方法 JlTuple 重载；就地版是 PowScalarElementMatrixMod（id 840）。开平方有专用的 SqrtMatrix，比传 0.5 更不易错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 3.0);
	///   using JlMatrix sq = m.PowScalarElementMatrix(2.0);
	///   double v = sq[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄必须 Dispose；负指数会产生小数而非报错 [待实测]，别假定输出仍是整数语义。</para>
	/// </remarks>
	public JlMatrix PowScalarElementMatrix(double power)
	{
		IntPtr proc = JlNativeApi.PreCall(841);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, power);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>原地逐元素开平方（A ← √A 按元素），不产生新句柄，原值不可恢复。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 842。输入原生参数序只有 0=this（Store 句柄）；方法体无 InitOCT/Load，结果由原生层直接写回 this。维度不变。</para>
	///   <para><b>约束或前提</b>含负元素时 NaN 还是原生报错由原生层决定 [待实测]，本法不会自动取绝对值，必要时先手动 AbsMatrixMod。这是逐元素操作，不是求满足 X·X=A 的矩阵平方根。</para>
	///   <para><b>与相邻算子的取舍</b>要保留原值用 SqrtMatrix（id 843）；指数不是 0.5 的幂运算走 PowScalarElementMatrixMod（id 840）；本法无参数、不会传错指数，开方优先用它。</para>
	///   <para><b>参数取向</b>void 返回；this 原地改写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 4.0);
	///   m.SqrtMatrixMod();
	///   double v = m[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>反复调用会连续开方（指数不断减半），不是幂等操作；旧值只能靠事先 CopyMatrix 快照找回。</para>
	/// </remarks>
	public void SqrtMatrixMod()
	{
		IntPtr proc = JlNativeApi.PreCall(842);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>逐元素开平方（不是矩阵平方根 A^{1/2}），返回同维度新句柄。</summary>
	/// <returns>新 JlMatrix 句柄（各元素 √x），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 843。输入原生参数序只有 0=this（Store 句柄）；输出 InitOCT(0)+LoadNew(0) 返回新句柄，this 不变。作用于每个元素：√x，维度不变。</para>
	///   <para><b>约束或前提</b>负元素的结果（NaN 还是原生报错）托管层不预检 [待实测]，方差/能量矩阵开方前先 AbsMatrix 或确认非负。本法是逐元素操作，若想要满足 X·X=A 的矩阵平方根，本库没有该算子 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>等价于 PowScalarElementMatrix(0.5)（id 841），但本法无参数、不会传错指数；任意次幂（0.33、-2 等）才用 Pow 族；要原地版本用 SqrtMatrixMod（id 842）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 4.0);
	///   using JlMatrix s = m.SqrtMatrix();
	///   double v = s[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄必须 Dispose；对 0 元素开方得 0，无奇异性问题。</para>
	/// </remarks>
	public JlMatrix SqrtMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(843);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>原地对 this 的每个元素取绝对值，不产生新句柄，原值不可恢复。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 844。输入原生参数序只有 0=this（Store 句柄）；方法体无 InitOCT/Load，即无输出装载，|x| 的结果由原生层直接写回 this。维度与元素类型（double）不变。</para>
	///   <para><b>约束或前提</b>调用后符号信息即丢失，之后想还原正负必须事先 CopyMatrix 存快照；NaN/Inf 元素的处理由原生层决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>还要保留原矩阵就用 AbsMatrix（id 845 返回新句柄）；只要一个标量幅值指标用 NormMatrix；取反保号用一元 operator -，本法是"去号"不是"变号"。</para>
	///   <para><b>参数取向</b>void 返回；this 是唯一被改写者。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, -3.0);
	///   m.AbsMatrixMod();
	///   double v = m[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无新增原生内存，也就没有额外 Dispose 义务；重复调用幂等（第二次取绝对值无变化），可与任何逐元素算子自由连用。</para>
	/// </remarks>
	public void AbsMatrixMod()
	{
		IntPtr proc = JlNativeApi.PreCall(844);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>对每个元素取绝对值，返回同维度的新矩阵句柄（this 不变）。</summary>
	/// <returns>新 JlMatrix 句柄（元素为 |x|），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 845。输入原生参数序只有 0=this（Store 句柄），无其他控制参数；输出 InitOCT(0)+LoadNew(0) 返回新句柄。逐元素 |x|：负号消失，小数部分保留。</para>
	///   <para><b>约束或前提</b>矩阵元素只有 double 一种，"绝对值"不存在整型最小值取负溢出的坑；NaN/Inf 元素取绝对值后的表现由原生层决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>不需要保留原矩阵时用 AbsMatrixMod（id 844，原地、少一个待释放句柄）；本法只消符号，与一元 operator -（全体变号，正也变负）不是一回事；想要行和范数这类标量指标去 NormMatrix（id 846），别拿逐元素绝对值当范数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, -3.0);
	///   using JlMatrix a = m.AbsMatrix();
	///   double v = a[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄必须 Dispose；输出维度与元素类型与原矩阵完全一致，可安全与 m 再做逐元素运算。</para>
	/// </remarks>
	public JlMatrix AbsMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(845);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>求矩阵范数，返回托管 double 标量（不是矩阵句柄，无原生资源要释放）。</summary>
	/// <param name="normType">Type of norm. Default: "2-norm"</param>
	/// <returns>范数值（DOUBLE 装载取回的第一个 double）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 846。输入原生参数序 0=this（Store 句柄）、1=normType（StoreS）；输出用 JlNativeApi.LoadD 按 DOUBLE 读回一个标量。计算 2-范数时原生侧要做奇异值分解，非方阵同样可算 [待实测：报错与否]。</para>
	///   <para><b>约束或前提</b>normType 支持的其余取值（如 1-范数、无穷范数、Frobenius 之类）及其确切定义托管层不校验也看不出来 [待实测]，换值前先在已知小矩阵上验证。空矩阵/零矩阵的范数由原生层决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要的是"逐列和/逐列均值"这类向量结果就用 SumMatrix/MeanMatrix（返回句柄），别拿范数当求和：2-范数是最大奇异值，不等于元素平方和的开方（那是 Frobenius）[待实测：本实现里 "2-norm" 的确切语义]。判断矩阵奇异性用 SvdMatrix 看奇异值谱，比单看范数可靠。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 3.0);
	///   double n = m.NormMatrix("2-norm");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回纯托管值，无泄漏问题；但每次调用都是完整原生求解过程，迭代收敛判断里频繁调用注意开销。</para>
	/// </remarks>
	public double NormMatrix(string normType)
	{
		IntPtr proc = JlNativeApi.PreCall(846);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, normType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>按列（或按行/整体，由 meanType 决定）求矩阵元素均值，返回一个新矩阵句柄。</summary>
	/// <param name="meanType">Type of mean determination. Default: "columns"</param>
	/// <returns>新 JlMatrix 句柄（各均值按 meanType 方向排布），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 847。输入原生参数序 0=this（Store 句柄）、1=meanType（StoreS 字符串直传，托管层不校验取值）；输出 InitOCT(0)+LoadNew(0) 返回新句柄，this 不变。</para>
	///   <para><b>约束或前提</b>meanType 支持的取值集合与各值下输出的行列形状托管层看不出来 [待实测]，拿到结果先 GetSizeMatrix 再取数。与 SumMatrix 一样是"归约成矩阵"，不是把均值广播回原尺寸。</para>
	///   <para><b>与相邻算子的取舍</b>它做的是统计归约，不是图像均值滤波——要给矩阵做平滑/卷积没有本算子，别被模板旧文案"图像滤波与预处理"误导；只要总和用 SumMatrix（id 848），要一个标量指标用 NormMatrix（id 846）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 2, 4.0);
	///   using JlMatrix mu = m.MeanMatrix("columns");
	///   double first = mu[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输出是新句柄必须 Dispose；均值在 double 域计算，无整数截断问题。</para>
	/// </remarks>
	public JlMatrix MeanMatrix(string meanType)
	{
		IntPtr proc = JlNativeApi.PreCall(847);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, meanType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按列（或按行/整体，由 sumType 决定）对矩阵元素求和，结果仍是一个新矩阵句柄而非标量。</summary>
	/// <param name="sumType">Type of summation. Default: "columns"</param>
	/// <returns>新 JlMatrix 句柄（各求和结果按 sumType 方向排布），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 848。输入原生参数序 0=this（Store 句柄）、1=sumType（StoreS 字符串直传原生，托管层不校验取值）；输出 InitOCT(0)+LoadNew(0) 返回新句柄，this 不变。</para>
	///   <para><b>约束或前提</b>"columns" 时输出是 1 行多列还是多行 1 列、以及是否支持"rows"/"all"之类取值，托管层看不出来 [待实测]，取回后先 GetSizeMatrix 确认形状再按下标读。sumType 拼错由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个总和使用 SumMatrix 后读 [0,0]（或对应位置），别用 NormMatrix——范数不是元素和；要平均值用 MeanMatrix（id 847）；要逐列最大/最小用 MaxMatrix/MinMatrix（id 834/835）。结果仍按 double 元素存放，无整型求和的溢出问题。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 2, 1.0);
	///   using JlMatrix s = m.SumMatrix("columns");
	///   s.GetSizeMatrix(out int r, out int c);
	///   double first = s[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输出是新句柄必须 Dispose；每次调用一次完整原生过程，循环里对多个矩阵分别求和无法省开销。</para>
	/// </remarks>
	public JlMatrix SumMatrix(string sumType)
	{
		IntPtr proc = JlNativeApi.PreCall(848);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, sumType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐元素相除并原地写回 this（A ← A÷B，B 只读），不产生新句柄。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B（作除数）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 849。输入原生参数序 0=this（被除数、被改写者）、1=matrixBID（除数，Store 句柄只读）；无 InitOCT/Load，结果由原生层直接落在 this 上。</para>
	///   <para><b>约束或前提</b>两矩阵维度须完全一致 [待实测：不匹配时报错还是广播]；B 含 0 元素时托管层不预检，Inf/NaN 或原生报错由原生层决定 [待实测]。this 原值即时消失，需要回退先做 CopyMatrix 快照。</para>
	///   <para><b>与相邻算子的取舍</b>要保留 A 用 DivElementMatrix（id 850 返回新句柄）；整块除以常数用 ScaleMatrixMod(1.0/k)，别为此造一张常数矩阵；operator / 是解方程组（id 828），不是逐元素除。</para>
	///   <para><b>参数取向</b>void 返回；this 是唯一被改写者，B 调用后仍归调用方。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 6.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   a.DivElementMatrixMod(b);
	///   double v = a[0, 0];
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多次调用是连除（除数累积相乘的效果），不是幂等；b 在本调用结束前不得释放（GC.KeepAlive 保证）。</para>
	/// </remarks>
	public void DivElementMatrixMod(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(849);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
	}

	/// <summary>两个矩阵逐元素相除（this ÷ B 按对应位置），返回新句柄；不是解方程组的 operator /。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B（作除数）。</param>
	/// <returns>新 JlMatrix 句柄（this 各元素除以 B 对应元素），维度与输入一致，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 850。输入原生参数序 0=this（被除数）、1=matrixBID（除数）；输出 InitOCT(0)+LoadNew(0) 返回新句柄，两个入参都不被改动。</para>
	///   <para><b>约束或前提</b>两矩阵维度须完全一致 [待实测：不匹配时报错还是广播]；B 中含 0 元素时的行为（Inf/NaN 或原生报错）托管层不做预检 [待实测]，归一化前应先把 B 清零位处理掉。</para>
	///   <para><b>与相邻算子的取舍</b>运算符 `A / B` 是解线性方程组（B·x=A，id 828），与逐元素除法完全是两码事，别指望 / 做掩模反归一化；就地除回 this 用 DivElementMatrixMod（id 849）；除以常数用 ScaleMatrixMod(1.0/k)。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 6.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   using JlMatrix q = a.DivElementMatrix(b);
	///   double v = q[0, 0];
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是独立新句柄须 Dispose；被除数在左：a.DivElementMatrix(b) 是 a÷b，反过来要自己再求或先 Copy。</para>
	/// </remarks>
	public JlMatrix DivElementMatrix(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(850);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
		return obj;
	}

	/// <summary>两个矩阵逐元素相乘并原地写回 this（A ← A∘B），不产生新句柄。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 851。输入原生参数序 0=this（Store 句柄）、1=matrixBID（Store 句柄）；方法体没有 InitOCT/Load，即无输出装载，结果由原生层直接落在 this 上。matrixBID 只被读取，调用后仍归调用方。</para>
	///   <para><b>约束或前提</b>两矩阵行列维度须完全一致，维度不匹配由原生层报错、不广播 [待实测]；维度不变、旧值不保留，改错了只能靠事先 CopyMatrix 的快照恢复。</para>
	///   <para><b>与相邻算子的取舍</b>要保留 A 用 MultElementMatrix（id 852 返回新句柄）；整块乘常数用 ScaleMatrixMod（id 853，double 直写更省）；矩阵乘法（A←A·B）是 MultMatrixMod（id 859），别混淆。</para>
	///   <para><b>参数取向</b>void 返回；this 是唯一被改写者。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 3.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   a.MultElementMatrixMod(b);
	///   double v = a[0, 0];
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多次调用连乘（B 的因子累积），不是各自独立；b 在本调用结束前不得释放（GC.KeepAlive 保证），平时用毕自行 Dispose 即可。</para>
	/// </remarks>
	public void MultElementMatrixMod(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(851);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
	}

	/// <summary>两个矩阵逐元素相乘（Hadamard 积），返回新句柄；不是线性代数的矩阵乘法。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <returns>新 JlMatrix 句柄（this 与 B 对应位置元素之积），维度与输入一致，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 852。输入原生参数序 0=this（Store 句柄）、1=matrixBID（Store 句柄）；输出 InitOCT(0)+LoadNew(0) 返回新句柄，this 不被改动。</para>
	///   <para><b>约束或前提</b>逐元素运算要求两矩阵行列维度完全一致，维度不匹配由原生层报错、不广播 [待实测]。乘 0 得 0，与矩阵是否奇异无关。</para>
	///   <para><b>与相邻算子的取舍</b>运算符 `a * b` 走的是矩阵乘法 MultMatrix（id 860，A 列数须等于 B 行数），别用它做逐元素乘；就地逐元素乘回 A 用 MultElementMatrixMod（id 851）；整块统一倍率用 ScaleMatrix（标量重载更省，无句柄入参开销）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 3.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   using JlMatrix h = a.MultElementMatrix(b);
	///   double v = h[0, 0];
	///   b.Dispose();
	///   a.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果是独立新句柄须 Dispose；两入参在本方法里只被读取（GC.KeepAlive 保活到原生调用结束），调用后仍可继续使用或各自释放。</para>
	/// </remarks>
	public JlMatrix MultElementMatrix(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(852);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
		return obj;
	}

	/// <summary>用元组携带的缩放系数原地缩放 this（不产生新句柄）；与标量重载同一个原生算子。</summary>
	/// <param name="factor">Scale factor. Default: 2.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 853，与 ScaleMatrixMod(double) 同一算子；差别仅在 factor 走 JlNativeApi.Store 钉住元组、调用后 UnpinTuple 解除固定（double 重载用 StoreD 直写、无钉固定开销）。方法体无 InitOCT/Load，结果原地写回 this，维度不变。</para>
	///   <para><b>约束或前提</b>传单元素元组与标量重载等价；多条值是逐元素对应、广播还是只取第一条，托管层看不出来 [待实测]——要严格的逐元素不同倍率请用 MultElementMatrixMod（id 851，倍率须是等维度矩阵句柄）。原值即时消失。</para>
	///   <para><b>与相邻算子的取舍</b>系数是编译期常数时直接传 double 命中标量重载，省一次钉固定；系数本来就是别的算子输出的 JlTuple 时用本重载，避免先取首值转 double；要保留原矩阵用 ScaleMatrix 族（id 854）。</para>
	///   <para><b>参数取向</b>void 返回；this 是唯一被改写者，factor 只读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 3.0);
	///   m.ScaleMatrixMod(new double[] { 2.0 });
	///   double v = m[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>隐式转换出的临时元组由内部 UnpinTuple；自己 new 的 JlTuple 用完自行 Dispose（纯数值元组不调也不漏原生句柄）。多次调用是系数连乘，不是各自独立。</para>
	/// </remarks>
	public void ScaleMatrixMod(JlTuple factor)
	{
		IntPtr proc = JlNativeApi.PreCall(853);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, factor);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(factor);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>标量缩放的原地版本：this 的每个元素乘 factor，不产生新句柄。</summary>
	/// <param name="factor">Scale factor. Default: 2.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 853，与非原地版 ScaleMatrix（id 854）是两个算子。输入原生参数序 0=this、1=factor（StoreD 直写 DOUBLE）；方法体里没有 InitOCT/Load，结果由原生层写回 this。</para>
	///   <para><b>约束或前提</b>维度不变，只改数值；旧值调用后不可恢复。传整数字面量（如 2）时走标准隐式数值转换命中本重载，不会被判给 JlTuple 重载。</para>
	///   <para><b>与相邻算子的取舍</b>归一化、改符号、按迭代步长缩放这类"不需要保留原值"的场合用它，比 ScaleMatrix 少一个待释放句柄；还要原值就 ScaleMatrix 或 `m * k`。逐元素乘另一张矩阵请用 MultElementMatrixMod（851）。</para>
	///   <para><b>参数取向</b>void 返回；this 原地改写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 2.0);
	///   m.ScaleMatrixMod(0.5);
	///   double v = m[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多次调用是连乘效果（factor 相乘），不是各自独立；中途想撤回必须先 CopyMatrix 存快照。</para>
	/// </remarks>
	public void ScaleMatrixMod(double factor)
	{
		IntPtr proc = JlNativeApi.PreCall(853);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, factor);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>用元组携带的缩放系数缩放矩阵，返回新句柄；与标量重载同一个原生算子。</summary>
	/// <param name="factor">Scale factor. Default: 2.0</param>
	/// <returns>Matrix handle with the scaled elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 854，与 ScaleMatrix(double) 完全同一个算子，唯一差别是 factor 走 JlNativeApi.Store 把元组钉在原生输入 1 上，调用后再 UnpinTuple 解除固定（double 重载用 StoreD 直写，没有这层固定开销）。输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>传单元素元组与标量重载等价；传多条值时原生层是按元素对应缩放、广播还是只用第一条，代码层面看不出来 [待实测]，需要逐元素乘一个系数场请用 MultElementMatrix。</para>
	///   <para><b>与相邻算子的取舍</b>系数是编译期已知的常数时直接 `m * 2.0`（走 double 重载，省一次元组钉固定）；只有当系数来自其它算子的输出（本来就是 JlTuple）时才用本重载，避免先取 [0] 再转 double。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   using JlMatrix s = m.ScaleMatrix(new double[] { 3.0 });
	///   double v = s[1, 1];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄要 Dispose。隐式转换出来的临时元组由本方法内部 UnpinTuple 解除固定；若自己 new 了 JlTuple 传进来，用完自行 Dispose（纯数值元组只释放句柄类元素，不调也不会漏原生句柄）。</para>
	/// </remarks>
	public JlMatrix ScaleMatrix(JlTuple factor)
	{
		IntPtr proc = JlNativeApi.PreCall(854);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, factor);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(factor);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用一个标量缩放全矩阵元素，返回新句柄（this 不变）。</summary>
	/// <param name="factor">Scale factor. Default: 2.0</param>
	/// <returns>Matrix handle with the scaled elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 854。输入原生参数序 0=this、1=factor（本重载用 StoreD 直写一个 DOUBLE，不钉元组）；输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>只改数值不改维度，也不做逐元素按位置缩放：factor 是标量，元组重载传多条值时是否按元素对应缩放本仓库无法确认 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>托管侧的运算符 `m * k`、`k * m` 以及一元 `-m`（内部传 -1.0）全部转调本方法，写表达式更短；矩阵乘法 `a * b` 走的是 MultMatrix(860)，别把标量缩放当成乘矩阵。不想保留原矩阵就用 ScaleMatrixMod（id 853，原地）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 1.0);
	///   using JlMatrix s = m.ScaleMatrix(0.5);
	///   double v = s[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄要 Dispose。factor 给 0 得到全零矩阵但维度保留（想按新维度重建该用 CreateMatrix）。除以常数没有对应算子，也没有 operator /(matrix, double)：只能 ScaleMatrix(1.0 / k)。</para>
	/// </remarks>
	public JlMatrix ScaleMatrix(double factor)
	{
		IntPtr proc = JlNativeApi.PreCall(854);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, factor);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐元素相减的原地版本：this 被替换为 this - matrixBID。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 855（非原地版 SubMatrix 是 856）。输入原生参数序 0=this（被减数）、1=matrixBID（减数）；无 InitOCT/LoadNew，差直接写回 this，维度保持不变。</para>
	///   <para><b>约束或前提</b>不可交换：本方法只能算 A-B，想要 B-A 得换实例调用者。维度不一致时由原生层报错 [待实测]。this 原值调用后即丢失。</para>
	///   <para><b>与相邻算子的取舍</b>迭代残差（r 每轮减掉一个更新量）用它，省掉每轮一个待释放句柄；还要保留 A 用 SubMatrix 或 `a - b`。若目的是把两矩阵对应元素取大/取小，那是 MinMatrix/MaxMatrix（对单矩阵做归约）之外的另一套，本方法不适用。</para>
	///   <para><b>参数取向</b>void 返回；this 唯一被改写，matrixBID 只读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix r = new JlMatrix(3, 1, 9.0);
	///   JlMatrix step = new JlMatrix(3, 1, 1.0);
	///   r.SubMatrixMod(step);
	///   double v = r[0, 0];
	///   r.Dispose();
	///   step.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>matrixBID 在原生调用结束前不得释放（末尾 GC.KeepAlive），返回后可立即 Dispose。</para>
	/// </remarks>
	public void SubMatrixMod(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(855);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
	}

	/// <summary>逐元素相减：this 减 matrixBID，返回新句柄（顺序不可交换）。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <returns>Matrix handle with the difference of the input matrices.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 856。输入原生参数序 0=this（被减数 A）、1=matrixBID（减数 B）；输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>减法不可交换：本方法算的是 A-B，参数顺序写反就得到符号相反的结果，这类错误不会报错、只会静默反号。两矩阵维度需一致 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>`a - b` 运算符即转调本方法。要把结果直接落在 A 上（做残差迭代）用 SubMatrixMod（id 855）；只想取反符号不必调减法，`-m`（一元负号）内部是 ScaleMatrix(-1.0)，id 854，只跑一次标量乘。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 5.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   using JlMatrix d = a.SubMatrix(b);
	///   double v = d[0, 0];
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄要 Dispose；两个入参都不被消费。</para>
	/// </remarks>
	public JlMatrix SubMatrix(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(856);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
		return obj;
	}

	/// <summary>逐元素加法的原地版本：把 matrixBID 累加进 this。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 857（非原地版 AddMatrix 是 858）。输入原生参数序 0=this、1=matrixBID；无 InitOCT、无 LoadNew，结果由原生层写回 this 的句柄，维度不变。</para>
	///   <para><b>约束或前提</b>两矩阵维度需一致，否则由原生层报错 [待实测]。this 的旧值在调用后即刻被覆盖，无回退手段（要留底先 CopyMatrix）。</para>
	///   <para><b>与相邻算子的取舍</b>累加器模式（sum 从零矩阵开始不断加）用本方法最省句柄；还要保留 A 时改用 AddMatrix 或 `a + b`。想加一个常数没有对应算子，只能配一个恒值矩阵。</para>
	///   <para><b>参数取向</b>void 返回；this 是唯一被改写者，matrixBID 只读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix sum = new JlMatrix(2, 2, 0.0);
	///   JlMatrix term = new JlMatrix(2, 2, 3.0);
	///   sum.AddMatrixMod(term);
	///   sum.AddMatrixMod(term);
	///   double v = sum[0, 0];
	///   sum.Dispose();
	///   term.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>matrixBID 在原生调用结束前不得释放；调用返回后即可 Dispose 它。反复对同一 this 调用是安全的，不会累积多余句柄。</para>
	/// </remarks>
	public void AddMatrixMod(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(857);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
	}

	/// <summary>逐元素相加：this + matrixBID，结果是新句柄。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <returns>Matrix handle with the sum of the input matrices.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 858。输入原生参数序 0=this、1=matrixBID，两个都是句柄参数（第二个用 JlNativeApi.Store，第一个用实例内 Store）；没有字符串控制参数，输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>两矩阵维度需一致 [待实测]。本方法只能矩阵加矩阵：要给每个元素加常数，库里没有"加标量"的重载，只能先造一个同维度、值恒等的矩阵（CreateMatrix(rows, columns, k)）再加，或者用 SetFullMatrix/SetValueMatrix 直接改值。</para>
	///   <para><b>与相邻算子的取舍</b>要保留 A 用本方法（`a + b` 运算符即转调它）；只是把 b 累加进 a 用 AddMatrixMod（id 857）省一次分配。逐元素相减用 SubMatrix（856），点乘用 MultElementMatrix（852），别把 858 当成矩阵拼接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 2, 2.0);
	///   using JlMatrix s = a.AddMatrix(b);
	///   double v = s[0, 0];
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄用完 Dispose；两个入参都不被消费。加法可交换，但本方法固定把 this 当 A，报出的维度不匹配错误里矩阵顺序按 this/B 计。</para>
	/// </remarks>
	public JlMatrix AddMatrix(JlMatrix matrixBID)
	{
		IntPtr proc = JlNativeApi.PreCall(858);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
		return obj;
	}

	/// <summary>矩阵乘法的原地版本：把 this（作为 A）乘成 A·B（或按 multType 的转置组合）。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <param name="multType">Type of the input matrices. Default: "AB"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 859，与非原地版 MultMatrix（id 860）是两个不同算子。输入原生参数序 0=this、1=matrixBID、2=multType（StoreS）；方法里没有 InitOCT/LoadNew，说明结果由原生层直接写回 this 指向的句柄。</para>
	///   <para><b>约束或前提</b>this 既当输入又当输出，乘完维度变成 A行×B列 的组合结果 [待实测]，维度不匹配时由原生层报错 [待实测]；multType 可选字符串集合本仓库未声明 [待实测]。A 的原始内容无法回取，需要留底先 CopyMatrix。</para>
	///   <para><b>与相邻算子的取舍</b>循环里累乘（如反复施加同一变换矩阵）用它，避免每轮多一个待释放句柄；一次性求积且还要保留 A 时用 MultMatrix。逐元素自乘请用 MultElementMatrixMod，别把 859 当成点乘用。</para>
	///   <para><b>参数取向</b>void 返回；this 被原地改写，matrixBID 不被消费。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(3, 3, 1.0);
	///   JlMatrix b = new JlMatrix(3, 3, 2.0);
	///   a.MultMatrixMod(b, "AB");
	///   a.GetSizeMatrix(out int ar, out int ac);
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>matrixBID 在原生调用结束前不得释放（方法末尾 GC.KeepAlive 保证这一点），调用之后你可以立即 Dispose 它。</para>
	/// </remarks>
	public void MultMatrixMod(JlMatrix matrixBID, string multType)
	{
		IntPtr proc = JlNativeApi.PreCall(859);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.StoreS(proc, 2, multType);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
	}

	/// <summary>矩阵乘法（按 multType 决定用 A、B 还是其转置参与），返回新句柄。</summary>
	/// <param name="matrixBID">Matrix handle of the input matrix B.</param>
	/// <param name="multType">Type of the input matrices. Default: "AB"</param>
	/// <returns>Matrix handle of the multiplied matrices.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 860。输入原生参数序 0=this（当作 A）、1=matrixBID（B，句柄）、2=multType（StoreS 字符串）——注意乘积类型排在两个矩阵之后。输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>线性代数意义上的维度约束：按 "AB" 时要求 A 的列数等于 B 的行数 [待实测]；multType 换成带转置的取值时参与运算的是 Aᵀ/Bᵀ，具体可取字符串集合本仓库没有声明，以原生层为准 [待实测]。不满足维度关系时报错由原生层给出 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>逐元素点乘请用 MultElementMatrix（要求两矩阵同维度、结果同维度），两者极易混用：矩阵乘法结果维度是 A行×B列，与元素乘完全不同。运算符 matrix1 * matrix2 就是本方法传 "AB" 的包装；想 AᵀB 之类必须显式调本方法给 multType。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(3, 2, 1.0);
	///   JlMatrix b = new JlMatrix(2, 4, 2.0);
	///   using JlMatrix c = a.MultMatrix(b, "AB");
	///   c.GetSizeMatrix(out int cr, out int cc);
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄要 Dispose；matrixBID 不被消费，仍归调用方。连乘时每一步都产生一个新句柄，长链式调用会攒下多个待释放句柄；如不想保留 A，可用 MultMatrixMod 原地改写省一次分配。</para>
	/// </remarks>
	public JlMatrix MultMatrix(JlMatrix matrixBID, string multType)
	{
		IntPtr proc = JlNativeApi.PreCall(860);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixBID);
		JlNativeApi.StoreS(proc, 2, multType);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixBID);
		return obj;
	}

	/// <summary>一次调用同时取回行数与列数，两个输出都按 INTEGER 装载。</summary>
	/// <param name="rows">Number of rows of the matrix.</param>
	/// <param name="columns">Number of columns of the matrix.</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 861：输入只有 0=this；输出侧对槽位 0 和 1 各调一次 InitOCT，再用 JlNativeApi.LoadI 读回两个 INTEGER，即 rows、columns。两个 LoadI 共享同一个 err 串联传递，第一个失败时第二个仍会被调用，错误码向后传播并由 PostCall 抛出。</para>
	///   <para><b>约束或前提</b>元素总数 = rows × columns；一维向量在这里也是 2 个数（如 n×1），光看总数分不清行向量还是列向量，需要维度时务必用本方法或两个属性。</para>
	///   <para><b>与相邻算子的取舍</b>NumRows、NumColumns 属性内部各调一次本算子并丢掉另一个输出，同时要两者时用本方法省一半原生调用；只关心元素个数用 GetFullMatrix().Length 会把整表读回，代价高得多。</para>
	///   <para><b>参数取向</b>两个 out int，无返回值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 5, 0.0);
	///   m.GetSizeMatrix(out int rows, out int columns);
	///   int total = rows * columns;
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>*Mod 系列（如 TransposeMatrixMod、RepeatMatrix 的结果、CreateMatrix）会改变维度，之后再读维度即得到新值，托管侧不缓存。</para>
	/// </remarks>
	public void GetSizeMatrix(out int rows, out int columns)
	{
		IntPtr proc = JlNativeApi.PreCall(861);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out rows);
		err = JlNativeApi.LoadI(proc, 1, err, out columns);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把矩阵在行、列方向各平铺若干份，拼成一个更大的新矩阵。</summary>
	/// <param name="rows">Number of copies of input matrix in row direction. Default: 2</param>
	/// <param name="columns">Number of copies of input matrix in column direction. Default: 2</param>
	/// <returns>Matrix handle of the repeated copied matrix.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 862。输入原生参数序 0=this、1=rows、2=columns，两个参数都按 INTEGER 装载（StoreI）；输出 InitOCT(0)+LoadNew(0) 返回新句柄。</para>
	///   <para><b>约束或前提</b>参数含义是"份数"，不是目标尺寸：结果行数 = 原行数 × rows、列数 = 原列数 × columns [待实测]。传 1 表示该方向不铺；传 0 或负数由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想要一个大尺寸的常量矩阵，用 CreateMatrix/构造器一次给定 rows、columns 和初值更省；本方法用于把已有的一块图案（如单位阵、模板窗口）按网格重复。要转置或抽行用 TransposeMatrix/GetSubMatrix，别拿平铺凑。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 0.0);
	///   using JlMatrix tiled = m.RepeatMatrix(2, 3);
	///   tiled.GetSizeMatrix(out int r, out int c);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；this 不变。平铺结果不共享内存，改 tiled 不会影响 m [待实测]。大份数会让元素数按乘法增长，注意内存。</para>
	/// </remarks>
	public JlMatrix RepeatMatrix(int rows, int columns)
	{
		IntPtr proc = JlNativeApi.PreCall(862);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, rows);
		JlNativeApi.StoreI(proc, 2, columns);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>复制出一个内容与维度相同的新矩阵句柄。</summary>
	/// <returns>Matrix handle of the copied matrix.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 863：输入原生参数序只有 0=this，输出 InitOCT(0)+LoadNew(0) 返回一个独立的新句柄，this 自身不被改动。</para>
	///   <para><b>与相邻算子的取舍</b>Clone() 走的是另一条路：先 SerializeMatrix 成 byte[] 再 DeserializeMatrix（序列化通道，不是复制算子），多一次内存往返且受序列化格式约束，跨进程/存档才用它；同一进程内做备份请用本方法。想"原地改但不想丢原值"时，先 CopyMatrix 存快照再调 *Mod 系列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 3, 1.0);
	///   using JlMatrix snapshot = m.CopyMatrix();
	///   m.ScaleMatrixMod(2.0);
	///   double before = snapshot[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的新句柄用完必须 Dispose，否则原生内存要到终结器才回收。副本是否为深拷贝（改原矩阵不影响副本）从托管侧无法判断 [待实测]；维度信息随副本一起复制。</para>
	/// </remarks>
	public JlMatrix CopyMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(863);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用给定矩阵里的值覆写 this 的某一条对角线，原地改写。</summary>
	/// <param name="vectorID">Matrix handle containing the diagonal elements to be set.</param>
	/// <param name="diagonal">Position of the diagonal. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 864。输入原生参数序 0=this、1=vectorID、2=diagonal（StoreI，INTEGER）。两个矩阵都是句柄参数，没有 InitOCT：结果落在 this 上，非对角元素保持原值。</para>
	///   <para><b>约束或前提</b>真坑：vectorID 的类型是 JlMatrix，不是 JlTuple——想设一组对角值必须先造一个矩阵装值（如 new JlMatrix(n, 1, new double[]{...}) 或 GetDiagonalMatrix 拿回来的形状），不能直接传 double[]。向量长度与目标对角线可容纳的元素数不符时的行为由原生层决定 [待实测]。diagonal 的正负方向约定同 GetDiagonalMatrix [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要一次改整条对角线用本方法；改个别几项用 SetValueMatrix(JlTuple,JlTuple,JlTuple) 传坐标对更灵活（还能同时改非对角位置）。构造单位矩阵：先 CreateMatrix(n,n,0.0) 再对本方法传入全 1 向量，比手写 GetFullMatrix/SetValueMatrix 循环省一轮原生调用。</para>
	///   <para><b>参数取向</b>void 返回；this 被原地改写，vectorID 不被消费。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   JlMatrix ones = new JlMatrix(3, 1, 1.0);
	///   m.SetDiagonalMatrix(ones, 0);
	///   ones.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>vectorID 用完自行 Dispose；本方法不释放它，也不接管它。多次调用同一 diagonal 会互相覆盖，不会累加。</para>
	/// </remarks>
	public void SetDiagonalMatrix(JlMatrix vectorID, int diagonal)
	{
		IntPtr proc = JlNativeApi.PreCall(864);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, vectorID);
		JlNativeApi.StoreI(proc, 2, diagonal);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(vectorID);
	}

	/// <summary>抽出某一条对角线，返回一个新矩阵句柄（当向量用）。</summary>
	/// <param name="diagonal">Number of the desired diagonal. Default: 0</param>
	/// <returns>Matrix handle containing the diagonal elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 865。输入原生参数序 0=this、1=diagonal（StoreI，INTEGER）；输出 InitOCT(0)+LoadNew(0) 返回新 JlMatrix 句柄，元素仍按 double 存。</para>
	///   <para><b>约束或前提</b>diagonal 是带符号的对角线编号：0 为主对角线，正数往上、负数往下偏移 [待实测]；编号超出矩阵可容纳的范围时报错由原生层决定 [待实测]。返回矩阵的行列形状（一列多行还是多列一行）代码里看不出来，用前先 GetSizeMatrix 确认 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要主对角线上那几个数、又不想多一次原生调用时，直接按坐标 GetValueMatrix(JlTuple,JlTuple) 传成对的 (i,i) 更直观；本方法胜在一条调用拿到整条对角线并可直接当矩阵参与运算（配合 SetDiagonalMatrix 写回）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   using JlMatrix diag = m.GetDiagonalMatrix(0);
	///   m.GetSizeMatrix(out int dr, out int dc);
	///   diag.GetSizeMatrix(out int vr, out int vc);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，必须 Dispose（或用 using）。写回用 SetDiagonalMatrix(JlMatrix,int)，注意它要的是矩阵句柄而不是 JlTuple。</para>
	/// </remarks>
	public JlMatrix GetDiagonalMatrix(int diagonal)
	{
		IntPtr proc = JlNativeApi.PreCall(865);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, diagonal);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把一个子矩阵写进 this 的指定左上角位置，原地改写。</summary>
	/// <param name="matrixSubID">Matrix handle of the input sub-matrix.</param>
	/// <param name="row">Row coordinate of the upper left corner of the sub-matrix.</param>
	/// <param name="column">Column coordinate of the upper left corner of the sub-matrix.</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 866。输入原生参数序 0=this（母矩阵）、1=matrixSubID（句柄）、2=row、3=column，位置参数按 INTEGER 装载；无 InitOCT，即没有输出装载——结果直接落在 this 上。</para>
	///   <para><b>约束或前提</b>子矩阵的行列数不在参数里出现，靠尺寸自身决定落点范围：row+子矩阵行数超过母矩阵行数、或列方向越界时由原生层报错 [待实测]。母矩阵必须先存在（先 CreateMatrix 或构造器建好尺寸），本方法不会改变 this 的维度。</para>
	///   <para><b>与相邻算子的取舍</b>整块覆盖用 SetFullMatrix；只改零星几个元素用 SetValueMatrix 或索引器；嵌一块已有矩阵进更大矩阵的某个偏移用本方法最省事。要读回来用 GetSubMatrix。</para>
	///   <para><b>参数取向</b>void 返回；两个矩阵都是入参，this 是唯一被改写者。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix big = new JlMatrix(4, 4, 0.0);
	///   JlMatrix patch = new JlMatrix(2, 2, 9.0);
	///   big.SetSubMatrix(patch, 1, 1);
	///   double v = big[2, 2];
	///   patch.Dispose();
	///   big.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>matrixSubID 只是被读取，调用后仍归你所有，要自己 Dispose（方法末尾的 GC.KeepAlive 只保证原生调用期间不被回收）。写进去的是当时的值快照，之后改 patch 不会影响 big [待实测]。</para>
	/// </remarks>
	public void SetSubMatrix(JlMatrix matrixSubID, int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(866);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, matrixSubID);
		JlNativeApi.StoreI(proc, 2, row);
		JlNativeApi.StoreI(proc, 3, column);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(matrixSubID);
	}

	/// <summary>取子块：从 (row,column) 起截 rowsSub×columnsSub 的新矩阵句柄。</summary>
	/// <param name="row">Upper row position of the sub-matrix in the input matrix. Default: 0</param>
	/// <param name="column">Left column position of the sub-matrix in the input matrix. Default: 0</param>
	/// <param name="rowsSub">Number of rows of the sub-matrix. Default: 1</param>
	/// <param name="columnsSub">Number of columns of the sub-matrix. Default: 1</param>
	/// <returns>Matrix handle of the sub-matrix.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 867。输入原生参数序 0=this、1=row、2=column、3=rowsSub、4=columnsSub，四个位置参数全部按 INTEGER 装载（StoreI）；输出 InitOCT(0)+LoadNew(0) 返回一个新的 JlMatrix 句柄（不是视图）。</para>
	///   <para><b>约束或前提</b>原点是左上角、0 基；row+rowsSub 超过矩阵行数或 column+columnsSub 超过列数时越界，由原生层报错 [待实测]。rowsSub/columnsSub 给 0 时结果形状未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想读几个离散点用 GetValueMatrix(JlTuple,JlTuple)；想连续一块就用本方法。写回必须显式调 SetSubMatrix，本方法拿不到原矩阵的写权限。取整块等价于 CopyMatrix，不必用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(4, 4, 0.0);
	///   using JlMatrix sub = m.GetSubMatrix(1, 2, 2, 1);
	///   double corner = sub[0, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，用完 Dispose（示例用 using 接管）。子块是独立拷贝还是与原矩阵共享内存，从托管侧看不出来 [待实测]；要保证写回，稳妥做法是改完 sub 再 SetSubMatrix(sub, 1, 2)。</para>
	/// </remarks>
	public JlMatrix GetSubMatrix(int row, int column, int rowsSub, int columnsSub)
	{
		IntPtr proc = JlNativeApi.PreCall(867);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, row);
		JlNativeApi.StoreI(proc, 2, column);
		JlNativeApi.StoreI(proc, 3, rowsSub);
		JlNativeApi.StoreI(proc, 4, columnsSub);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out JlMatrix obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Set all values of a matrix.
	/// </summary>
	/// <param name="values">Values to be set.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 all 值 矩阵。</para>
	///   <para><b>典型场景</b></para>
	///   <para>矩阵运算与线性求解</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple values = ...;
	///   JlMatrix obj = ...;
	///   obj.SetFullMatrix(values);
	///   </code>
	/// </remarks>
	public void SetFullMatrix(JlTuple values)
	{
		IntPtr proc = JlNativeApi.PreCall(868);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, values);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(values);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set all values of a matrix.
	/// </summary>
	/// <param name="values">Values to be set.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 all 值 矩阵。</para>
	///   <para><b>典型场景</b></para>
	///   <para>矩阵运算与线性求解</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMatrix obj = ...;
	///   obj.SetFullMatrix(0.0);
	///   </code>
	/// </remarks>
	public void SetFullMatrix(double values)
	{
		IntPtr proc = JlNativeApi.PreCall(868);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, values);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>把整个矩阵展平成一条 DOUBLE 元组一次读回，长度等于行数乘列数。</summary>
	/// <returns>Values of the matrix elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 869：输入只有 0=this；输出 InitOCT(0) 后用 JlTuple.LoadNew 以 JlTupleType.DOUBLE 装载成一条新元组。元组是数值元组，不含句柄元素。</para>
	///   <para><b>约束或前提</b>元素总数 = GetSizeMatrix 的 rows × columns [待实测]；展平是行优先还是列优先，托管侧看不出来 [待实测]，跨行列定位前先小规模验证或用 GetValueMatrix 显式给坐标。</para>
	///   <para><b>与相邻算子的取舍</b>只想求和、求均值、求极值不必把整表搬回托管侧再 LINQ，用 SumMatrix/MeanMatrix/MaxMatrix/MinMatrix 让原生层归约；只要少数几个点用 GetValueMatrix，别为读 2 个数读回 10000 个。写回用 SetFullMatrix，或逐项 SetValueMatrix。</para>
	///   <para><b>参数取向</b>只有一个返回值元组，没有 out。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 0.0);
	///   using JlTuple all = m.GetFullMatrix();
	///   int n = all.Length;
	///   double first = all.DArr[0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>大矩阵一次读回会在托管侧生成等长 double[]，内存和拷贝成本都不低；反复调本方法比原生算子本身更贵。返回的元组实现了 IDisposable，纯数值元组不调用 Dispose 也不会漏原生句柄。</para>
	/// </remarks>
	public JlTuple GetFullMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(869);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按坐标表批量写值：三条元组分别给行号、列号、值，同一次原生调用改多个元素。</summary>
	/// <param name="row">Row numbers of the matrix elements to be modified. Default: 0</param>
	/// <param name="column">Column numbers of the matrix elements to be modified. Default: 0</param>
	/// <param name="value">Values to be set in the indicated matrix elements. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 SetValueMatrix(int,int,double) 同为原生算子 id 870，只是三个参数改用 JlNativeApi.Store 钉成元组：输入原生参数序 0=this、1=row、2=column、3=value，调用后按 row、column、value 的顺序依次 UnpinTuple。原地改写、无输出装载。</para>
	///   <para><b>约束或前提</b>row/column 是"成对"的坐标表，第 i 个写入落在 (row[i], column[i]) [待实测]；三条元组长度不一致时行为未定义 [待实测]。索引 0 基，任一坐标越界由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>改一个元素用标量重载或索引器更直白；改一片连续区域用 SetSubMatrix；全表覆盖用 SetFullMatrix。本重载的价值是把 N 次原生调用压成 1 次，坐标能从其它算子直接拿到时（例如某列的下标集合）尤其顺手。</para>
	///   <para><b>参数取向</b>void 返回，三个参数顺序与签名一致；没有 out。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   m.SetValueMatrix(new int[] { 0, 2 }, new int[] { 1, 0 }, new double[] { 5.0, -3.0 });
	///   double v = m[2, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>值一律按 DOUBLE 装载，整数数组经隐式转换进来的 int[] 也一样。传入的 JlTuple 由方法内部解除固定，但对象本身仍归你，需要时自行 Dispose。</para>
	/// </remarks>
	public void SetValueMatrix(JlTuple row, JlTuple column, JlTuple value)
	{
		IntPtr proc = JlNativeApi.PreCall(870);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, value);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(value);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>原地改单个元素：行、列按 INTEGER 传，值按 DOUBLE 传。</summary>
	/// <param name="row">Row numbers of the matrix elements to be modified. Default: 0</param>
	/// <param name="column">Column numbers of the matrix elements to be modified. Default: 0</param>
	/// <param name="value">Values to be set in the indicated matrix elements. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 870。输入原生参数序 0=this、1=row、2=column（两者 StoreI，INTEGER）、3=value（StoreD，DOUBLE）；没有 InitOCT，原地改写、无输出装载。</para>
	///   <para><b>约束或前提</b>索引 0 基，越界由原生层报错 [待实测]。维度不受影响：不能靠写越界来扩矩阵。</para>
	///   <para><b>与相邻算子的取舍</b>索引器 this[row, column] 的 set 就是转调本方法。一次改多个坐标必须用 SetValueMatrix(JlTuple,JlTuple,JlTuple)（同 id 870），别写循环：每循环一次都是一整次原生调用。整表覆盖用 SetFullMatrix，整块覆盖用 SetSubMatrix。</para>
	///   <para><b>参数取向</b>void 返回；三个标量参数顺序与签名一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   m.SetValueMatrix(2, 1, 4.5);
	///   double v = m[2, 1];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>value 是 double，整数也按 DOUBLE 装载，不会把矩阵变成整型矩阵。</para>
	/// </remarks>
	public void SetValueMatrix(int row, int column, double value)
	{
		IntPtr proc = JlNativeApi.PreCall(870);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, row);
		JlNativeApi.StoreI(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, value);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>按坐标成对取回多个元素值，输出按 DOUBLE 装载成一条新元组。</summary>
	/// <param name="row">Row numbers of matrix elements to be returned. Default: 0</param>
	/// <param name="column">Column numbers of matrix elements to be returned. Default: 0</param>
	/// <returns>Values of indicated matrix elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 871，与标量版 GetValueMatrix(int,int) 是同一算子。输入原生参数序 0=this、1=row 元组、2=column 元组（两条都用 Store 钉住，调用后依次 UnpinTuple）；输出 InitOCT(0) 后用 JlTuple.LoadNew 以 JlTupleType.DOUBLE 装载成一条新元组。</para>
	///   <para><b>约束或前提</b>row 与 column 是"成对"解释的坐标表（第 i 个结果对应 (row[i], column[i])）[待实测]，两条长度不等时的行为未定义 [待实测]。索引 0 基，任一坐标越界由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要一整块连续区域用 GetSubMatrix + GetFullMatrix；只要一个值用标量重载直接拿 double，不必再索引元组 [0]。批量取散点（对角线、抽样若干位置）时本重载一条调用胜过 N 次标量调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   using JlTuple vals = m.GetValueMatrix(new int[] { 0, 1, 2 }, new int[] { 0, 1, 2 });
	///   int n = vals.Length;
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新 JlTuple（实现了 IDisposable）；纯数值元组的 Dispose 只处理句柄类元素，不释放也不会漏原生句柄，但传进来的元组若是句柄类元素则会被固定到本调用结束。int[]/double[] 字面量靠隐式转换生成临时元组，无需处理。</para>
	/// </remarks>
	public JlTuple GetValueMatrix(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(871);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取单个元素的值：坐标按 INTEGER 传，结果按 DOUBLE 读回一个数。</summary>
	/// <param name="row">Row numbers of matrix elements to be returned. Default: 0</param>
	/// <param name="column">Column numbers of matrix elements to be returned. Default: 0</param>
	/// <returns>Values of indicated matrix elements.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 871，输入原生参数序 0=this 矩阵、1=row（StoreI）、2=column（StoreI）；输出 0 用 JlNativeApi.LoadD 装载，且只取第一个 DOUBLE —— 本重载坐标是标量，恰好对应一个值。</para>
	///   <para><b>约束或前提</b>行列号 0 基；越界坐标由原生层报错 [待实测]。矩阵类型不影响结果：所有元素对外都是 double。</para>
	///   <para><b>与相邻算子的取舍</b>索引器 this[row, column] 的 get 就是转调本方法，写 m[i, j] 与调本方法开销相同，挑顺手的即可。要一次取多个坐标必须用 GetValueMatrix(JlTuple,JlTuple)；只想要整表则用 GetFullMatrix() 一次读回，比循环调本方法少 N-1 次原生往返。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 0.0);
	///   double v = m.GetValueMatrix(1, 0);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>每次调用都是一次完整原生过程（含 PreCall/CallProcedure），嵌套在二重循环里遍历大矩阵会明显变慢。</para>
	/// </remarks>
	public double GetValueMatrix(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(871);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, row);
		JlNativeApi.StoreI(proc, 2, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>批量释放一组矩阵的内存：把整个句柄数组当成一条元组交给原生 clear_matrix。</summary>
	/// <param name="matrixID">Matrix handle.</param>
	/// <remarks>
	///   <para><b>功能说明</b>与实例版 ClearMatrix() 同为原生算子 id 872，只是输入改成一条句柄元组：先 JlHandleBase.ConcatArray(matrixID) 把 JlMatrix[] 转成 JlTuple，再 Store(proc, 0, hTuple)（钉住），调用后 UnpinTuple。注意句柄数组只占原生输入 0 这一个参数位，不是每个元素一个参数。</para>
	///   <para><b>约束或前提</b>数组元素必须是仍然有效的 JlMatrix 句柄；本方法不检查、也不会把数组里各对象的托管侧句柄置为 UNDEF。</para>
	///   <para><b>与相邻算子的取舍</b>手上是一堆临时矩阵时用它一次调用省掉 N 次原生往返；单个矩阵直接 m.Dispose() 更干净（Dispose 会同时清托管侧标记）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix a = new JlMatrix(2, 2, 0.0);
	///   JlMatrix b = new JlMatrix(3, 1, 1.0);
	///   JlMatrix.ClearMatrix(new JlMatrix[] { a, b });
	///   </code>
	///   <para><b>资源与坑</b>调用后 a、b 两个 C# 对象还在，但底层内存已回收，再用它们或再 Dispose() 会重复释放同一句柄 [待实测]。数组中出现 null 元素时 ConcatArray 的结果不可控 [待实测]。</para>
	/// </remarks>
	public static void ClearMatrix(JlMatrix[] matrixID)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(matrixID);
		IntPtr proc = JlNativeApi.PreCall(872);
		JlNativeApi.Store(proc, 0, hTuple);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(matrixID);
	}

	/// <summary>调原生 clear_matrix 释放本矩阵占用的内存，不返回任何东西。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 872，输入原生参数序只有一个 0=this 的句柄（Store 取 mHandle），InitOCT 未调用，即无输出装载。</para>
	///   <para><b>与相邻算子的取舍</b>托管侧的等价物是 Dispose()：JlHandleBase.Dispose 走 ClearHandle 并把 mHandle 置回 UNDEF 且 SuppressFinalize；本方法只让原生层回收内存，C# 对象里的句柄值不动。除非要把句柄交给外部（如 JlOperatorSet 或别的语言接口）管理，否则用 Dispose() 更安全。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(3, 3, 0.0);
	///   m.ClearMatrix();
	///   </code>
	///   <para><b>资源与坑</b>调用后 this 仍"看起来是个矩阵"：IsInitialized() 之外没有任何托管侧标记被清掉，紧接着再调任何算子会拿着已释放句柄进原生层，行为不可靠 [待实测]。也不要指望终结器救场——对象被 GC 回收时会再释放一次同一句柄 [待实测]。</para>
	/// </remarks>
	public void ClearMatrix()
	{
		IntPtr proc = JlNativeApi.PreCall(872);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>原地重建本实例的矩阵，并用一个元组逐元素给出初值。</summary>
	/// <param name="rows">Number of rows of the matrix. Default: 3</param>
	/// <param name="columns">Number of columns of the matrix. Default: 3</param>
	/// <param name="value">Values for initializing the elements of the matrix. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 CreateMatrix(int,int,double) 共用原生算子 id 873，输入原生参数序同样是 0=rows、1=columns、2=value；区别在 value 用 JlNativeApi.Store 钉住整条元组传入，调用后再 UnpinTuple 解除固定。输出 InitOCT(0)+Load(0) 绑回 this，原地改写、无返回值。</para>
	///   <para><b>约束或前提</b>进入方法即 Dispose() 释放旧句柄（否则 Load 会抛 JlException）。元组长度与 rows*columns 的关系决定结果形状是否合法：等长按元素铺满，长度不等时的行为由原生层判定 [待实测]；元素展平顺序（行优先还是列优先）代码层面看不出 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"全矩阵同一初值"用 double 重载即可，不必造一条长元组；已有矩阵改尺寸+改值用本方法，只改值用 SetFullMatrix(JlTuple)（后者不重建句柄、维度必须已经匹配）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(1, 1, 0.0);
	///   m.CreateMatrix(2, 2, new double[] { 1.0, 2.0, 3.0, 4.0 });
	///   double v = m[1, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>double[] 靠 JlTuple 的隐式转换生成临时元组，无需手动释放；若显式建 JlTuple 变量传进来，用完请自行 Dispose（数值元组的 Dispose 只处理句柄类元素，不调用也无原生泄漏风险）。传入空元组时行为未定义 [待实测]。</para>
	/// </remarks>
	public void CreateMatrix(int rows, int columns, JlTuple value)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(873);
		JlNativeApi.StoreI(proc, 0, rows);
		JlNativeApi.StoreI(proc, 1, columns);
		JlNativeApi.Store(proc, 2, value);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(value);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>原地重建本实例的矩阵：先释放旧句柄，再按 rows×columns 新建并装入初值。</summary>
	/// <param name="rows">Number of rows of the matrix. Default: 3</param>
	/// <param name="columns">Number of columns of the matrix. Default: 3</param>
	/// <param name="value">Values for initializing the elements of the matrix. Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 873，与构造器 JlMatrix(int,int,double) 是同一个算子。输入原生参数序 0=rows、1=columns（都按 INTEGER 装载）、2=value（本重载用 StoreD 直写一个 DOUBLE）；输出侧 InitOCT(0) + Load(0)，即把新建的句柄绑回 this 自身，原地改写、不返回新对象。</para>
	///   <para><b>约束或前提</b>方法体第一句就是 Dispose()：JlHandleBase.Load 在实例仍持有有效句柄时会抛 JlException（"Undisposed handle instance when loading output parameter"），所以本方法必须先自释放才能装载输出。副作用是旧矩阵数据在调用瞬间就没了。</para>
	///   <para><b>与相邻算子的取舍</b>尺寸不变、只想换元素值时用 SetFullMatrix 或索引器 this[row,column]，不必重建句柄；只有行列数也要变时才用本方法。一次性建矩阵直接 new JlMatrix(rows, columns, value) 更短，但它不含 Dispose，不能拿它复用已有实例。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMatrix m = new JlMatrix(2, 2, 0.0);
	///   m.CreateMatrix(3, 1, 7.0);
	///   double v = m[2, 0];
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本重载只交一个 double，按惯例广播到全部元素 [待实测]。调用后 this 指向全新矩阵，先前由它派生的独立句柄（CopyMatrix/GetSubMatrix 的返回）不受影响；若传入的 rows 或 columns 为 0 或负数，由原生层报错 [待实测]。</para>
	/// </remarks>
	public void CreateMatrix(int rows, int columns, double value)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(873);
		JlNativeApi.StoreI(proc, 0, rows);
		JlNativeApi.StoreI(proc, 1, columns);
		JlNativeApi.StoreD(proc, 2, value);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}
}
