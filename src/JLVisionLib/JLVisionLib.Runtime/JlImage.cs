using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an image object(-array).</summary>
[Serializable]
public class JlImage : JlObject, ISerializable, ICloneable
{
	/// <summary>按 1 起始的索引取回对象数组中的单个图像元素。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>本索引器直接转调 <c>SelectObj(index)</c> 的元组重载（原生 id 572）：
	///   <c>index</c> 以 <see cref="JlTuple"/> 传入，内部先固定后解固定，取回的是<b>新句柄</b>而不是原对象的引用。</para>
	///   <para><b>索引从 1 起</b>对象数组沿用 1 基索引，<c>this[0]</c> 取不到首元素（结果大概率为空对象数组）[待实测]；
	///   要第一个元素写 <c>this[1]</c>，元素个数用 <c>CountObj()</c> 核对，别按 C# 数组的 0 基习惯用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   int n = img.CountObj();
	///   using JlImage first = img[1];                       // 单个元素，返回新句柄
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需自行释放；一次取多段请用 <c>SelectObj(JlTuple)</c> 传索引列表，
	///   只取一个单值时用 <c>SelectObj(int)</c> 可省掉元组固定开销。</para>
	/// </remarks>
	public new JlImage this[JlTuple index] => SelectObj(index);

	/// <summary>Create an uninitialized iconic object</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlImage：创建未初始化的图像对象</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlImage 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = new JlImage(false);
	///   </code>
	/// </remarks>
	public JlImage()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>由同族 <see cref="JlObject"/> 对象复制构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "image");
	}

	/// <summary>内部工厂：从算子的输出对象槽装载本类新实例；<paramref name="err"/> 为调用错误码并原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlImage obj)
	{
		obj = new JlImage(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	/// <summary>
	///   Create an image from a pointer to the pixels.
	/// </summary>
	/// <param name="type">Pixel type. Default: "byte"</param>
	/// <param name="width">Width of image. Default: 512</param>
	/// <param name="height">Height of image. Default: 512</param>
	/// <param name="pixelPointer">Pointer to first gray value.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlImage：由像素指针创建图像</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlImage 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = new JlImage("byte", 512, 512, 0);
	///   </code>
	/// </remarks>
	public JlImage(string type, int width, int height, IntPtr pixelPointer)
	{
		IntPtr proc = JlNativeApi.PreCall(591);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create an image with constant gray value.
	/// </summary>
	/// <param name="type">Pixel type. Default: "byte"</param>
	/// <param name="width">Width of image. Default: 512</param>
	/// <param name="height">Height of image. Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlImage：创建灰度为常数的图像</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlImage 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = new JlImage("byte", 512, 512);
	///   </code>
	/// </remarks>
	public JlImage(string type, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(592);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read an image with different file formats.
	/// </summary>
	/// <param name="fileName">Name of the image to be read. Default: "printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlImage：读取多种文件格式的图像</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlImage 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = new JlImage("printer_chip/printer_chip_01");
	///   </code>
	/// </remarks>
	public JlImage(JlTuple fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.Store(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fileName);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read an image with different file formats.
	/// </summary>
	/// <param name="fileName">Name of the image to be read. Default: "printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlImage：读取多种文件格式的图像</para>
	///   <para><b>典型场景</b></para>
	///   <para>创建 JlImage 对象</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = new JlImage("printer_chip/printer_chip_01");
	///   </code>
	/// </remarks>
	public JlImage(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeImage();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(SerializationInfo info, StreamingContext context)
	{
		DeserializeImage((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把图像对象序列化为二进制流。</summary>
	/// <remarks>
	///   <para><b>功能说明</b><c>Serialize(Stream)</c> 走托管层的 <c>SerializeImage()</c>：把当前对象的像素数据与对象数组
	///   结构打包成一段字节写进流。它序列化的是<b>数据副本</b>，与原生句柄无关，因此可用于落盘或跨进程传输，
	///   接收端反序列化得到的是新分配的句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using (var ms = new MemoryStream())
	///   {
	///       img.Serialize(ms);
	///       ms.Position = 0;
	///       using JlImage back = JlImage.Deserialize(ms);   // 新句柄，与 img 独立
	///   }
	///   </code>
	///   <para><b>资源与坑</b>与 <see cref="Deserialize(Stream)"/> 配对使用；反序列化得到的对象由调用方释放。
	///   单张图与对象数组都能序列化，但流里不带类型信息，读回需按同族接口。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeImage(), stream);
	}

	/// <summary>从二进制流反序列化出图像对象。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法，从 <see cref="Serialize(Stream)"/> 写出的流重建一个新 <c>JlImage</c>，
	///   经 <c>DeserializeImage</c> 在原生端重新分配像素内存。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using JLVisionLib;
	///
	///   using JlImage img = JlImage.Deserialize(File.OpenRead("c:\\tmp\\image.bin"));
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，用完释放；只喂本族 <c>Serialize</c> 写出的流，其它来源的字节无法保证能读回。</para>
	/// </remarks>
	public new static JlImage Deserialize(Stream stream)
	{
		JlImage hImage = new JlImage();
		hImage.DeserializeImage(JlSerializationBuffer.ReadFromStream(stream));
		return hImage;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b><c>Clone()</c> 通过 <c>SerializeImage()</c>+<c>DeserializeImage()</c> 做一次<b>深拷贝</b>：
	///   新对象的像素内存是重新分配的，改副本不影响原图。这与 <c>CopyImage()</c>（原生 id 571，同为深拷贝但走原生端）
	///   目的相同，区别只是本方法在托管层绕了序列化一圈。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage copy = img.Clone();                   // 独立副本，像素已复制
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；对象数组会连同结构一起复制，<c>CountObj()</c> 保持不变。
	///   只需一个引用别名、不打算改动时不要 <c>Clone()</c>，白白复制一份像素。</para>
	/// </remarks>
	public new JlImage Clone()
	{
		byte[] data = SerializeImage();
		JlImage obj = new JlImage();
		obj.DeserializeImage(data);
		return obj;
	}

	/// <summary>图像取反：-image 转调 InvertImage()。</summary>
	/// <remarks>
	///   <para><b>功能说明</b><c>-image</c> 返回 <c>image.InvertImage()</c> 的新句柄，输入不变。
	///   取反是按通道类型最大灰度做镜像：byte 上 <c>255-g</c>，<c>uint2</c> 上 <c>65535-g</c>；
	///   <c>real</c> 的镜像基准本层不确定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage inv = -img;                           // 等价于 img.InvertImage()
	///   </code>
	/// </remarks>
	public static JlImage operator -(JlImage image)
	{
		return image.InvertImage();
	}

	/// <summary>两图相加：image1 + image2 转调 AddImage(image2, 1.0, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素求和，返回新句柄。要求两图同尺寸、同通道数；类型不一致时的取舍由原生决定 [待实测]。</para>
	///   <para><b>截断坑</b>结果按操作数类型存储：<c>byte</c> 图上两值相加超过 255 会被截断而非进位，
	///   要保住量程先 <c>ConvertImageType("real")</c> 再相加，或事后 <c>ScaleImage</c> 归一化。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("real", 64, 64);
	///   JlImage b = new JlImage("real", 64, 64);
	///   using JlImage sum = a + b;                          // 等价于 a.AddImage(b, 1.0, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator +(JlImage image1, JlImage image2)
	{
		return image1.AddImage(image2, 1.0, 0.0);
	}

	/// <summary>两图相减：image1 - image2 转调 SubImage(image2, 1.0, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素求差，返回新句柄，常用于配准残差、背景差分。</para>
	///   <para><b>负值丢失</b><c>byte</c> 图差分会出现负值并被截成 0（或回绕），差分图因此看不到暗下去的一侧。
	///   做缺陷/运动差分前先 <c>ConvertImageType("real")</c>，或差完再 <c>+ 128</c> 抬偏移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage cur = new JlImage("real", 64, 64);
	///   JlImage bg = new JlImage("real", 64, 64);
	///   using JlImage diff = cur - bg;                      // 保留正负，可再取绝对值/阈值
	///   </code>
	/// </remarks>
	public static JlImage operator -(JlImage image1, JlImage image2)
	{
		return image1.SubImage(image2, 1.0, 0.0);
	}

	/// <summary>两图相乘：image1 * image2 转调 MultImage(image2, 1.0, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素乘积，返回新句柄。常见用法是拿一幅二值/掩膜图与彩色或灰度图相乘做区域屏蔽。</para>
	///   <para><b>截断坑</b>两个 0..255 的 <c>byte</c> 值相乘会迅速超过 255 并被截断，乘出来的图往往一片死白；
	///   做掩膜请用 0/1 取值的一路，或先 <c>ConvertImageType("real")</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlImage mask = new JlImage("real", 64, 64);
	///   using JlImage outImg = img * mask;                  // 等价于 img.MultImage(mask, 1.0, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator *(JlImage image1, JlImage image2)
	{
		return image1.MultImage(image2, 1.0, 0.0);
	}

	/// <summary>整幅加常数偏移：image + add 转调 ScaleImage(1.0, add)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素加 <paramref name="add"/>，返回新句柄。常用来给差分图/对数图抬偏移，把负值搬进正区间。</para>
	///   <para><b>截断坑</b>结果按原类型存储，<c>byte</c> 图加正数超过 255 会饱和或截断 [待实测：饱和还是截断]，
	///   需要可逆的量纲时先 <c>ConvertImageType("real")</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage diff = new JlImage("real", 64, 64);
	///   using JlImage lifted = diff + 128.0;                // 等价于 diff.ScaleImage(1.0, 128.0)
	///   </code>
	/// </remarks>
	public static JlImage operator +(JlImage image, double add)
	{
		return image.ScaleImage(1.0, add);
	}

	/// <summary>加常数偏移（常数在左）：add + image 与 image + add 等价。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>加法可交换，本重载同样转调 <c>image.ScaleImage(1.0, add)</c>，行为、截断坑与
	///   <c>image + add</c> 完全一致，只是写法语序。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage diff = new JlImage("real", 64, 64);
	///   using JlImage lifted = 128.0 + diff;               // 与 diff + 128.0 相同
	///   </code>
	/// </remarks>
	public static JlImage operator +(double add, JlImage image)
	{
		return image.ScaleImage(1.0, add);
	}

	/// <summary>整幅减常数偏移：image - sub 转调 ScaleImage(1.0, -sub)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素减 <paramref name="sub"/>，返回新句柄。没有 <c>sub - image</c> 的反向重载，
	///   需要"常数减去图像"请写 <c>(-image) + sub</c>。</para>
	///   <para><b>截断坑</b><c>byte</c> 图减出负值会被截到 0，做黑电平扣除前建议 <c>ConvertImageType("real")</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage darkened = img - 10.0;               // 等价于 img.ScaleImage(1.0, -10.0)
	///   </code>
	/// </remarks>
	public static JlImage operator -(JlImage image, double sub)
	{
		return image.ScaleImage(1.0, 0.0 - sub);
	}

	/// <summary>按系数缩放灰度：image * mult 转调 ScaleImage(mult, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素乘以 <paramref name="mult"/>，返回新句柄，用于增益/归一化。</para>
	///   <para><b>截断坑</b><c>byte</c> 上乘大于 1 的系数会超过 255 而饱和或截断 [待实测]，乘小于 1 会丢低位精度；
	///   要精确增益先转 <c>real</c> 再乘、再转回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage gain = img * 2.0;                    // 等价于 img.ScaleImage(2.0, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator *(JlImage image, double mult)
	{
		return image.ScaleImage(mult, 0.0);
	}

	/// <summary>按系数缩放灰度（常数在左）：mult * image 与 image * mult 等价。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>乘法可交换，同样转调 <c>image.ScaleImage(mult, 0.0)</c>，行为与 <c>image * mult</c> 一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage gain = 2.0 * img;                    // 与 img * 2.0 相同
	///   </code>
	/// </remarks>
	public static JlImage operator *(double mult, JlImage image)
	{
		return image.ScaleImage(mult, 0.0);
	}

	/// <summary>按除数缩放灰度：image / div 转调 ScaleImage(1.0/div, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素除以 <paramref name="div"/>，本层实现是乘 <c>1.0/div</c> 再交给 <c>ScaleImage</c>，
	///   返回新句柄。<paramref name="div"/> 为 0 时 <c>1.0/0</c> 得 Infinity，行为传下去由原生决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage dim = img / 2.0;                     // 等价于 img.ScaleImage(0.5, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator /(JlImage image, double div)
	{
		return image.ScaleImage(1.0 / div, 0.0);
	}

	/// <summary>逐像素动态分割：image1 &gt;= image2 转调 image1.DynThreshold(image2, 0.0, "light")。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>把 image2 当作逐像素阈值图，取 image1 中亮于（含等于）它的像素，偏移 0、取 light。
	///   这正是做局部阈值/背景差分的写法：image2 是低频照度图或参考帧。</para>
	///   <para><b>返回的是区域不是布尔</b>结果是 <see cref="JlRegion"/>，不是 <c>bool</c>，也不能与 <c>true/false</c> 比较；
	///   要"哪些像素满足"就接区域，要按像素计数用 <c>.Area()</c> 一类。两图须同尺寸，本层不校验 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage refImg = new JlImage("real", 64, 64);
	///   using JlRegion bright = img &gt;= refImg;            // 逐像素比参考帧，返回区域
	///   int n = bright.CountObj();
	///   </code>
	/// </remarks>
	public static JlRegion operator >=(JlImage image1, JlImage image2)
	{
		return image1.DynThreshold(image2, 0.0, "light");
	}

	/// <summary>逐像素动态分割（取暗区）：image1 &lt;= image2 转调 image1.DynThreshold(image2, 0.0, "dark")。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>image1 &gt;= image2</c> 同族、同为逐像素、同样返回 <see cref="JlRegion"/>，
	///   差别仅在取 dark：保留 image1 中暗于（含等于）阈值图 image2 的像素。用于暗点/阴影缺陷。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage refImg = new JlImage("real", 64, 64);
	///   using JlRegion dark = img &lt;= refImg;              // 逐像素暗于参考帧
	///   </code>
	/// </remarks>
	public static JlRegion operator <=(JlImage image1, JlImage image2)
	{
		return image1.DynThreshold(image2, 0.0, "dark");
	}

	/// <summary>常数阈值分割：image &gt;= threshold 转调 image.Threshold(threshold, double.MaxValue)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>取灰度不小于 <paramref name="threshold"/> 的像素，上界用 <c>double.MaxValue</c> 表示"到此为止全要"。
	///   返回 <see cref="JlRegion"/>，不是 <c>bool</c>——别把比较运算符当布尔表达式用。</para>
	///   <para><b>量纲</b><paramref name="threshold"/> 是 <c>double</c>，但比较对象是图像实际灰度：<c>byte</c> 0..255、
	///   <c>uint2</c> 0..65535。给一个超过本类型量程的阈值不会报错，只会得到空区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion bright = img &gt;= 128.0;            // 等价于 img.Threshold(128.0, double.MaxValue)
	///   int n = bright.Connection().CountObj();
	///   </code>
	/// </remarks>
	public static JlRegion operator >=(JlImage image, double threshold)
	{
		return image.Threshold(threshold, double.MaxValue);
	}

	/// <summary>常数阈值分割（取暗区）：image &lt;= threshold 转调 image.Threshold(double.MinValue, threshold)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>image &gt;= threshold</c> 同族，同样返回 <see cref="JlRegion"/>；这里取灰度不大于阈值的像素，
	///   下界用 <c>double.MinValue</c> 兜住。量纲与"不是布尔"的注意事项见 <c>image &gt;= threshold</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dark = img &lt;= 60.0;               // 等价于 img.Threshold(double.MinValue, 60.0)
	///   </code>
	/// </remarks>
	public static JlRegion operator <=(JlImage image, double threshold)
	{
		return image.Threshold(double.MinValue, threshold);
	}

	/// <summary>常数阈值分割（常数在左）：threshold &gt;= image 等价于 image &lt;= threshold。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>常数写在左边时语义会翻转："<c>threshold &gt;= image</c>"其实是"取 image 中不大于阈值的像素"，
	///   故转调 <c>image.Threshold(double.MinValue, threshold)</c>。同样返回 <see cref="JlRegion"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dark = 100.0 &gt;= img;               // 与 img &lt;= 100.0 相同
	///   </code>
	/// </remarks>
	public static JlRegion operator >=(double threshold, JlImage image)
	{
		return image.Threshold(double.MinValue, threshold);
	}

	/// <summary>Segment image using constant threshold</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment 图像 使用 常数 阈值分割。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion bright = 100.0 &lt;= img;             // 与 img &gt;= 100.0 相同
	///   </code>
	/// </remarks>
	public static JlRegion operator <=(double threshold, JlImage image)
	{
		return image.Threshold(threshold, double.MaxValue);
	}

	/// <summary>Reduces the domain of an image</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reduces domain 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(8, 8, 48, 48);
	///   using JlImage reduced = img &amp; roi;               // 按 ROI 缩小图像域
	///   </code>
	/// </remarks>
	public static JlImage operator &(JlImage image, JlRegion region)
	{
		return image.ReduceDomain(region);
	}

	/// <summary>Returns the domain of an image</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回 domain 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlData data = ...;
	///   JlTuple tuple = data;
	///   </code>
	/// </remarks>
	public static implicit operator JlRegion(JlImage image)
	{
		return image.GetDomain();
	}

	/// <summary>
	///   Image restoration by Wiener filtering.
	/// </summary>
	/// <param name="psf">impulse response (PSF) of degradation (in spatial domain).</param>
	/// <param name="noiseRegion">Region for noise estimation.</param>
	/// <param name="maskWidth">Width of filter mask. Default: 3</param>
	/// <param name="maskHeight">Height of filter mask. Default: 3</param>
	/// <returns>Restored image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 75，返回<b>新图像句柄</b>（<c>LoadNew</c>），输入图、psf、noiseRegion 都不被改写。
	///   维纳滤波需要一个退化模型的冲激响应 <paramref name="psf"/> 和一块用于估计噪声功率的 <paramref name="noiseRegion"/>：
	///   噪声是从这块区域里量出来的，所以要选在"确定是纯噪声/平坦背景"的地方，选在目标上会把目标纹理当噪声。</para>
	///   <para><b>与 WienerFilter 的取舍</b>本重载自带噪声估计，只要给噪声区；<see cref="WienerFilter(JlImage,JlImage)"/>
	///   要你自己先算一幅平滑图当噪声来源。已知噪声区在哪时用它更方便。</para>
	///   <para><b>约束</b><paramref name="psf"/> 尺寸、<paramref name="maskWidth"/>/<paramref name="maskHeight"/> 的合法范围
	///   本层不校验 [待实测]；PSF 与图像不匹配时结果无意义但不报错。多通道图的处理方式未在本层体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfMotion(64, 64, 20.0, 0, 3);                // 退化冲激响应
	///   using JlRegion noise = img.Threshold(0.0, 40.0);      // 估噪区（平坦背景）
	///   using JlImage restored = img.WienerFilterNi(psf, noise, 3, 3);
	///   </code>
	///   <para><b>资源与坑</b>结果是新句柄需释放；末尾对 <c>this</c>、psf、noiseRegion 都做 <c>GC.KeepAlive</c>，
	///   三者在原生调用期间都不能被回收。</para>
	/// </remarks>
	public JlImage WienerFilterNi(JlImage psf, JlRegion noiseRegion, int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(75);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, psf);
		JlNativeApi.Store(proc, 3, noiseRegion);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(psf);
		GC.KeepAlive(noiseRegion);
		return obj;
	}

	/// <summary>
	///   Image restoration by Wiener filtering.
	/// </summary>
	/// <param name="psf">impulse response (PSF) of degradation (in spatial domain).</param>
	/// <param name="filteredImage">Smoothed version of corrupted image.</param>
	/// <returns>Restored image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 76，同样返回新图像句柄。与 <see cref="WienerFilterNi(JlImage,JlRegion,int,int)"/>
	///   的区别在噪声来源：这里不传噪声区，而是传一幅<b>已平滑的退化图</b> <paramref name="filteredImage"/>，
	///   算子用"原图减平滑图"来估计噪声谱，因此 <paramref name="filteredImage"/> 必须是本图的低通版本，
	///   由 <c>MeanImage</c>/<c>GaussImage</c> 得到，且尺寸/通道与 <c>this</c> 一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfMotion(64, 64, 20.0, 0, 3);
	///   using JlImage smooth = img.MeanImage(5, 5);          // 平滑版=噪声参考
	///   using JlImage restored = img.WienerFilter(psf, smooth);
	///   </code>
	///   <para><b>资源与坑</b>psf 与 filteredImage 只是被读取、不转交所有权；三者末尾都 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlImage WienerFilter(JlImage psf, JlImage filteredImage)
	{
		IntPtr proc = JlNativeApi.PreCall(76);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, psf);
		JlNativeApi.Store(proc, 3, filteredImage);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(psf);
		GC.KeepAlive(filteredImage);
		return obj;
	}

	/// <summary>
	///   Generate an impulse response of a (linearly) motion blurring.
	/// </summary>
	/// <param name="PSFwidth">Width of impulse response image. Default: 256</param>
	/// <param name="PSFheight">Height of impulse response image. Default: 256</param>
	/// <param name="blurring">Degree of motion-blur. Default: 20.0</param>
	/// <param name="angle">Angle between direction of motion and x-axis (anticlockwise). Default: 0</param>
	/// <param name="type">PSF prototype resp. type of motion. Default: 3</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 77，是<b>原地生成器</b>：先 <c>Dispose()</c> 再 <c>Load</c> 把 PSF 写进当前对象，
	///   返回 <c>void</c>。所以要在一个可用的 <c>JlImage</c> 实例上调用（示例里对新建对象调用），不能拿返回值。</para>
	///   <para><b>参数</b><paramref name="blurring"/> 是运动拖尾的像素长度（double）；<paramref name="angle"/> 是运动方向
	///   与 x 轴逆时针夹角且本层是 <c>int</c>（<c>StoreI</c>），拿不到小数角度 [待实测]；<paramref name="type"/> 是运动原型编号
	///   （<c>int</c>），取值 0..N 各自对应哪种运动本层不体现 [待实测]。PSFwidth/PSFheight 决定 PSF 图像尺寸。</para>
	///   <para><b>与 SimulateMotion 的取舍</b>本算子只造冲激响应（给 <c>WienerFilter*</c> 当退化模型）；
	///   要"把一张清晰图做成模糊图"用 <see cref="SimulateMotion(double,int,int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfMotion(64, 64, 20.0, 0, 3);               // 结果写进 psf 本身
	///   </code>
	///   <para><b>资源与坑</b>会 Dispose 掉调用前对象持有的句柄再重建，别在还想要原内容时调用。</para>
	/// </remarks>
	public void GenPsfMotion(int PSFwidth, int PSFheight, double blurring, int angle, int type)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(77);
		JlNativeApi.StoreI(proc, 0, PSFwidth);
		JlNativeApi.StoreI(proc, 1, PSFheight);
		JlNativeApi.StoreD(proc, 2, blurring);
		JlNativeApi.StoreI(proc, 3, angle);
		JlNativeApi.StoreI(proc, 4, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Simulation of (linearly) motion blur.
	/// </summary>
	/// <param name="blurring">extent of blurring. Default: 20.0</param>
	/// <param name="angle">Angle between direction of motion and x-axis (anticlockwise). Default: 0</param>
	/// <param name="type">impulse response of motion blur. Default: 3</param>
	/// <returns>motion blurred image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 78，返回<b>新图像句柄</b>，输入不变。它在内部按 <paramref name="blurring"/>/
	///   <paramref name="angle"/>/<paramref name="type"/> 生成运动 PSF 并对输入做卷积，一步得到"被运动模糊后的图"，
	///   用于造测试样本或评估去模糊算法。</para>
	///   <para><b>与 GenPsfMotion 的取舍</b>只要模糊图 → 本方法；还想要那个 PSF 本身（喂给 <c>WienerFilterNi</c> 复原）
	///   → 用 <see cref="GenPsfMotion(int,int,double,int,int)"/>，两者的 blurring/angle/type 语义一致。</para>
	///   <para><b>参数</b><paramref name="blurring"/> 拖尾像素长度（double）；<paramref name="angle"/>/
	///   <paramref name="type"/> 与 GenPsfMotion 同为 <c>int</c>，含义/取值范围见该重载 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage blurred = img.SimulateMotion(20.0, 0, 3);
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage SimulateMotion(double blurring, int angle, int type)
	{
		IntPtr proc = JlNativeApi.PreCall(78);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, blurring);
		JlNativeApi.StoreI(proc, 1, angle);
		JlNativeApi.StoreI(proc, 2, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Generate an impulse response of an uniform out-of-focus blurring.
	/// </summary>
	/// <param name="PSFwidth">Width of result image. Default: 256</param>
	/// <param name="PSFheight">Height of result image. Default: 256</param>
	/// <param name="blurring">Degree of Blurring. Default: 5.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 79，<b>原地生成器</b>（先 <c>Dispose()</c> 再 <c>Load</c>，<c>void</c>）：把均匀离焦的
	///   冲激响应写进当前对象。<paramref name="blurring"/> 是离焦程度（默认 5.0），越大散焦圆盘越大。</para>
	///   <para><b>与 GenPsfMotion 的取舍</b>散焦（离焦）用本算子，运动拖尾用 <see cref="GenPsfMotion(int,int,double,int,int)"/>；
	///   两者产物都是喂给 <c>WienerFilter*</c> 的退化模型。要直接得到模糊图用 <see cref="SimulateDefocus(double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfDefocus(64, 64, 5.0);                    // 结果写进 psf 本身
	///   </code>
	///   <para><b>资源与坑</b>会先释放调用前对象的句柄再重建。</para>
	/// </remarks>
	public void GenPsfDefocus(int PSFwidth, int PSFheight, double blurring)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(79);
		JlNativeApi.StoreI(proc, 0, PSFwidth);
		JlNativeApi.StoreI(proc, 1, PSFheight);
		JlNativeApi.StoreD(proc, 2, blurring);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Simulate an uniform out-of-focus blurring of an image.
	/// </summary>
	/// <param name="blurring">Degree of blurring. Default: 5.0</param>
	/// <returns>Blurred image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 80，返回<b>新图像句柄</b>，输入不变：按 <paramref name="blurring"/> 对输入做均匀离焦模糊。
	///   <paramref name="blurring"/> 与 <see cref="GenPsfDefocus(int,int,double)"/> 的离焦程度同义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage soft = img.SimulateDefocus(5.0);
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；<c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage SimulateDefocus(double blurring)
	{
		IntPtr proc = JlNativeApi.PreCall(80);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, blurring);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}




























	/// <summary>
	///   Compute the essential matrix for a pair of stereo images by automatically finding correspondences between image points.
	/// </summary>
	/// <param name="image2">Input image 2.</param>
	/// <param name="rows1">Row coordinates of characteristic points in image 1.</param>
	/// <param name="cols1">Column coordinates of characteristic points in image 1.</param>
	/// <param name="rows2">Row coordinates of characteristic points in image 2.</param>
	/// <param name="cols2">Column coordinates of characteristic points in image 2.</param>
	/// <param name="camMat1">Camera matrix of the 1st camera.</param>
	/// <param name="camMat2">Camera matrix of the 2nd camera.</param>
	/// <param name="grayMatchMethod">Gray value comparison metric. Default: "ssd"</param>
	/// <param name="maskSize">Size of gray value masks. Default: 10</param>
	/// <param name="rowMove">Average row coordinate shift of corresponding points. Default: 0</param>
	/// <param name="colMove">Average column coordinate shift of corresponding points. Default: 0</param>
	/// <param name="rowTolerance">Half height of matching search window. Default: 200</param>
	/// <param name="colTolerance">Half width of matching search window. Default: 200</param>
	/// <param name="rotation">Estimate of the relative orientation of the right image with respect to the left image. Default: 0.0</param>
	/// <param name="matchThreshold">Threshold for gray value matching. Default: 10</param>
	/// <param name="estimationMethod">Algorithm for the computation of the essential matrix and for special camera orientations. Default: "normalized_dlt"</param>
	/// <param name="distanceThreshold">Maximal deviation of a point from its epipolar line. Default: 1</param>
	/// <param name="randSeed">Seed for the random number generator. Default: 0</param>
	/// <param name="covEMat">9x9 covariance matrix of the essential matrix.</param>
	/// <param name="error">Root-Mean-Square of the epipolar distance error.</param>
	/// <param name="points1">Indices of matched input points in image 1.</param>
	/// <param name="points2">Indices of matched input points in image 2.</param>
	/// <returns>Computed essential matrix.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 356，对立体图对求基础/本质矩阵：返回值以 <see cref="JlHomMat2D"/> 承载
	///   （<c>JlHomMat2D.LoadNew(proc,0,...)</c>），另有 4 个 <c>out</c> 元组：9×9 协方差 <paramref name="covEMat"/>、
	///   平均误差 <paramref name="error"/>、参与拟合的匹配点索引 <paramref name="points1"/>/<paramref name="points2"/>。</para>
	///   <para><b>它要特征点、不是全自动</b><paramref name="rows1"/>/<paramref name="cols1"/> 等是你先找好的角点/特征点坐标，
	///   本方法做的是"自动配对 + RANSAC 拟合"，不是"自动找点"。两图坐标系约定 (row,column) 且行在前。</para>
	///   <para><b>多值参数用元组</b>本重载 <paramref name="rotation"/>/<paramref name="matchThreshold"/>/
	///   <paramref name="distanceThreshold"/> 走 <c>Store</c>（可多值、逐通道），代价是每次固定/解固定；
	///   单值调参请见 <see cref="MatchEssentialMatrixRansac(JlImage,JlTuple,JlTuple,JlTuple,JlTuple,JlHomMat2D,JlHomMat2D,string,int,int,int,int,int,double,int,string,double,int,out JlTuple,out double,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>坑</b>相机矩阵 <paramref name="camMat1"/>/<paramref name="camMat2"/> 直接 <c>Store</c>，
	///   本层不校验其形状 [待实测]；RANSAC 有随机性，<paramref name="randSeed"/> 给 0 时结果逐次可能不同。
	///   <c>out</c> 实参必须写 <c>out</c>，不能预声明后按值传。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img1 = new JlImage("byte", 64, 64);
	///   using JlImage img2 = new JlImage("byte", 64, 64);
	///   JlTuple r1 = new JlTuple(10.0, 20.0), c1 = new JlTuple(12.0, 22.0);
	///   JlTuple r2 = new JlTuple(10.0, 20.0), c2 = new JlTuple(14.0, 24.0);
	///   JlHomMat2D cam1 = new JlHomMat2D(), cam2 = new JlHomMat2D();
	///   JlHomMat2D eMat = img1.MatchEssentialMatrixRansac(img2, r1, c1, r2, c2, cam1, cam2,
	///       "ssd", 10, 0, 0, 200, 200, new JlTuple(0.0), new JlTuple(10), "normalized_dlt",
	///       new JlTuple(1), 0, out JlTuple cov, out JlTuple err, out JlTuple p1, out JlTuple p2);
	///   </code>
	///   <para><b>资源与坑</b>返回矩阵与各 <c>out</c> 元组都是新对象，各自释放；<c>this</c> 与 <paramref name="image2"/> 都 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlHomMat2D MatchEssentialMatrixRansac(JlImage image2, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlHomMat2D camMat1, JlHomMat2D camMat2, string grayMatchMethod, int maskSize, int rowMove, int colMove, int rowTolerance, int colTolerance, JlTuple rotation, JlTuple matchThreshold, string estimationMethod, JlTuple distanceThreshold, int randSeed, out JlTuple covEMat, out JlTuple error, out JlTuple points1, out JlTuple points2)
	{
		IntPtr proc = JlNativeApi.PreCall(356);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
		JlNativeApi.Store(proc, 4, camMat1);
		JlNativeApi.Store(proc, 5, camMat2);
		JlNativeApi.StoreS(proc, 6, grayMatchMethod);
		JlNativeApi.StoreI(proc, 7, maskSize);
		JlNativeApi.StoreI(proc, 8, rowMove);
		JlNativeApi.StoreI(proc, 9, colMove);
		JlNativeApi.StoreI(proc, 10, rowTolerance);
		JlNativeApi.StoreI(proc, 11, colTolerance);
		JlNativeApi.Store(proc, 12, rotation);
		JlNativeApi.Store(proc, 13, matchThreshold);
		JlNativeApi.StoreS(proc, 14, estimationMethod);
		JlNativeApi.Store(proc, 15, distanceThreshold);
		JlNativeApi.StoreI(proc, 16, randSeed);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(camMat1);
		JlNativeApi.UnpinTuple(camMat2);
		JlNativeApi.UnpinTuple(rotation);
		JlNativeApi.UnpinTuple(matchThreshold);
		JlNativeApi.UnpinTuple(distanceThreshold);
		err = JlHomMat2D.LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out points1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out points2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Compute the essential matrix for a pair of stereo images by automatically finding correspondences between image points.
	/// </summary>
	/// <param name="image2">Input image 2.</param>
	/// <param name="rows1">Row coordinates of characteristic points in image 1.</param>
	/// <param name="cols1">Column coordinates of characteristic points in image 1.</param>
	/// <param name="rows2">Row coordinates of characteristic points in image 2.</param>
	/// <param name="cols2">Column coordinates of characteristic points in image 2.</param>
	/// <param name="camMat1">Camera matrix of the 1st camera.</param>
	/// <param name="camMat2">Camera matrix of the 2nd camera.</param>
	/// <param name="grayMatchMethod">Gray value comparison metric. Default: "ssd"</param>
	/// <param name="maskSize">Size of gray value masks. Default: 10</param>
	/// <param name="rowMove">Average row coordinate shift of corresponding points. Default: 0</param>
	/// <param name="colMove">Average column coordinate shift of corresponding points. Default: 0</param>
	/// <param name="rowTolerance">Half height of matching search window. Default: 200</param>
	/// <param name="colTolerance">Half width of matching search window. Default: 200</param>
	/// <param name="rotation">Estimate of the relative orientation of the right image with respect to the left image. Default: 0.0</param>
	/// <param name="matchThreshold">Threshold for gray value matching. Default: 10</param>
	/// <param name="estimationMethod">Algorithm for the computation of the essential matrix and for special camera orientations. Default: "normalized_dlt"</param>
	/// <param name="distanceThreshold">Maximal deviation of a point from its epipolar line. Default: 1</param>
	/// <param name="randSeed">Seed for the random number generator. Default: 0</param>
	/// <param name="covEMat">9x9 covariance matrix of the essential matrix.</param>
	/// <param name="error">Root-Mean-Square of the epipolar distance error.</param>
	/// <param name="points1">Indices of matched input points in image 1.</param>
	/// <param name="points2">Indices of matched input points in image 2.</param>
	/// <returns>Computed essential matrix.</returns>
	/// <remarks>
	///   <para>算法、特征点前提与随机性见 <see cref="MatchEssentialMatrixRansac(JlImage,JlTuple,JlTuple,JlTuple,JlTuple,JlHomMat2D,JlHomMat2D,string,int,int,int,int,int,JlTuple,JlTuple,string,JlTuple,int,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>：同一原生 id 356，区域/矩阵输出路径完全相同。</para>
	///   <para><b>实际差异</b><paramref name="rotation"/>/<paramref name="distanceThreshold"/> 经 <c>StoreD</c>、
	///   <paramref name="matchThreshold"/> 经 <c>StoreI</c> 直写单值，省掉固定/解固定；<paramref name="error"/> 用 <c>LoadD</c>
	///   读第一个值（本就是标量 RMS，无损失），而 <paramref name="covEMat"/> 仍是 <c>JlTuple</c>（9×9 协方差不被裁）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img1 = new JlImage("byte", 64, 64);
	///   using JlImage img2 = new JlImage("byte", 64, 64);
	///   JlTuple r1 = new JlTuple(10.0, 20.0), c1 = new JlTuple(12.0, 22.0);
	///   JlTuple r2 = new JlTuple(10.0, 20.0), c2 = new JlTuple(14.0, 24.0);
	///   JlHomMat2D cam1 = new JlHomMat2D(), cam2 = new JlHomMat2D();
	///   JlHomMat2D eMat = img1.MatchEssentialMatrixRansac(img2, r1, c1, r2, c2, cam1, cam2,
	///       "ssd", 10, 0, 0, 200, 200, 0.0, 10, "normalized_dlt", 1.0, 0,
	///       out JlTuple cov, out double err, out JlTuple p1, out JlTuple p2);
	///   </code>
	/// </remarks>
	public JlHomMat2D MatchEssentialMatrixRansac(JlImage image2, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlHomMat2D camMat1, JlHomMat2D camMat2, string grayMatchMethod, int maskSize, int rowMove, int colMove, int rowTolerance, int colTolerance, double rotation, int matchThreshold, string estimationMethod, double distanceThreshold, int randSeed, out JlTuple covEMat, out double error, out JlTuple points1, out JlTuple points2)
	{
		IntPtr proc = JlNativeApi.PreCall(356);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
		JlNativeApi.Store(proc, 4, camMat1);
		JlNativeApi.Store(proc, 5, camMat2);
		JlNativeApi.StoreS(proc, 6, grayMatchMethod);
		JlNativeApi.StoreI(proc, 7, maskSize);
		JlNativeApi.StoreI(proc, 8, rowMove);
		JlNativeApi.StoreI(proc, 9, colMove);
		JlNativeApi.StoreI(proc, 10, rowTolerance);
		JlNativeApi.StoreI(proc, 11, colTolerance);
		JlNativeApi.StoreD(proc, 12, rotation);
		JlNativeApi.StoreI(proc, 13, matchThreshold);
		JlNativeApi.StoreS(proc, 14, estimationMethod);
		JlNativeApi.StoreD(proc, 15, distanceThreshold);
		JlNativeApi.StoreI(proc, 16, randSeed);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(camMat1);
		JlNativeApi.UnpinTuple(camMat2);
		err = JlHomMat2D.LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlNativeApi.LoadD(proc, 2, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out points1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out points2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}














	/// <summary>
	///   Shade a height field.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 0.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 0.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <param name="shadows">Should shadows be calculated? Default: "false"</param>
	/// <returns>Shaded image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 387，输入被当作<b>高度场</b>（每像素是表面高度，通常 <c>real</c> 图），
	///   按给定的光源方向与反射参数合成一幅明暗图，返回<b>新图像句柄</b>，高度场本身不变。</para>
	///   <para><b>参数单位</b><paramref name="slant"/> 光源与 +z 轴夹角、<paramref name="tilt"/> 光源在 xy 投影与 x 轴夹角，均为<b>角度</b>（非弧度）；
	///   <paramref name="albedo"/> 反射率、<paramref name="ambient"/> 环境光。要正向渲染明暗图用它；
	///   反过来从明暗图估 slant/albedo 用 <c>EstimateSlAl*</c> 一族。</para>
	///   <para><b>多值参数</b>本重载四个参数走 <c>Store</c>（可逐区域/逐像素不同光照，代价是固定/解固定）；
	///   单一光照请见 <see cref="ShadeHeightField(double,double,double,double,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage hf = new JlImage("real", 64, 64);
	///   using JlImage shaded = hf.ShadeHeightField(new JlTuple(45.0), new JlTuple(45.0),
	///       new JlTuple(1.0), new JlTuple(0.0), "false");
	///   </code>
	///   <para><b>资源与坑</b><paramref name="shadows"/> 是字符串，取值不校验；结果新句柄需释放。</para>
	/// </remarks>
	public JlImage ShadeHeightField(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient, string shadows)
	{
		IntPtr proc = JlNativeApi.PreCall(387);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.StoreS(proc, 4, shadows);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Shade a height field.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 0.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 0.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <param name="shadows">Should shadows be calculated? Default: "false"</param>
	/// <returns>Shaded image.</returns>
	/// <remarks>
	///   <para>算法、参数单位与正向/逆向用途见 <see cref="ShadeHeightField(JlTuple,JlTuple,JlTuple,JlTuple,string)"/>：
	///   同一原生 id 387。本重载四个光照参数经 <c>StoreD</c> 直写单值，全图一套光照，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage hf = new JlImage("real", 64, 64);
	///   using JlImage shaded = hf.ShadeHeightField(45.0, 45.0, 1.0, 0.0, "false");
	///   </code>
	/// </remarks>
	public JlImage ShadeHeightField(double slant, double tilt, double albedo, double ambient, string shadows)
	{
		IntPtr proc = JlNativeApi.PreCall(387);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.StoreS(proc, 4, shadows);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Estimate the albedo of a surface and the amount of ambient light.
	/// </summary>
	/// <param name="ambient">Amount of ambient light.</param>
	/// <returns>Amount of light reflected by the surface.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 388，从明暗图反估反射率与环境光：返回值是 <b>albedo</b>（反射率）元组，
	///   <paramref name="ambient"/>（环境光）经 <c>out</c> 带回，两者都是 <c>JlTuple</c>（<c>LoadNew</c> 读，可含多值）。
	///   输入被当作带光照信息的图，本方法不产生图像输出。</para>
	///   <para><b>与相邻算子的取舍</b>估"倾斜角+反射率"用 <c>EstimateSlAlZc</c>/<c>EstimateSlAlLr</c>；
	///   估光源方位角用 <c>EstimateTilt*</c>；本方法专估 albedo 与 ambient 两个反射量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple albedo = img.EstimateAlAm(out JlTuple ambient);
	///   int n = albedo.Length;
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 <paramref name="ambient"/> 都是新对象需释放；<c>out</c> 必须写 <c>out</c>。</para>
	/// </remarks>
	public JlTuple EstimateAlAm(out JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(388);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out ambient);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Estimate the albedo of a surface and the amount of ambient light.
	/// </summary>
	/// <param name="ambient">Amount of ambient light.</param>
	/// <returns>Amount of light reflected by the surface.</returns>
	/// <remarks>
	///   <para>反估什么量、与 <c>EstimateSlAl*</c> 的取舍见 <see cref="EstimateAlAm(out JlTuple)"/>：同一原生 id 388。</para>
	///   <para><b>实际差异（重要）</b>本重载用 <c>LoadD</c> 读结果，<b>只取第一个值</b>：当算子对多区域/多通道给出成组 albedo、
	///   ambient 时，除首个外的值会被静默丢弃。要拿全量必须用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   double albedo = img.EstimateAlAm(out double ambient);   // 只有第一个值
	///   </code>
	/// </remarks>
	public double EstimateAlAm(out double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(388);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out ambient);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Estimate the slant of a light source and the albedo of a surface.
	/// </summary>
	/// <param name="albedo">Amount of light reflected by the surface.</param>
	/// <returns>Angle of the light sources and the positive z-axis (in degrees).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 389，反估光源倾斜角（slant，光源与 +z 轴夹角，单位角度）与表面反射率：
	///   返回 <b>slant</b> 元组，<paramref name="albedo"/> 经 <c>out</c> 带回，均为 <c>JlTuple</c>。是 <c>ShadeHeightField</c>
	///   的反问题之一（正向渲染 ↔ 逆向估计）。</para>
	///   <para><b>Zc 与 Lr 之别</b>本方法与 <see cref="EstimateSlAlLr(out JlTuple)"/> 是同题不同法（原生 id 389 vs 390），
	///   两者的算法差异与精度差异本层无从体现 [待实测]，一般按同一份数据分别试、看拟合误差再选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple slant = img.EstimateSlAlZc(out JlTuple albedo);
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 <paramref name="albedo"/> 需各自释放；<c>out</c> 必须写 <c>out</c>。</para>
	/// </remarks>
	public JlTuple EstimateSlAlZc(out JlTuple albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(389);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Estimate the slant of a light source and the albedo of a surface.
	/// </summary>
	/// <param name="albedo">Amount of light reflected by the surface.</param>
	/// <returns>Angle of the light sources and the positive z-axis (in degrees).</returns>
	/// <remarks>
	///   <para>反估的量、与 Lr 法的关系见 <see cref="EstimateSlAlZc(out JlTuple)"/>：同一原生 id 389。</para>
	///   <para><b>实际差异（重要）</b>用 <c>LoadD</c> 读 slant 与 <paramref name="albedo"/>，<b>各只取第一个值</b>，
	///   多区域成组结果会被裁掉；要全量请用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   double slant = img.EstimateSlAlZc(out double albedo);
	///   </code>
	/// </remarks>
	public double EstimateSlAlZc(out double albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(389);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Estimate the slant of a light source and the albedo of a surface.
	/// </summary>
	/// <param name="albedo">Amount of light reflected by the surface.</param>
	/// <returns>Angle between the light sources and the positive z-axis (in degrees).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Estimate the slant of a light source and the albedo of a surface。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EstimateSlAlLr(out JlTuple albedo);
	///   </code>
	/// </remarks>
	public JlTuple EstimateSlAlLr(out JlTuple albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(390);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Estimate the slant of a light source and the albedo of a surface.
	/// </summary>
	/// <param name="albedo">Amount of light reflected by the surface.</param>
	/// <returns>Angle between the light sources and the positive z-axis (in degrees).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Estimate the slant of a light source and the albedo of a surface。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EstimateSlAlLr(out double albedo);
	///   </code>
	/// </remarks>
	public double EstimateSlAlLr(out double albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(390);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Estimate the tilt of a light source.
	/// </summary>
	/// <returns>Angle between the light source and the x-axis after projection into the xy-plane (in degrees).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Estimate the tilt of a light source。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EstimateTiltZc();
	///   </code>
	/// </remarks>
	public JlTuple EstimateTiltZc()
	{
		IntPtr proc = JlNativeApi.PreCall(391);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Estimate the tilt of a light source.
	/// </summary>
	/// <returns>Angle between the light source and the x-axis after projection into the xy-plane (in degrees).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Estimate the tilt of a light source。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EstimateTiltLr();
	///   </code>
	/// </remarks>
	public JlTuple EstimateTiltLr()
	{
		IntPtr proc = JlNativeApi.PreCall(392);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Reconstruct a surface from surface gradients.
	/// </summary>
	/// <param name="reconstructionMethod">Type of the reconstruction method. Default: "poisson"</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>ReconstructHeight场FromGradient。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ReconstructHeightFieldFromGradient("poisson", new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlImage ReconstructHeightFieldFromGradient(string reconstructionMethod, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(393);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, reconstructionMethod);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   Reconstruct a surface from a gray value image.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 45.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 45.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reconstruct surface 从 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SfsPentland(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsPentland(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(395);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Reconstruct a surface from a gray value image.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 45.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 45.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reconstruct surface 从 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SfsPentland(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsPentland(double slant, double tilt, double albedo, double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(395);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Reconstruct a surface from a gray value image.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 45.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 45.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reconstruct surface 从 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SfsOrigLr(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsOrigLr(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(396);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Reconstruct a surface from a gray value image.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 45.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 45.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reconstruct surface 从 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SfsOrigLr(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsOrigLr(double slant, double tilt, double albedo, double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(396);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Reconstruct a surface from a gray value image.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 45.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 45.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reconstruct surface 从 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SfsModLr(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsModLr(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(397);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Reconstruct a surface from a gray value image.
	/// </summary>
	/// <param name="slant">Angle between the light source and the positive z-axis (in degrees). Default: 45.0</param>
	/// <param name="tilt">Angle between the light source and the x-axis after projection into the xy-plane (in degrees). Default: 45.0</param>
	/// <param name="albedo">Amount of light reflected by the surface. Default: 1.0</param>
	/// <param name="ambient">Amount of ambient light. Default: 0.0</param>
	/// <returns>Reconstructed height field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Reconstruct surface 从 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SfsModLr(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsModLr(double slant, double tilt, double albedo, double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(397);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}















	/// <summary>
	///   Segment an image using two-dimensional pixel classification.
	/// </summary>
	/// <param name="imageRow">Input image (second channel).</param>
	/// <param name="featureSpace">Region defining the feature space.</param>
	/// <returns>Classified regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment 图像 使用 two-dimensional 像素 classification。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageRow = ...;
	///   JlRegion featureSpace = ...;
	///   JlImage obj = ...;
	///   var result = obj.Class2dimSup(imageRow, featureSpace);
	///   </code>
	/// </remarks>
	public JlRegion Class2dimSup(JlImage imageRow, JlRegion featureSpace)
	{
		IntPtr proc = JlNativeApi.PreCall(431);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageRow);
		JlNativeApi.Store(proc, 3, featureSpace);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageRow);
		GC.KeepAlive(featureSpace);
		return obj;
	}

	/// <summary>
	///   Segment two images by clustering.
	/// </summary>
	/// <param name="image2">Second input image.</param>
	/// <param name="threshold">Threshold (maximum distance to the cluster's center). Default: 15</param>
	/// <param name="numClasses">Number of classes (cluster centers). Default: 5</param>
	/// <returns>Classification result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment two 图像 通过 clustering。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.Class2dimUnsup(image2, 15, 5);
	///   </code>
	/// </remarks>
	public JlRegion Class2dimUnsup(JlImage image2, int threshold, int numClasses)
	{
		IntPtr proc = JlNativeApi.PreCall(432);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreI(proc, 0, threshold);
		JlNativeApi.StoreI(proc, 1, numClasses);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Compare two images pixel by pixel.
	/// </summary>
	/// <param name="pattern">Comparison image.</param>
	/// <param name="mode">Mode: return similar or different pixels. Default: "diff_outside"</param>
	/// <param name="diffLowerBound">Lower bound of the tolerated gray value difference. Default: -5</param>
	/// <param name="diffUpperBound">Upper bound of the tolerated gray value difference. Default: 5</param>
	/// <param name="grayOffset">Offset gray value subtracted from the input image. Default: 0</param>
	/// <param name="addRow">Row coordinate by which the comparison image is translated. Default: 0</param>
	/// <param name="addCol">Column coordinate by which the comparison image is translated. Default: 0</param>
	/// <returns>Points in which the two images are similar/different.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>比较 two 图像 像素 通过 像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage pattern = ...;
	///   JlImage obj = ...;
	///   var result = obj.CheckDifference(pattern, "diff_outside", -5, 5, 0, 0, 0);
	///   </code>
	/// </remarks>
	public JlRegion CheckDifference(JlImage pattern, string mode, int diffLowerBound, int diffUpperBound, int grayOffset, int addRow, int addCol)
	{
		IntPtr proc = JlNativeApi.PreCall(433);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, pattern);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, diffLowerBound);
		JlNativeApi.StoreI(proc, 2, diffUpperBound);
		JlNativeApi.StoreI(proc, 3, grayOffset);
		JlNativeApi.StoreI(proc, 4, addRow);
		JlNativeApi.StoreI(proc, 5, addCol);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(pattern);
		return obj;
	}

	/// <summary>
	///   Perform a threshold segmentation for extracting characters.
	/// </summary>
	/// <param name="histoRegion">Region in which the histogram is computed.</param>
	/// <param name="sigma">Sigma for the Gaussian smoothing of the histogram. Default: 2.0</param>
	/// <param name="percent">Percentage for the gray value difference. Default: 95</param>
	/// <param name="threshold">Calculated threshold.</param>
	/// <returns>Dark regions (characters).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Char阈值。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion histoRegion = ...;
	///   JlImage obj = ...;
	///   var result = obj.CharThreshold(histoRegion, 2.0, 95, out JlTuple threshold);
	///   </code>
	/// </remarks>
	public JlRegion CharThreshold(JlRegion histoRegion, double sigma, JlTuple percent, out JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(434);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, histoRegion);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.Store(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percent);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out threshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(histoRegion);
		return obj;
	}

	/// <summary>
	///   Perform a threshold segmentation for extracting characters.
	/// </summary>
	/// <param name="histoRegion">Region in which the histogram is computed.</param>
	/// <param name="sigma">Sigma for the Gaussian smoothing of the histogram. Default: 2.0</param>
	/// <param name="percent">Percentage for the gray value difference. Default: 95</param>
	/// <param name="threshold">Calculated threshold.</param>
	/// <returns>Dark regions (characters).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Char阈值。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion histoRegion = ...;
	///   JlImage obj = ...;
	///   var result = obj.CharThreshold(histoRegion, 2.0, 95, out int threshold);
	///   </code>
	/// </remarks>
	public JlRegion CharThreshold(JlRegion histoRegion, double sigma, double percent, out int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(434);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, histoRegion);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadI(proc, 0, err, out threshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(histoRegion);
		return obj;
	}

	/// <summary>
	///   Extract regions with equal gray values from an image.
	/// </summary>
	/// <returns>Regions having a constant gray value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>提取 区域 使用 equal 灰度值s 从 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LabelToRegion();
	///   </code>
	/// </remarks>
	public JlRegion LabelToRegion()
	{
		IntPtr proc = JlNativeApi.PreCall(435);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Suppress non-maximum points on an edge.
	/// </summary>
	/// <param name="mode">Select horizontal/vertical or undirected NMS. Default: "hvnms"</param>
	/// <returns>Image with thinned edge regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Suppress non-maximum 点 在 边缘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.NonmaxSuppressionAmp("hvnms");
	///   </code>
	/// </remarks>
	public JlImage NonmaxSuppressionAmp(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(436);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Suppress non-maximum points on an edge using a direction image.
	/// </summary>
	/// <param name="imgDir">Direction image.</param>
	/// <param name="mode">Select non-maximum-suppression or interpolating NMS. Default: "nms"</param>
	/// <returns>Image with thinned edge regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Suppress non-maximum 点 在 边缘 使用 direction 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imgDir = ...;
	///   JlImage obj = ...;
	///   var result = obj.NonmaxSuppressionDir(imgDir, "nms");
	///   </code>
	/// </remarks>
	public JlImage NonmaxSuppressionDir(JlImage imgDir, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(437);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imgDir);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imgDir);
		return obj;
	}

	/// <summary>双阈值（滞后）分割：强像素作种子，弱像素只在能连通到种子时才保留。</summary>
	/// <param name="low">弱阈值下界。Default: 30</param>
	/// <param name="high">强阈值下界。Default: 60</param>
	/// <param name="maxLength">"潜在"点沿弱像素路径走到"安全"点所允许的最大步数。Default: 10</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 438，<c>InitOCT(proc,1)</c> 后经 <c>JlRegion.LoadNew</c> 取一个新区域。
	///   灰度 <c>&gt; high</c> 的像素是安全点，<c>low..high</c> 之间的潜在点只有能在 <paramref name="maxLength"/> 步内
	///   沿潜在点走到某个安全点时才进入结果，因此它不是"两个 <c>Threshold</c> 求并"：孤立出现的弱区域会被丢掉。</para>
	///   <para><b>什么时候该用它</b>边缘/缺陷对比度局部不足、用单一阈值要么断线要么吞噪声时。
	///   灰度整体可靠时不要用它——它比 <c>Threshold</c> 多了路径搜索，且 <paramref name="maxLength"/> 给小会随机截断弱结构，
	///   给大则把弱噪声一路串进来，这两个方向的误差都不体现在返回值上，只能靠面积统计发现。</para>
	///   <para><b>约束</b>本层不检查输入是否单通道 [待实测]，多通道图请先 <c>AccessChannel(1)</c> 取一个通道再分割。
	///   <paramref name="low"/> 与 <paramref name="high"/> 的相对大小未在本层校验，传反了不会在这里报错 [待实测]。
	///   <paramref name="maxLength"/> 是 <c>int</c>（<c>StoreI</c>），只能整数步数；0 与负值的语义本层未体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion weak = img.HysteresisThreshold(new JlTuple(20.0), new JlTuple(60.0), 30);
	///   int n = weak.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>元组重载对 <paramref name="low"/>/<paramref name="high"/> 做固定与 <c>UnpinTuple</c>；
	///   返回句柄由调用者释放。末尾 <c>GC.KeepAlive(this)</c> 只保输入，不保输出。</para>
	/// </remarks>
	public JlRegion HysteresisThreshold(JlTuple low, JlTuple high, int maxLength)
	{
		IntPtr proc = JlNativeApi.PreCall(438);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, low);
		JlNativeApi.Store(proc, 1, high);
		JlNativeApi.StoreI(proc, 2, maxLength);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>双阈值分割（整数阈值版）。</summary>
	/// <param name="low">弱阈值下界。Default: 30</param>
	/// <param name="high">强阈值下界。Default: 60</param>
	/// <param name="maxLength">潜在点走到安全点的最大步数。Default: 10</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法与取舍见 <see cref="HysteresisThreshold(JlTuple,JlTuple,int)"/>：同一原生 id 438。</para>
	///   <para><b>实际差异</b><paramref name="low"/>/<paramref name="high"/> 以 <c>StoreI</c> 作整数传，
	///   灰度阈值不能带小数；元组版走 <c>Store</c> 并额外做固定/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.HysteresisThreshold(30, 60, 10);
	///   </code>
	/// </remarks>
	public JlRegion HysteresisThreshold(int low, int high, int maxLength)
	{
		IntPtr proc = JlNativeApi.PreCall(438);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, low);
		JlNativeApi.StoreI(proc, 1, high);
		JlNativeApi.StoreI(proc, 2, maxLength);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>自动求一个二值化阈值并分割，同时把实际用到的阈值回传。</summary>
	/// <param name="method">判据方法。Default: "max_separability"</param>
	/// <param name="lightDark">取前景还是背景。Default: "dark"</param>
	/// <param name="usedThreshold">回传：本帧实际算出的阈值。</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 439。两个输出：<c>JlRegion.LoadNew(proc,1,...)</c> 取区域，
	///   <c>JlTuple.LoadNew(proc,0,...)</c> 取阈值——阈值是<b>控制参数输出</b>（<c>InitOCT(proc,0)</c>），
	///   不是图标对象，所以它可以逐帧变化并被记录下来。</para>
	///   <para><b>为什么先想到它而不是 <c>Threshold</c></b>光源或工件反射率批次漂移时，写死阈值会缓慢失配；
	///   本算子按直方图判据自动定阈值，并给出该帧阈值，便于写日志、做统计门限（阈值突变通常意味着来料或光照变了）。
	///   反过来：需要跨帧严格可比的量纲（"灰度 &gt; 180 才算合格"）时<b>不要</b>用它，每帧重算的阈值会让判定标准本身漂移。</para>
	///   <para><b>与 <c>AutoThreshold</c> 的取舍</b><c>AutoThreshold</c> 只需一个直方图平滑量 <c>sigma</c>，不回报阈值；
	///   本算子按 <paramref name="method"/> 选判据并回报阈值。要看阈值、要在多判据之间比较时用它。</para>
	///   <para><b>约束</b><paramref name="method"/> 与 <paramref name="lightDark"/> 都是字符串（<c>StoreS</c>），
	///   本层不校验取值，写错只能等原生端在 <c>PostCall</c> 抛 <c>JlOperatorException</c>；
	///   <paramref name="lightDark"/> 的两个取值给出互补的两块区域，边界像素归哪一侧未在本层体现 [待实测]。
	///   <paramref name="usedThreshold"/> 的元素个数由判据决定（多类判据可能不止一个）[待实测]，
	///   用 <c>Length</c> 判断后再按下标取值。多通道输入的通道数不检查 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion fg = img.BinaryThreshold("max_separability", "dark", out JlTuple used);
	///   double t = used[0].D;                                    // 本帧阈值
	///   using JlRegion same = img.Threshold(new JlTuple(0.0), new JlTuple(t));
	///   fg.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>区域与新元组都是新对象，需各自释放；<c>out</c> 实参必须写 <c>out</c>，
	///   不能预先声明后按值传（CS1615 方向不匹配）。</para>
	/// </remarks>
	public JlRegion BinaryThreshold(string method, string lightDark, out JlTuple usedThreshold)
	{
		IntPtr proc = JlNativeApi.PreCall(439);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out usedThreshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>自动二值化并分割（阈值以整数回传）。</summary>
	/// <param name="method">判据方法。Default: "max_separability"</param>
	/// <param name="lightDark">取前景还是背景。Default: "dark"</param>
	/// <param name="usedThreshold">回传：本帧实际用到的整数阈值。</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法、判据选择与 <paramref name="lightDark"/> 的取舍见 <see cref="BinaryThreshold(string,string,out JlTuple)"/>：
	///   同一原生 id 439，区域输出路径完全相同。</para>
	///   <para><b>实际差异</b>阈值改用 <c>JlNativeApi.LoadI</c> 读取，因此只适合 8 位/整型灰度阈值。
	///   <c>float</c> 图或判据给出非整数阈值时会被截断成 <c>int</c> [待实测：是否改为报错]，
	///   这类图像请用元组版按 <c>double</c> 取值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion fg = img.BinaryThreshold("max_separability", "light", out int used);
	///   bool plausible = used &gt;= 0 &amp;&amp; used &lt;= 255;
	///   fg.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion BinaryThreshold(string method, string lightDark, out int usedThreshold)
	{
		IntPtr proc = JlNativeApi.PreCall(439);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadI(proc, 0, err, out usedThreshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Segment an image using local thresholding.
	/// </summary>
	/// <param name="method">Segmentation method. Default: "adapted_std_deviation"</param>
	/// <param name="lightDark">Extract foreground or background? Default: "dark"</param>
	/// <param name="genParamName">List of generic parameter names. Default: []</param>
	/// <param name="genParamValue">List of generic parameter values. Default: []</param>
	/// <returns>Segmented output region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment 图像 使用 local thresholding。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LocalThreshold("adapted_std_deviation", "dark", new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlRegion LocalThreshold(string method, string lightDark, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(440);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Segment an image using local thresholding.
	/// </summary>
	/// <param name="method">Segmentation method. Default: "adapted_std_deviation"</param>
	/// <param name="lightDark">Extract foreground or background? Default: "dark"</param>
	/// <param name="genParamName">List of generic parameter names. Default: []</param>
	/// <param name="genParamValue">List of generic parameter values. Default: []</param>
	/// <returns>Segmented output region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment 图像 使用 local thresholding。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LocalThreshold("adapted_std_deviation", "dark", new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlRegion LocalThreshold(string method, string lightDark, string genParamName, int genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(440);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.StoreS(proc, 2, genParamName);
		JlNativeApi.StoreI(proc, 3, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按局部均值与局部标准差自适应阈值分割（元组版）。</summary>
	/// <param name="maskWidth">计算局部均值/标准差的窗口宽。Default: 15</param>
	/// <param name="maskHeight">窗口高。Default: 15</param>
	/// <param name="stdDevScale">局部标准差的加权系数。Default: 0.2</param>
	/// <param name="absThreshold">与局部均值的最小灰度差。Default: 2</param>
	/// <param name="lightDark">取亮区还是暗区。Default: "dark"</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 441，一个区域输出（<c>InitOCT(proc,1)</c> + <c>JlRegion.LoadNew</c>）。
	///   每像素的判据来自其 <paramref name="maskWidth"/>×<paramref name="maskHeight"/> 邻域：局部阈值由均值与
	///   <paramref name="stdDevScale"/>×局部标准差构成，并要求像素与均值的差超过 <paramref name="absThreshold"/>
	///   才算目标——两个条件谁起决定作用，取决于平坦区（标准差小，<paramref name="absThreshold"/> 说话）
	///   还是纹理区（标准差大，<paramref name="stdDevScale"/> 说话）。</para>
	///   <para><b>什么时候该用它</b>阴影、渐晕等低频不均 + 目标本身灰度接近背景。平坦背景上它会比
	///   <c>Threshold</c> 稳，但在<b>大片的强纹理区会把纹理本身整片切成目标</b>——这是它最常见的误用，
	///   表现为区域面积随纹理而非随缺陷变化。此时先调大 <paramref name="stdDevScale"/>（提高门限），
	///   或改用 <c>DynThreshold</c> 配合自己构造的阈值图。</para>
	///   <para><b>窗口与代价</b><paramref name="maskWidth"/>/<paramref name="maskHeight"/> 是 <c>int</c>（<c>StoreI</c>），
	///   窗口需大于目标尺寸才有意义，但逐窗统计的开销按面积增长，大图上它比 <c>Threshold</c> 慢得多 [待实测：具体倍率]。
	///   偶数窗口与 1×1 窗口本层不校验 [待实测]。窗口边缘像素的补齐方式（是否等同 <c>Reflection</c>/<c>Representative</c> 那类边界处理）在本层没有体现 [待实测]。</para>
	///   <para><b>参数取向</b><paramref name="stdDevScale"/>、<paramref name="absThreshold"/> 接受元组，
	///   多值语义（是否按通道或按区间展开）本层无法判断 [待实测]；单值场景请直接用
	///   <see cref="VarThreshold(int,int,double,double,string)"/>，省掉固定/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion defects = img.VarThreshold(15, 15, new JlTuple(0.2), new JlTuple(2.0), "dark");
	///   int n = defects.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄归调用者释放；<c>lightDark</c> 为字符串，取值错误只在原生端报错。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion VarThreshold(int maskWidth, int maskHeight, JlTuple stdDevScale, JlTuple absThreshold, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(441);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.Store(proc, 2, stdDevScale);
		JlNativeApi.Store(proc, 3, absThreshold);
		JlNativeApi.StoreS(proc, 4, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(stdDevScale);
		JlNativeApi.UnpinTuple(absThreshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部均值/标准差自适应阈值分割（单值版）。</summary>
	/// <param name="maskWidth">窗口宽。Default: 15</param>
	/// <param name="maskHeight">窗口高。Default: 15</param>
	/// <param name="stdDevScale">局部标准差加权系数。Default: 0.2</param>
	/// <param name="absThreshold">与均值的最小灰度差。Default: 2</param>
	/// <param name="lightDark">取亮区还是暗区。Default: "dark"</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法、窗口代价与误用场景见 <see cref="VarThreshold(int,int,JlTuple,JlTuple,string)"/>：同一原生 id 441，
	///   本版本把两个阈值参数经 <c>StoreD</c> 直写，不做元组固定/解固定，单值调参时用它更省事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion dark = img.VarThreshold(15, 15, 0.2, 2.0, "dark");
	///   </code>
	/// </remarks>
	public JlRegion VarThreshold(int maskWidth, int maskHeight, double stdDevScale, double absThreshold, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(441);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, stdDevScale);
		JlNativeApi.StoreD(proc, 3, absThreshold);
		JlNativeApi.StoreS(proc, 4, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用另一幅图逐像素当阈值做分割（元组偏移版）。</summary>
	/// <param name="thresholdImage">逐像素阈值的来源图像。</param>
	/// <param name="offset">叠加在 <paramref name="thresholdImage"/> 上的偏移量。Default: 5.0</param>
	/// <param name="lightDark">取亮于、暗于还是近似等于阈值的像素。Default: "light"</param>
	/// <returns>新区域句柄；两幅输入图像都不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 442，区域输出。阈值图以图像句柄存在第 3 个槽位
	///   （<c>Store(proc, 2, thresholdImage)</c>），偏移与控制参数 <paramref name="lightDark"/> 分别在槽位 0、1，
	///   也就是说<b>阈值是图，不是标量</b>：每个像素各比各的。</para>
	///   <para><b>典型搭配</b>把 <c>MeanImage(...)</c> 或 <c>GaussImage(...)</c> 得到的低频图当阈值图，
	///   就得到"比局部平均亮 <c>offset</c> 的像素"，这是亮斑/暗点检测的标准做法，也是它区别于
	///   <c>VarThreshold</c> 的地方：阈值的来源由你自己选（还可以是另一帧的参考图、或标定好的照度补偿图），
	///   而不是算子内部统计出来的。</para>
	///   <para><b>约束</b>两幅图必须同尺寸，本层不做尺寸/类型匹配检查 [待实测]：阈值图与被分割图的宽高不一致时，
	///   错误由原生端在 <c>PostCall</c> 抛出。<c>MeanImage</c>/<c>GaussImage</c> 的输出类型可能与输入不同
	///   （如 <c>byte</c> 进 <c>real</c> 出），跨类型时 <paramref name="offset"/> 的量纲按谁的类型理解 [待实测]。</para>
	///   <para><b>参数取向</b><paramref name="lightDark"/> 按参数说明有三种取向（亮、暗、与阈值相近）；字符串不经本层校验。
	///   <paramref name="offset"/> 给元组时多值语义本层无法确定 [待实测]，单值请改用
	///   <see cref="DynThreshold(JlImage,double,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage localMean = img.MeanImage(15, 15);                       // 低频照度
	///   using JlRegion spots = img.DynThreshold(localMean, new JlTuple(5.0), "light");
	///   int n = spots.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回区域是新句柄；<c>thresholdImage</c> 只是被读取，不因此转交所有权，
	///   用完自己释放。代码末尾对 <c>this</c> 与 <paramref name="thresholdImage"/> 都做了
	///   <c>GC.KeepAlive</c>，即两幅图在整个原生调用期间都不能被回收。</para>
	/// </remarks>
	public JlRegion DynThreshold(JlImage thresholdImage, JlTuple offset, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(442);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, thresholdImage);
		JlNativeApi.Store(proc, 0, offset);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(offset);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(thresholdImage);
		return obj;
	}

	/// <summary>用另一幅图逐像素当阈值做分割（单值偏移版）。</summary>
	/// <param name="thresholdImage">逐像素阈值的来源图像。</param>
	/// <param name="offset">叠加在阈值图上的偏移量。Default: 5.0</param>
	/// <param name="lightDark">取亮于、暗于还是近似等于阈值的像素。Default: "light"</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法、尺寸匹配与 KeepAlive 细节见 <see cref="DynThreshold(JlImage,JlTuple,string)"/>：同一原生 id 442。
	///   本版本用 <c>StoreD</c> 直写偏移，无元组固定开销，实际调参时基本都用这一版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage refImg = img.GaussImage(15);
	///   using JlRegion bright = img.DynThreshold(refImg, 10.0, "light");
	///   </code>
	/// </remarks>
	public JlRegion DynThreshold(JlImage thresholdImage, double offset, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(442);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, thresholdImage);
		JlNativeApi.StoreD(proc, 0, offset);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(thresholdImage);
		return obj;
	}

	/// <summary>按一个或多个灰度区间分割整幅图像，返回区域。</summary>
	/// <param name="minGray">各区间下界，或特殊值 "min"。Default: 128.0</param>
	/// <param name="maxGray">各区间上界，或特殊值 "max"。Default: 255.0</param>
	/// <returns>落在任一区间内的像素组成的新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 443，<c>InitOCT(proc,1)</c> 只声明一个输出对象，
	///   经 <c>JlRegion.LoadNew(proc,1,...)</c> 取回<b>新句柄</b>：给 N 个区间也只有一个区域（各区间的并），
	///   不是长度为 N 的区域数组——想按区间分别处理，必须先 <c>Connection()</c> 再按灰度另做区分。</para>
	///   <para><b>输出类型</b>本算子出的是 <see cref="JlRegion"/>，不是 <c>JlImage</c>。
	///   需要"分割后的图像"时，把结果区域交给 <c>ReduceDomain</c> 或 <c>PaintRegion</c>；
	///   比较运算符 <c>image &gt;= 128.0</c> 同样返回区域而不是图像，别按布尔图的思路去接。</para>
	///   <para><b>灰度写死 0.0/255.0 会静默漏像素</b>上下界是 double，与本层无关的图像类型决定实际量程：
	///   <c>byte</c> 为 0..255，<c>uint2</c> 到 65535，<c>float</c>、<c>direction</c> 可超过 255。
	///   在 <c>uint2</c>/<c>float</c> 图上写 <c>Threshold(0.0, 255.0)</c> 不报错，只是高灰度像素被丢掉。
	///   跨类型通用写法是用参数说明里的特殊值 <c>"min"</c>/<c>"max"</c>（元组重载可直接
	///   <c>new JlTuple("min")</c>），由原生端按图像类型取实际极值 [待实测：double 重载能否表达特殊值]。</para>
	///   <para><b>区间边界</b>给出的是下界与上界，属闭区间还是左闭右开，托管层只把两个值 <c>Store</c> 给原生，未做任何裁剪或校验 [待实测]。
	///   两个元组按下标两两配对，长度不等或长度为奇数时本层不检查，行为由原生端决定 [待实测]。</para>
	///   <para><b>通道数</b>本层不检查通道数（<c>CountChannels()</c> 需自行调用）。多通道图如何取舍通道未在本层体现 [待实测]，
	///   常规做法是先 <c>AccessChannel(1)</c>（取单个通道，索引从 1 起）或 <c>ChannelsToImage()</c> 拆成通道数组后
	///   用 <c>Rgb3ToGray(imageGreen, imageBlue)</c> 加权合成，再分割。</para>
	///   <para><b>与相邻算子的取舍</b>光照不均用 <c>DynThreshold</c>；对比度弱的边缘用 <c>HysteresisThreshold</c>；
	///   不知道阈值时用 <c>AutoThreshold</c>/<c>BinaryThreshold</c>；只要亚像素等灰度线时用 <c>ThresholdSubPix</c>。
	///   本算子按全图绝对灰度切，亮度漂移会让同一组参数在不同批次图上给出不同面积的区域，这是它最典型的失效方式。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   string type = img.GetImageType()[0].S;                                  // "byte" -&gt; 0..255
	///   using JlRegion all = img.Threshold(new JlTuple("min"), new JlTuple("max"));   // 随类型自适应
	///   int n = all.Connection().CountObj();
	///   using JlRegion two = img.Threshold(new JlTuple(0.0, 200.0), new JlTuple(60.0, 255.0));   // 两个区间取并
	///   </code>
	///   <para><b>资源与坑</b>返回的新句柄归调用者释放（<c>JlRegion</c> 实现 <c>IDisposable</c>）；
	///   末尾 <c>GC.KeepAlive(this)</c> 保证输入图像在原生调用期间不被回收。
	///   元组重载每次调用都固定并解固定 <paramref name="minGray"/>/<paramref name="maxGray"/>，
	///   单次取值请改用 <see cref="Threshold(double,double)"/>。</para>
	/// </remarks>
	public JlRegion Threshold(JlTuple minGray, JlTuple maxGray)
	{
		IntPtr proc = JlNativeApi.PreCall(443);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, minGray);
		JlNativeApi.Store(proc, 1, maxGray);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minGray);
		JlNativeApi.UnpinTuple(maxGray);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按单个灰度区间分割图像（单值版）。</summary>
	/// <param name="minGray">区间下界。Default: 128.0</param>
	/// <param name="maxGray">区间上界。Default: 255.0</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para>语义、边界与灰度量程问题见 <see cref="Threshold(JlTuple,JlTuple)"/>：两个重载同走原生 id 443，
	///   本版本用 <c>StoreD</c> 直写两个 double，不做元组固定/解固定，单次分割应当用它。</para>
	///   <para><b>实际差异</b>参数是 <c>double</c>，因此无法传 <c>"min"</c>/<c>"max"</c> 特殊值：
	///   需要按图像类型自适应量程时只能用元组重载；多区间也只能用元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(128.0, 255.0);
	///   double area = reg.RegionFeatures("area");
	///   </code>
	/// </remarks>
	public JlRegion Threshold(double minGray, double maxGray)
	{
		IntPtr proc = JlNativeApi.PreCall(443);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, minGray);
		JlNativeApi.StoreD(proc, 1, maxGray);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>提取等灰度线（level crossing），结果为亚像素轮廓。</summary>
	/// <param name="threshold">等灰度线的灰度值。Default: 128</param>
	/// <returns>提取出的等灰度线轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 444，输出经 <c>JlXLDCont.LoadNew</c> 取回，是 <see cref="JlXLDCont"/>
	///   轮廓而不是 <see cref="JlRegion"/>：像素中心间用插值定位，所以轮廓坐标可落在像素之间。</para>
	///   <para><b>什么时候用它</b>要拿亚像素位置做测量/拟合时。要的是"哪些像素属于目标"（面积、连通域）时用它反而绕远：
	///   轮廓得先经 <c>GenRegionContourXld("filled")</c> 才能变回区域，且面积不再与像素网格严格对应。</para>
	///   <para><b>易踩</b>它只看灰度等于 <paramref name="threshold"/> 的位置，没有幅值/梯度门限，
	///   所以噪声图上会得到大量几像素长的闭合碎轮廓，可用 <c>LengthXld()</c> 返回的逐条长度筛掉。
	///   多个灰度值经元组一次提多条等灰度线时，输出是单个多轮廓对象还是对象数组，本层无法判断，
	///   用 <c>CountObj()</c> 确认 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont contours = img.ThresholdSubPix(new JlTuple(128.0));
	///   int numContours = contours.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；元组重载会固定/解固定 <paramref name="threshold"/>。</para>
	/// </remarks>
	public JlXLDCont ThresholdSubPix(JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(444);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>提取等灰度线（单值版）。</summary>
	/// <param name="threshold">等灰度线的灰度值。Default: 128</param>
	/// <returns>亚像素轮廓。</returns>
	/// <remarks>
	///   <para>语义、碎轮廓筛查与 XLD→区域的代价见 <see cref="ThresholdSubPix(JlTuple)"/>：同一原生 id 444，
	///   本版本 <c>StoreD</c> 直写单个 double，只提一条等灰度线，无元组固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont c = img.ThresholdSubPix(128.0);
	///   int n = c.CountObj();
	///   </code>
	/// </remarks>
	public JlXLDCont ThresholdSubPix(double threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(444);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按多通道特征向量的距离做区域生长（元组容差版）。</summary>
	/// <param name="metric">特征向量距离的度量。Default: "2-norm"</param>
	/// <param name="minTolerance">距离下界。Default: 0.0</param>
	/// <param name="maxTolerance">距离上界。Default: 20.0</param>
	/// <param name="minSize">输出区域的最小像素数。Default: 30</param>
	/// <returns>生长得到的区域（对象数组）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 445，单路区域输出。判据是<b>特征向量</b>距离：每个像素的各通道值组成向量，
	///   按 <paramref name="metric"/> 比较。因此它的正当用途是多通道图（RGB、Lab，或 <c>Compose3</c>/<c>Compose2</c>
	///   拼出来的自定义特征图）。单通道图上它退化成按灰度差生长，此时更该用 <see cref="Regiongrowing(int,int,double,int)"/>。</para>
	///   <para><b>两个容差是一上一下</b>距离被分成三档：不超过 <paramref name="minTolerance"/> 的像素直接并入，
	///   介于两界之间的像素是否并入由原生端决定 [待实测]，超过 <paramref name="maxTolerance"/> 则拒绝。
	///   <c>minTolerance = 0</c> 是默认，等于"只有完全相同的值才无条件并入"。<paramref name="maxTolerance"/> 越大，
	///   跨区域合并越激进、区域数越少，最终表现为"欠分割"（目标和背景粘成一片）。</para>
	///   <para><b>坑：结果不覆盖全图</b>小于 <paramref name="minSize"/> 的区域被直接丢弃，丢弃部分不会并入邻区，
	///   于是区域之间留下空隙。想拿它做"整图分块"必须再 <c>Union1</c> 或补一次 <c>FillUp</c>，否则面积统计对不上图像总像素数。
	///   <paramref name="minSize"/> 是 <c>int</c> 像素个数，与目标实际尺寸同量纲，换相机分辨率后必须重新给值。</para>
	///   <para><b>与相邻算子的取舍</b>已知每个块内灰度应围绕某个均值保持一致 → <see cref="RegiongrowingMean(JlTuple,JlTuple,double,int)"/>；
	///   想省算力、按栅格播种 → <see cref="Regiongrowing(int,int,JlTuple,int)"/>；只要按灰度区间分块 → <c>Threshold</c>。
	///   生长类算子普遍比阈值类慢，且对椒盐噪声敏感，常规做法是先 <c>MedianImage("circle",2,"mirrored")</c> 再生长。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose3(g, b);
	///   using JlRegion seg = rgb.RegiongrowingN("2-norm", new JlTuple(0.0), new JlTuple(20.0), 30);
	///   int numSegments = seg.CountObj();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="metric"/> 为字符串，不校验取值，错了由原生端 <c>PostCall</c> 抛
	///   <c>JlOperatorException</c>；元组容差版会固定/解固定两个元组，定值调参用 double 版。</para>
	/// </remarks>
	public JlRegion RegiongrowingN(string metric, JlTuple minTolerance, JlTuple maxTolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(445);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, metric);
		JlNativeApi.Store(proc, 1, minTolerance);
		JlNativeApi.Store(proc, 2, maxTolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minTolerance);
		JlNativeApi.UnpinTuple(maxTolerance);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>多通道特征区域生长（单值容差版）。</summary>
	/// <param name="metric">距离度量。Default: "2-norm"</param>
	/// <param name="minTolerance">距离下界。Default: 0.0</param>
	/// <param name="maxTolerance">距离上界。Default: 20.0</param>
	/// <param name="minSize">最小区域像素数。Default: 30</param>
	/// <returns>区域数组。</returns>
	/// <remarks>
	///   <para>三档容差、<c>minSize</c> 造成的空隙、以及"单通道图该改用 <c>Regiongrowing</c>"等结论见
	///   <see cref="RegiongrowingN(string,JlTuple,JlTuple,int)"/>：同一原生 id 445。</para>
	///   <para><b>实际差异</b>两个容差经 <c>StoreD</c> 直写，无固定/解固定开销，定值调参用这一版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seg = img.RegiongrowingN("2-norm", 0.0, 20.0, 30);
	///   int n = seg.CountObj();
	///   </code>
	/// </remarks>
	public JlRegion RegiongrowingN(string metric, double minTolerance, double maxTolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(445);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, metric);
		JlNativeApi.StoreD(proc, 1, minTolerance);
		JlNativeApi.StoreD(proc, 2, maxTolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>栅格播种的区域生长（元组容差版）。</summary>
	/// <param name="rasterHeight">播种点的行间距。Default: 3</param>
	/// <param name="rasterWidth">播种点的列间距。Default: 3</param>
	/// <param name="tolerance">允许并入同一区域的灰度差上限。Default: 6.0</param>
	/// <param name="minSize">输出区域的最小像素数。Default: 100</param>
	/// <returns>生长得到的区域；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 446。按 <paramref name="rasterHeight"/>×<paramref name="rasterWidth"/>
	///   的栅格取候选点做生长；参数说明写明"灰度差不超过 <paramref name="tolerance"/> 的点归入同一区域"，
	///   即上界取等号。参数说明用"accumulated into the same object"描述并入过程，因此缓慢渐变的区域有可能被
	///   逐级并成一大块（与 <c>Threshold</c> 按绝对灰度切的结果不可互相替换）[待实测]。</para>
	///   <para><b>栅格是速度来源，也是漏检来源</b>播种点之间隔 <paramref name="rasterWidth"/> 列，
	///   小于栅格间距的结构根本没有播种点，会<b>整块不出现在结果里</b>（不是变小，是没有）。
	///   默认 3×3 适合做背景分块；要找小缺陷请把栅格降到 1 并改用 <c>Threshold</c>/<c>VarThreshold</c>，
	///   靠生长找小目标既慢又不可复现。</para>
	///   <para><b>坑</b>小于 <paramref name="minSize"/>（像素个数，<c>int</c>）的区域被丢弃且不并邻，
	///   结果不覆盖全图；<paramref name="rasterHeight"/>/<paramref name="rasterWidth"/> 大于图像尺寸时
	///   本层不做检查，播种点数为 0 时返回什么由原生决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>多通道一致性 → <see cref="RegiongrowingN(string,double,double,int)"/>；
	///   已知种子坐标、要求"块内围绕均值一致" → <see cref="RegiongrowingMean(JlTuple,JlTuple,double,int)"/>。
	///   本算子不需要种子，适合"把图自动切成若干块"的分块场景，不适合按目标找目标。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion blocks = img.Regiongrowing(3, 3, new JlTuple(6.0), 100);
	///   int n = blocks.CountObj();
	///   using JlRegion cover = blocks.Union1();            // 生长结果有空隙，需要覆盖全图时自己并起来
	///   </code>
	///   <para><b>资源与坑</b>元组版对 <paramref name="tolerance"/> 固定/解固定；单值请用
	///   <see cref="Regiongrowing(int,int,double,int)"/>。</para>
	/// </remarks>
	public JlRegion Regiongrowing(int rasterHeight, int rasterWidth, JlTuple tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(446);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, rasterHeight);
		JlNativeApi.StoreI(proc, 1, rasterWidth);
		JlNativeApi.Store(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tolerance);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>栅格播种的区域生长（单值容差版）。</summary>
	/// <param name="rasterHeight">播种点行间距。Default: 3</param>
	/// <param name="rasterWidth">播种点列间距。Default: 3</param>
	/// <param name="tolerance">灰度差上限。Default: 6.0</param>
	/// <param name="minSize">最小区域像素数。Default: 100</param>
	/// <returns>区域数组。</returns>
	/// <remarks>
	///   <para>栅格漏检、<c>minSize</c> 空隙等结论见 <see cref="Regiongrowing(int,int,JlTuple,int)"/>：同一原生 id 446，
	///   本版本用 <c>StoreD</c> 直写容差，无固定/解固定开销，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion blocks = img.Regiongrowing(3, 3, 6.0, 100);
	///   int n = blocks.CountObj();
	///   </code>
	/// </remarks>
	public JlRegion Regiongrowing(int rasterHeight, int rasterWidth, double tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(446);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, rasterHeight);
		JlNativeApi.StoreI(proc, 1, rasterWidth);
		JlNativeApi.StoreD(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从给定种子点出发、按"与区域均值的偏差"生长（元组种子版）。</summary>
	/// <param name="startRows">种子点行坐标，与 <paramref name="startColumns"/> 一一对应。Default: []</param>
	/// <param name="startColumns">种子点列坐标。Default: []</param>
	/// <param name="tolerance">像素与区域均值的最大允许偏差。Default: 5.0</param>
	/// <param name="minSize">小于该像素数的区域不输出。Default: 100</param>
	/// <returns>每个合格种子长出的区域；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 447。与前两个生长算子的根本区别：判据是<b>像素与"区域均值"的偏差</b>
	///   （参数说明 Maximum deviation from the mean），而 <see cref="Regiongrowing(int,int,JlTuple,int)"/> 用的是相邻像素差。
	///   均值随并入的像素更新，因此 <paramref name="tolerance"/> 给大时区域会顺着渐变一路吞下去；
	///   给小时区域停在纹理边界，面积对 <paramref name="tolerance"/> 极其敏感，建议按批次直方图重新定值而不是沿用。</para>
	///   <para><b>种子决定结果</b>只有能长成不小于 <paramref name="minSize"/> 的种子才会出现在输出里，
	///   一个种子可能长成多个区域也可能一个都不长（长度与顺序都不保证与种子一一对应）[待实测]，
	///   所以<b>不要按下标把输出区域当成对应种子</b>，需要对应关系时用 <c>TestSubsetRegion</c> 一类包含判断。
	///   两个坐标元组按下标配对，长度不等时多余部分如何处理本层不校验 [待实测]；
	///   默认值是<b>空元组</b>，即不给种子：本层不会替你报错，结果大概率为空区域 [待实测]。</para>
	///   <para><b>坐标顺序</b><paramref name="startRows"/> 在前、<paramref name="startColumns"/> 在后，
	///   与常见的 (column,row) 图像坐标习惯相反；由标定/模板得到的 x,y 记得交换。</para>
	///   <para><b>什么时候用它</b>已知目标大概位置（上一工位、标定 ROI、手工点选），要"以该点为中心把这块东西完整捞出来"。
	///   不知道位置就别用它：全图撒种子比不过 <c>Threshold</c>+<c>Connection</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple rows = new JlTuple(120.0, 340.0);
	///   JlTuple cols = new JlTuple(80.0, 500.0);          // 两个种子，元组等长成对给出
	///   using JlRegion parts = img.RegiongrowingMean(rows, cols, 5.0, 100);
	///   int n = parts.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>两个坐标元组各做固定与 <c>UnpinTuple</c>；返回区域需释放。</para>
	/// </remarks>
	public JlRegion RegiongrowingMean(JlTuple startRows, JlTuple startColumns, double tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(447);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, startRows);
		JlNativeApi.Store(proc, 1, startColumns);
		JlNativeApi.StoreD(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(startRows);
		JlNativeApi.UnpinTuple(startColumns);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从单个种子点按均值偏差生长（整数坐标版）。</summary>
	/// <param name="startRows">种子点行坐标（单个）。Default: []</param>
	/// <param name="startColumns">种子点列坐标（单个）。Default: []</param>
	/// <param name="tolerance">与区域均值的最大偏差。Default: 5.0</param>
	/// <param name="minSize">最小区域像素数。Default: 100</param>
	/// <returns>长出的区域。</returns>
	/// <remarks>
	///   <para>算法与"种子数与输出区域数不对应"等注意事项见 <see cref="RegiongrowingMean(JlTuple,JlTuple,double,int)"/>：
	///   同一原生 id 447，<paramref name="tolerance"/> 在两个重载里都是 <c>double</c>。</para>
	///   <para><b>实际差异（不只是省一个元组）</b>种子坐标经 <c>StoreI</c> 按<b>单个整数</b>传入，
	///   本重载只能给一个种子点，坐标也无法取半像素；多种子必须用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion part = img.RegiongrowingMean(120, 80, 5.0, 100);
	///   double area = part.RegionFeatures("area");
	///   </code>
	/// </remarks>
	public JlRegion RegiongrowingMean(int startRows, int startColumns, double tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(447);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, startRows);
		JlNativeApi.StoreI(proc, 1, startColumns);
		JlNativeApi.StoreD(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Segment an image by "pouring water" over it.
	/// </summary>
	/// <param name="mode">Mode of operation. Default: "all"</param>
	/// <param name="minGray">All gray values smaller than this threshold are disregarded. Default: 0</param>
	/// <param name="maxGray">All gray values larger than this threshold are disregarded. Default: 255</param>
	/// <returns>Segmented regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment an image by "pouring water" over it。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Pouring("all", 0, 255);
	///   </code>
	/// </remarks>
	public JlRegion Pouring(string mode, int minGray, int maxGray)
	{
		IntPtr proc = JlNativeApi.PreCall(448);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, minGray);
		JlNativeApi.StoreI(proc, 2, maxGray);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按阈值提取分水盆地（暗盆地，元组阈值版）。</summary>
	/// <param name="threshold">分水阈值。Default: 10</param>
	/// <returns>找到的分段（暗盆地）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 449，一路区域输出。英文说明把输出明确为 dark basins：
	///   灰度被当成高程面，暗处是盆地，<paramref name="threshold"/> 控制"浸水深度"，决定相邻盆地合并到什么程度 [待实测：确切判据]。
	///   输入应是单通道灰度图；本层不检查通道数 [待实测]。</para>
	///   <para><b>输出规模</b>分水类算子天然过分割：纹理多的图上区域数可达几千，直接 <c>Connection()</c>+逐个统计会拖死节拍。
	///   先 <c>CountObj()</c> 看规模，再按面积/灰度筛（<c>SelectShape</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要"分水线"本身而不是盆地 → <see cref="Watersheds(out JlRegion)"/>；
	///   已经知道每个目标该有一个种子（标记）→ <see cref="WatershedsMarker(JlRegion)"/>，它是控制过分割最直接的手段。
	///   只是想按灰度分层，不要用水分：用 <c>Threshold</c>/<c>AutoThreshold</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.WatershedsThreshold(new JlTuple(10.0));
	///   int n = basins.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄，需释放；元组版做固定/解固定，单值请用
	///   <see cref="WatershedsThreshold(int)"/>。</para>
	/// </remarks>
	public JlRegion WatershedsThreshold(JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(449);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按阈值提取分水盆地（整数阈值版）。</summary>
	/// <param name="threshold">分水阈值。Default: 10</param>
	/// <returns>暗盆地区域数组。</returns>
	/// <remarks>
	///   <para>算法、过分割与三个分水算子之间的取舍见 <see cref="WatershedsThreshold(JlTuple)"/>：同一原生 id 449。</para>
	///   <para><b>实际差异</b>阈值经 <c>StoreI</c> 作整数传，<c>float</c>/<c>direction</c> 图上需要非整数阈值时用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.WatershedsThreshold(10);
	///   </code>
	/// </remarks>
	public JlRegion WatershedsThreshold(int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(449);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>一次算出盆地与盆地之间的分水线（两路区域输出）。</summary>
	/// <param name="watersheds">回传：盆地之间的分水线区域。</param>
	/// <returns>分割出的盆地。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 450，<b>没有</b>任何控制参数：代码里两次 <c>InitOCT</c>（槽位 1 与 2）
	///   声明两个图标输出，返回值是盆地（<c>LoadNew(proc,1,...)</c>），<c>out</c> 参数是分水线
	///   （<c>LoadNew(proc,2,...)</c>）。两者都是区域，容易搞混：要"目标块"用返回值，要"边界线"用 <paramref name="watersheds"/>。</para>
	///   <para><b>无参数的代价</b>没有阈值、没有标记，分割粒度完全由图像本身的灰度极小值决定，
	///   因此在噪声/纹理图上会严重过分割 [待实测：典型区域数量级]。需要控制粒度时改用
	///   <see cref="WatershedsThreshold(JlTuple)"/>（按深度合并）或 <see cref="WatershedsMarker(JlRegion)"/>（按标记生长）。</para>
	///   <para><b>典型用法</b>把分水线当作"减法"用：先分割出粘连块，再用 <c>Difference</c> 从块里挖掉分水线，
	///   让粘连目标在像素级分开。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.Watersheds(out JlRegion lines);
	///   int n = basins.CountObj();
	///   lines.Dispose();                                   // 两个输出都要释放
	///   </code>
	///   <para><b>资源与坑</b><paramref name="watersheds"/> 必须写成 <c>out JlRegion x</c>（按值传会 CS1615）；
	///   两路输出互不隶属，只释放其中一个会漏掉另一个的句柄。</para>
	/// </remarks>
	public JlRegion Watersheds(out JlRegion watersheds)
	{
		IntPtr proc = JlNativeApi.PreCall(450);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out watersheds);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   Threshold operator for signed images.
	/// </summary>
	/// <param name="minSize">Regions smaller than MinSize are suppressed. Default: 20</param>
	/// <param name="minGray">Regions whose maximum absolute gray value is smaller than MinGray are suppressed. Default: 5.0</param>
	/// <param name="threshold">Regions that have a gray value smaller than Threshold (or larger than -Threshold) are suppressed. Default: 2.0</param>
	/// <returns>Positive and negative regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>阈值分割 operator 用于 signed 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.DualThreshold(20, 5.0, 2.0);
	///   </code>
	/// </remarks>
	public JlRegion DualThreshold(int minSize, double minGray, double threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(453);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, minSize);
		JlNativeApi.StoreD(proc, 1, minGray);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Expand a region starting at a given line.
	/// </summary>
	/// <param name="coordinate">Row or column coordinate. Default: 256</param>
	/// <param name="expandType">Stopping criterion. Default: "gradient"</param>
	/// <param name="rowColumn">Segmentation mode (row or column). Default: "row"</param>
	/// <param name="threshold">Threshold for the expansion. Default: 3.0</param>
	/// <returns>Extracted segments.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Expand 区域 starting at 给定 直线。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ExpandLine(256, "gradient", "row", 3.0);
	///   </code>
	/// </remarks>
	public JlRegion ExpandLine(int coordinate, string expandType, string rowColumn, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(454);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, coordinate);
		JlNativeApi.StoreS(proc, 1, expandType);
		JlNativeApi.StoreS(proc, 2, rowColumn);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Expand a region starting at a given line.
	/// </summary>
	/// <param name="coordinate">Row or column coordinate. Default: 256</param>
	/// <param name="expandType">Stopping criterion. Default: "gradient"</param>
	/// <param name="rowColumn">Segmentation mode (row or column). Default: "row"</param>
	/// <param name="threshold">Threshold for the expansion. Default: 3.0</param>
	/// <returns>Extracted segments.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Expand 区域 starting at 给定 直线。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ExpandLine(256, "gradient", "row", 3.0);
	///   </code>
	/// </remarks>
	public JlRegion ExpandLine(int coordinate, string expandType, string rowColumn, double threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(454);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, coordinate);
		JlNativeApi.StoreS(proc, 1, expandType);
		JlNativeApi.StoreS(proc, 2, rowColumn);
		JlNativeApi.StoreD(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect all local minima in an image.
	/// </summary>
	/// <returns>Extracted local minima as regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect all local minima （在图像中）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LocalMin();
	///   </code>
	/// </remarks>
	public JlRegion LocalMin()
	{
		IntPtr proc = JlNativeApi.PreCall(455);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect all gray value lowlands.
	/// </summary>
	/// <returns>Extracted lowlands as regions (one region for each lowland).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect all 灰度值 lowlands。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Lowlands();
	///   </code>
	/// </remarks>
	public JlRegion Lowlands()
	{
		IntPtr proc = JlNativeApi.PreCall(456);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect the centers of all gray value lowlands.
	/// </summary>
	/// <returns>Centers of gravity of the extracted lowlands as regions (one region for each lowland).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect centers all 灰度值 lowlands。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LowlandsCenter();
	///   </code>
	/// </remarks>
	public JlRegion LowlandsCenter()
	{
		IntPtr proc = JlNativeApi.PreCall(457);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect all local maxima in an image.
	/// </summary>
	/// <returns>Extracted local maxima as a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect all local maxima （在图像中）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LocalMax();
	///   </code>
	/// </remarks>
	public JlRegion LocalMax()
	{
		IntPtr proc = JlNativeApi.PreCall(458);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect all gray value plateaus.
	/// </summary>
	/// <returns>Extracted plateaus as regions (one region for each plateau).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect all 灰度值 plateaus。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Plateaus();
	///   </code>
	/// </remarks>
	public JlRegion Plateaus()
	{
		IntPtr proc = JlNativeApi.PreCall(459);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect the centers of all gray value plateaus.
	/// </summary>
	/// <returns>Centers of gravity of the extracted plateaus as regions (one region for each plateau).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect centers all 灰度值 plateaus。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.PlateausCenter();
	///   </code>
	/// </remarks>
	public JlRegion PlateausCenter()
	{
		IntPtr proc = JlNativeApi.PreCall(460);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由直方图自动确定若干灰度区间并分割（元组版）。</summary>
	/// <param name="sigma">直方图高斯平滑量。Default: 2.0</param>
	/// <returns>各自动确定区间内的区域。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 462。对<b>直方图</b>（不是图像）做高斯平滑后找峰，按峰间位置切出多个灰度区间。
	///   参数说明里的 <c>sigma</c> 作用在直方图索引上，与空间分辨率无关：换相机或改图像尺寸不会改变它的含义，
	///   但改图像类型（<c>byte</c> 256 级 vs <c>uint2</c> 65536 级）会让同一个 <c>sigma</c> 对应完全不同的灰度跨度 [待实测]。</para>
	///   <para><b>输出形状</b>返回值可能是<b>区域数组</b>（每个区间一个区域），也可能是单个区域，本层只经
	///   <c>JlRegion.LoadNew(proc,1,...)</c> 取回一个句柄对象；用 <c>CountObj()</c> 判断段数再决定按索引取
	///   （<c>SelectObj</c>）还是整体处理。假设它只出一段是这个算子最常见的写法错误。</para>
	///   <para><b>什么时候该用它</b>灰度级数已知、目标与背景双峰明显、但不想手调阈值时。<b>不该</b>用它的情况：
	///   需要固定量纲的判定标准（它每帧重算区间，标准会漂）；直方图单峰或近似均匀（切出来的区间由噪声峰决定，
	///   结果逐帧抖动）；需要可控的区间数（区间数由峰数决定，不由你决定）。</para>
	///   <para><b>与 <c>BinaryThreshold</c> 的取舍</b>只要前景/背景两类且想知道阈值时优先 <c>BinaryThreshold</c>（它会回报阈值）；
	///   要按多级灰度分层（如料堆高度分级）时用本算子。调 <c>sigma</c> 的方向：调大→峰更少→区间更少。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion bands = img.AutoThreshold(new JlTuple(2.0));
	///   int numBands = bands.CountObj();
	///   using JlRegion first = bands.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b>数组切片 <c>SelectObj</c> 又会产生新句柄，逐层都要释放；元组版有固定/解固定开销。</para>
	/// </remarks>
	public JlRegion AutoThreshold(JlTuple sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(462);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>直方图自动分区间（单值版）。</summary>
	/// <param name="sigma">直方图高斯平滑量。Default: 2.0</param>
	/// <returns>各区间对应的区域（可能为区域数组）。</returns>
	/// <remarks>
	///   <para>算法、"输出可能是区域数组"这一点与不该用它的场景见 <see cref="AutoThreshold(JlTuple)"/>：
	///   同一原生 id 462，本版本用 <c>StoreD</c> 直写单个 <c>sigma</c>，无元组固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion bands = img.AutoThreshold(2.0);
	///   int numBands = bands.CountObj();
	///   </code>
	/// </remarks>
	public JlRegion AutoThreshold(double sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(462);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Segment an image using an automatically determined threshold.
	/// </summary>
	/// <returns>Dark regions of the image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment 图像 使用 automatically determined 阈值分割。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.BinThreshold();
	///   </code>
	/// </remarks>
	public JlRegion BinThreshold()
	{
		IntPtr proc = JlNativeApi.PreCall(463);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Fast thresholding of images using global thresholds.
	/// </summary>
	/// <param name="minGray">Lower threshold for the gray values. Default: 128</param>
	/// <param name="maxGray">Upper threshold for the gray values. Default: 255.0</param>
	/// <param name="minSize">Minimum size of objects to be extracted. Default: 20</param>
	/// <returns>Segmented regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Fast阈值。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.FastThreshold(128, 255.0, 20);
	///   </code>
	/// </remarks>
	public JlRegion FastThreshold(JlTuple minGray, JlTuple maxGray, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(464);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, minGray);
		JlNativeApi.Store(proc, 1, maxGray);
		JlNativeApi.StoreI(proc, 2, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minGray);
		JlNativeApi.UnpinTuple(maxGray);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Fast thresholding of images using global thresholds.
	/// </summary>
	/// <param name="minGray">Lower threshold for the gray values. Default: 128</param>
	/// <param name="maxGray">Upper threshold for the gray values. Default: 255.0</param>
	/// <param name="minSize">Minimum size of objects to be extracted. Default: 20</param>
	/// <returns>Segmented regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Fast阈值。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像分割、连通域分析与区域筛选</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.FastThreshold(128, 255.0, 20);
	///   </code>
	/// </remarks>
	public JlRegion FastThreshold(double minGray, double maxGray, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(464);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, minGray);
		JlNativeApi.StoreD(proc, 1, maxGray);
		JlNativeApi.StoreI(proc, 2, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按灰度相似性做区域扩张/分离，原生算子 id 499，控制参数以元组传入。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="threshold">候选像素与区域边界灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像本身是 iconc 输入（<c>Store(proc, 2)</c>），<paramref name="regions"/> 与 <paramref name="forbiddenArea"/>
	///   是 iconc 1、3 的区域输入，输出为全新 <see cref="JlRegion"/> 元组，输入区域不被修改。与形态学扩张（如
	///   <see cref="JlRegion.DilationCircle(double)"/>）的本质区别是吞并像素由灰度决定：从区域边界出发，
	///   候选像素与边界灰度差不超过 <paramref name="threshold"/> 才被吸收，因此扩张会停在材质边界上；
	///   <paramref name="iterations"/>="maximal" 表示迭代到边界不再变化为止。</para>
	///   <para><b>约束与失效方式</b>弥合间隙要求缝隙两侧灰度差在 <paramref name="threshold"/> 内，否则扩张无效；
	///   <paramref name="threshold"/> 给大了则两种不同材质直接"焊死"成一块——这是本算子最常见的翻车点。
	///   输出个数一般不等于输入：每弥合一个间隙少一个区域，分离模式下又可能变多，
	///   用 <c>CountObj()</c> 核对数量，不要假设 result[i] 对应 regions[i]。<paramref name="mode"/> 各取值的确切语义
	///   托管层看不出来 [待实测]。多通道图像下按灰度还是按颜色距离比较 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要让扩张停在固定的材质灰度（不随边界推进漂移）→
	///   <see cref="ExpandGrayRef(JlRegion,JlRegion,JlTuple,string,JlTuple,JlTuple)"/>；纯几何放大、不看灰度 → 直接用区域形态学。</para>
	///   <para><b>参数取向</b>本重载 <paramref name="iterations"/>/<paramref name="threshold"/> 以 <see cref="JlTuple"/> 传入：
	///   <c>Store</c> 固定、调用后 <c>UnpinTuple</c>；多元素是否逐区域对应 [待实测]，单值请用
	///   <see cref="ExpandGray(JlRegion,JlRegion,string,string,int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(120.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);   // 左上角 10x10 不许扩张
	///   using JlRegion grown = img.ExpandGray(seeds, banned, new JlTuple("maximal"), "image", new JlTuple(32.0));
	///   int n = grown.CountObj();   // 通常不等于 seeds.CountObj()
	///   </code>
	///   <para><b>资源与坑</b>返回的新区域需释放；实现只靠 <c>GC.KeepAlive</c> 钉住图像与两个输入区域，调用结束即可各自释放。
	///   大图上 "maximal" 迭代很慢，只为弥合几个缺口时先 <see cref="JlRegion.Connection()"/> 拆分连通域、只喂需要的种子。</para>
	/// </remarks>
	public JlRegion ExpandGray(JlRegion regions, JlRegion forbiddenArea, JlTuple iterations, string mode, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(499);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.Store(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.Store(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>灰度扩张/分离（迭代与阈值以标量传入）。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="threshold">候选像素与区域边界灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para>灰度吞并机制、<paramref name="threshold"/> 过大导致误粘连、输出个数变化等要点见
	///   <see cref="ExpandGray(JlRegion,JlRegion,JlTuple,string,JlTuple)"/>：同一原生 id 499，本版本
	///   <paramref name="iterations"/>/<paramref name="mode"/> 走 <c>StoreS</c>、<paramref name="threshold"/> 走 <c>StoreI</c> 直写，
	///   无元组固定/解固定，是常规写法；<paramref name="threshold"/> 只接受整数灰度差，需要 0.5 级差值时走元组版 [待实测：小数是否被截断]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(120.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);
	///   using JlRegion grown = img.ExpandGray(seeds, banned, "maximal", "image", 32);
	///   </code>
	/// </remarks>
	public JlRegion ExpandGray(JlRegion regions, JlRegion forbiddenArea, string iterations, string mode, int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(499);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.StoreS(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreI(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>按固定参考灰度做区域扩张/分离，原生算子 id 500，控制参数以元组传入。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="refGray">用于比较的参考灰度值（或颜色）。Default: 128</param>
	/// <param name="threshold">候选像素与参考灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>输入布局与 <see cref="ExpandGray(JlRegion,JlRegion,JlTuple,string,JlTuple)"/> 相同
	///   （图像 iconc 2、区域 iconc 1/3、新区域输出 iconc 1），本版本多出 iconc 控制槽位 2 的 <paramref name="refGray"/>
	///   （<paramref name="threshold"/> 因此后移到 3）。实质差异是对比基准：<c>ExpandGray</c> 拿候选像素和<b>区域当前边界</b>的灰度比，
	///   边界会随灰度渐变慢慢推进；这里候选像素只和<b>固定参考值</b> <paramref name="refGray"/> 比，
	///   差值在 <paramref name="threshold"/> 内才吸收——扩张停在"该材质"的灰度带内，不被过渡带拖走。</para>
	///   <para><b>取舍</b>目标材质灰度稳定但边界发虚、希望"长到某个材质为止"→ 本算子；希望吸收一切与当前区域连续的相近像素 →
	///   <c>ExpandGray</c>。颜色图像需要逐通道给参考值，用本元组版最自然（多元素与通道数的对应关系 [待实测]）。</para>
	///   <para><b>参数取向</b><paramref name="iterations"/>/<paramref name="refGray"/>/<paramref name="threshold"/> 均为
	///   <c>Store</c> 固定 + <c>UnpinTuple</c> 解固定；单值简写见 <see cref="ExpandGrayRef(JlRegion,JlRegion,string,string,int,int)"/>，
	///   但那里 <paramref name="refGray"/> 只能传一个 int，颜色图像无法逐通道设定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(200.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);
	///   using JlRegion grown = img.ExpandGrayRef(seeds, banned, new JlTuple("maximal"), "image",
	///       new JlTuple(230.0), new JlTuple(25.0));   // 只长到灰度 230±25 的像素
	///   </code>
	///   <para><b>资源与坑</b>返回的新区域需释放；<paramref name="refGray"/> 与种子实际灰度明显失配时（如参考值落在背景上），
	///   扩张结果会整体偏空或吞进背景，调 <c>Intensity</c> 量一下种子均值再定参考值。</para>
	/// </remarks>
	public JlRegion ExpandGrayRef(JlRegion regions, JlRegion forbiddenArea, JlTuple iterations, string mode, JlTuple refGray, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(500);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.Store(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.Store(proc, 2, refGray);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		JlNativeApi.UnpinTuple(refGray);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>固定参考灰度的区域扩张（控制参数全部以标量传入）。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="refGray">参考灰度值。Default: 128</param>
	/// <param name="threshold">候选像素与参考灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para>固定 <c>refGray</c> 基准与 <c>ExpandGray</c> 的取舍见
	///   <see cref="ExpandGrayRef(JlRegion,JlRegion,JlTuple,string,JlTuple,JlTuple)"/>：同一原生 id 500，
	///   本版本 <c>StoreS</c>/<c>StoreI</c> 直写四个控制参数，无固定/解固定；<paramref name="refGray"/> 与
	///   <paramref name="threshold"/> 都是 <c>int</c>，只能对单一灰度值生效，颜色图像的逐通道参考需回元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(200.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);
	///   using JlRegion grown = img.ExpandGrayRef(seeds, banned, "maximal", "image", 230, 25);
	///   </code>
	/// </remarks>
	public JlRegion ExpandGrayRef(JlRegion regions, JlRegion forbiddenArea, string iterations, string mode, int refGray, int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(500);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.StoreS(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreI(proc, 2, refGray);
		JlNativeApi.StoreI(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Calculate the difference of two object tuples.
	/// </summary>
	/// <param name="objectsSub">Object tuple 2.</param>
	/// <returns>Objects from Objects that are not part of ObjectsSub.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 求差 two 对象 元组。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage objectsSub = ...;
	///   JlImage obj = ...;
	///   var result = obj.ObjDiff(objectsSub);
	///   </code>
	/// </remarks>
	public JlImage ObjDiff(JlImage objectsSub)
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
	///   Set single gray values in an image.
	/// </summary>
	/// <param name="row">Row coordinates of the pixels to be modified. Default: 0</param>
	/// <param name="column">Column coordinates of the pixels to be modified. Default: 0</param>
	/// <param name="grayval">Gray values to be used. Default: 255.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 single 灰度值s （在图像中）。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.SetGrayval(0, 0, 255.0);
	///   </code>
	/// </remarks>
	public void SetGrayval(JlTuple row, JlTuple column, JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(559);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, grayval);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(grayval);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set single gray values in an image.
	/// </summary>
	/// <param name="row">Row coordinates of the pixels to be modified. Default: 0</param>
	/// <param name="column">Column coordinates of the pixels to be modified. Default: 0</param>
	/// <param name="grayval">Gray values to be used. Default: 255.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 single 灰度值s （在图像中）。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.SetGrayval(0, 0, 255.0);
	///   </code>
	/// </remarks>
	public void SetGrayval(int row, int column, double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(559);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, grayval);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Paint XLD objects into an image.
	/// </summary>
	/// <param name="XLD">XLD objects to be painted into the input image.</param>
	/// <param name="grayval">Desired gray value of the xld object. Default: 255.0</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>绘制 XLD objects 为 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLD XLD = ...;
	///   JlImage obj = ...;
	///   var result = obj.PaintXld(XLD, 255.0);
	///   </code>
	/// </remarks>
	public JlImage PaintXld(JlXLD XLD, JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(560);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, XLD);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(XLD);
		return obj;
	}

	/// <summary>
	///   Paint XLD objects into an image.
	/// </summary>
	/// <param name="XLD">XLD objects to be painted into the input image.</param>
	/// <param name="grayval">Desired gray value of the xld object. Default: 255.0</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>绘制 XLD objects 为 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLD XLD = ...;
	///   JlImage obj = ...;
	///   var result = obj.PaintXld(XLD, 255.0);
	///   </code>
	/// </remarks>
	public JlImage PaintXld(JlXLD XLD, double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(560);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, XLD);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(XLD);
		return obj;
	}

	/// <summary>
	///   Paint regions into an image.
	/// </summary>
	/// <param name="region">Regions to be painted into the input image.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>绘制 区域 为 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.PaintRegion(region, 255.0, "fill");
	///   </code>
	/// </remarks>
	public JlImage PaintRegion(JlRegion region, JlTuple grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(561);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Paint regions into an image.
	/// </summary>
	/// <param name="region">Regions to be painted into the input image.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>绘制 区域 为 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.PaintRegion(region, 255.0, "fill");
	///   </code>
	/// </remarks>
	public JlImage PaintRegion(JlRegion region, double grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(561);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Overpaint regions in an image.
	/// </summary>
	/// <param name="region">Regions to be painted into the input image.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Overpaint 区域 （在图像中）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   obj.OverpaintRegion(region, 255.0, "fill");
	///   </code>
	/// </remarks>
	public void OverpaintRegion(JlRegion region, JlTuple grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(562);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
	}

	/// <summary>
	///   Overpaint regions in an image.
	/// </summary>
	/// <param name="region">Regions to be painted into the input image.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Overpaint 区域 （在图像中）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   obj.OverpaintRegion(region, 255.0, "fill");
	///   </code>
	/// </remarks>
	public void OverpaintRegion(JlRegion region, double grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(562);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
	}

	/// <summary>
	///   Create an image with a specified constant gray value.
	/// </summary>
	/// <param name="grayval">Gray value to be used for the output image. Default: 0</param>
	/// <returns>Image with constant gray value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 图像 使用 指定 常数 灰度值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GenImageProto(0);
	///   </code>
	/// </remarks>
	public JlImage GenImageProto(JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(563);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create an image with a specified constant gray value.
	/// </summary>
	/// <param name="grayval">Gray value to be used for the output image. Default: 0</param>
	/// <returns>Image with constant gray value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 图像 使用 指定 常数 灰度值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GenImageProto(0);
	///   </code>
	/// </remarks>
	public JlImage GenImageProto(double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(563);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Paint the gray values of an image into another image.
	/// </summary>
	/// <param name="imageDestination">Input image to be painted over.</param>
	/// <returns>Result image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>绘制 灰度值s 图像 为 another 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageDestination = ...;
	///   JlImage obj = ...;
	///   var result = obj.PaintGray(imageDestination);
	///   </code>
	/// </remarks>
	public JlImage PaintGray(JlImage imageDestination)
	{
		IntPtr proc = JlNativeApi.PreCall(564);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageDestination);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageDestination);
		return obj;
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
	///   JlImage obj = ...;
	///   var result = obj.CopyObj(1, 1);
	///   </code>
	/// </remarks>
	public new JlImage CopyObj(int index, int numObj)
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
	///   JlImage objects2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.ConcatObj(objects2);
	///   </code>
	/// </remarks>
	public JlImage ConcatObj(JlImage objects2)
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
	///   Copy an image and allocate new memory for it.
	/// </summary>
	/// <returns>Copied image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Copy 图像 和 allocate new memory 用于 it。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CopyImage();
	///   </code>
	/// </remarks>
	public JlImage CopyImage()
	{
		IntPtr proc = JlNativeApi.PreCall(571);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
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
	///   JlImage obj = ...;
	///   var result = obj.SelectObj(1);
	///   </code>
	/// </remarks>
	public new JlImage SelectObj(JlTuple index)
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
	///   JlImage obj = ...;
	///   var result = obj.SelectObj(1);
	///   </code>
	/// </remarks>
	public new JlImage SelectObj(int index)
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
	///   JlImage objects2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.CompareObj(objects2, 0.0);
	///   </code>
	/// </remarks>
	public int CompareObj(JlImage objects2, JlTuple epsilon)
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

	/// <summary>逐像素比较两栈图像是否近似相等，返回 1/0 布尔整数。</summary>
	/// <param name="objects2">与当前图像栈逐张比较的测试对象。</param>
	/// <param name="epsilon">两个灰度值（或坐标）之间允许的最大差值。Default: 0.0</param>
	/// <returns>1 表示两栈逐张、逐像素均在容差内相等；0 表示不相等。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 573。当前对象是第一路输入：把自身栈内每张图与 <paramref name="objects2"/> 栈内对应位置的图逐像素比较，任一张超差即整体返回 0。</para>
	///   <para><b>与同类算子的取舍</b>需要灰度容差时用本方法；只判严格等价用 <see cref="TestEqualObj"/>。与 <see cref="JlImage"/> 相等性判断不同，本方法不抛异常、只给 0/1，适合做帧间去重。</para>
	///   <para><b>参数取向</b>返回 int（非 bool），由原生侧按 INTEGER 装载（LoadI）；比较结果不落新句柄，两栈图像均保持原样。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage imgA = new JlImage("byte", 64, 64);
	///   JlImage imgB = new JlImage("byte", 64, 64);
	///   int isEqual = imgA.CompareObj(imgB, 0.0);
	///   imgA.Dispose();
	///   imgB.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两栈对象个数、尺寸或类型不一致时按"不等"处理而不会报错 [待实测]。元组重载（epsilon 为 JlTuple）与标量重载同用 id 573：元组重载调用后需钉住并解除固定（UnpinTuple），单值场景直接用本标量重载可省去该开销。返回的 0/1 不产生新句柄，但示例中两个图像句柄仍须各自 Dispose。</para>
	/// </remarks>
	public int CompareObj(JlImage objects2, double epsilon)
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

	/// <summary>不带容差地判断两栈图像对象是否等价，返回 1/0 布尔整数。</summary>
	/// <param name="objects2">与当前图像栈比较的对照对象。</param>
	/// <returns>1 表示等价；0 表示不等价。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 576。与 <see cref="CompareObj(JlImage,double)"/> 的区别：本方法没有 epsilon 参数，做的是无容差的严格比较，适合校验"同一句柄内容未被改动"或复制前后一致性检查。</para>
	///   <para><b>参数取向</b>返回 int（非 bool），原生侧按 INTEGER 装载；不产生新句柄，两栈均保持原样。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage imgA = new JlImage("byte", 64, 64);
	///   JlImage imgB = new JlImage("byte", 64, 64);
	///   int same = imgA.TestEqualObj(imgB);
	///   imgA.Dispose();
	///   imgB.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>需要容忍灰度抖动（例如压缩或噪声引起的 ±1）时不要用本方法，改用带 epsilon 的 CompareObj，否则帧间几乎永远返回 0。两栈个数或尺寸不一致时的返回约定 [待实测]。</para>
	/// </remarks>
	public int TestEqualObj(JlImage objects2)
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

	/// <summary>从交错（interleaved）像素缓冲生成三通道图像，原地改写当前句柄。</summary>
	/// <param name="pixelPointer">指向交错排列像素首地址的指针，通道按 colorFormat 顺序逐像素交替存放。</param>
	/// <param name="colorFormat">输入像素的通道顺序格式。Default: "rgb"</param>
	/// <param name="originalWidth">输入缓冲一行实际的像素个数（用于定位下一行）。Default: 512</param>
	/// <param name="originalHeight">输入缓冲的行数。Default: 512</param>
	/// <param name="alignment">保留参数，当前版本不使用。</param>
	/// <param name="type">输出图像像素类型。Default: "byte"</param>
	/// <param name="imageWidth">输出图像宽度，0 表示取 originalWidth。Default: 0</param>
	/// <param name="imageHeight">输出图像高度，0 表示取 originalHeight。Default: 0</param>
	/// <param name="startRow">所需图像部分左上角的行号。Default: 0</param>
	/// <param name="startColumn">所需图像部分左上角的列号。Default: 0</param>
	/// <param name="bitsPerChannel">输出图像每通道有效位数，-1 表示全部位。Default: -1</param>
	/// <param name="bitShift">颜色值右移位数（仅对 uint2 输入有意义）。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 580。从外部交错缓冲（如相机 SDK 的 RGBRGBRGB… 帧）零拷贝建图；本方法体开头先 Dispose 再 Load，属<b>原地改写</b>：调用后当前句柄即新图，旧句柄内容作废，不返回新对象。</para>
	///   <para><b>约束或前提</b>像素指针必须指向非托管可见且<b>不会被 GC 移动</b>的内存：托管数组须先 GCHandle.Pinned 固定或由非托管层持有；图像后续被其它算子读取前不得释放该内存 [待实测]。缓冲按 originalWidth 跨行寻址，与输出尺寸不同时靠 startRow/startColumn + originalWidth 抽取子区域。</para>
	///   <para><b>与相邻算子的取舍</b>数据已是三平面（R 整幅、G 整幅、B 整幅）时用 <see cref="GenImage3"/>，交错格式硬套 GenImage3 会得到通道串扰的图；只需单通道用 <see cref="GenImage1"/>。</para>
	///   <para><b>参数取向</b>返回 void，输出经 Load 写回 this；type 为 "uint2" 时才考虑 bitShift。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] rgb = new byte[64 * 64 * 3];
	///   System.Runtime.InteropServices.GCHandle pinned =
	///       System.Runtime.InteropServices.GCHandle.Alloc(rgb, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   try
	///   {
	///       JlImage img = new JlImage();
	///       img.GenImageInterleaved(pinned.AddrOfPinnedObject(), "rgb", 64, 64, 0, "byte", 0, 0, 0, 0, -1, 0);
	///   }
	///   finally
	///   {
	///       pinned.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>bitsPerChannel 与 type 位数不匹配会截断高位 [待实测]；本方法不复制像素，Free 固定句柄后继续用该图像读取像素属未定义行为。</para>
	/// </remarks>
	public void GenImageInterleaved(IntPtr pixelPointer, string colorFormat, int originalWidth, int originalHeight, int alignment, string type, int imageWidth, int imageHeight, int startRow, int startColumn, int bitsPerChannel, int bitShift)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(580);
		JlNativeApi.StoreIP(proc, 0, pixelPointer);
		JlNativeApi.StoreS(proc, 1, colorFormat);
		JlNativeApi.StoreI(proc, 2, originalWidth);
		JlNativeApi.StoreI(proc, 3, originalHeight);
		JlNativeApi.StoreI(proc, 4, alignment);
		JlNativeApi.StoreS(proc, 5, type);
		JlNativeApi.StoreI(proc, 6, imageWidth);
		JlNativeApi.StoreI(proc, 7, imageHeight);
		JlNativeApi.StoreI(proc, 8, startRow);
		JlNativeApi.StoreI(proc, 9, startColumn);
		JlNativeApi.StoreI(proc, 10, bitsPerChannel);
		JlNativeApi.StoreI(proc, 11, bitShift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从三个平面指针（R/G/B 各一整幅）生成三通道图像，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pixelPointerRed">第一通道（R）首个灰度值指针。</param>
	/// <param name="pixelPointerGreen">第二通道（G）首个灰度值指针。</param>
	/// <param name="pixelPointerBlue">第三通道（B）首个灰度值指针。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 590。三平面（planar）布局零拷贝建图：三个指针各指向 width*height 个连续同类像素。方法体先 Dispose 再 Load，属<b>原地改写</b>，不返回新句柄。</para>
	///   <para><b>约束或前提</b>三个缓冲在图像存续期内必须保持有效且不被 GC 移动（托管数组须 GCHandle 固定）；三块缓冲须同尺寸同类型，否则读像素越界。</para>
	///   <para><b>与相邻算子的取舍</b>相机给出的是逐像素交错的 RGBRGB… 时用 <see cref="GenImageInterleaved"/>；只要一个通道用 <see cref="GenImage1"/>。本方法在原生侧不复制像素，通道顺序由指针传入顺序决定（第一指针即通道 1，语义上叫 Red 但不强制）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] red = new byte[64 * 64];
	///   byte[] green = new byte[64 * 64];
	///   byte[] blue = new byte[64 * 64];
	///   System.Runtime.InteropServices.GCHandle h1 = System.Runtime.InteropServices.GCHandle.Alloc(red, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   System.Runtime.InteropServices.GCHandle h2 = System.Runtime.InteropServices.GCHandle.Alloc(green, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   System.Runtime.InteropServices.GCHandle h3 = System.Runtime.InteropServices.GCHandle.Alloc(blue, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   try
	///   {
	///       JlImage img = new JlImage();
	///       img.GenImage3("byte", 64, 64, h1.AddrOfPinnedObject(), h2.AddrOfPinnedObject(), h3.AddrOfPinnedObject());
	///   }
	///   finally
	///   {
	///       h1.Free();
	///       h2.Free();
	///       h3.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>与 GenImage3Extern 的区别：本算子假定内存生命周期由调用方管理且无释放回调；需要在图像销毁时自动回收非托管内存应改用 GenImage3Extern。</para>
	/// </remarks>
	public void GenImage3(string type, int width, int height, IntPtr pixelPointerRed, IntPtr pixelPointerGreen, IntPtr pixelPointerBlue)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(590);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointerRed);
		JlNativeApi.StoreIP(proc, 4, pixelPointerGreen);
		JlNativeApi.StoreIP(proc, 5, pixelPointerBlue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从单通道像素指针建图，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pixelPointer">指向首个灰度值的指针，像素按行连续存放。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 591。把外部单通道缓冲包成图像；方法体先 Dispose 再 Load，属<b>原地改写</b>，不返回新句柄。</para>
	///   <para><b>约束或前提</b>缓冲须连续按行存放、长度至少 width*height*每像素字节数；托管数组要先 GCHandle 固定，且在不再使用图像前保持固定，本算子不复制像素 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>需要"复制一份、之后缓冲可立即释放"的语义时用 <see cref="GenImage1Rect"/>（doCopy 传 "true"）；需要图像销毁时回调释放内存用 <see cref="GenImage1Extern"/>；三通道用 <see cref="GenImage3"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] gray = new byte[64 * 64];
	///   System.Runtime.InteropServices.GCHandle h = System.Runtime.InteropServices.GCHandle.Alloc(gray, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   try
	///   {
	///       JlImage img = new JlImage();
	///       img.GenImage1("byte", 64, 64, h.AddrOfPinnedObject());
	///   }
	///   finally
	///   {
	///       h.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>域（domain）为整幅矩形；只想造一块全常数图（不需要外部内存）应改用 <see cref="GenImageConst"/>，由运行时管理内存、无悬挂指针风险。</para>
	/// </remarks>
	public void GenImage1(string type, int width, int height, IntPtr pixelPointer)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(591);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>创建整幅为常数灰度的单通道图像，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 592。内存由视觉运行时自行分配并全部置为该类型的常数灰度（0）；方法体先 Dispose 再 Load，属<b>原地改写</b>，不返回新句柄。</para>
	///   <para><b>与相邻算子的取舍</b>想包一块已有像素缓冲用 <see cref="GenImage1"/>；想要线性灰度坡用 <see cref="GenImageGrayRamp"/>。本算子是拿"干净底图"（做叠加、掩码、计时占位）最省事的途径，无外部内存生命周期负担。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage();
	///   img.GenImageConst("uint2", 512, 512);
	///   </code>
	///   <para><b>资源与坑</b>uint2 图像常数值 0 并非"最暗可视值"意义上的 12bit 起点，后续与 8bit 图做 AddImage 等运算前先注意位深不一致会被类型检查拒绝 [待实测]。生成多通道常数图没有对应参数，需自行 AppendChannel 拼接。</para>
	/// </remarks>
	public void GenImageConst(string type, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(592);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成线性灰度坡图像（坡率按行/列方向给定），原地改写当前句柄。</summary>
	/// <param name="alpha">沿行方向（row 增大）每行灰度增量。Default: 1.0</param>
	/// <param name="beta">沿列方向（column 增大）每列灰度增量。Default: 1.0</param>
	/// <param name="mean">参考点处的灰度值。Default: 128</param>
	/// <param name="row">参考点的行号。Default: 256</param>
	/// <param name="column">参考点的列号。Default: 256</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 604。像素灰度按 gray(r,c) = mean + alpha*(r-row) + beta*(c-column) 线性铺展；方法体先 Dispose 再 Load，属<b>原地改写</b>。</para>
	///   <para><b>约束或前提</b>参考点 (row,column) 不必落在图内，落在图外则整幅位于坡面同一侧；byte 类型下越出 0…255 的像素会按饱和处理 [待实测]，标定照明均匀性时建议先用 float 类型验证坡幅。</para>
	///   <para><b>与相邻算子的取舍</b>要常数底图用 <see cref="GenImageConst"/>；要模拟渐晕/平场不均时本算子的两个独立坡率（行、列）比先建图再乘系数更省一步，但无法表达径向渐变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage ramp = new JlImage();
	///   ramp.GenImageGrayRamp(1.0, 2.0, 128, 256, 256, 512, 512);
	///   </code>
	///   <para><b>资源与坑</b>alpha/beta 为 double、参考点行列与宽高为 int，原生装载序为 D,D,D,I,I,I,I，与 C# 形参序一致（无重排）。产物是单通道图，参与彩色流程前需 ChannelsToImage 合成。</para>
	/// </remarks>
	public void GenImageGrayRamp(double alpha, double beta, double mean, int row, int column, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(604);
		JlNativeApi.StoreD(proc, 0, alpha);
		JlNativeApi.StoreD(proc, 1, beta);
		JlNativeApi.StoreD(proc, 2, mean);
		JlNativeApi.StoreI(proc, 3, row);
		JlNativeApi.StoreI(proc, 4, column);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从三个平面指针建三通道图并可选注册内存释放回调，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pointerRed">第一通道首个灰度值指针。</param>
	/// <param name="pointerGreen">第二通道首个灰度值指针。</param>
	/// <param name="pointerBlue">第三通道首个灰度值指针。</param>
	/// <param name="clearProc">图像销毁时调用的内存释放过程指针，0 表示不回调。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 605。与 <see cref="GenImage3"/> 同为三平面零拷贝建图，多出的 clearProc 让运行时在 Dispose 该图像时代为释放非托管缓冲，实现"图像持有并接管内存"。</para>
	///   <para><b>约束或前提</b>clearProc 只适用于非托管内存（如 Marshal.AllocHGlobal 或原生分配器）；对托管数组传回调是错误用法——托管数组须 GCHandle 固定且回调无法回收它。传 0 时内存管理责任仍在调用方。</para>
	///   <para><b>与相邻算子的取舍</b>拿不准释放时机就不要传回调、改用 GenImage3 自管生命周期；单通道对应 <see cref="GenImage1Extern"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr red = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   IntPtr green = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   IntPtr blue = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   JlImage img = new JlImage();
	///   img.GenImage3Extern("byte", 64, 64, red, green, blue, IntPtr.Zero);
	///   img.Dispose();
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(red);
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(green);
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(blue);
	///   </code>
	///   <para><b>资源与坑</b>示例传 0 意味着自行负责释放三块 HGlobal 内存；若把某块内存交给 clearProc 接管后又手动释放同一块，会二次释放崩溃。图像存续期内不得移动或释放缓冲。</para>
	/// </remarks>
	public void GenImage3Extern(string type, int width, int height, IntPtr pointerRed, IntPtr pointerGreen, IntPtr pointerBlue, IntPtr clearProc)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(605);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pointerRed);
		JlNativeApi.StoreIP(proc, 4, pointerGreen);
		JlNativeApi.StoreIP(proc, 5, pointerBlue);
		JlNativeApi.StoreIP(proc, 6, clearProc);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从单通道指针建图并可选注册内存释放回调，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pixelPointer">指向首个灰度值的指针。</param>
	/// <param name="clearProc">图像销毁时调用的内存释放过程指针，0 表示不回调。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 606。相对 <see cref="GenImage1"/> 增加 clearProc：图像对象被删除时由运行时回调释放该像素内存，适合把 Marshal.AllocHGlobal 或原生帧缓冲的生命周期交给图像接管。</para>
	///   <para><b>约束或前提</b>回调只应对非托管内存注册；托管数组用本方法会在数组被 GC 回收后留下悬挂图像。传 0 则调用方自管释放时机（必须晚于图像最后一次使用）。</para>
	///   <para><b>与相邻算子的取舍</b>想要"复制一份再脱钩"用 <see cref="GenImage1Rect"/> 的 doCopy="true"；三通道用 <see cref="GenImage3Extern"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   JlImage img = new JlImage();
	///   img.GenImage1Extern("byte", 64, 64, buf, IntPtr.Zero);
	///   img.Dispose();
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);
	///   </code>
	///   <para><b>资源与坑</b>同一块内存被注册给两张图像（clearProc 非 0）会二次释放；本示例因回调传 0 而手动配对释放。</para>
	/// </remarks>
	public void GenImage1Extern(string type, int width, int height, IntPtr pixelPointer, IntPtr clearProc)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(606);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointer);
		JlNativeApi.StoreIP(proc, 4, clearProc);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从带行距/位距的任意外部缓冲抠出矩形区域建图，原地改写当前句柄。</summary>
	/// <param name="pixelPointer">指向首像素的指针，可指向外部大图内部的子矩形起点。</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="verticalPitch">外部缓冲中相邻两行同列像素间的字节距离（= 外部行字节跨度，可大于 width*像素字节数）。</param>
	/// <param name="horizontalBitPitch">外部缓冲中相邻两像素间的位距离。Default: 8</param>
	/// <param name="bitsPerPixel">每像素有效位数。Default: 8</param>
	/// <param name="doCopy">"true" 时复制像素数据、与原缓冲脱钩；"false" 时仅引用。Default: "false"</param>
	/// <param name="clearProc">图像销毁时的内存释放过程指针，0 表示不回调。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 607。专为"外部图像行不对齐/位打包"场景设计：verticalPitch 以<b>字节</b>计、horizontalBitPitch 以<b>位</b>计，两值允许与紧凑布局不同（如 4:2:2 或带 padding 的 stride）；方法体先 Dispose 再 Load，属<b>原地改写</b>。</para>
	///   <para><b>约束或前提</b>horizontalBitPitch 小于 bitsPerPixel 表示位打包格式，逐像素按位距离寻址；参数组合不合法（如位距为 0）时行为未定义 [待实测]。像素类型面向 8 位字节图 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只有连续紧凑的整幅缓冲时用更简单的 <see cref="GenImage1"/>；需要在原缓冲可释放后仍安全使用图像，务必 doCopy="true"，代价是一次内存复制。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] buf = new byte[100 * 64];
	///   System.Runtime.InteropServices.GCHandle h = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   JlImage img = new JlImage();
	///   try
	///   {
	///       img.GenImage1Rect(h.AddrOfPinnedObject(), 64, 64, 100, 8, 8, "true", IntPtr.Zero);
	///   }
	///   finally
	///   {
	///       h.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>示例因 doCopy="true" 才允许在 finally 立即解除固定；若传 "false"，h.Free() 之后图像像素即为悬挂读。clearProc 语义同 <see cref="GenImage1Extern"/>。</para>
	/// </remarks>
	public void GenImage1Rect(IntPtr pixelPointer, int width, int height, int verticalPitch, int horizontalBitPitch, int bitsPerPixel, string doCopy, IntPtr clearProc)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(607);
		JlNativeApi.StoreIP(proc, 0, pixelPointer);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreI(proc, 3, verticalPitch);
		JlNativeApi.StoreI(proc, 4, horizontalBitPitch);
		JlNativeApi.StoreI(proc, 5, bitsPerPixel);
		JlNativeApi.StoreS(proc, 6, doCopy);
		JlNativeApi.StoreIP(proc, 7, clearProc);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>取图像域最小外接矩形对应的像素指针及行/位距信息。</summary>
	/// <param name="width">输出外接矩形宽度。</param>
	/// <param name="height">输出外接矩形高度。</param>
	/// <param name="verticalPitch">相邻两行的字节距离，等于 输入图宽*(HorizontalBitPitch/8)。</param>
	/// <param name="horizontalBitPitch">相邻两像素的位距离。</param>
	/// <param name="bitsPerPixel">每像素有效位数。</param>
	/// <returns>指向外接矩形首像素的非托管指针（不是新句柄，无需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 608。与 <see cref="GetImagePointer1(out string,out int,out int)"/> 不同：返回的是<b>域（domain）最小外接矩形</b>的指针与布局参数，域被 ReduceDomain 缩小后行距仍按原图宽计，须用 verticalPitch 跨行寻址，不能假定 width*height 连续。</para>
	///   <para><b>约束或前提</b>仅单通道图像可用（彩色图用 GetImagePointer3）；域为空时外接矩形退化，指针无效 [待实测]。</para>
	///   <para><b>参数取向</b>1 返回 + 5 个 out，均按 INTEGER 装载（LoadI/LoadIP）；不产生新图像句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   IntPtr ptr = img.GetImagePointer1Rect(out int width, out int height,
	///       out int verticalPitch, out int horizontalBitPitch, out int bitsPerPixel);
	///   byte firstPixel = System.Runtime.InteropServices.Marshal.ReadByte(ptr);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>指针存活期受图像句柄约束（实现末尾 GC.KeepAlive(this) 只保证本次调用内不被释放）：任何原地改写 img 的 Gen*/Paint* 之后再解 ptr 都是悬挂读；Marshal.ReadByte 示例仅演示读取第一像素，逐行遍历须 ptr + n*verticalPitch。</para>
	/// </remarks>
	public IntPtr GetImagePointer1Rect(out int width, out int height, out int verticalPitch, out int horizontalBitPitch, out int bitsPerPixel)
	{
		IntPtr proc = JlNativeApi.PreCall(608);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out var intPtrValue);
		err = JlNativeApi.LoadI(proc, 1, err, out width);
		err = JlNativeApi.LoadI(proc, 2, err, out height);
		err = JlNativeApi.LoadI(proc, 3, err, out verticalPitch);
		err = JlNativeApi.LoadI(proc, 4, err, out horizontalBitPitch);
		err = JlNativeApi.LoadI(proc, 5, err, out bitsPerPixel);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intPtrValue;
	}

	/// <summary>按栈内每张图取彩色图像三通道指针与规格（元组版）。</summary>
	/// <param name="pointerRed">各图第一通道像素指针，INTEGER 元组。</param>
	/// <param name="pointerGreen">各图第二通道像素指针，INTEGER 元组。</param>
	/// <param name="pointerBlue">各图第三通道像素指针，INTEGER 元组。</param>
	/// <param name="type">各图类型字符串元组。</param>
	/// <param name="width">各图宽度元组。</param>
	/// <param name="height">各图高度元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 609。当前句柄是图像栈时，本重载为<b>每张图各占一个元素</b>返回六条元组（LoadNew，指针按 INTEGER 装载）；这是访问栈内第 2 张以后图像指针的唯一途径。</para>
	///   <para><b>约束或前提</b>图像须为三通道；单通道图调用失败 [待实测]。指针元素只能解引用到对应图像 Dispose 之前。</para>
	///   <para><b>与相邻算子的取舍</b>只关心栈中第一张图时用标量重载 <see cref="GetImagePointer3(out IntPtr,out IntPtr,out IntPtr,out string,out int,out int)"/>，免去六条元组的分配与固定开销；元组版适合批量导出到外部编解码缓冲。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage rgb = new JlImage("byte", 64, 64);
	///   rgb.GetImagePointer3(out JlTuple pr, out JlTuple pg, out JlTuple pb,
	///       out JlTuple type, out JlTuple width, out JlTuple height);
	///   </code>
	///   <para><b>资源与坑</b>示例中的构造仅示意调用形式，本算子要求图像确为三通道；解出的 INTEGER 指针值在原生侧不受引用计数保护，遍历前先读出 type/width/height 并换算每像素字节数，别越过缓冲末尾，且在图像 Dispose 后不得再用。</para>
	/// </remarks>
	public void GetImagePointer3(out JlTuple pointerRed, out JlTuple pointerGreen, out JlTuple pointerBlue, out JlTuple type, out JlTuple width, out JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(609);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out pointerRed);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out pointerGreen);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out pointerBlue);
		err = JlTuple.LoadNew(proc, 3, err, out type);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>取彩色图像（栈中第一张）三通道指针与规格的标量版。</summary>
	/// <param name="pointerRed">第一通道像素指针。</param>
	/// <param name="pointerGreen">第二通道像素指针。</param>
	/// <param name="pointerBlue">第三通道像素指针。</param>
	/// <param name="type">图像类型字符串。</param>
	/// <param name="width">图像宽度。</param>
	/// <param name="height">图像高度。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 609 的标量装载版：LoadIP/LoadS/LoadI 各只读原生输出的<b>第一个值</b>。</para>
	///   <para><b>与相邻算子的取舍</b>与元组重载的关键差异在此：当前句柄若含多张图，本重载会<b>静默丢弃第一张以外的全部结果</b>——不报错、不截断提示；需要逐张遍历栈时必须改用 <see cref="GetImagePointer3(out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>约束或前提</b>图像须为三通道，单通道图调用失败 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage rgb = new JlImage("byte", 64, 64);
	///   rgb.GetImagePointer3(out IntPtr pr, out IntPtr pg, out IntPtr pb,
	///       out string type, out int width, out int height);
	///   </code>
	///   <para><b>资源与坑</b>三个指针在 rgb 被原地改写或 Dispose 后立即失效；示例中的构造仅示意形式，真实调用前三通道前提必须成立。</para>
	/// </remarks>
	public void GetImagePointer3(out IntPtr pointerRed, out IntPtr pointerGreen, out IntPtr pointerBlue, out string type, out int width, out int height)
	{
		IntPtr proc = JlNativeApi.PreCall(609);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out pointerRed);
		err = JlNativeApi.LoadIP(proc, 1, err, out pointerGreen);
		err = JlNativeApi.LoadIP(proc, 2, err, out pointerBlue);
		err = JlNativeApi.LoadS(proc, 3, err, out type);
		err = JlNativeApi.LoadI(proc, 4, err, out width);
		err = JlNativeApi.LoadI(proc, 5, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按栈内每张图取单通道像素指针与规格（元组版）。</summary>
	/// <param name="type">各图类型字符串元组。</param>
	/// <param name="width">各图宽度元组（INTEGER）。</param>
	/// <param name="height">各图高度元组（INTEGER）。</param>
	/// <returns>各图像素指针组成的 INTEGER 元组，一张图一个元素；是新元组，不需释放句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 610。图像栈的每张图各占一个输出元素（LoadNew），是逐张访问栈内像素缓冲的唯一途径；type 按字符串装载、其余按 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>单通道图用它；三通道用 <see cref="GetImagePointer3(out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；需要域外接矩形布局参数（行距/位距）用 <see cref="GetImagePointer1Rect"/>；只关心第一张图时用标量重载更省。</para>
	///   <para><b>约束或前提</b>对彩色图调用会失败（原生要求 1 通道）[待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple ptrs = img.GetImagePointer1(out JlTuple type, out JlTuple width, out JlTuple height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的指针值只在 img 存活且未被原地改写期间有效；JlTuple 不实现 IDisposable，无释放负担但也不要跨线程长期持有指针。</para>
	/// </remarks>
	public JlTuple GetImagePointer1(out JlTuple type, out JlTuple width, out JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(610);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out type);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取栈中第一张单通道图的像素指针与规格（标量版）。</summary>
	/// <param name="type">图像类型字符串。</param>
	/// <param name="width">图像宽度。</param>
	/// <param name="height">图像高度。</param>
	/// <returns>指向像素数据的非托管指针（非新句柄，勿单独释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 610 的标量装载版：LoadIP/LoadS/LoadI 只读原生输出的<b>第一个值</b>。</para>
	///   <para><b>与相邻算子的取舍</b>与元组重载同 id，但句柄含多张图时本重载会<b>静默丢弃第一张以外的所有指针/规格</b>，批量遍历必须改用元组重载；单图取指针本重载免去元组分配。彩色图应使用 GetImagePointer3 族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   IntPtr ptr = img.GetImagePointer1(out string type, out int width, out int height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回指针的有效期止于 img 的 Dispose 或任何原地改写；type/width/height 与指针同源，遍历前先用它们核对缓冲长度。</para>
	/// </remarks>
	public IntPtr GetImagePointer1(out string type, out int width, out int height)
	{
		IntPtr proc = JlNativeApi.PreCall(610);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out var intPtrValue);
		err = JlNativeApi.LoadS(proc, 1, err, out type);
		err = JlNativeApi.LoadI(proc, 2, err, out width);
		err = JlNativeApi.LoadI(proc, 3, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intPtrValue;
	}

	/// <summary>取图像像素类型（栈内每张图一个元素）。</summary>
	/// <returns>类型字符串组成的新 JlTuple，如 "byte"/"int1"/"uint2"/"float"；不是句柄，无需释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 611，LoadNew 按字符串装载。彩色图的类型也只报一次（各通道类型一致），不含通道数信息——想知道通道数用 <see cref="CountChannels()"/>。</para>
	///   <para><b>与相邻算子的取舍</b>与 <see cref="GetImagePointer1(out string,out int,out int)"/> 相比不触像素缓冲，可在解引用前安全探测位深以决定步长。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("uint2", 64, 64);
	///   JlTuple types = img.GetImageType();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>uint2 图灰度上限 4095 而非 65535 [待实测]，按 255 阈值处理 uint2 图会整图判白，先查类型再定 Threshold 范围。</para>
	/// </remarks>
	public JlTuple GetImageType()
	{
		IntPtr proc = JlNativeApi.PreCall(611);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取栈内每张图像的宽与高（元组版，INTEGER 装载）。</summary>
	/// <param name="width">各图宽度元组。</param>
	/// <param name="height">各图高度元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 612 的元组版：句柄含 N 张图时输出 N 个元素的元组，用于核对 ConcatObj/TileImages 之后栈内各成员尺寸是否一致。</para>
	///   <para><b>与相邻算子的取舍</b>只要第一张图的尺寸时用标量重载 <see cref="GetImageSize(out int,out int)"/>，省两条元组分配；元组版是为批量一致性检查设计的。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 48);
	///   img.GetImageSize(out JlTuple width, out JlTuple height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是像素尺寸而非域尺寸：ReduceDomain 之后宽高不变，域范围要用 <see cref="GetDomain()"/>。</para>
	/// </remarks>
	public void GetImageSize(out JlTuple width, out JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(612);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>取栈中第一张图像的宽与高（标量版）。</summary>
	/// <param name="width">图像宽度（像素个数）。</param>
	/// <param name="height">图像高度（行数）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 612 的标量装载版：LoadI 只读第一个值。</para>
	///   <para><b>与相邻算子的取舍</b>句柄含多张图时本重载<b>静默丢弃第一张以外的尺寸</b>；批量核对尺寸请用元组重载 <see cref="GetImageSize(out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 48);
	///   img.GetImageSize(out int width, out int height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>width/height 是全幅像素尺寸，与域（domain）范围无关；坐标合法范围是 0..width-1 与 0..height-1（闭区间）。</para>
	/// </remarks>
	public void GetImageSize(out int width, out int height)
	{
		IntPtr proc = JlNativeApi.PreCall(612);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out width);
		err = JlNativeApi.LoadI(proc, 1, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>读取图像对象内记录的时间戳（毫秒为返回值，其余字段 out）。</summary>
	/// <param name="second">秒（0..59）。</param>
	/// <param name="minute">分（0..59）。</param>
	/// <param name="hour">时（0..23）。</param>
	/// <param name="day">当月日（1..31）。</param>
	/// <param name="YDay">当年日（1..366）。</param>
	/// <param name="month">月（1..12）。</param>
	/// <param name="year">四位年份。</param>
	/// <returns>毫秒（0..999），按 INTEGER 装载。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 613。时间戳由图像创建时的运行环境写入：本库已无 framegrabber 采集，Gen*/读文件所得图像通常即"本次创建时刻"，不要指望它反映真实曝光时间 [待实测]。</para>
	///   <para><b>参数取向</b>1 返回 + 7 个 out 共 8 个 INTEGER 输出；年月日与时分秒字段同时给出，day/YDay 二者信息重复，取任一即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   int ms = img.GetImageTime(out int second, out int minute, out int hour,
	///       out int day, out int YDay, out int month, out int year);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>对同一图像的多次读取不会变化；但任何原地改写（如 PaintRegion 直接画进 this）之后时间戳是否刷新为当前时刻 [待实测]，做帧序追踪请改用外部计数器而不是本方法。</para>
	/// </remarks>
	public int GetImageTime(out int second, out int minute, out int hour, out int day, out int YDay, out int month, out int year)
	{
		IntPtr proc = JlNativeApi.PreCall(613);
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
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadI(proc, 1, err, out second);
		err = JlNativeApi.LoadI(proc, 2, err, out minute);
		err = JlNativeApi.LoadI(proc, 3, err, out hour);
		err = JlNativeApi.LoadI(proc, 4, err, out day);
		err = JlNativeApi.LoadI(proc, 5, err, out YDay);
		err = JlNativeApi.LoadI(proc, 6, err, out month);
		err = JlNativeApi.LoadI(proc, 7, err, out year);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>在亚像素坐标处按指定插值取一组灰度值（元组版，结果为 DOUBLE）。</summary>
	/// <param name="row">采样点行坐标元组（double，可为亚像素）。Default: 0</param>
	/// <param name="column">采样点列坐标元组（double，可为亚像素）。Default: 0</param>
	/// <param name="interpolation">插值方法。Default: "bilinear"</param>
	/// <returns>各采样点灰度值组成的新 JlTuple，按 DOUBLE 装载；多点多通道时元素数会成倍增加 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 614。像素中心位于整数坐标 (row, column)；给出非整坐标时由 interpolation 决定邻域混合，结果保留小数——与 <see cref="GetGrayval(JlTuple,JlTuple)"/> 只取整数像素、结果按像素类型取整不同。</para>
	///   <para><b>约束或前提</b>row/column 必须等长；坐标越出图像或邻域触及域外时该点结果的约定 [待实测]，采样前应自行裁剪坐标。</para>
	///   <para><b>与相邻算子的取舍</b>批量曲线采样用本元组重载（一次钉住两个元组、一次调用）；单点探测用标量重载省去 UnpinTuple 固定开销。要精确像素值而不要混合值时必须用 GetGrayval，用双线性会在边缘处引入半值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple vals = img.GetGrayvalInterpolated(new double[] { 10.5, 20.0 },
	///       new double[] { 30.0, 40.25 }, "bilinear");
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>元组重载调用中 row/column 被钉住、调用后 UnpinTuple；返回 DOUBLE 意味着 byte 图也会得到 128.75 这类值，做等值比较前想清楚取整策略。</para>
	/// </remarks>
	public JlTuple GetGrayvalInterpolated(JlTuple row, JlTuple column, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(614);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在单个亚像素坐标处插值取灰度值（标量版）。</summary>
	/// <param name="row">采样点行坐标（double，可为亚像素）。Default: 0</param>
	/// <param name="column">采样点列坐标（double，可为亚像素）。Default: 0</param>
	/// <param name="interpolation">插值方法。Default: "bilinear"</param>
	/// <returns>该点插值灰度值，按 DOUBLE 装载（LoadD 只读原生输出的第一个值）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 614 的标量版：StoreD 直写单值，无元组钉住/解固定开销，与元组重载同 id。</para>
	///   <para><b>与相邻算子的取舍</b>多点批量采样不要循环调用本重载（每点一次原生调用），应改用元组重载一次取回；只要整数像素原值时用 <see cref="GetGrayval(int,int)"/>。</para>
	///   <para><b>约束或前提</b>像素中心在整数坐标；坐标越界或邻域触域外的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   double g = img.GetGrayvalInterpolated(10.5, 20.0, "bilinear");
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>若图像为多通道，原生输出含逐通道多个值，本重载只回第一个通道值——其余通道被静默丢弃；需要全部通道值时用元组重载。</para>
	/// </remarks>
	public double GetGrayvalInterpolated(double row, double column, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(614);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>取一批整数坐标像素的灰度值（元组版）。</summary>
	/// <param name="row">各采样点行坐标（整数）元组。Default: 0</param>
	/// <param name="column">各采样点列坐标（整数）元组。Default: 0</param>
	/// <returns>各点（及多通道图的各通道）灰度值组成的新 JlTuple。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 615。不做任何插值/取整换算，返回的就是像素存储值；对多通道图，每点会按通道连续给出多个值 [待实测]。</para>
	///   <para><b>约束或前提</b>坐标必须是落在图内且在域内的整数点，越界或触域外时原生报错而非返回缺省值 [待实测]；row/column 等长。</para>
	///   <para><b>与相邻算子的取舍</b>亚像素位置要用 <see cref="GetGrayvalInterpolated(JlTuple,JlTuple,string)"/>；本元组重载适合掩模抽检等批量点位。与标量 int 重载同 id，差异仅在元组钉住（Store+UnpinTuple）与 StoreI 直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple g = img.GetGrayval(new int[] { 10, 20 }, new int[] { 30, 40 });
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>float 图的返回值可能带小数（DOUBLE 元组），byte 图则是整数值；对返回值做元素运算前先看 GetImageType。</para>
	/// </remarks>
	public JlTuple GetGrayval(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(615);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取单个整数坐标像素的灰度值（标量版，返回仍是元组）。</summary>
	/// <param name="row">像素行坐标。Default: 0</param>
	/// <param name="column">像素列坐标。Default: 0</param>
	/// <returns>该像素灰度值元组：单通道图只有一个元素，多通道图按通道给出多个元素 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 615 的标量版：StoreI 直写坐标，无元组钉住开销。注意返回类型不是 double/int 而是 JlTuple——因为一个点在彩色图上对应多个通道值，签名故意不做截断。</para>
	///   <para><b>与相邻算子的取舍</b>多点批量用元组重载 <see cref="GetGrayval(JlTuple,JlTuple)"/>（一次调用换 N 点）；坐标非整数用 GetGrayvalInterpolated 族。</para>
	///   <para><b>约束或前提</b>坐标越界或在域外时原生侧行为为错误而非零值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple gray = img.GetGrayval(10, 20);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>调用方常误以为返回单值而直接强转；应取元组首元素并自行判长度。本重载不会像某些 LoadI/LoadD 标量重载那样丢多值——它原样带回整条元组。</para>
	/// </remarks>
	public JlTuple GetGrayval(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(615);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}








	/// <summary>以域的外接矩形为基准四边各裁去指定行数，返回裁剪后的新图像句柄。</summary>
	/// <param name="top">上边裁掉的行数。Default: -1</param>
	/// <param name="left">左边裁掉的列数。Default: -1</param>
	/// <param name="bottom">下边裁掉的行数。Default: -1</param>
	/// <param name="right">右边裁掉的列数。Default: -1</param>
	/// <returns>裁剪结果的新图像句柄（LoadNew），需释放；当前对象不受影响。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 705。基准是<b>域</b>的外接矩形而不是全幅：先取域包围盒，再按四个参数收缩，输出图的宽高即收缩后的矩形尺寸。</para>
	///   <para><b>约束或前提</b>-1 表示该边不裁；裁缩量超过包围盒一半导致宽高退化为 0 时的行为 [待实测]。域为空时无法定基准 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要按绝对坐标裁剪用 CropRectangle1/CropPart；要"贴紧域"一刀切边用无参 CropDomain；本算子适合在域上再去掉毛边（例如 GenRectangle 后各缩 2 像素避开羽化）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dom = img.Threshold(1.0, 255.0);
	///   using JlImage trimmed = img.ReduceDomain(dom).CropDomainRel(2, 2, 2, 2);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；像素保留、域被裁小后若还想统计"原域"要先算再裁。输出图的域约定（全幅矩形还是原域平移）[待实测]。</para>
	/// </remarks>
	public JlImage CropDomainRel(int top, int left, int bottom, int right)
	{
		IntPtr proc = JlNativeApi.PreCall(705);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, top);
		JlNativeApi.StoreI(proc, 1, left);
		JlNativeApi.StoreI(proc, 2, bottom);
		JlNativeApi.StoreI(proc, 3, right);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}








	/// <summary>灰度黑帽：闭运算结果减去原图，突出比邻域暗的窄谷。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>黑帽图像的新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 759。黑帽 = <see cref="GrayClosing(JlImage)"/> − 原图，
	///   保留"比周围低、且宽度小于 SE"的暗结构：划痕、凹坑、压印缺口。SE 的尺寸/灰度取法与顶帽完全对称，
	///   共用一套注意事项，见 <see cref="GrayTophat(JlImage)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>目标比背景<b>亮</b>时用 <see cref="GrayTophat(JlImage)"/>；
	///   想把暗结构直接抹平而不是提出来，用 <c>GrayClosing</c>。
	///   不要用"先 <c>InvertImage</c> 再顶帽"来代替黑帽：反相会改变后续阈值的量纲含义，还得再反回来。</para>
	///   <para><b>输出</b>是图像，且黑帽图的灰度以"0 = 无谷"为基准，背景被压平到接近 0，
	///   因此阈值下限通常取几而不是 128 [待实测：byte 输出是否含偏置]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 9, 9, 30.0);
	///   using JlImage valleys = img.GrayBothat(se);
	///   using JlRegion scratches = valleys.Threshold(15.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b><paramref name="SE"/> 只读；返回新句柄需释放。</para>
	/// </remarks>
	public JlImage GrayBothat(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(759);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度顶帽：原图减去开运算结果，突出比邻域亮的窄峰（输出图像）。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>顶帽图像的新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 760。英文说明 bottom/top hat 成对：顶帽 = 原图 − <see cref="GrayOpening(JlImage)"/>，
	///   留下的是"比周围高、且宽度小于 SE"的亮结构（划痕亮点、灰尘、字符笔画），背景趋势被抵消掉。
	///   反过来的 <see cref="GrayBothat(JlImage)"/> 用闭运算提取暗谷。</para>
	///   <para><b>SE 是图像不是区域</b><paramref name="SE"/> 走第 3 个槽位（<c>Store(proc, 2, SE)</c>），
	///   它带灰度值：SE 的<b>尺寸</b>决定多宽的结构会被当作"背景"保留、多窄的会被顶帽留下，SE 的<b>灰度峰值</b>
	///   （见 <c>GenDiscSe</c> 的 <c>smax</c>）决定多高的峰才够格。所以 SE 要给"刚好比目标宽一点、比目标高一点"的帽状体，
	///   给大了目标整体被当背景吃掉，给小了噪声全留下。</para>
	///   <para><b>与相邻算子的取舍</b>只要区域级别的去毛刺/断开粘连，用 <see cref="JlRegion"/> 上的二值形态学，不要用本族：
	///   本族<b>输出仍是图像</b>（<c>LoadNew(proc,1,...)</c> 取 <c>JlImage</c>），接成 <c>JlRegion</c> 是常见错误，
	///   灰度结果通常还要再 <c>Threshold</c> 才能统计。想按固定矩形/圆盘 SE 做同类滤波，用更省事的
	///   <see cref="GrayOpeningRect(int,int)"/>/<see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>，不必自己造 SE 图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();               // 无参构造：未初始化句柄
	///   se.GenDiscSe("byte", 7, 7, 40.0);              // 原地生成帽状灰度结构元
	///   using JlImage peaks = img.GrayTophat(se);      // 只剩比局部背景高的窄亮结构
	///   using JlRegion dust = peaks.Threshold(10.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新图像句柄需释放；<paramref name="SE"/> 只读，调用结束前由
	///   <c>GC.KeepAlive</c> 保命，本层不检查 SE 尺寸是否为奇数、是否比图像还大 [待实测]。
	///   多通道图上本族的通道行为本层未体现 [待实测]。</para>
	/// </remarks>
	public JlImage GrayTophat(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(760);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度闭运算（先膨胀后腐蚀）：填掉比 SE 暗且窄的谷。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>闭运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 761，在灰度上先 <see cref="GrayDilation(JlImage)"/> 后 <see cref="GrayErosion(JlImage)"/>，
	///   得到原图的上包络：窄而深的暗谷（文字笔画断口、表面麻点、暗划痕）被抬到邻近水平，宽谷不受影响。</para>
	///   <para><b>与相邻算子的取舍</b>开运算治亮毛刺、闭运算治暗毛刺，两者方向相反，不可互相顶替；
	///   要把暗谷<b>提取</b>出来而不是抹平，用 <see cref="GrayBothat(JlImage)"/>。
	///   只要按矩形窗做闭运算，用 <see cref="GrayClosingRect(int,int)"/>，不需要造 SE 图。</para>
	///   <para><b>坑</b>闭运算会抬高灰度：对被处理区域做灰度测量（平均灰度、<c>Intensity</c>）的结果会系统性偏亮，
	///   把它接在灰度统计前必须说明；SE 灰度峰值 <c>smax</c> 给多大，最多就把谷抬多深，超过部分不会继续填。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 5, 5, 20.0);
	///   using JlImage filled = img.GrayClosing(se);         // 暗麻点被抬平
	///   using JlRegion reg = filled.Threshold(0.0, 80.0);
	///   </code>
	///   <para><b>资源与坑</b>SE 只读；输出为新句柄。</para>
	/// </remarks>
	public JlImage GrayClosing(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(761);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度开运算（先腐蚀后膨胀）：削掉比 SE 亮且窄的峰，保留整体灰度趋势。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>开运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 762，在灰度上先 <see cref="GrayErosion(JlImage)"/> 后 <see cref="GrayDilation(JlImage)"/>。
	///   结果是原图的下包络：高于局部背景、且宽度或高度不足以放下 SE 的亮峰被削平，其余像素值基本不动。</para>
	///   <para><b>与 <c>GrayTophat</c> 的分界</b>要"去掉亮毛刺、继续做灰度统计"→ 本算子；
	///   要"把亮毛刺单独拿出来做检测"→ <see cref="GrayTophat(JlImage)"/>。
	///   两者常被混用：顶帽的输出量纲已经是残差（背景≈0），拿它当"滤波后的图"再 <c>Threshold(128,255)</c> 会什么都分不出来。</para>
	///   <para><b>SE 取法与副作用</b>SE 比目标宽才会削到目标，因此本算子不能用于"目标可能很小"的场合；
	///   它会<b>不可逆地改变</b>被削区域的灰度，后面若还要原始灰度测量，先在这一步之前存图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 5, 5, 10.0);
	///   using JlImage cleaned = img.GrayOpening(se);        // 削平亮毛刺，保留低频
	///   using JlRegion reg = cleaned.Threshold(100.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>SE 为图像输入、只读；输出类型与输入类型的关系本层未体现 [待实测]。</para>
	/// </remarks>
	public JlImage GrayOpening(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(762);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度膨胀：邻域内按 SE 取值后的逐点最大，亮区扩张且整体变亮。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>膨胀后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 763。与二值膨胀的区别在于 SE 带灰度：结果是把邻域灰度按 SE 偏移后取最大，
	///   所以亮结构变大、暗结构被吃掉，并且<b>整幅图的灰度只会升不会降</b>。</para>
	///   <para><b>坑：饱和与量纲</b>在 <c>byte</c> 图上，接近 255 的区域再膨胀会被压在上限附近 [待实测：截断还是回绕]，
	///   后续做 <c>Threshold</c> 或 <c>GrayHisto</c> 时直方图会在高端堆出一个尖峰；需要严格可加的量（高度图、灰度测量）
	///   应改用 <c>float</c> 类型（<c>ConvertImageType("float")</c>）再膨胀。</para>
	///   <para><b>与相邻算子的取舍</b>只是想让亮区变宽、形状变圆，用 <see cref="GrayDilationRect(int,int)"/> 免去造 SE；
	///   想把膨胀参与"上包络/闭运算"链条，直接用 <see cref="GrayClosing(JlImage)"/> 而不是自己串两步，
	///   两步之间会多一次中间图的分配。做减背景请配 <see cref="GrayErosion(JlImage)"/>，两者不成对使用时结果会整体偏移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 5, 5, 0.0);                    // 平顶 SE：只扩形状不改灰度偏移
	///   using JlImage grown = img.GrayDilation(se);
	///   </code>
	///   <para><b>资源与坑</b>SE 只读、输出新句柄；图像边缘处的取值方式本层未体现 [待实测]。</para>
	/// </remarks>
	public JlImage GrayDilation(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(763);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度腐蚀：邻域内按 SE 取值后的逐点最小，暗区扩张且整体变暗。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>腐蚀后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 764，与 <see cref="GrayDilation(JlImage)"/> 互为对偶（取最小而非最大）：
	///   亮结构变窄、暗背景被抬高，<b>整幅图的灰度只会降不会升</b>。用它可以估出局部背景的下界，
	///   再配合 <c>SubImage</c> 得到扣除背景后的图。</para>
	///   <para><b>坑</b>窄亮目标在腐蚀后可能整体掉到接近背景值，"腐蚀后再阈值"会稳定漏检小目标；
	///   <c>byte</c> 图低端同理存在截断风险 [待实测]。与膨胀一样，边缘像素处理本层不体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>固定矩形窗用 <see cref="GrayErosionRect(int,int)"/>；
	///   目标是"先腐蚀后膨胀"的滤波就写 <see cref="GrayOpening(JlImage)"/>，别手拼两步。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 9, 9, 0.0);
	///   using JlImage background = img.GrayErosion(se);          // 背景（暗）估计
	///   using JlImage residual = img.SubImage(background, 1.0, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>SE 只读；输出新句柄。</para>
	/// </remarks>
	public JlImage GrayErosion(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(764);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>从文件读入灰度形态学结构元，原地改写当前句柄。</summary>
	/// <param name="fileName">存放结构元的文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 765。方法体先 Dispose 再 Load：当前 JlImage 句柄被改写为读入的结构元图（SE 就是带灰度的小图），不返回新对象。</para>
	///   <para><b>与相邻算子的取舍</b>SE 形状能用现成生成器造出来时优先 <see cref="GenDiscSe(string,int,int,double)"/>（圆盘/椭球帽），本方法专用于把外场标定的自定义 SE 落盘复用；与二值区域形态学的 SE 不通用——这里读进来的 SE 带灰度值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage se = new JlImage();
	///   se.ReadGraySe("E:/se/cap8.dat");
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage hat = img.GrayTophat(se);
	///   </code>
	///   <para><b>资源与坑</b>文件不存在或格式不符时原生报错、当前句柄已被 Dispose 处于未初始化态，需要重新 Gen；SE 用于 Gray* 族前请确认其 smax 峰值与检测目标灰度差匹配。</para>
	/// </remarks>
	public void ReadGraySe(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(765);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成帽状（椭球/圆盘）灰度结构元，原地改写当前句柄（smax 元组版）。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">结构元宽度。Default: 5</param>
	/// <param name="height">结构元高度。Default: 5</param>
	/// <param name="smax">结构元中心最大灰度值（元组，调用期间被钉住）。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 766。SE 为从中心 smax 向边缘衰减到 0 的帽状灰度图，供 <see cref="GrayTophat(JlImage)"/>/<see cref="GrayClosing(JlImage)"/> 等灰度形态学使用；方法体先 Dispose 再 Load，原地改写当前句柄。</para>
	///   <para><b>与相邻算子的取舍</b>与 double 重载同 id：元组版走 Store+UnpinTuple（钉住元组），仅当需要以元组形式批量传递 smax 或与其它元组逻辑复用时有意义，常规单值请直接传 double。smax=0 得到平顶 SE（只改形状不改灰度基准），smax&gt;0 才形成"帽高"，决定多深的谷会被填、多高的峰够格被顶帽提取。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage se = new JlImage();
	///   JlTuple smax = 30.0;
	///   se.GenDiscSe("byte", 9, 9, smax);
	///   </code>
	///   <para><b>资源与坑</b>示例传入字面量经隐式转换落到元组重载/标量重载均可；宽高应给奇数保证中心像素存在 [待实测]。</para>
	/// </remarks>
	public void GenDiscSe(string type, int width, int height, JlTuple smax)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(766);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.Store(proc, 3, smax);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(smax);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成帽状（椭球/圆盘）灰度结构元，原地改写当前句柄（smax 标量版）。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">结构元宽度。Default: 5</param>
	/// <param name="height">结构元高度。Default: 5</param>
	/// <param name="smax">结构元中心最大灰度值。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 766 的标量版：StoreD 直写 smax，无元组钉住/解固定开销；生成帽状灰度 SE（中心 smax、边缘 0），供 <see cref="GrayOpening(JlImage)"/>/<see cref="GrayBothat(JlImage)"/> 等灰度形态学使用。方法体先 Dispose 再 Load，<b>原地改写</b>当前句柄。</para>
	///   <para><b>与相邻算子的取舍</b>SE 需从文件复用时改用 <see cref="ReadGraySe(string)"/>；矩形平顶 SE 可直接用 GrayOpeningRect 族而不必造 SE 图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 7, 7, 40.0);
	///   </code>
	///   <para><b>资源与坑</b>smax=0 是平顶 SE：灰度形态学退化为"只看形状不看高度"；帽高大于待测峰高时顶帽会把目标整体当背景吃掉（详见 GrayTophat 的 SE 取法）。</para>
	/// </remarks>
	public void GenDiscSe(string type, int width, int height, double smax)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(766);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreD(proc, 3, smax);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>沿卡尺轮廓找出灰度等于给定阈值的点（亚像素，元组输出）。</summary>
	/// <param name="measureHandle">已生成的 JlMeasure 卡尺句柄（矩形/圆弧均可）。</param>
	/// <param name="sigma">提取前对轮廓做高斯平滑的 sigma。Default: 1.0</param>
	/// <param name="threshold">要提取的灰度值。Default: 128.0</param>
	/// <param name="select">交点选取方式（全部/首/末）。Default: "all"</param>
	/// <param name="rowThresh">交点行坐标（DOUBLE 元组）。</param>
	/// <param name="columnThresh">交点列坐标（DOUBLE 元组）。</param>
	/// <param name="distance">各交点沿轮廓距起点的距离（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 803。在本图像上按 measureHandle 定义的轮廓采样，再取灰度恰等于 threshold 的亚像素交点；三条输出一一对应、均按 DOUBLE 装载（LoadNew）。</para>
	///   <para><b>约束或前提</b>图像与 measure 生成时传入的 width/height 必须一致，否则卡尺越出图像；threshold 对 byte 图取值 0..255 且必须落在轮廓实际灰度范围内才有交点——轮廓从未到过该灰度时输出为空元组而非报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要的是"边缘"（灰度跳变）用 <see cref="MeasurePos"/>/<see cref="MeasurePairs"/>（基于导数与幅值），本算子提取的是"等灰度线交点"（如液面高度、印刷灰度线），两者不可互替；sigma 越大交点越稳但会钝化、位置系统偏移 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.MeasureThresh(caliper, 1.0, 128.0, "all",
	///       out JlTuple rowThresh, out JlTuple columnThresh, out JlTuple distance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlMeasure 经 JlObjectBase 实现 IDisposable（另有 CloseMeasure），用完必须关闭；measureHandle 在原生调用结束前不得释放（实现末尾 GC.KeepAlive 佐证）。本层不缓存采样结果，换图重调即可复用同一卡尺。</para>
	/// </remarks>
	public void MeasureThresh(JlMeasure measureHandle, double sigma, double threshold, string select, out JlTuple rowThresh, out JlTuple columnThresh, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(803);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreS(proc, 3, select);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowThresh);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnThresh);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out distance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺提取垂直于矩形/圆弧方向的原始灰度剖面（一条 DOUBLE 元组）。</summary>
	/// <param name="measureHandle">已生成的 JlMeasure 卡尺句柄。</param>
	/// <returns>灰度剖面元组，按 DOUBLE 装载；是新元组，不需释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 805。不做任何边缘评估，只把卡尺内逐条扫描线的灰度值原样铺出来，供自写峰值/过零检测使用。</para>
	///   <para><b>约束或前提</b>剖面元素的排列顺序（先扫描线后采样点，或反之）本层未体现 [待实测]；卡尺生成时的 interpolation 决定采样点是整数还是插值灰度。图像尺寸须与卡尺登记的 width/height 一致。</para>
	///   <para><b>与相邻算子的取舍</b>要现成的边缘坐标/配对宽度，直接用 <see cref="MeasurePos"/> 或 <see cref="MeasurePairs"/>，不要拿本算子的剖面手搓；本算子适合教学调试卡尺参数或对剖面做自定义滤波后再判读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   JlTuple profile = img.MeasureProjection(caliper);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>剖面平滑建议先对图做 <c>SmoothImage</c> 而不是事后对元组差分平均——后者会破坏采样点与坐标的对应关系。</para>
	/// </remarks>
	public JlTuple MeasureProjection(JlMeasure measureHandle)
	{
		IntPtr proc = JlNativeApi.PreCall(805);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
		return tuple;
	}

	/// <summary>沿卡尺做带模糊评分的边缘配对，限制配对数量与配对方式（元组输出）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="ampThresh">最小边缘幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">最小模糊隶属度。Default: 0.5</param>
	/// <param name="transition">边缘对首边的灰度跳变方向。Default: "all"</param>
	/// <param name="pairing">配对约束方式。Default: "no_restriction"</param>
	/// <param name="numPairs">最多输出的边缘对个数。Default: 10</param>
	/// <param name="rowEdgeFirst">第一边缘行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdgeFirst">第一边缘列坐标。</param>
	/// <param name="amplitudeFirst">第一边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二边缘行坐标。</param>
	/// <param name="columnEdgeSecond">第二边缘列坐标。</param>
	/// <param name="amplitudeSecond">第二边缘幅值（带符号）。</param>
	/// <param name="rowPairCenter">边缘对中点行坐标。</param>
	/// <param name="columnPairCenter">边缘对中点列坐标。</param>
	/// <param name="fuzzyScore">该边缘对的模糊隶属度。</param>
	/// <param name="intraDistance">对内两边缘间距。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 809。与 <see cref="FuzzyMeasurePairs"/> 同族但多出 pairing/numPairs 控制：pairing 决定相邻边缘如何成对、numPairs 限制输出对数（取值集合本层未体现 [待实测]）；本方法输出没有 interDistance。</para>
	///   <para><b>约束或前提</b>10 条输出全部 DOUBLE 装载、逐条一一对应；ampThresh 是对比度门槛，byte 图上给 30 表示灰度跳变至少 30 级；fuzzyScore 越大配对越可信，用它可以按置信度二次筛。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.FuzzyMeasurePairing(caliper, 1.0, 30.0, 0.5, "all", "no_restriction", 10,
	///       out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///       out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///       out JlTuple rowPairCenter, out JlTuple columnPairCenter,
	///       out JlTuple fuzzyScore, out JlTuple intraDistance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>numPairs 截断的是输出条数，边缘对按轮廓推进顺序排列；下游若按"第 k 对对应第 k 条扫描线"理解会静默错位，需要坐标回查时用 rowPairCenter/columnPairCenter。</para>
	/// </remarks>
	public void FuzzyMeasurePairing(JlMeasure measureHandle, double sigma, double ampThresh, double fuzzyThresh, string transition, string pairing, int numPairs, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple rowPairCenter, out JlTuple columnPairCenter, out JlTuple fuzzyScore, out JlTuple intraDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(809);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, ampThresh);
		JlNativeApi.StoreD(proc, 3, fuzzyThresh);
		JlNativeApi.StoreS(proc, 4, transition);
		JlNativeApi.StoreS(proc, 5, pairing);
		JlNativeApi.StoreI(proc, 6, numPairs);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		JlNativeApi.InitOCT(proc, 9);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdgeFirst);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdgeFirst);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitudeFirst);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out rowEdgeSecond);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out columnEdgeSecond);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out amplitudeSecond);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out rowPairCenter);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out columnPairCenter);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out fuzzyScore);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out intraDistance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺做带模糊评分的边缘配对（元组输出，含对间距与相邻对间距）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="ampThresh">最小边缘幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">最小模糊隶属度。Default: 0.5</param>
	/// <param name="transition">边缘对首边的灰度跳变方向。Default: "all"</param>
	/// <param name="rowEdgeFirst">第一边缘点行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdgeFirst">第一边缘点列坐标。</param>
	/// <param name="amplitudeFirst">第一边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二边缘点行坐标。</param>
	/// <param name="columnEdgeSecond">第二边缘点列坐标。</param>
	/// <param name="amplitudeSecond">第二边缘幅值（带符号）。</param>
	/// <param name="rowEdgeCenter">边缘对中点行坐标。</param>
	/// <param name="columnEdgeCenter">边缘对中点列坐标。</param>
	/// <param name="fuzzyScore">边缘对的模糊隶属度。</param>
	/// <param name="intraDistance">对内两边缘间距（目标宽度）。</param>
	/// <param name="interDistance">相邻边缘对之间的间距（目标节距）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 810。测宽+测距一体：intraDistance 给"每根目标有多宽"，interDistance 给"目标之间的空隙有多宽"，适合等间距条纹/引脚节距测量。</para>
	///   <para><b>与相邻算子的取舍</b>与 <see cref="FuzzyMeasurePairing"/> 的差别是它可按对数截断、本方法不可以；与硬阈值的 <see cref="MeasurePairs"/> 相比，本方法以 fuzzyThresh 隶属度筛对，噪声场景更稳但多一条 fuzzyScore 输出需要自己设线。测量单边缘用 FuzzyMeasurePos 即可，别用配对算子凑。</para>
	///   <para><b>约束或前提</b>11 条输出全部 DOUBLE 装载、按扫描线推进顺序一一对应；transition 选 "positive" 还是 "negative" 决定只配"暗→亮"或"亮→暗"起步的对（关键词与方向的对应关系 [待实测]），选错方向会整批漏配。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.FuzzyMeasurePairs(caliper, 1.0, 30.0, 0.5, "all",
	///       out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///       out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///       out JlTuple rowEdgeCenter, out JlTuple columnEdgeCenter,
	///       out JlTuple fuzzyScore, out JlTuple intraDistance, out JlTuple interDistance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原英文备注的"相关算子 GenMeasureRectangle2、CloseMeasure"仍在 JlMeasure 侧存在；卡尺用完记得关闭，句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FuzzyMeasurePairs(JlMeasure measureHandle, double sigma, double ampThresh, double fuzzyThresh, string transition, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple rowEdgeCenter, out JlTuple columnEdgeCenter, out JlTuple fuzzyScore, out JlTuple intraDistance, out JlTuple interDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, ampThresh);
		JlNativeApi.StoreD(proc, 3, fuzzyThresh);
		JlNativeApi.StoreS(proc, 4, transition);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		JlNativeApi.InitOCT(proc, 9);
		JlNativeApi.InitOCT(proc, 10);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdgeFirst);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdgeFirst);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitudeFirst);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out rowEdgeSecond);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out columnEdgeSecond);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out amplitudeSecond);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out rowEdgeCenter);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out columnEdgeCenter);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out fuzzyScore);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out intraDistance);
		err = JlTuple.LoadNew(proc, 10, JlTupleType.DOUBLE, err, out interDistance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺提取带模糊评分的单边缘点集（不做配对）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="ampThresh">最小边缘幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">最小模糊隶属度。Default: 0.5</param>
	/// <param name="transition">保留的跳变方向（亮暗向）。Default: "all"</param>
	/// <param name="rowEdge">边缘点行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdge">边缘点列坐标。</param>
	/// <param name="amplitude">边缘幅值（带符号，符号即跳变方向）。</param>
	/// <param name="fuzzyScore">各边缘的模糊隶属度。</param>
	/// <param name="distance">相邻（连续）边缘点之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 811。Fuzzy 族的"单边缘"版：每条扫描线上留下的是一串独立边缘点及其置信度，5 条输出均 DOUBLE 装载。</para>
	///   <para><b>与相邻算子的取舍</b>目标是一根线/一条轮廓边界时用本方法或 <see cref="MeasurePos"/>；目标是"宽度"才用 FuzzyMeasurePairs 族。与硬阈值 MeasurePos 相比，本方法能顺带给出 fuzzyScore 供按置信度筛点，代价是多一个参数要调（fuzzyThresh 定太低会把噪声边缘全放进来）。</para>
	///   <para><b>约束或前提</b>ampThresh 以灰度量纲计；图像类型是 uint2 时 30 的门槛含义与 byte 完全不同，先换算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.FuzzyMeasurePos(caliper, 1.0, 30.0, 0.5, "all",
	///       out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude,
	///       out JlTuple fuzzyScore, out JlTuple distance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>distance 的基准（相邻点间距还是到轮廓起点距离）本层未体现 [待实测]，画回图上时一律以 rowEdge/columnEdge 为准；卡尺句柄在原生调用结束前不得释放。</para>
	/// </remarks>
	public void FuzzyMeasurePos(JlMeasure measureHandle, double sigma, double ampThresh, double fuzzyThresh, string transition, out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple fuzzyScore, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(811);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, ampThresh);
		JlNativeApi.StoreD(proc, 3, fuzzyThresh);
		JlNativeApi.StoreS(proc, 4, transition);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdge);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdge);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitude);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out fuzzyScore);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out distance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺做硬阈值边缘配对，按 transition/select 筛选（元组输出）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="threshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="transition">决定如何把边缘归组成对的灰度跳变类型。Default: "all"</param>
	/// <param name="select">边缘对选取（全部/首末等）。Default: "all"</param>
	/// <param name="rowEdgeFirst">第一边缘中心行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdgeFirst">第一边缘中心列坐标。</param>
	/// <param name="amplitudeFirst">第一边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二边缘中心行坐标。</param>
	/// <param name="columnEdgeSecond">第二边缘中心列坐标。</param>
	/// <param name="amplitudeSecond">第二边缘幅值（带符号）。</param>
	/// <param name="intraDistance">对内两边缘间距（目标宽度）。</param>
	/// <param name="interDistance">相邻边缘对间距（目标节距）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 812。经典硬阈值配对：边缘以导数过零+幅值 threshold 判定，成对由 transition 规定首边方向；8 条输出均 DOUBLE 装载。</para>
	///   <para><b>与相邻算子的取舍</b>没有置信度可用、也不接受模糊隶属度参数——需要按 fuzzyScore 筛对时改 <see cref="FuzzyMeasurePairs"/>；只需要单条边缘宽度序列时 <see cref="MeasurePos"/> 更轻。select 用 "first"/"last" 时输出条数骤减，务必确认卡尺起点方向与工艺约定一致，否则拿到的是"另一端"的边。</para>
	///   <para><b>约束或前提</b>threshold 与图像灰度量纲一致；轮廓未出现指定 transition 方向的对时输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.MeasurePairs(caliper, 1.0, 30.0, "all", "all",
	///       out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///       out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///       out JlTuple intraDistance, out JlTuple interDistance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本方法不输出对中点坐标（Fuzzy 族有），要"目标中心"需自行取 first/second 均值；卡尺句柄在调用结束前不得释放。</para>
	/// </remarks>
	public void MeasurePairs(JlMeasure measureHandle, double sigma, double threshold, string transition, string select, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple intraDistance, out JlTuple interDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(812);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreS(proc, 3, transition);
		JlNativeApi.StoreS(proc, 4, select);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdgeFirst);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdgeFirst);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitudeFirst);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out rowEdgeSecond);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out columnEdgeSecond);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out amplitudeSecond);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out intraDistance);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out interDistance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺提取硬阈值单边缘点集（最常用的卡尺测边算子）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="threshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="transition">保留亮→暗或暗→亮方向的边缘。Default: "all"</param>
	/// <param name="select">端点选取（全部/首/末）。Default: "all"</param>
	/// <param name="rowEdge">边缘中心行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdge">边缘中心列坐标。</param>
	/// <param name="amplitude">边缘幅值（带符号，符号表示跳变方向）。</param>
	/// <param name="distance">连续边缘之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 813。对每条扫描线做一维高斯导数卷积，幅值过 threshold 者成为亚像素边缘点；4 条输出均 DOUBLE 装载，按扫描线推进顺序一一对应。</para>
	///   <para><b>与相邻算子的取舍</b>需要置信度筛选用 <see cref="FuzzyMeasurePos"/>；要宽度用 <see cref="MeasurePairs"/>；要"灰度恰好等于某值"的点用 <see cref="MeasureThresh"/>。本方法无评分输出，噪声图上调 threshold 往往不够，还要配合 sigma（sigma 加大会钝化并平移边缘 [待实测]）。</para>
	///   <para><b>约束或前提</b>threshold 与灰度量纲一致；select="first"/"last" 的"首末"以卡尺局部坐标方向为准，卡尺角度摆反会选中工件另一侧边缘且无任何报错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.MeasurePos(caliper, 1.0, 30.0, "all", "all",
	///       out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多张扫描线共用一次调用：结果把各线边缘串接在一起，不给出"属于哪条线"的索引，需按每条线最大边缘数自行切分或改用逐线测量 [待实测]；卡尺句柄在调用结束前不得释放。</para>
	/// </remarks>
	public void MeasurePos(JlMeasure measureHandle, double sigma, double threshold, string transition, string select, out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(813);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreS(proc, 3, transition);
		JlNativeApi.StoreS(proc, 4, select);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdge);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdge);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitude);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out distance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>在本模板图上试算形状模型的自动化参数（元组版，可传 "auto"）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleMin">最小缩放或 "auto"。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放或 "auto"。Default: 1.1</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（或滞后双阈值+最小尺寸）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度。Default: "auto"</param>
	/// <param name="parameters">要自动确定的参数名集合。Default: "all"</param>
	/// <param name="parameterValue">与返回的参数名一一对应的建议值（新元组）。</param>
	/// <returns>被自动确定的参数名元组，如 "num_levels"、"contrast"。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 880。它<b>不创建模型</b>，只回答"若按这些取值建 CreateShapeModel 会自动定出哪些参数、定成多少"，用于把确定后的值原样喂给 CreateShapeModel 复现建模型过程。九个元组形参全程钉住、调用后逐个 UnpinTuple。</para>
	///   <para><b>约束或前提</b>当前对象必须是已裁好域的模板图：建议先 ReduceDomain 把目标圈进域内再调用，否则 contrast 的 "auto" 会按整图噪声估计 [待实测]。角度单位为弧度且 angleExtent 覆盖 angleStart 之后的区间。</para>
	///   <para><b>与相邻算子的取舍</b>与 int 重载同 id：int 重载（numLevels/contrast/minContrast 为 int）不能表达 "auto"，适合把本重载算出的结果回填；首次摸索参数用本元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage template = new JlImage("byte", 64, 64);
	///   JlTuple names = template.DetermineShapeModelParams("auto", -0.39, 0.79, "auto", "auto",
	///       "auto", "use_polarity", "auto", "auto", "all", out JlTuple values);
	///   template.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>names 与 values 等长按序对应；把它们逐项传给 CreateShapeModel 前注意 int/double/JlTuple 重载选择，传错重载会把字符串 "auto" 再交给原生（等价于不采用建议值）。</para>
	/// </remarks>
	public JlTuple DetermineShapeModelParams(JlTuple numLevels, double angleStart, double angleExtent, JlTuple scaleMin, JlTuple scaleMax, string optimization, string metric, JlTuple contrast, JlTuple minContrast, JlTuple parameters, out JlTuple parameterValue)
	{
		IntPtr proc = JlNativeApi.PreCall(880);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, scaleMin);
		JlNativeApi.Store(proc, 4, scaleMax);
		JlNativeApi.StoreS(proc, 5, optimization);
		JlNativeApi.StoreS(proc, 6, metric);
		JlNativeApi.Store(proc, 7, contrast);
		JlNativeApi.Store(proc, 8, minContrast);
		JlNativeApi.Store(proc, 9, parameters);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(scaleMin);
		JlNativeApi.UnpinTuple(scaleMax);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		JlNativeApi.UnpinTuple(parameters);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out parameterValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在本模板图上试算形状模型参数（标量版，numLevels/contrast 必须是数值）。</summary>
	/// <param name="numLevels">金字塔层数（无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数，无法传 "auto"）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数，无法传 "auto"）。Default: "auto"</param>
	/// <param name="parameters">要自动确定的参数名集合。Default: "all"</param>
	/// <param name="parameterValue">与返回参数名一一对应的建议值（新元组）。</param>
	/// <returns>被自动确定的参数名元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 880 的标量版：numLevels/contrast/minContrast 走 StoreI、scaleMin/scaleMax 走 StoreD，无元组钉住开销。</para>
	///   <para><b>与相邻算子的取舍</b>本重载把 "auto" 类语义换成了强制给数值：适合在元组版跑完、拿到建议值后做<b>复核性重算</b>或纯数值实验；第一次建模型请仍用元组版 <see cref="DetermineShapeModelParams(JlTuple,double,double,JlTuple,JlTuple,string,string,JlTuple,JlTuple,JlTuple,out JlTuple)"/>。</para>
	///   <para><b>约束或前提</b>contrast 给单整数即单阈值；滞后双阈值只能走元组版。角度弧度制。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   JlTuple names = tmpl.DetermineShapeModelParams(4, -0.39, 0.79, 0.9, 1.1,
	///       "auto", "use_polarity", 40, 15, "all", out JlTuple values);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>注意与元组版的重载选择：任何实参写成字符串字面量都会跳回元组重载（隐式转换），需要数值语义时写 int/double 字面量。</para>
	/// </remarks>
	public JlTuple DetermineShapeModelParams(int numLevels, double angleStart, double angleExtent, double scaleMin, double scaleMax, string optimization, string metric, int contrast, int minContrast, string parameters, out JlTuple parameterValue)
	{
		IntPtr proc = JlNativeApi.PreCall(880);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.StoreS(proc, 5, optimization);
		JlNativeApi.StoreS(proc, 6, metric);
		JlNativeApi.StoreI(proc, 7, contrast);
		JlNativeApi.StoreI(proc, 8, minContrast);
		JlNativeApi.StoreS(proc, 9, parameters);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out parameterValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在图中查找多个各向异性缩放形状模型的全部/前 N 个最佳匹配（元组参数版）。</summary>
	/// <param name="modelIDs">模型句柄数组，原生侧先 ConcatArray 拼成句柄元组。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">找到的实例个数，0 表示所有满足条件的匹配。Default: 1</param>
	/// <param name="maxOverlap">实例间允许的最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（=2 时兼作最低层）。Default: 0</param>
	/// <param name="greediness">搜索启发式贪心度：0 稳而慢，1 快但可能漏检。Default: 0.9</param>
	/// <param name="row">找到实例的形心行坐标（DOUBLE）。</param>
	/// <param name="column">形心列坐标（DOUBLE）。</param>
	/// <param name="angle">实例旋转角（弧度，DOUBLE）。</param>
	/// <param name="scaleR">实例行方向缩放。</param>
	/// <param name="scaleC">实例列方向缩放。</param>
	/// <param name="score">实例匹配分。</param>
	/// <param name="model">实例来自第几个输入模型（INTEGER 索引）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 884。行、列缩放独立（anisotropic），适合工件在两个方向上有不同形变的场景；7 条输出按匹配实例对齐，前 6 条 DOUBLE、model 条 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>各向同性缩放用 <see cref="FindScaledShapeModels(JlShapeModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>（更省），无缩放需求用 FindShapeModels；标量参数版（单模型+double 形参）适合固定参数的高频调用，本数组元组版适合"一批模型一次查"。model 索引指明每条结果属于 modelIDs 数组中哪个模型。</para>
	///   <para><b>约束或前提</b>angleStart/angleExtent 弧度制；numMatches=0 时 maxOverlap 才参与去重（否则按分数取前 N）[待实测]；图像与模型坐标系一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindAnisoShapeModels(new JlShapeModel[] { model }, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1,
	///       0.5, 0, 0.5, "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄数组在原生调用结束前不得释放（GC.KeepAlive(modelIDs) 佐证）；实例顺序按分数降序还是按模型分组排列本层未体现 [待实测]，跨帧追踪不要依赖输出次序。</para>
	/// </remarks>
	public void FindAnisoShapeModels(JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple scaleRMin, JlTuple scaleRMax, JlTuple scaleCMin, JlTuple scaleCMax, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(884);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, scaleRMin);
		JlNativeApi.Store(proc, 4, scaleRMax);
		JlNativeApi.Store(proc, 5, scaleCMin);
		JlNativeApi.Store(proc, 6, scaleCMax);
		JlNativeApi.Store(proc, 7, minScore);
		JlNativeApi.Store(proc, 8, numMatches);
		JlNativeApi.Store(proc, 9, maxOverlap);
		JlNativeApi.Store(proc, 10, subPixel);
		JlNativeApi.Store(proc, 11, numLevels);
		JlNativeApi.Store(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(scaleRMin);
		JlNativeApi.UnpinTuple(scaleRMax);
		JlNativeApi.UnpinTuple(scaleCMin);
		JlNativeApi.UnpinTuple(scaleCMax);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(greediness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找单个各向异性缩放形状模型的最佳匹配（标量参数版）。</summary>
	/// <param name="modelIDs">单个模型句柄（多模型请用数组元组重载）。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度：0 稳而慢，1 快但漏检。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scaleR">行方向缩放。</param>
	/// <param name="scaleC">列方向缩放。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型索引（INTEGER；单模型时恒指同一模型）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 884 的标量版：角度/缩放/分数阈值走 StoreD、numMatches/numLevels 走 StoreI、subPixel 走 StoreS，句柄直接 Store——没有元组钉住与 ConcatArray 组装开销，单模型高频产线调用首选。</para>
	///   <para><b>与相邻算子的取舍</b>要一次查多个模型改数组元组重载 <see cref="FindAnisoShapeModels(JlShapeModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；不需要缩放差改用 FindScaledShapeModels/FindShapeModels 以省 pyramid 计算。</para>
	///   <para><b>约束或前提</b>角度弧度制；numMatches=0 时 maxOverlap 才参与去重 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindAnisoShapeModels(model, -0.79, 1.57, 0.9, 1.1, 0.9, 1.1, 0.6, 1, 0.5,
	///       "least_squares", 0, 0.8,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证），可在本方法返回后再 Dispose；单模型时 model 输出仍需读取——用它区分"未找到"（空元组）与找到。</para>
	/// </remarks>
	public void FindAnisoShapeModels(JlShapeModel modelIDs, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(884);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleRMin);
		JlNativeApi.StoreD(proc, 4, scaleRMax);
		JlNativeApi.StoreD(proc, 5, scaleCMin);
		JlNativeApi.StoreD(proc, 6, scaleCMax);
		JlNativeApi.StoreD(proc, 7, minScore);
		JlNativeApi.StoreI(proc, 8, numMatches);
		JlNativeApi.StoreD(proc, 9, maxOverlap);
		JlNativeApi.StoreS(proc, 10, subPixel);
		JlNativeApi.StoreI(proc, 11, numLevels);
		JlNativeApi.StoreD(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找多个各向同性缩放形状模型的最佳匹配（数组+元组参数版）。</summary>
	/// <param name="modelIDs">模型句柄数组，原生侧 ConcatArray 拼为句柄元组。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放（行列同步）。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数（行=列）。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型在数组中的索引（INTEGER）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 885。与 884（Aniso）同为多模型查找，但只有一个 scale 维度：6 条 DOUBLE/INTEGER 混装输出（前 5 条 DOUBLE、model 条 INTEGER）。本库文档默认 angleExtent 与 Aniso 版不同（0.78 vs 0.79），照抄默认值时注意区分。</para>
	///   <para><b>与相邻算子的取舍</b>工件存在透视/非均匀缩放（如倾斜放置的标签）时 scale 单一维度表达不了，须用 <see cref="FindAnisoShapeModels(JlShapeModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；完全无缩放则 FindShapeModels 更快。</para>
	///   <para><b>约束或前提</b>角度弧度制；numMatches=0 时 maxOverlap 参与去重 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindScaledShapeModels(new JlShapeModel[] { model }, -0.39, 0.78, 0.9, 1.1, 0.5, 0, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>数组内任一模型句柄已 Dispose 会连带本次调用失败；句柄数组在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindScaledShapeModels(JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple scaleMin, JlTuple scaleMax, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(885);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, scaleMin);
		JlNativeApi.Store(proc, 4, scaleMax);
		JlNativeApi.Store(proc, 5, minScore);
		JlNativeApi.Store(proc, 6, numMatches);
		JlNativeApi.Store(proc, 7, maxOverlap);
		JlNativeApi.Store(proc, 8, subPixel);
		JlNativeApi.Store(proc, 9, numLevels);
		JlNativeApi.Store(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(scaleMin);
		JlNativeApi.UnpinTuple(scaleMax);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(greediness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找单个各向同性缩放形状模型的最佳匹配（标量参数版）。</summary>
	/// <param name="modelIDs">单个模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型索引（INTEGER，单模型时用于判定是否命中）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 885 的标量版：StoreD/StoreI/StoreS 直写、无 ConcatArray 与钉固定开销；前 5 条输出 DOUBLE、model 条 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>单模型且不需要 scale 输出时用最普通的 FindShapeModels（少一维搜索更省时）；行列形变不等用 FindAnisoShapeModels 族；本方法专属"等比缩放+旋转"的找料场景。</para>
	///   <para><b>约束或前提</b>角度弧度制；未命中时全部输出为空元组；numMatches=0 时 maxOverlap 参与去重 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindScaledShapeModels(model, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在本方法返回前不得释放（GC.KeepAlive 佐证）；scale 输出可反推工件距离变化，做定标时先固定 numMatches=1。</para>
	/// </remarks>
	public void FindScaledShapeModels(JlShapeModel modelIDs, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(885);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.StoreD(proc, 5, minScore);
		JlNativeApi.StoreI(proc, 6, numMatches);
		JlNativeApi.StoreD(proc, 7, maxOverlap);
		JlNativeApi.StoreS(proc, 8, subPixel);
		JlNativeApi.StoreI(proc, 9, numLevels);
		JlNativeApi.StoreD(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找多个形状模型（仅旋转、不缩放）的最佳匹配（数组+元组参数版）。</summary>
	/// <param name="modelIDs">模型句柄数组，原生侧 ConcatArray 拼为句柄元组。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度：0 稳而慢，1 快但漏检。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型在数组中的索引（INTEGER）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 886。形状匹配族里搜索维度最少、速度最快的多模型版：4 条 DOUBLE + 1 条 INTEGER 输出，无 scale 维度。</para>
	///   <para><b>与相邻算子的取舍</b>工件有缩放（输送距离波动、镜头变焦）时本方法会因分数不达标而漏检，须换 FindScaledShapeModels/FindAnisoShapeModels 族；灰度渐变、无稳定边缘的工件形状匹配本就不合适，改用 NCC 族（<see cref="CreateNccModel(JlTuple,double,double,JlTuple,string)"/>）。</para>
	///   <para><b>约束或前提</b>角度弧度制；未命中输出空元组；模型句柄保持未释放期间本方法可对同一模型反复调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindShapeModels(new JlShapeModel[] { model }, -0.39, 0.79, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>数组与九条元组参数全部钉住后逐个 UnpinTuple（与标量重载的本质差异）；输出实例的排列次序不承诺稳定，跨帧对应请按 (row,column) 距离匹配而非按下标。</para>
	/// </remarks>
	public void FindShapeModels(JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(886);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, minScore);
		JlNativeApi.Store(proc, 4, numMatches);
		JlNativeApi.Store(proc, 5, maxOverlap);
		JlNativeApi.Store(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.Store(proc, 8, greediness);
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
		JlNativeApi.UnpinTuple(greediness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找单个形状模型（仅旋转）的最佳匹配（标量参数版，最常用的定位入口）。</summary>
	/// <param name="modelIDs">单个模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式，"none" 关闭亚像素。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度：0 稳而慢，1 快但漏检。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型索引（INTEGER，单模型场景配合判空即可）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 886 的标量版：StoreD/StoreI/StoreS 直写全部参数，无钉固定开销，是"取位姿"场景的默认入口；row/column/angle 三者可直接喂给仿射变换（angle 为弧度）。</para>
	///   <para><b>与相邻算子的取舍</b>只允许一个实例时设 numMatches=1 最快；要同型号多件用数组元组重载一次查全。对亮度/灰度漂移敏感的场景不用形状匹配，改 NCC 族。</para>
	///   <para><b>约束或前提</b>angleExtent 与 angleStart 定义的是同一个连续区间，跨过 0 的摆动角区间要写成 angleStart=-a、angleExtent=2a 而不是两段拼接 [待实测]；未命中时所有输出为空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindShapeModels(model, -0.39, 0.79, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在本方法返回前不得释放（GC.KeepAlive 佐证），返回后即可安全 Dispose；score 上限 1，接近 1 不代表无偏移，仅表示轮廓一致度高，平移精度另看 subPixel 设置。</para>
	/// </remarks>
	public void FindShapeModels(JlShapeModel modelIDs, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(886);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.StoreD(proc, 8, greediness);
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
		GC.KeepAlive(modelIDs);
	}

	/// <summary>查找单个各向异性缩放形状模型的最佳匹配（混合元组参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放（标量直写）。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分（元组，被钉住）。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式（元组）。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（元组）。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scaleR">行方向缩放。</param>
	/// <param name="scaleC">列方向缩放。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 887。单模型版没有 model 索引输出（6 条输出全 DOUBLE）——与 FindAnisoShapeModels 族的本质区别。本重载中 minScore/subPixel/numLevels 走 Store+UnpinTuple（钉住），缩放与角度参数走 StoreD。</para>
	///   <para><b>与相邻算子的取舍</b>纯标量参数请选 double/string 重载以免钉固定开销；本重载存在的意义是把 subPixel、numLevels 以多元素元组一次性交给原生（行为 [待实测]）。只找位姿、无需行列分别缩放时用 FindScaledShapeModel/FindShapeModel。</para>
	///   <para><b>约束或前提</b>角度弧度制；scaleR/scaleC 分别对应建模型时给定的行/列缩放区间，超出部分不会被搜到；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   JlTuple minScore = 0.5;
	///   JlTuple subPixel = "least_squares";
	///   JlTuple numLevels = 0;
	///   scene.FindAnisoShapeModel(model, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, minScore, 1, 0.5,
	///       subPixel, numLevels, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）；行列两个缩放输出做几何补偿时需一起用，只取其一会把工件拉变形。</para>
	/// </remarks>
	public void FindAnisoShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(887);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleRMin);
		JlNativeApi.StoreD(proc, 4, scaleRMax);
		JlNativeApi.StoreD(proc, 5, scaleCMin);
		JlNativeApi.StoreD(proc, 6, scaleCMax);
		JlNativeApi.Store(proc, 7, minScore);
		JlNativeApi.StoreI(proc, 8, numMatches);
		JlNativeApi.StoreD(proc, 9, maxOverlap);
		JlNativeApi.Store(proc, 10, subPixel);
		JlNativeApi.Store(proc, 11, numLevels);
		JlNativeApi.StoreD(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个各向异性缩放形状模型的最佳匹配（全标量参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scaleR">行方向缩放。</param>
	/// <param name="scaleC">列方向缩放。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 887 的全标量版：除模型句柄外全部 StoreD/StoreI/StoreS 直写，无钉固定开销；6 条输出全 DOUBLE、无 model 索引（单模型无需）。</para>
	///   <para><b>与相邻算子的取舍</b>与混合元组重载（minScore/subPixel/numLevels 为 JlTuple）同 id，单值场景选本重载；无行列差异的缩放用 FindScaledShapeModel 少一维搜索。</para>
	///   <para><b>约束或前提</b>角度弧度制；行列缩放区间独立生效；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindAnisoShapeModel(model, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindAnisoShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(887);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleRMin);
		JlNativeApi.StoreD(proc, 4, scaleRMax);
		JlNativeApi.StoreD(proc, 5, scaleCMin);
		JlNativeApi.StoreD(proc, 6, scaleCMax);
		JlNativeApi.StoreD(proc, 7, minScore);
		JlNativeApi.StoreI(proc, 8, numMatches);
		JlNativeApi.StoreD(proc, 9, maxOverlap);
		JlNativeApi.StoreS(proc, 10, subPixel);
		JlNativeApi.StoreI(proc, 11, numLevels);
		JlNativeApi.StoreD(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个等比缩放形状模型的最佳匹配（混合元组参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放（标量直写）。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分（元组，被钉住）。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式（元组）。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（元组）。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 888。单模型等比缩放查找：5 条 DOUBLE 输出、无 model 索引；minScore/subPixel/numLevels 走 Store+UnpinTuple（钉住），其余标量直写。</para>
	///   <para><b>与相邻算子的取舍</b>单值参数请选全标量重载以免钉固定开销；行列缩放不等用 FindAnisoShapeModel；无缩放需求用 FindShapeModel（搜索维度最少最快）。本重载用于需要以元组形式批量传分阈值/亚像素模式的场合（原生对多元素的行为 [待实测]）。</para>
	///   <para><b>约束或前提</b>角度弧度制；scale 输出可乘回建模型时的基准得到工件实际尺寸比例；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   JlTuple minScore = 0.5;
	///   JlTuple subPixel = "least_squares";
	///   JlTuple numLevels = 0;
	///   scene.FindScaledShapeModel(model, -0.39, 0.78, 0.9, 1.1, minScore, 1, 0.5,
	///       subPixel, numLevels, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindScaledShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleMin, double scaleMax, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(888);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.Store(proc, 5, minScore);
		JlNativeApi.StoreI(proc, 6, numMatches);
		JlNativeApi.StoreD(proc, 7, maxOverlap);
		JlNativeApi.Store(proc, 8, subPixel);
		JlNativeApi.Store(proc, 9, numLevels);
		JlNativeApi.StoreD(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个等比缩放形状模型的最佳匹配（全标量参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 888 的全标量版：minScore/scale 等走 StoreD、numMatches/numLevels 走 StoreI、subPixel 走 StoreS，无钉固定开销；5 条 DOUBLE 输出、无 model 索引。</para>
	///   <para><b>与相邻算子的取舍</b>本重载是等比缩放定位的默认入口；只要位姿不要缩放时用 FindShapeModel 更省；缩放各向异性时用 FindAnisoShapeModel。</para>
	///   <para><b>约束或前提</b>角度弧度制；未命中时输出全为空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindScaledShapeModel(model, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindScaledShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(888);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.StoreD(proc, 5, minScore);
		JlNativeApi.StoreI(proc, 6, numMatches);
		JlNativeApi.StoreD(proc, 7, maxOverlap);
		JlNativeApi.StoreS(proc, 8, subPixel);
		JlNativeApi.StoreI(proc, 9, numLevels);
		JlNativeApi.StoreD(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个形状模型（仅旋转）的最佳匹配（混合元组参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分（元组，被钉住）。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式（元组）。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（元组）。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 889。最基础的"给位姿"算子：4 条 DOUBLE 输出即刚体位姿 (row, column, angle) + score；本重载把 minScore/subPixel/numLevels 以钉住的元组交给原生。</para>
	///   <para><b>与相邻算子的取舍</b>单值场合请用全标量重载（免钉固）；有缩放/透视时位姿会因分数不足而漏检，须换 Scaled/Aniso 族；NCC 定位用 FindNccModel 族。</para>
	///   <para><b>约束或前提</b>angle 为相对模型坐标系的弧度角；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   JlTuple minScore = 0.5;
	///   JlTuple subPixel = "least_squares";
	///   JlTuple numLevels = 0;
	///   scene.FindShapeModel(model, -0.39, 0.79, minScore, 1, 0.5, subPixel, numLevels, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）；row/column/angle 三个元组长度一致但顺序不承诺跨帧稳定，多实例时自行按 score 排序再消费。</para>
	/// </remarks>
	public void FindShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(889);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.Store(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.StoreD(proc, 8, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个形状模型（仅旋转）的最佳匹配（全标量参数版，最常用的定位入口）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式，"none" 关闭。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 889 的全标量版：全部参数 StoreD/StoreI/StoreS 直写，无钉固定开销；输出 4 条 DOUBLE 元组即刚体位姿+分数，可直接生成 <see cref="JlHomMat2D"/> 做后续对齐。</para>
	///   <para><b>与相邻算子的取舍</b>与混合元组重载同 id、行为一致，仅装载路径不同；单值参数场景一律用本重载。多模型轮询用 FindShapeModels 数组版更省调用次数。</para>
	///   <para><b>约束或前提</b>angle 弧度制；未命中输出空元组（用 row.TupleLength()==0 判空，而非比较 score 与 minScore）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindShapeModel(model, -0.39, 0.79, 0.5, 1, 0.5, "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(889);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.StoreD(proc, 8, greediness);
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
		GC.KeepAlive(modelID);
	}

	/// <summary>对由 XLD 轮廓生成的形状模型重设匹配度量（需带上其模板图作为当前对象）。</summary>
	/// <param name="modelID">待改的模型句柄。</param>
	/// <param name="homMat2D">建模型时所用的变换矩阵（与建模型时保持一致才有效）。</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 890。当前 JlImage 会作为第一路 iconic 输入（实现里 Store(proc,1) 先存 this）——必须是当初生成该模型的模板图，原生要据此重算轮廓；把别的图传进来结果不可信 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>改的是"极性/忽略极性"这一匹配语义；改其它参数（角度序、层数等）走静态 <see cref="SetShapeModelParam"/>。metric 与 CreateShapeModel 的 metric 形参同一取值集。</para>
	///   <para><b>约束或前提</b>仅适用于 XLD 生成法建的模型；homMat2D 以钉住方式传给原生（调用后 UnpinTuple），期间不得改写该矩阵。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "ignore_polarity", "auto", "auto");
	///   JlHomMat2D hom = new JlHomMat2D();
	///   tmpl.SetShapeModelMetric(model, hom, "use_polarity");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlHomMat2D 不实现 IDisposable，无释放负担；model 句柄调用后仍归调用方管理；示例中 hom 为恒等阵，与建模型时的缺省变换一致。</para>
	/// </remarks>
	public void SetShapeModelMetric(JlShapeModel modelID, JlHomMat2D homMat2D, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(890);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreS(proc, 2, metric);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>按"参数名/参数值"元组批量改写形状模型的可选参数（静态方法，不需要模板图）。</summary>
	/// <param name="modelID">待改的模型句柄。</param>
	/// <param name="genParamName">参数名元组（如 "num_levels"、"angle_step"，取值集合本层未体现 [待实测]）。</param>
	/// <param name="genParamValue">与参数名等长、按序对应的值元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 891。这是静态方法：实现里没有 Store(this)，与当前图像对象无关，通过 JlImage.SetShapeModelParam(...) 调用即可；两个元组全程钉住、调用后 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>改匹配极性用 <see cref="SetShapeModelMetric(JlShapeModel,JlHomMat2D,string)"/>（那条还要模板图）；本方法是"万能后门"，参数名写错时原生可能静默忽略 [待实测]，改完建议用一次 FindShapeModel 验证分数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage.SetShapeModelParam(model, new string[] { "num_levels" }, new int[] { 4 });
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive(modelID) 佐证）；name 与 value 长度不等时的行为未定义 [待实测]。</para>
	/// </remarks>
	public static void SetShapeModelParam(JlShapeModel modelID, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(891);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(modelID);
	}

	/// <summary>在当前（已裁域的）模板图上创建各向异性缩放形状模型，返回模型新句柄。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长或 "auto"。Default: "auto"</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleRStep">行方向缩放步长或 "auto"。Default: "auto"</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCStep">列方向缩放步长或 "auto"。Default: "auto"</param>
	/// <param name="optimization">优化方式或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值/滞后双阈值或 "auto"。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度或 "auto"。Default: "auto"</param>
	/// <returns>模型的新句柄（JlShapeModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 895。行列缩放各自成维（anisotropic），搜索空间是 Scaled 版的平方级放大：建模型与匹配都更慢、内存更多。当前图像是模板源，其<b>域</b>决定取哪块轮廓，建模型前务必 ReduceDomain 圈住目标。</para>
	///   <para><b>与相邻算子的取舍</b>只有等比缩放用 CreateScaledShapeModel（id 896，一维 scale）；无缩放用 CreateShapeModel（id 897）；三者的 metric/contrast 语义相同。能 "auto" 的形参在标量重载里必须给数值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateAnisoShapeModel("auto", -0.39, 0.79, "auto",
	///       0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", "auto", "auto");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄与模板图生命周期独立：Dispose 模板图后模型仍可用；angleStep 给得过小会把角度库撑大、Find 阶段逐角匹配变慢。</para>
	/// </remarks>
	public JlShapeModel CreateAnisoShapeModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(895);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleRMin);
		JlNativeApi.StoreD(proc, 5, scaleRMax);
		JlNativeApi.Store(proc, 6, scaleRStep);
		JlNativeApi.StoreD(proc, 7, scaleCMin);
		JlNativeApi.StoreD(proc, 8, scaleCMax);
		JlNativeApi.Store(proc, 9, scaleCStep);
		JlNativeApi.Store(proc, 10, optimization);
		JlNativeApi.StoreS(proc, 11, metric);
		JlNativeApi.Store(proc, 12, contrast);
		JlNativeApi.Store(proc, 13, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(scaleRStep);
		JlNativeApi.UnpinTuple(scaleCStep);
		JlNativeApi.UnpinTuple(optimization);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建各向异性缩放形状模型（全标量参数版，numLevels/contrast 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleRStep">行方向缩放步长。Default: "auto"</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCStep">列方向缩放步长。Default: "auto"</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数）。Default: "auto"</param>
	/// <returns>模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 895 的全标量版：numLevels/contrast/minContrast 走 StoreI、步长与缩放走 StoreD，无钉固定开销。适合把 DetermineShapeModelParams 定出的数值直接回填、复现同一模型。</para>
	///   <para><b>与相邻算子的取舍</b>要 "auto" 自动量纲就用元组重载；等比缩放用 CreateScaledShapeModel。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateAnisoShapeModel(4, -0.39, 0.79, 0.052,
	///       0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "use_polarity", 40, 15);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>重载选择与元组版互斥于实参类型：任一处写 "auto" 字符串会整体落到元组重载；建模型在 Dispose 模板图后模型仍可用。</para>
	/// </remarks>
	public JlShapeModel CreateAnisoShapeModel(int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(895);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleRMin);
		JlNativeApi.StoreD(proc, 5, scaleRMax);
		JlNativeApi.StoreD(proc, 6, scaleRStep);
		JlNativeApi.StoreD(proc, 7, scaleCMin);
		JlNativeApi.StoreD(proc, 8, scaleCMax);
		JlNativeApi.StoreD(proc, 9, scaleCStep);
		JlNativeApi.StoreS(proc, 10, optimization);
		JlNativeApi.StoreS(proc, 11, metric);
		JlNativeApi.StoreI(proc, 12, contrast);
		JlNativeApi.StoreI(proc, 13, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建等比缩放形状模型，返回模型新句柄（元组参数版）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度）或 "auto"。Default: "auto"</param>
	/// <param name="scaleMin">最小缩放（行列同步）。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="scaleStep">缩放步长或 "auto"。Default: "auto"</param>
	/// <param name="optimization">优化方式或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值/滞后双阈值或 "auto"。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度或 "auto"。Default: "auto"</param>
	/// <returns>模型的新句柄（JlShapeModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 896。当前图像为模板源，其<b>域</b>决定提取哪块轮廓；输出模型携带 angle×scale 两级搜索库。可 "auto" 的形参走 Store+UnpinTuple（钉住）。</para>
	///   <para><b>与相邻算子的取舍</b>行列形变不等才升 CreateAnisoShapeModel（895，搜索量平方级）；确定无缩放用 CreateShapeModel（897）。metric 选 "ignore_polarity" 可容忍反色打光，但误匹配率上升。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateScaledShapeModel("auto", -0.39, 0.79, "auto",
	///       0.9, 1.1, "auto", "auto", "use_polarity", "auto", "auto");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模板图 Dispose 后模型仍可用（生命周期独立）；scaleStep 过密会让角度×缩放组合数暴涨，Find 阶段耗时按组合数线性上升。</para>
	/// </remarks>
	public JlShapeModel CreateScaledShapeModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(896);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleMin);
		JlNativeApi.StoreD(proc, 5, scaleMax);
		JlNativeApi.Store(proc, 6, scaleStep);
		JlNativeApi.Store(proc, 7, optimization);
		JlNativeApi.StoreS(proc, 8, metric);
		JlNativeApi.Store(proc, 9, contrast);
		JlNativeApi.Store(proc, 10, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(scaleStep);
		JlNativeApi.UnpinTuple(optimization);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建等比缩放形状模型（全标量参数版，numLevels/contrast 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="scaleStep">缩放步长（数值）。Default: "auto"</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数）。Default: "auto"</param>
	/// <returns>模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 896 的全标量版：numLevels/contrast/minContrast 走 StoreI、角度与缩放走 StoreD，无钉固定开销；适合回填 DetermineShapeModelParams 定出的数值以复现模型。</para>
	///   <para><b>与相邻算子的取舍</b>需要 "auto" 语义（让原生自选层数/对比度）就用元组重载；无缩放场景降档 CreateShapeModel 更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateScaledShapeModel(4, -0.39, 0.79, 0.052,
	///       0.9, 1.1, 0.05, "auto", "use_polarity", 40, 15);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>任一 "auto" 字符串实参会使整体落到元组重载，勿混写；Dispose 模板图不影响已建模型。</para>
	/// </remarks>
	public JlShapeModel CreateScaledShapeModel(int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(896);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleMin);
		JlNativeApi.StoreD(proc, 5, scaleMax);
		JlNativeApi.StoreD(proc, 6, scaleStep);
		JlNativeApi.StoreS(proc, 7, optimization);
		JlNativeApi.StoreS(proc, 8, metric);
		JlNativeApi.StoreI(proc, 9, contrast);
		JlNativeApi.StoreI(proc, 10, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建（仅旋转的）形状模型，返回模型新句柄（元组参数版）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度）或 "auto"。Default: "auto"</param>
	/// <param name="optimization">优化方式或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值/滞后双阈值或 "auto"。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度或 "auto"。Default: "auto"</param>
	/// <returns>模型的新句柄（JlShapeModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 897。形状匹配族的"标准建模型"：只搜旋转不搜缩放；当前图像为模板源，其<b>域</b>决定提取哪块轮廓，先 ReduceDomain 再建。可 "auto" 的形参钉住后 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>有缩放用 CreateScaledShapeModel/CreateAnisoShapeModel；亮度漂移大、纹理弱（如印刷灰度块）时形状匹配不如 NCC：CreateNccModel。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>contrast 用 "auto" 时由原生按模板噪声水平定阈值——模板里若混入半个相邻零件，auto 会把它的轮廓也编进模型，建前务必裁域；Dispose 模板图不影响已建模型。</para>
	/// </remarks>
	public JlShapeModel CreateShapeModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(897);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.Store(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.Store(proc, 6, contrast);
		JlNativeApi.Store(proc, 7, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(optimization);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建（仅旋转的）形状模型（全标量参数版，numLevels/contrast 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数）。Default: "auto"</param>
	/// <returns>模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 897 的全标量版：numLevels/contrast/minContrast 走 StoreI、angleStep 走 StoreD，无钉固定开销；适合按 DetermineShapeModelParams 的建议值精确复现模型。</para>
	///   <para><b>与相邻算子的取舍</b>要让原生自选层数/对比度就用元组重载；本重载 contrast 只能给单阈值，滞后双阈值（"high_low" 两值）不可表达。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel(4, -0.39, 0.79, 0.052, "auto", "use_polarity", 40, 15);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>混写字符串实参会整体落到元组重载；建模型阶段对比度门槛给太低会把背景纹理一起编进模型，Find 分数天花板被拉低。</para>
	/// </remarks>
	public JlShapeModel CreateShapeModel(int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(897);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.StoreI(proc, 6, contrast);
		JlNativeApi.StoreI(proc, 7, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>可视化"若按此对比度建模型会提取到哪些轮廓"：返回金字塔图像与模型区域。</summary>
	/// <param name="modelRegions">模型区域金字塔（新 JlRegion 句柄，每层一个区域）。</param>
	/// <param name="numLevels">金字塔层数。Default: 4</param>
	/// <param name="contrast">对比度阈值/滞后双阈值或最小尺寸（元组，被钉住）。Default: 30</param>
	/// <returns>输入图的金字塔（新图像句柄，每层一幅）。返回值与 out 都要释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 898。它<b>不创建模型</b>，是把 CreateShapeModel 前半段（边缘提取+按对比度筛选）的结果画出来供人眼核对：把 modelRegions 叠回图上即可看到将被编入模型的轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>正式建模型直接调 CreateShapeModel；调 contrast/域 拿不准时先用本方法验证，避免建出"半个零件"或漏轮廓的模型。int contrast 重载省去钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   JlTuple contrast = 30;
	///   using JlImage pyramid = tmpl.InspectShapeModel(out JlRegion modelRegions, 4, contrast);
	///   modelRegions.Dispose();
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的图像与 out 的区域都是新句柄，示例中 pyramid 用 using、modelRegions 手动 Dispose；numLevels 与金字塔幅数一致，region 元组式栈按层排列，取第 k 层用 SelectObj。</para>
	/// </remarks>
	public JlImage InspectShapeModel(out JlRegion modelRegions, int numLevels, JlTuple contrast)
	{
		IntPtr proc = JlNativeApi.PreCall(898);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.Store(proc, 1, contrast);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(contrast);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out modelRegions);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>预览按给定单一对比度阈值建形状模型将提取到的轮廓（标量版）。</summary>
	/// <param name="modelRegions">模型区域金字塔（新 JlRegion 句柄栈，每层一个区域，需释放）。</param>
	/// <param name="numLevels">金字塔层数。Default: 4</param>
	/// <param name="contrast">对比度阈值（整数，无法传滞后双阈值/"auto"）。Default: 30</param>
	/// <returns>输入图的金字塔（新图像句柄栈，需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 898 的标量版：contrast 走 StoreI、不钉元组。用途同元组版——把 CreateShapeModel 的边缘提取阶段可视化核对。</para>
	///   <para><b>与相邻算子的取舍</b>要滞后双阈值（contrast 两元素）或"auto"，用 <see cref="InspectShapeModel(out JlRegion,int,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlImage pyramid = tmpl.InspectShapeModel(out JlRegion modelRegions, 4, 30);
	///   modelRegions.Dispose();
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个输出都是新句柄：示例中 pyramid 用 using、modelRegions 手动 Dispose；本方法只是预览，最终模型仍要 CreateShapeModel 生成。</para>
	/// </remarks>
	public JlImage InspectShapeModel(out JlRegion modelRegions, int numLevels, int contrast)
	{
		IntPtr proc = JlNativeApi.PreCall(898);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreI(proc, 1, contrast);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out modelRegions);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}























	/// <summary>在图中查找单个 NCC 模型的最佳匹配（灰度相关定位）。</summary>
	/// <param name="modelID">NCC 模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.8</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">是否亚像素（"true"/"false" 风格）。Default: "true"</param>
	/// <param name="numLevels">金字塔层数（元组，被钉住）。Default: 0</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">归一化互相关得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 945。按灰度相关（NCC）打分而非边缘形状：适合纹理弱、对比度低但亮度模式稳定的工件。缺省 minScore 比形状族高（0.8 vs 0.5）——互相关分数分布更集中。</para>
	///   <para><b>与相邻算子的取舍</b>边缘清晰、光照渐变不敏感的用 FindShapeModel；需要多模型一次查用 FindNccModels；本算子的 NCC 模型由 <see cref="CreateNccModel(JlTuple,double,double,JlTuple,string)"/> 创建。</para>
	///   <para><b>约束或前提</b>角度弧度制；NCC 对整体亮度线性变化不敏感但对非线性gamma差异敏感 [待实测]；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel("auto", -0.39, 0.79, "auto", "use_polarity");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindNccModel(model, -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>numLevels 是钉住的元组形参（与标量版 FindShapeModel 不同），传字面量 0 时经隐式转换仍走本签名；模型句柄在原生调用结束前不得释放。</para>
	/// </remarks>
	public void FindNccModel(JlNCCModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(945);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
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
		GC.KeepAlive(modelID);
	}

	/// <summary>在图中查找单个 NCC 模型的最佳匹配（numLevels 标量版）。</summary>
	/// <param name="modelID">NCC 模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.8</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">是否亚像素（"true"/"false" 风格）。Default: "true"</param>
	/// <param name="numLevels">金字塔层数（int 直写，无钉固定）。Default: 0</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">归一化互相关得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 945：numLevels 走 StoreI 的标量重载，其余与元组版一致（4 条 DOUBLE 输出）。NCC 按灰度相关打分，适合边缘弱、亮度模式稳的工件。</para>
	///   <para><b>与相邻算子的取舍</b>传字面量 0 时两个重载都可用，本重载绑定 int 实参更严格、免 UnpinTuple；多模型一次查用 FindNccModels。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel(4, -0.39, 0.79, 0.052, "use_polarity");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindNccModel(model, -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）；未命中输出空元组。</para>
	/// </remarks>
	public void FindNccModel(JlNCCModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(945);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
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
		GC.KeepAlive(modelID);
	}

	/// <summary>按"参数名/参数值"元组批量改写 NCC 模型的可选参数（静态方法，不需要图像）。</summary>
	/// <param name="modelID">待改的 NCC 模型句柄。</param>
	/// <param name="genParamName">参数名元组（取值集合本层未体现 [待实测]）。</param>
	/// <param name="genParamValue">与参数名等长、按序对应的值元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 946。实现里无 Store(this)：静态调用 JlImage.SetNccModelParam(...)，两个元组钉住后 UnpinTuple；与形状模型的 <see cref="SetShapeModelParam(JlShapeModel,JlTuple,JlTuple)"/> 同构。</para>
	///   <para><b>与相邻算子的取舍</b>模型的角度范围/层数在建模型时定死，本方法能改哪些项不确定 [待实测]，改后务必用 FindNccModel 验证。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel(4, -0.39, 0.79, 0.052, "use_polarity");
	///   JlImage.SetNccModelParam(model, new string[] { "min_contrast" }, new int[] { 15 });
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>name/value 长度不等时行为未定义 [待实测]；模型句柄在原生调用结束前不得释放。</para>
	/// </remarks>
	public static void SetNccModelParam(JlNCCModel modelID, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(946);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(modelID);
	}

	/// <summary>以当前图为模板创建 NCC 模型，返回模型新句柄（元组参数版）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度）或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <returns>NCC 模型的新句柄（JlNCCModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 947。与形状模型的差异：不抽边缘、不要求 contrast 参数，直接以<b>域内灰度图样</b>做互相关模板——当前图像的域就是模板本身，裁域比形状族更关键。</para>
	///   <para><b>与相邻算子的取舍</b>需要行列独立缩放或遮挡容忍时形状模型更强；光照线性波动、弱边缘工件用本族。查找配 <see cref="FindNccModel(JlNCCModel,double,double,double,int,double,string,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel("auto", -0.39, 0.79, "auto", "use_polarity");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>numLevels/angleStep 钉住后 UnpinTuple；Dispose 模板图后模型仍可用；metric 用 "ignore_polarity" 时黑白反转也能匹配，注意误配风险。</para>
	/// </remarks>
	public JlNCCModel CreateNccModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(947);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		err = JlNCCModel.LoadNew(proc, 0, err, out JlNCCModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>以当前图为模板创建 NCC 模型（全标量参数版，numLevels/angleStep 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <returns>NCC 模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 947 的标量版：numLevels 走 StoreI、angleStep 走 StoreD，无钉固定开销。模板=当前图像域内灰度图样。</para>
	///   <para><b>与相邻算子的取舍</b>要 "auto" 让原生选层数/步长用元组重载；本重载参数组合固定、适合产线固化配置。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel(4, -0.39, 0.79, 0.052, "use_polarity");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>angleStep 决定角度库密度，过密建模型时间与内存上升；Dispose 模板图不影响模型。</para>
	/// </remarks>
	public JlNCCModel CreateNccModel(int numLevels, double angleStart, double angleExtent, double angleStep, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(947);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNCCModel.LoadNew(proc, 0, err, out JlNCCModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}










	/// <summary>按滞后对比度把图像自动分割成初始连通域，返回轮廓区域新句柄（元组参数版）。</summary>
	/// <param name="contrastLow">滞后下阈值（对比度低限）。Default: "auto"</param>
	/// <param name="contrastHigh">滞后上阈值（对比度高限）。Default: "auto"</param>
	/// <param name="minSize">初始成分最小尺寸（像素数）。Default: "auto"</param>
	/// <param name="mode">自动分割方式。Default: "connection"</param>
	/// <param name="genericName">可选控制参数名（空元组表示不传）。Default: []</param>
	/// <param name="genericValue">可选控制参数值，与 genericName 等长。Default: []</param>
	/// <returns>各初始成分的轮廓区域栈（JlRegion 新句柄，LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 970。这是成分模型（component model）流水线的第一步：先用滞后阈值+连通域把目标切成"初始成分"，再逐组喂给 GenShapeTrans/成分训练算子；单用价值有限。</para>
	///   <para><b>约束或前提</b>contrastLow 必须 ≤ contrastHigh，滞后双阈值语义同 EdgesHysteresis 族；mode/取值集合与 genericName 可选项本层未体现 [待实测]。当前对象是待分割图像。</para>
	///   <para><b>与相邻算子的取舍</b>只要普通二值域时用 Threshold+Connection 即可，不必进本条成分模型链路；本算子输出的区域是"轮廓区域"，面积统计前需 AreaTrans/重算语义 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion comps = img.GenInitialComponents("auto", "auto", "auto", "connection",
	///       new JlTuple(), new JlTuple());
	///   int n = comps.CountObj();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个元组形参全部钉住后逐个 UnpinTuple；输出区域栈顺序由连通域扫描序决定，与上游 Connection 一样不保证跨阈值参数稳定。</para>
	/// </remarks>
	public JlRegion GenInitialComponents(JlTuple contrastLow, JlTuple contrastHigh, JlTuple minSize, string mode, JlTuple genericName, JlTuple genericValue)
	{
		IntPtr proc = JlNativeApi.PreCall(970);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, contrastLow);
		JlNativeApi.Store(proc, 1, contrastHigh);
		JlNativeApi.Store(proc, 2, minSize);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.Store(proc, 4, genericName);
		JlNativeApi.Store(proc, 5, genericValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(contrastLow);
		JlNativeApi.UnpinTuple(contrastHigh);
		JlNativeApi.UnpinTuple(minSize);
		JlNativeApi.UnpinTuple(genericName);
		JlNativeApi.UnpinTuple(genericValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract the initial components of a component model.
	/// </summary>
	/// <param name="contrastLow">Lower hysteresis threshold for the contrast of the initial components in the image. Default: "auto"</param>
	/// <param name="contrastHigh">Upper hysteresis threshold for the contrast of the initial components in the image. Default: "auto"</param>
	/// <param name="minSize">Minimum size of the initial components. Default: "auto"</param>
	/// <param name="mode">Type of automatic segmentation. Default: "connection"</param>
	/// <param name="genericName">Names of optional control parameters. Default: []</param>
	/// <param name="genericValue">Values of optional control parameters. Default: []</param>
	/// <returns>Contour regions of initial components.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>提取 initial components component 模型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GenInitialComponents("auto", "auto", "auto", "connection", new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlRegion GenInitialComponents(int contrastLow, int contrastHigh, int minSize, string mode, string genericName, double genericValue)
	{
		IntPtr proc = JlNativeApi.PreCall(970);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, contrastLow);
		JlNativeApi.StoreI(proc, 1, contrastHigh);
		JlNativeApi.StoreI(proc, 2, minSize);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.StoreS(proc, 4, genericName);
		JlNativeApi.StoreD(proc, 5, genericValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   Convert one-channel images into a multi-channel image
	/// </summary>
	/// <returns>Multi-channel image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 one-channel 图像 为 multi-channel 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ChannelsToImage();
	///   </code>
	/// </remarks>
	public JlImage ChannelsToImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1067);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a multi-channel image into One-channel images
	/// </summary>
	/// <returns>Generated one-channel images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 multi-channel 图像 为 One-channel 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ImageToChannels();
	///   </code>
	/// </remarks>
	public JlImage ImageToChannels()
	{
		IntPtr proc = JlNativeApi.PreCall(1068);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>七幅图合成七通道图，id 1069（本族通道数上限）。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <param name="image5">第 5 通道。</param>
	/// <param name="image6">第 6 通道。</param>
	/// <param name="image7">第 7 通道。</param>
	/// <returns>七通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>族内分工、尺寸一致性与多通道图的后续处理见 <see cref="Compose3(JlImage,JlImage)"/>；
	///   本重载是独立原生 id 1069。7 是本族上限，再多通道需要按图像数组分开处理或分次合成
	///   [待实测：是否还有更高通道数的算子]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage seven = ch0.Compose7(                 // this 提供第 1 路，故只传 6 个实参
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   int channels = seven.CountChannels();                                 // 7
	///   </code>
	/// </remarks>
	public JlImage Compose7(JlImage image2, JlImage image3, JlImage image4, JlImage image5, JlImage image6, JlImage image7)
	{
		IntPtr proc = JlNativeApi.PreCall(1069);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.Store(proc, 5, image5);
		JlNativeApi.Store(proc, 6, image6);
		JlNativeApi.Store(proc, 7, image7);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		GC.KeepAlive(image5);
		GC.KeepAlive(image6);
		GC.KeepAlive(image7);
		return obj;
	}

	/// <summary>六幅图合成六通道图，id 1070。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <param name="image5">第 5 通道。</param>
	/// <param name="image6">第 6 通道。</param>
	/// <returns>六通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>族内分工与约束见 <see cref="Compose3(JlImage,JlImage)"/>；本重载是独立原生 id 1070，
	///   通道顺序 <c>this → image2 → … → image6</c>。再多一路用 <see cref="Compose7"/>（id 1069），这是本族上限。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage six = ch0.Compose6(                   // this 提供第 1 路，故只传 5 个实参
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   int channels = six.CountChannels();                                   // 6
	///   </code>
	/// </remarks>
	public JlImage Compose6(JlImage image2, JlImage image3, JlImage image4, JlImage image5, JlImage image6)
	{
		IntPtr proc = JlNativeApi.PreCall(1070);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.Store(proc, 5, image5);
		JlNativeApi.Store(proc, 6, image6);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		GC.KeepAlive(image5);
		GC.KeepAlive(image6);
		return obj;
	}

	/// <summary>五幅图合成五通道图，id 1071。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <param name="image5">第 5 通道。</param>
	/// <returns>五通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>族内分工、尺寸一致性与"多通道图先转灰度再分割"见 <see cref="Compose3(JlImage,JlImage)"/>；
	///   本重载是独立原生 id 1071，通道顺序 <c>this → image2 → … → image5</c>。
	///   需要更多通道依次用 <see cref="Compose6"/>、<see cref="Compose7"/>（id 1070、1069），上限 7。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("byte", 640, 480);
	///   JlImage b = new JlImage("byte", 640, 480);
	///   JlImage c = new JlImage("byte", 640, 480);
	///   JlImage d = new JlImage("byte", 640, 480);
	///   JlImage e = new JlImage("byte", 640, 480);
	///   using (b) using (c) using (d) using (e)
	///   {
	///       using JlImage five = a.Compose5(b, c, d, e);
	///   }
	///   a.Dispose();
	///   </code>
	/// </remarks>
	public JlImage Compose5(JlImage image2, JlImage image3, JlImage image4, JlImage image5)
	{
		IntPtr proc = JlNativeApi.PreCall(1071);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.Store(proc, 5, image5);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		GC.KeepAlive(image5);
		return obj;
	}

	/// <summary>四幅图合成四通道图，id 1072。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <returns>四通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>与 <see cref="Compose3(JlImage,JlImage)"/> 同一族但<b>原生 id 不同（1072）</b>：
	///   通道顺序 <c>this → image2 → image3 → image4</c>，仍是一路图像输出。约束与坑（尺寸必须一致、
	///   多通道图不能直接进灰度算子）见该重载说明。</para>
	///   <para><b>注意</b>四通道不是"3+1"的扩展：把 alpha/置信度放第 4 通道是本库之外的约定，
	///   本层不会在任何算子里按第 4 通道特殊处理 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("byte", 640, 480);
	///   JlImage b = new JlImage("byte", 640, 480);
	///   JlImage c = new JlImage("byte", 640, 480);
	///   JlImage d = new JlImage("byte", 640, 480);
	///   using (b) using (c) using (d)
	///   {
	///       using JlImage quad = a.Compose4(b, c, d);
	///   }
	///   a.Dispose();
	///   </code>
	/// </remarks>
	public JlImage Compose4(JlImage image2, JlImage image3, JlImage image4)
	{
		IntPtr proc = JlNativeApi.PreCall(1072);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		return obj;
	}

	/// <summary>把三幅图合成一幅三通道图（通道顺序 = 参数顺序）。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <returns>三通道图像的新句柄；输入图不变。</returns>
	/// <remarks>
	///   <para><b>通道族分工（先选对算子）</b>
	///   <c>ComposeN</c>：把 N 幅图合成<b>一幅 N 通道图</b>，N=2…7 各占一个原生算子（1074、1073、1072、1071、1070、1069，随 N 递减），
	///   彼此不是同一 id 的参数化，别指望"一个算子支持任意通道数"；
	///   <c>DecomposeN</c>：其逆运算；
	///   <see cref="ChannelsToImage"/> / <see cref="ImageToChannels"/>（1067/1068）：在"图像数组"与"一幅多通道图"之间整批转换；
	///   <see cref="AppendChannel(JlImage)"/>（1082）：只追加一个通道，不必预先数清通道数；
	///   <c>TileChannels</c>：把通道<b>平铺成一张大图</b>便于比较，产物仍是单通道图；
	///   <see cref="AccessChannel(int)"/>（1083）：反向取通道。</para>
	///   <para><b>功能说明</b>本算子 id 1073，只声明一路图标输出（<c>InitOCT(proc,1)</c>），
	///   结果必定是<b>一幅</b>三通道图，通道顺序严格为 <c>this → image2 → image3</c>。
	///   所谓 "RGB" 只是这个顺序的命名约定，本层不会校正你把 G 放在哪一位。</para>
	///   <para><b>约束</b>三幅输入需同宽高；类型是否必须一致本层未校验 [待实测：不一致时报错还是隐式提升]。
	///   输入本身已是多通道图时的展开方式本层无法判断 [待实测]，稳妥做法是先 <c>AccessChannel</c> 取单通道。
	///   合成出的多通道图不能直接进灰度算子 [待实测]，先转灰度或取通道。</para>
	///   <para><b>与 <c>Compose2</c> 的取舍</b>只有两个特征通道（灰度 + 梯度幅值、可见光 + 红外）就用
	///   <see cref="Compose2(JlImage)"/>；不要为凑三通道复制一幅无意义图——<see cref="CountChannels()"/> 的结果会被下游按通道数分支的代码读到并误解。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose3(g, b);
	///   int channels = rgb.CountChannels();                       // 3
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；三幅输入只读、各做 <c>GC.KeepAlive</c>，所有权不转移。</para>
	/// </remarks>
	public JlImage Compose3(JlImage image2, JlImage image3)
	{
		IntPtr proc = JlNativeApi.PreCall(1073);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		return obj;
	}

	/// <summary>把两幅图合成一幅两通道图，id 1074。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <returns>两通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>通道族分工、尺寸/类型约束、多通道图不能直接进灰度算子等要点见 <see cref="Compose3(JlImage,JlImage)"/>；
	///   本算子是同一族的 N=2 情形，<b>独立原生 id 1074</b>，通道顺序为 <c>this → image2</c>。</para>
	///   <para><b>典型用法</b>把两路特征并到一幅图里再做双通道判据：灰度 + <c>SobelAmp</c> 幅值、可见光 + 热成像。
	///   如果只是想让两幅图共用一次处理流程，用图像数组（<c>ConcatObj</c>）而不是加通道。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage amp = img.SobelAmp("sum", 3);
	///   using JlImage pair = img.Compose2(amp);
	///   int channels = pair.CountChannels();
	///   </code>
	///   <para><b>资源与坑</b>输入只读，返回新句柄。</para>
	/// </remarks>
	public JlImage Compose2(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1074);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Convert a seven-channel image into seven images.
	/// </summary>
	/// <param name="image2">Output image 2.</param>
	/// <param name="image3">Output image 3.</param>
	/// <param name="image4">Output image 4.</param>
	/// <param name="image5">Output image 5.</param>
	/// <param name="image6">Output image 6.</param>
	/// <param name="image7">Output image 7.</param>
	/// <returns>Output image 1.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 seven-channel 图像 为 seven 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>位姿表示与变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Decompose7(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5, out JlImage image6, out JlImage image7);
	///   </code>
	/// </remarks>
	public JlImage Decompose7(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5, out JlImage image6, out JlImage image7)
	{
		IntPtr proc = JlNativeApi.PreCall(1075);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		err = LoadNew(proc, 5, err, out image5);
		err = LoadNew(proc, 6, err, out image6);
		err = LoadNew(proc, 7, err, out image7);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a six-channel image into six images.
	/// </summary>
	/// <param name="image2">Output image 2.</param>
	/// <param name="image3">Output image 3.</param>
	/// <param name="image4">Output image 4.</param>
	/// <param name="image5">Output image 5.</param>
	/// <param name="image6">Output image 6.</param>
	/// <returns>Output image 1.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 six-channel 图像 为 six 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>位姿表示与变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Decompose6(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5, out JlImage image6);
	///   </code>
	/// </remarks>
	public JlImage Decompose6(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5, out JlImage image6)
	{
		IntPtr proc = JlNativeApi.PreCall(1076);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		err = LoadNew(proc, 5, err, out image5);
		err = LoadNew(proc, 6, err, out image6);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a five-channel image into five images.
	/// </summary>
	/// <param name="image2">Output image 2.</param>
	/// <param name="image3">Output image 3.</param>
	/// <param name="image4">Output image 4.</param>
	/// <param name="image5">Output image 5.</param>
	/// <returns>Output image 1.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 five-channel 图像 为 five 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>位姿表示与变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Decompose5(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5);
	///   </code>
	/// </remarks>
	public JlImage Decompose5(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5)
	{
		IntPtr proc = JlNativeApi.PreCall(1077);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		err = LoadNew(proc, 5, err, out image5);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a four-channel image into four images.
	/// </summary>
	/// <param name="image2">Output image 2.</param>
	/// <param name="image3">Output image 3.</param>
	/// <param name="image4">Output image 4.</param>
	/// <returns>Output image 1.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 four-channel 图像 为 four 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>位姿表示与变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Decompose4(out JlImage image2, out JlImage image3, out JlImage image4);
	///   </code>
	/// </remarks>
	public JlImage Decompose4(out JlImage image2, out JlImage image3, out JlImage image4)
	{
		IntPtr proc = JlNativeApi.PreCall(1078);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a three-channel image into three images.
	/// </summary>
	/// <param name="image2">Output image 2.</param>
	/// <param name="image3">Output image 3.</param>
	/// <returns>Output image 1.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 three-channel 图像 为 three 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>位姿表示与变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Decompose3(out JlImage image2, out JlImage image3);
	///   </code>
	/// </remarks>
	public JlImage Decompose3(out JlImage image2, out JlImage image3)
	{
		IntPtr proc = JlNativeApi.PreCall(1079);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a two-channel image into two images.
	/// </summary>
	/// <param name="image2">Output image 2.</param>
	/// <returns>Output image 1.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 two-channel 图像 为 two 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>位姿表示与变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Decompose2(out JlImage image2);
	///   </code>
	/// </remarks>
	public JlImage Decompose2(out JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1080);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Count channels of image.
	/// </summary>
	/// <returns>Number of channels.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Count channels 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CountChannels();
	///   </code>
	/// </remarks>
	public JlTuple CountChannels()
	{
		IntPtr proc = JlNativeApi.PreCall(1081);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Append additional matrices (channels) to the image.
	/// </summary>
	/// <param name="image">Image to be appended.</param>
	/// <returns>Image appended by Image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Append additional matrices (channels) to the image。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlImage obj = ...;
	///   var result = obj.AppendChannel(image);
	///   </code>
	/// </remarks>
	public JlImage AppendChannel(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(1082);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>取多通道图的通道（索引从 1 开始，可重排）。</summary>
	/// <param name="channel">通道索引。Default: 1</param>
	/// <returns>取出的通道图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1083。通道索引是<b>控制参数</b>（<c>Store(proc, 0, channel)</c>），
	///   不是图标对象；从 <b>1</b> 开始计数，写 0 不会在这里被拦下 [待实测：越界是报错还是回绕]。</para>
	///   <para><b>多索引</b>元组可一次给多个索引，本层仍只声明一路图标输出（<c>InitOCT(proc,1)</c>），
	///   因此结果应是<b>一幅按给定顺序重排的</b>多通道图 [待实测]；要拆成逐通道单图请用 <c>DecomposeN</c>
	///   或反复 <c>AccessChannel(1)</c>/<see cref="AppendChannel(JlImage)"/> 组合。这与
	///   <see cref="Compose3(JlImage,JlImage)"/> 互为逆操作。</para>
	///   <para><b>为什么常要用它</b>阈值、滤波、直方图这类灰度算子不接受多通道输入 [待实测]，
	///   彩色图做分割前先 <c>AccessChannel(1)</c> 取一个通道；要按加权亮度合成，则用
	///   <c>ChannelsToImage()</c> 拆成三幅单通道图后再调 <c>Rgb3ToGray(imageGreen, imageBlue)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose3(g, b);
	///   using JlImage red = rgb.AccessChannel(new JlTuple(1));      // 第 1 通道
	///   using JlImage swapped = rgb.AccessChannel(new JlTuple(3.0, 2.0, 1.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；取出的通道是否与原图共享像素内存本层无法判断 [待实测]，
	///   要改写像素先 <c>CopyImage()</c>。</para>
	/// </remarks>
	public JlImage AccessChannel(JlTuple channel)
	{
		IntPtr proc = JlNativeApi.PreCall(1083);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, channel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(channel);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>取一个通道（单索引版）。</summary>
	/// <param name="channel">通道索引，从 1 开始。Default: 1</param>
	/// <returns>该通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>1 起始计数、越界不校验、与 Compose/Decompose 的关系见 <see cref="AccessChannel(JlTuple)"/>：
	///   同一原生 id 1083，本版本 <c>StoreI</c> 只传一个索引，返回单通道图，是绝大多数场合的写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose2(g);
	///   using JlImage green = rgb.AccessChannel(2);
	///   </code>
	/// </remarks>
	public JlImage AccessChannel(int channel)
	{
		IntPtr proc = JlNativeApi.PreCall(1083);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, channel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Tile multiple image objects into a large image with explicit positioning information.
	/// </summary>
	/// <param name="offsetRow">Row coordinate of the upper left corner of the input images in the output image. Default: 0</param>
	/// <param name="offsetCol">Column coordinate of the upper left corner of the input images in the output image. Default: 0</param>
	/// <param name="row1">Row coordinate of the upper left corner of the copied part of the respective input image. Default: -1</param>
	/// <param name="col1">Column coordinate of the upper left corner of the copied part of the respective input image. Default: -1</param>
	/// <param name="row2">Row coordinate of the lower right corner of the copied part of the respective input image. Default: -1</param>
	/// <param name="col2">Column coordinate of the lower right corner of the copied part of the respective input image. Default: -1</param>
	/// <param name="width">Width of the output image. Default: 512</param>
	/// <param name="height">Height of the output image. Default: 512</param>
	/// <returns>Tiled output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Tile图像Offset。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.TileImagesOffset(0, 0, -1, -1, -1, -1, 512, 512);
	///   </code>
	/// </remarks>
	public JlImage TileImagesOffset(JlTuple offsetRow, JlTuple offsetCol, JlTuple row1, JlTuple col1, JlTuple row2, JlTuple col2, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1084);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, offsetRow);
		JlNativeApi.Store(proc, 1, offsetCol);
		JlNativeApi.Store(proc, 2, row1);
		JlNativeApi.Store(proc, 3, col1);
		JlNativeApi.Store(proc, 4, row2);
		JlNativeApi.Store(proc, 5, col2);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(offsetRow);
		JlNativeApi.UnpinTuple(offsetCol);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(col1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(col2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 `this`（图像对象集）里的每幅图按显式坐标拼贴到一张大图中，位置与裁取范围均以标量整数给定。
	/// </summary>
	/// <param name="offsetRow">该图左上角落在输出图中的行坐标（像素）。Default: 0</param>
	/// <param name="offsetCol">该图左上角落在输出图中的列坐标（像素）。Default: 0</param>
	/// <param name="row1">从该图上裁取的源区域左上角行坐标；填 -1 表示整幅拷入。Default: -1</param>
	/// <param name="col1">从该图上裁取的源区域左上角列坐标；填 -1 表示整幅拷入。Default: -1</param>
	/// <param name="row2">裁取源区域右下角行坐标（闭区间）。Default: -1</param>
	/// <param name="col2">裁取源区域右下角列坐标（闭区间）。Default: -1</param>
	/// <param name="width">输出大图宽度（像素）。Default: 512</param>
	/// <param name="height">输出大图高度（像素）。Default: 512</param>
	/// <returns>拼贴后的新图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本重载把 offsetRow/offsetCol 等六个定位参数按标量整数一次性写入原生侧（`StoreI`），对所有输入图共用同一组数值。原生算子 id 1084。</para>
	///   <para><b>约束或前提</b>`this` 须为含多幅图的图像对象集（iconc）才有拼贴意义，单幅图只会得到一张把该图放到指定位置的画布。源裁取参数任一为 -1 即视为整幅拷入；越界部分被丢弃，落在 width/height 之外的像素被裁掉。</para>
	///   <para><b>与相邻算子的取舍</b>当每幅图需要各自不同的落位/裁取范围时，改用 JlTuple 重载（可传入等长元组逐图给定）；本标量重载省去钉元组开销，适合整齐排布。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage tiled = img.TileImagesOffset(0, 0, -1, -1, -1, -1, 128, 128);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；`this` 在原生调用结束前不得 Dispose（代码末尾 GC.KeepAlive(this) 已保证）。</para>
	/// </remarks>
	public JlImage TileImagesOffset(int offsetRow, int offsetCol, int row1, int col1, int row2, int col2, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1084);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, offsetRow);
		JlNativeApi.StoreI(proc, 1, offsetCol);
		JlNativeApi.StoreI(proc, 2, row1);
		JlNativeApi.StoreI(proc, 3, col1);
		JlNativeApi.StoreI(proc, 4, row2);
		JlNativeApi.StoreI(proc, 5, col2);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 `this`（图像对象集）里的多幅图按网格拼成一张大图。
	/// </summary>
	/// <param name="numColumns">输出网格的列数（&gt;0）。Default: 1</param>
	/// <param name="tileOrder">排列方向："vertical" 逐列填满后再进下一列，"horizontal" 逐行填满后再进下一行。Default: "vertical"</param>
	/// <returns>拼贴后的新图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1085。tileOrder 以 `StoreS` 写字符串，numColumns 以 `StoreI` 写整数。</para>
	///   <para><b>约束或前提</b>`this` 内的各幅图必须宽高一致，且通道数一致，否则原生侧报错。输出图尺寸 = 单图尺寸 × 网格行列数；图数不能被 numColumns 整除时末尾留空。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐图自定义落位或裁取范围时改用 TileImagesOffset；本算子只做规整网格。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage tiled = img.TileImages(2, "horizontal");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；拼贴顺序依赖 `this` 内图像对象的既有次序，次序不稳会静默错位。</para>
	/// </remarks>
	public JlImage TileImages(int numColumns, string tileOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(1085);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numColumns);
		JlNativeApi.StoreS(proc, 1, tileOrder);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 `this`（图像对象集）中各图的所有通道拆成单通道灰度图，再按网格拼成一张大图。
	/// </summary>
	/// <param name="numColumns">输出网格的列数（&gt;0）。Default: 1</param>
	/// <param name="tileOrder">通道排列方向："vertical" 逐列，"horizontal" 逐行。Default: "vertical"</param>
	/// <returns>拼贴后的新图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1086。与 TileImages 的区别在于：这里被拼的是"通道"——每幅多通道图会被拆成多张单通道图后统一排布。</para>
	///   <para><b>约束或前提</b>`this` 内各图宽高必须一致。输出网格按通道总数铺放，图数不能被 numColumns 整除时末尾留空。</para>
	///   <para><b>与相邻算子的取舍</b>只想并排放整图（不拆通道）时用 TileImages；要看某彩色图各分量分布时用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage rgb = new JlImage("byte", 64, 64);
	///   JlImage tiled = rgb.TileChannels(3, "horizontal");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；通道次序沿用图像通道序（如 R、G、B），换序需先用通道重排类算子。</para>
	/// </remarks>
	public JlImage TileChannels(int numColumns, string tileOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(1086);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numColumns);
		JlNativeApi.StoreS(proc, 1, tileOrder);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把图像裁到恰好覆盖其当前 definition domain 的最小外接矩形。
	/// </summary>
	/// <returns>裁剪后的新图像句柄（尺寸可能变小，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1087。只依赖 domain，不看灰度值；domain 由先前 Threshold/ReduceDomain 等操作决定。</para>
	///   <para><b>约束或前提</b>若 domain 已是整幅图，输出与输入同尺寸（等价拷贝）。domain 为空/退化时结果尺寸随之坍缩，慎用。</para>
	///   <para><b>与相邻算子的取舍</b>想按固定坐标裁剪用 CropRectangle1/CropPart；想改尺寸重采样用 ChangeSize（若本库提供）；本算子是"跟着 domain 走"，适合处理已被非矩形区域限定过的图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage cut = img.CropDomain();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；裁剪后像素坐标原点已平移，此前记录的行/列坐标不能再套用。</para>
	/// </remarks>
	public JlImage CropDomain()
	{
		IntPtr proc = JlNativeApi.PreCall(1087);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按角点坐标从图像中裁出一个或多个矩形区域（各角点以元组逐矩形给定）。
	/// </summary>
	/// <param name="row1">各矩形左上角行坐标（像素，闭区间）。Default: 100</param>
	/// <param name="column1">各矩形左上角列坐标（像素，闭区间）。Default: 100</param>
	/// <param name="row2">各矩形右下角行坐标（须 ≥ row1）。Default: 200</param>
	/// <param name="column2">各矩形右下角列坐标（须 ≥ column1）。Default: 200</param>
	/// <returns>裁剪结果的新图像句柄；多矩形时返回图像对象集，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1088。四个元组须等长，逐个对应一个矩形；调用后逐一 UnpinTuple。</para>
	///   <para><b>约束或前提</b>所有矩形必须完全落在图像范围内（角点越界即原生报错），坐标以像素索引计、闭区间（右下角像素包含在内）。要裁"任意形状"不能靠本算子，它只处理轴对齐矩形。</para>
	///   <para><b>与相邻算子的取舍</b>已知左上角+宽高时改用 CropPart（免去 row2/col2 换算）；跟着 domain 走用 CropDomain。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple r1 = 10, c1 = 10, r2 = 50, c2 = 50;
	///   JlImage cut = img.CropRectangle1(r1, c1, r2, c2);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄（或对象集）需释放；每个输出矩形自带新的局部坐标原点。</para>
	/// </remarks>
	public JlImage CropRectangle1(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1088);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按角点坐标从图像中裁出一个矩形区域（标量整数重载）。
	/// </summary>
	/// <param name="row1">矩形左上角行坐标（像素，闭区间）。Default: 100</param>
	/// <param name="column1">矩形左上角列坐标（像素，闭区间）。Default: 100</param>
	/// <param name="row2">矩形右下角行坐标（须 ≥ row1）。Default: 200</param>
	/// <param name="column2">矩形右下角列坐标（须 ≥ column1）。Default: 200</param>
	/// <returns>裁剪后的新图像句柄（单幅，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1088，与元组重载同一算子；本重载以 `StoreI` 直写单矩形，无钉元组开销。</para>
	///   <para><b>约束或前提</b>矩形必须完全落在图像范围内且 row2≥row1、column2≥column1，坐标为闭区间的像素索引。只裁单矩形时用本重载，多矩形用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b>已知宽高改用 CropPart；跟随 domain 用 CropDomain。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage cut = img.CropRectangle1(10, 10, 50, 50);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出图自带局部坐标原点。</para>
	/// </remarks>
	public JlImage CropRectangle1(int row1, int column1, int row2, int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1088);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row1);
		JlNativeApi.StoreI(proc, 1, column1);
		JlNativeApi.StoreI(proc, 2, row2);
		JlNativeApi.StoreI(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按左上角+宽高从图像中裁出一个或多个矩形区域（各参数以元组逐区域给定）。
	/// </summary>
	/// <param name="row">各矩形左上角行坐标（像素）。Default: 100</param>
	/// <param name="column">各矩形左上角列坐标（像素）。Default: 100</param>
	/// <param name="width">各矩形宽度（像素，&gt;0）。Default: 128</param>
	/// <param name="height">各矩形高度（像素，&gt;0）。Default: 128</param>
	/// <returns>裁剪结果的新图像句柄；多区域时返回图像对象集，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1089。四个元组须等长，逐一对应一个裁剪窗口；调用后逐一 UnpinTuple。</para>
	///   <para><b>约束或前提</b>窗口须整体落在图像内：row+height-1、column+width-1 不得越过图像下边界，否则原生报错。尺寸以像素计。</para>
	///   <para><b>与相邻算子的取舍</b>习惯用右下角坐标时用 CropRectangle1；本重载适合"原点+尺寸"的表达。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple r = 10, c = 10, w = 41, h = 41;
	///   JlImage cut = img.CropPart(r, c, w, h);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄/对象集需释放；每个输出自带局部坐标原点。</para>
	/// </remarks>
	public JlImage CropPart(JlTuple row, JlTuple column, JlTuple width, JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(1089);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, width);
		JlNativeApi.Store(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(width);
		JlNativeApi.UnpinTuple(height);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按左上角+宽高从图像中裁出一个矩形区域（标量整数重载）。
	/// </summary>
	/// <param name="row">矩形左上角行坐标（像素）。Default: 100</param>
	/// <param name="column">矩形左上角列坐标（像素）。Default: 100</param>
	/// <param name="width">矩形宽度（像素，&gt;0）。Default: 128</param>
	/// <param name="height">矩形高度（像素，&gt;0）。Default: 128</param>
	/// <returns>裁剪后的新图像句柄（单幅，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1089，与元组重载同一算子；本重载以 `StoreI` 直写单窗口，无钉元组开销。</para>
	///   <para><b>约束或前提</b>窗口须整体落在图像内（row+height-1、column+width-1 不越界）。裁单个区域时用本重载。[待实测]</para>
	///   <para><b>与相邻算子的取舍</b>用右下角坐标表达时改用 CropRectangle1。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage cut = img.CropPart(10, 10, 41, 41);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出自带局部坐标原点。</para>
	/// </remarks>
	public JlImage CropPart(int row, int column, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1089);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把图像改成指定的 width×height：变大则补零边，变小则裁掉多余行列（不做插值重采样）。
	/// </summary>
	/// <param name="width">目标宽度（像素）。Default: 512</param>
	/// <param name="height">目标高度（像素）。Default: 512</param>
	/// <returns>新尺寸图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1090。它只做"裁剪或填充"，像素一一对应、绝不缩放内容。</para>
	///   <para><b>约束或前提</b>目标尺寸可大于或小于原图；补出来的区域灰度为 0。宽高为整数像素。</para>
	///   <para><b>与相邻算子的取舍</b>若要把内容按倍率缩放（真缩放像素网格）不能用本算子，需改用缩放/仿射类算子；本算子只用于凑齐统一尺寸或补边。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 300, 300);
	///   JlImage resized = img.ChangeFormat(512, 512);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；补零边会拉低整图统计量（如灰度均值），做特征前先想清楚。</para>
	/// </remarks>
	public JlImage ChangeFormat(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1090);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用给定的区域整体替换图像的 definition domain（不做与旧 domain 的交集）。
	/// </summary>
	/// <param name="newDomain">作为新 domain 的区域。</param>
	/// <returns>携带新 domain 的图像句柄（像素尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1091。图像宽高与像素数据保持不变，只换掉"哪些像素算有效"。newDomain 以 `Store` 作为句柄参数传入。</para>
	///   <para><b>约束或前提</b>newDomain 与图像须同一坐标系；区域超出图像范围的部分无效。想"在旧 domain 基础上再收窄"应用 ReduceDomain（取交集），本算子会丢弃旧 domain。</para>
	///   <para><b>与相邻算子的取舍</b>ReduceDomain=交集、ChangeDomain=替换、FullDomain=恢复全幅——按是否需要保留旧限制选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion dom = new JlRegion(10, 10, 100, 100);
	///   JlImage restricted = img.ChangeDomain(dom);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄与传入区域在原生调用结束前均不得释放（GC.KeepAlive 已保 this 与 newDomain）。</para>
	/// </remarks>
	public JlImage ChangeDomain(JlRegion newDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1091);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, newDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(newDomain);
		return obj;
	}

	/// <summary>
	///   把图像的 domain 收窄到与指定矩形的交集（图像像素尺寸不变，仅有效区缩小）。
	/// </summary>
	/// <param name="row1">矩形左上角行坐标（像素，闭区间）。Default: 100</param>
	/// <param name="column1">矩形左上角列坐标（像素，闭区间）。Default: 100</param>
	/// <param name="row2">矩形右下角行坐标。Default: 200</param>
	/// <param name="column2">矩形右下角列坐标。Default: 200</param>
	/// <returns>domain 收窄后的图像句柄（尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1093。等价于用矩形区域做一次 ReduceDomain：原 domain ∩ 矩形。</para>
	///   <para><b>约束或前提</b>矩形坐标以闭区间像素索引计。与 CropRectangle1 不同，本算子不裁掉矩形外的像素数据，只把它们标记为 domain 之外——宽高保持原样。</para>
	///   <para><b>与相邻算子的取舍</b>想真正改变图像尺寸去 CropRectangle1；只想限制后续统计/滤波作用范围用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage limited = img.Rectangle1Domain(20, 20, 120, 120);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；后续 Intensity/算子只在 domain 内统计。</para>
	/// </remarks>
	public JlImage Rectangle1Domain(int row1, int column1, int row2, int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1093);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row1);
		JlNativeApi.StoreI(proc, 1, column1);
		JlNativeApi.StoreI(proc, 2, row2);
		JlNativeApi.StoreI(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把图像的 domain 收窄到与给定区域的交集（尺寸不变，仅有效区缩小）。
	/// </summary>
	/// <param name="region">用于收窄的新 domain 区域。</param>
	/// <returns>domain 收窄后的图像句柄（尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1094。结果是"旧 domain ∩ region"，与 ChangeDomain 的"直接替换"相对。</para>
	///   <para><b>约束或前提</b>region 与图像同一坐标系；超出图像范围或落在旧 domain 外的部分被排除。多次 ReduceDomain 逐次累积收窄。</para>
	///   <para><b>与相邻算子的取舍</b>要整体替换 domain 用 ChangeDomain；要真正裁掉像素改变尺寸用 CropDomain/CropRectangle1；本算子只做交集且保留原尺寸。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion keep = new JlRegion(20, 20, 120, 120);
	///   JlImage limited = img.ReduceDomain(keep);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄与传入区域在原生调用结束前不得释放（GC.KeepAlive 已保 this 与 region）。</para>
	/// </remarks>
	public JlImage ReduceDomain(JlRegion region)
	{
		IntPtr proc = JlNativeApi.PreCall(1094);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   把图像的 domain 扩张到整幅（清除之前 ReduceDomain/Threshold 造成的有效区限制）。
	/// </summary>
	/// <returns>domain 恢复为全幅的图像句柄（尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1095。之后所有算子都会在全部像素上生效。</para>
	///   <para><b>约束或前提</b>不会恢复被 CropDomain 真正裁掉的像素——那只改了图像尺寸，不可逆；本算子只放开 domain。</para>
	///   <para><b>与相邻算子的取舍</b>想反向操作（收窄）用 ReduceDomain/Rectangle1Domain；想以当前 domain 外接矩形裁小图像用 CropDomain。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage full = img.FullDomain();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage FullDomain()
	{
		IntPtr proc = JlNativeApi.PreCall(1095);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   以区域形式取出图像当前的 definition domain。
	/// </summary>
	/// <returns>表示当前 domain 的新 JlRegion 句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1096，经 JlRegion.LoadNew 装载为区域句柄。未收窄过的图，其 domain 即整幅矩形。</para>
	///   <para><b>约束或前提</b>返回的是 domain（有效像素集合），不是阈值分割结果，也不是灰度值。</para>
	///   <para><b>与相邻算子的取舍</b>想按 domain 外接矩形裁图用 CropDomain；想直接拿分割结果用 Threshold。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion dom = img.GetDomain();
	///   </code>
	///   <para><b>资源与坑</b>返回新区域句柄需释放；它是快照，之后再改图像 domain 不会影响已取出的区域。</para>
	/// </remarks>
	public JlRegion GetDomain()
	{
		IntPtr proc = JlNativeApi.PreCall(1096);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   在图像中定位矫正网格（rectification grid）区域，参数以元组给定。
	/// </summary>
	/// <param name="minContrast">识别网格线所需的最小对比度（灰度级）。Default: 8.0</param>
	/// <param name="radius">所用圆形结构元的半径（像素）。Default: 7.5</param>
	/// <returns>包含网格区域的新 JlRegion 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1104。用于标定/畸变矫正流程中第一步找出规则网格，供后续 FindGrid/CropGrid 类算子使用。</para>
	///   <para><b>约束或前提</b>输入应为含规则网格图案的单通道灰度图；minContrast 过大找不到、过小易把噪声当网格。radius 应匹配网格线粗细。</para>
	///   <para><b>与相邻算子的取舍</b>本算子只给"网格在哪"，逐点连接与映射由 ConnectGridPoints/GenGridRectificationMap 完成。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple contrast = 8.0, rad = 7.5;
	///   JlRegion grid = img.FindRectificationGrid(contrast, rad);
	///   </code>
	///   <para><b>资源与坑</b>返回新区域句柄需释放；元组重载调用后逐一 UnpinTuple。</para>
	/// </remarks>
	public JlRegion FindRectificationGrid(JlTuple minContrast, JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1104);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, minContrast);
		JlNativeApi.Store(proc, 1, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minContrast);
		JlNativeApi.UnpinTuple(radius);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   在图像中定位矫正网格（rectification grid）区域，参数以标量给定。
	/// </summary>
	/// <param name="minContrast">识别网格线所需的最小对比度（灰度级）。Default: 8.0</param>
	/// <param name="radius">所用圆形结构元的半径（像素）。Default: 7.5</param>
	/// <returns>包含网格区域的新 JlRegion 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1104，与元组重载同一算子；本重载以 `StoreD` 直写双精度，无钉元组开销。</para>
	///   <para><b>约束或前提</b>输入应为含规则网格图案的单通道灰度图。minContrast 过大漏检、过小误检噪声；radius 应匹配线宽。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐网格不同参数时改用元组重载；本算子只做单组参数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlRegion grid = img.FindRectificationGrid(8.0, 7.5);
	///   </code>
	///   <para><b>资源与坑</b>返回新区域句柄需释放。</para>
	/// </remarks>
	public JlRegion FindRectificationGrid(double minContrast, double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1104);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, minContrast);
		JlNativeApi.StoreD(proc, 1, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把矫正网格的网格点连成轮廓线（各点坐标与参数以元组给定），返回 XLD 轮廓。
	/// </summary>
	/// <param name="row">各网格点的行坐标。</param>
	/// <param name="column">各网格点的列坐标。</param>
	/// <param name="sigma">所用高斯核的宽度（越大连接越保守）。Default: 0.9</param>
	/// <param name="maxDist">连线相对网格点的最大偏离距离（像素）。Default: 5.5</param>
	/// <returns>连接线的新 JlXLD 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1106。row/column 通常来自网格点提取算子的输出，二者长度必须一致。</para>
	///   <para><b>约束或前提</b>点集须构成规则网格拓扑；sigma/maxDist 共同决定哪些相邻点被连起来，配错会漏连或错连。</para>
	///   <para><b>与相邻算子的取舍</b>本算子产出"连线"，是 GenGridRectificationMap 的输入；不做映射本身。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlTuple sigma = 0.9, maxDist = 5.5;
	///   JlXLD lines = img.ConnectGridPoints(row, col, sigma, maxDist);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；元组重载调用后逐一 UnpinTuple。JlTuple 构造签名以本仓库实际为准 [待实测]。</para>
	/// </remarks>
	public JlXLD ConnectGridPoints(JlTuple row, JlTuple column, JlTuple sigma, JlTuple maxDist)
	{
		IntPtr proc = JlNativeApi.PreCall(1106);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, sigma);
		JlNativeApi.Store(proc, 3, maxDist);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(sigma);
		JlNativeApi.UnpinTuple(maxDist);
		err = JlXLD.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把矫正网格的网格点连成轮廓线，sigma 以整数、maxDist 以标量给定。
	/// </summary>
	/// <param name="row">各网格点的行坐标。</param>
	/// <param name="column">各网格点的列坐标。</param>
	/// <param name="sigma">所用高斯核宽度（此重载取整，Default: 0.9 会被截为 0）。</param>
	/// <param name="maxDist">连线相对网格点的最大偏离距离（像素）。Default: 5.5</param>
	/// <returns>连接线的新 JlXLD 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1106，与元组重载同一算子；row/column 仍以 `Store` 钉元组，sigma 用 `StoreI`、maxDist 用 `StoreD` 直写。</para>
	///   <para><b>约束或前提</b>sigma 是整数形参，无法表达 0.9 这类小数默认值——要精确控制核宽请改用全元组重载。</para>
	///   <para><b>与相邻算子的取舍</b>需要小数 sigma 时用 <see cref="ConnectGridPoints(JlTuple,JlTuple,JlTuple,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlXLD lines = img.ConnectGridPoints(row, col, 1, 5.5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；row/column 逐一 UnpinTuple。</para>
	/// </remarks>
	public JlXLD ConnectGridPoints(JlTuple row, JlTuple column, int sigma, double maxDist)
	{
		IntPtr proc = JlNativeApi.PreCall(1106);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, sigma);
		JlNativeApi.StoreD(proc, 3, maxDist);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlXLD.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   依据规则网格点计算畸变图与矫正图之间的映射，rotation 以元组给定。
	/// </summary>
	/// <param name="connectingLines">网格点连线轮廓（ConnectGridPoints 的输出）。</param>
	/// <param name="meshes">输出的网格单元轮廓。</param>
	/// <param name="gridSpacing">矫正图中网格点间距（像素）；填 0 表示由算子自动推断。Default: 0</param>
	/// <param name="rotation">点网格的旋转，以元组给定。Default: "auto"</param>
	/// <param name="row">网格点行坐标。</param>
	/// <param name="column">网格点列坐标。</param>
	/// <param name="mapType">映射类型："bilinear"/"inverse_affine"/"linear_trans"。Default: "bilinear"</param>
	/// <returns>承载映射数据的新图像句柄（供网格矫正类映射应用），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1107。返回两样东西：函数返回 JlImage（映射图），并通过 out 参数返回 meshes（JlXLD）。</para>
	///   <para><b>约束或前提</b>connectingLines/row/column 必须来自同一网格检测结果且相互一致；gridSpacing=0 才走自动推断。本库不提供 3D/显示族，此映射仅用于图像域的网格矫正。</para>
	///   <para><b>与相邻算子的取舍</b>rotation 想用字符串常量（如 "auto"）改用 <see cref="GenGridRectificationMap(JlXLD,out JlXLD,int,string,JlTuple,JlTuple,string)"/>；本重载支持逐点数值旋转。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlXLD lines = img.ConnectGridPoints(row, col, 1, 5.5);
	///   JlTuple rot = "auto";
	///   JlImage map = img.GenGridRectificationMap(lines, out JlXLD meshes, 0, rot, row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回的映射图与 out meshes 都是新句柄，均需释放；connectingLines 由调用方持有至调用结束（GC.KeepAlive 已保）。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlXLD connectingLines, out JlXLD meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, connectingLines);
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
		err = LoadNew(proc, 1, err, out var obj);
		err = JlXLD.LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(connectingLines);
		return obj;
	}

	/// <summary>
	///   依据规则网格点计算畸变图与矫正图之间的映射，rotation 以字符串常量给定。
	/// </summary>
	/// <param name="connectingLines">网格点连线轮廓（ConnectGridPoints 的输出）。</param>
	/// <param name="meshes">输出的网格单元轮廓。</param>
	/// <param name="gridSpacing">矫正图中网格点间距（像素）；填 0 表示自动推断。Default: 0</param>
	/// <param name="rotation">点网格旋转："auto" 自动判定。Default: "auto"</param>
	/// <param name="row">网格点行坐标。</param>
	/// <param name="column">网格点列坐标。</param>
	/// <param name="mapType">映射类型："bilinear"/"inverse_affine"/"linear_trans"。Default: "bilinear"</param>
	/// <returns>承载映射数据的新图像句柄（供网格矫正类映射应用），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1107，与元组重载同一算子；本重载 rotation 以 `StoreS` 写字符串。返回 JlImage 映射图，out 参数 meshes 为 JlXLD。</para>
	///   <para><b>约束或前提</b>connectingLines/row/column 须来自同一网格检测且相互一致；gridSpacing=0 才走自动推断。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐点数值旋转时改用 <see cref="GenGridRectificationMap(JlXLD,out JlXLD,int,JlTuple,JlTuple,JlTuple,string)"/>；本重载适合用 "auto" 一把梭。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlXLD lines = img.ConnectGridPoints(row, col, 1, 5.5);
	///   JlImage map = img.GenGridRectificationMap(lines, out JlXLD meshes, 0, "auto", row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>映射图与 out meshes 均为新句柄需释放；connectingLines 保持到调用结束。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlXLD connectingLines, out JlXLD meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, connectingLines);
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
		err = LoadNew(proc, 1, err, out var obj);
		err = JlXLD.LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(connectingLines);
		return obj;
	}

	/// <summary>
	///   计算每个矩形窗口内灰度的标准差，生成局部标准差图。
	/// </summary>
	/// <param name="width">计算标准差的窗口宽（像素）。Default: 11</param>
	/// <param name="height">计算标准差的窗口高（像素）。Default: 11</param>
	/// <returns>承载各窗标准差的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1342。输出像素是"邻域纹理/噪声强度"，不是灰度本身；窗越大越平滑、越偏低频结构。</para>
	///   <para><b>约束或前提</b>输入应为单通道图。输出数值范围可能超过原图类型位深，用于二次 Threshold 时注意量纲。</para>
	///   <para><b>与相邻算子的取舍</b>要看"平均亮度"用 MeanImage；要看"信息量/混乱度"用 EntropyImage；标准差更擅长定位纹理边界。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage dev = img.DeviationImage(11, 11);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；结果在图像四缘受窗口越界处理影响。</para>
	/// </remarks>
	public JlImage DeviationImage(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1342);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   计算矩形窗口内灰度分布的信息熵，生成局部熵图。
	/// </summary>
	/// <param name="width">计算熵的窗口宽（像素）。Default: 9</param>
	/// <param name="height">计算熵的窗口高（像素）。Default: 9</param>
	/// <returns>承载各窗灰度熵的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1343。熵高代表窗内灰度分布杂乱（纹理/边缘），熵低代表平坦。</para>
	///   <para><b>约束或前提</b>单通道输入。窗尺寸决定统计的灰度直方图跨度，窗太小估计噪声大。</para>
	///   <para><b>与相邻算子的取舍</b>只要区分"平坦 vs 复杂"且对纹理方向不敏感时，熵比标准差更聚焦分布形态；两者常互为备选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage ent = img.EntropyImage(9, 9);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出为统计量，非 0-255 直观灰度。</para>
	/// </remarks>
	public JlImage EntropyImage(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1343);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对图像做各向同性扩散（等价于反复高斯平滑），不区分边缘方向。
	/// </summary>
	/// <param name="sigma">高斯分布的标准差（越大单次平滑越强）。Default: 1.0</param>
	/// <param name="iterations">扩散迭代次数（越多累计平滑越强）。Default: 10</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1344。sigma 用 `StoreD`、iterations 用 `StoreI` 写入。</para>
	///   <para><b>约束或前提</b>各向同性意味着边缘也会被抹平（不像各向异性扩散保边）。总平滑强度大致随 sigma×iterations 增长。</para>
	///   <para><b>与相邻算子的取舍</b>需要保边去噪改用各向异性/非局部扩散类算子；只想去噪又不介意糊边时本算子简单可用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage smooth = img.IsotropicDiffusion(1.0, 10);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；iterations 过大耗时上升且细节全丢。</para>
	/// </remarks>
	public JlImage IsotropicDiffusion(double sigma, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(1344);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   用指定滤波器对图像做平滑。
	/// </summary>
	/// <param name="filter">滤波器类型："deriche2"/"gauss"/"binomial"/"mean"。Default: "deriche2"</param>
	/// <param name="alpha">滤波参数：deriche2/binomial 下越小平滑越强；但 gauss 下语义相反（越小越弱）。Default: 0.5</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1346。filter 以 `StoreS` 写字符串，alpha 以 `StoreD` 写。</para>
	///   <para><b>约束或前提</b>alpha 的调参方向依赖 filter：切到 "gauss" 时别沿用 "deriche2" 的直觉，否则平滑力度会事与愿违。</para>
	///   <para><b>与相邻算子的取舍</b>deriche 类便于后续求导/边缘，gauss 是通用低通；只想快速去椒盐点用中值类而非本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage sm = img.SmoothImage("gauss", 1.5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage SmoothImage(string filter, double alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(1346);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 sigma 滤波器做非线性平滑：只把与窗均值偏差不超过 sigma 的像素纳入平均。
	/// </summary>
	/// <param name="maskHeight">滤波掩膜的高度（行数）。Default: 5</param>
	/// <param name="maskWidth">滤波掩膜的宽度（列数）。Default: 5</param>
	/// <param name="sigma">允许并入平均的最大灰度偏差（灰度级）。Default: 3</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1347。形参序是"先高后宽"（maskHeight 在前），与直觉的宽×高相反，别写反。</para>
	///   <para><b>约束或前提</b>sigma 小则抗噪弱但保边强，大则接近普通均值滤波。与 GaussFilter 相比它能把椒盐离群点排除在均值外。</para>
	///   <para><b>与相邻算子的取舍</b>要更强保边去噪用中值/截尾均值；只要各向同性模糊用高斯/SmoothImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage sm = img.SigmaImage(5, 5, 3);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；三参数均为整数，掩膜尺寸直接决定成本。</para>
	/// </remarks>
	public JlImage SigmaImage(int maskHeight, int maskWidth, int sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1347);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.StoreI(proc, 2, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   取掩膜内最大值与最小值的平均（中值程滤波），margin 以元组给定。
	/// </summary>
	/// <param name="mask">其区域作为滤波掩膜的图像。</param>
	/// <param name="margin">边界处理方式，以元组传入。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1348。输出 = (窗内最大 + 窗内最小)/2，介于最小/最大之间，能压掉极端噪声同时较好保留台阶边缘。</para>
	///   <para><b>约束或前提</b>mask 是 iconc 区域输入（`Store(proc,2,mask)`），其形状决定滤波邻域；margin 元组重载以 `Store`+`UnpinTuple` 处理。</para>
	///   <para><b>与相邻算子的取舍</b>要更稳的抗椒盐用中值/截尾均值；要普通模糊用均值/高斯。中值程对"平台+细线"结构边缘位移小。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   JlTuple margin = "mirrored";
	///   JlImage outImg = img.MidrangeImage(mask, margin);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；掩膜区域由调用方释放。</para>
	/// </remarks>
	public JlImage MidrangeImage(JlRegion mask, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1348);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.Store(proc, 0, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>
	///   取掩膜内最大值与最小值的平均（中值程滤波），margin 以字符串给定。
	/// </summary>
	/// <param name="mask">其区域作为滤波掩膜的图像。</param>
	/// <param name="margin">边界处理方式："mirrored"/"reduced"/"bound"/"cyclic"。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1348，与元组重载同一算子；本重载 margin 以 `StoreS` 写字符串，无钉元组开销。</para>
	///   <para><b>约束或前提</b>mask 是 iconc 区域输入。margin 决定图像四缘如何延拓，"reduced" 会缩短有效域。</para>
	///   <para><b>与相邻算子的取舍</b>单值 margin 用本重载；需要元组化传参才用 <see cref="MidrangeImage(JlRegion,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   JlImage outImg = img.MidrangeImage(mask, "mirrored");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；掩膜区域由调用方释放。</para>
	/// </remarks>
	public JlImage MidrangeImage(JlRegion mask, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1348);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreS(proc, 0, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>截尾均值滤波：掩膜内排序后取中间若干像素求平均，原生算子 id 1349，margin 以元组传入。</summary>
	/// <param name="mask">其区域作为滤波掩膜的图像。</param>
	/// <param name="number">参与平均的像素个数。典型值是掩膜面积的一半。Default: 5</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>掩膜是 iconc 区域输入（<c>Store(proc, 2, mask)</c>），窗内灰度排序后只把<b>中间</b>
	///   <paramref name="number"/> 个像素求平均，两端极端值全部截掉。定位介于 <see cref="MedianImage(string,int,JlTuple)"/>
	///   与 <see cref="MeanImage(int,int)"/> 之间：和均值滤波相比对离群点免疫（亮点/暗点进不了平均），
	///   和中值滤波相比平坦区输出的是平均值而不是"择一"的某个像素值，大窗下不会出现中值特有的块状纹理退化，
	///   同时边缘位置像中值一样基本不漂移。</para>
	///   <para><b>关键约束</b><paramref name="number"/> 不得超过掩膜像素数——上限由 <paramref name="mask"/> 面积决定，
	///   换掩膜必须重算该值 [待实测：越界行为]。它是 <c>StoreI</c> 的 <c>int</c> 像素计数，不是百分比，
	///   也不能表达"截尾比例"这类语义；<paramref name="number"/> 等于掩膜面积时退化为普通均值（截不到任何端）。</para>
	///   <para><b>与相邻算子的取舍</b>只去椒盐点、平坦区粗糙无所谓 → <see cref="MedianImage(string,int,JlTuple)"/>（更快）；
	///   纯平滑、噪声不极端 → <see cref="MeanImage(int,int)"/>；传感器坏点/灰尘亮斑会污染 <c>Intensity</c> 类量测 →
	///   本算子。截尾均值比两者都贵，大图先 <c>ReduceDomain</c> 限定处理域。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 元组版 <c>Store</c>+<c>UnpinTuple</c>，多值语义本层未体现 [待实测]；
	///   单值用 <see cref="TrimmedMean(JlRegion,int,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);   // 4x4 共 16 像素
	///   using JlImage tm = img.TrimmedMean(mask, 8, new JlTuple("mirrored"));
	///   </code>
	///   <para><b>资源与坑</b>输出新图像需释放；掩膜区域同样由调用方释放；边界环带的 "mirrored" 延拓只影响四周一圈，
	///   但量测 ROI 贴图像边缘时该环带误差会直接进入统计值。</para>
	/// </remarks>
	public JlImage TrimmedMean(JlRegion mask, int number, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1349);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, number);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>截尾均值滤波（margin 以字符串传入）。</summary>
	/// <param name="mask">滤波掩膜区域。</param>
	/// <param name="number">参与平均的像素个数。Default: 5</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>"掩膜内排序、平均中间 number 个"的机制与 number/掩膜面积的约束见
	///   <see cref="TrimmedMean(JlRegion,int,JlTuple)"/>：同一原生 id 1349，本版本 <c>StoreS</c> 直写
	///   <paramref name="margin"/>，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   using JlImage tm = img.TrimmedMean(mask, 8, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage TrimmedMean(JlRegion mask, int number, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1349);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, number);
		JlNativeApi.StoreS(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>可分离中值滤波：横竖两次一维中值近似大矩形窗中值，原生算子 id 1350，margin 以元组传入。</summary>
	/// <param name="maskWidth">秩掩膜宽度，单位是像素。Default: 25</param>
	/// <param name="maskHeight">秩掩膜高度，单位是像素。Default: 25</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>中值滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先以 <c>maskWidth×1</c> 的水平窗做一次一维中值，再以 <c>1×maskHeight</c> 的竖直窗做一次，
	///   用两次一维运算逼近 <see cref="MedianImage(string,int,string)"/> 在 <c>maskWidth×maskHeight</c> 矩形窗上的严格中值
	///   （两个尺寸都是 <c>StoreI</c> 的 <c>int</c>，单位是像素）。默认 25×25：这种大窗全中值逐窗排序极其慢，
	///   分离方案把排序规模从 625 降到 25+25 [待实测：实际加速幅度]。</para>
	///   <para><b>与 MedianImage 的取舍</b>近似是有代价的：两遍滤波去除的像素集合不同，结果<b>不是任何窗的真中值</b>——
	///   对角/斜向细线经过两遍后比全中值更容易被削断，接近 min/max 组合；而水平、垂直边缘与条纹噪声保留得好。
	///   小窗（半径 ≤4 像素量级）直接用 <c>MedianImage</c>/<c>MedianRect</c>，没必要分离；
	///   大窗压低频噪声或条纹背景才用本算子，且结构以横平竖直为主。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 元组版 <c>Store</c>+<c>UnpinTuple</c>，多值语义 [待实测]，单值用
	///   <see cref="MedianSeparate(int,int,string)"/>；窗宽高的偶数与 ≤0 行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage sep = img.MedianSeparate(15, 15, new JlTuple("mirrored"));
	///   </code>
	///   <para><b>资源与坑</b>输出新图像需释放；多通道输入是否逐通道独立滤波 [待实测]。
	///   两次滤波叠加的偏移会把细结构削得比预期多，标定滤波强度时用真图上的最细结构试。</para>
	/// </remarks>
	public JlImage MedianSeparate(int maskWidth, int maskHeight, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1350);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.Store(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>可分离中值滤波（margin 以字符串传入）。</summary>
	/// <param name="maskWidth">秩掩膜宽度，单位是像素。Default: 25</param>
	/// <param name="maskHeight">秩掩膜高度，单位是像素。Default: 25</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>中值滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>两遍一维中值的近似性质与和 <c>MedianImage</c> 的取舍见
	///   <see cref="MedianSeparate(int,int,JlTuple)"/>：同一原生 id 1350，本版本 <c>StoreS</c> 直写
	///   <paramref name="margin"/>，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage sep = img.MedianSeparate(25, 25, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage MedianSeparate(int maskWidth, int maskHeight, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1350);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreS(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用矩形掩膜做严格中值滤波，原生算子 id 1351。
	/// </summary>
	/// <param name="maskWidth">矩形掩膜的宽（像素）。Default: 15</param>
	/// <param name="maskHeight">矩形掩膜的高（像素）。Default: 15</param>
	/// <returns>中值滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对 maskWidth×maskHeight 矩形窗内全部像素排序取中值，去除小于掩膜的孤立亮/暗点，阶跃边缘位置基本不动。</para>
	///   <para><b>约束或前提</b>本算子无边界参数，四缘环带处理由原生决定 [待实测]。大窗（如 15×15=225 像素排序）很慢。</para>
	///   <para><b>与相邻算子的取舍</b>要圆形/自定义掩膜或可控边界用 <see cref="MedianImage(string,int,string)"/>；只想横竖两遍快速近似用 <see cref="MedianSeparate(int,int,string)"/>（非严格中值但更快）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage med = img.MedianRect(5, 5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；密集高对比纹理会被中值"择一"，之后别再做方向量测。</para>
	/// </remarks>
	public JlImage MedianRect(int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(1351);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>中值滤波（可选掩膜形状），边界处理以元组传入。</summary>
	/// <param name="maskType">掩膜类型。Default: "circle"</param>
	/// <param name="radius">掩膜半径。Default: 1</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1352。半径 <paramref name="radius"/> 的掩膜内取中值：
	///   椒盐点（面积小于掩膜一半的孤立亮/暗点）被直接替换掉，而<b>阶跃边缘的位置基本不动</b>——
	///   这是它相对 <see cref="MeanImage(int,int)"/> 的核心优势：均值滤波会把边缘线性拖糊，
	///   紧接着做 <c>Threshold</c> 时边缘像素会被"半收"，宽度测量随之漂移。</para>
	///   <para><b>尺寸怎么定</b>中值滤波只能去掉<b>小于掩膜</b>的结构：<paramref name="radius"/>=1 的圆掩膜覆盖 5 像素，
	///   只能去单点噪声；掩膜再大就开始吃掉细线、窄脊（例如字符笔画），表现为断笔。
	///   <see cref="MedianSeparate(int,int,string)"/> 用横竖两次小掩膜代替大掩膜，能保住线状结构，代价是不再是严格中值。</para>
	///   <para><b>边界</b><paramref name="margin"/> 是本族少见的显式边界参数：默认 "mirrored"（镜像延拓），
	///   其余可取值由原生决定 [待实测]。它只影响图像四周 <paramref name="radius"/> 宽的环带，
	///   但该环带内的灰度会系统性偏离中心统计值——量测 ROI 贴到图像边缘时误差会体现在 <c>Intensity</c> 上。</para>
	///   <para><b>与相邻算子的取舍</b>要压亮毛刺而保留趋势 → <see cref="GrayOpeningRect(int,int)"/>；
	///   要按窗内第 k 小取值（比中值更极端）→ <see cref="RankImage(JlRegion,int,string)"/>/<see cref="RankRect(int,int,int)"/>；
	///   要各向同性平滑、不在乎边缘位置 → <see cref="GaussImage(int)"/>。中值滤波在<b>密集高对比纹理</b>上会把纹理"择一"，
	///   纹理方向信息丢失，此时不要用中值预处理再做方向量测。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 接受元组，多值语义本层无法判断 [待实测]；
	///   单值请用 <see cref="MedianImage(string,int,string)"/>（字符串直传，无固定/解固定）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage med = img.MedianImage("circle", 1, new JlTuple("mirrored"));
	///   using JlRegion parts = med.Threshold(128.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>掩膜类型/半径是 <c>StoreS</c>/<c>StoreI</c> 控制参数；输出新句柄需释放；
	///   <c>radius</c> 为 <c>int</c>，需要半像素或大窗口时改用 <see cref="MedianRect(int,int)"/> 或 Rank 族 [待实测]。</para>
	/// </remarks>
	public JlImage MedianImage(string maskType, int radius, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1352);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.Store(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>中值滤波（边界处理以字符串传入）。</summary>
	/// <param name="maskType">掩膜类型。Default: "circle"</param>
	/// <param name="radius">掩膜半径。Default: 1</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>算法、掩膜尺寸与边界环带的注意事项见 <see cref="MedianImage(string,int,JlTuple)"/>：
	///   同一原生 id 1352，本版本 <c>StoreS</c> 直写 <paramref name="margin"/>，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage med = img.MedianImage("circle", 2, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage MedianImage(string maskType, int radius, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1352);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.StoreS(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用不同秩掩膜做加权中值滤波，原生算子 id 1353。
	/// </summary>
	/// <param name="maskType">中值掩膜类型："all"/"inner"/"outer"/"border"（决定各像素的加权方式）。Default: "inner"</param>
	/// <param name="maskSize">掩膜尺寸（奇数，像素）。Default: 3</param>
	/// <returns>加权中值滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对窗内像素按 maskType 指定的权重重排后取加权中值，比标准中值对边缘/线条有不同保留特性。maskType 以 `StoreS` 写、maskSize 以 `StoreI` 写。</para>
	///   <para><b>约束或前提</b>maskSize 应为奇数 [待实测：偶数行为]。四种 maskType 的确切权重定义由原生决定，换类型结果差异明显，需用真图标定。</para>
	///   <para><b>与相邻算子的取舍</b>普通去椒盐优先 <see cref="MedianRect(int,int)"/>/<see cref="MedianImage(string,int,string)"/>；本算子用于需要偏向保留中心/边缘权重的场合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage med = img.MedianWeighted("inner", 3);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MedianWeighted(string maskType, int maskSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1353);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, maskSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗排序（rank）滤波，id 1354。</summary>
	/// <param name="maskWidth">窗宽。Default: 15</param>
	/// <param name="maskHeight">窗高。Default: 15</param>
	/// <param name="rank">取窗内第几小的值。Default: 5</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把窗内 <c>maskWidth×maskHeight</c> 个灰度排序后取第 <paramref name="rank"/> 个。
	///   默认参数是 15×15=225 个值里取第 5 小，等于"接近最小值滤波"：亮噪声被压掉、暗结构保留；
	///   <c>rank ≈ 窗面积/2</c> 时退化为中值滤波，<c>rank = 窗面积</c> 时就是最大值滤波（≈ <see cref="GrayDilationRect(int,int)"/>）。</para>
	///   <para><b>关键差异：本重载没有边界参数</b>与 <see cref="RankImage(JlRegion,int,string)"/> 不同，
	///   <c>RankRect</c> 的参数只有三个 <c>int</c>（<c>StoreI</c>），边缘环带如何处理由原生决定 [待实测]，
	///   不能照搬 MedianImage 的 "mirrored" 设定。要求边界可控时改用 <c>RankImage</c> 并自己给掩膜区域。</para>
	///   <para><b>坑</b><paramref name="rank"/> 超出窗面积、或 ≤0 时本层不校验 [待实测]；
	///   rank 滤波是最慢的一类空域滤波（每窗排序），大图上先 <c>ReduceDomain</c> 缩小处理域；
	///   与 <c>GrayOpeningRect</c> 相比：rank 会同时改动一大片区域的灰度（不只是毛刺），别当通用去噪用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage darkOnly = img.RankRect(15, 15, 5);          // 接近最小值：压掉亮噪点
	///   using JlRegion bright = darkOnly.Threshold(180.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>单路图像输出；输入不变。</para>
	/// </remarks>
	public JlImage RankRect(int maskWidth, int maskHeight, int rank)
	{
		IntPtr proc = JlNativeApi.PreCall(1354);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreI(proc, 2, rank);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>任意形状掩膜的排序滤波，边界处理以元组传入（id 1355）。</summary>
	/// <param name="mask">滤波掩膜区域。</param>
	/// <param name="rank">取掩膜内第几小的值。Default: 5</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1355。掩膜是<b>图标输入</b>（区域，<c>Store(proc, 2, mask)</c>），
	///   <paramref name="rank"/> 与 <paramref name="margin"/> 分别占控制槽位 0、1。
	///   排序范围是<b>掩膜覆盖的像素集合</b>，不是外接矩形：用细长/环形掩膜可以做方向性 rank 滤波，
	///   这是它相对 <see cref="RankRect(int,int,int)"/> 的唯一实质优势。</para>
	///   <para><b>rank 的上限由掩膜决定</b>合法范围是掩膜的像素个数 [待实测：越界行为]，
	///   所以换掩膜后 <paramref name="rank"/> 必须重算，沿用上一次的数值会得到含义完全不同的滤波强度。
	///   排序位置是第几<b>小</b>：小 rank 压亮、大 rank 压暗，中位附近约等于中值滤波。</para>
	///   <para><b>与相邻算子的取舍</b>矩形窗且不在乎边界 → <see cref="RankRect(int,int,int)"/>（更快、参数更少）；
	///   只要均值 → <see cref="MeanImageShape(JlRegion)"/>；要去椒盐且掩膜是圆/方 → <see cref="MedianImage(string,int,string)"/>。
	///   掩膜像素数很少（如 5 个）时 rank 滤波等于最小值滤波，会把亮结构整体抹掉，不要拿它做"温和去噪"。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 元组版多值语义未在本层体现 [待实测]，单值请用
	///   <see cref="RankImage(JlRegion,int,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 5.0, 5.0);       // (0,0)-(5,5) 共 36 像素的矩形掩膜
	///   using JlImage filtered = img.RankImage(mask, 5, new JlTuple("mirrored"));
	///   </code>
	///   <para><b>资源与坑</b>掩膜只读、需调用方自行释放（代码对 <c>this</c> 与 <paramref name="mask"/> 都 <c>GC.KeepAlive</c>）；
	///   掩膜若带"洞"或不连通，排序集合按掩膜实际像素计 [待实测]。</para>
	/// </remarks>
	public JlImage RankImage(JlRegion mask, int rank, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1355);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, rank);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>任意形状掩膜排序滤波（边界处理以字符串传入）。</summary>
	/// <param name="mask">滤波掩膜区域。</param>
	/// <param name="rank">取掩膜内第几小。Default: 5</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>掩膜决定排序集合、rank 上限随掩膜变化等要点见 <see cref="RankImage(JlRegion,int,JlTuple)"/>：
	///   同一原生 id 1355，本版本 <c>StoreS</c> 直写 <paramref name="margin"/>，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 7.0, 7.0);
	///   using JlImage filtered = img.RankImage(mask, 5, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage RankImage(JlRegion mask, int rank, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1355);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, rank);
		JlNativeApi.StoreS(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>
	///   灰度开/中值/闭一体化滤波（连续可调），边界处理以元组传入。原生算子 id 1356。
	/// </summary>
	/// <param name="maskType">掩膜形状："circle" 或 "rect"。Default: "circle"</param>
	/// <param name="radius">滤波掩膜半径（像素）。Default: 1</param>
	/// <param name="modePercent">模式：0=灰度开运算，50=中值，100=灰度闭运算，中间为插值。Default: 10</param>
	/// <param name="margin">边界处理方式，以元组传入。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>modePercent 在开运算（去亮结构/毛刺）→中值→闭运算（去暗结构）之间连续取值，一个算子覆盖三种灰度形态学。</para>
	///   <para><b>约束或前提</b>maskType="rect" 时以 radius 定方形窗（如需各向不同宽高需换算子）[待实测]。margin 元组重载调用后 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>只要纯中值用 MedianImage/MedianRect；只要纯开/闭用 GrayOpening/GrayClosing 族；本算子适合"偏开一点/偏闭一点"的折中滤噪。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple margin = "mirrored";
	///   JlImage f = img.DualRank("circle", 1, 10, margin);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；modePercent 越出 0..100 的行为未定义 [待实测]。</para>
	/// </remarks>
	public JlImage DualRank(string maskType, int radius, int modePercent, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1356);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.StoreI(proc, 2, modePercent);
		JlNativeApi.Store(proc, 3, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   灰度开/中值/闭一体化滤波（连续可调），边界处理以字符串传入。原生算子 id 1356。
	/// </summary>
	/// <param name="maskType">掩膜形状："circle" 或 "rect"。Default: "circle"</param>
	/// <param name="radius">滤波掩膜半径（像素）。Default: 1</param>
	/// <param name="modePercent">模式：0=灰度开运算，50=中值，100=灰度闭运算。Default: 10</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1356，本重载 margin 以 `StoreS` 直写，无钉元组开销。</para>
	///   <para><b>约束或前提</b>modePercent 在开→中值→闭之间连续取值；radius 决定形态学作用尺度。</para>
	///   <para><b>与相邻算子的取舍</b>需要元组化传参才用 <see cref="DualRank(string,int,int,JlTuple)"/>；本重载是常规单值写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage f = img.DualRank("circle", 1, 10, "mirrored");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage DualRank(string maskType, int radius, int modePercent, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1356);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.StoreI(proc, 2, modePercent);
		JlNativeApi.StoreS(proc, 3, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗均值滤波（平滑），id 1357。</summary>
	/// <param name="maskWidth">窗宽。Default: 9</param>
	/// <param name="maskHeight">窗高。Default: 9</param>
	/// <returns>平滑后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>窗内算术平均，是最低成本的空间平滑。参数只有两个 <c>int</c>（<c>StoreI</c>），
	///   <b>没有</b>边界处理参数（与 <see cref="MedianImage(string,int,string)"/> 不同），边缘环带的处理方式由原生决定 [待实测]。</para>
	///   <para><b>典型用途：低频背景估计</b>它常作为 <see cref="DynThreshold(JlImage,double,string)"/> 的阈值图来源，
	///   或经 <see cref="SubImage(JlImage,double,double)"/> 相减做背景归一化。窗要明显大于目标，
	///   否则目标自身被算进"背景"，减法后目标消失——这是该用法最常见的失效方式。</para>
	///   <para><b>什么时候不该用它</b>尺寸/边缘位置测量之前。均值滤波把阶跃边缘展宽为窗宽量级的斜坡，
	///   之后 <c>Threshold</c> 的 50% 交点会随窗尺寸移动，宽度与位置测量随之系统偏移；
	///   这种场合用 <see cref="MedianImage(string,int,string)"/> 或直接不滤波。
	///   要"平滑但更贴近高斯"用 <see cref="GaussImage(int)"/>；要任意形状窗用 <see cref="MeanImageShape(JlRegion)"/>。</para>
	///   <para><b>坑</b>平均会把小数部分量化：源为 <c>byte</c> 时输出若仍是 <c>byte</c>，低对比结构的差异可能被量化抹平 [待实测：输出类型]；
	///   做灰度测量前建议先 <c>ConvertImageType("float")</c> 再平滑（类型名以 <c>GetImageType()</c> 实际返回的字符串为准）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage lowFreq = img.MeanImage(31, 31);
	///   using JlRegion spots = img.DynThreshold(lowFreq, 10.0, "light");
	///   </code>
	///   <para><b>资源与坑</b>输出新句柄；输入不变；窗面积越大耗时越长 [待实测：耗时量级]。</para>
	/// </remarks>
	public JlImage MeanImage(int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(1357);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用二项式滤波器平滑图像，原生算子 id 1359。
	/// </summary>
	/// <param name="maskWidth">滤波器宽（像素）。Default: 5</param>
	/// <param name="maskHeight">滤波器高（像素）。Default: 5</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>二项式核近似高斯低通，尺寸越大平滑越强、振铃越小。参数以 `StoreI` 直写整数。</para>
	///   <para><b>约束或前提</b>单通道处理更常见；本算子无独立边界参数，四缘由原生决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要更锐利的矩窗均值用 MeanImage；要真正的高斯用 GaussImage/GaussFilter；二项式在平滑与保边间取折中。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage sm = img.BinomialFilter(5, 5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；滤波会压低峰值，后续灰度阈值不能沿用未平滑时的取值。</para>
	/// </remarks>
	public JlImage BinomialFilter(int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(1359);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>离散高斯平滑，参数是滤波器尺寸而非 sigma（id 1360）。</summary>
	/// <param name="size">所需滤波器尺寸。Default: 5</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1360。唯一的参数 <paramref name="size"/> 是<b>尺寸</b>（英文 "Required filter size"），
	///   不是标准差：核的 σ 由原生按尺寸推出 [待实测：换算关系]，因此"把 σ 调成 1.5"这类需求在这里表达不了，
	///   需要显式 σ 时改用 <c>GenGaussFilter(...)</c> + <c>ConvolImage(...)</c>。</para>
	///   <para><b>与 <c>GaussFilter</c> 的区别</b>本库另有 <see cref="GaussFilter(int)"/>，同名同参却是<b>另一个原生 id 1361</b>。
	///   两者的实际差别无法从托管层看出 [待实测]，不要以为换名字只是别名——切换实现时要用输出图逐像素比对确认。</para>
	///   <para><b>与相邻算子的取舍</b>只要快的粗略平滑 → <see cref="MeanImage(int,int)"/>（矩形核，等效截止更钝）；
	///   要保边缘去椒盐 → <see cref="MedianImage(string,int,string)"/>；要给 <c>DynThreshold</c> 造低频阈值图，
	///   <c>GaussImage</c> 与 <c>MeanImage</c> 都行，但高斯的振铃更小、边缘处背景估计更贴近局部。</para>
	///   <para><b>坑</b>平滑会抬高噪声底、压低峰值：随后做 <c>GrayHisto</c>/<c>Intensity</c> 时同一物理条件的直方图会整体变窄，
	///   阈值随平滑强度变化，不能沿用未平滑时调出的值。边缘与核截断处理本层不体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage g = img.GaussImage(5);
	///   using JlRegion reg = g.Threshold(100.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>输出新句柄；<paramref name="size"/> 为 <c>int</c>，偶数/负值不做校验 [待实测]。</para>
	/// </remarks>
	public JlImage GaussImage(int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1360);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用离散高斯函数平滑图像，原生算子 id 1361。
	/// </summary>
	/// <param name="size">所需滤波器尺寸（像素，非标准差）。Default: 5</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>size 是核尺寸而非 σ，σ 由原生按尺寸推出 [待实测：换算关系]。以 `StoreI` 写整数。</para>
	///   <para><b>与相邻算子的取舍</b>本库另有 <see cref="GaussImage(int)"/>（id 1360），同参不同算子，二者托管层看不出差别 [待实测]；需要显式控制高斯 σ 时本算子表达不了。粗略快速平滑可用 MeanImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage g = img.GaussFilter(5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；平滑会压低峰值、抬高噪声底，阈值需重调。</para>
	/// </remarks>
	public JlImage GaussFilter(int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1361);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把掩膜内偏离邻域过大的极值像素替换为邻域均值，做定向去噪，原生算子 id 1362。
	/// </summary>
	/// <param name="maskWidth">滤波掩膜宽（像素）。Default: 3</param>
	/// <param name="maskHeight">滤波掩膜高（像素）。Default: 3</param>
	/// <param name="gap">极值与邻域其余灰度之间所需的最小差值（灰度级）；差值超过它才被替换。Default: 1.0</param>
	/// <param name="mode">替换规则：选替换极小/极大/两者 [待实测：具体取值含义]。Default: 3</param>
	/// <returns>处理后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>只针对窗内"孤立的极亮/极暗"像素动手，其余像素保持原样，因此比均值/高斯更保边。</para>
	///   <para><b>约束或前提</b>gap 越大越保守（只替换极端离群），越小越接近普通均值滤波。mode 的确切取值语义本层无法确定 [待实测]，务必实测确认。</para>
	///   <para><b>与相邻算子的取舍</b>密集椒盐用 MedianImage；只想削掉个别坏点又不动边缘用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage outImg = img.EliminateMinMax(3, 3, 1.0, 3);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage EliminateMinMax(int maskWidth, int maskHeight, double gap, int mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1362);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, gap);
		JlNativeApi.StoreI(proc, 3, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对隔行扫描图像做去交错：插值或丢弃偶/奇场行，得到逐行连续的图像，原生算子 id 1363。
	/// </summary>
	/// <param name="mode">被替换/移除的行奇偶："even" 或 "odd"。Default: "odd"</param>
	/// <returns>去交错后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用相邻保留行插值补齐被丢弃的场行，消除隔行采集造成的水平横纹错位。</para>
	///   <para><b>约束或前提</b>只对确为隔行采集的图像有意义；逐行采集的图强行去交错会引入竖直方向模糊。mode 选哪一半场被替换。</para>
	///   <para><b>与相邻算子的取舍</b>本库不提供 framegrabber 采集族，去交错是离线图像后处理手段。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage prog = img.FillInterlace("odd");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage FillInterlace(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1363);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在多幅/多通道图之间按逐像素排序取第 rankIndex 个灰度。</summary>
	/// <param name="rankIndex">取第几个排序位置。Default: 2</param>
	/// <returns>排序结果图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1364，英文定义 "Return gray values with given rank from multiple channels"：
	///   排序发生在<b>通道之间</b>而不是空间邻域内——对每个像素，把各通道的取值排序后输出第 <paramref name="rankIndex"/> 个。
	///   所以它<b>不是</b> <see cref="RankRect(int,int,int)"/>/<see cref="RankImage(JlRegion,int,string)"/> 那一类空间滤波，
	///   两者参数相似但作用完全不同，混用会得到一张"没有空间平滑效果"的图。</para>
	///   <para><b>输入形态</b>需要的是"同一位置有多个灰度"的数据：一幅 N 通道图（<see cref="Compose3(JlImage,JlImage)"/> 之类合成），
	///   或图像数组 [待实测：本层只 <c>Store(proc,1)</c> 声明一路输入，数组是否等价于多通道无法从托管层判断]。
	///   通道数用 <see cref="CountChannels()"/> 先确认。</para>
	///   <para><b>坑</b><paramref name="rankIndex"/> 是 <c>int</c> 控制参数，超过通道数时本层不校验 [待实测]；
	///   <c>rankIndex = 1</c> 即逐像素取最小通道值（多曝光/多视角里的"最暗"），最后一个即"最亮"，
	///   与 <see cref="MinImage(JlImage)"/>/<see cref="MaxImage(JlImage)"/> 只在两幅图时结果相同，多于两幅时才是它的用武之地。
	///   取均值请直接用 <c>MeanN()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage two = a.Compose2(b);
	///   using JlImage darker = two.RankN(1);                       // 逐像素取两通道中较小者
	///   </code>
	///   <para><b>资源与坑</b>输出新句柄；输入不变。</para>
	/// </remarks>
	public JlImage RankN(int rankIndex)
	{
		IntPtr proc = JlNativeApi.PreCall(1364);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, rankIndex);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把多通道在每一像素上求算术平均，压成单通道图，原生算子 id 1365。
	/// </summary>
	/// <returns>通道平均后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="RankN(int)"/> 同族，但取的是均值而非第 k 序值：对每像素把 N 个通道值求平均。</para>
	///   <para><b>约束或前提</b>输入须为多通道图（通道数用 CountChannels 确认）；单通道时输出约等于拷贝。</para>
	///   <para><b>与相邻算子的取舍</b>想逐像素取最亮/最暗用 RankN；想把彩色图整体降灰度又保留平均亮度用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage a = new JlImage("byte", 256, 256);
	///   JlImage b = new JlImage("byte", 256, 256);
	///   JlImage two = a.Compose2(b);
	///   JlImage avg = two.MeanN();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；Compose2 的签名以本仓库实际为准 [待实测]。</para>
	/// </remarks>
	public JlImage MeanN()
	{
		IntPtr proc = JlNativeApi.PreCall(1365);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把掩膜内超出 [minThresh,maxThresh] 的灰度值替换为该掩膜的均值，原生算子 id 1366。
	/// </summary>
	/// <param name="maskWidth">滤波掩膜宽（像素）。Default: 3</param>
	/// <param name="maskHeight">滤波掩膜高（像素）。Default: 3</param>
	/// <param name="minThresh">保留区间下界（灰度级）。Default: 1</param>
	/// <param name="maxThresh">保留区间上界（灰度级）。Default: 254</param>
	/// <returns>处理后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>只把"落到有效区间外"的像素（过亮/过暗的坏点）改成掩膜均值，区间内像素原样保留。</para>
	///   <para><b>约束或前提</b>minThresh/maxThresh 是按图像灰度量纲设定的硬阈值——对 byte 图常用 1/254 只削纯黑纯白坏点；改成 float 图时区间语义完全不同。</para>
	///   <para><b>与相邻算子的取舍</b>要按"偏离邻域统计"判离群用 EliminateMinMax（自适应）；本算子是按绝对灰度区间判，适合已知坏点落在黑/白端。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage outImg = img.EliminateSp(3, 3, 1, 254);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage EliminateSp(int maskWidth, int maskHeight, int minThresh, int maxThresh)
	{
		IntPtr proc = JlNativeApi.PreCall(1366);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreI(proc, 2, minThresh);
		JlNativeApi.StoreI(proc, 3, maxThresh);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   抑制椒盐噪声：对落在 [minThresh,maxThresh] 之外的中心像素，用掩膜内均值替换，原生算子 id 1367。
	/// </summary>
	/// <param name="maskWidth">滤波掩膜宽（像素）。Default: 3</param>
	/// <param name="maskHeight">滤波掩膜高（像素）。Default: 3</param>
	/// <param name="minThresh">判定为噪声的下界（灰度级）。Default: 1</param>
	/// <param name="maxThresh">判定为噪声的上界（灰度级）。Default: 254</param>
	/// <returns>去噪后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>仅替换被 minThresh/maxThresh 判为"过暗/过亮"的中心像素，其余原样保留，比全窗均值更保边。</para>
	///   <para><b>约束或前提</b>阈值按图像灰度量纲设定：默认 1/254 针对 byte 图的纯黑/纯白噪声。区间太宽则几乎不动，太窄则连正常细节都被当噪声替换。</para>
	///   <para><b>与相邻算子的取舍</b>噪声幅度不固定时改用自适应的 EliminateMinMax 或中值滤波；坏点稳定贴黑/贴白时用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage outImg = img.MeanSp(3, 3, 1, 254);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MeanSp(int maskWidth, int maskHeight, int minThresh, int maxThresh)
	{
		IntPtr proc = JlNativeApi.PreCall(1367);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreI(proc, 2, minThresh);
		JlNativeApi.StoreI(proc, 3, maxThresh);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 Sojka 算子检测角点，权重/阈值参数以元组传入，输出角点坐标。原生算子 id 1368。
	/// </summary>
	/// <param name="maskSize">所需滤波器尺寸（像素）。Default: 9</param>
	/// <param name="sigmaW">距离角点候选的高斯权重 σ。Default: 2.5</param>
	/// <param name="sigmaD">距离理想灰度边缘的高斯权重 σ。Default: 0.75</param>
	/// <param name="minGrad">梯度幅值阈值。Default: 30.0</param>
	/// <param name="minApparentness">显著度(apparentness)阈值。Default: 90.0</param>
	/// <param name="minAngle">角点处方向变化的阈值（弧度）。Default: 0.5</param>
	/// <param name="subpix">是否亚像素精化："true"/"false"。Default: "false"</param>
	/// <param name="row">输出角点行坐标（新元组句柄）。</param>
	/// <param name="column">输出角点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Sojka 是灰度型角点检测：minGrad 决定"边缘要多强"，minApparentness 决定"角要多显著"，minAngle 用弧度限定两边缘的夹角跨度。本元组重载对前四个参数以 `Store`+`UnpinTuple` 传值。</para>
	///   <para><b>约束或前提</b>minAngle 是弧度不是角度。输入建议单通道灰度图。</para>
	///   <para><b>与相邻算子的取舍</b>想要标量签名用 <see cref="PointsSojka(int,double,double,double,double,double,string,out JlTuple,out JlTuple)"/>；相对 Harris/Lepetit/Foerstner，Sojka 更依赖灰度边缘模型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple sw = 2.5, sd = 0.75, mg = 30.0, ma = 90.0;
	///   img.PointsSojka(9, sw, sd, mg, ma, 0.5, "false", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 是新元组句柄，用完可 Dispose；无角点时输出空元组。</para>
	/// </remarks>
	public void PointsSojka(int maskSize, JlTuple sigmaW, JlTuple sigmaD, JlTuple minGrad, JlTuple minApparentness, double minAngle, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1368);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.Store(proc, 1, sigmaW);
		JlNativeApi.Store(proc, 2, sigmaD);
		JlNativeApi.Store(proc, 3, minGrad);
		JlNativeApi.Store(proc, 4, minApparentness);
		JlNativeApi.StoreD(proc, 5, minAngle);
		JlNativeApi.StoreS(proc, 6, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigmaW);
		JlNativeApi.UnpinTuple(sigmaD);
		JlNativeApi.UnpinTuple(minGrad);
		JlNativeApi.UnpinTuple(minApparentness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Sojka 算子检测角点，参数以标量给定，输出角点坐标。原生算子 id 1368。
	/// </summary>
	/// <param name="maskSize">所需滤波器尺寸（像素）。Default: 9</param>
	/// <param name="sigmaW">距离角点候选的高斯权重 σ。Default: 2.5</param>
	/// <param name="sigmaD">距离理想灰度边缘的高斯权重 σ。Default: 0.75</param>
	/// <param name="minGrad">梯度幅值阈值。Default: 30.0</param>
	/// <param name="minApparentness">显著度阈值。Default: 90.0</param>
	/// <param name="minAngle">角点处方向变化阈值（弧度）。Default: 0.5</param>
	/// <param name="subpix">是否亚像素精化："true"/"false"。Default: "false"</param>
	/// <param name="row">输出角点行坐标（新元组句柄）。</param>
	/// <param name="column">输出角点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1368；本重载全部参数以 `StoreI`/`StoreD`/`StoreS` 直写，无钉元组开销。</para>
	///   <para><b>约束或前提</b>minAngle 为弧度。用字面量调用即绑定到本重载（优先于需隐式转换的元组重载）。</para>
	///   <para><b>与相邻算子的取舍</b>常规单组阈值检测用本重载；需元组传参见元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsSojka(9, 2.5, 0.75, 30.0, 90.0, 0.5, "false", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 是新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsSojka(int maskSize, double sigmaW, double sigmaD, double minGrad, double minApparentness, double minAngle, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1368);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.StoreD(proc, 1, sigmaW);
		JlNativeApi.StoreD(proc, 2, sigmaD);
		JlNativeApi.StoreD(proc, 3, minGrad);
		JlNativeApi.StoreD(proc, 4, minApparentness);
		JlNativeApi.StoreD(proc, 5, minAngle);
		JlNativeApi.StoreS(proc, 6, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   增强图像中的圆形点（斑点），按直径匹配做亮/暗响应，原生算子 id 1369。
	/// </summary>
	/// <param name="diameter">待增强圆点的直径（像素）。Default: 5</param>
	/// <param name="filterType">增强对象："light" 亮斑、"dark" 暗斑、"all" 两者。Default: "light"</param>
	/// <param name="pixelShift">滤波响应的平移量（灰度级偏置）。Default: 0</param>
	/// <returns>增强后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>输出是"点响应图"而非原图：直径匹配目标点处响应高，其余接近背景。便于随后 Threshold 提取点。</para>
	///   <para><b>约束或前提</b>diameter 必须接近真实点径，否则不响应。filterType 决定只看亮/暗，混用会同时增强噪声斑。</para>
	///   <para><b>与相邻算子的取舍</b>找十字用 CrossImage，找线用 LineExtractor 类；本算子专用于圆点。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage dots = img.DotsImage(5, "light", 0);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage DotsImage(int diameter, string filterType, int pixelShift)
	{
		IntPtr proc = JlNativeApi.PreCall(1369);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, diameter);
		JlNativeApi.StoreS(proc, 1, filterType);
		JlNativeApi.StoreI(proc, 2, pixelShift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   亚像素检测图像中的局部极小值（暗点），输出坐标元组。原生算子 id 1370。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 表示不对输入做平滑。</param>
	/// <param name="threshold">Hessian 矩阵特征值绝对值的最小门限（越大越只留强极值）。Default: 5.0</param>
	/// <param name="row">输出极小值行坐标（新元组句柄）。</param>
	/// <param name="column">输出极小值列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>基于二阶导（Hessian）特征值定位亚像素极小值。threshold 是响应强度门限，直接决定检出数量。</para>
	///   <para><b>约束或前提</b>filter="facet" 且 sigma=0 时用最原始邻域、不预平滑，噪声多时易误检，此时改 deriche 或给非零 sigma。</para>
	///   <para><b>与相邻算子的取舍</b>找极大多用 LocalMaxSubPix，找鞍点用 SaddlePointsSubPix；要一次拿全三类用 CriticalPointsSubPix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.LocalMinSubPix("facet", 0.0, 5.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void LocalMinSubPix(string filter, double sigma, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1370);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   亚像素检测图像中的局部极大值（亮点），输出坐标元组。原生算子 id 1371。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 不做预平滑。</param>
	/// <param name="threshold">Hessian 特征值绝对值门限。Default: 5.0</param>
	/// <param name="row">输出极大值行坐标（新元组句柄）。</param>
	/// <param name="column">输出极大值列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 LocalMinSubPix 对称，定位亚像素局部极大值。</para>
	///   <para><b>约束或前提</b>filter="facet" 且 sigma=0 时对噪声敏感。</para>
	///   <para><b>与相邻算子的取舍</b>找暗点用 LocalMinSubPix；三类一起用 CriticalPointsSubPix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.LocalMaxSubPix("facet", 0.0, 5.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void LocalMaxSubPix(string filter, double sigma, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1371);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   亚像素检测图像中的鞍点，输出坐标元组。原生算子 id 1372。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 不做预平滑。</param>
	/// <param name="threshold">Hessian 特征值绝对值门限。Default: 5.0</param>
	/// <param name="row">输出鞍点行坐标（新元组句柄）。</param>
	/// <param name="column">输出鞍点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>鞍点是"一个方向极大、垂直方向极小"的点，常用于分叉/桥接结构定位。</para>
	///   <para><b>约束或前提</b>同族亚像素检测器，threshold 决定强度门限。</para>
	///   <para><b>与相邻算子的取舍</b>要同时得极小/极大/鞍点用 CriticalPointsSubPix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.SaddlePointsSubPix("facet", 0.0, 5.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void SaddlePointsSubPix(string filter, double sigma, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1372);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   亚像素一次性检测极小值、极大值与鞍点三类临界点。原生算子 id 1373。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 不做预平滑。</param>
	/// <param name="threshold">Hessian 特征值绝对值门限。Default: 5.0</param>
	/// <param name="rowMin">输出极小值行坐标（新元组句柄）。</param>
	/// <param name="columnMin">输出极小值列坐标（新元组句柄）。</param>
	/// <param name="rowMax">输出极大值行坐标（新元组句柄）。</param>
	/// <param name="columnMax">输出极大值列坐标（新元组句柄）。</param>
	/// <param name="rowSaddle">输出鞍点行坐标（新元组句柄）。</param>
	/// <param name="columnSaddle">输出鞍点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>等价于把 LocalMinSubPix + LocalMaxSubPix + SaddlePointsSubPix 一次算完，省三遍偏导计算。</para>
	///   <para><b>约束或前提</b>六个 out 全部为 DOUBLE 元组，长度各不相同（各自一类点的数量）。</para>
	///   <para><b>与相邻算子的取舍</b>只要其中一类时用三个单独算子，避免装载多余输出。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.CriticalPointsSubPix("facet", 0.0, 5.0, out JlTuple rmin, out JlTuple cmin, out JlTuple rmax, out JlTuple cmax, out JlTuple rsad, out JlTuple csad);
	///   </code>
	///   <para><b>资源与坑</b>六个元组均为新句柄，用完可 Dispose。</para>
	/// </remarks>
	public void CriticalPointsSubPix(string filter, double sigma, double threshold, out JlTuple rowMin, out JlTuple columnMin, out JlTuple rowMax, out JlTuple columnMax, out JlTuple rowSaddle, out JlTuple columnSaddle)
	{
		IntPtr proc = JlNativeApi.PreCall(1373);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnMin);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out rowMax);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out columnMax);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out rowSaddle);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out columnSaddle);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 算子检测兴趣点，threshold 以元组传入。原生算子 id 1374。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量。Default: 0.7</param>
	/// <param name="sigmaSmooth">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重（Harris k）。Default: 0.08</param>
	/// <param name="threshold">点的最小滤波响应门限，以元组传入。Default: 1000.0</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Harris 依据二阶矩矩阵的角点度量响应，对旋转稳定、对尺度不敏感。threshold 越高检出越少。</para>
	///   <para><b>约束或前提</b>threshold 以 `Store`+`UnpinTuple` 传值；响应量纲随 sigma/alpha 变，换参数后 threshold 需重调。</para>
	///   <para><b>与相邻算子的取舍</b>要标量签名用 <see cref="PointsHarris(double,double,double,double,out JlTuple,out JlTuple)"/>；要亚像素/二项式近似用 PointsHarrisBinomial。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple thr = 1000.0;
	///   img.PointsHarris(0.7, 2.0, 0.08, thr, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarris(double sigmaGrad, double sigmaSmooth, double alpha, JlTuple threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1374);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigmaGrad);
		JlNativeApi.StoreD(proc, 1, sigmaSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 算子检测兴趣点，threshold 以标量给定。原生算子 id 1374。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量。Default: 0.7</param>
	/// <param name="sigmaSmooth">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重。Default: 0.08</param>
	/// <param name="threshold">点的最小滤波响应门限。Default: 1000.0</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1374；threshold 以 `StoreD` 直写。</para>
	///   <para><b>约束或前提</b>响应量纲随 sigma/alpha 变，换参数须重调 threshold。</para>
	///   <para><b>与相邻算子的取舍</b>字面量调用绑定到本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsHarris(0.7, 2.0, 0.08, 1000.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarris(double sigmaGrad, double sigmaSmooth, double alpha, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1374);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigmaGrad);
		JlNativeApi.StoreD(proc, 1, sigmaSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.StoreD(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 算子的二项式近似检测兴趣点，threshold 以元组传入。原生算子 id 1375。
	/// </summary>
	/// <param name="maskSizeGrad">计算梯度时的二项式平滑量（核尺寸）。Default: 5</param>
	/// <param name="maskSizeSmooth">积分梯度时的平滑量（核尺寸）。Default: 15</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重。Default: 0.08</param>
	/// <param name="threshold">点的最小响应门限，以元组传入。Default: 1000.0</param>
	/// <param name="subpix">是否亚像素精化："on"/"off"。Default: "on"</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>用二项式核近似高斯，核尺寸以 `StoreI` 给（是尺寸不是 σ）；比 PointsHarris 更快，参数序不同。</para>
	///   <para><b>约束或前提</b>maskSize 越大越平滑、检出越少；subpix 与 threshold 配合决定最终点数。</para>
	///   <para><b>与相邻算子的取舍</b>需要 σ 精控用 <see cref="PointsHarris(double,double,double,JlTuple,out JlTuple,out JlTuple)"/>；要标量 threshold 用本类标量重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple thr = 1000.0;
	///   img.PointsHarrisBinomial(5, 15, 0.08, thr, "on", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarrisBinomial(int maskSizeGrad, int maskSizeSmooth, double alpha, JlTuple threshold, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1375);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSizeGrad);
		JlNativeApi.StoreI(proc, 1, maskSizeSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.StoreS(proc, 4, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 二项式近似检测兴趣点，threshold 以标量给定。原生算子 id 1375。
	/// </summary>
	/// <param name="maskSizeGrad">计算梯度时的二项式平滑量（核尺寸）。Default: 5</param>
	/// <param name="maskSizeSmooth">积分梯度时的平滑量（核尺寸）。Default: 15</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重。Default: 0.08</param>
	/// <param name="threshold">点的最小响应门限。Default: 1000.0</param>
	/// <param name="subpix">是否亚像素精化："on"/"off"。Default: "on"</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1375；threshold 以 `StoreD` 直写。</para>
	///   <para><b>约束或前提</b>maskSize 为整数核尺寸。</para>
	///   <para><b>与相邻算子的取舍</b>字面量 threshold 调用绑定到本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsHarrisBinomial(5, 15, 0.08, 1000.0, "on", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarrisBinomial(int maskSizeGrad, int maskSizeSmooth, double alpha, double threshold, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1375);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSizeGrad);
		JlNativeApi.StoreI(proc, 1, maskSizeSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.StoreD(proc, 3, threshold);
		JlNativeApi.StoreS(proc, 4, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Lepetit(FAST) 算子检测兴趣点。原生算子 id 1376。
	/// </summary>
	/// <param name="radius">检测圆的半径（像素）。Default: 3</param>
	/// <param name="checkNeighbor">圆周上被检查的相邻点数（连续计数）。Default: 1</param>
	/// <param name="minCheckNeighborDiff">与圆周每一点所需的灰度差阈值。Default: 15</param>
	/// <param name="minScore">与全部圆周点灰度差之和的阈值。Default: 30</param>
	/// <param name="subpix">坐标亚像素精度："none"/"interpolation"/"regression"。Default: "interpolation"</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>FAST 型检测：比较中心与半径 radius 圆周上像素的灰度差，连续超阈点数达 checkNeighbor 判为角点。全参数以 `StoreI`/`StoreS` 直写。</para>
	///   <para><b>关键坑</b>row/column 走的是<b>不带 DOUBLE 类型标记</b>的 `JlTuple.LoadNew`（对比 PointsHarris 等用 `JlTupleType.DOUBLE`）——即使 subpix="interpolation"，输出坐标也可能是整数量纲，亚像素小数被丢 [待实测：实际装载类型]。</para>
	///   <para><b>与相邻算子的取舍</b>需要可靠亚像素坐标改用 PointsHarrisBinomial(subpix="on")；本算子胜在快。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsLepetit(3, 1, 15, 30, "interpolation", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsLepetit(int radius, int checkNeighbor, int minCheckNeighborDiff, int minScore, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1376);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, radius);
		JlNativeApi.StoreI(proc, 1, checkNeighbor);
		JlNativeApi.StoreI(proc, 2, minCheckNeighborDiff);
		JlNativeApi.StoreI(proc, 3, minScore);
		JlNativeApi.StoreS(proc, 4, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out row);
		err = JlTuple.LoadNew(proc, 1, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Foerstner 算子同时检测角点(junction)与区域(area)兴趣点，平滑参数以元组传入。原生算子 id 1377。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量；smoothing="mean" 时忽略。Default: 1.0</param>
	/// <param name="sigmaInt">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="sigmaPoints">优化函数中的平滑量。Default: 3.0</param>
	/// <param name="threshInhom">非均匀区域分割阈值。Default: 200</param>
	/// <param name="threshShape">点区域分割阈值。Default: 0.3</param>
	/// <param name="smoothing">平滑方法："gauss"/"mean"。Default: "gauss"</param>
	/// <param name="eliminateDoublets">是否合并重复点。Default: "false"</param>
	/// <param name="rowJunctions">角点行坐标（新元组句柄）。</param>
	/// <param name="columnJunctions">角点列坐标（新元组句柄）。</param>
	/// <param name="coRRJunctions">角点协方差矩阵行-行分量。</param>
	/// <param name="coRCJunctions">角点协方差矩阵行-列混合分量。</param>
	/// <param name="coCCJunctions">角点协方差矩阵列-列分量。</param>
	/// <param name="rowArea">区域点行坐标。</param>
	/// <param name="columnArea">区域点列坐标。</param>
	/// <param name="coRRArea">区域点协方差矩阵行-行分量。</param>
	/// <param name="coRCArea">区域点协方差矩阵行-列混合分量。</param>
	/// <param name="coCCArea">区域点协方差矩阵列-列分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Foerstner 通过二阶矩矩阵同时给出"点位置"与"定位不确定度"（协方差三分量）。本元组重载 sigmaGrad/sigmaInt/sigmaPoints/threshInhom 走 `Store`+`UnpinTuple`。</para>
	///   <para><b>约束或前提</b>smoothing="mean" 时 sigmaGrad 被忽略；threshShape 是标量 double。输出共 10 个 DOUBLE 元组，长度分别对应 junction 与 area 两组。</para>
	///   <para><b>与相邻算子的取舍</b>只要 Harris/Lepetit 类点数不含协方差时用相应算子；需要点位置+不确定度用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple sg = 1.0, si = 2.0, sp = 3.0, ti = 200.0;
	///   img.PointsFoerstner(sg, si, sp, ti, 0.3, "gauss", "false",
	///       out JlTuple rj, out JlTuple cj, out JlTuple rrj, out JlTuple rcj, out JlTuple ccj,
	///       out JlTuple ra, out JlTuple ca, out JlTuple rra, out JlTuple rca, out JlTuple cca);
	///   </code>
	///   <para><b>资源与坑</b>10 个 out 元组都是新句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsFoerstner(JlTuple sigmaGrad, JlTuple sigmaInt, JlTuple sigmaPoints, JlTuple threshInhom, double threshShape, string smoothing, string eliminateDoublets, out JlTuple rowJunctions, out JlTuple columnJunctions, out JlTuple coRRJunctions, out JlTuple coRCJunctions, out JlTuple coCCJunctions, out JlTuple rowArea, out JlTuple columnArea, out JlTuple coRRArea, out JlTuple coRCArea, out JlTuple coCCArea)
	{
		IntPtr proc = JlNativeApi.PreCall(1377);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigmaGrad);
		JlNativeApi.Store(proc, 1, sigmaInt);
		JlNativeApi.Store(proc, 2, sigmaPoints);
		JlNativeApi.Store(proc, 3, threshInhom);
		JlNativeApi.StoreD(proc, 4, threshShape);
		JlNativeApi.StoreS(proc, 5, smoothing);
		JlNativeApi.StoreS(proc, 6, eliminateDoublets);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		JlNativeApi.InitOCT(proc, 9);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigmaGrad);
		JlNativeApi.UnpinTuple(sigmaInt);
		JlNativeApi.UnpinTuple(sigmaPoints);
		JlNativeApi.UnpinTuple(threshInhom);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowJunctions);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnJunctions);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out coRRJunctions);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out coRCJunctions);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out coCCJunctions);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out rowArea);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out columnArea);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out coRRArea);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out coRCArea);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out coCCArea);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Foerstner 算子同时检测角点(junction)与区域(area)兴趣点，平滑参数以标量给定。原生算子 id 1377。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量；smoothing="mean" 时忽略。Default: 1.0</param>
	/// <param name="sigmaInt">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="sigmaPoints">优化函数中的平滑量。Default: 3.0</param>
	/// <param name="threshInhom">非均匀区域分割阈值。Default: 200</param>
	/// <param name="threshShape">点区域分割阈值。Default: 0.3</param>
	/// <param name="smoothing">平滑方法："gauss"/"mean"。Default: "gauss"</param>
	/// <param name="eliminateDoublets">是否合并重复点。Default: "false"</param>
	/// <param name="rowJunctions">角点行坐标（新元组句柄）。</param>
	/// <param name="columnJunctions">角点列坐标（新元组句柄）。</param>
	/// <param name="coRRJunctions">角点协方差行-行分量。</param>
	/// <param name="coRCJunctions">角点协方差行-列分量。</param>
	/// <param name="coCCJunctions">角点协方差列-列分量。</param>
	/// <param name="rowArea">区域点行坐标。</param>
	/// <param name="columnArea">区域点列坐标。</param>
	/// <param name="coRRArea">区域点协方差行-行分量。</param>
	/// <param name="coRCArea">区域点协方差行-列分量。</param>
	/// <param name="coCCArea">区域点协方差列-列分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1377；本重载 sigmaGrad/sigmaInt/sigmaPoints/threshInhom 以 `StoreD` 直写，无钉元组开销。</para>
	///   <para><b>约束或前提</b>10 个 out 均为 DOUBLE 元组。</para>
	///   <para><b>与相邻算子的取舍</b>字面量参数调用即绑定本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsFoerstner(1.0, 2.0, 3.0, 200.0, 0.3, "gauss", "false",
	///       out JlTuple rj, out JlTuple cj, out JlTuple rrj, out JlTuple rcj, out JlTuple ccj,
	///       out JlTuple ra, out JlTuple ca, out JlTuple rra, out JlTuple rca, out JlTuple cca);
	///   </code>
	///   <para><b>资源与坑</b>10 个 out 元组都是新句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsFoerstner(double sigmaGrad, double sigmaInt, double sigmaPoints, double threshInhom, double threshShape, string smoothing, string eliminateDoublets, out JlTuple rowJunctions, out JlTuple columnJunctions, out JlTuple coRRJunctions, out JlTuple coRCJunctions, out JlTuple coCCJunctions, out JlTuple rowArea, out JlTuple columnArea, out JlTuple coRRArea, out JlTuple coRCArea, out JlTuple coCCArea)
	{
		IntPtr proc = JlNativeApi.PreCall(1377);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigmaGrad);
		JlNativeApi.StoreD(proc, 1, sigmaInt);
		JlNativeApi.StoreD(proc, 2, sigmaPoints);
		JlNativeApi.StoreD(proc, 3, threshInhom);
		JlNativeApi.StoreD(proc, 4, threshShape);
		JlNativeApi.StoreS(proc, 5, smoothing);
		JlNativeApi.StoreS(proc, 6, eliminateDoublets);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		JlNativeApi.InitOCT(proc, 9);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowJunctions);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnJunctions);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out coRRJunctions);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out coRCJunctions);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out coCCJunctions);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out rowArea);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out columnArea);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out coRRArea);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out coRCArea);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out coCCArea);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   从单幅图像估计噪声标准差，以元组返回（每通道一个值）。原生算子 id 1378。
	/// </summary>
	/// <param name="method">噪声估计方法："foerstner"/"deriche1"/"lmed"。Default: "foerstner"</param>
	/// <param name="percent">参与估计的图像点百分比（0..100），以元组传入。Default: 20</param>
	/// <returns>噪声标准差元组（DOUBLE，可能多值），用毕可 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用于自动调参/质量评估。percent 小 → 只用最"平坦"的点、估计偏保守；大 → 覆盖面广但可能把边缘当噪声。</para>
	///   <para><b>约束或前提</b>元组版按 `LoadNew(DOUBLE)` 返回全部值，多通道图每通道一项。</para>
	///   <para><b>与相邻算子的取舍</b>只需第一路数值用 <see cref="EstimateNoise(string,double)"/>（返回 double，丢弃其余通道）；要逐通道 σ 用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple pct = 20;
	///   JlTuple noise = img.EstimateNoise("foerstner", pct);
	///   </code>
	///   <para><b>资源与坑</b>返回新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public JlTuple EstimateNoise(string method, JlTuple percent)
	{
		IntPtr proc = JlNativeApi.PreCall(1378);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.Store(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percent);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   从单幅图像估计噪声标准差，以 double 返回。原生算子 id 1378。
	/// </summary>
	/// <param name="method">噪声估计方法。Default: "foerstner"</param>
	/// <param name="percent">参与估计的图像点百分比（0..100）。Default: 20</param>
	/// <returns>噪声标准差（仅第一个值）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1378；本重载以 `LoadD` 只取第一个返回值。</para>
	///   <para><b>约束或前提</b>多通道输入下原生可能返回逐通道 σ，本 double 版会静默丢弃除首值以外的所有通道。</para>
	///   <para><b>与相邻算子的取舍</b>要每通道 σ 请用 <see cref="EstimateNoise(string,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   double noise = img.EstimateNoise("foerstner", 20.0);
	///   </code>
	///   <para><b>资源与坑</b>返回值为原生 double，无需释放。</para>
	/// </remarks>
	public double EstimateNoise(string method, double percent)
	{
		IntPtr proc = JlNativeApi.PreCall(1378);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreD(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Determine the noise distribution of an image.
	/// </summary>
	/// <param name="constRegion">Region from which the noise distribution is to be estimated.</param>
	/// <param name="filterSize">Size of the mean filter. Default: 21</param>
	/// <returns>Noise distribution of all input regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>确定 noise distribution 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion constRegion = ...;
	///   JlImage obj = ...;
	///   var result = obj.NoiseDistributionMean(constRegion, 21);
	///   </code>
	/// </remarks>
	public JlTuple NoiseDistributionMean(JlRegion constRegion, int filterSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1379);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, constRegion);
		JlNativeApi.StoreI(proc, 0, filterSize);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(constRegion);
		return tuple;
	}

	/// <summary>
	///   Add noise to an image.
	/// </summary>
	/// <param name="amp">Maximum noise amplitude. Default: 60.0</param>
	/// <returns>Noisy image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add noise 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AddNoiseWhite(60.0);
	///   </code>
	/// </remarks>
	public JlImage AddNoiseWhite(double amp)
	{
		IntPtr proc = JlNativeApi.PreCall(1380);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, amp);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Add noise to an image.
	/// </summary>
	/// <param name="distribution">Noise distribution.</param>
	/// <returns>Noisy image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add noise 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple distribution = ...;
	///   JlImage obj = ...;
	///   var result = obj.AddNoiseDistribution(distribution);
	///   </code>
	/// </remarks>
	public JlImage AddNoiseDistribution(JlTuple distribution)
	{
		IntPtr proc = JlNativeApi.PreCall(1381);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, distribution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(distribution);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate standard deviation over several channels.
	/// </summary>
	/// <returns>Result of calculation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Calculate standard deviation over several channels。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.DeviationN();
	///   </code>
	/// </remarks>
	public JlImage DeviationN()
	{
		IntPtr proc = JlNativeApi.PreCall(1384);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   Perform an inpainting by coherence transport.
	/// </summary>
	/// <param name="region">Inpainting region.</param>
	/// <param name="epsilon">Radius of the pixel neighborhood. Default: 5.0</param>
	/// <param name="kappa">Sharpness parameter in percent. Default: 25.0</param>
	/// <param name="sigma">Pre-smoothing parameter. Default: 1.41</param>
	/// <param name="rho">Smoothing parameter for the direction estimation. Default: 4.0</param>
	/// <param name="channelCoefficients">Channel weights. Default: 1</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform an inpainting by coherence transport。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.InpaintingCt(region, 5.0, 25.0, 1.41, 4.0, 1);
	///   </code>
	/// </remarks>
	public JlImage InpaintingCt(JlRegion region, double epsilon, double kappa, double sigma, double rho, JlTuple channelCoefficients)
	{
		IntPtr proc = JlNativeApi.PreCall(1386);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, epsilon);
		JlNativeApi.StoreD(proc, 1, kappa);
		JlNativeApi.StoreD(proc, 2, sigma);
		JlNativeApi.StoreD(proc, 3, rho);
		JlNativeApi.Store(proc, 4, channelCoefficients);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(channelCoefficients);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Perform an inpainting by coherence transport.
	/// </summary>
	/// <param name="region">Inpainting region.</param>
	/// <param name="epsilon">Radius of the pixel neighborhood. Default: 5.0</param>
	/// <param name="kappa">Sharpness parameter in percent. Default: 25.0</param>
	/// <param name="sigma">Pre-smoothing parameter. Default: 1.41</param>
	/// <param name="rho">Smoothing parameter for the direction estimation. Default: 4.0</param>
	/// <param name="channelCoefficients">Channel weights. Default: 1</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform an inpainting by coherence transport。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.InpaintingCt(region, 5.0, 25.0, 1.41, 4.0, 1);
	///   </code>
	/// </remarks>
	public JlImage InpaintingCt(JlRegion region, double epsilon, double kappa, double sigma, double rho, double channelCoefficients)
	{
		IntPtr proc = JlNativeApi.PreCall(1386);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, epsilon);
		JlNativeApi.StoreD(proc, 1, kappa);
		JlNativeApi.StoreD(proc, 2, sigma);
		JlNativeApi.StoreD(proc, 3, rho);
		JlNativeApi.StoreD(proc, 4, channelCoefficients);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Perform an inpainting by smoothing of level lines.
	/// </summary>
	/// <param name="region">Inpainting region.</param>
	/// <param name="sigma">Smoothing for derivative operator. Default: 0.5</param>
	/// <param name="theta">Time step. Default: 0.5</param>
	/// <param name="iterations">Number of iterations. Default: 10</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform an inpainting by smoothing of level lines。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.InpaintingMcf(region, 0.5, 0.5, 10);
	///   </code>
	/// </remarks>
	public JlImage InpaintingMcf(JlRegion region, double sigma, double theta, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(1387);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, theta);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Perform an inpainting by coherence enhancing diffusion.
	/// </summary>
	/// <param name="region">Inpainting region.</param>
	/// <param name="sigma">Smoothing for derivative operator. Default: 0.5</param>
	/// <param name="rho">Smoothing for diffusion coefficients. Default: 3.0</param>
	/// <param name="theta">Time step. Default: 0.5</param>
	/// <param name="iterations">Number of iterations. Default: 10</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform an inpainting by coherence enhancing diffusion。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.InpaintingCed(region, 0.5, 3.0, 0.5, 10);
	///   </code>
	/// </remarks>
	public JlImage InpaintingCed(JlRegion region, double sigma, double rho, double theta, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(1388);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, rho);
		JlNativeApi.StoreD(proc, 2, theta);
		JlNativeApi.StoreI(proc, 3, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Perform an inpainting by anisotropic diffusion.
	/// </summary>
	/// <param name="region">Inpainting region.</param>
	/// <param name="mode">Type of edge sharpening algorithm. Default: "weickert"</param>
	/// <param name="contrast">Contrast parameter. Default: 5.0</param>
	/// <param name="theta">Step size. Default: 0.5</param>
	/// <param name="iterations">Number of iterations. Default: 10</param>
	/// <param name="rho">Smoothing coefficient for edge information. Default: 3.0</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform an inpainting by anisotropic diffusion。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.InpaintingAniso(region, "weickert", 5.0, 0.5, 10, 3.0);
	///   </code>
	/// </remarks>
	public JlImage InpaintingAniso(JlRegion region, string mode, double contrast, double theta, int iterations, double rho)
	{
		IntPtr proc = JlNativeApi.PreCall(1389);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, contrast);
		JlNativeApi.StoreD(proc, 2, theta);
		JlNativeApi.StoreI(proc, 3, iterations);
		JlNativeApi.StoreD(proc, 4, rho);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   Perform a harmonic interpolation on an image region.
	/// </summary>
	/// <param name="region">Inpainting region.</param>
	/// <param name="precision">Computational accuracy. Default: 0.001</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform harmonic interpolation 在 图像 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.HarmonicInterpolation(region, 0.001);
	///   </code>
	/// </remarks>
	public JlImage HarmonicInterpolation(JlRegion region, double precision)
	{
		IntPtr proc = JlNativeApi.PreCall(1390);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, precision);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>向外扩张图像定义域并为新增环带补灰度，原生算子 id 1391。</summary>
	/// <param name="expansionRange">灰度扩张半径，单位是像素。Default: 2</param>
	/// <returns>定义域变大、扩张区带灰度的新图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把当前定义域沿边界向外生长 <paramref name="expansionRange"/> 像素，新增环带内生成有效灰度；
	///   输入图像不变，输出是全新图像句柄（iconc 槽位 1）。典型用途：ROI 处理链——<see cref="ReduceDomain(JlRegion)"/>
	///   之后的滤波、特征计算或写文件会在 ROI 外圈留下"域空洞"，用本算子把边界一圈补出来。</para>
	///   <para><b>约束</b>它不是"把整幅图填满"：每次只从现有域边界外推一圈，内部孤立空洞的半径大于
	///   <paramref name="expansionRange"/> 时填不到中心，需要加大半径或迭代调用。扩张带灰度的生成方法
	///   （插值/延拓/邻域统计）由原生决定，托管层看不出来 [待实测]；输出像素类型与输入的关系同 [待实测]。</para>
	///   <para><b>资源与坑</b><paramref name="expansionRange"/> 是 <c>int</c>（<c>StoreI</c>），0 与负值行为 [待实测]；
	///   返回图像需释放；对定义域本就覆盖全图的图像调用它，只是白复制一份图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(100.0, 100.0, 300.0, 300.0);
	///   using JlImage cut = img.ReduceDomain(roi);
	///   using JlImage filled = cut.ExpandDomainGray(3);   // 把 ROI 边界外 3 像素一圈补成有效域
	///   </code>
	/// </remarks>
	public JlImage ExpandDomainGray(int expansionRange)
	{
		IntPtr proc = JlNativeApi.PreCall(1391);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, expansionRange);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the topographic primal sketch of an image.
	/// </summary>
	/// <returns>Label image containing the 11 classes.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 topographic primal sketch 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.TopographicSketch();
	///   </code>
	/// </remarks>
	public JlImage TopographicSketch()
	{
		IntPtr proc = JlNativeApi.PreCall(1392);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute an affine transformation of the color values of a multichannel image.
	/// </summary>
	/// <param name="transMat">Transformation matrix for the color values.</param>
	/// <returns>Multichannel output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 仿射变换 color 值 multichannel 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple transMat = ...;
	///   JlImage obj = ...;
	///   var result = obj.LinearTransColor(transMat);
	///   </code>
	/// </remarks>
	public JlImage LinearTransColor(JlTuple transMat)
	{
		IntPtr proc = JlNativeApi.PreCall(1393);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, transMat);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(transMat);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the transformation matrix of the principal component analysis of multichannel images.
	/// </summary>
	/// <param name="transInv">Transformation matrix for the computation of the inverse PCA.</param>
	/// <param name="mean">Mean gray value of the channels.</param>
	/// <param name="cov">Covariance matrix of the channels.</param>
	/// <param name="infoPerComp">Information content of the transformed channels.</param>
	/// <returns>Transformation matrix for the computation of the PCA.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>生成PrincipalComp变换。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GenPrincipalCompTrans(out JlTuple transInv, out JlTuple mean, out JlTuple cov, out JlTuple infoPerComp);
	///   </code>
	/// </remarks>
	public JlTuple GenPrincipalCompTrans(out JlTuple transInv, out JlTuple mean, out JlTuple cov, out JlTuple infoPerComp)
	{
		IntPtr proc = JlNativeApi.PreCall(1394);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out transInv);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out mean);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out cov);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out infoPerComp);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Compute the principal components of multichannel images.
	/// </summary>
	/// <param name="infoPerComp">Information content of each output channel.</param>
	/// <returns>Multichannel output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 principal components multichannel 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.PrincipalComp(out JlTuple infoPerComp);
	///   </code>
	/// </remarks>
	public JlImage PrincipalComp(out JlTuple infoPerComp)
	{
		IntPtr proc = JlNativeApi.PreCall(1395);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out infoPerComp);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Determine the fuzzy entropy of regions.
	/// </summary>
	/// <param name="regions">Regions for which the fuzzy entropy is to be calculated.</param>
	/// <param name="apar">Start of the fuzzy function. Default: 0</param>
	/// <param name="cpar">End of the fuzzy function. Default: 255</param>
	/// <returns>Fuzzy entropy of a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>确定 fuzzy entropy 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.FuzzyEntropy(regions, 0, 255);
	///   </code>
	/// </remarks>
	public JlTuple FuzzyEntropy(JlRegion regions, int apar, int cpar)
	{
		IntPtr proc = JlNativeApi.PreCall(1396);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreI(proc, 0, apar);
		JlNativeApi.StoreI(proc, 1, cpar);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   Calculate the fuzzy perimeter of a region.
	/// </summary>
	/// <param name="regions">Regions for which the fuzzy perimeter is to be calculated.</param>
	/// <param name="apar">Start of the fuzzy function. Default: 0</param>
	/// <param name="cpar">End of the fuzzy function. Default: 255</param>
	/// <returns>Fuzzy perimeter of a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 fuzzy perimeter 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.FuzzyPerimeter(regions, 0, 255);
	///   </code>
	/// </remarks>
	public JlTuple FuzzyPerimeter(JlRegion regions, int apar, int cpar)
	{
		IntPtr proc = JlNativeApi.PreCall(1397);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreI(proc, 0, apar);
		JlNativeApi.StoreI(proc, 1, cpar);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>用指定形状掩膜做灰度闭运算（先 max 后 min）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>闭运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>本族三种写法（先选这一层）</b>
	///   ① <c>*Rect(int,int)</c>：矩形窗，尺寸是 <c>int</c>，见 <see cref="GrayClosingRect(int,int)"/>；
	///   ② <c>*Shape</c>（本族）：形状可选 <paramref name="maskShape"/>，且掩膜尺寸是 <c>double</c>，可以传 7.5 这类非整窗 [待实测：如何取整]；
	///   ③ <c>Gray*(JlImage SE)</c>：结构元自己造（<c>GenDiscSe</c>/<c>ReadGraySe</c>），SE 可带灰度坡度和任意形状，见
	///   <see cref="GrayClosing(JlImage)"/>。
	///   99% 的调参需求停在 ① 或 ②，只有需要"平顶以外的灰度帽"（黑帽/顶帽的 SE）时才值得上 ③。</para>
	///   <para><b>功能说明</b>原生算子 id 1398：窗内先取最大后取最小，即"上包络"，把窄暗谷抬到邻近水平。
	///   本族英文 <c>returns</c> 一律写着 "minimum gray values"，那是模板串抄错了：只有腐蚀才是窗内最小值，
	///   闭运算的输出量纲与 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/> 不同，别照抄那句说明理解算子。</para>
	///   <para><b>坑</b>掩膜是<b>邻域窗</b>，不是二值形态学里的"能不能放下"：窗给大一片，暗谷就整体被抬，
	///   随后 <c>Threshold</c> 的同一阈值会给出不同面积，灰度测量类流程里必须先固定窗再固定阈值。
	///   图像边缘的开窗方式本层未体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage closed = img.GrayClosingShape(new JlTuple(11.0), new JlTuple(11.0), "octagon");
	///   using JlRegion dark = closed.Threshold(0.0, 60.0);
	///   </code>
	///   <para><b>参数取向</b>元组版可一次传多组尺寸（多值如何与输出对应本层无法判断 [待实测]）；
	///   单组尺寸请用 <see cref="GrayClosingShape(double,double,string)"/>。
	///   重载绑定要注意：写 <c>GrayClosingShape(11, 11, "octagon")</c> 或 <c>(11.0, 11.0, ...)</c> 都会选到 double 版——
	///   本库有 <c>int/double/string → JlTuple</c> 的隐式转换，但用户定义转换在重载解析里排在标准隐式转换之后；
	///   想显式命中元组版必须写 <c>new JlTuple(...)</c>。</para>
	/// </remarks>
	public JlImage GrayClosingShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1398);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度闭运算（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>算法与本族三种写法的取舍见 <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>：同一原生 id 1398。</para>
	///   <para><b>实际差异</b>两个尺寸经 <c>StoreD</c> 直写，无固定/解固定；掩膜形状仍是字符串，取值不校验。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage closed = img.GrayClosingShape(11.0, 11.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayClosingShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1398);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用指定形状掩膜做灰度开运算（先 min 后 max）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>开运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1399：窗内先取最小后取最大，得到原图的<b>下包络</b>，
	///   把比邻域高且窄的亮峰削掉，暗结构不受影响。与闭运算方向相反，两者不能互相顶替。</para>
	///   <para>三种灰度形态学写法（Rect / Shape / 自建 SE）的选择、掩膜尺寸的 <c>double</c> 语义、
	///   以及"本族英文 <c>returns</c> 写成 minimum gray values 是模板抄错"这一点，见
	///   <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>。</para>
	///   <para><b>与顶帽的分界</b>要"削掉亮峰继续用灰度"用本算子；要"把亮峰单独取出来"用
	///   <see cref="GrayTophat(JlImage)"/>（它就是原图减本算子的结果）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage cleaned = img.GrayOpeningShape(new JlTuple(11.0), new JlTuple(11.0), "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayOpeningShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1399);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度开运算（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>下包络语义见 <see cref="GrayOpeningShape(JlTuple,JlTuple,string)"/>，本族写法取舍见
	///   <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>：同一原生 id 1399，尺寸走 <c>StoreD</c>。
	///   数字字面量会绑定到本重载；要打元组版必须显式 <c>new JlTuple(...)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage opened = img.GrayOpeningShape(11.0, 11.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayOpeningShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1399);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜内的灰度最小值滤波（灰度腐蚀）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>窗内最小值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1400，英文说明就是"取掩膜范围内的最小灰度"：输出是逐窗最小值图。
	///   它把亮结构按窗尺寸"蚀掉"，只留下暗的东西，因此常用作<b>暗背景估计</b>：
	///   <c>img.GrayErosionShape(...)</c> 后与 <c>img.SubImage(bg, 1.0, 0.0)</c> 相减，即得到扣除背景后的亮目标图。</para>
	///   <para><b>与 <c>MinImage</c> 的区别</b>本算子在<b>同一幅图</b>的邻域内取最小（会移动边缘），
	///   <c>MinImage(JlImage)</c> 在两幅图之间逐像素取最小（不动边缘），二者不可互换。</para>
	///   <para><b>坑</b>输出整体变暗：亮目标小于窗宽时会被完全抹掉，做"背景估计"时窗必须明显大于目标最大尺寸，
	///   否则目标本身被当成背景减掉。边缘开窗与 <c>double</c> 尺寸取整方式见
	///   <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/> [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayErosionShape(new JlTuple(21.0), new JlTuple(21.0), "octagon");
	///   using JlImage flat = img.SubImage(bg, 1.0, 0.0);          // 去背景的亮目标
	///   using JlRegion parts = flat.Threshold(20.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage GrayErosionShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1400);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度最小值滤波（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>逐窗最小值语义、背景估计用法见 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/>；
	///   本族写法取舍见 <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>。同一原生 id 1400，尺寸走 <c>StoreD</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage min = img.GrayErosionShape(21.0, 21.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayErosionShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1400);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜内的灰度最大值滤波（灰度膨胀）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>窗内最大值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1401，英文说明"取掩膜范围内的最大灰度"：逐窗最大值图。
	///   暗结构被抬到邻近亮水平，只剩最暗的东西，因此它是 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/> 的对偶：
	///   找<b>暗</b>目标（黑点、气泡、孔）时用本算子做<b>亮背景估计</b>，再 <c>SubImage</c>（背景减目标方向）或 <c>GrayBothat</c>。</para>
	///   <para><b>坑</b>输出整体变亮，<c>byte</c> 图接近 255 的部分被压顶，后续阈值/直方图在高端堆积 [待实测：饱和行为]。
	///   与 <c>MaxImage(JlImage)</c> 的区别同腐蚀：<b>邻域</b>取最大 vs <b>两幅图间</b>取最大。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayDilationShape(new JlTuple(21.0), new JlTuple(21.0), "octagon");
	///   using JlRegion holes = bg.SubImage(img, 1.0, 0.0).Threshold(20.0, 255.0);   // 背景减原图，暗孔变亮
	///   </code>
	///   <para><b>参数取向</b>多组尺寸走元组版，单组用 <see cref="GrayDilationShape(double,double,string)"/>；
	///   数字字面量会绑定到 double 版。</para>
	/// </remarks>
	public JlImage GrayDilationShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1401);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度最大值滤波（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>逐窗最大值语义与"亮背景估计"用法见 <see cref="GrayDilationShape(JlTuple,JlTuple,string)"/>；
	///   本族写法取舍见 <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>。同一原生 id 1401，尺寸走 <c>StoreD</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage max = img.GrayDilationShape(21.0, 21.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayDilationShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1401);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗内的灰度极差（最大值减最小值），得到局部对比度图。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>逐窗灰度极差图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1402。输出既不是膨胀也不是腐蚀，而是 <c>max − min</c>：
	///   平坦区接近 0，纹理/边缘/缺陷处变大。因此它是"哪里有起伏"的图，与"哪里亮"无关——
	///   用它 <c>Threshold</c> 出的区域是<b>纹理区</b>而不是亮区，反过来光照变化不影响它的判据。</para>
	///   <para><b>与相邻算子的取舍</b>要"起伏强度"的另一种统计量用 <c>DeviationImage(width,height)</c>（窗内标准差），
	///   对离群单点没本算子敏感；要"局部亮度趋势"用 <c>MeanImage</c>。自己用 <c>GrayDilationRect</c> 减
	///   <c>GrayErosionRect</c> 也能拼出同样结果，但多一次 <c>SubImage</c> 分配，本算子一步到位。</para>
	///   <para><b>坑</b>窗尺寸 <c>int</c>（<c>StoreI</c>），必须 ≥ 目标起伏的跨度才能把该起伏记进极差；
	///   窗越大越会把整片背景的低频不均也算成"对比度"。图像边缘开窗方式本层不体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage contrast = img.GrayRangeRect(9, 9);
	///   using JlRegion textured = contrast.Threshold(25.0, 255.0);   // 只看起伏，不看亮度
	///   </code>
	/// </remarks>
	public JlImage GrayRangeRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1402);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗灰度闭运算（先 max 后 min），id 1403。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>闭运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>矩形窗版本的 <see cref="GrayClosing(JlImage)"/>（用平顶 SE），把窄暗谷抬平。
	///   尺寸是 <c>int</c>（<c>StoreI</c>），只能整窗；需要圆形/八边形窗用
	///   <see cref="GrayClosingShape(double,double,string)"/>。</para>
	///   <para><b>坑</b>矩形窗的角部会在结果里留下 45° 以外的方角伪影：被抬平的区域外缘会带上窗形状，
	///   后面做 <c>Roundness</c>/<c>EllipticAxis</c> 一类形状量测时偏差来自窗形状而不是目标 [待实测：偏差量级]。
	///   非正方形窗（高≠宽）等价于给方向性滤波，横向暗线用 <c>(3,15)</c> 才抬得平。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage closed = img.GrayClosingRect(11, 11);
	///   using JlRegion dark = closed.Threshold(0.0, 50.0);
	///   </code>
	/// </remarks>
	public JlImage GrayClosingRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1403);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗灰度开运算（先 min 后 max），id 1404。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>开运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>矩形窗版本的 <see cref="GrayOpening(JlImage)"/>：削掉窄亮峰、保留下包络，
	///   常放在 <c>Threshold</c> 之前当"灰度域去噪"。与 <c>MedianImage</c> 的分工：中值滤波保边缘去椒盐，
	///   开运算专门压亮毛刺但会把亮边缘整体压低。</para>
	///   <para><b>坑</b>亮目标宽度小于窗宽时会被一起削掉；输出偏暗，<c>byte</c> 图低端有截断风险 [待实测]。
	///   形状用圆/八边形请改 <see cref="GrayOpeningShape(double,double,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage opened = img.GrayOpeningRect(5, 5);
	///   using JlRegion bright = opened.Threshold(150.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage GrayOpeningRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1404);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗内灰度最小值滤波，id 1405。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>逐窗最小值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>窗内取最小：亮结构按窗尺寸被抹掉，留下暗的东西，所以它是"暗背景/下包络"估计。
	///   与 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/> 只差窗形状（矩形 vs 可选圆/八边形）与尺寸类型（<c>int</c> vs <c>double</c>）。</para>
	///   <para><b>坑</b>窗必须明显大于亮目标，否则目标被当成背景；后续 <c>SubImage</c> 相减时两幅图类型不一致会引入
	///   量化误差 [待实测]。矩形窗的方角同样会留在结果边缘。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayErosionRect(21, 21);
	///   using JlImage flat = img.SubImage(bg, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage GrayErosionRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1405);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗内灰度最大值滤波，id 1406。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>逐窗最大值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>窗内取最大：暗结构被邻近亮值覆盖，得到"上包络/亮背景"，与
	///   <see cref="GrayErosionRect(int,int)"/> 成对使用即可做背景归一化。</para>
	///   <para><b>与相邻算子的取舍</b>要圆/八边形窗或小数尺寸用 <see cref="GrayDilationShape(JlTuple,JlTuple,string)"/>；
	///   要在两幅图之间逐像素取最大用 <c>MaxImage(JlImage)</c>（它不做邻域、不会移动边缘）。</para>
	///   <para><b>坑</b>暗目标小于窗尺寸时会被彻底抹掉，做背景估计时窗要明显大于目标；
	///   <c>byte</c> 图输出偏亮，接近 255 处存在饱和 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayDilationRect(21, 21);
	///   using JlRegion pits = bg.SubImage(img, 1.0, 0.0).Threshold(20.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage GrayDilationRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1406);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度图像细化：把亮结构缩成保留灰度值的脊线。</summary>
	/// <returns>细化后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1407，英文说明即 "Thinning of gray value images"：在保持亮结构连通性的前提下
	///   把它细化到一线宽，并且<b>脊线上保留原灰度值</b>（不像二值细化那样把灰度信息丢掉），
	///   因此脊线灰度仍可当作宽度/强度量。</para>
	///   <para><b>与相邻算子的取舍</b>只需要形状骨架（拓扑、分支点）时用 <see cref="JlRegion"/> 的 <c>Skeleton()</c>，输出区域更省内存；
	///   要沿纹路的灰度剖面（划痕深度、焊点亮度）用本算子。本算子输出<b>仍是图像</b>，需要区域时再 <c>Threshold</c>。</para>
	///   <para><b>坑</b>细化对毛刺极敏感：亮毛刺会被细化成额外的枝杈，通常先做 <c>GrayOpeningRect</c> 或
	///   <c>MedianImage</c> 再细化；枝杈要量化可用区域侧 <c>JunctionsSkeleton(out JlRegion juncPoints)</c> 取分支点后再修剪。
	///   耗时随亮结构面积增长，大面积高亮图上比二值细化明显慢 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage prepared = img.GrayOpeningRect(3, 3);
	///   using JlImage ridges = prepared.GraySkeleton();
	///   using JlRegion lines = ridges.Threshold(1.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>无参数、单路输出，返回新句柄需释放；输入图不变。</para>
	/// </remarks>
	public JlImage GraySkeleton()
	{
		IntPtr proc = JlNativeApi.PreCall(1407);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Transform an image with a gray-value look-up-table
	/// </summary>
	/// <param name="lut">Table containing the transformation.</param>
	/// <returns>Transformed image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Transform 图像 使用 灰度-值 look-up-table。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple lut = ...;
	///   JlImage obj = ...;
	///   var result = obj.LutTrans(lut);
	///   </code>
	/// </remarks>
	public JlImage LutTrans(JlTuple lut)
	{
		IntPtr proc = JlNativeApi.PreCall(1408);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, lut);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(lut);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the correlation between an image and an arbitrary filter mask
	/// </summary>
	/// <param name="filterMask">Filter mask as file name or tuple. Default: "sobel"</param>
	/// <param name="margin">Border treatment. Default: "mirrored"</param>
	/// <returns>Result of the correlation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 correlation between 图像 和 arbitrary 滤波掩膜。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ConvolImage("sobel", "mirrored");
	///   </code>
	/// </remarks>
	public JlImage ConvolImage(JlTuple filterMask, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1409);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, filterMask);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(filterMask);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the correlation between an image and an arbitrary filter mask
	/// </summary>
	/// <param name="filterMask">Filter mask as file name or tuple. Default: "sobel"</param>
	/// <param name="margin">Border treatment. Default: "mirrored"</param>
	/// <returns>Result of the correlation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 correlation between 图像 和 arbitrary 滤波掩膜。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ConvolImage("sobel", "mirrored");
	///   </code>
	/// </remarks>
	public JlImage ConvolImage(string filterMask, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1409);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterMask);
		JlNativeApi.StoreS(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert the type of an image.
	/// </summary>
	/// <param name="newType">Desired image type (i.e., type of the gray values). Default: "byte"</param>
	/// <returns>Converted image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 type 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ConvertImageType("byte");
	///   </code>
	/// </remarks>
	public JlImage ConvertImageType(string newType)
	{
		IntPtr proc = JlNativeApi.PreCall(1410);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, newType);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert two real-valued images into a vector field image.
	/// </summary>
	/// <param name="col">Vector component in the column direction.</param>
	/// <param name="type">Semantic kind of the vector field. Default: "vector_field_relative"</param>
	/// <returns>Displacement vector field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>RealTo向量场。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage col = ...;
	///   JlImage obj = ...;
	///   var result = obj.RealToVectorField(col, "vector_field_relative");
	///   </code>
	/// </remarks>
	public JlImage RealToVectorField(JlImage col, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(1411);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, col);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(col);
		return obj;
	}

	/// <summary>
	///   Convert a vector field image into two real-valued images.
	/// </summary>
	/// <param name="col">Vector component in the column direction.</param>
	/// <returns>Vector component in the row direction.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>向量场ToReal。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.VectorFieldToReal(out JlImage col);
	///   </code>
	/// </remarks>
	public JlImage VectorFieldToReal(out JlImage col)
	{
		IntPtr proc = JlNativeApi.PreCall(1412);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out col);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert two real images into a complex image.
	/// </summary>
	/// <param name="imageImaginary">Imaginary part.</param>
	/// <returns>Complex image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 two real 图像 为 complex 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageImaginary = ...;
	///   JlImage obj = ...;
	///   var result = obj.RealToComplex(imageImaginary);
	///   </code>
	/// </remarks>
	public JlImage RealToComplex(JlImage imageImaginary)
	{
		IntPtr proc = JlNativeApi.PreCall(1413);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageImaginary);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageImaginary);
		return obj;
	}

	/// <summary>
	///   Convert a complex image into two real images.
	/// </summary>
	/// <param name="imageImaginary">Imaginary part.</param>
	/// <returns>Real part.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 complex 图像 为 two real 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ComplexToReal(out JlImage imageImaginary);
	///   </code>
	/// </remarks>
	public JlImage ComplexToReal(out JlImage imageImaginary)
	{
		IntPtr proc = JlNativeApi.PreCall(1414);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imageImaginary);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Paint regions with their average gray value.
	/// </summary>
	/// <param name="regions">Input regions.</param>
	/// <returns>Result image with painted regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>绘制 区域 使用 their average 灰度值。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.RegionToMean(regions);
	///   </code>
	/// </remarks>
	public JlImage RegionToMean(JlRegion regions)
	{
		IntPtr proc = JlNativeApi.PreCall(1415);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return obj;
	}

	/// <summary>逐像素求"通往图像边界任意路径上所能遇到的最低灰度"。</summary>
	/// <returns>灰度内部值图像（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1416。英文定义："对每个像素，求通往图像边界任意路径上的最低可能灰度"。
	///   即把图像当高程面，从<b>边界</b>往里浸水：一个像素的输出值 = 它能沿连续路径"逃到"边界时会遇到的最低水位。
	///   被高灰度包围的封闭低洼区（孔、暗斑内部）逃不出去，输出值会高于其实际灰度——这正是它用来区分
	///   "真正连到外部的低区"与"封闭洼地"的能力。</para>
	///   <para><b>什么时候用它</b>需要判断暗区是否与图像边界连通（涂布边缘、连通气孔 vs 内部夹杂），
	///   或作为分水/流域合并（见 <see cref="WatershedsMarker(JlRegion)"/>）的前置量。
	///   只是想去低频背景，用 <c>GrayErosionRect</c>/<c>GrayBothat</c> 更直接也更快。</para>
	///   <para><b>坑</b>输入被当成高程面：如果图像是"目标亮、背景暗"，洼地在背景，输出几乎处处等于背景最小值，
	///   信息全丢——这类图必须先 <c>InvertImage()</c> 或改用对偶方向的算子 [待实测：本算子是否有方向参数]。
	///   边界本身的灰度会向内传播整个连通区域，所以来料边缘压暗会污染整块判据。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage inside = img.GrayInside();
	///   using JlRegion trapped = img.SubImage(inside, 1.0, 0.0).Threshold(1.0, 255.0);   // 封闭洼地处有差值
	///   </code>
	///   <para><b>资源与坑</b>无参数、单路图像输出；输入图不变。</para>
	/// </remarks>
	public JlImage GrayInside()
	{
		IntPtr proc = JlNativeApi.PreCall(1416);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Symmetry of gray values along a row.
	/// </summary>
	/// <param name="maskSize">Extension of search area. Default: 40</param>
	/// <param name="direction">Angle of test direction. Default: 0.0</param>
	/// <param name="exponent">Exponent for weighting. Default: 0.5</param>
	/// <returns>Symmetry image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Symmetry 灰度值s along row。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Symmetry(40, 0.0, 0.5);
	///   </code>
	/// </remarks>
	public JlImage Symmetry(int maskSize, double direction, double exponent)
	{
		IntPtr proc = JlNativeApi.PreCall(1417);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.StoreD(proc, 1, direction);
		JlNativeApi.StoreD(proc, 2, exponent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Selection of gray values of a multi-channel image using an index image.
	/// </summary>
	/// <param name="indexImage">Image, where pixel values are interpreted as channel index.</param>
	/// <returns>Resulting image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Selection 灰度值s multi-channel 图像 使用 索引 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage indexImage = ...;
	///   JlImage obj = ...;
	///   var result = obj.SelectGrayvaluesFromChannels(indexImage);
	///   </code>
	/// </remarks>
	public JlImage SelectGrayvaluesFromChannels(JlImage indexImage)
	{
		IntPtr proc = JlNativeApi.PreCall(1418);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, indexImage);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(indexImage);
		return obj;
	}






	/// <summary>
	///   Convolve a vector field with derivatives of the Gaussian.
	/// </summary>
	/// <param name="sigma">Sigma of the Gaussian. Default: 1.0</param>
	/// <param name="component">Component to be calculated. Default: "mean_curvature"</param>
	/// <returns>Filtered result images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Derivate向量场。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.DerivateVectorField(1.0, "mean_curvature");
	///   </code>
	/// </remarks>
	public JlImage DerivateVectorField(JlTuple sigma, string component)
	{
		IntPtr proc = JlNativeApi.PreCall(1423);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.StoreS(proc, 1, component);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convolve a vector field with derivatives of the Gaussian.
	/// </summary>
	/// <param name="sigma">Sigma of the Gaussian. Default: 1.0</param>
	/// <param name="component">Component to be calculated. Default: "mean_curvature"</param>
	/// <returns>Filtered result images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Derivate向量场。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.DerivateVectorField(1.0, "mean_curvature");
	///   </code>
	/// </remarks>
	public JlImage DerivateVectorField(double sigma, string component)
	{
		IntPtr proc = JlNativeApi.PreCall(1423);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreS(proc, 1, component);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the length of the vectors of a vector field.
	/// </summary>
	/// <param name="mode">Mode for computing the length of the vectors. Default: "length"</param>
	/// <returns>Length of the vectors of the vector field.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>向量场Length。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.VectorFieldLength("length");
	///   </code>
	/// </remarks>
	public JlImage VectorFieldLength(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1424);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}





	/// <summary>
	///   Searching corners in images.
	/// </summary>
	/// <param name="size">Desired filtersize of the graymask. Default: 3</param>
	/// <param name="weight">Weighting. Default: 0.04</param>
	/// <returns>Result of the filtering.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Searching corners 在 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CornerResponse(3, 0.04);
	///   </code>
	/// </remarks>
	public JlImage CornerResponse(int size, double weight)
	{
		IntPtr proc = JlNativeApi.PreCall(1428);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.StoreD(proc, 1, weight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculating a Gauss pyramid.
	/// </summary>
	/// <param name="mode">Kind of filter mask. Default: "weighted"</param>
	/// <param name="scale">Factor for scaling down. Default: 0.5</param>
	/// <returns>Output images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Calculating Gauss pyramid。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GenGaussPyramid("weighted", 0.5);
	///   </code>
	/// </remarks>
	public JlImage GenGaussPyramid(string mode, double scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1429);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   Detect color lines and their width.
	/// </summary>
	/// <param name="sigma">Amount of Gaussian smoothing to be applied. Default: 1.5</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 3</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 8</param>
	/// <param name="extractWidth">Should the line width be extracted? Default: "true"</param>
	/// <param name="completeJunctions">Should junctions be added where they cannot be extracted? Default: "true"</param>
	/// <returns>Extracted lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect color lines and their width。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LinesColor(1.5, 3, 8, "true", "true");
	///   </code>
	/// </remarks>
	public JlXLDCont LinesColor(JlTuple sigma, JlTuple low, JlTuple high, string extractWidth, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1432);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.Store(proc, 1, low);
		JlNativeApi.Store(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, extractWidth);
		JlNativeApi.StoreS(proc, 4, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect color lines and their width.
	/// </summary>
	/// <param name="sigma">Amount of Gaussian smoothing to be applied. Default: 1.5</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 3</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 8</param>
	/// <param name="extractWidth">Should the line width be extracted? Default: "true"</param>
	/// <param name="completeJunctions">Should junctions be added where they cannot be extracted? Default: "true"</param>
	/// <returns>Extracted lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect color lines and their width。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LinesColor(1.5, 3, 8, "true", "true");
	///   </code>
	/// </remarks>
	public JlXLDCont LinesColor(double sigma, double low, double high, string extractWidth, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1432);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, low);
		JlNativeApi.StoreD(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, extractWidth);
		JlNativeApi.StoreS(proc, 4, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect lines and their width.
	/// </summary>
	/// <param name="sigma">Amount of Gaussian smoothing to be applied. Default: 1.5</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 3</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 8</param>
	/// <param name="lightDark">Extract bright or dark lines. Default: "light"</param>
	/// <param name="extractWidth">Should the line width be extracted? Default: "true"</param>
	/// <param name="lineModel">Line model used to correct the line position and width. Default: "bar-shaped"</param>
	/// <param name="completeJunctions">Should junctions be added where they cannot be extracted? Default: "true"</param>
	/// <returns>Extracted lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Lines高斯。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LinesGauss(1.5, 3, 8, "light", "true", "bar-shaped", "true");
	///   </code>
	/// </remarks>
	public JlXLDCont LinesGauss(JlTuple sigma, JlTuple low, JlTuple high, string lightDark, string extractWidth, string lineModel, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1433);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.Store(proc, 1, low);
		JlNativeApi.Store(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.StoreS(proc, 4, extractWidth);
		JlNativeApi.StoreS(proc, 5, lineModel);
		JlNativeApi.StoreS(proc, 6, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect lines and their width.
	/// </summary>
	/// <param name="sigma">Amount of Gaussian smoothing to be applied. Default: 1.5</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 3</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 8</param>
	/// <param name="lightDark">Extract bright or dark lines. Default: "light"</param>
	/// <param name="extractWidth">Should the line width be extracted? Default: "true"</param>
	/// <param name="lineModel">Line model used to correct the line position and width. Default: "bar-shaped"</param>
	/// <param name="completeJunctions">Should junctions be added where they cannot be extracted? Default: "true"</param>
	/// <returns>Extracted lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Lines高斯。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LinesGauss(1.5, 3, 8, "light", "true", "bar-shaped", "true");
	///   </code>
	/// </remarks>
	public JlXLDCont LinesGauss(double sigma, double low, double high, string lightDark, string extractWidth, string lineModel, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1433);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, low);
		JlNativeApi.StoreD(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.StoreS(proc, 4, extractWidth);
		JlNativeApi.StoreS(proc, 5, lineModel);
		JlNativeApi.StoreS(proc, 6, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detection of lines using the facet model.
	/// </summary>
	/// <param name="maskSize">Size of the facet model mask. Default: 5</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 3</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 8</param>
	/// <param name="lightDark">Extract bright or dark lines. Default: "light"</param>
	/// <returns>Extracted lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detection lines 使用 facet 模型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LinesFacet(5, 3, 8, "light");
	///   </code>
	/// </remarks>
	public JlXLDCont LinesFacet(int maskSize, JlTuple low, JlTuple high, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(1434);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.Store(proc, 1, low);
		JlNativeApi.Store(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detection of lines using the facet model.
	/// </summary>
	/// <param name="maskSize">Size of the facet model mask. Default: 5</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 3</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 8</param>
	/// <param name="lightDark">Extract bright or dark lines. Default: "light"</param>
	/// <returns>Extracted lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detection lines 使用 facet 模型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LinesFacet(5, 3, 8, "light");
	///   </code>
	/// </remarks>
	public JlXLDCont LinesFacet(int maskSize, double low, double high, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(1434);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.StoreD(proc, 1, low);
		JlNativeApi.StoreD(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Store a filter mask in the spatial domain as a real-image.
	/// </summary>
	/// <param name="filterMask">Filter mask as file name or tuple. Default: "gauss"</param>
	/// <param name="scale">Scaling factor. Default: 1.0</param>
	/// <param name="width">Width of the image (filter). Default: 512</param>
	/// <param name="height">Height of the image (filter). Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>存储 滤波掩膜 在 spatial domain 作为 real-图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenFilterMask("gauss", 1.0, 512, 512);
	///   </code>
	/// </remarks>
	public void GenFilterMask(JlTuple filterMask, double scale, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1435);
		JlNativeApi.Store(proc, 0, filterMask);
		JlNativeApi.StoreD(proc, 1, scale);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(filterMask);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Store a filter mask in the spatial domain as a real-image.
	/// </summary>
	/// <param name="filterMask">Filter mask as file name or tuple. Default: "gauss"</param>
	/// <param name="scale">Scaling factor. Default: 1.0</param>
	/// <param name="width">Width of the image (filter). Default: 512</param>
	/// <param name="height">Height of the image (filter). Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>存储 滤波掩膜 在 spatial domain 作为 real-图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenFilterMask("gauss", 1.0, 512, 512);
	///   </code>
	/// </remarks>
	public void GenFilterMask(string filterMask, double scale, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1435);
		JlNativeApi.StoreS(proc, 0, filterMask);
		JlNativeApi.StoreD(proc, 1, scale);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Generate a mean filter in the frequency domain.
	/// </summary>
	/// <param name="maskShape">Shape of the filter mask in the spatial domain. Default: "ellipse"</param>
	/// <param name="diameter1">Diameter of the mean filter in the principal direction of the filter in the spatial domain. Default: 11.0</param>
	/// <param name="diameter2">Diameter of the mean filter perpendicular to the principal direction of the filter in the spatial domain. Default: 11.0</param>
	/// <param name="phi">Principal direction of the filter in the spatial domain. Default: 0.0</param>
	/// <param name="norm">Normalizing factor of the filter. Default: "none"</param>
	/// <param name="mode">Location of the DC term in the frequency domain. Default: "dc_center"</param>
	/// <param name="width">Width of the image (filter). Default: 512</param>
	/// <param name="height">Height of the image (filter). Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>生成 均值滤波 在 frequency domain。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenMeanFilter("ellipse", 11.0, 11.0, 0.0, "none", "dc_center", 512, 512);
	///   </code>
	/// </remarks>
	public void GenMeanFilter(string maskShape, double diameter1, double diameter2, double phi, string norm, string mode, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1436);
		JlNativeApi.StoreS(proc, 0, maskShape);
		JlNativeApi.StoreD(proc, 1, diameter1);
		JlNativeApi.StoreD(proc, 2, diameter2);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreS(proc, 4, norm);
		JlNativeApi.StoreS(proc, 5, mode);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Generate a Gaussian filter in the frequency domain.
	/// </summary>
	/// <param name="sigma1">Standard deviation of the Gaussian in the principal direction of the filter in the spatial domain. Default: 1.0</param>
	/// <param name="sigma2">Standard deviation of the Gaussian perpendicular to the principal direction of the filter in the spatial domain. Default: 1.0</param>
	/// <param name="phi">Principal direction of the filter in the spatial domain. Default: 0.0</param>
	/// <param name="norm">Normalizing factor of the filter. Default: "none"</param>
	/// <param name="mode">Location of the DC term in the frequency domain. Default: "dc_center"</param>
	/// <param name="width">Width of the image (filter). Default: 512</param>
	/// <param name="height">Height of the image (filter). Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>生成 Gaussian 滤波 在 frequency domain。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenGaussFilter(1.0, 1.0, 0.0, "none", "dc_center", 512, 512);
	///   </code>
	/// </remarks>
	public void GenGaussFilter(double sigma1, double sigma2, double phi, string norm, string mode, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1437);
		JlNativeApi.StoreD(proc, 0, sigma1);
		JlNativeApi.StoreD(proc, 1, sigma2);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreS(proc, 3, norm);
		JlNativeApi.StoreS(proc, 4, mode);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Generate a derivative filter in the frequency domain.
	/// </summary>
	/// <param name="derivative">Derivative to be computed. Default: "x"</param>
	/// <param name="exponent">Exponent used in the reverse transform. Default: 1</param>
	/// <param name="norm">Normalizing factor of the filter. Default: "none"</param>
	/// <param name="mode">Location of the DC term in the frequency domain. Default: "dc_center"</param>
	/// <param name="width">Width of the image (filter). Default: 512</param>
	/// <param name="height">Height of the image (filter). Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>生成 derivative 滤波 在 frequency domain。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenDerivativeFilter("x", 1, "none", "dc_center", 512, 512);
	///   </code>
	/// </remarks>
	public void GenDerivativeFilter(string derivative, int exponent, string norm, string mode, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1438);
		JlNativeApi.StoreS(proc, 0, derivative);
		JlNativeApi.StoreI(proc, 1, exponent);
		JlNativeApi.StoreS(proc, 2, norm);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.StoreI(proc, 4, width);
		JlNativeApi.StoreI(proc, 5, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

























	/// <summary>
	///   Histogram linearization of images
	/// </summary>
	/// <returns>Image with linearized gray values.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Histogram linearization 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EquHistoImage();
	///   </code>
	/// </remarks>
	public JlImage EquHistoImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1469);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Illuminate image.
	/// </summary>
	/// <param name="maskWidth">Width of low pass mask. Default: 101</param>
	/// <param name="maskHeight">Height of low pass mask. Default: 101</param>
	/// <param name="factor">Scales the "`correction gray value"' added to the original gray values. Default: 0.7</param>
	/// <returns>"`Illuminated"' image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Illuminate 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Illuminate(101, 101, 0.7);
	///   </code>
	/// </remarks>
	public JlImage Illuminate(int maskWidth, int maskHeight, double factor)
	{
		IntPtr proc = JlNativeApi.PreCall(1470);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, factor);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Enhance contrast of the image.
	/// </summary>
	/// <param name="maskWidth">Width of low pass mask. Default: 7</param>
	/// <param name="maskHeight">Height of the low pass mask. Default: 7</param>
	/// <param name="factor">Intensity of contrast emphasis. Default: 1.0</param>
	/// <returns>contrast enhanced image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Enhance contrast 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Emphasize(7, 7, 1.0);
	///   </code>
	/// </remarks>
	public JlImage Emphasize(int maskWidth, int maskHeight, double factor)
	{
		IntPtr proc = JlNativeApi.PreCall(1471);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, factor);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Maximum gray value spreading in the value range 0 to 255.
	/// </summary>
	/// <returns>contrast enhanced image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Maximum 灰度值 spreading 在 值 range 0 255。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ScaleImageMax();
	///   </code>
	/// </remarks>
	public JlImage ScaleImageMax()
	{
		IntPtr proc = JlNativeApi.PreCall(1472);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}









	/// <summary>
	///   Detect edges (amplitude) using the Sobel operator.
	/// </summary>
	/// <param name="filterType">Filter type. Default: "sum_abs"</param>
	/// <param name="size">Size of filter mask. Default: 3</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect edges (amplitude) using the Sobel operator。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SobelAmp("sum_abs", 3);
	///   </code>
	/// </remarks>
	public JlImage SobelAmp(string filterType, JlTuple size)
	{
		IntPtr proc = JlNativeApi.PreCall(1481);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.Store(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(size);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect edges (amplitude) using the Sobel operator.
	/// </summary>
	/// <param name="filterType">Filter type. Default: "sum_abs"</param>
	/// <param name="size">Size of filter mask. Default: 3</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect edges (amplitude) using the Sobel operator。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SobelAmp("sum_abs", 3);
	///   </code>
	/// </remarks>
	public JlImage SobelAmp(string filterType, int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1481);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.StoreI(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect edges (amplitude and direction) using the Sobel operator.
	/// </summary>
	/// <param name="edgeDirection">Edge direction image.</param>
	/// <param name="filterType">Filter type. Default: "sum_abs"</param>
	/// <param name="size">Size of filter mask. Default: 3</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect edges (amplitude and direction) using the Sobel operator。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SobelDir(out JlImage edgeDirection, "sum_abs", 3);
	///   </code>
	/// </remarks>
	public JlImage SobelDir(out JlImage edgeDirection, string filterType, JlTuple size)
	{
		IntPtr proc = JlNativeApi.PreCall(1482);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.Store(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(size);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out edgeDirection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect edges (amplitude and direction) using the Sobel operator.
	/// </summary>
	/// <param name="edgeDirection">Edge direction image.</param>
	/// <param name="filterType">Filter type. Default: "sum_abs"</param>
	/// <param name="size">Size of filter mask. Default: 3</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect edges (amplitude and direction) using the Sobel operator。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SobelDir(out JlImage edgeDirection, "sum_abs", 3);
	///   </code>
	/// </remarks>
	public JlImage SobelDir(out JlImage edgeDirection, string filterType, int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1482);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.StoreI(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out edgeDirection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect edges using the Roberts filter.
	/// </summary>
	/// <param name="filterType">Filter type. Default: "gradient_sum"</param>
	/// <returns>Roberts-filtered result images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect 边缘 使用 Roberts 滤波。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Roberts("gradient_sum");
	///   </code>
	/// </remarks>
	public JlImage Roberts(string filterType)
	{
		IntPtr proc = JlNativeApi.PreCall(1483);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the Laplace operator by using finite differences.
	/// </summary>
	/// <param name="resultType">Type of the result image, whereas for byte and uint2 the absolute value is used. Default: "absolute"</param>
	/// <param name="maskSize">Size of filter mask. Default: 3</param>
	/// <param name="filterMask">Filter mask used in the Laplace operator Default: "n_4"</param>
	/// <returns>Laplace-filtered result image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Calculate the Laplace operator by using finite differences。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Laplace("absolute", 3, "n_4");
	///   </code>
	/// </remarks>
	public JlImage Laplace(string resultType, JlTuple maskSize, string filterMask)
	{
		IntPtr proc = JlNativeApi.PreCall(1484);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, resultType);
		JlNativeApi.Store(proc, 1, maskSize);
		JlNativeApi.StoreS(proc, 2, filterMask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskSize);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the Laplace operator by using finite differences.
	/// </summary>
	/// <param name="resultType">Type of the result image, whereas for byte and uint2 the absolute value is used. Default: "absolute"</param>
	/// <param name="maskSize">Size of filter mask. Default: 3</param>
	/// <param name="filterMask">Filter mask used in the Laplace operator Default: "n_4"</param>
	/// <returns>Laplace-filtered result image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Calculate the Laplace operator by using finite differences。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Laplace("absolute", 3, "n_4");
	///   </code>
	/// </remarks>
	public JlImage Laplace(string resultType, int maskSize, string filterMask)
	{
		IntPtr proc = JlNativeApi.PreCall(1484);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, resultType);
		JlNativeApi.StoreI(proc, 1, maskSize);
		JlNativeApi.StoreS(proc, 2, filterMask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract high frequency components from an image.
	/// </summary>
	/// <param name="width">Width of the filter mask. Default: 9</param>
	/// <param name="height">Height of the filter mask. Default: 9</param>
	/// <returns>High-pass-filtered result image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>提取 high frequency components 从 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.HighpassImage(9, 9);
	///   </code>
	/// </remarks>
	public JlImage HighpassImage(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1485);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract subpixel precise color edges using Deriche, Shen, or Canny filters.
	/// </summary>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 40</param>
	/// <returns>Extracted edges.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘Color亚像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesColorSubPix("canny", 1.0, 20, 40);
	///   </code>
	/// </remarks>
	public JlXLDCont EdgesColorSubPix(string filter, double alpha, JlTuple low, JlTuple high)
	{
		IntPtr proc = JlNativeApi.PreCall(1487);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.Store(proc, 2, low);
		JlNativeApi.Store(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract subpixel precise color edges using Deriche, Shen, or Canny filters.
	/// </summary>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 40</param>
	/// <returns>Extracted edges.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘Color亚像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesColorSubPix("canny", 1.0, 20, 40);
	///   </code>
	/// </remarks>
	public JlXLDCont EdgesColorSubPix(string filter, double alpha, double low, double high)
	{
		IntPtr proc = JlNativeApi.PreCall(1487);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreD(proc, 2, low);
		JlNativeApi.StoreD(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract color edges using Canny, Deriche, or Shen filters.
	/// </summary>
	/// <param name="imaDir">Edge direction image.</param>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="NMS">Non-maximum suppression ('none', if not desired). Default: "nms"</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation (negative if no thresholding is desired). Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation (negative if no thresholding is desired). Default: 40</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘Color。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesColor(out JlImage imaDir, "canny", 1.0, "nms", 20, 40);
	///   </code>
	/// </remarks>
	public JlImage EdgesColor(out JlImage imaDir, string filter, double alpha, string NMS, int low, int high)
	{
		IntPtr proc = JlNativeApi.PreCall(1488);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreS(proc, 2, NMS);
		JlNativeApi.StoreI(proc, 3, low);
		JlNativeApi.StoreI(proc, 4, high);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imaDir);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract sub-pixel precise edges using Deriche, Lanser, Shen, or Canny filters.
	/// </summary>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 40</param>
	/// <returns>Extracted edges.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘亚像素。</para>
	///   <para><b>典型场景</b></para>
	///   <para>亚像素边缘提取与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesSubPix("canny", 1.0, 20, 40);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>SelectContoursXld、FitLineContourXld</para>
	/// </remarks>
	public JlXLDCont EdgesSubPix(string filter, double alpha, JlTuple low, JlTuple high)
	{
		IntPtr proc = JlNativeApi.PreCall(1489);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.Store(proc, 2, low);
		JlNativeApi.Store(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract sub-pixel precise edges using Deriche, Lanser, Shen, or Canny filters.
	/// </summary>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation. Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation. Default: 40</param>
	/// <returns>Extracted edges.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘亚像素。</para>
	///   <para><b>典型场景</b></para>
	///   <para>亚像素边缘提取与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesSubPix("canny", 1.0, 20, 40);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>SelectContoursXld、FitLineContourXld</para>
	/// </remarks>
	public JlXLDCont EdgesSubPix(string filter, double alpha, int low, int high)
	{
		IntPtr proc = JlNativeApi.PreCall(1489);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreI(proc, 2, low);
		JlNativeApi.StoreI(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract edges using Deriche, Lanser, Shen, or Canny filters.
	/// </summary>
	/// <param name="imaDir">Edge direction image.</param>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="NMS">Non-maximum suppression ('none', if not desired). Default: "nms"</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation (negative, if no thresholding is desired). Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation (negative, if no thresholding is desired). Default: 40</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesImage(out JlImage imaDir, "canny", 1.0, "nms", 20, 40);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>SelectContoursXld、FitLineContourXld</para>
	/// </remarks>
	public JlImage EdgesImage(out JlImage imaDir, string filter, double alpha, string NMS, JlTuple low, JlTuple high)
	{
		IntPtr proc = JlNativeApi.PreCall(1490);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreS(proc, 2, NMS);
		JlNativeApi.Store(proc, 3, low);
		JlNativeApi.Store(proc, 4, high);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imaDir);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract edges using Deriche, Lanser, Shen, or Canny filters.
	/// </summary>
	/// <param name="imaDir">Edge direction image.</param>
	/// <param name="filter">Edge operator to be applied. Default: "canny"</param>
	/// <param name="alpha">Filter parameter: small values result in strong smoothing, and thus less detail (opposite for 'canny'). Default: 1.0</param>
	/// <param name="NMS">Non-maximum suppression ('none', if not desired). Default: "nms"</param>
	/// <param name="low">Lower threshold for the hysteresis threshold operation (negative, if no thresholding is desired). Default: 20</param>
	/// <param name="high">Upper threshold for the hysteresis threshold operation (negative, if no thresholding is desired). Default: 40</param>
	/// <returns>Edge amplitude (gradient magnitude) image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>边缘图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EdgesImage(out JlImage imaDir, "canny", 1.0, "nms", 20, 40);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>SelectContoursXld、FitLineContourXld</para>
	/// </remarks>
	public JlImage EdgesImage(out JlImage imaDir, string filter, double alpha, string NMS, int low, int high)
	{
		IntPtr proc = JlNativeApi.PreCall(1490);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreS(proc, 2, NMS);
		JlNativeApi.StoreI(proc, 3, low);
		JlNativeApi.StoreI(proc, 4, high);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imaDir);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>LoG 带通算子：高斯平滑后再拉普拉斯微分，原生算子 id 1492，sigma 以元组传入。</summary>
	/// <param name="sigma">高斯平滑的标准差，单位是像素。Default: 2.0</param>
	/// <returns>拉普拉斯滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先以标准差 <paramref name="sigma"/> 做高斯平滑压噪，再取拉普拉斯（二阶导数），
	///   等效带通：只有尺度约在 σ 附近的灰度结构被突出，恒定区和缓变背景输出接近 0。相比不做平滑的有限差分
	///   <see cref="Laplace(string,int,string)"/>，对单像素噪声的敏感度大幅下降；<paramref name="sigma"/> 因此是
	///   "目标尺度旋钮"——要找某一直径的斑点/孔洞，σ 取该尺度的一半上下 [待实测：精确对应关系]。</para>
	///   <para><b>负值域（最容易错的地方）</b>LoG 输出天然含负值（边缘一侧为负）。本算子没有 <c>resultType</c> 参数
	///   （不同于 <c>Laplace</c> 可要绝对值），输出类型与负值是否被截断完全由原生决定 [待实测]。稳妥做法是先转
	///   <c>float</c> 再滤波，或用 <see cref="ScaleImage(double,double)"/> 加偏移抬到正值域后再 <c>Threshold</c>，
	///   否则负边缘响应在 <c>byte</c> 域里被静默丢光。</para>
	///   <para><b>与相邻算子的取舍</b>要更快（两次高斯相减的近似 LoG，Marr 路线）→ <see cref="DiffOfGauss(double,double)"/>；
	///   要梯度幅值/方向（一阶）而不是二阶过零 → <see cref="SobelAmp(string,int)"/>、<see cref="SobelDir(out JlImage,string,int)"/>。
	///   多通道输入行为 [待实测]。</para>
	///   <para><b>参数取向</b>元组版 <paramref name="sigma"/> 走 <c>Store</c>+<c>UnpinTuple</c>，多元素（一次给多个尺度）
	///   语义本层看不出来 [待实测]；单尺度用 <see cref="LaplaceOfGauss(double)"/> 更省事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage f = img.ConvertImageType("float");
	///   using JlImage log = f.LaplaceOfGauss(new JlTuple(2.0));
	///   using JlImage shifted = log.ScaleImage(1.0, 128.0);   // 抬偏移，负响应不再丢
	///   </code>
	///   <para><b>资源与坑</b>σ≤0 行为 [待实测]；float 中间图 + LoG 图内存是原图数倍，大图及时 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage LaplaceOfGauss(JlTuple sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1492);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>LoG 算子（单个 σ 以 double 传入）。</summary>
	/// <param name="sigma">高斯平滑的标准差，单位是像素。Default: 2.0</param>
	/// <returns>拉普拉斯滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>σ 的尺度含义与负值域处理（先转 float 或加偏移再 Threshold）见
	///   <see cref="LaplaceOfGauss(JlTuple)"/>：同一原生 id 1492，本版本 <c>StoreD</c> 直写 σ，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage log = img.LaplaceOfGauss(2.0);
	///   </code>
	/// </remarks>
	public JlImage LaplaceOfGauss(double sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1492);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Approximate the LoG operator (Laplace of Gaussian).
	/// </summary>
	/// <param name="sigma">Smoothing parameter of the Laplace operator to approximate. Default: 3.0</param>
	/// <param name="sigFactor">Ratio of the standard deviations used (Marr recommends 1.6). Default: 1.6</param>
	/// <returns>LoG image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>DiffOf高斯。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.DiffOfGauss(3.0, 1.6);
	///   </code>
	/// </remarks>
	public JlImage DiffOfGauss(double sigma, double sigFactor)
	{
		IntPtr proc = JlNativeApi.PreCall(1493);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, sigFactor);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Detect straight edge segments.
	/// </summary>
	/// <param name="sobelSize">Mask size of the Sobel operator. Default: 5</param>
	/// <param name="minAmplitude">Minimum edge strength. Default: 32</param>
	/// <param name="maxDistance">Maximum distance of the approximating line to its original edge. Default: 3</param>
	/// <param name="minLength">Minimum length of to resulting line segments. Default: 10</param>
	/// <param name="beginRow">Row coordinate of the line segments' start points.</param>
	/// <param name="beginCol">Column coordinate of the line segments' start points.</param>
	/// <param name="endRow">Row coordinate of the line segments' end points.</param>
	/// <param name="endCol">Column coordinate of the line segments' end points.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Detect 直线 边缘 segments。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.DetectEdgeSegments(5, 32, 3, 10, out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol);
	///   </code>
	/// </remarks>
	public void DetectEdgeSegments(int sobelSize, int minAmplitude, int maxDistance, int minLength, out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol)
	{
		IntPtr proc = JlNativeApi.PreCall(1496);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, sobelSize);
		JlNativeApi.StoreI(proc, 1, minAmplitude);
		JlNativeApi.StoreI(proc, 2, maxDistance);
		JlNativeApi.StoreI(proc, 3, minLength);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out beginRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out beginCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out endRow);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out endCol);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}




	/// <summary>
	///   Convert a single-channel color filter array image into an RGB image.
	/// </summary>
	/// <param name="CFAType">Color filter array type. Default: "bayer_gb"</param>
	/// <param name="interpolation">Interpolation type. Default: "bilinear"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Convert a single-channel color filter array image into an RGB image。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CfaToRgb("bayer_gb", "bilinear");
	///   </code>
	/// </remarks>
	public JlImage CfaToRgb(string CFAType, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1500);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, CFAType);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Transform an RGB image into a gray scale image.
	/// </summary>
	/// <returns>Gray scale image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Transform RGB 图像 为 灰度 scale 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.Rgb1ToGray();
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>Threshold、MeanImage</para>
	/// </remarks>
	public JlImage Rgb1ToGray()
	{
		IntPtr proc = JlNativeApi.PreCall(1501);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Transform an RGB image to a gray scale image.
	/// </summary>
	/// <param name="imageGreen">Input image (green channel).</param>
	/// <param name="imageBlue">Input image (blue channel).</param>
	/// <returns>Gray scale image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Transform RGB 图像 灰度 scale 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageGreen = ...;
	///   JlImage imageBlue = ...;
	///   JlImage obj = ...;
	///   var result = obj.Rgb3ToGray(imageGreen, imageBlue);
	///   </code>
	/// </remarks>
	public JlImage Rgb3ToGray(JlImage imageGreen, JlImage imageBlue)
	{
		IntPtr proc = JlNativeApi.PreCall(1502);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageGreen);
		JlNativeApi.Store(proc, 3, imageBlue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageGreen);
		GC.KeepAlive(imageBlue);
		return obj;
	}

	/// <summary>
	///   Transform an image from the RGB color space to an arbitrary color space.
	/// </summary>
	/// <param name="imageGreen">Input image (green channel).</param>
	/// <param name="imageBlue">Input image (blue channel).</param>
	/// <param name="imageResult2">Color-transformed output image (channel 1).</param>
	/// <param name="imageResult3">Color-transformed output image (channel 1).</param>
	/// <param name="colorSpace">Color space of the output image. Default: "hsv"</param>
	/// <returns>Color-transformed output image (channel 1).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>变换FromRGB。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageGreen = ...;
	///   JlImage imageBlue = ...;
	///   JlImage obj = ...;
	///   var result = obj.TransFromRgb(imageGreen, imageBlue, out JlImage imageResult2, out JlImage imageResult3, "hsv");
	///   </code>
	/// </remarks>
	public JlImage TransFromRgb(JlImage imageGreen, JlImage imageBlue, out JlImage imageResult2, out JlImage imageResult3, string colorSpace)
	{
		IntPtr proc = JlNativeApi.PreCall(1503);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageGreen);
		JlNativeApi.Store(proc, 3, imageBlue);
		JlNativeApi.StoreS(proc, 0, colorSpace);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imageResult2);
		err = LoadNew(proc, 3, err, out imageResult3);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageGreen);
		GC.KeepAlive(imageBlue);
		return obj;
	}

	/// <summary>
	///   Transform an image from an arbitrary color space to the RGB color space.
	/// </summary>
	/// <param name="imageInput2">Input image (channel 2).</param>
	/// <param name="imageInput3">Input image (channel 3).</param>
	/// <param name="imageGreen">Green channel.</param>
	/// <param name="imageBlue">Blue channel.</param>
	/// <param name="colorSpace">Color space of the input image. Default: "hsv"</param>
	/// <returns>Red channel.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>变换ToRGB。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageInput2 = ...;
	///   JlImage imageInput3 = ...;
	///   JlImage obj = ...;
	///   var result = obj.TransToRgb(imageInput2, imageInput3, out JlImage imageGreen, out JlImage imageBlue, "hsv");
	///   </code>
	/// </remarks>
	public JlImage TransToRgb(JlImage imageInput2, JlImage imageInput3, out JlImage imageGreen, out JlImage imageBlue, string colorSpace)
	{
		IntPtr proc = JlNativeApi.PreCall(1504);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageInput2);
		JlNativeApi.Store(proc, 3, imageInput3);
		JlNativeApi.StoreS(proc, 0, colorSpace);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imageGreen);
		err = LoadNew(proc, 3, err, out imageBlue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageInput2);
		GC.KeepAlive(imageInput3);
		return obj;
	}

	/// <summary>
	///   Logical "AND" of each pixel using a bit mask.
	/// </summary>
	/// <param name="bitMask">Bit field Default: 128</param>
	/// <returns>Result image(s) by combination with mask.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Logical "和" each 像素 使用 bit 掩膜。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.BitMask(128);
	///   </code>
	/// </remarks>
	public JlImage BitMask(int bitMask)
	{
		IntPtr proc = JlNativeApi.PreCall(1505);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, bitMask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Extract a bit from the pixels.
	/// </summary>
	/// <param name="bit">Bit to be selected. Default: 8</param>
	/// <returns>Result image(s) by extraction.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>提取 bit 从 像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.BitSlice(8);
	///   </code>
	/// </remarks>
	public JlImage BitSlice(int bit)
	{
		IntPtr proc = JlNativeApi.PreCall(1506);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, bit);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Right shift of all pixels of the image.
	/// </summary>
	/// <param name="shift">shift value Default: 3</param>
	/// <returns>Result image(s) by shift operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Right shift all 像素 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.BitRshift(3);
	///   </code>
	/// </remarks>
	public JlImage BitRshift(int shift)
	{
		IntPtr proc = JlNativeApi.PreCall(1507);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, shift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Left shift of all pixels of the image.
	/// </summary>
	/// <param name="shift">Shift value. Default: 3</param>
	/// <returns>Result image(s) by shift operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Left shift all 像素 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.BitLshift(3);
	///   </code>
	/// </remarks>
	public JlImage BitLshift(int shift)
	{
		IntPtr proc = JlNativeApi.PreCall(1508);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, shift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Complement all bits of the pixels.
	/// </summary>
	/// <returns>Result image(s) by complement operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Complement all bits 像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.BitNot();
	///   </code>
	/// </remarks>
	public JlImage BitNot()
	{
		IntPtr proc = JlNativeApi.PreCall(1509);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Bit-by-bit XOR of all pixels of the input images.
	/// </summary>
	/// <param name="image2">Input image(s) 2.</param>
	/// <returns>Result image(s) by XOR-operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Bit-通过-bit XOR all 像素 输入图像s。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.BitXor(image2);
	///   </code>
	/// </remarks>
	public JlImage BitXor(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1510);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Bit-by-bit OR of all pixels of the input images.
	/// </summary>
	/// <param name="image2">Input image(s) 2.</param>
	/// <returns>Result image(s) by OR-operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Bit-通过-bit 或 all 像素 输入图像s。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.BitOr(image2);
	///   </code>
	/// </remarks>
	public JlImage BitOr(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1511);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Bit-by-bit AND of all pixels of the input images.
	/// </summary>
	/// <param name="image2">Input image(s) 2.</param>
	/// <returns>Result image(s) by AND-operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Bit-通过-bit 和 all 像素 输入图像s。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.BitAnd(image2);
	///   </code>
	/// </remarks>
	public JlImage BitAnd(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1512);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Perform a gamma encoding or decoding of an image.
	/// </summary>
	/// <param name="gamma">Gamma coefficient of the exponential part of the transformation. Default: 0.416666666667</param>
	/// <param name="offset">Offset of the exponential part of the transformation. Default: 0.055</param>
	/// <param name="threshold">Gray value for which the transformation switches from linear to exponential. Default: 0.0031308</param>
	/// <param name="maxGray">Maximum gray value of the input image type. Default: 255.0</param>
	/// <param name="encode">If 'true', perform a gamma encoding, otherwise a gamma decoding. Default: "true"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Gamma图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GammaImage(0.416666666667, 0.055, 0.0031308, 255.0, "true");
	///   </code>
	/// </remarks>
	public JlImage GammaImage(double gamma, double offset, double threshold, JlTuple maxGray, string encode)
	{
		IntPtr proc = JlNativeApi.PreCall(1513);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, gamma);
		JlNativeApi.StoreD(proc, 1, offset);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.Store(proc, 3, maxGray);
		JlNativeApi.StoreS(proc, 4, encode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxGray);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Perform a gamma encoding or decoding of an image.
	/// </summary>
	/// <param name="gamma">Gamma coefficient of the exponential part of the transformation. Default: 0.416666666667</param>
	/// <param name="offset">Offset of the exponential part of the transformation. Default: 0.055</param>
	/// <param name="threshold">Gray value for which the transformation switches from linear to exponential. Default: 0.0031308</param>
	/// <param name="maxGray">Maximum gray value of the input image type. Default: 255.0</param>
	/// <param name="encode">If 'true', perform a gamma encoding, otherwise a gamma decoding. Default: "true"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Gamma图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.GammaImage(0.416666666667, 0.055, 0.0031308, 255.0, "true");
	///   </code>
	/// </remarks>
	public JlImage GammaImage(double gamma, double offset, double threshold, double maxGray, string encode)
	{
		IntPtr proc = JlNativeApi.PreCall(1513);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, gamma);
		JlNativeApi.StoreD(proc, 1, offset);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreD(proc, 3, maxGray);
		JlNativeApi.StoreS(proc, 4, encode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Raise an image to a power.
	/// </summary>
	/// <param name="exponent">Power to which the gray values are raised. Default: 2</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Raise 图像 power。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.PowImage(2);
	///   </code>
	/// </remarks>
	public JlImage PowImage(JlTuple exponent)
	{
		IntPtr proc = JlNativeApi.PreCall(1514);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, exponent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(exponent);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Raise an image to a power.
	/// </summary>
	/// <param name="exponent">Power to which the gray values are raised. Default: 2</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Raise 图像 power。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.PowImage(2);
	///   </code>
	/// </remarks>
	public JlImage PowImage(double exponent)
	{
		IntPtr proc = JlNativeApi.PreCall(1514);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, exponent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the exponentiation of an image.
	/// </summary>
	/// <param name="baseVal">Base of the exponentiation. Default: "e"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 exponentiation 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ExpImage("e");
	///   </code>
	/// </remarks>
	public JlImage ExpImage(JlTuple baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1515);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(baseVal);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the exponentiation of an image.
	/// </summary>
	/// <param name="baseVal">Base of the exponentiation. Default: "e"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 exponentiation 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ExpImage("e");
	///   </code>
	/// </remarks>
	public JlImage ExpImage(string baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1515);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the logarithm of an image.
	/// </summary>
	/// <param name="baseVal">Base of the logarithm. Default: "e"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 logarithm 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LogImage("e");
	///   </code>
	/// </remarks>
	public JlImage LogImage(JlTuple baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1516);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(baseVal);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the logarithm of an image.
	/// </summary>
	/// <param name="baseVal">Base of the logarithm. Default: "e"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 logarithm 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.LogImage("e");
	///   </code>
	/// </remarks>
	public JlImage LogImage(string baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1516);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the arctangent of two images.
	/// </summary>
	/// <param name="imageX">Input image 2.</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 arctangent two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageX = ...;
	///   JlImage obj = ...;
	///   var result = obj.Atan2Image(imageX);
	///   </code>
	/// </remarks>
	public JlImage Atan2Image(JlImage imageX)
	{
		IntPtr proc = JlNativeApi.PreCall(1517);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageX);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageX);
		return obj;
	}

	/// <summary>
	///   Calculate the arctangent of an image.
	/// </summary>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 arctangent 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AtanImage();
	///   </code>
	/// </remarks>
	public JlImage AtanImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1518);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the arccosine of an image.
	/// </summary>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 arccosine 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AcosImage();
	///   </code>
	/// </remarks>
	public JlImage AcosImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1519);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the arcsine of an image.
	/// </summary>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 arcsine 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AsinImage();
	///   </code>
	/// </remarks>
	public JlImage AsinImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1520);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the tangent of an image.
	/// </summary>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 tangent 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.TanImage();
	///   </code>
	/// </remarks>
	public JlImage TanImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1521);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the cosine of an image.
	/// </summary>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 cosine 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CosImage();
	///   </code>
	/// </remarks>
	public JlImage CosImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1522);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the sine of an image.
	/// </summary>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 sine 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SinImage();
	///   </code>
	/// </remarks>
	public JlImage SinImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1523);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the absolute difference of two images.
	/// </summary>
	/// <param name="image2">Input image 2.</param>
	/// <param name="mult">Scale factor. Default: 1.0</param>
	/// <returns>Absolute value of the difference of the input images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 absolute 求差 two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.AbsDiffImage(image2, 1.0);
	///   </code>
	/// </remarks>
	public JlImage AbsDiffImage(JlImage image2, JlTuple mult)
	{
		IntPtr proc = JlNativeApi.PreCall(1524);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Calculate the absolute difference of two images.
	/// </summary>
	/// <param name="image2">Input image 2.</param>
	/// <param name="mult">Scale factor. Default: 1.0</param>
	/// <returns>Absolute value of the difference of the input images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 absolute 求差 two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.AbsDiffImage(image2, 1.0);
	///   </code>
	/// </remarks>
	public JlImage AbsDiffImage(JlImage image2, double mult)
	{
		IntPtr proc = JlNativeApi.PreCall(1524);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Calculate the square root of an image.
	/// </summary>
	/// <returns>Output image</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 square root 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SqrtImage();
	///   </code>
	/// </remarks>
	public JlImage SqrtImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1525);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Subtract two images.
	/// </summary>
	/// <param name="imageSubtrahend">Subtrahend(s).</param>
	/// <param name="mult">Correction factor. Default: 1.0</param>
	/// <param name="add">Correction value. Default: 128.0</param>
	/// <returns>Result image(s) by the subtraction.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Subtract two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageSubtrahend = ...;
	///   JlImage obj = ...;
	///   var result = obj.SubImage(imageSubtrahend, 1.0, 128.0);
	///   </code>
	/// </remarks>
	public JlImage SubImage(JlImage imageSubtrahend, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1526);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageSubtrahend);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageSubtrahend);
		return obj;
	}

	/// <summary>
	///   Subtract two images.
	/// </summary>
	/// <param name="imageSubtrahend">Subtrahend(s).</param>
	/// <param name="mult">Correction factor. Default: 1.0</param>
	/// <param name="add">Correction value. Default: 128.0</param>
	/// <returns>Result image(s) by the subtraction.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Subtract two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage imageSubtrahend = ...;
	///   JlImage obj = ...;
	///   var result = obj.SubImage(imageSubtrahend, 1.0, 128.0);
	///   </code>
	/// </remarks>
	public JlImage SubImage(JlImage imageSubtrahend, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1526);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageSubtrahend);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageSubtrahend);
		return obj;
	}

	/// <summary>
	///   Scale the gray values of an image.
	/// </summary>
	/// <param name="mult">Scale factor. Default: 0.01</param>
	/// <param name="add">Offset. Default: 0</param>
	/// <returns>Result image(s) by the scale.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Scale 灰度值s 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ScaleImage(0.01, 0);
	///   </code>
	/// </remarks>
	public JlImage ScaleImage(JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1527);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Scale the gray values of an image.
	/// </summary>
	/// <param name="mult">Scale factor. Default: 0.01</param>
	/// <param name="add">Offset. Default: 0</param>
	/// <returns>Result image(s) by the scale.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Scale 灰度值s 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ScaleImage(0.01, 0);
	///   </code>
	/// </remarks>
	public JlImage ScaleImage(double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1527);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Divide two images.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <param name="mult">Factor for gray range adaption. Default: 255</param>
	/// <param name="add">Value for gray range adaption. Default: 0</param>
	/// <returns>Result image(s) by the division.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Divide two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.DivImage(image2, 255, 0);
	///   </code>
	/// </remarks>
	public JlImage DivImage(JlImage image2, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1528);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Divide two images.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <param name="mult">Factor for gray range adaption. Default: 255</param>
	/// <param name="add">Value for gray range adaption. Default: 0</param>
	/// <returns>Result image(s) by the division.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Divide two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.DivImage(image2, 255, 0);
	///   </code>
	/// </remarks>
	public JlImage DivImage(JlImage image2, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1528);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Multiply two images.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <param name="mult">Factor for gray range adaption. Default: 0.005</param>
	/// <param name="add">Value for gray range adaption. Default: 0</param>
	/// <returns>Result image(s) by the product.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Multiply two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.MultImage(image2, 0.005, 0);
	///   </code>
	/// </remarks>
	public JlImage MultImage(JlImage image2, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1529);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Multiply two images.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <param name="mult">Factor for gray range adaption. Default: 0.005</param>
	/// <param name="add">Value for gray range adaption. Default: 0</param>
	/// <returns>Result image(s) by the product.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Multiply two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.MultImage(image2, 0.005, 0);
	///   </code>
	/// </remarks>
	public JlImage MultImage(JlImage image2, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1529);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Add two images.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <param name="mult">Factor for gray value adaption. Default: 0.5</param>
	/// <param name="add">Value for gray value range adaption. Default: 0</param>
	/// <returns>Result image(s) by the addition.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.AddImage(image2, 0.5, 0);
	///   </code>
	/// </remarks>
	public JlImage AddImage(JlImage image2, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1530);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Add two images.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <param name="mult">Factor for gray value adaption. Default: 0.5</param>
	/// <param name="add">Value for gray value range adaption. Default: 0</param>
	/// <returns>Result image(s) by the addition.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add two 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.AddImage(image2, 0.5, 0);
	///   </code>
	/// </remarks>
	public JlImage AddImage(JlImage image2, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1530);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Calculate the absolute value (modulus) of an image.
	/// </summary>
	/// <returns>Result image(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 absolute 值 (modulus) 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AbsImage();
	///   </code>
	/// </remarks>
	public JlImage AbsImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1531);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the minimum of two images pixel by pixel.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <returns>Result image(s) by the minimization.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 minimum two 图像 像素 通过 像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.MinImage(image2);
	///   </code>
	/// </remarks>
	public JlImage MinImage(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1532);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Calculate the maximum of two images pixel by pixel.
	/// </summary>
	/// <param name="image2">Image(s) 2.</param>
	/// <returns>Result image(s) by the maximization.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 maximum two 图像 像素 通过 像素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image2 = ...;
	///   JlImage obj = ...;
	///   var result = obj.MaxImage(image2);
	///   </code>
	/// </remarks>
	public JlImage MaxImage(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1533);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   Invert an image.
	/// </summary>
	/// <returns>Image(s) with inverted gray values.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Invert 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.InvertImage();
	///   </code>
	/// </remarks>
	public JlImage InvertImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1534);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply an automatic color correction to panorama images.
	/// </summary>
	/// <param name="from">List of source images.</param>
	/// <param name="to">List of destination images.</param>
	/// <param name="referenceImage">Reference image.</param>
	/// <param name="homMatrices2D">Projective matrices.</param>
	/// <param name="estimationMethod">Estimation algorithm for the correction. Default: "standard"</param>
	/// <param name="estimateParameters">Parameters to be estimated. Default: ["mult_gray"]</param>
	/// <param name="OECFModel">Model of OECF to be used. Default: ["laguerre"]</param>
	/// <returns>Output images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>AdjustMosaic图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple from = ...;
	///   JlTuple to = ...;
	///   JlTuple homMatrices2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.AdjustMosaicImages(from, to, 0, homMatrices2D, "standard", ["mult_gray"], "["laguerre"]");
	///   </code>
	/// </remarks>
	public JlImage AdjustMosaicImages(JlTuple from, JlTuple to, int referenceImage, JlTuple homMatrices2D, string estimationMethod, JlTuple estimateParameters, string OECFModel)
	{
		IntPtr proc = JlNativeApi.PreCall(1535);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, from);
		JlNativeApi.Store(proc, 1, to);
		JlNativeApi.StoreI(proc, 2, referenceImage);
		JlNativeApi.Store(proc, 3, homMatrices2D);
		JlNativeApi.StoreS(proc, 4, estimationMethod);
		JlNativeApi.Store(proc, 5, estimateParameters);
		JlNativeApi.StoreS(proc, 6, OECFModel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(from);
		JlNativeApi.UnpinTuple(to);
		JlNativeApi.UnpinTuple(homMatrices2D);
		JlNativeApi.UnpinTuple(estimateParameters);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply an automatic color correction to panorama images.
	/// </summary>
	/// <param name="from">List of source images.</param>
	/// <param name="to">List of destination images.</param>
	/// <param name="referenceImage">Reference image.</param>
	/// <param name="homMatrices2D">Projective matrices.</param>
	/// <param name="estimationMethod">Estimation algorithm for the correction. Default: "standard"</param>
	/// <param name="estimateParameters">Parameters to be estimated. Default: ["mult_gray"]</param>
	/// <param name="OECFModel">Model of OECF to be used. Default: ["laguerre"]</param>
	/// <returns>Output images.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>AdjustMosaic图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple from = ...;
	///   JlTuple to = ...;
	///   JlTuple homMatrices2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.AdjustMosaicImages(from, to, 0, homMatrices2D, "standard", "["mult_gray"]", "["laguerre"]");
	///   </code>
	/// </remarks>
	public JlImage AdjustMosaicImages(JlTuple from, JlTuple to, int referenceImage, JlTuple homMatrices2D, string estimationMethod, string estimateParameters, string OECFModel)
	{
		IntPtr proc = JlNativeApi.PreCall(1535);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, from);
		JlNativeApi.Store(proc, 1, to);
		JlNativeApi.StoreI(proc, 2, referenceImage);
		JlNativeApi.Store(proc, 3, homMatrices2D);
		JlNativeApi.StoreS(proc, 4, estimationMethod);
		JlNativeApi.StoreS(proc, 5, estimateParameters);
		JlNativeApi.StoreS(proc, 6, OECFModel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(from);
		JlNativeApi.UnpinTuple(to);
		JlNativeApi.UnpinTuple(homMatrices2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create 6 cube map images of a spherical mosaic.
	/// </summary>
	/// <param name="rear">Rear cube map.</param>
	/// <param name="left">Left cube map.</param>
	/// <param name="right">Right cube map.</param>
	/// <param name="top">Top cube map.</param>
	/// <param name="bottom">Bottom cube map.</param>
	/// <param name="cameraMatrices">(Array of) 3x3 projective camera matrices that determine the internal camera parameters.</param>
	/// <param name="rotationMatrices">Array of 3x3 transformation matrices that determine rotation of the camera in the respective image.</param>
	/// <param name="cubeMapDimension">Width and height of the resulting cube maps. Default: 1000</param>
	/// <param name="stackingOrder">Mode of adding the images to the mosaic image. Default: "voronoi"</param>
	/// <param name="interpolation">Mode of image interpolation. Default: "bilinear"</param>
	/// <returns>Front cube map.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 6 cube map 图像 spherical mosaic。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D cameraMatrices = ...;
	///   JlHomMat2D rotationMatrices = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right, out JlImage top, out JlImage bottom, cameraMatrices, rotationMatrices, 1000, "voronoi", "bilinear");
	///   </code>
	/// </remarks>
	public JlImage GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right, out JlImage top, out JlImage bottom, JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, int cubeMapDimension, JlTuple stackingOrder, string interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1536);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.StoreI(proc, 2, cubeMapDimension);
		JlNativeApi.Store(proc, 3, stackingOrder);
		JlNativeApi.StoreS(proc, 4, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		JlNativeApi.UnpinTuple(stackingOrder);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out rear);
		err = LoadNew(proc, 3, err, out left);
		err = LoadNew(proc, 4, err, out right);
		err = LoadNew(proc, 5, err, out top);
		err = LoadNew(proc, 6, err, out bottom);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create 6 cube map images of a spherical mosaic.
	/// </summary>
	/// <param name="rear">Rear cube map.</param>
	/// <param name="left">Left cube map.</param>
	/// <param name="right">Right cube map.</param>
	/// <param name="top">Top cube map.</param>
	/// <param name="bottom">Bottom cube map.</param>
	/// <param name="cameraMatrices">(Array of) 3x3 projective camera matrices that determine the internal camera parameters.</param>
	/// <param name="rotationMatrices">Array of 3x3 transformation matrices that determine rotation of the camera in the respective image.</param>
	/// <param name="cubeMapDimension">Width and height of the resulting cube maps. Default: 1000</param>
	/// <param name="stackingOrder">Mode of adding the images to the mosaic image. Default: "voronoi"</param>
	/// <param name="interpolation">Mode of image interpolation. Default: "bilinear"</param>
	/// <returns>Front cube map.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 6 cube map 图像 spherical mosaic。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D cameraMatrices = ...;
	///   JlHomMat2D rotationMatrices = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right, out JlImage top, out JlImage bottom, cameraMatrices, rotationMatrices, 1000, "voronoi", "bilinear");
	///   </code>
	/// </remarks>
	public JlImage GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right, out JlImage top, out JlImage bottom, JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, int cubeMapDimension, string stackingOrder, string interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1536);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.StoreI(proc, 2, cubeMapDimension);
		JlNativeApi.StoreS(proc, 3, stackingOrder);
		JlNativeApi.StoreS(proc, 4, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out rear);
		err = LoadNew(proc, 3, err, out left);
		err = LoadNew(proc, 4, err, out right);
		err = LoadNew(proc, 5, err, out top);
		err = LoadNew(proc, 6, err, out bottom);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create a spherical mosaic image.
	/// </summary>
	/// <param name="cameraMatrices">(Array of) 3x3 projective camera matrices that determine the internal camera parameters.</param>
	/// <param name="rotationMatrices">Array of 3x3 transformation matrices that determine rotation of the camera in the respective image.</param>
	/// <param name="latMin">Minimum latitude of points in the spherical mosaic image. Default: -90</param>
	/// <param name="latMax">Maximum latitude of points in the spherical mosaic image. Default: 90</param>
	/// <param name="longMin">Minimum longitude of points in the spherical mosaic image. Default: -180</param>
	/// <param name="longMax">Maximum longitude of points in the spherical mosaic image. Default: 180</param>
	/// <param name="latLongStep">Latitude and longitude angle step width. Default: 0.1</param>
	/// <param name="stackingOrder">Mode of adding the images to the mosaic image. Default: "voronoi"</param>
	/// <param name="interpolation">Mode of interpolation when creating the mosaic image. Default: "bilinear"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 spherical mosaic 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D cameraMatrices = ...;
	///   JlHomMat2D rotationMatrices = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenSphericalMosaic(cameraMatrices, rotationMatrices, -90, 90, -180, 180, 0.1, "voronoi", "bilinear");
	///   </code>
	/// </remarks>
	public JlImage GenSphericalMosaic(JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, JlTuple latMin, JlTuple latMax, JlTuple longMin, JlTuple longMax, JlTuple latLongStep, JlTuple stackingOrder, JlTuple interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1537);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.Store(proc, 2, latMin);
		JlNativeApi.Store(proc, 3, latMax);
		JlNativeApi.Store(proc, 4, longMin);
		JlNativeApi.Store(proc, 5, longMax);
		JlNativeApi.Store(proc, 6, latLongStep);
		JlNativeApi.Store(proc, 7, stackingOrder);
		JlNativeApi.Store(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		JlNativeApi.UnpinTuple(latMin);
		JlNativeApi.UnpinTuple(latMax);
		JlNativeApi.UnpinTuple(longMin);
		JlNativeApi.UnpinTuple(longMax);
		JlNativeApi.UnpinTuple(latLongStep);
		JlNativeApi.UnpinTuple(stackingOrder);
		JlNativeApi.UnpinTuple(interpolation);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create a spherical mosaic image.
	/// </summary>
	/// <param name="cameraMatrices">(Array of) 3x3 projective camera matrices that determine the internal camera parameters.</param>
	/// <param name="rotationMatrices">Array of 3x3 transformation matrices that determine rotation of the camera in the respective image.</param>
	/// <param name="latMin">Minimum latitude of points in the spherical mosaic image. Default: -90</param>
	/// <param name="latMax">Maximum latitude of points in the spherical mosaic image. Default: 90</param>
	/// <param name="longMin">Minimum longitude of points in the spherical mosaic image. Default: -180</param>
	/// <param name="longMax">Maximum longitude of points in the spherical mosaic image. Default: 180</param>
	/// <param name="latLongStep">Latitude and longitude angle step width. Default: 0.1</param>
	/// <param name="stackingOrder">Mode of adding the images to the mosaic image. Default: "voronoi"</param>
	/// <param name="interpolation">Mode of interpolation when creating the mosaic image. Default: "bilinear"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 spherical mosaic 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D cameraMatrices = ...;
	///   JlHomMat2D rotationMatrices = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenSphericalMosaic(cameraMatrices, rotationMatrices, -90, 90, -180, 180, 0.1, "voronoi", "bilinear");
	///   </code>
	/// </remarks>
	public JlImage GenSphericalMosaic(JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, double latMin, double latMax, double longMin, double longMax, double latLongStep, string stackingOrder, string interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1537);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.StoreD(proc, 2, latMin);
		JlNativeApi.StoreD(proc, 3, latMax);
		JlNativeApi.StoreD(proc, 4, longMin);
		JlNativeApi.StoreD(proc, 5, longMax);
		JlNativeApi.StoreD(proc, 6, latLongStep);
		JlNativeApi.StoreS(proc, 7, stackingOrder);
		JlNativeApi.StoreS(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Combine multiple images into a mosaic image.
	/// </summary>
	/// <param name="homMatrices2D">Array of 3x3 projective transformation matrices.</param>
	/// <param name="stackingOrder">Stacking order of the images in the mosaic. Default: "default"</param>
	/// <param name="transformDomain">Should the domains of the input images also be transformed? Default: "false"</param>
	/// <param name="transMat2D">3x3 projective transformation matrix that describes the translation that was necessary to transform all images completely into the output image.</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Combine multiple 图像 为 mosaic 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homMatrices2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenBundleAdjustedMosaic(homMatrices2D, "default", "false", out JlHomMat2D transMat2D);
	///   </code>
	/// </remarks>
	public JlImage GenBundleAdjustedMosaic(JlHomMat2D[] homMatrices2D, JlTuple stackingOrder, string transformDomain, out JlHomMat2D transMat2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1538);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, stackingOrder);
		JlNativeApi.StoreS(proc, 2, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(stackingOrder);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlHomMat2D.LoadNew(proc, 0, err, out transMat2D);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Combine multiple images into a mosaic image.
	/// </summary>
	/// <param name="homMatrices2D">Array of 3x3 projective transformation matrices.</param>
	/// <param name="stackingOrder">Stacking order of the images in the mosaic. Default: "default"</param>
	/// <param name="transformDomain">Should the domains of the input images also be transformed? Default: "false"</param>
	/// <param name="transMat2D">3x3 projective transformation matrix that describes the translation that was necessary to transform all images completely into the output image.</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Combine multiple 图像 为 mosaic 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homMatrices2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenBundleAdjustedMosaic(homMatrices2D, "default", "false", out JlHomMat2D transMat2D);
	///   </code>
	/// </remarks>
	public JlImage GenBundleAdjustedMosaic(JlHomMat2D[] homMatrices2D, string stackingOrder, string transformDomain, out JlHomMat2D transMat2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1538);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.StoreS(proc, 1, stackingOrder);
		JlNativeApi.StoreS(proc, 2, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlHomMat2D.LoadNew(proc, 0, err, out transMat2D);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Combine multiple images into a mosaic image.
	/// </summary>
	/// <param name="startImage">Index of the central input image.</param>
	/// <param name="mappingSource">Indices of the source images of the transformations.</param>
	/// <param name="mappingDest">Indices of the target images of the transformations.</param>
	/// <param name="homMatrices2D">Array of 3x3 projective transformation matrices.</param>
	/// <param name="stackingOrder">Stacking order of the images in the mosaic. Default: "default"</param>
	/// <param name="transformDomain">Should the domains of the input images also be transformed? Default: "false"</param>
	/// <param name="mosaicMatrices2D">Array of 3x3 projective transformation matrices that determine the position of the images in the mosaic.</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Combine multiple 图像 为 mosaic 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple mappingSource = ...;
	///   JlTuple mappingDest = ...;
	///   JlHomMat2D homMatrices2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenProjectiveMosaic(0, mappingSource, mappingDest, homMatrices2D, "default", "false", out JlHomMat2D mosaicMatrices2D);
	///   </code>
	/// </remarks>
	public JlImage GenProjectiveMosaic(int startImage, JlTuple mappingSource, JlTuple mappingDest, JlHomMat2D[] homMatrices2D, JlTuple stackingOrder, string transformDomain, out JlHomMat2D[] mosaicMatrices2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1539);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, startImage);
		JlNativeApi.Store(proc, 1, mappingSource);
		JlNativeApi.Store(proc, 2, mappingDest);
		JlNativeApi.Store(proc, 3, hTuple);
		JlNativeApi.Store(proc, 4, stackingOrder);
		JlNativeApi.StoreS(proc, 5, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mappingSource);
		JlNativeApi.UnpinTuple(mappingDest);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(stackingOrder);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		mosaicMatrices2D = JlHomMat2D.SplitArray(tuple);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Combine multiple images into a mosaic image.
	/// </summary>
	/// <param name="startImage">Index of the central input image.</param>
	/// <param name="mappingSource">Indices of the source images of the transformations.</param>
	/// <param name="mappingDest">Indices of the target images of the transformations.</param>
	/// <param name="homMatrices2D">Array of 3x3 projective transformation matrices.</param>
	/// <param name="stackingOrder">Stacking order of the images in the mosaic. Default: "default"</param>
	/// <param name="transformDomain">Should the domains of the input images also be transformed? Default: "false"</param>
	/// <param name="mosaicMatrices2D">Array of 3x3 projective transformation matrices that determine the position of the images in the mosaic.</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Combine multiple 图像 为 mosaic 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple mappingSource = ...;
	///   JlTuple mappingDest = ...;
	///   JlHomMat2D homMatrices2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.GenProjectiveMosaic(0, mappingSource, mappingDest, homMatrices2D, "default", "false", out JlHomMat2D mosaicMatrices2D);
	///   </code>
	/// </remarks>
	public JlImage GenProjectiveMosaic(int startImage, JlTuple mappingSource, JlTuple mappingDest, JlHomMat2D[] homMatrices2D, string stackingOrder, string transformDomain, out JlHomMat2D[] mosaicMatrices2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1539);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, startImage);
		JlNativeApi.Store(proc, 1, mappingSource);
		JlNativeApi.Store(proc, 2, mappingDest);
		JlNativeApi.Store(proc, 3, hTuple);
		JlNativeApi.StoreS(proc, 4, stackingOrder);
		JlNativeApi.StoreS(proc, 5, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mappingSource);
		JlNativeApi.UnpinTuple(mappingDest);
		JlNativeApi.UnpinTuple(hTuple);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		mosaicMatrices2D = JlHomMat2D.SplitArray(tuple);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply a projective transformation to an image and specify the output image size.
	/// </summary>
	/// <param name="homMat2D">Homogeneous projective transformation matrix.</param>
	/// <param name="interpolation">Interpolation method for the transformation. Default: "bilinear"</param>
	/// <param name="width">Output image width.</param>
	/// <param name="height">Output image height.</param>
	/// <param name="transformDomain">Should the domain of the input image also be transformed? Default: "false"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Apply 投影变换 图像 和 specify 输出图像 size。</para>
	///   <para><b>典型场景</b></para>
	///   <para>坐标变换、位姿对齐与几何校正</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homMat2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.ProjectiveTransImageSize(homMat2D, "bilinear", 0, 0, "false");
	///   </code>
	/// </remarks>
	public JlImage ProjectiveTransImageSize(JlHomMat2D homMat2D, string interpolation, int width, int height, string transformDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1540);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.StoreS(proc, 4, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply a projective transformation to an image.
	/// </summary>
	/// <param name="homMat2D">Homogeneous projective transformation matrix.</param>
	/// <param name="interpolation">Interpolation method for the transformation. Default: "bilinear"</param>
	/// <param name="adaptImageSize">Adapt the size of the output image automatically? Default: "false"</param>
	/// <param name="transformDomain">Should the domain of the input image also be transformed? Default: "false"</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Apply 投影变换 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>坐标变换、位姿对齐与几何校正</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homMat2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.ProjectiveTransImage(homMat2D, "bilinear", "false", "false");
	///   </code>
	/// </remarks>
	public JlImage ProjectiveTransImage(JlHomMat2D homMat2D, string interpolation, string adaptImageSize, string transformDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1541);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreS(proc, 2, adaptImageSize);
		JlNativeApi.StoreS(proc, 3, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply an arbitrary affine 2D transformation to an image and specify the output image size.
	/// </summary>
	/// <param name="homMat2D">Input transformation matrix.</param>
	/// <param name="interpolation">Type of interpolation. Default: "constant"</param>
	/// <param name="width">Width of the output image. Default: 640</param>
	/// <param name="height">Height of the output image. Default: 480</param>
	/// <returns>Transformed image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>仿射变换图像Size。</para>
	///   <para><b>典型场景</b></para>
	///   <para>坐标变换、位姿对齐与几何校正</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homMat2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.AffineTransImageSize(homMat2D, "constant", 640, 480);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>AffineTransRegion、HomMat2dIdentity</para>
	/// </remarks>
	public JlImage AffineTransImageSize(JlHomMat2D homMat2D, string interpolation, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1542);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply an arbitrary affine 2D transformation to images.
	/// </summary>
	/// <param name="homMat2D">Input transformation matrix.</param>
	/// <param name="interpolation">Type of interpolation. Default: "constant"</param>
	/// <param name="adaptImageSize">Adaption of size of result image. Default: "false"</param>
	/// <returns>Transformed image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Apply arbitrary 仿射变换 2D transformation 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>坐标变换、位姿对齐与几何校正</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homMat2D = ...;
	///   JlImage obj = ...;
	///   var result = obj.AffineTransImage(homMat2D, "constant", "false");
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>AffineTransRegion、HomMat2dIdentity</para>
	/// </remarks>
	public JlImage AffineTransImage(JlHomMat2D homMat2D, string interpolation, string adaptImageSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1543);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreS(proc, 2, adaptImageSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Zoom an image by a given factor.
	/// </summary>
	/// <param name="scaleWidth">Scale factor for the width of the image. Default: 0.5</param>
	/// <param name="scaleHeight">Scale factor for the height of the image. Default: 0.5</param>
	/// <param name="interpolation">Type of interpolation. Default: "constant"</param>
	/// <returns>Scaled image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>缩放 图像 通过 给定 factor。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ZoomImageFactor(0.5, 0.5, "constant");
	///   </code>
	/// </remarks>
	public JlImage ZoomImageFactor(double scaleWidth, double scaleHeight, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1544);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, scaleWidth);
		JlNativeApi.StoreD(proc, 1, scaleHeight);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Zoom an image to a given size.
	/// </summary>
	/// <param name="width">Width of the resulting image. Default: 512</param>
	/// <param name="height">Height of the resulting image. Default: 512</param>
	/// <param name="interpolation">Type of interpolation. Default: "constant"</param>
	/// <returns>Scaled image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>缩放 图像 给定 size。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ZoomImageSize(512, 512, "constant");
	///   </code>
	/// </remarks>
	public JlImage ZoomImageSize(int width, int height, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1545);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Mirror an image.
	/// </summary>
	/// <param name="mode">Axis of reflection. Default: "row"</param>
	/// <returns>Reflected image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>镜像 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.MirrorImage("row");
	///   </code>
	/// </remarks>
	public JlImage MirrorImage(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1546);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Rotate an image about its center.
	/// </summary>
	/// <param name="phi">Rotation angle. Default: 90</param>
	/// <param name="interpolation">Type of interpolation. Default: "constant"</param>
	/// <returns>Rotated image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>旋转 图像 about its center。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.RotateImage(90, "constant");
	///   </code>
	/// </remarks>
	public JlImage RotateImage(JlTuple phi, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1547);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, phi);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(phi);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Rotate an image about its center.
	/// </summary>
	/// <param name="phi">Rotation angle. Default: 90</param>
	/// <param name="interpolation">Type of interpolation. Default: "constant"</param>
	/// <returns>Rotated image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>旋转 图像 about its center。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.RotateImage(90, "constant");
	///   </code>
	/// </remarks>
	public JlImage RotateImage(double phi, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1547);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, phi);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}






	/// <summary>
	///   Approximate an affine map from a displacement vector field.
	/// </summary>
	/// <returns>Output transformation matrix.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>向量场To齐次Mat2d。</para>
	///   <para><b>典型场景</b></para>
	///   <para>坐标变换、位姿对齐与几何校正</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.VectorFieldToHomMat2d();
	///   </code>
	/// </remarks>
	public JlHomMat2D VectorFieldToHomMat2d()
	{
		IntPtr proc = JlNativeApi.PreCall(1551);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlHomMat2D.LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Deserialize a serialized image object.
	/// </summary>
	/// <param name="serializedItemHandle">Handle of the serialized item.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>反序列化 serialized 图像 对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.DeserializeImage(serializedItemHandle);
	///   </code>
	/// </remarks>
	public void DeserializeImage(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1570);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   Serialize an image object.
	/// </summary>
	/// <returns>Handle of the serialized item.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>序列化 图像 对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SerializeImage();
	///   </code>
	/// </remarks>
	public byte[] SerializeImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1571);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   Write images in graphic formats.
	/// </summary>
	/// <param name="format">Graphic format. Default: "tiff"</param>
	/// <param name="fillColor">Fill gray value for pixels not belonging to the image domain (region). Default: 0</param>
	/// <param name="fileName">Name of image file.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>写入 图像 在 graphic formats。</para>
	///   <para><b>典型场景</b></para>
	///   <para>将图像、区域、模型或数据保存到文件</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.WriteImage("tiff", 0, "image.png");
	///   </code>
	/// </remarks>
	public void WriteImage(string format, JlTuple fillColor, JlTuple fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1575);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.Store(proc, 1, fillColor);
		JlNativeApi.Store(proc, 2, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fillColor);
		JlNativeApi.UnpinTuple(fileName);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Write images in graphic formats.
	/// </summary>
	/// <param name="format">Graphic format. Default: "tiff"</param>
	/// <param name="fillColor">Fill gray value for pixels not belonging to the image domain (region). Default: 0</param>
	/// <param name="fileName">Name of image file.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>写入 图像 在 graphic formats。</para>
	///   <para><b>典型场景</b></para>
	///   <para>将图像、区域、模型或数据保存到文件</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.WriteImage("tiff", 0, "image.png");
	///   </code>
	/// </remarks>
	public void WriteImage(string format, int fillColor, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1575);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.StoreI(proc, 1, fillColor);
		JlNativeApi.StoreS(proc, 2, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read images.
	/// </summary>
	/// <param name="headerSize">Number of bytes for file header. Default: 0</param>
	/// <param name="sourceWidth">Number of image columns of the filed image. Default: 512</param>
	/// <param name="sourceHeight">Number of image lines of the filed image. Default: 512</param>
	/// <param name="startRow">Starting point of image area (line). Default: 0</param>
	/// <param name="startColumn">Starting point of image area (column). Default: 0</param>
	/// <param name="destWidth">Number of image columns of output image. Default: 512</param>
	/// <param name="destHeight">Number of image lines of output image. Default: 512</param>
	/// <param name="pixelType">Type of pixel values. Default: "byte"</param>
	/// <param name="bitOrder">Sequence of bits within one byte. Default: "MSBFirst"</param>
	/// <param name="byteOrder">Sequence of bytes within one 'short' unit. Default: "MSBFirst"</param>
	/// <param name="pad">Data units within one image line (alignment). Default: "byte"</param>
	/// <param name="index">Number of images in the file. Default: 1</param>
	/// <param name="fileName">Name of input file.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>从文件加载图像、区域、模型或数据</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.ReadSequence(0, 512, 512, 0, 0, 512, 512, "byte", "MSBFirst", "MSBFirst", "byte", 1, "data.dat");
	///   </code>
	/// </remarks>
	public void ReadSequence(int headerSize, int sourceWidth, int sourceHeight, int startRow, int startColumn, int destWidth, int destHeight, string pixelType, string bitOrder, string byteOrder, string pad, int index, string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1576);
		JlNativeApi.StoreI(proc, 0, headerSize);
		JlNativeApi.StoreI(proc, 1, sourceWidth);
		JlNativeApi.StoreI(proc, 2, sourceHeight);
		JlNativeApi.StoreI(proc, 3, startRow);
		JlNativeApi.StoreI(proc, 4, startColumn);
		JlNativeApi.StoreI(proc, 5, destWidth);
		JlNativeApi.StoreI(proc, 6, destHeight);
		JlNativeApi.StoreS(proc, 7, pixelType);
		JlNativeApi.StoreS(proc, 8, bitOrder);
		JlNativeApi.StoreS(proc, 9, byteOrder);
		JlNativeApi.StoreS(proc, 10, pad);
		JlNativeApi.StoreI(proc, 11, index);
		JlNativeApi.StoreS(proc, 12, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read an image with different file formats.
	/// </summary>
	/// <param name="fileName">Name of the image to be read. Default: "printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取多种文件格式的图像</para>
	///   <para><b>典型场景</b></para>
	///   <para>从文件加载图像、区域、模型或数据</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.ReadImage("printer_chip/printer_chip_01");
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>Rgb1ToGray、Threshold、CropDomain</para>
	/// </remarks>
	public void ReadImage(JlTuple fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.Store(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fileName);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Read an image with different file formats.
	/// </summary>
	/// <param name="fileName">Name of the image to be read. Default: "printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取多种文件格式的图像</para>
	///   <para><b>典型场景</b></para>
	///   <para>从文件加载图像、区域、模型或数据</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.ReadImage("printer_chip/printer_chip_01");
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>Rgb1ToGray、Threshold、CropDomain</para>
	/// </remarks>
	public void ReadImage(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Return gray values of an image at the positions of an XLD contour.
	/// </summary>
	/// <param name="contour">Input XLD contour with the coordinates of the positions.</param>
	/// <param name="interpolation">Interpolation method. Default: "nearest_neighbor"</param>
	/// <returns>Gray values of the selected image coordinates.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回 灰度值s 图像 at positions XLD 轮廓。</para>
	///   <para><b>典型场景</b></para>
	///   <para>轮廓（XLD）生成、合并与几何拟合</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlXLDCont contour = ...;
	///   JlImage obj = ...;
	///   var result = obj.GetGrayvalContourXld(contour, "nearest_neighbor");
	///   </code>
	/// </remarks>
	public JlTuple GetGrayvalContourXld(JlXLDCont contour, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1587);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour);
		JlNativeApi.StoreS(proc, 0, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
		return tuple;
	}





	/// <summary>
	///   Create a curved gray surface with second order polynomial.
	/// </summary>
	/// <param name="type">Pixel type. Default: "byte"</param>
	/// <param name="alpha">Second order coefficient in vertical direction. Default: 1.0</param>
	/// <param name="beta">Second order coefficient in horizontal direction. Default: 1.0</param>
	/// <param name="gamma">Mixed second order coefficient. Default: 1.0</param>
	/// <param name="delta">First order coefficient in vertical direction. Default: 1.0</param>
	/// <param name="epsilon">First order coefficient in horizontal direction. Default: 1.0</param>
	/// <param name="zeta">Zero order coefficient. Default: 1.0</param>
	/// <param name="row">Row coordinate of the reference point of the surface. Default: 256.0</param>
	/// <param name="column">Column coordinate of the reference point of the surface. Default: 256.0</param>
	/// <param name="width">Width of image. Default: 512</param>
	/// <param name="height">Height of image. Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>生成图像SurfaceSecondOrder。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenImageSurfaceSecondOrder("byte", 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 256.0, 256.0, 512, 512);
	///   </code>
	/// </remarks>
	public void GenImageSurfaceSecondOrder(string type, double alpha, double beta, double gamma, double delta, double epsilon, double zeta, double row, double column, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1664);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreD(proc, 2, beta);
		JlNativeApi.StoreD(proc, 3, gamma);
		JlNativeApi.StoreD(proc, 4, delta);
		JlNativeApi.StoreD(proc, 5, epsilon);
		JlNativeApi.StoreD(proc, 6, zeta);
		JlNativeApi.StoreD(proc, 7, row);
		JlNativeApi.StoreD(proc, 8, column);
		JlNativeApi.StoreI(proc, 9, width);
		JlNativeApi.StoreI(proc, 10, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create a tilted gray surface with first order polynomial.
	/// </summary>
	/// <param name="type">Pixel type. Default: "byte"</param>
	/// <param name="alpha">First order coefficient in vertical direction. Default: 1.0</param>
	/// <param name="beta">First order coefficient in horizontal direction. Default: 1.0</param>
	/// <param name="gamma">Zero order coefficient. Default: 1.0</param>
	/// <param name="row">Row coordinate of the reference point of the surface. Default: 256.0</param>
	/// <param name="column">Column coordinate of the reference point of the surface. Default: 256.0</param>
	/// <param name="width">Width of image. Default: 512</param>
	/// <param name="height">Height of image. Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>生成图像SurfaceFirstOrder。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   obj.GenImageSurfaceFirstOrder("byte", 1.0, 1.0, 1.0, 256.0, 256.0, 512, 512);
	///   </code>
	/// </remarks>
	public void GenImageSurfaceFirstOrder(string type, double alpha, double beta, double gamma, double row, double column, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1665);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreD(proc, 2, beta);
		JlNativeApi.StoreD(proc, 3, gamma);
		JlNativeApi.StoreD(proc, 4, row);
		JlNativeApi.StoreD(proc, 5, column);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>区域内最小/最大灰度，percent 可截掉离群像素，原生算子 id 1670，三个输出均为元组。</summary>
	/// <param name="regions">要计算特征的区域，逐区域各出一个值。</param>
	/// <param name="percent">相对绝对最大值/最小值截去的百分比。Default: 0</param>
	/// <param name="min">"最小"灰度，每区域一个元素。</param>
	/// <param name="max">"最大"灰度。</param>
	/// <param name="range">max 与 min 之差。</param>
	/// <remarks>
	///   <para><b>功能说明</b>图像是 iconc 2 输入、区域 iconc 1，<paramref name="percent"/> 占控制槽 0；
	///   三个 iconc 输出（0/1/2）都是 <see cref="JlTupleType"/> 为 DOUBLE 的 <see cref="JlTuple"/>——结果是元组不是图像。
	///   <paramref name="percent"/>=0 时是严格的逐区域 min/max；取正数则变成"伪极值"：<c>max</c> 返回比绝对最大值低
	///   <paramref name="percent"/>% 的像素数量级处、<c>min</c> 对称上抬——用来防少数灰尘亮斑或坏点把极值绑架掉。</para>
	///   <para><b>与相邻算子的取舍</b>要均值与标准差 → <see cref="Intensity(JlRegion,out JlTuple)"/>；要完整分布 →
	///   <see cref="GrayHisto(JlRegion,out JlTuple)"/>；要逐像素改写灰度 → 形态学/滤波族。本算子的 <c>range</c>
	///   常见用法是配合 <see cref="ScaleImage(double,double)"/> 做逐区域对比度拉伸——直接拿严格 min/max 归一有噪场景，
	///   极值被单点噪声决定，拉伸后整体反差反而塌掉，这正是 <paramref name="percent"/> 存在的理由。</para>
	///   <para><b>参数取向</b>元组版 <paramref name="percent"/> 走 <c>Store</c>+<c>UnpinTuple</c>，且三个输出按区域逐个给值，
	///   元素个数与 <c>regions.CountObj()</c> 对应；单区域简写见 <see cref="MinMaxGray(JlRegion,double,out double,out double,out double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion thr = img.Threshold(50.0, 255.0);
	///   using JlRegion parts = thr.Connection();
	///   img.MinMaxGray(parts, new JlTuple(1.0), out JlTuple mins, out JlTuple maxs, out JlTuple ranges);
	///   double firstMax = maxs[0].D;   // 截掉顶部 1% 像素后的伪最大值
	///   mins.Dispose(); maxs.Dispose(); ranges.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三个输出元组与输入区域都要释放；空区域对应的 min/max 取值 [待实测]；
	///   <paramref name="percent"/> 超过 100 或为负 [待实测]。</para>
	/// </remarks>
	public void MinMaxGray(JlRegion regions, JlTuple percent, out JlTuple min, out JlTuple max, out JlTuple range)
	{
		IntPtr proc = JlNativeApi.PreCall(1670);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 0, percent);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percent);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out min);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out max);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out range);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>区域内最小/最大灰度（三个输出都是单值 double）。</summary>
	/// <param name="regions">要计算特征的区域。</param>
	/// <param name="percent">相对绝对最大值/最小值截去的百分比。Default: 0</param>
	/// <param name="min">"最小"灰度。</param>
	/// <param name="max">"最大"灰度。</param>
	/// <param name="range">max 与 min 之差。</param>
	/// <remarks>
	///   <para>percent 截尾语义、与 <c>Intensity</c>/<c>ScaleImage</c> 的配合见
	///   <see cref="MinMaxGray(JlRegion,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>：同一原生 id 1670，
	///   本版本 <c>StoreD</c> 直写 percent，三个 iconc 输出用 <c>LoadD</c> 按标量读取——只对单区域有意义，
	///   多区域时取第几个值 [待实测]，逐区域结果请用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(100.0, 100.0, 300.0, 300.0);
	///   img.MinMaxGray(roi, 1.0, out double min, out double max, out double range);
	///   </code>
	/// </remarks>
	public void MinMaxGray(JlRegion regions, double percent, out double min, out double max, out double range)
	{
		IntPtr proc = JlNativeApi.PreCall(1670);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreD(proc, 0, percent);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out min);
		err = JlNativeApi.LoadD(proc, 1, err, out max);
		err = JlNativeApi.LoadD(proc, 2, err, out range);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>
	///   Calculate the mean and deviation of gray values.
	/// </summary>
	/// <param name="regions">Regions in which the features are calculated.</param>
	/// <param name="deviation">Deviation of gray values within a region.</param>
	/// <returns>Mean gray value of a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 mean 和 deviation 灰度值s。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.Intensity(regions, out JlTuple deviation);
	///   </code>
	/// </remarks>
	public JlTuple Intensity(JlRegion regions, out JlTuple deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(1671);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   Calculate the mean and deviation of gray values.
	/// </summary>
	/// <param name="regions">Regions in which the features are calculated.</param>
	/// <param name="deviation">Deviation of gray values within a region.</param>
	/// <returns>Mean gray value of a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 mean 和 deviation 灰度值s。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.Intensity(regions, out double deviation);
	///   </code>
	/// </remarks>
	public double Intensity(JlRegion regions, out double deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(1671);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}

	/// <summary>指定灰度区间与 bin 数的灰度直方图（单通道图像），原生算子 id 1672，区间以元组传入。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="min">直方图下界。Default: 0</param>
	/// <param name="max">直方图上界。Default: 255</param>
	/// <param name="numBins">bin 个数。Default: 256</param>
	/// <param name="binSize">实际 bin 宽。</param>
	/// <returns>各 bin 的像素计数（INTEGER 元组），长度等于 numBins。</returns>
	/// <remarks>
	///   <para><b>功能说明与前提</b>英文签名文档明确适用对象是<b>单通道图像</b>；多通道输入的检查与行为本层看不到
	///   [待实测]。区域 iconc 1、图像 iconc 2，控制槽 0/1/2 是 <c>min</c>/<c>max</c>/<paramref name="numBins"/>；
	///   直方图从 iconc 输出 0 读出，实际 bin 宽由原生算好经 <paramref name="binSize"/>（iconc 1）返回，
	///   第 i 个 bin 覆盖 [min + i·binSize, min + (i+1)·binSize) 一类的区间，首尾闭开细节 [待实测]。
	///   灰度落在 <c>min..max</c> 之外的像素不进入任何 bin：byte 图传 0..255 才不漏，
	///   uint2/float 图先量实际范围（<see cref="MinMaxGray(JlRegion,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>）再定区间。</para>
	///   <para><b>与相邻算子的取舍</b>不想手动选区间、要按全灰度域分组计数 → <see cref="GrayHistoAbs(JlRegion,JlTuple)"/>；
	///   要相对频率以便比较不同面积的区域 → <see cref="GrayHisto(JlRegion,out JlTuple)"/>；
	///   <see cref="Intensity(JlRegion,out JlTuple)"/> 只给均值/标准差，双峰分布会被完全抹掉，选阈值必须靠直方图。</para>
	///   <para><b>参数取向</b>本重载 <paramref name="min"/>/<paramref name="max"/> 是 <c>Store</c>+<c>UnpinTuple</c>，
	///   多元素语义 [待实测]；<paramref name="numBins"/> 两个重载都是 <c>StoreI</c> <c>int</c>，单位是 bin 个数。
	///   min 大于 max、numBins 为 0 或负 [待实测]。返回值是<b>元组</b>而不是图像。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   JlTuple histo = img.GrayHistoRange(roi, new JlTuple(0.0), new JlTuple(255.0), 256, out double binSize);
	///   int bin0 = histo[0].I;       // 直方图只统计区域与定义域交集内的像素
	///   histo.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组需 <c>Dispose</c>；uint2 图上 binSize 通常大于 1，按"灰度值=bin 下标"画图会错位，
	///   横轴要乘回 <paramref name="binSize"/>。</para>
	/// </remarks>
	public JlTuple GrayHistoRange(JlRegion region, JlTuple min, JlTuple max, int numBins, out double binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1672);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.Store(proc, 0, min);
		JlNativeApi.Store(proc, 1, max);
		JlNativeApi.StoreI(proc, 2, numBins);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlNativeApi.LoadD(proc, 1, err, out binSize);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>指定区间的灰度直方图（区间以 double 传入，返回值退化为主 bin 计数）。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="min">直方图下界。Default: 0</param>
	/// <param name="max">直方图上界。Default: 255</param>
	/// <param name="numBins">bin 个数。Default: 256</param>
	/// <param name="binSize">实际 bin 宽。</param>
	/// <returns>iconc 输出 0 按标量读出的单个整数。</returns>
	/// <remarks>
	///   <para>区间/分箱语义与单通道前提见 <see cref="GrayHistoRange(JlRegion,JlTuple,JlTuple,int,out double)"/>：
	///   同一原生 id 1672，本版本 <c>StoreD</c> 直写 min/max。<b>要注意的坑</b>：直方图本来是逐 bin 的向量，
	///   这里却用 <c>LoadI</c> 按单个 <c>int</c> 读——多 bin 时只拿得到一个值（第几个 [待实测]），完整直方图请一律用元组版。
	///   此重载实际只在 numBins=1（把区间当"灰度计数窗"用）时有意义 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   int count = img.GrayHistoRange(roi, 128.0, 255.0, 1, out double binSize);
	///   </code>
	/// </remarks>
	public int GrayHistoRange(JlRegion region, double min, double max, int numBins, out double binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1672);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreD(proc, 0, min);
		JlNativeApi.StoreD(proc, 1, max);
		JlNativeApi.StoreI(proc, 2, numBins);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out binSize);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return intValue;
	}

	/// <summary>
	///   Calculate the histogram of two-channel gray value images.
	/// </summary>
	/// <param name="regions">Region in which the histogram is to be calculated.</param>
	/// <param name="imageRow">Channel 2.</param>
	/// <returns>Histogram to be calculated.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 histogram two-channel 灰度值 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage imageRow = ...;
	///   JlImage obj = ...;
	///   var result = obj.Histo2dim(regions, imageRow);
	///   </code>
	/// </remarks>
	public JlImage Histo2dim(JlRegion regions, JlImage imageRow)
	{
		IntPtr proc = JlNativeApi.PreCall(1673);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, imageRow);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(imageRow);
		return obj;
	}

	/// <summary>按灰度步长分组的绝对计数直方图，原生算子 id 1674，步长以元组传入。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="quantization">灰度分组步长。Default: 1.0</param>
	/// <returns>各灰度组的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="GrayHistoRange(JlRegion,JlTuple,JlTuple,int,out double)"/> 的分工：Range 版要你指定
	///   区间和 bin 数（<c>binSize</c> 是算出来的），本算子反过来——只给<b>步长</b> <paramref name="quantization"/>（单位是灰度值，
	///   不是像素、不是比例），统计范围由图像自身灰度域决定 [待实测]。默认 1.0 即逐灰度值计数。</para>
	///   <para><b>坑</b>计数是<b>绝对值</b>，随区域面积线性增长：两个大小不同的区域直接对比原始计数毫无意义，
	///   归一化用 <see cref="GrayHisto(JlRegion,out JlTuple)"/> 的 relativeHisto，或自行除以区域面积。
	///   直方图起点随区域实际出现的灰度而定的细节（空灰度段是否补零 bin）[待实测]。</para>
	///   <para><b>参数取向</b>元组版 <paramref name="quantization"/> 走 <c>Store</c>+<c>UnpinTuple</c>，
	///   与标量版 <see cref="GrayHistoAbs(JlRegion,double)"/> <b>返回类型相同</b>（都是完整 INTEGER 元组），
	///   差异只在传参方式；步长 ≤0 行为 [待实测]。单通道前提同 Range 版 [待实测：多通道]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   JlTuple histo = img.GrayHistoAbs(roi, new JlTuple(2.0));   // 每 2 个灰度一档
	///   int n = histo.Length;
	///   histo.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组需 <c>Dispose</c>；bin 下标不再等于灰度值，换算要乘 <paramref name="quantization"/>。</para>
	/// </remarks>
	public JlTuple GrayHistoAbs(JlRegion region, JlTuple quantization)
	{
		IntPtr proc = JlNativeApi.PreCall(1674);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.Store(proc, 0, quantization);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(quantization);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>按灰度步长分组的绝对计数直方图（步长以单个 double 传入）。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="quantization">灰度分组步长。Default: 1.0</param>
	/// <returns>各灰度组的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para>步长语义、绝对计数随面积增长的坑见 <see cref="GrayHistoAbs(JlRegion,JlTuple)"/>：同一原生 id 1674，
	///   本版本 <c>StoreD</c> 直写步长，无固定/解固定，返回值同样是完整直方图元组，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   JlTuple histo = img.GrayHistoAbs(roi, 1.0);
	///   histo.Dispose();
	///   </code>
	/// </remarks>
	public JlTuple GrayHistoAbs(JlRegion region, double quantization)
	{
		IntPtr proc = JlNativeApi.PreCall(1674);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreD(proc, 0, quantization);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>一次给出绝对与相对两张灰度直方图，原生算子 id 1675，无分箱参数。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="relativeHisto">按区域面积归一化的相对频率。</param>
	/// <returns>各灰度值的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本族唯一没有分箱/区间参数的直方图：一个调用同时从 iconc 输出 0、1 拿到绝对计数
	///   （返回值）与<b>按区域面积归一化</b>的相对频率（<paramref name="relativeHisto"/>，DOUBLE 元组）。
	///   相对版解决的正是绝对计数的坑——不同大小的区域直方图不可直接比：绝对计数随面积线性膨胀，
	///   相对频率才可比（逐 bin 求和约等于 1，归一化基数是区域面积还是有效像素数 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>要自定义区间与 bin 数 → <see cref="GrayHistoRange(JlRegion,JlTuple,JlTuple,int,out double)"/>；
	///   只要绝对计数、按灰度步长分组 → <see cref="GrayHistoAbs(JlRegion,double)"/>。本算子无分箱旋钮，bin 的灰度跨度
	///   由原生决定（byte 图大概率逐灰度 [待实测]），要精确控制分箱别用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion a = new JlRegion(10.0, 10.0, 60.0, 60.0);
	///   using JlRegion b = new JlRegion(100.0, 100.0, 200.0, 200.0);
	///   JlTuple absA = img.GrayHisto(a, out JlTuple relA);
	///   JlTuple absB = img.GrayHisto(b, out JlTuple relB);   // 比较 relA 与 relB 才不受两块面积差异干扰
	///   </code>
	///   <para><b>资源与坑</b>注意 this 才是图像、区域是第一个实参；四个输出元组（absA/relA/absB/relB 这类）都要
	///   <c>Dispose</c>。多通道输入 [待实测]。</para>
	/// </remarks>
	public JlTuple GrayHisto(JlRegion region, out JlTuple relativeHisto)
	{
		IntPtr proc = JlNativeApi.PreCall(1675);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out relativeHisto);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>逐区域计算灰度分布的信息熵与各向异性，原生算子 id 1676，两个输出均为元组。</summary>
	/// <param name="regions">要计算特征的区域，逐区域各出一个值。</param>
	/// <param name="anisotropy">灰度分布对称性（各向异性）度量。</param>
	/// <returns>灰度信息熵，每区域一个元素。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1，无控制参数；从 iconc 输出 0/1 读两个 DOUBLE 元组：
	///   熵是区域内灰度直方图的信息量——平坦区接近 0，灰度分散的纹理区显著更高；<paramref name="anisotropy"/>
	///   按英文文档是"灰度分布对称性的度量"，其确切公式与取值方向 [待实测]。输出是<b>元组</b>不是图像。</para>
	///   <para><b>与相邻算子的取舍</b>要逐区域一个标量做纹理分类/特征向量 → 本算子；要在图上按空间位置分纹理区/非纹理区 →
	///   <see cref="EntropyImage(int,int)"/>（它返回局部窗熵图像，窗尺寸要求见其文档）。灰度区分不够时别指望熵：
	///   两块均值相同、方差不同的区域熵值差距可能远小于直觉预期。</para>
	///   <para><b>统计坑</b>熵由直方图估计，bin 数与像素数同量级时估计偏置明显：几百像素的小区域，其熵上限被
	///   log2(像素数) 卡住，且不同面积的区域间直接比熵值不公平 [待实测：分箱/归一方式]。这是用熵做区域筛选时
	///   最主要的误判来源。</para>
	///   <para><b>参数取向</b>元组版逐区域出值，元素与 <c>regions.CountObj()</c> 对应；单区域标量版见
	///   <see cref="EntropyGray(JlRegion,out double)"/>。空区域、多通道输入行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion thr = img.Threshold(30.0, 255.0);
	///   using JlRegion parts = thr.Connection();
	///   JlTuple ent = img.EntropyGray(parts, out JlTuple aniso);
	///   double first = ent[0].D;
	///   ent.Dispose(); aniso.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个输出元组与输入区域都要释放；熵对预处理极敏感——滤波直方图形态的算子
	///   （中值、rank）都会系统性压低它，比较不同批次图像时预处理链必须一致。</para>
	/// </remarks>
	public JlTuple EntropyGray(JlRegion regions, out JlTuple anisotropy)
	{
		IntPtr proc = JlNativeApi.PreCall(1676);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out anisotropy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>灰度熵与各向异性（两个输出都是单值 double）。</summary>
	/// <param name="regions">要计算特征的区域。</param>
	/// <param name="anisotropy">灰度分布对称性度量。</param>
	/// <returns>灰度信息熵。</returns>
	/// <remarks>
	///   <para>熵的直方图估计偏置、与 <c>EntropyImage</c> 的分工见
	///   <see cref="EntropyGray(JlRegion,out JlTuple)"/>：同一原生 id 1676，本版本把两个 iconc 输出用 <c>LoadD</c>
	///   按标量读出——只对单区域（或只要第一个值 [待实测]）有意义，多区域一律用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(100.0, 100.0, 200.0, 200.0);
	///   double ent = img.EntropyGray(roi, out double aniso);
	///   </code>
	/// </remarks>
	public double EntropyGray(JlRegion regions, out double anisotropy)
	{
		IntPtr proc = JlNativeApi.PreCall(1676);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out anisotropy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}





	/// <summary>
	///   Calculate gray value moments and approximation by a plane.
	/// </summary>
	/// <param name="regions">Regions to be checked.</param>
	/// <param name="MRow">Mixed moments along a line.</param>
	/// <param name="MCol">Mixed moments along a column.</param>
	/// <param name="alpha">Parameter Alpha of the approximating plane.</param>
	/// <param name="beta">Parameter Beta of the approximating plane.</param>
	/// <param name="mean">Mean gray value.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 灰度值 moments 和 approximation 通过 plane。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   obj.MomentsGrayPlane(regions, out JlTuple MRow, out JlTuple MCol, out JlTuple alpha, out JlTuple beta, out JlTuple mean);
	///   </code>
	/// </remarks>
	public void MomentsGrayPlane(JlRegion regions, out JlTuple MRow, out JlTuple MCol, out JlTuple alpha, out JlTuple beta, out JlTuple mean)
	{
		IntPtr proc = JlNativeApi.PreCall(1680);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out MRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out MCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out alpha);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out beta);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out mean);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>
	///   Calculate gray value moments and approximation by a plane.
	/// </summary>
	/// <param name="regions">Regions to be checked.</param>
	/// <param name="MRow">Mixed moments along a line.</param>
	/// <param name="MCol">Mixed moments along a column.</param>
	/// <param name="alpha">Parameter Alpha of the approximating plane.</param>
	/// <param name="beta">Parameter Beta of the approximating plane.</param>
	/// <param name="mean">Mean gray value.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 灰度值 moments 和 approximation 通过 plane。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   obj.MomentsGrayPlane(regions, out double MRow, out double MCol, out double alpha, out double beta, out double mean);
	///   </code>
	/// </remarks>
	public void MomentsGrayPlane(JlRegion regions, out double MRow, out double MCol, out double alpha, out double beta, out double mean)
	{
		IntPtr proc = JlNativeApi.PreCall(1680);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out MRow);
		err = JlNativeApi.LoadD(proc, 1, err, out MCol);
		err = JlNativeApi.LoadD(proc, 2, err, out alpha);
		err = JlNativeApi.LoadD(proc, 3, err, out beta);
		err = JlNativeApi.LoadD(proc, 4, err, out mean);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>
	///   Calculate the deviation of the gray values from the approximating image plane.
	/// </summary>
	/// <param name="regions">Regions, of which the plane deviation is to be calculated.</param>
	/// <returns>Deviation of the gray values within a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 deviation 灰度值s 从 approximating 图像 plane。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.PlaneDeviation(regions);
	///   </code>
	/// </remarks>
	public JlTuple PlaneDeviation(JlRegion regions)
	{
		IntPtr proc = JlNativeApi.PreCall(1681);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   Compute the orientation and major axes of a region in a gray value image.
	/// </summary>
	/// <param name="regions">Region(s) to be examined.</param>
	/// <param name="rb">Minor axis of the region.</param>
	/// <param name="phi">Angle enclosed by the major axis and the x-axis.</param>
	/// <returns>Major axis of the region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 orientation 和 major axes 区域 在 灰度值 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.EllipticAxisGray(regions, out JlTuple rb, out JlTuple phi);
	///   </code>
	/// </remarks>
	public JlTuple EllipticAxisGray(JlRegion regions, out JlTuple rb, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1682);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rb);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   Compute the orientation and major axes of a region in a gray value image.
	/// </summary>
	/// <param name="regions">Region(s) to be examined.</param>
	/// <param name="rb">Minor axis of the region.</param>
	/// <param name="phi">Angle enclosed by the major axis and the x-axis.</param>
	/// <returns>Major axis of the region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 orientation 和 major axes 区域 在 灰度值 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.EllipticAxisGray(regions, out double rb, out double phi);
	///   </code>
	/// </remarks>
	public double EllipticAxisGray(JlRegion regions, out double rb, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1682);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out rb);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}

	/// <summary>
	///   Compute the area and center of gravity of a region in a gray value image.
	/// </summary>
	/// <param name="regions">Region(s) to be examined.</param>
	/// <param name="row">Row coordinate of the gray value center of gravity.</param>
	/// <param name="column">Column coordinate of the gray value center of gravity.</param>
	/// <returns>Gray value volume of the region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 area 和 center gravity 区域 在 灰度值 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.AreaCenterGray(regions, out JlTuple row, out JlTuple column);
	///   </code>
	/// </remarks>
	public JlTuple AreaCenterGray(JlRegion regions, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1683);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   Compute the area and center of gravity of a region in a gray value image.
	/// </summary>
	/// <param name="regions">Region(s) to be examined.</param>
	/// <param name="row">Row coordinate of the gray value center of gravity.</param>
	/// <param name="column">Column coordinate of the gray value center of gravity.</param>
	/// <returns>Gray value volume of the region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 area 和 center gravity 区域 在 灰度值 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions = ...;
	///   JlImage obj = ...;
	///   var result = obj.AreaCenterGray(regions, out double row, out double column);
	///   </code>
	/// </remarks>
	public double AreaCenterGray(JlRegion regions, out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1683);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row);
		err = JlNativeApi.LoadD(proc, 2, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}

	/// <summary>
	///   Calculate horizontal and vertical gray-value projections.
	/// </summary>
	/// <param name="region">Region to be processed.</param>
	/// <param name="mode">Method to compute the projections. Default: "simple"</param>
	/// <param name="vertProjection">Vertical projection.</param>
	/// <returns>Horizontal projection.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 horizontal 和 vertical 灰度-值 projections。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion region = ...;
	///   JlImage obj = ...;
	///   var result = obj.GrayProjections(region, "simple", out JlTuple vertProjection);
	///   </code>
	/// </remarks>
	public JlTuple GrayProjections(JlRegion region, string mode, out JlTuple vertProjection)
	{
		IntPtr proc = JlNativeApi.PreCall(1684);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out vertProjection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}



	/// <summary>
	///   Convert image maps into other map types.
	/// </summary>
	/// <param name="newType">Type of MapConverted. Default: "coord_map_sub_pix"</param>
	/// <param name="imageWidth">Width of images to be mapped. Default: "map_width"</param>
	/// <returns>Converted map.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 图像 maps 为 other map types。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ConvertMapType("coord_map_sub_pix", "map_width");
	///   </code>
	/// </remarks>
	public JlImage ConvertMapType(string newType, JlTuple imageWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1796);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, newType);
		JlNativeApi.Store(proc, 1, imageWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(imageWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert image maps into other map types.
	/// </summary>
	/// <param name="newType">Type of MapConverted. Default: "coord_map_sub_pix"</param>
	/// <param name="imageWidth">Width of images to be mapped. Default: "map_width"</param>
	/// <returns>Converted map.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>转换 图像 maps 为 other map types。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.ConvertMapType("coord_map_sub_pix", "map_width");
	///   </code>
	/// </remarks>
	public JlImage ConvertMapType(string newType, int imageWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1796);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, newType);
		JlNativeApi.StoreI(proc, 1, imageWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>Compute a pose out of a homography describing the relation between world and image coordinates.</summary>
	/// <param name="homography">The homography from world- to image coordinates.</param>
	/// <param name="cameraMatrix">The camera calibration matrix K.</param>
	/// <param name="method">Type of pose computation. Default: "decomposition"</param>
	/// <returns>Pose of the 2D object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Proj齐次Mat2dTo位姿。</para>
	///   <para><b>典型场景</b></para>
	///   <para>坐标变换、位姿对齐与几何校正</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHomMat2D homography = ...;
	///   JlHomMat2D cameraMatrix = ...;
	///   var result = JlImage.ProjHomMat2dToPose(homography, cameraMatrix, "decomposition");
	///   </code>
	/// </remarks>
	public static JlPose ProjHomMat2dToPose(JlHomMat2D homography, JlHomMat2D cameraMatrix, string method)
	{
		IntPtr proc = JlNativeApi.PreCall(1798);
		JlNativeApi.Store(proc, 0, homography);
		JlNativeApi.Store(proc, 1, cameraMatrix);
		JlNativeApi.StoreS(proc, 2, method);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homography);
		JlNativeApi.UnpinTuple(cameraMatrix);
		err = JlPose.LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}



	/// <summary>
	///   Apply a general transformation to an image.
	/// </summary>
	/// <param name="map">Image containing the mapping data.</param>
	/// <returns>Mapped image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Apply general transformation 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage map = ...;
	///   JlImage obj = ...;
	///   var result = obj.MapImage(map);
	///   </code>
	/// </remarks>
	public JlImage MapImage(JlImage map)
	{
		IntPtr proc = JlNativeApi.PreCall(1806);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, map);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(map);
		return obj;
	}





























	/// <summary>
	///   Find the best matches of multiple NCC models.
	/// </summary>
	/// <param name="modelIDs">Handle of the models.</param>
	/// <param name="angleStart">Smallest rotation of the models. Default: -0.39</param>
	/// <param name="angleExtent">Extent of the rotation angles. Default: 0.79</param>
	/// <param name="minScore">Minimum score of the instances of the models to be found. Default: 0.8</param>
	/// <param name="numMatches">Number of instances of the models to be found (or 0 for all matches). Default: 1</param>
	/// <param name="maxOverlap">Maximum overlap of the instances of the models to be found. Default: 0.5</param>
	/// <param name="subPixel">Subpixel accuracy if not equal to 'none'. Default: "true"</param>
	/// <param name="numLevels">Number of pyramid levels used in the matching (and lowest pyramid level to use if $|NumLevels| = 2$). Default: 0</param>
	/// <param name="row">Row coordinate of the found instances of the models.</param>
	/// <param name="column">Column coordinate of the found instances of the models.</param>
	/// <param name="angle">Rotation angle of the found instances of the models.</param>
	/// <param name="score">Score of the found instances of the models.</param>
	/// <param name="model">Index of the found instances of the models.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>查找 最佳 匹配结果 multiple NCC 模型s。</para>
	///   <para><b>典型场景</b></para>
	///   <para>模板匹配定位（形状匹配或 NCC）</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlNCCModel modelIDs = ...;
	///   JlImage obj = ...;
	///   obj.FindNccModels(modelIDs, -0.39, 0.79, 0.8, 1, 0.5, "true", 0, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateNccModel、ClearNccModel</para>
	/// </remarks>
	public void FindNccModels(JlNCCModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(1958);
		Store(proc, 1);
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
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>
	///   Find the best matches of multiple NCC models.
	/// </summary>
	/// <param name="modelIDs">Handle of the models.</param>
	/// <param name="angleStart">Smallest rotation of the models. Default: -0.39</param>
	/// <param name="angleExtent">Extent of the rotation angles. Default: 0.79</param>
	/// <param name="minScore">Minimum score of the instances of the models to be found. Default: 0.8</param>
	/// <param name="numMatches">Number of instances of the models to be found (or 0 for all matches). Default: 1</param>
	/// <param name="maxOverlap">Maximum overlap of the instances of the models to be found. Default: 0.5</param>
	/// <param name="subPixel">Subpixel accuracy if not equal to 'none'. Default: "true"</param>
	/// <param name="numLevels">Number of pyramid levels used in the matching (and lowest pyramid level to use if $|NumLevels| = 2$). Default: 0</param>
	/// <param name="row">Row coordinate of the found instances of the models.</param>
	/// <param name="column">Column coordinate of the found instances of the models.</param>
	/// <param name="angle">Rotation angle of the found instances of the models.</param>
	/// <param name="score">Score of the found instances of the models.</param>
	/// <param name="model">Index of the found instances of the models.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>查找 最佳 匹配结果 multiple NCC 模型s。</para>
	///   <para><b>典型场景</b></para>
	///   <para>模板匹配定位（形状匹配或 NCC）</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlNCCModel modelIDs = ...;
	///   JlImage obj = ...;
	///   obj.FindNccModels(modelIDs, -0.39, 0.79, 0.8, 1, 0.5, "true", 0, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateNccModel、ClearNccModel</para>
	/// </remarks>
	public void FindNccModels(JlNCCModel modelIDs, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(1958);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
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
		GC.KeepAlive(modelIDs);
	}



	/// <summary>
	///   Create an interleaved image from a multichannel image.
	/// </summary>
	/// <param name="pixelFormat">Target format for InterleavedImage. Default: "rgba"</param>
	/// <param name="rowBytes">Number of bytes in a row of the output image. Default: "match"</param>
	/// <param name="alpha">Alpha value for three channel input images. Default: 255</param>
	/// <returns>Output interleaved image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 interleaved 图像 从 multichannel 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.InterleaveChannels("rgba", "match", 255);
	///   </code>
	/// </remarks>
	public JlImage InterleaveChannels(string pixelFormat, JlTuple rowBytes, int alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(1969);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, pixelFormat);
		JlNativeApi.Store(proc, 1, rowBytes);
		JlNativeApi.StoreI(proc, 2, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBytes);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Create an interleaved image from a multichannel image.
	/// </summary>
	/// <param name="pixelFormat">Target format for InterleavedImage. Default: "rgba"</param>
	/// <param name="rowBytes">Number of bytes in a row of the output image. Default: "match"</param>
	/// <param name="alpha">Alpha value for three channel input images. Default: 255</param>
	/// <returns>Output interleaved image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 interleaved 图像 从 multichannel 图像。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.InterleaveChannels("rgba", "match", 255);
	///   </code>
	/// </remarks>
	public JlImage InterleaveChannels(string pixelFormat, string rowBytes, int alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(1969);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, pixelFormat);
		JlNativeApi.StoreS(proc, 1, rowBytes);
		JlNativeApi.StoreI(proc, 2, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Segment image using Maximally Stable Extremal Regions (MSER).
	/// </summary>
	/// <param name="MSERLight">Segmented light MSERs.</param>
	/// <param name="polarity">The polarity of the returned MSERs. Default: "both"</param>
	/// <param name="minArea">Minimal size of an MSER. Default: 10</param>
	/// <param name="maxArea">Maximal size of an MSER. Default: []</param>
	/// <param name="delta">Amount of thresholds for which a region needs to be stable. Default: 15</param>
	/// <param name="genParamName">List of generic parameter names. Default: []</param>
	/// <param name="genParamValue">List of generic parameter values. Default: []</param>
	/// <returns>Segmented dark MSERs.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment图像Mser。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SegmentImageMser(out JlRegion MSERLight, "both", 10, new JlTuple(), 15, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlRegion SegmentImageMser(out JlRegion MSERLight, string polarity, JlTuple minArea, JlTuple maxArea, JlTuple delta, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(1977);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, polarity);
		JlNativeApi.Store(proc, 1, minArea);
		JlNativeApi.Store(proc, 2, maxArea);
		JlNativeApi.Store(proc, 3, delta);
		JlNativeApi.Store(proc, 4, genParamName);
		JlNativeApi.Store(proc, 5, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minArea);
		JlNativeApi.UnpinTuple(maxArea);
		JlNativeApi.UnpinTuple(delta);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out MSERLight);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Segment image using Maximally Stable Extremal Regions (MSER).
	/// </summary>
	/// <param name="MSERLight">Segmented light MSERs.</param>
	/// <param name="polarity">The polarity of the returned MSERs. Default: "both"</param>
	/// <param name="minArea">Minimal size of an MSER. Default: 10</param>
	/// <param name="maxArea">Maximal size of an MSER. Default: []</param>
	/// <param name="delta">Amount of thresholds for which a region needs to be stable. Default: 15</param>
	/// <param name="genParamName">List of generic parameter names. Default: []</param>
	/// <param name="genParamValue">List of generic parameter values. Default: []</param>
	/// <returns>Segmented dark MSERs.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Segment图像Mser。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.SegmentImageMser(out JlRegion MSERLight, "both", 10, new JlTuple(), 15, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlRegion SegmentImageMser(out JlRegion MSERLight, string polarity, int minArea, int maxArea, int delta, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(1977);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, polarity);
		JlNativeApi.StoreI(proc, 1, minArea);
		JlNativeApi.StoreI(proc, 2, maxArea);
		JlNativeApi.StoreI(proc, 3, delta);
		JlNativeApi.Store(proc, 4, genParamName);
		JlNativeApi.Store(proc, 5, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out MSERLight);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   Insert objects into an iconic object tuple.
	/// </summary>
	/// <param name="objectsInsert">Object tuple to insert.</param>
	/// <param name="index">Index to insert objects.</param>
	/// <returns>Extended object tuple.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Insert objects 为 图像对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage objectsInsert = ...;
	///   JlImage obj = ...;
	///   var result = obj.InsertObj(objectsInsert, 0);
	///   </code>
	/// </remarks>
	public JlImage InsertObj(JlImage objectsInsert, int index)
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
	///   <para><b>功能说明</b></para>
	///   <para>Remove objects 从 图像对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple index = ...;
	///   JlImage obj = ...;
	///   var result = obj.RemoveObj(index);
	///   </code>
	/// </remarks>
	public new JlImage RemoveObj(JlTuple index)
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
	///   <para><b>功能说明</b></para>
	///   <para>Remove objects 从 图像对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.RemoveObj(0);
	///   </code>
	/// </remarks>
	public new JlImage RemoveObj(int index)
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
	///   <para><b>功能说明</b></para>
	///   <para>Replaces one 或 more 元素 图像对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage objectsReplace = ...;
	///   JlTuple index = ...;
	///   JlImage obj = ...;
	///   var result = obj.ReplaceObj(objectsReplace, index);
	///   </code>
	/// </remarks>
	public JlImage ReplaceObj(JlImage objectsReplace, JlTuple index)
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
	///   <para><b>功能说明</b></para>
	///   <para>Replaces one 或 more 元素 图像对象 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage objectsReplace = ...;
	///   JlImage obj = ...;
	///   var result = obj.ReplaceObj(objectsReplace, 0);
	///   </code>
	/// </remarks>
	public JlImage ReplaceObj(JlImage objectsReplace, int index)
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

	/// <summary>Get the clutter parameters of a shape model.</summary>
	/// <param name="modelID">Handle of the model.</param>
	/// <param name="genParamName">Parameter names. Default: "use_clutter"</param>
	/// <param name="genParamValue">Parameter values.</param>
	/// <param name="homMat2D">Transformation matrix.</param>
	/// <param name="clutterContrast">Minimum contrast of clutter in the search images.</param>
	/// <returns>Region where no clutter should occur.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取 clutter 参数 形状模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>模板匹配定位（形状匹配或 NCC）</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlShapeModel modelID = ...;
	///   var result = JlImage.GetShapeModelClutter(modelID, "use_clutter", out JlTuple genParamValue, out JlHomMat2D homMat2D, out int clutterContrast);
	///   </code>
	/// </remarks>
	public static JlRegion GetShapeModelClutter(JlShapeModel modelID, JlTuple genParamName, out JlTuple genParamValue, out JlHomMat2D homMat2D, out int clutterContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2055);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out genParamValue);
		err = JlHomMat2D.LoadNew(proc, 1, err, out homMat2D);
		err = JlNativeApi.LoadI(proc, 2, err, out clutterContrast);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(modelID);
		return obj;
	}

	/// <summary>Get the clutter parameters of a shape model.</summary>
	/// <param name="modelID">Handle of the model.</param>
	/// <param name="genParamName">Parameter names. Default: "use_clutter"</param>
	/// <param name="genParamValue">Parameter values.</param>
	/// <param name="homMat2D">Transformation matrix.</param>
	/// <param name="clutterContrast">Minimum contrast of clutter in the search images.</param>
	/// <returns>Region where no clutter should occur.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取 clutter 参数 形状模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>模板匹配定位（形状匹配或 NCC）</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlShapeModel modelID = ...;
	///   var result = JlImage.GetShapeModelClutter(modelID, "use_clutter", out string genParamValue, out JlHomMat2D homMat2D, out int clutterContrast);
	///   </code>
	/// </remarks>
	public static JlRegion GetShapeModelClutter(JlShapeModel modelID, string genParamName, out string genParamValue, out JlHomMat2D homMat2D, out int clutterContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2055);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadS(proc, 0, err, out genParamValue);
		err = JlHomMat2D.LoadNew(proc, 1, err, out homMat2D);
		err = JlNativeApi.LoadI(proc, 2, err, out clutterContrast);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(modelID);
		return obj;
	}

	/// <summary>Set the clutter parameters of a shape model.</summary>
	/// <param name="clutterRegion">Region where no clutter should occur.</param>
	/// <param name="modelID">Handle of the model.</param>
	/// <param name="homMat2D">Transformation matrix.</param>
	/// <param name="clutterContrast">Minimum contrast of clutter in the search images. Default: 128</param>
	/// <param name="genParamName">Parameter names.</param>
	/// <param name="genParamValue">Parameter values.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 clutter 参数 形状模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>模板匹配定位（形状匹配或 NCC）</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion clutterRegion = ...;
	///   JlShapeModel modelID = ...;
	///   JlHomMat2D homMat2D = ...;
	///   JlTuple genParamName = ...;
	///   JlTuple genParamValue = ...;
	///   JlImage.SetShapeModelClutter(clutterRegion, modelID, homMat2D, 128, genParamName, genParamValue);
	///   </code>
	/// </remarks>
	public static void SetShapeModelClutter(JlRegion clutterRegion, JlShapeModel modelID, JlHomMat2D homMat2D, int clutterContrast, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(2057);
		JlNativeApi.Store(proc, 1, clutterRegion);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreI(proc, 2, clutterContrast);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(clutterRegion);
		GC.KeepAlive(modelID);
	}

	/// <summary>Set the clutter parameters of a shape model.</summary>
	/// <param name="clutterRegion">Region where no clutter should occur.</param>
	/// <param name="modelID">Handle of the model.</param>
	/// <param name="homMat2D">Transformation matrix.</param>
	/// <param name="clutterContrast">Minimum contrast of clutter in the search images. Default: 128</param>
	/// <param name="genParamName">Parameter names.</param>
	/// <param name="genParamValue">Parameter values.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 clutter 参数 形状模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>模板匹配定位（形状匹配或 NCC）</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion clutterRegion = ...;
	///   JlShapeModel modelID = ...;
	///   JlHomMat2D homMat2D = ...;
	///   JlImage.SetShapeModelClutter(clutterRegion, modelID, homMat2D, 128, "value", 0.0);
	///   </code>
	/// </remarks>
	public static void SetShapeModelClutter(JlRegion clutterRegion, JlShapeModel modelID, JlHomMat2D homMat2D, int clutterContrast, string genParamName, double genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(2057);
		JlNativeApi.Store(proc, 1, clutterRegion);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreI(proc, 2, clutterContrast);
		JlNativeApi.StoreS(proc, 3, genParamName);
		JlNativeApi.StoreD(proc, 4, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(clutterRegion);
		GC.KeepAlive(modelID);
	}

	/// <summary>Read metadata from image files.</summary>
	/// <param name="format">Graphic format. Default: "tiff"</param>
	/// <param name="tagName">Name of the tag to be written in the image file. Default: "tiff_image_description"</param>
	/// <param name="fileName">Name of image file.</param>
	/// <returns>Output tag value read from the image file.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取 metadata 从 图像 files。</para>
	///   <para><b>典型场景</b></para>
	///   <para>从文件加载图像、区域、模型或数据</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   var result = JlImage.ReadImageMetadata("tiff", "tiff_image_description", "image.png");
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>Rgb1ToGray、Threshold、CropDomain</para>
	/// </remarks>
	public static JlTuple ReadImageMetadata(string format, JlTuple tagName, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(2062);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.Store(proc, 1, tagName);
		JlNativeApi.StoreS(proc, 2, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tagName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>以给定标记为种子做分水，把盆地与标记对应起来。</summary>
	/// <param name="markers">浸水的起始标记区域。</param>
	/// <returns>每个标记对应的盆地。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 2067。标记区域作为第二个图标输入（<c>Store(proc, 2, markers)</c>）传进去，
	///   图像本身是第一个输入；英文说明是"按标记合并盆地"，即先分水再把同属一个标记的盆地并回去。
	///   这是三个分水算子里唯一能<b>由调用方控制分割粒度</b>的那个：要几个目标就给几个标记。</para>
	///   <para><b>前提</b>标记必须是落在各目标内部的小区域（例如 <c>RegiongrowingMean</c> 的种子、
	///   区域 <c>DistanceTransform(...)</c> 得到的距离图峰值、或 <c>Threshold</c> 后手工缩到核心）；
	///   标记压不到底（重叠/相邻标记距离过近）时
	///   两个目标会被并进同一盆地。标记区域与图像坐标系必须一致，本层不做尺寸/域检查 [待实测]。</para>
	///   <para><b>输出</b>返回值是盆地集合；输出个数与标记个数的对应关系（是否一一对应、顺序是否保持）本层无法确定 [待实测]，
	///   用 <c>CountObj()</c> 核对，需要严格对应时用 <c>TestSubsetRegion</c> 判断包含关系。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.RegiongrowingMean(new JlTuple(120.0), new JlTuple(80.0), 5.0, 50);
	///   using JlRegion basins = img.WatershedsMarker(seeds);
	///   int n = basins.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>只读 <paramref name="markers"/>，所有权不转移，调用方自行释放；
	///   代码末尾对图像与标记都 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlRegion WatershedsMarker(JlRegion markers)
	{
		IntPtr proc = JlNativeApi.PreCall(2067);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, markers);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(markers);
		return obj;
	}

	/// <summary>Write metadata of image files.</summary>
	/// <param name="format">Graphic format. Default: "tiff"</param>
	/// <param name="tagName">Name of the tag to be written in the image file. Default: "tiff_image_description"</param>
	/// <param name="tagValue">Value of the tag to be written in the image file.</param>
	/// <param name="fileName">Name of image file.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>写入 metadata 图像 files。</para>
	///   <para><b>典型场景</b></para>
	///   <para>将图像、区域、模型或数据保存到文件</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple tagValue = ...;
	///   JlImage.WriteImageMetadata("tiff", "tiff_image_description", tagValue, "image.png");
	///   </code>
	/// </remarks>
	public static void WriteImageMetadata(string format, JlTuple tagName, JlTuple tagValue, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(2068);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.Store(proc, 1, tagName);
		JlNativeApi.Store(proc, 2, tagValue);
		JlNativeApi.StoreS(proc, 3, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tagName);
		JlNativeApi.UnpinTuple(tagValue);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>
	///   Cut out one or more arbitrarily oriented rectangular image areas.
	/// </summary>
	/// <param name="row">Row index of the image crop center. Default: 300.0</param>
	/// <param name="column">Column index of the image crop center. Default: 200.0</param>
	/// <param name="phi">Orientation of the rectangle (arc measure). Default: 0.0</param>
	/// <param name="length1">First half edge length of the rectangle. Default: 100.0</param>
	/// <param name="length2">Second half edge length of the rectangle. Default: 20.0</param>
	/// <param name="alignToAxis">Determines whether the cropped image part is aligned with the coordinate axes. Default: "true"</param>
	/// <param name="interpolation">Interpolation method. Default: "constant"</param>
	/// <returns>Cropped image part(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>裁剪Rectangle2。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CropRectangle2(300.0, 200.0, 0.0, 100.0, 20.0, "true", "constant");
	///   </code>
	/// </remarks>
	public JlImage CropRectangle2(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2, string alignToAxis, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(2086);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, length1);
		JlNativeApi.Store(proc, 4, length2);
		JlNativeApi.StoreS(proc, 5, alignToAxis);
		JlNativeApi.StoreS(proc, 6, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Cut out one or more arbitrarily oriented rectangular image areas.
	/// </summary>
	/// <param name="row">Row index of the image crop center. Default: 300.0</param>
	/// <param name="column">Column index of the image crop center. Default: 200.0</param>
	/// <param name="phi">Orientation of the rectangle (arc measure). Default: 0.0</param>
	/// <param name="length1">First half edge length of the rectangle. Default: 100.0</param>
	/// <param name="length2">Second half edge length of the rectangle. Default: 20.0</param>
	/// <param name="alignToAxis">Determines whether the cropped image part is aligned with the coordinate axes. Default: "true"</param>
	/// <param name="interpolation">Interpolation method. Default: "constant"</param>
	/// <returns>Cropped image part(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>裁剪Rectangle2。</para>
	///   <para><b>典型场景</b></para>
	///   <para>几何裁剪与尺寸变换</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.CropRectangle2(300.0, 200.0, 0.0, 100.0, 20.0, "true", "constant");
	///   </code>
	/// </remarks>
	public JlImage CropRectangle2(double row, double column, double phi, double length1, double length2, string alignToAxis, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(2086);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, length1);
		JlNativeApi.StoreD(proc, 4, length2);
		JlNativeApi.StoreS(proc, 5, alignToAxis);
		JlNativeApi.StoreS(proc, 6, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}




	/// <summary>
	///   Histogram linearization within a rectangluar mask.
	/// </summary>
	/// <param name="mode">Processing mode. Default: "accurate"</param>
	/// <param name="maskWidth">Width of the filter mask. Default: 51</param>
	/// <param name="maskHeight">Height of the filter mask. Default: 51</param>
	/// <param name="maxContrast">Maximum contrast. Default: 0.01</param>
	/// <returns>Image with linearized gray values.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Equ直方图图像Rect。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EquHistoImageRect("accurate", 51, 51, 0.01);
	///   </code>
	/// </remarks>
	public JlImage EquHistoImageRect(string mode, int maskWidth, int maskHeight, JlTuple maxContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2152);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.StoreI(proc, 2, maskHeight);
		JlNativeApi.Store(proc, 3, maxContrast);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxContrast);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Histogram linearization within a rectangluar mask.
	/// </summary>
	/// <param name="mode">Processing mode. Default: "accurate"</param>
	/// <param name="maskWidth">Width of the filter mask. Default: 51</param>
	/// <param name="maskHeight">Height of the filter mask. Default: 51</param>
	/// <param name="maxContrast">Maximum contrast. Default: 0.01</param>
	/// <returns>Image with linearized gray values.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Equ直方图图像Rect。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.EquHistoImageRect("accurate", 51, 51, 0.01);
	///   </code>
	/// </remarks>
	public JlImage EquHistoImageRect(string mode, int maskWidth, int maskHeight, double maxContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2152);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.StoreI(proc, 2, maskHeight);
		JlNativeApi.StoreD(proc, 3, maxContrast);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>Return the parameters and properties of a measure object.</summary>
	/// <param name="measureHandle">Measure object handle.</param>
	/// <param name="genParamName">Name of the parameter to be returned. Default: "type"</param>
	/// <returns>Value of the parameter.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回 参数 和 properties 测量对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMeasure measureHandle = ...;
	///   var result = JlImage.GetMeasureParam(measureHandle, "type");
	///   </code>
	/// </remarks>
	public static JlTuple GetMeasureParam(JlMeasure measureHandle, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(2153);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(measureHandle);
		return tuple;
	}

	/// <summary>Return the parameters and properties of a measure object.</summary>
	/// <param name="measureHandle">Measure object handle.</param>
	/// <param name="genParamName">Name of the parameter to be returned. Default: "type"</param>
	/// <returns>Value of the parameter.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回 参数 和 properties 测量对象。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMeasure measureHandle = ...;
	///   var result = JlImage.GetMeasureParam(measureHandle, "type");
	///   </code>
	/// </remarks>
	public static JlTuple GetMeasureParam(JlMeasure measureHandle, string genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(2153);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(measureHandle);
		return tuple;
	}

	/// <summary>用任意形状区域掩膜做均值滤波，id 2154。</summary>
	/// <param name="mask">掩膜区域，决定参与平均的像素集合。</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 2154，掩膜是<b>图标输入</b>（区域，<c>Store(proc, 2, mask)</c>），
	///   每像素的输出只取"掩膜盖住的那些像素"的平均。因此掩膜形状直接决定平滑的方向性：
	///   细长水平掩膜沿行平滑、竖直掩膜沿列平滑，这是矩形窗 <see cref="MeanImage(int,int)"/> 做不到的。</para>
	///   <para><b>坑</b>掩膜像素数越少越接近原图（1 个像素时是恒等变换），噪声抑制与方向性直接受掩膜面积影响；
	///   掩膜若由分割结果生成，面积逐帧变化会让平滑强度逐帧不同——需要稳定强度时改用带显式窗尺寸的
	///   <see cref="MeanImage(int,int)"/>。本算子<b>没有</b>边界处理参数 [待实测：边缘如何取值]。</para>
	///   <para><b>与相邻算子的取舍</b>要各向同性平滑用 <c>MeanImage</c>/<see cref="GaussImage(int)"/>（更快、参数简单）；
	///   要任意形状但取排序值而不是均值，用 <see cref="RankImage(JlRegion,int,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 0.0, 9.0);        // 1×10 水平线掩膜：只沿行平滑
	///   using JlImage smooth = img.MeanImageShape(mask);
	///   </code>
	///   <para><b>资源与坑</b>掩膜只读、调用方释放；输出为新句柄。</para>
	/// </remarks>
	public JlImage MeanImageShape(JlRegion mask)
	{
		IntPtr proc = JlNativeApi.PreCall(2154);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}




	/// <summary>
	///   Add a border to an image.
	/// </summary>
	/// <param name="size">Size of the border in pixels. Default: 10</param>
	/// <param name="value">Gray value of the border. Default: 100</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add border 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AddImageBorder(10, 100);
	///   </code>
	/// </remarks>
	public JlImage AddImageBorder(JlTuple size, JlTuple value)
	{
		IntPtr proc = JlNativeApi.PreCall(2172);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, size);
		JlNativeApi.Store(proc, 1, value);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(size);
		JlNativeApi.UnpinTuple(value);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Add a border to an image.
	/// </summary>
	/// <param name="size">Size of the border in pixels. Default: 10</param>
	/// <param name="value">Gray value of the border. Default: 100</param>
	/// <returns>Output image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add border 图像。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage obj = ...;
	///   var result = obj.AddImageBorder(10, 100);
	///   </code>
	/// </remarks>
	public JlImage AddImageBorder(int size, int value)
	{
		IntPtr proc = JlNativeApi.PreCall(2172);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.StoreI(proc, 1, value);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Perform a convolution along the channel dimension.
	/// </summary>
	/// <param name="filter">Filter coefficients.</param>
	/// <param name="border">Type of boundary treatment. Default: "constant"</param>
	/// <returns>Smoothed multichannel image.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Perform a convolution along the channel dimension。</para>
	///   <para><b>典型场景</b></para>
	///   <para>图像滤波与预处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple filter = ...;
	///   JlImage obj = ...;
	///   var result = obj.ConvolChannels(filter, "constant");
	///   </code>
	/// </remarks>
	public JlImage ConvolChannels(JlTuple filter, string border)
	{
		IntPtr proc = JlNativeApi.PreCall(2219);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, filter);
		JlNativeApi.StoreS(proc, 1, border);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(filter);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>Compute the filter coefficients of a Savitzky-Golay filter.</summary>
	/// <param name="filterSize">Size of the filter. Default: 11</param>
	/// <param name="polynomialDegree">Degree of the approximating polynomial. Default: 3</param>
	/// <param name="derivative">Derivative of the polynomial. Default: 0</param>
	/// <returns>Filter coefficients.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 滤波 coefficients Savitzky-Golay 滤波。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   var result = JlImage.GenSavitzkyGolayFilter(11, 3, 0);
	///   </code>
	/// </remarks>
	public static JlTuple GenSavitzkyGolayFilter(int filterSize, int polynomialDegree, int derivative)
	{
		IntPtr proc = JlNativeApi.PreCall(2223);
		JlNativeApi.StoreI(proc, 0, filterSize);
		JlNativeApi.StoreI(proc, 1, polynomialDegree);
		JlNativeApi.StoreI(proc, 2, derivative);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}
}
