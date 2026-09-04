using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an iconic object(-array). Base class for images, regions and XLDs</summary>
[Serializable]
public class JlObject : JlObjectBase, ISerializable, ICloneable
{
	/// <summary>按 HALCON 序号取出对象元组中的一个（或多个）元素，等价于 <see cref="SelectObj(JlTuple)"/>。</summary>
	/// <param name="index">要取出的对象序号，1-based（1 指向元组首个对象）。可传入 int、int[] 或 JlTuple（int 经隐式转换得到单元素元组）。越界序号由原生层报错。</param>
	/// <returns>由被选中对象组成的新 JlObject 句柄，与原对象独立，需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 select_obj（id 572）。把元组里位于 <paramref name="index"/> 处的对象抽出来，返回一个新的对象句柄。</para>
	///   <para><b>约束或前提</b>序号从 1 开始，不是 C# 的 0；传入 0 或超过 <see cref="CountObj()"/> 的值属非法请求。索引参数走原生 select_obj，因此它选的是"元组内位置"，与对象内容无关——上游 <c>Connection()</c> 等操作产生的顺序若不固定，这里按位置取会静默错取。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个元素时用 <see cref="SelectObj(int)"/>（标量重载，直接把 int 写进参数、无固定元组开销）；本索引器接收 JlTuple，适合一次取多下标。想"复制出去独立持有"用 <see cref="CopyObj"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10, 10, 40, 40);
	///   JlRegion r2 = new JlRegion(50, 50, 80, 80);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject first = pair[1];
	///   first.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，用完要 Dispose；它不共享被索引对象的引用计数，释放 <c>pair</c> 后 <c>first</c> 仍各自独立存在直到自己 Dispose。被索引对象必须已初始化，否则 <c>key</c> 为 UNDEF 时原生调用报错。</para>
	/// </remarks>
	public JlObject this[JlTuple index] => SelectObj(index);

	/// <summary>创建一个句柄为 UNDEF（未初始化）的空 JlObject，供后续原地装载输出使用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>转调基类 <c>JlObjectBase(UNDEF, copy: false)</c>，即内部 <c>key = IntPtr.Zero</c>，不申请任何原生对象。它本身不调用任何底层算子。</para>
	///   <para><b>约束或前提</b>未初始化的句柄不能直接当输入传给原生算子（<see cref="JlObjectBase.IsInitialized()"/> 返回 false）。它主要用于两类场景：一是像 <see cref="Deserialize(Stream)"/> 那样先建空壳再 <c>DeserializeObject</c> 填内容；二是 <see cref="GenEmptyObj()"/>、<see cref="ReadObject(string)"/>、<see cref="IntegerToObj(JlTuple)"/> 这类"原地装载输出"的方法，要求句柄当前必须是 UNDEF。</para>
	///   <para><b>与相邻算子的取舍</b>想要引用计数式浅拷贝用 <see cref="JlObject(JlObject)"/>；想要独立深拷贝用 <see cref="Clone()"/>；本构造只是拿到一个"待填充"的空对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObject obj = new JlObject();
	///   obj.GenEmptyObj();
	///   obj.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>基类 <see cref="IDisposable"/> 与终结器保证 <c>key != UNDEF</c> 时才 <c>ClearObject</c>，故对刚 new 出来、尚未装载的对象 Dispose 是安全的空操作。</para>
	/// </remarks>
	public JlObject()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>用给定的原生句柄 <paramref name="key"/> 构造 JlObject，默认以"引用计数拷贝"方式接管。</summary>
	/// <param name="key">一个已存在的原生对象句柄（HALCON handle）。null/IntPtr.Zero 视为未初始化。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <see cref="JlObject(IntPtr, bool)"/> 且 <c>copy = true</c>，基类会执行 <c>JlNativeApi.CopyObject(key)</c>，即让新对象与 <paramref name="key"/> 共享底层数据、各自持一份引用计数。</para>
	///   <para><b>约束或前提</b>这是内部/互操作入口（<c>[EditorBrowsable(Never)]</c>）。传入的 key 必须是本进程内有效的图标对象句柄，否则原生调用报错。</para>
	///   <para><b>与相邻算子的取舍</b>引用计数拷贝 ≠ 深拷贝：二者指向同一底层对象，改一处影响另一处的可见数据，除非某一方再次触发真正的拷贝语义。</para>
	///   <para><b>资源与坑</b><c>GC.KeepAlive(this)</c> 保证在原生句柄装载完成前本对象不被回收；调用方持有的原 key 与其自身释放时机互不影响。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>用给定句柄构造 JlObject，由 <paramref name="copy"/> 决定是"引用计数拷贝"还是"直接接管裸句柄"。</summary>
	/// <param name="key">原生对象句柄；<c>IntPtr.Zero</c>（UNDEF）表示未初始化，<c>IntPtr(1)</c>（UNDEF2）会被规整为 UNDEF。</param>
	/// <param name="copy">true：对 key 执行 <c>CopyObject</c>，得到共享底层数据、独立引用计数的新句柄；false：直接把 key 作为本对象的句柄接管（不增加引用）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>基类 <c>JlObjectBase(IntPtr, bool)</c> 的透传入口，是所有"接管/拷贝"路径的公共落点。</para>
	///   <para><b>约束或前提</b>仅在 <c>copy &amp;&amp; key != UNDEF &amp;&amp; key != UNDEF2</c> 时才真正 CopyObject；否则按原样存储（UNDEF2 归零为 UNDEF）。当 <c>copy = false</c> 接管裸句柄时，本对象的 Dispose 会负责清该句柄，调用方不得再重复释放。</para>
	///   <para><b>与相邻算子的取舍</b>从 C# 对象复制请用 <see cref="JlObject(JlObject)"/>；从序列化流恢复用 <see cref="Deserialize(Stream)"/>。</para>
	///   <para><b>资源与坑</b>内部/互操作用（<c>[EditorBrowsable(Never)]</c>）。接管语义下所有权转移到本对象，误用会导致双重释放或句柄泄漏。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从另一个 JlObject 复制出新对象：这是一次引用计数拷贝（clone），非深拷贝。</summary>
	/// <param name="obj">被复制的源 JlObject；其内部句柄会被 <c>CopyObject</c> 引用一次。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>JlObjectBase(JlObjectBase)</c>，即以 <c>copy = true</c> 对 <c>obj.key</c> 执行 <c>CopyObject</c>，得到共享底层数据、独立引用计数的新句柄。</para>
	///   <para><b>约束或前提</b>源对象 <paramref name="obj"/> 必须非 null 且已初始化；构造期间基类用 <c>GC.KeepAlive(obj)</c> 防止源句柄被提前回收。</para>
	///   <para><b>与相邻算子的取舍</b>需要彼此独立、改一个不影响另一个时改用 <see cref="Clone()"/>（序列化往返的深拷贝）；只需轻量共享引用时用本拷贝构造。</para>
	///   <para><b>资源与坑</b>两者各自 Dispose 时只减一次引用计数，底层对象在最后一个持有者释放后才真正回收。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
	}

	/// <summary>把原生过程的输出对象参数装载进一个全新的 JlObject（"返回新句柄"路径，对应 LoadNew 语义）。</summary>
	/// <param name="proc">当前原生过程句柄。</param>
	/// <param name="parIndex">输出对象在原生侧的参数序号。</param>
	/// <param name="err">调用返回码，透传给 <c>Load</c> 判断是否失败。</param>
	/// <param name="obj">输出参数：装载成功时返回一个持有新句柄的 JlObject。</param>
	/// <returns>原生装载的结果码（0 表示成功）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>new JlObject(UNDEF)</c> 建空壳，再调实例的 <c>Load</c> 从原生输出取回句柄——因此与"原地改写"系（<see cref="GenEmptyObj()"/> 等先 Dispose 再 Load 到 this）相对，本方法产出的对象所有权归调用方。</para>
	///   <para><b>约束或前提</b>基类 <c>Load</c> 要求目标句柄必须为 UNDEF，这里用刚 new 的空对象天然满足；若 <paramref name="err"/> 已是失败码则不覆盖句柄。</para>
	///   <para><b>资源与坑</b>调用方拿到的 <paramref name="obj"/> 是独立新句柄，用完须 <see cref="JlObjectBase.Dispose()"/>。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlObject obj)
	{
		obj = new JlObject(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeObject();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>二进制反序列化构造器：从 <paramref name="info"/> 里名为 "data" 的字节块重建出对象句柄。</summary>
	/// <param name="info">序列化载体，须包含由 <c>GetObjectData</c> 写入的 "data"（<c>byte[]</c>）。</param>
	/// <param name="context">流式上下文，本实现不使用。</param>
	/// <remarks>
	///   <para><b>功能说明</b>读出 "data" 字节块后调用 <see cref="DeserializeObject(byte[])"/>，走原生 <c>deserialize_obj</c>（id 1568）把字节流还原成一个新的对象句柄。</para>
	///   <para><b>约束或前提</b>字节块必须来自同一序列化族（<see cref="SerializeObject"/>/<see cref="Serialize(Stream)"/>），格式与版本需匹配，否则原生报错。</para>
	///   <para><b>资源与坑</b>这是 <see cref="ISerializable"/> 契约所需（供 BinaryFormatter 等使用），构造完本对象即持有还原后的句柄，需正常 Dispose。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(SerializationInfo info, StreamingContext context)
	{
		DeserializeObject((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把当前对象序列化为 Vision 二进制格式并写入 <paramref name="stream"/>，不改变本对象句柄。</summary>
	/// <param name="stream">可写的目标流。方法把 <see cref="SerializeObject"/> 得到的字节块写入其中，流的位置由 <c>JlSerializationBuffer</c> 管理。</param>
	/// <remarks>
	///   <para><b>功能说明</b>先调用 <see cref="SerializeObject"/>（原生 <c>serialize_obj</c>，id 1569）拿到内存序列块，再原样落到流上。对象本身仍归本实例持有，序列化不消费句柄。</para>
	///   <para><b>约束或前提</b>对象必须已初始化且非空内容才有意义；目标流必须支持写入。与 <see cref="WriteObject(string)"/> 的区别：后者按文件名落盘成独立文件，本方法只把字节写进任意 <see cref="Stream"/>（内存流、网络流、自定义容器）。</para>
	///   <para><b>与相邻算子的取舍</b>跨进程/网络传对象用本方法或 <see cref="SerializeObject"/>；落文件存档用 <see cref="WriteObject(string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       image.Serialize(ms);
	///       ms.Position = 0;
	///       JlObject back = JlObject.Deserialize(ms);
	///       back.Dispose();
	///   }
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>读回时 <see cref="Deserialize(Stream)"/> 会新建一个独立句柄，需单独 Dispose；<paramref name="stream"/> 的关闭由调用方负责。</para>
	/// </remarks>
	public void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeObject(), stream);
	}

	/// <summary>从 <paramref name="stream"/> 读取 Vision 二进制格式，还原出一个全新的 JlObject 句柄并返回。</summary>
	/// <param name="stream">可读的源流，其内容须由 <see cref="Serialize(Stream)"/> 写入（同族格式）。</param>
	/// <returns>持有反序列化后新句柄的 JlObject；调用方负责 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>内部 <c>new JlObject()</c> 建空壳，读出流字节后调 <see cref="DeserializeObject(byte[])"/>（原生 <c>deserialize_obj</c>，id 1568）把句柄装载进这个新对象——属于"返回新句柄"，不影响任何已有对象。</para>
	///   <para><b>约束或前提</b>返回类型为基类 JlObject，还原出的运行时类别取决于流中原始对象；如需强类型可再判 <see cref="GetObjClass"/> 或按已知类型包装。</para>
	///   <para><b>与相邻算子的取舍</b>从内存字节还原用 <see cref="DeserializeObject(byte[])"/>（原地）；从文件还原用 <see cref="ReadObject(string)"/>（原地改写 this）；本方法是"流 → 新对象"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       JlImage src = new JlImage("byte", 32, 32);
	///       src.Serialize(ms);
	///       ms.Position = 0;
	///       JlObject restored = JlObject.Deserialize(ms);
	///       restored.Dispose();
	///       src.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄独立于流；关闭流不会使返回对象失效，但两者都需各自释放。</para>
	/// </remarks>
	public static JlObject Deserialize(Stream stream)
	{
		JlObject hObject = new JlObject();
		hObject.DeserializeObject(JlSerializationBuffer.ReadFromStream(stream));
		return hObject;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>通过序列化往返生成一个与原对象完全独立的深拷贝。</summary>
	/// <returns>内容与本对象相同、但句柄独立的新 JlObject；调用方负责 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现 <see cref="ICloneable.Clone"/> 的公开版本：先 <see cref="SerializeObject"/> 取字节块，再 <c>new JlObject()</c> + <see cref="DeserializeObject(byte[])"/> 还原，等价于"序列化→反序列化"的深拷贝。</para>
	///   <para><b>约束或前提</b>原对象必须可序列化（已初始化）。深拷贝不共享底层内存，之后对副本的任何修改都不影响原对象。</para>
	///   <para><b>与相邻算子的取舍</b>只需共享底层、省内存省时间的浅拷贝用 <see cref="JlObject(JlObject)"/>（引用计数 clone）或 <see cref="CopyObj"/>；要真正独立、可各自改写时用本方法。深拷贝代价高于引用计数拷贝。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   JlObject copy = image.Clone();
	///   copy.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄独立于原对象，二者分别 Dispose；不要因"看起来是同一个"而漏放其一。</para>
	/// </remarks>
	public JlObject Clone()
	{
		byte[] data = SerializeObject();
		JlObject obj = new JlObject();
		obj.DeserializeObject(data);
		return obj;
	}

	/// <summary>对两个对象元组求差：返回属于本对象元组、但不属于 <paramref name="objectsSub"/> 的对象。</summary>
	/// <param name="objectsSub">被减对象元组（第二路输入）。</param>
	/// <returns>结果对象元组，是一个新句柄，需 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 obj_diff（id 558）。以对象句柄为单位做集合差：<c>this</c> 为被减集，<paramref name="objectsSub"/> 为减集，输出保留 <c>this</c> 中未被 <paramref name="objectsSub"/> 命中的元素及其原顺序。</para>
	///   <para><b>约束或前提</b>判等依据是句柄身份/相等性，与像素或几何内容是否"看起来相同"无关——两个内容相同但独立生成的对象不算同一元素。两路输入必须同族且已初始化。</para>
	///   <para><b>与相邻算子的取舍</b>求并集用 <see cref="ConcatObj"/>；按条件挑选用 <see cref="SelectObj(JlTuple)"/>；这里做的是"去掉另一元组里也有的那些对象"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion a = new JlRegion(10, 10, 40, 40);
	///   JlRegion b = new JlRegion(50, 50, 80, 80);
	///   JlRegion ab = a.ConcatObj(b);
	///   JlObject rest = ab.ObjDiff(a);
	///   rest.Dispose();
	///   ab.Dispose();
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；输入元组 <c>this</c>、<paramref name="objectsSub"/> 由各自的 <c>GC.KeepAlive</c> 保证在原生调用期间不被回收，调用方可在其后照常释放。</para>
	/// </remarks>
	public JlObject ObjDiff(JlObject objectsSub)
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

	/// <summary>把表示对象的"整数替身"（surrogate）转换回真实的图标对象，结果原地写入本实例。</summary>
	/// <param name="surrogateTuple">对象替身的整数元组（如 <see cref="ObjToInteger"/> 的返回值），每个整数代表一个句柄的整型编码。</param>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 integer_to_obj（id 566）。方法体先 <see cref="JlObjectBase.Dispose()"/> 释放本实例旧句柄，再把转换结果 <c>Load</c> 进 this（输出参数序 1）。</para>
	///   <para><b>约束或前提</b>基类 <c>Load</c> 要求目标句柄为 UNDEF，故本方法必须先 Dispose——意味着这是一次"替换"而非"追加"：调用前 this 持有的对象被丢弃。替身整数必须仍指向有效对象，否则原生调用报错。</para>
	///   <para><b>与相邻算子的取舍</b>本重载走 JlTuple 固定参数（Store + 调用后 <c>UnpinTuple</c>），适合一次传入多个替身；单个裸句柄用 <see cref="IntegerToObj(IntPtr)"/> 可省固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   JlTuple surrogate = image.ObjToInteger(1, -1);
	///   JlObject restored = new JlObject();
	///   restored.IntegerToObj(surrogate);
	///   surrogate.Dispose();
	///   restored.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写：调用后 this 拥有新句柄、旧句柄已释放，不要对同一实例再期望旧内容。本方法不负责释放 <paramref name="surrogateTuple"/> 指向的原对象，替身所有权仍归原持有者。</para>
	/// </remarks>
	public void IntegerToObj(JlTuple surrogateTuple)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(566);
		JlNativeApi.Store(proc, 0, surrogateTuple);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(surrogateTuple);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Convert an "integer number" into an iconic object.
	/// </summary>
	/// <param name="surrogateTuple">Tuple of object surrogates.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 "integer number" 为 图像对象。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   obj.IntegerToObj(0);
	///   </code>
	/// </remarks>
	public void IntegerToObj(IntPtr surrogateTuple)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(566);
		JlNativeApi.StoreIP(proc, 0, surrogateTuple);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Convert an iconic object into an "integer number."
	/// </summary>
	/// <param name="index">Starting index of the surrogates to be returned. Default: 1</param>
	/// <param name="number">Number of surrogates to be returned. Default: -1</param>
	/// <returns>Tuple containing the surrogates.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 图像对象 为 "integer number."。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.ObjToInteger(1, -1);
	///   </code>
	/// </remarks>
	public JlTuple ObjToInteger(int index, int number)
	{
		IntPtr proc = JlNativeApi.PreCall(567);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.StoreI(proc, 1, number);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Copy an iconic object in the Vision database.
	/// </summary>
	/// <param name="index">Starting index of the objects to be copied. Default: 1</param>
	/// <param name="numObj">Number of objects to be copied or -1. Default: 1</param>
	/// <returns>Copied objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Copy 图像对象 在 Vision database。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.CopyObj(1, 1);
	///   </code>
	/// </remarks>
	public JlObject CopyObj(int index, int numObj)
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
	///   <para><b>功能说明</b></para>
	///   <para>Concatenate two 图像对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject objects2 = ...;
	///   JlObject obj = ...;
	///   var result = obj.ConcatObj(objects2);
	///   </code>
	/// </remarks>
	public JlObject ConcatObj(JlObject objects2)
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
	///   <para><b>功能说明</b></para>
	///   <para>选择 objects 从 对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.SelectObj(1);
	///   </code>
	/// </remarks>
	public JlObject SelectObj(JlTuple index)
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
	///   <para><b>功能说明</b></para>
	///   <para>选择 objects 从 对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.SelectObj(1);
	///   </code>
	/// </remarks>
	public JlObject SelectObj(int index)
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
	///   <para><b>功能说明</b></para>
	///   <para>比较 图像对象s regarding equality。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject objects2 = ...;
	///   JlObject obj = ...;
	///   var result = obj.CompareObj(objects2, 0.0);
	///   </code>
	/// </remarks>
	public int CompareObj(JlObject objects2, JlTuple epsilon)
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
	///   <para><b>功能说明</b></para>
	///   <para>比较 图像对象s regarding equality。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject objects2 = ...;
	///   JlObject obj = ...;
	///   var result = obj.CompareObj(objects2, 0.0);
	///   </code>
	/// </remarks>
	public int CompareObj(JlObject objects2, double epsilon)
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
	///   <para><b>功能说明</b></para>
	///   <para>比较 图像 objects regarding equality。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject objects2 = ...;
	///   JlObject obj = ...;
	///   var result = obj.TestEqualObj(objects2);
	///   </code>
	/// </remarks>
	public int TestEqualObj(JlObject objects2)
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
	///   Number of objects in a tuple.
	/// </summary>
	/// <returns>Number of objects in the tuple Objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Number objects 在 元组。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.CountObj();
	///   </code>
	/// </remarks>
	public int CountObj()
	{
		IntPtr proc = JlNativeApi.PreCall(577);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Information about the components of an image object.
	/// </summary>
	/// <param name="request">Required information about object components. Default: "creator"</param>
	/// <param name="channel">Components to be examined (0 for region/XLD). Default: 0</param>
	/// <returns>Requested information.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Information about components 图像 对象。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.GetChannelInfo("creator", 0);
	///   </code>
	/// </remarks>
	public JlTuple GetChannelInfo(string request, JlTuple channel)
	{
		IntPtr proc = JlNativeApi.PreCall(578);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, request);
		JlNativeApi.Store(proc, 1, channel);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(channel);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Information about the components of an image object.
	/// </summary>
	/// <param name="request">Required information about object components. Default: "creator"</param>
	/// <param name="channel">Components to be examined (0 for region/XLD). Default: 0</param>
	/// <returns>Requested information.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Information about components 图像 对象。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.GetChannelInfo("creator", 0);
	///   </code>
	/// </remarks>
	public string GetChannelInfo(string request, int channel)
	{
		IntPtr proc = JlNativeApi.PreCall(578);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, request);
		JlNativeApi.StoreI(proc, 1, channel);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}

	/// <summary>
	///   Name of the class of an image object.
	/// </summary>
	/// <returns>Name of class.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Name class 图像 对象。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   var result = obj.GetObjClass();
	///   </code>
	/// </remarks>
	public JlTuple GetObjClass()
	{
		IntPtr proc = JlNativeApi.PreCall(579);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Create an empty object tuple.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 empty 对象 元组。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   obj.GenEmptyObj();
	///   </code>
	/// </remarks>
	public void GenEmptyObj()
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(602);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read an iconic object.
	/// </summary>
	/// <param name="fileName">Name of file.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取 图像对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>从文件加载图像、区域、模型或数据</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   obj.ReadObject("data.dat");
	///   </code>
	/// </remarks>
	public void ReadObject(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1566);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Write an iconic object.
	/// </summary>
	/// <param name="fileName">Name of file.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>写入 图像对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>将图像、区域、模型或数据保存到文件</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   obj.WriteObject("data.dat");
	///   </code>
	/// </remarks>
	public void WriteObject(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1567);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Deserialize a serialized iconic object.
	/// </summary>
	/// <param name="serializedItemHandle">Handle of the serialized item.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>反序列化 serialized 图像对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlObject obj = ...;
	///   obj.DeserializeObject(serializedItemHandle);
	///   </code>
	/// </remarks>
	public void DeserializeObject(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1568);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把当前对象整体序列化为 Vision 二进制字节块并返回，供内存传输或 <see cref="DeserializeObject(byte[])"/> 还原。</summary>
	/// <returns>序列化后的字节数组（纯托管数据，不涉及句柄释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 serialize_obj（id 1569）。本对象存入原生参数 1，输出按字节缓冲装载（<c>JlSerializationBuffer.LoadBytes</c>）。这是一次只读操作：不消费、不改变本对象句柄，调用后对象照常可用。</para>
	///   <para><b>约束或前提</b>对象必须已初始化（UNDEF 句柄调用由原生层报错）；对空元组调用的行为 [待实测]。多对象元组会被完整序列化，还原后元组长度不变。</para>
	///   <para><b>与相邻算子的取舍</b>要落到任意 <see cref="Stream"/>（网络、自定义容器）用 <see cref="Serialize(Stream)"/>（内部就是本方法加一次写流）；落盘成独立文件用 <see cref="WriteObject(string)"/>；<see cref="Clone"/> 则是序列化+反序列化的深拷贝组合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   byte[] data = image.SerializeObject();
	///   JlObject restored = new JlObject();
	///   restored.DeserializeObject(data);
	///   restored.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 byte[] 是拷贝，与原生缓冲无生命周期牵连，可随意保留、跨线程传递；还原端 <see cref="DeserializeObject(byte[])"/> 会先释放目标旧句柄，注意别把结果装进还想保留内容的对象。</para>
	/// </remarks>
	public byte[] SerializeObject()
	{
		IntPtr proc = JlNativeApi.PreCall(1569);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>把 <paramref name="objectsInsert"/> 整段插入本元组第 <paramref name="index"/> 个位置之前，返回加长后的新元组。</summary>
	/// <param name="objectsInsert">待插入的对象元组（可含多个对象，按原顺序整体嵌入）。</param>
	/// <param name="index">插入位置，1-based；合法范围 1..N+1（N 为本元组长度），取 N+1 即追加到尾部。</param>
	/// <returns>插入后的新对象元组（新句柄），需自行 <see cref="JlObjectBase.Dispose()"/>；原元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 insert_obj（id 2003）。原生参数序：索引在 0、输入元组在 1、插入元组在 2；索引经 <c>StoreI</c> 直写。输出 InitOCT 装载新句柄。</para>
	///   <para><b>约束或前提</b>索引 0 或大于 N+1 由原生层报错。对本元组为空元组（<see cref="CountObj()"/> 为 0）的情形，只能插到位置 1。</para>
	///   <para><b>与相邻算子的取舍</b>只在尾部追加用 <see cref="ConcatObj"/> 更直观；要覆盖已有位置用 <see cref="ReplaceObj(JlObject,int)"/>；本算子的价值是把新对象插进元组中部、维持既有序列语义（比如把补测结果插回原编号段）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10, 10, 40, 40);
	///   JlRegion r2 = new JlRegion(50, 50, 80, 80);
	///   JlRegion mid = new JlRegion(30, 30, 45, 45);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject grown = pair.InsertObj(mid, 2);
	///   grown.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   mid.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组中插入的元素与 <paramref name="objectsInsert"/> 引用计数共享，释放结果不影响元素本身。本对象与插入元组都由 <c>GC.KeepAlive</c> 保活到原生调用结束；插入后其后元素编号整体后移，下游按序号取数需按新序换算。</para>
	/// </remarks>
	public JlObject InsertObj(JlObject objectsInsert, int index)
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

	/// <summary>一次性删掉本元组中位于 <paramref name="index"/> 各位置（按原始元组计）的对象，返回剩余新元组。</summary>
	/// <param name="index">待删除位置的 1-based 索引元组，可含多个值；int/int[] 可隐式转换。</param>
	/// <returns>剩余对象的新元组（新句柄），相对顺序不变；需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 remove_obj（id 2005）。索引以固定元组 <c>Store</c> 进原生参数 0，调用后 <c>UnpinTuple</c>；本对象在参数 1；输出 InitOCT 装载为新句柄。</para>
	///   <para><b>约束或前提</b>元组内各索引都针对调用时的原元组解释（不是边删边重排），重复索引的处理 [待实测]；任一索引为 0 或越界由原生层报错。空索引元组等于整份浅拷贝。</para>
	///   <para><b>与相邻算子的取舍</b>配合 <c>Connection</c>+按面积/灰度筛选的典型流程：先用条件算子得到要剔除的序号元组，再一次性 RemoveObj，比循环调用 <see cref="RemoveObj(int)"/> 既少一次原生往返、又不会因元组缩短而错位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10, 10, 40, 40);
	///   JlRegion r2 = new JlRegion(50, 50, 80, 80);
	///   JlRegion r3 = new JlRegion(90, 90, 120, 120);
	///   JlObject triple = r1.ConcatObj(r2).ConcatObj(r3);
	///   JlTuple drop = new int[] { 1, 3 };
	///   JlObject rest = triple.RemoveObj(drop);
	///   drop.Dispose();
	///   rest.Dispose();
	///   triple.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   r3.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="index"/> 在原生调用返回前被钉住，调用完成前不要 Dispose 它；结果元组与新句柄独立，但元素与其它句柄共享引用计数。</para>
	/// </remarks>
	public JlObject RemoveObj(JlTuple index)
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

	/// <summary>从本对象元组中删掉第 <paramref name="index"/> 个对象，返回删除后的新元组。</summary>
	/// <param name="index">要删除的对象位置，1-based（1 指向元组首元素）。</param>
	/// <returns>剩余对象组成的新元组（新句柄），保持原有相对顺序；需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 remove_obj（id 2005）。本对象存入原生参数 1，索引经 <c>StoreI</c> 写入参数 0，输出用 InitOCT 装载为新句柄——原元组不被修改。</para>
	///   <para><b>约束或前提</b>索引 0 或超过 <see cref="CountObj()"/> 属非法请求，由原生层报错。删除后其余元素位置整体前移：连续删多个位置时用 <see cref="RemoveObj(JlTuple)"/> 一次完成，避免按"原始序号"逐个删时因元组缩短而错位。</para>
	///   <para><b>与相邻算子的取舍</b>只删一个位置用本重载（无钉固定元组开销）；删多个位置或位置来自上游算子输出时用 JlTuple 重载；按内容剔除用 <see cref="ObjDiff"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10, 10, 40, 40);
	///   JlRegion r2 = new JlRegion(50, 50, 80, 80);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject rest = pair.RemoveObj(1);
	///   rest.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；被"删除"只是从结果元组中移除引用，元素对象本身仍由原持有者引用计数管理。删除最后一个元素得到的是空元组而非 null，可继续 <see cref="CountObj()"/> 判 0。</para>
	/// </remarks>
	public JlObject RemoveObj(int index)
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

	/// <summary>按 <paramref name="index"/> 给出的一个或多个位置，用 <paramref name="objectsReplace"/> 的元素逐个覆盖本元组，返回新元组。</summary>
	/// <param name="objectsReplace">替换对象元组；其元素按顺序与 <paramref name="index"/> 的各位置配对。</param>
	/// <param name="index">被替换元素的 1-based 位置序列（单值或多值 JlTuple，int/int[] 可隐式转换）。</param>
	/// <returns>替换后的新对象元组（新句柄），需自行 <see cref="JlObjectBase.Dispose()"/>；原元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 replace_obj（id 2006）。原生参数序：索引在 0、输入元组在 1、替换元组在 2（与 C# 形参序相反）。索引以固定元组方式 <c>Store</c>，调用完成后 <c>UnpinTuple</c> 解除固定。</para>
	///   <para><b>约束或前提</b>所有索引位置必须存在于原元组内（1-based，0 或越界由原生层报错）。多位置替换时 <paramref name="index"/> 的元素数应与 <paramref name="objectsReplace"/> 的对象数一致；替换元组只有 1 个对象而索引多个时是否"广播"到全部位置 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只替换固定一个位置时用 <see cref="ReplaceObj(JlObject,int)"/>（StoreI 直写、无钉元组开销）；典型场景是 <c>Connection</c> 之后按上游序号替换掉误检的那一块——注意按位置替换依赖上游输出顺序稳定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10, 10, 40, 40);
	///   JlRegion r2 = new JlRegion(50, 50, 80, 80);
	///   JlRegion r3 = new JlRegion(90, 90, 120, 120);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject replaced = pair.ReplaceObj(r3, 1);
	///   replaced.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   r3.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；结果内未被打断位置的元素仍与原元组引用计数共享，释放结果不影响原对象。<paramref name="index"/> 在原生调用结束后才 Unpin，调用前不要 Dispose 它。</para>
	/// </remarks>
	public JlObject ReplaceObj(JlObject objectsReplace, JlTuple index)
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

	/// <summary>用 <paramref name="objectsReplace"/> 中的对象替换本元组自 <paramref name="index"/> 起的元素，返回替换后的新元组。</summary>
	/// <param name="objectsReplace">替换用对象元组；从 <paramref name="index"/> 起按顺序逐个覆盖原元组的对应位置。</param>
	/// <param name="index">起始替换位置，1-based（1 指向元组首元素）。本重载的索引必须是元组中真实存在的位置。</param>
	/// <returns>替换后的新对象元组（新句柄）；原元组本身不变，结果需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 replace_obj（id 2006）。本对象存入原生参数 1，<paramref name="objectsReplace"/> 存入参数 2，索引写入参数 0；输出经 InitOCT 装载为新句柄，属"返回新句柄"而非原地改写。</para>
	///   <para><b>约束或前提</b>替换区间 [index, index + len(objectsReplace)) 必须落在原元组长度内，越界由原生层报错；两方都必须已初始化。本标量重载用 <c>StoreI</c> 直写 int，无钉固定元组的开销。</para>
	///   <para><b>与相邻算子的取舍</b>要在多个离散位置各放一个对象时用 <see cref="ReplaceObj(JlObject,JlTuple)"/>（元组索引走 Store + 调用后 UnpinTuple）；只做插入不做覆盖用 <see cref="InsertObj"/>；只删不换用 <see cref="RemoveObj(int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion a = new JlRegion(10, 10, 40, 40);
	///   JlRegion b = new JlRegion(50, 50, 80, 80);
	///   JlRegion c = new JlRegion(90, 90, 120, 120);
	///   JlObject pair = a.ConcatObj(b);
	///   JlObject replaced = pair.ReplaceObj(c, 2);
	///   replaced.Dispose();
	///   pair.Dispose();
	///   a.Dispose();
	///   b.Dispose();
	///   c.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组里的元素与原元组/替换元组按引用计数共享底层数据：Dispose 结果句柄不会释放元素对象本身，三个输入句柄与结果要各自释放。本对象与 <paramref name="objectsReplace"/> 由 <c>GC.KeepAlive</c> 保证在原生调用结束前不被回收。</para>
	/// </remarks>
	public JlObject ReplaceObj(JlObject objectsReplace, int index)
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
