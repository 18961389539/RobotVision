using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of a region object(-array).</summary>
[Serializable]
public class JlRegion : JlObject, ISerializable, ICloneable
{
	/// <summary>按 1 基序号从区域元组中取出单个区域。</summary>
	/// <param name="index">要取出的元素序号，从 1 开始（HALCON <c>select_obj</c> 约定）。</param>
	/// <returns>新句柄，用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>SelectObj(index)</c>（原生 id 572），作用于区域元组的元素序号而非像素坐标。</para>
	///   <para><b>约束或前提</b>序号是 1 基；单区域对象索引 <c>[1]</c> 返回其副本，索引 &gt;1 会越界由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只取单个元素用本索引器；要一次挑多个或重排整个元组用 <see cref="SelectObj(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion first = r[1];
	///   r.Dispose();
	///   first.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，不共享 <c>this</c> 的生命周期，两者须分别释放。</para>
	/// </remarks>
	public new JlRegion this[JlTuple index] => SelectObj(index);

	/// <summary>区域面积（像素个数）。</summary>
	/// <returns>逐元素面积元组；对区域元组返回每个区域各自的面积。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>AreaCenter(out row, out column)</c> 并只取返回的面积分量，单位为像素数。</para>
	///   <para><b>约束或前提</b>每次访问都会重跑一次 <c>AreaCenter</c>（含原生调用），不要在循环里高频读；只需中心时用 <see cref="Row"/>/<see cref="Column"/> 也别连着读三个属性。</para>
	///   <para><b>与相邻算子的取舍</b>要更多几何量用 <c>RegionFeatures</c>，要主方向用 <c>OrientationRegion</c>；膨胀/腐蚀/仿射后面积必须重新读取，旧值不随之更新。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple area = r.Area;
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>退化（单像素/空）区域面积为 0 或 1，别用它判断"有没有像素"以外的语义。</para>
	/// </remarks>
	public JlTuple Area
	{
		get
		{
			JlTuple row;
			JlTuple column;
			return AreaCenter(out row, out column);
		}
	}

	/// <summary>区域重心行坐标（row = y，向下为正，单位像素）。</summary>
	/// <returns>逐元素重心行坐标元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>AreaCenter(out row, out _)</c> 并只取行分量。</para>
	///   <para><b>与相邻算子的取舍</b>同时要行和列时，别分别读 <see cref="Row"/> 与 <see cref="Column"/>（那会跑两次原生 <c>AreaCenter</c>），应直接调 <c>AreaCenter(out row, out col)</c> 一次拿全。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 20.0, 30.0, 40.0);
	///   JlTuple row = r.Row;
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>空区域的重心无定义，返回可能为 0/NaN 或由原生报错 [待实测]。</para>
	/// </remarks>
	public JlTuple Row
	{
		get
		{
			AreaCenter(out JlTuple row, out JlTuple _);
			return row;
		}
	}

	/// <summary>区域重心列坐标（column = x，向右为正，单位像素）。</summary>
	/// <returns>逐元素重心列坐标元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>AreaCenter(out _, out column)</c> 并只取列分量。</para>
	///   <para><b>与相邻算子的取舍</b>同时要行和列时别分别读 <see cref="Row"/> 与 <see cref="Column"/>，直接调一次 <c>AreaCenter(out row, out col)</c> 更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 20.0, 30.0, 40.0);
	///   JlTuple col = r.Column;
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>空区域的重心无定义 [待实测]。</para>
	/// </remarks>
	public JlTuple Column
	{
		get
		{
			AreaCenter(out JlTuple _, out JlTuple column);
			return column;
		}
	}

	/// <summary>创建一个未初始化（UNDEF）的区域句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>以 <c>JlObjectBase.UNDEF</c> 键构造、且不复制，得到一个空占位句柄，随后由 <c>Load</c>/<c>Deserialize</c> 等填充。</para>
	///   <para><b>约束或前提</b>UNDEF 句柄未参与任何原生运算前不能直接取属性或做布尔运算，否则由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>已知几何形状时用带参构造器（矩形/圆等）直接生成，别先无参再改。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///   using System.IO;
	///
	///   JlRegion placeholder = new JlRegion();
	///   byte[] bytes = new JlRegion(10.0, 10.0, 30.0, 30.0).SerializeRegion();
	///   MemoryStream ms = new MemoryStream(bytes);
	///   JlRegion r = JlRegion.Deserialize(ms);
	///   placeholder.Dispose();
	///   r.Dispose();
	///   ms.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>仍是 <see cref="IDisposable"/>，即便未初始化也需 <c>Dispose</c>。</para>
	/// </remarks>
	public JlRegion()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlRegion(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlRegion(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>由同族 <see cref="JlObject"/> 对象复制构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlRegion(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "region");
	}

	/// <summary>内部工厂：从算子的输出对象槽装载本类新实例；<paramref name="err"/> 为调用错误码并原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlRegion obj)
	{
		obj = new JlRegion(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	/// <summary>
	///   创建与坐标轴平行的矩形（轴对齐，闭区间角点）。
	/// </summary>
	/// <param name="row1">左上角行坐标。Default: 30.0</param>
	/// <param name="column1">左上角列坐标。Default: 20.0</param>
	/// <param name="row2">右下角行坐标。Default: 100.0</param>
	/// <param name="column2">右下角列坐标。Default: 200.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_rectangle1</c>（id 588），把结果原地生成到正在构造的实例上（<c>InitOCT</c> 1 个输出 + <c>Load</c>），不是返回新句柄。</para>
	///   <para><b>约束或前提</b>(row1,column1) 为左上、(row2,column2) 为右下，须满足 row1≤row2、col1≤col2；坐标为闭区间端点。</para>
	///   <para><b>与相邻算子的取舍</b>要旋转矩形用 <c>gen_rectangle2</c> 族；本重载收 <c>JlTuple</c> 可一次生成多个矩形（元组等长），标量版本用 <see cref="JlRegion(double, double, double, double)"/>。</para>
	///   <para><b>参数取向</b>形参序 0..3 与原生一致；本重载 <c>Store</c> 固定每个元组后 <c>UnpinTuple</c>，double 重载 <c>StoreD</c> 直写无固定开销，共用 id 588。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(30.0, 20.0, 100.0, 200.0);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>坐标越界或行列反序由原生层处理 [待实测]。</para>
	/// </remarks>
	public JlRegion(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(588);
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
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建与坐标轴平行的矩形（轴对齐，闭区间角点，标量重载）。
	/// </summary>
	/// <param name="row1">左上角行坐标。Default: 30.0</param>
	/// <param name="column1">左上角列坐标。Default: 20.0</param>
	/// <param name="row2">右下角行坐标。Default: 100.0</param>
	/// <param name="column2">右下角列坐标。Default: 200.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_rectangle1</c>（id 588），结果原地生成到该实例，几何/角点语义同 <see cref="JlRegion(JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>只画一个矩形用本标量重载；要一次批量生成多个矩形用 <c>JlTuple</c> 重载。</para>
	///   <para><b>参数取向</b>本重载四参均 <c>StoreD</c> 直写，无 <c>Store</c>+<c>UnpinTuple</c> 的钉固定开销，共用 id 588。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(30.0, 20.0, 100.0, 200.0);
	///   r.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion(double row1, double column1, double row2, double column2)
	{
		IntPtr proc = JlNativeApi.PreCall(588);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建椭圆扇形（可整椭圆）。
	/// </summary>
	/// <param name="row">中心行坐标。Default: 200.0</param>
	/// <param name="column">中心列坐标。Default: 200.0</param>
	/// <param name="phi">长半径 radius1 的朝向，弧度。Default: 0.0</param>
	/// <param name="radius1">长半径（像素）。Default: 100.0</param>
	/// <param name="radius2">短半径（像素）。Default: 60.0</param>
	/// <param name="startAngle">扇形起始角，弧度。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角，弧度。Default: 3.14159</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_ellipse_sector</c>（id 593），结果原地生成到该实例。</para>
	///   <para><b>约束或前提</b>三个角均为弧度；<c>startAngle</c>/<c>endAngle</c> 决定扇形张角，默认 0..3.14159 只得到半椭圆扇形，要整椭圆需 0..2π。</para>
	///   <para><b>与相邻算子的取舍</b>要圆扇形用五参构造器（id 595）；radius1 与 radius2 相等时退化为圆但仍走椭圆路径。</para>
	///   <para><b>参数取向</b>形参序 0..6 与原生一致；本重载逐元组 <c>Store</c>+<c>UnpinTuple</c>，标量重载 <c>StoreD</c> 直写，共用 id 593。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion half = new JlRegion(200.0, 200.0, 0.0, 100.0, 60.0, 0.0, 3.14159);
	///   half.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>半径给负或 phi 语义（图像坐标下正方向）以原生为准 [待实测]。</para>
	/// </remarks>
	public JlRegion(JlTuple row, JlTuple column, JlTuple phi, JlTuple radius1, JlTuple radius2, JlTuple startAngle, JlTuple endAngle)
	{
		IntPtr proc = JlNativeApi.PreCall(593);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, radius1);
		JlNativeApi.Store(proc, 4, radius2);
		JlNativeApi.Store(proc, 5, startAngle);
		JlNativeApi.Store(proc, 6, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(radius1);
		JlNativeApi.UnpinTuple(radius2);
		JlNativeApi.UnpinTuple(startAngle);
		JlNativeApi.UnpinTuple(endAngle);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建椭圆扇形（标量重载）。
	/// </summary>
	/// <param name="row">中心行坐标。Default: 200.0</param>
	/// <param name="column">中心列坐标。Default: 200.0</param>
	/// <param name="phi">长半径 radius1 的朝向，弧度。Default: 0.0</param>
	/// <param name="radius1">长半径（像素）。Default: 100.0</param>
	/// <param name="radius2">短半径（像素）。Default: 60.0</param>
	/// <param name="startAngle">扇形起始角，弧度。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角，弧度。Default: 3.14159</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_ellipse_sector</c>（id 593），结果原地生成到该实例；角度单位与整椭圆条件同 <see cref="JlRegion(JlTuple, JlTuple, JlTuple, JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>单个扇形用本标量重载，批量用 <c>JlTuple</c> 重载。</para>
	///   <para><b>参数取向</b>七参均 <c>StoreD</c> 直写、无固定与 <c>UnpinTuple</c>，共用 id 593。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion full = new JlRegion(200.0, 200.0, 0.0, 100.0, 100.0, 0.0, 6.28318);
	///   full.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion(double row, double column, double phi, double radius1, double radius2, double startAngle, double endAngle)
	{
		IntPtr proc = JlNativeApi.PreCall(593);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, radius1);
		JlNativeApi.StoreD(proc, 4, radius2);
		JlNativeApi.StoreD(proc, 5, startAngle);
		JlNativeApi.StoreD(proc, 6, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建圆扇形（可整圆）。
	/// </summary>
	/// <param name="row">中心行坐标。Default: 200.0</param>
	/// <param name="column">中心列坐标。Default: 200.0</param>
	/// <param name="radius">半径（像素）。Default: 100.5</param>
	/// <param name="startAngle">扇形起始角，弧度。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角，弧度。Default: 3.14159</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_circle_sector</c>（id 595），结果原地生成到该实例。</para>
	///   <para><b>约束或前提</b>角度弧度制；默认 0..3.14159 是半圆扇形，整圆需 0..2π。要与椭圆路径区分：本构造器无 phi/短半径。</para>
	///   <para><b>与相邻算子的取舍</b>只要整圆用三参构造器（id 596）；需要椭圆用 <c>gen_ellipse_sector</c>。</para>
	///   <para><b>参数取向</b>形参序 0..4 与原生一致；本重载 <c>Store</c>+<c>UnpinTuple</c>，标量重载 <c>StoreD</c> 直写，共用 id 595。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion sector = new JlRegion(200.0, 200.0, 100.5, 0.0, 3.14159);
	///   sector.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion(JlTuple row, JlTuple column, JlTuple radius, JlTuple startAngle, JlTuple endAngle)
	{
		IntPtr proc = JlNativeApi.PreCall(595);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.Store(proc, 3, startAngle);
		JlNativeApi.Store(proc, 4, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(startAngle);
		JlNativeApi.UnpinTuple(endAngle);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建圆扇形（标量重载）。
	/// </summary>
	/// <param name="row">中心行坐标。Default: 200.0</param>
	/// <param name="column">中心列坐标。Default: 200.0</param>
	/// <param name="radius">半径（像素）。Default: 100.5</param>
	/// <param name="startAngle">扇形起始角，弧度。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角，弧度。Default: 3.14159</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_circle_sector</c>（id 595），结果原地生成到该实例；角度语义同 <see cref="JlRegion(JlTuple, JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>单个扇形用本重载，批量用元组重载；只要整圆用 <see cref="JlRegion(double, double, double)"/>。</para>
	///   <para><b>参数取向</b>五参均 <c>StoreD</c> 直写、无固定开销，共用 id 595。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion sector = new JlRegion(200.0, 200.0, 100.5, 0.0, 3.14159);
	///   sector.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion(double row, double column, double radius, double startAngle, double endAngle)
	{
		IntPtr proc = JlNativeApi.PreCall(595);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.StoreD(proc, 3, startAngle);
		JlNativeApi.StoreD(proc, 4, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建整圆。
	/// </summary>
	/// <param name="row">圆心行坐标。Default: 200.0</param>
	/// <param name="column">圆心列坐标。Default: 200.0</param>
	/// <param name="radius">半径（像素）。Default: 100.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_circle</c>（id 596），结果原地生成到该实例，生成完整圆盘。</para>
	///   <para><b>约束或前提</b>radius 以像素计，圆心可为亚像素；radius≤0 的退化行为以原生为准 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要扇形用五参构造器（id 595），要椭圆用七参（id 593）。</para>
	///   <para><b>参数取向</b>形参序 0..2 与原生一致；本重载 <c>Store</c>+<c>UnpinTuple</c>，标量重载 <c>StoreD</c> 直写，共用 id 596。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion c = new JlRegion(200.0, 200.0, 100.5);
	///   c.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion(JlTuple row, JlTuple column, JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(596);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建整圆（标量重载）。
	/// </summary>
	/// <param name="row">圆心行坐标。Default: 200.0</param>
	/// <param name="column">圆心列坐标。Default: 200.0</param>
	/// <param name="radius">半径（像素）。Default: 100.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_circle</c>（id 596），结果原地生成到该实例；语义同 <see cref="JlRegion(JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>单个圆用本重载，批量用元组重载。</para>
	///   <para><b>参数取向</b>三参均 <c>StoreD</c> 直写、无固定开销，共用 id 596。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion c = new JlRegion(200.0, 200.0, 100.5);
	///   c.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion(double row, double column, double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(596);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeRegion();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlRegion(SerializationInfo info, StreamingContext context)
	{
		DeserializeRegion((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把区域（元组）以二进制 Vision 格式写入流。</summary>
	/// <param name="stream">目标可写流；本方法在其当前位置追加写入 <c>SerializeRegion()</c> 的字节。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>SerializeRegion()</c> 拿到字节后 <c>WriteToStream</c>；隐藏基类同名方法（<c>new</c>）。</para>
	///   <para><b>约束或前提</b>序列化的是整个区域元组（多元素一并写入），不是单个区域；流须可写且位置可控。</para>
	///   <para><b>与相邻算子的取舍</b>跨进程/落盘用本方法配 <see cref="Deserialize(Stream)"/>；内存内独立复制用 <see cref="Clone()"/> 更直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///   using System.IO;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   MemoryStream ms = new MemoryStream();
	///   r.Serialize(ms);
	///   r.Dispose();
	///   ms.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>不释放 <c>this</c>；调用后句柄仍可继续用。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeRegion(), stream);
	}

	/// <summary>从二进制 Vision 格式流重建区域（元组）。</summary>
	/// <param name="stream">含 <see cref="Serialize(Stream)"/> 写出字节的可读流。</param>
	/// <returns>新句柄（内部新建空区域后 <c>DeserializeRegion</c> 填充），用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法，先 <c>ReadFromStream</c> 取字节，再对新建实例 <c>DeserializeRegion</c>；隐藏基类同名静态方法（<c>new static</c>）。</para>
	///   <para><b>约束或前提</b>流须由本库 <c>Serialize</c> 或 <c>SerializeRegion</c> 写出，格式不匹配由原生层报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>从流还原用本方法；内存内复制用 <see cref="Clone()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///   using System.IO;
	///
	///   MemoryStream ms = new MemoryStream();
	///   new JlRegion(10.0, 10.0, 30.0, 30.0).Serialize(ms);
	///   ms.Position = 0;
	///   JlRegion r = JlRegion.Deserialize(ms);
	///   r.Dispose();
	///   ms.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，与来源流无关；注意把流位置复位到写入起点再读。</para>
	/// </remarks>
	public new static JlRegion Deserialize(Stream stream)
	{
		JlRegion hRegion = new JlRegion();
		hRegion.DeserializeRegion(JlSerializationBuffer.ReadFromStream(stream));
		return hRegion;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>深拷贝区域（元组），返回独立的新句柄。</summary>
	/// <returns>与原对象内容相同、但句柄独立的新 <see cref="JlRegion"/>，用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>SerializeRegion()</c> 再对新建实例 <c>DeserializeRegion</c>，得到的副本与原对象完全解耦（改动互不影响）；隐藏基类 <c>new</c>，显式 <c>ICloneable.Clone</c> 转调本方法。</para>
	///   <para><b>与相邻算子的取舍</b>JlRegion 是不可变语义的句柄，多数运算已返回新对象，通常无需 Clone；只有在要把当前句柄"另存一份、之后可能被 Dispose 而另一份仍要留"时才用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion copy = r.Clone();
	///   r.Dispose();
	///   copy.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>克隆走完整序列化/反序列化，对超大区域元组有实打实的内存与耗时开销 [待实测]。</para>
	/// </remarks>
	public new JlRegion Clone()
	{
		byte[] data = SerializeRegion();
		JlRegion obj = new JlRegion();
		obj.DeserializeRegion(data);
		return obj;
	}

	/// <summary>两区域求交，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region1.Intersection(region2)</c>，返回二者公共像素组成的新区域。</para>
	///   <para><b>约束或前提</b>两侧都可为区域元组，逐元素按 HALCON 元组语义求交；不相交时结果为空区域。</para>
	///   <para><b>与相邻算子的取舍</b>要"区域裁剪到图像范围"用 <c>operator &amp;(JlRegion, JlImage)</c>（右侧传 <c>JlImage</c>，内部先 <c>GetDomain</c> 再求交）；要并集用 <c>|</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(0.0, 0.0, 50.0, 50.0);
	///   JlRegion b = new JlRegion(25.0, 25.0, 80.0, 80.0);
	///   JlRegion c = a &amp; b;
	///   a.Dispose();
	///   b.Dispose();
	///   c.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，操作数不释放仍可继续用。</para>
	/// </remarks>
	public static JlRegion operator &(JlRegion region1, JlRegion region2)
	{
		return region1.Intersection(region2);
	}

	/// <summary>两区域（元组）求并，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region1.Union2(region2)</c>，把两侧所有元素的并集作为结果区域返回。</para>
	///   <para><b>约束或前提</b>这是并的合并结果（不保留"哪一侧"的归属），也不自动做 <c>Connection</c> 连通拆分。</para>
	///   <para><b>与相邻算子的取舍</b>对单个元组内部各元素求并用 <c>Union1</c>；要保留两块重叠区域各自独立时别用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(0.0, 0.0, 50.0, 50.0);
	///   JlRegion b = new JlRegion(25.0, 25.0, 80.0, 80.0);
	///   JlRegion u = a | b;
	///   a.Dispose();
	///   b.Dispose();
	///   u.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；操作数需各自释放。</para>
	/// </remarks>
	public static JlRegion operator |(JlRegion region1, JlRegion region2)
	{
		return region1.Union2(region2);
	}

	/// <summary>区域集合差：region1 减去 region2，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region1.Difference(region2)</c>，结果为属于 region1 但不属于 region2 的像素。</para>
	///   <para><b>与相邻算子的取舍</b>集合差用 <c>/</c>，别误用 <c>-</c>：两个区域之间的 <c>-</c> 是 Minkowski 减（形态学结构元语义），结果完全不同。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(0.0, 0.0, 50.0, 50.0);
	///   JlRegion b = new JlRegion(25.0, 25.0, 80.0, 80.0);
	///   JlRegion d = a / b;
	///   a.Dispose();
	///   b.Dispose();
	///   d.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>方向敏感：<c>a/b</c> 与 <c>b/a</c> 不同；返回新句柄。</para>
	/// </remarks>
	public static JlRegion operator /(JlRegion region1, JlRegion region2)
	{
		return region1.Difference(region2);
	}

	/// <summary>区域求补（相对整个平面），返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.Complement()</c>，结果是原区域的补集。</para>
	///   <para><b>约束或前提</b>补集不一定有限（覆盖图像范围之外的无穷区域），直接用常出问题；应先与感兴趣的图像域求交：<c>(!r) &amp; image</c>。</para>
	///   <para><b>与相邻算子的取舍</b>想要"限定在图像内的补"配 <c>operator &amp;(JlRegion, JlImage)</c> 一起用；想要相对另一区域的差用 <c>/</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlImage img = new JlImage("byte", 100, 100);
	///   JlRegion clipped = (!r) &amp; img;
	///   r.Dispose();
	///   img.Dispose();
	///   clipped.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；不裁剪时其面积可能巨大乃至无界。</para>
	/// </remarks>
	public static JlRegion operator !(JlRegion region)
	{
		return region.Complement();
	}

	/// <summary>把区域裁剪到图像定义域，结果不超出图像范围。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>先取 <c>image.GetDomain()</c> 再与区域求交（<c>region.Intersection(domain)</c>），等价于按图像边界对区域做交集裁剪。</para>
	///   <para><b>约束或前提</b>常用于把 <c>Complement</c> 的无界结果或越界的几何生成结果拉回图像内；图像为多通道不影响域裁剪（域只与尺寸有关）。</para>
	///   <para><b>与相邻算子的取舍</b>两个区域求交用 <c>operator &amp;(JlRegion, JlRegion)</c>；这里右侧是 <c>JlImage</c>，走的是定义域裁剪。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(-20.0, -20.0, 500.0, 500.0);
	///   JlImage img = new JlImage("byte", 100, 100);
	///   JlRegion clipped = r &amp; img;
	///   r.Dispose();
	///   img.Dispose();
	///   clipped.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；被裁掉的像素不可恢复。</para>
	/// </remarks>
	public static JlRegion operator &(JlRegion region, JlImage image)
	{
		return region.Intersection(image.GetDomain());
	}

	/// <summary>判断 region1 是否为 region2 的子集（仅单区域时有效）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>当两侧各只含 1 个区域（<c>CountObj()==1</c>）时，按 <c>(region1/region2).Area==0</c> 判定，即"region1 减去 region2 后为空"。</para>
	///   <para><b>约束或前提</b>只要任一侧是含多个元素的区域元组，就直接返回 <c>false</c>——不报错也不逐元素比较，是易踩的静默坑。</para>
	///   <para><b>与相邻算子的取舍</b><c>&gt;=</c> 是本算子交换两操作数；要显式包含判定也可自行 <c>(a/b).Area==0</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion small = new JlRegion(10.0, 10.0, 20.0, 20.0);
	///   JlRegion big = new JlRegion(0.0, 0.0, 50.0, 50.0);
	///   bool isSubset = small &lt;= big;
	///   small.Dispose();
	///   big.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>判定时会临时构造差集与面积（内部新句柄由原生管理），多区域场景请自行拆分后再比。</para>
	/// </remarks>
	public static bool operator <=(JlRegion region1, JlRegion region2)
	{
		int num = region1.CountObj();
		int num2 = region2.CountObj();
		if (num == 1 && num2 == 1)
		{
			return (int)(region1 / region2).Area == 0;
		}
		return false;
	}

	/// <summary>判断 region1 是否包含 region2（仅单区域时有效）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>region2 &lt;= region1</c>，即 <c>(region2/region1).Area==0</c>。</para>
	///   <para><b>约束或前提</b>与 <c>&lt;=</c> 同样：任一侧为多元素区域元组时直接返回 <c>false</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion big = new JlRegion(0.0, 0.0, 50.0, 50.0);
	///   JlRegion small = new JlRegion(10.0, 10.0, 20.0, 20.0);
	///   bool contains = big &gt;= small;
	///   big.Dispose();
	///   small.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>注意方向：<c>a&gt;=b</c> 表示 a 包含 b，等价于 <c>b&lt;=a</c>。</para>
	/// </remarks>
	public static bool operator >=(JlRegion region1, JlRegion region2)
	{
		return region2 <= region1;
	}

	/// <summary>两区域的 Minkowski 加（region2 作结构元，迭代 1 次），返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region1.MinkowskiAdd1(region2, 1)</c>，把 region2 作为结构元对 region1 做 Minkowski 加（一种广义膨胀）。</para>
	///   <para><b>与相邻算子的取舍</b>别把本算子当并集：并集是 <c>|</c>。也别与 <c>+(JlRegion,double)</c>（半径圆膨胀）混淆——右侧是区域走 Minkowski，右侧是标量走 <c>DilationCircle</c>。</para>
	///   <para><b>参数取向</b>迭代次数被硬编码为 1；要多次迭代直接调 <c>MinkowskiAdd1(region2, n)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion base1 = new JlRegion(20.0, 20.0, 40.0, 40.0);
	///   JlRegion se = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   JlRegion m = base1 + se;
	///   base1.Dispose();
	///   se.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结构元形状会显著改变结果面积与连通块数，返回新句柄。</para>
	/// </remarks>
	public static JlRegion operator +(JlRegion region1, JlRegion region2)
	{
		return region1.MinkowskiAdd1(region2, 1);
	}

	/// <summary>两区域的 Minkowski 减（region2 作结构元，迭代 1 次），返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region1.MinkowskiSub1(region2, 1)</c>，把 region2 作为结构元做广义腐蚀。</para>
	///   <para><b>与相邻算子的取舍</b>别把本算子当集合差：集合差是 <c>/</c>。也别与 <c>-(JlRegion,double)</c>（半径圆腐蚀）混淆——右侧是区域走 Minkowski，右侧是标量走 <c>ErosionCircle</c>。</para>
	///   <para><b>参数取向</b>迭代次数被硬编码为 1；要多次迭代直接调 <c>MinkowskiSub1(region2, n)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion base1 = new JlRegion(10.0, 10.0, 60.0, 60.0);
	///   JlRegion se = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   JlRegion m = base1 - se;
	///   base1.Dispose();
	///   se.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结构元过大可把区域腐蚀为空；返回新句柄。</para>
	/// </remarks>
	public static JlRegion operator -(JlRegion region1, JlRegion region2)
	{
		return region1.MinkowskiSub1(region2, 1);
	}

	/// <summary>用半径为 radius 的圆盘结构元膨胀区域，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.DilationCircle(radius)</c>，radius 为圆结构元半径（像素）。</para>
	///   <para><b>与相邻算子的取舍</b>要按任意区域作结构元做广义膨胀用 <c>+(JlRegion, JlRegion)</c>（Minkowski 加）；本重载只支持圆盘。腐蚀配 <c>-(JlRegion, double)</c>。</para>
	///   <para><b>参数取向</b>与 <c>+(double, JlRegion)</c> 是一对对称重载，写法 <c>r+5.0</c> 与 <c>5.0+r</c> 等价，内部都调 <c>DilationCircle</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(20.0, 20.0, 40.0, 40.0);
	///   JlRegion grown = r + 5.0;
	///   r.Dispose();
	///   grown.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>膨胀后面积/重心/矩全部改变，需重算再读；radius 取负或非整圆的行为以原生为准 [待实测]。</para>
	/// </remarks>
	public static JlRegion operator +(JlRegion region, double radius)
	{
		return region.DilationCircle(radius);
	}

	/// <summary>用半径为 radius 的圆盘结构元膨胀区域（标量在左的对称写法），返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>+(JlRegion, double)</c> 完全等价，仅参数顺序相反，内部同样 <c>region.DilationCircle(radius)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(20.0, 20.0, 40.0, 40.0);
	///   JlRegion grown = 5.0 + r;
	///   r.Dispose();
	///   grown.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>语义、单位、坑同正向膨胀重载。</para>
	/// </remarks>
	public static JlRegion operator +(double radius, JlRegion region)
	{
		return region.DilationCircle(radius);
	}

	/// <summary>用半径为 radius 的圆盘结构元腐蚀区域，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.ErosionCircle(radius)</c>，radius 为圆结构元半径（像素）。</para>
	///   <para><b>与相邻算子的取舍</b>膨胀用 <c>+(JlRegion, double)</c>；按任意区域作结构元的广义腐蚀用 <c>-(JlRegion, JlRegion)</c>（Minkowski 减）。注意腐蚀只有区域在左这一种写法，不存在 <c>double - JlRegion</c> 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(20.0, 20.0, 40.0, 40.0);
	///   JlRegion shrunk = r - 5.0;
	///   r.Dispose();
	///   shrunk.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>radius 超过区域尺度会腐蚀为空；腐蚀后面积/重心需重算。</para>
	/// </remarks>
	public static JlRegion operator -(JlRegion region, double radius)
	{
		return region.ErosionCircle(radius);
	}

	/// <summary>按 Point 平移区域，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.MoveRegion(p.Y, p.X)</c>：<c>p.Y</c> 是行(row=y)方向的整数像素位移，<c>p.X</c> 是列(column=x)方向位移（正数向右/向下）。</para>
	///   <para><b>约束或前提</b>用 <see cref="System.Drawing.Point"/>，位移为整数像素；亚像素平移请改用仿射变换族。</para>
	///   <para><b>与相邻算子的取舍</b>平移是整数步长，缩放用 <c>*(JlRegion,double)</c>；本重载无对应的"负向"减法平移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///   using System.Drawing;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion moved = r + new Point(20, 5);
	///   r.Dispose();
	///   moved.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>平移到图像外不报错、也不裁剪，返回新句柄。</para>
	/// </remarks>
	public static JlRegion operator +(JlRegion region, Point p)
	{
		return region.MoveRegion(p.Y, p.X);
	}

	/// <summary>按各向同性比例缩放区域，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.ZoomRegion(factor, factor)</c>，宽高用同一 factor（factor&gt;1 放大、&lt;1 缩小）。</para>
	///   <para><b>与相邻算子的取舍</b>要横纵不同比例用 <c>ZoomRegion(sw, sh)</c> 直接调；要平移/旋转用仿射变换族。缩放参考点以原生 <c>zoom_region</c> 约定为准 [待实测]。</para>
	///   <para><b>参数取向</b>与 <c>*(double, JlRegion)</c> 是一对对称重载，<c>r*2.0</c> 与 <c>2.0*r</c> 等价。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 40.0);
	///   JlRegion big = r * 2.0;
	///   r.Dispose();
	///   big.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>缩放后面积按 factor 平方变化，重心/矩需重算。</para>
	/// </remarks>
	public static JlRegion operator *(JlRegion region, double factor)
	{
		return region.ZoomRegion(factor, factor);
	}

	/// <summary>按各向同性比例缩放区域（标量在左的对称写法），返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>*(JlRegion, double)</c> 等价，仅顺序相反，内部同样 <c>region.ZoomRegion(factor, factor)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 40.0);
	///   JlRegion big = 2.0 * r;
	///   r.Dispose();
	///   big.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>语义与坑同正向缩放重载。</para>
	/// </remarks>
	public static JlRegion operator *(double factor, JlRegion region)
	{
		return region.ZoomRegion(factor, factor);
	}

	/// <summary>关于点 (row=0, column=0) 作点对称翻折区域，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.TransposeRegion(0, 0)</c>，把区域绕像素坐标 (0,0) 做 180° 点对称（中心对称）。</para>
	///   <para><b>约束或前提</b>别望文生义：一元 <c>-</c> 不是"取负/求反"，而是翻折；结果常落在图像外（坐标为负），一般需再平移回正坐标区。</para>
	///   <para><b>与相邻算子的取舍</b>要按任意基点翻折直接调 <c>TransposeRegion(row, column)</c>；要水平/垂直镜像是另一种对称轴。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 40.0);
	///   JlRegion flipped = -r;
	///   r.Dispose();
	///   flipped.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>与 <c>-(JlRegion,double)</c>（腐蚀）、<c>-(JlRegion,JlRegion)</c>（Minkowski 减）是三个不同重载，靠操作数个数/类型区分。</para>
	/// </remarks>
	public static JlRegion operator -(JlRegion region)
	{
		return region.TransposeRegion(0, 0);
	}

	/// <summary>把 XLD 轮廓隐式转换为填充区域（<c>GenRegionContourXld("filled")</c>）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>用轮廓围成的内部像素生成区域，等价 <c>xld.GenRegionContourXld("filled")</c>。</para>
	///   <para><b>约束或前提</b>轮廓应为闭合轮廓；开口轮廓的填充以原生补边规则为准 [待实测]。边界走像素中心，填回的面积与原区域在半像素意义下可能有系统性偏差。</para>
	///   <para><b>与相邻算子的取舍</b>多边形版是 <c>implicit operator JlRegion(JlXLDPoly)</c>；本库没有 Contregion。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion rect = new JlRegion(10.0, 10.0, 40.0, 50.0);
	///   JlXLDCont border = new JlXLDCont(rect, "border");
	///   JlRegion filled = border;
	///   rect.Dispose();
	///   border.Dispose();
	///   filled.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>这是隐式转换，易在重载选择或方法传参时意外触发（区域与 XLD 之间存在双向隐式转换）。</para>
	/// </remarks>
	public static implicit operator JlRegion(JlXLDCont xld)
	{
		return xld.GenRegionContourXld("filled");
	}

	/// <summary>把 XLD 多边形隐式转换为填充区域（<c>GenRegionPolygonXld("filled")</c>）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>用多边形围成的内部像素生成区域，等价 <c>xld.GenRegionPolygonXld("filled")</c>。</para>
	///   <para><b>约束或前提</b>多边形顶点须能围成闭合区域；开放折线由原生闭合后再填充 [待实测]。填充落在像素栅格上，与亚像素多边形面积会有偏差。</para>
	///   <para><b>与相邻算子的取舍</b>输入是普通轮廓（非多边形）时用 <c>implicit operator JlRegion(JlXLDCont)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion rect = new JlRegion(10.0, 10.0, 40.0, 50.0);
	///   JlXLDCont border = new JlXLDCont(rect, "border");
	///   JlXLDPoly poly = border.GenPolygonsXld("ramer", 2.0);
	///   JlRegion filled = poly;
	///   rect.Dispose();
	///   border.Dispose();
	///   poly.Dispose();
	///   filled.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>隐式转换，注意与 XLD/区域间的其它隐式转换共同造成重载歧义。</para>
	/// </remarks>
	public static implicit operator JlRegion(JlXLDPoly xld)
	{
		return xld.GenRegionPolygonXld("filled");
	}

	/// <summary>把区域隐式转换为表示其边界的 XLD 轮廓（<c>GenContourRegionXld("border")</c>）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>region.GenContourRegionXld("border")</c>，得到沿区域边界的轮廓，返回新句柄。</para>
	///   <para><b>约束或前提</b>边界轮廓走的是边界像素中心、非像素外沿；带孔区域的孔边界也会各成一条轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>要"填充/边缘"等其它模式用 <c>GenContourRegionXld(mode)</c> 显式传 mode；本隐式转换固定用 "border"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 40.0, 50.0);
	///   JlXLDCont border = r;
	///   r.Dispose();
	///   border.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；与 <c>implicit operator JlRegion(JlXLDCont)</c> 构成双向隐式转换，链式传参时易触发意外往返转换。</para>
	/// </remarks>
	public static implicit operator JlXLDCont(JlRegion region)
	{
		return region.GenContourRegionXld("border");
	}

	/// <summary>
	///   由区域生成 XLD 轮廓，返回新句柄。
	/// </summary>
	/// <param name="mode">轮廓生成模式。Default: "border"</param>
	/// <returns>新 <see cref="JlXLDCont"/> 句柄（<c>LoadNew</c>），用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_contour_region_xld</c>（id 70），把区域边界按 <c>mode</c> 指定的方式转成轮廓。</para>
	///   <para><b>约束或前提</b><c>mode</c> 为字符串（如 "border" 沿边界、"filled" 填充边界等），非托管层校验，非法规格由原生报错 [待实测：mode 取值集合]。</para>
	///   <para><b>与相邻算子的取舍</b>想把轮廓转回填充区域用 <c>GenRegionContourXld("filled")</c>；要骨架转轮廓用 <c>GenContoursSkeletonXld</c>。</para>
	///   <para><b>参数取向</b>mode 走 <c>StoreS</c>；输出走 <c>InitOCT</c>+<c>JlXLDCont.LoadNew</c>，是新句柄而非原地改写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 40.0, 50.0);
	///   JlXLDCont c = r.GenContourRegionXld("border");
	///   r.Dispose();
	///   c.Dispose();
	///   </code>
	///   <para><b>相关算子</b>SelectContoursXld、FitLineContourXld。</para>
	/// </remarks>
	public JlXLDCont GenContourRegionXld(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(70);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把骨架区域转成 XLD 轮廓，返回新句柄。
	/// </summary>
	/// <param name="length">轮廓至少需包含的点数，短于此者被滤除。Default: 1</param>
	/// <param name="mode">轮廓过滤模式。Default: "filter"</param>
	/// <returns>新 <see cref="JlXLDCont"/> 句柄（<c>LoadNew</c>），用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 <c>gen_contours_skeleton_xld</c>（id 73），把 1 像素宽的骨架区域跟踪为轮廓，并按 <c>length</c> 过滤太短的段。</para>
	///   <para><b>约束或前提</b>输入应为骨架化后的细区域；喂普通团块区域会得到贴合边界的意外轮廓。<c>length</c> 以 <c>StoreI</c> 直写（整数），<c>mode</c> 以 <c>StoreS</c> 传字符串。</para>
	///   <para><b>与相邻算子的取舍</b>普通区域转轮廓用 <c>GenContourRegionXld</c>；本算子专供骨架且带最短长度过滤。</para>
	///   <para><b>参数取向</b>输出走 <c>InitOCT</c>+<c>JlXLDCont.LoadNew</c>，是新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 40.0, 50.0);
	///   JlXLDCont c = r.GenContoursSkeletonXld(1, "filter");
	///   r.Dispose();
	///   c.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>mode</c> 非 "filter" 时的过滤语义以原生为准 [待实测]。</para>
	/// </remarks>
	public JlXLDCont GenContoursSkeletonXld(int length, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(73);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, length);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}





	/// <summary>
	///   从区域中挑选字符形状的区域（文本预处理），返回新句柄。
	/// </summary>
	/// <param name="dotPrint">是否识别点阵字符。Default: "false"</param>
	/// <param name="strokeWidth">字符笔画粗细档位。Default: "medium"</param>
	/// <param name="charWidth">字符宽度（像素）。Default: 25</param>
	/// <param name="charHeight">字符高度（像素）。Default: 25</param>
	/// <param name="punctuation">是否包含标点。Default: "false"</param>
	/// <param name="diacriticMarks">是否存在变音符。Default: "false"</param>
	/// <param name="partitionMethod">相邻字符的切分方法。Default: "none"</param>
	/// <param name="partitionLines">是否按行切分。Default: "false"</param>
	/// <param name="fragmentDistance">碎片合并距离档位。Default: "medium"</param>
	/// <param name="connectFragments">是否连接碎片。Default: "false"</param>
	/// <param name="clutterSizeMax">杂点尺寸上限。Default: 0</param>
	/// <param name="stopAfter">在该处理步骤后提前结束。Default: "completion"</param>
	/// <returns>选中的字符区域，新句柄（<c>LoadNew</c>），用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 416，对输入区域做字符级筛选，输出仍是区域（不含识别文字内容，属区域算子而非 OCR 模型）。</para>
	///   <para><b>约束或前提</b>多个开关是字符串枚举、由 <c>StoreS</c> 传入，非托管校验，非法值由原生报错 [待实测]；<c>charWidth</c>/<c>charHeight</c> 是 <c>JlTuple</c>（<c>Store</c>+<c>UnpinTuple</c>，可给多档）；<c>clutterSizeMax</c> 为整数 <c>StoreI</c>。</para>
	///   <para><b>参数取向</b><c>stopAfter</c> 控制流水线提前退出到某步；要完整筛字符保持默认 "completion"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion src = new JlRegion(10.0, 10.0, 200.0, 200.0);
	///   JlRegion chars = src.SelectCharacters("false", "medium", 25, 25, "false", "false", "none", "false", "medium", "false", 0, "completion");
	///   src.Dispose();
	///   chars.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输出是新句柄；12 个形参顺序必须与签名严格一致，漏写/错位会静默改变语义。</para>
	/// </remarks>
	public JlRegion SelectCharacters(string dotPrint, string strokeWidth, JlTuple charWidth, JlTuple charHeight, string punctuation, string diacriticMarks, string partitionMethod, string partitionLines, string fragmentDistance, string connectFragments, int clutterSizeMax, string stopAfter)
	{
		IntPtr proc = JlNativeApi.PreCall(416);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, dotPrint);
		JlNativeApi.StoreS(proc, 1, strokeWidth);
		JlNativeApi.Store(proc, 2, charWidth);
		JlNativeApi.Store(proc, 3, charHeight);
		JlNativeApi.StoreS(proc, 4, punctuation);
		JlNativeApi.StoreS(proc, 5, diacriticMarks);
		JlNativeApi.StoreS(proc, 6, partitionMethod);
		JlNativeApi.StoreS(proc, 7, partitionLines);
		JlNativeApi.StoreS(proc, 8, fragmentDistance);
		JlNativeApi.StoreS(proc, 9, connectFragments);
		JlNativeApi.StoreI(proc, 10, clutterSizeMax);
		JlNativeApi.StoreS(proc, 11, stopAfter);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(charWidth);
		JlNativeApi.UnpinTuple(charHeight);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   求文本行/段落的倾斜角（slant），返回弧度值。
	/// </summary>
	/// <param name="image">用于测算字符笔画的输入图像。</param>
	/// <param name="charHeight">文本行字符高度（像素）。Default: 25</param>
	/// <param name="slantFrom">倾斜角搜索下限，弧度。Default: -0.523599</param>
	/// <param name="slantTo">倾斜角搜索上限，弧度。Default: 0.523599</param>
	/// <returns>字符倾斜角（弧度），经 <c>LoadNew</c> 按 DOUBLE 装载的 <see cref="JlTuple"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 418，量的是字符笔画相对垂直方向的倾斜，与整行的旋转方向 <c>TextLineOrientation</c> 是不同物理量。</para>
	///   <para><b>约束或前提</b>需要配对的灰度图与"一行文字"的区域；<c>charHeight</c> 要接近真实字高，搜索窗 <c>slantFrom/slantTo</c> 以弧度计（默认约 ±30°），落在窗外则测不到。</para>
	///   <para><b>与相邻算子的取舍</b>要找"整行该转多少度摆正"用 <c>TextLineOrientation</c>（id 419）；本算子给的是斜体倾角。</para>
	///   <para><b>参数取向</b>结果按 DOUBLE 装载（区别于用 <c>StoreI</c> 的整数族）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 200, 200);
	///   JlRegion line = new JlRegion(10.0, 10.0, 190.0, 190.0);
	///   JlTuple slant = line.TextLineSlant(img, 25, -0.523599, 0.523599);
	///   img.Dispose();
	///   line.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 <c>JlTuple</c> 不实现 <c>IDisposable</c>，无需也无法 <c>Dispose</c>。</para>
	/// </remarks>
	public JlTuple TextLineSlant(JlImage image, int charHeight, double slantFrom, double slantTo)
	{
		IntPtr proc = JlNativeApi.PreCall(418);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreI(proc, 0, charHeight);
		JlNativeApi.StoreD(proc, 1, slantFrom);
		JlNativeApi.StoreD(proc, 2, slantTo);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   求文本行/段落的朝向角（整行旋转），返回弧度值。
	/// </summary>
	/// <param name="image">用于测算的输入图像。</param>
	/// <param name="charHeight">文本行字符高度（像素）。Default: 25</param>
	/// <param name="orientationFrom">朝向角搜索下限，弧度。Default: -0.523599</param>
	/// <param name="orientationTo">朝向角搜索上限，弧度。Default: 0.523599</param>
	/// <returns>文本行旋转角（弧度），经 <c>LoadNew</c> 按 DOUBLE 装载的 <see cref="JlTuple"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 419，给出把该行文字摆正所需的旋转角，是"整行方向"；与量笔画斜度的 <c>TextLineSlant</c> 不同。</para>
	///   <para><b>约束或前提</b>需要配对图像与整行区域；搜索窗 <c>orientationFrom/To</c> 以弧度计（默认约 ±30°），真实倾角超出该窗会测不到。</para>
	///   <para><b>与相邻算子的取舍</b>要斜体倾角用 <c>TextLineSlant</c>；拿到本角后通常再走仿射/旋转让文字水平。</para>
	///   <para><b>参数取向</b>结果按 DOUBLE 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 200, 200);
	///   JlRegion line = new JlRegion(10.0, 10.0, 190.0, 190.0);
	///   JlTuple angle = line.TextLineOrientation(img, 25, -0.523599, 0.523599);
	///   img.Dispose();
	///   line.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 <c>JlTuple</c> 不实现 <c>IDisposable</c>。</para>
	/// </remarks>
	public JlTuple TextLineOrientation(JlImage image, int charHeight, double orientationFrom, double orientationTo)
	{
		IntPtr proc = JlNativeApi.PreCall(419);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreI(proc, 0, charHeight);
		JlNativeApi.StoreD(proc, 1, orientationFrom);
		JlNativeApi.StoreD(proc, 2, orientationTo);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}







	/// <summary>
	///   Merge regions from line scan images.
	/// </summary>
	/// <param name="prevRegions">Merged regions from the previous iteration.</param>
	/// <param name="prevMergedRegions">Regions from the previous iteration which could not be merged with the current ones.</param>
	/// <param name="imageHeight">Height of the line scan images. Default: 512</param>
	/// <param name="mergeBorder">Image line of the current image, which touches the previous image. Default: "top"</param>
	/// <param name="maxImagesRegion">Maximum number of images for a single region. Default: 3</param>
	/// <returns>Current regions, merged with old ones where applicable.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>线扫描接图算子（原生 id 467）：把本帧区域 <c>this</c> 与上一帧残留
	///   区域 <c>prevRegions</c> 按"上下帧相接"的关系缝合；跨帧未缝上的部分从
	///   <c>prevMergedRegions</c> 输出，留待下一帧继续处理。返回值是"当前帧缝完"的新句柄，
	///   两个输入句柄均不被改写。</para>
	///   <para><b>约束与前提</b><c>imageHeight</c> 必须是实际线扫拼接周期内的帧高，
	///   <c>mergeBorder</c> 指明当前帧与上一帧相贴的是哪一行（默认 "top"，即物料自下向上走）；
	///   取错方向会把不该缝的两帧缝在一起。<c>maxImagesRegion</c> 限制单区域允许跨越的帧数，
	///   超限的区域会被拆出而不是继续拼接。其他字面量取值 [待实测]。</para>
	///   <para><b>参数取向</b>两个输出：返回值 + <c>out</c> 各一个新句柄，调用处必须写
	///   <c>out</c>，且两者都要各自 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   // 上一帧残段在画幅顶部，当前帧同一条料带出现在画幅底部
	///   JlRegion prev = new JlRegion(0.0, 100.0, 40.0, 160.0);
	///   JlRegion curr = new JlRegion(500.0, 100.0, 540.0, 160.0);
	///   using JlRegion merged = curr.MergeRegionsLineScan(prev, out JlRegion leftover, 540, "top", 3);
	///   prev.Dispose();
	///   curr.Dispose();
	///   leftover.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>缝合按像素归属计算，帧间灰度漂移不在此算子职责内 [待实测]；
	///   <c>prevRegions</c> 保活到调用结束（<c>GC.KeepAlive</c>）。</para>
	/// </remarks>
	public JlRegion MergeRegionsLineScan(JlRegion prevRegions, out JlRegion prevMergedRegions, int imageHeight, string mergeBorder, int maxImagesRegion)
	{
		IntPtr proc = JlNativeApi.PreCall(467);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, prevRegions);
		JlNativeApi.StoreI(proc, 0, imageHeight);
		JlNativeApi.StoreS(proc, 1, mergeBorder);
		JlNativeApi.StoreI(proc, 2, maxImagesRegion);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out prevMergedRegions);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(prevRegions);
		return obj;
	}

	/// <summary>
	///   Partition a region into rectangles of approximately equal size.
	/// </summary>
	/// <param name="width">Width of the individual rectangles.</param>
	/// <param name="height">Height of the individual rectangles.</param>
	/// <returns>Partitioned region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域按固定 <c>width</c>×<c>height</c> 的矩形瓦片网格切开（原生 id 468，
	///   两个参数经 <c>StoreD</c> 直写）：一个连通块会被切成多块，结果句柄里装的是全部瓦片，
	///   元素数用 <c>CountObj()</c> 查询。输入句柄不被改写。</para>
	///   <para><b>与相邻算子的取舍</b><c>Connection()</c> 按连通性拆，粘连目标拆不开；本算子不看形状
	///   只看网格，适合把超大区域切块做分批量测或并行处理。字符粘连且高度相近时改用
	///   <c>PartitionDynamic</c>（id 469），它按笔画间窄缝下刀，不会把笔画拦腰切断。</para>
	///   <para><b>约束</b>瓦片网格的原点位置托管层未注明 [待实测]；切完的面积、矩都是碎片值，
	///   与整件不可混用，后续要按瓦片重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("sheet.hobj");
	///   JlRegion part = image.Threshold(100.0, 255.0);
	///   using JlRegion tiles = part.PartitionRectangle(64.0, 64.0);
	///   int n = tiles.CountObj();
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；瓦片数多时逐元素处理的耗时按 <c>n</c> 线性增长。</para>
	/// </remarks>
	public JlRegion PartitionRectangle(double width, double height)
	{
		IntPtr proc = JlNativeApi.PreCall(468);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, width);
		JlNativeApi.StoreD(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Partition a region horizontally at positions of small vertical extent.
	/// </summary>
	/// <param name="distance">Approximate width of the resulting region parts.</param>
	/// <param name="percent">Maximum percental shift of the split position. Default: 20</param>
	/// <returns>Partitioned region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>沿水平方向、在区域纵向跨度小的位置（笔画间隙）下刀，把一行文本一类的
	///   粘连区域拆成单字符（原生 id 469）。<c>distance</c> 给出期望碎片宽度的近似值，
	///   <c>percent</c> 允许刀位左右浮动该百分比；两者都经 <c>StoreD</c> 直写。</para>
	///   <para><b>与相邻算子的取舍</b>固定网格切割用 <c>PartitionRectangle</c>（id 468）；
	///   笔画粗细不均或字距不定时用本算子，刀位跟着字形走。切完仍是粘连时，调大 percent
	///   或改 distance 重新切，而不是回头补 <c>Connection</c>——本算子拆出的碎片在结果句柄里
	///   已是分立的元素。</para>
	///   <para><b>约束</b>对纵向处处饱满的块（实心矩形条）找不到窄缝，效果 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("labels.hobj");
	///   JlRegion line = image.Threshold(0.0, 100.0);
	///   using JlRegion chars = line.PartitionDynamic(20.0, 20.0);
	///   int n = chars.CountObj();
	///   line.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；碎片序号即后续 <c>SelectObj</c> 的索引基准，
	///   切割位置一变序号全部错位。</para>
	/// </remarks>
	public JlRegion PartitionDynamic(double distance, double percent)
	{
		IntPtr proc = JlNativeApi.PreCall(469);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, distance);
		JlNativeApi.StoreD(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert regions to a label image.
	/// </summary>
	/// <param name="type">Pixel type of the result image. Default: "int2"</param>
	/// <param name="width">Width of the image to be generated. Default: 512</param>
	/// <param name="height">Height of the image to be generated. Default: 512</param>
	/// <returns>Result image of dimension Width * Height containing the converted regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域元组逐元素填入一张标签图：第 i 个元素的像素值为 i，背景为 0
	///   （原生 id 470，输出经 <c>JlImage.LoadNew</c> 装入新图像句柄）。<c>type</c> 决定像素类型、
	///   <c>width</c>/<c>height</c> 决定画幅，全部经 <c>StoreS</c>/<c>StoreI</c> 直写。</para>
	///   <para><b>与相邻算子的取舍</b>只要前景/背景两级时用 <c>RegionToBin</c>（id 471）并自定
	///   前景值；本算子保留"哪个像素属于哪个对象"，供连通域级的图像后处理或导出标签用。</para>
	///   <para><b>约束</b>元素数超过 <c>type</c> 表示范围时的截断行为 [待实测]；画幅取多少是调用者的
	///   责任，超出 <c>width</c>×<c>height</c> 的区域像素去向 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0).Connection();
	///   using JlImage label = blobs.RegionToLabel("int2", 640, 480);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新图像句柄，输入区域不被改写。</para>
	/// </remarks>
	public JlImage RegionToLabel(string type, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(470);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Convert a region into a binary byte-image.
	/// </summary>
	/// <param name="foregroundGray">Gray value in which the regions are displayed. Default: 255</param>
	/// <param name="backgroundGray">Gray value in which the background is displayed. Default: 0</param>
	/// <param name="width">Width of the image to be generated. Default: 512</param>
	/// <param name="height">Height of the image to be generated. Default: 512</param>
	/// <returns>Result image of dimension Width * Height containing the converted regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域转成只有两个灰度值的图像：区域像素为 <c>foregroundGray</c>、
	///   其余为 <c>backgroundGray</c>（原生 id 471，两个灰度值经 <c>StoreI</c> 直写，必须是整数）。
	///   画幅由 <c>width</c>/<c>height</c> 给定，没有 type 参数，输出像素类型固定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要区分对象用 <c>RegionToLabel</c>（id 470）；要把区域并成
	///   单一前景先 <c>Union1</c> 再转。做"二值掩膜图"用本算子最直接。</para>
	///   <para><b>约束</b>多个区域元素重叠时前景值不会叠加或区分；前景值与背景值相同时结果
	///   是整幅平灰，注意传参对调。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pcb.hobj");
	///   JlRegion pads = image.Threshold(128.0, 255.0);
	///   using JlImage mask = pads.RegionToBin(255, 0, 640, 480);
	///   pads.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新图像句柄；区域超出画幅部分的裁剪行为 [待实测]。</para>
	/// </remarks>
	public JlImage RegionToBin(int foregroundGray, int backgroundGray, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(471);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, foregroundGray);
		JlNativeApi.StoreI(proc, 1, backgroundGray);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   两个区域求并，返回一个新区域：结果是两块轮廓的合体，中间不再有分界。
	/// </summary>
	/// <param name="region2">要与本句柄求并的区域（本身可以是区域元组，先整体看待再并入）。</param>
	/// <returns>并集的新句柄；两个输入句柄都不被修改。</returns>
	/// <remarks>
	///   <para><b>与 <c>union1</c> 的分工</b>本算子只做"1 对 1"（两侧各算一个输入整体）。
	///   要把一个区域元组折叠成一块用 <c>Union1()</c>；逐对合并多块要循环调用本方法，
	///   每次产生一个新句柄，旧句柄记得 Dispose，否则原生内存只涨不落。</para>
	///   <para><b>并完了还是 N 块吗</b>几何相交的两块并完后是 1 块；只是挨近但不共像素的两块
	///   并完仍是 1 个句柄内的 2 个连通分量，后面 <c>Connection()</c> 还会拆回去。
	///   "并完是否粘成一块"无法由签名判定 [待实测]。</para>
	///   <para><b>空区域</b>任一侧无像素时结果的形状 [待实测]。</para>
	///   <para><b>重载选择</b>无重载。原生 id 472：两个句柄分别 <c>Store</c> 到控制参数 1、2，
	///   结果由 <c>LoadNew</c> 装入新对象，<c>GC.KeepAlive</c> 同时保活两者。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pcb.hobj");
	///   JlRegion pads = image.Threshold(128.0, 255.0);
	///   JlRegion probe = new JlRegion(50.0, 40.0, 200.0, 260.0);
	///   using JlRegion both = pads.Union2(probe);
	///   pads.Dispose();
	///   probe.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；<c>region2</c> 在本调用返回前不得 Dispose。</para>
	/// </remarks>
	public JlRegion Union2(JlRegion region2)
	{
		IntPtr proc = JlNativeApi.PreCall(472);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region2);
		return obj;
	}

	/// <summary>
	///   把句柄内的全部区域折叠成一个并集区域（N 进 1 出）。
	/// </summary>
	/// <returns>折叠后的单区域新句柄；调用者句柄不变。</returns>
	/// <remarks>
	///   <para><b>何时用它</b>要对所有目标的总体做一件事时先 <c>Union1</c> 再算一次：
	///   总覆盖范围 <c>SmallestRectangle1</c>、总轮廓 <c>Boundary</c>、整体补 ROI 等。
	///   <c>Union2</c> 一次只能并两个，把元组里 N 块挨个并起来必须循环 <c>Union2</c> 或用本成员。</para>
	///   <para><b>折叠是单向的</b>并集抹掉对象间边界，之后要再 <c>Connection</c> 也回不到原来的
	///   分块——保留原句柄另作它用。</para>
	///   <para><b>重载选择</b>无重载。原生 id 473：<c>Store</c> 进、<c>LoadNew</c> 出各 1 个 iconic 参数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("sheet.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0);
	///   JlRegion passable = blobs.SelectShape("area", "and", 200.0, 99999999.0);
	///   using JlRegion all = passable.Union1();
	///   blobs.Dispose();
	///   passable.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄里是 1 个区域（<c>CountObj()</c> 可用于核对元素数）。</para>
	/// </remarks>
	public JlRegion Union1()
	{
		IntPtr proc = JlNativeApi.PreCall(473);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the closest-point transformation of a region.
	/// </summary>
	/// <param name="closestPoints">Image containing the coordinates of the closest points.</param>
	/// <param name="metric">Type of metric to be used for the closest-point transformation. Default: "city-block"</param>
	/// <param name="foreground">Compute the distance for pixels inside ('true') or outside ('false') the input region. Default: "true"</param>
	/// <param name="closestPointMode">Mode in which the coordinates of the closest points are returned. Default: "absolute"</param>
	/// <param name="width">Width of the output images. Default: 640</param>
	/// <param name="height">Height of the output images. Default: 480</param>
	/// <returns>Image containing the distance information.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>一次产出两张图（原生 id 474，两个输出各自 <c>JlImage.LoadNew</c>）：
	///   返回值是距离图，<c>out</c> 的 <c>closestPoints</c> 是每个像素到参考侧最近点的坐标图；
	///   <c>closestPointMode='absolute'</c> 时坐标为绝对位置（其余取值 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>只要距离不要最近点坐标时用 <c>DistanceTransform</c>（id 475），
	///   省一张输出。最近点坐标可直接喂给标定/对位类计算（判断"该往哪走"），距离图只能判断"差多少"。</para>
	///   <para><b>约束</b><c>foreground='true'</c> 量区域内像素到边界的距离，<c>'false'</c> 量区域外；
	///   两图尺寸相同且必须用同一 <c>width</c>/<c>height</c> 解释，坐标图按双通道读取 [待实测]。</para>
	///   <para><b>参数取向</b>两个输出：返回值 + <c>out</c>，调用处 <c>out</c> 不可省，两者都要 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlRegion(100.0, 80.0, 260.0, 400.0);
	///   using JlImage dist = part.ClosestPointTransform(out JlImage pts, "city-block", "true", "absolute", 512, 512);
	///   pts.Dispose();
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>metric 可选字面量清单托管层无法枚举 [待实测]；输入不被改写。</para>
	/// </remarks>
	public JlImage ClosestPointTransform(out JlImage closestPoints, string metric, string foreground, string closestPointMode, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(474);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, metric);
		JlNativeApi.StoreS(proc, 1, foreground);
		JlNativeApi.StoreS(proc, 2, closestPointMode);
		JlNativeApi.StoreI(proc, 3, width);
		JlNativeApi.StoreI(proc, 4, height);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		err = JlImage.LoadNew(proc, 2, err, out closestPoints);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对区域做距离变换，输出一张每个像素携带"到参考侧最短距离"的图像。
	/// </summary>
	/// <param name="metric">距离度量类型。Default: "city-block"</param>
	/// <param name="foreground">对区域内（'true'）还是区域外（'false'）像素计算距离。Default: "true"</param>
	/// <param name="width">输出图像宽度。Default: 640</param>
	/// <param name="height">输出图像高度。Default: 480</param>
	/// <returns>距离图的<strong>新句柄</strong>（<c>JlImage</c>），不是区域。</returns>
	/// <remarks>
	///   <para><b>两个方向对应两类任务</b><c>foreground='true'</c>量"每个前景像素离边界多远"，
	///   给骨架化、最大内径类算法供数据；<c>'false'</c>量背景到目标的距离，做膨胀预算
	///   （"要膨胀多少圈才能碰到目标"）或间距检查前算一次，比反复 <c>DilationCircle</c> 试便宜。</para>
	///   <para><b>与 <c>ClosestPointTransform</c> 的取舍</b>后者（原生 id 474）在同一距离图上额外给出
	///   最近点坐标图；只要距离用本算子（id 475），省一个输出。</para>
	///   <para><b>画幅</b><c>width</c>/<c>height</c> 是独立入参，库不会从区域自动推尺寸；
	///   metric 除默认值外的可选字面量清单在托管层无法枚举 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("coat.hobj");
	///   JlRegion defect = image.Threshold(200.0, 255.0);
	///   using JlImage dist = defect.DistanceTransform("city-block", "false", 640, 480);
	///   defect.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>空区域的距离图内容 [待实测]。返回新句柄，输入不被修改。</para>
	/// </remarks>
	public JlImage DistanceTransform(string metric, string foreground, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(475);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, metric);
		JlNativeApi.StoreS(proc, 1, foreground);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the skeleton of a region.
	/// </summary>
	/// <returns>Resulting skeleton.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域细化成 1 像素宽的骨架（原生 id 476，<c>Store</c> 进、
	///   <c>LoadNew</c> 出新句柄），保持连通性与端点拓扑，笔画交叉处仍保留分叉。</para>
	///   <para><b>与相邻算子的取舍</b><c>ThinningGolay</c>/<c>Thinning</c> 族（id 720/721）需要自己选
	///   结构元素并控制迭代次数，适合"细化到某个形状"的场合；只要中轴线用本算子，一次到位。
	///   骨架上的毛刺用 <c>Pruning</c>（id 714）剪，交叉点与端点用 <c>JunctionsSkeleton</c>（id 482）取。</para>
	///   <para><b>坑：特征必须重算</b>骨架像素数远小于原区域，先取好的面积、矩、轮廓类特征值
	///   在骨架上全部作废，不能沿用；骨架上算出的"圆度/矩形度"也没有意义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("crack.hobj");
	///   JlRegion crack = image.Threshold(150.0, 255.0);
	///   using JlRegion skel = crack.Skeleton();
	///   crack.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果句柄内元素数与输入 [待实测]（是否逐对象保持对齐）。</para>
	/// </remarks>
	public JlRegion Skeleton()
	{
		IntPtr proc = JlNativeApi.PreCall(476);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply a projective transformation to a region.
	/// </summary>
	/// <param name="homMat2D">Homogeneous projective transformation matrix.</param>
	/// <param name="interpolation">Interpolation method for the transformation. Default: "bilinear"</param>
	/// <returns>Output regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对区域施加投影（单应）变换（原生 id 477）。矩阵按控制参数 0 以元组
	///   形式 <c>Store</c> 传入、调用后 <c>UnpinTuple</c>；<c>interpolation</c> 经 <c>StoreS</c> 透传字符串。</para>
	///   <para><b>与相邻算子的取舍</b>仿射保持平行性与面积比，投影允许近大远小；相机斜拍平面的
	///   校正用本算子，机械平移旋转用 <c>AffineTransRegion</c>（id 478）。区域是像素集合，投影后
	///   边缘按栅格重采样，测量建议先变换 XLD 轮廓再转区域 [待实测：本库对应链路]。</para>
	///   <para><b>约束</b>落在消失线附近的区域会被拉伸到无穷，结果异常；矩阵需与坐标系一致
	///   （row=y 向下、column=x 向右）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlRegion(100.0, 80.0, 260.0, 400.0);
	///   JlHomMat2D h = new JlHomMat2D();
	///   h.VectorAngleToRigid(100.0, 100.0, 0.0, 120.0, 140.0, 0.2618);
	///   using JlRegion moved = part.ProjectiveTransRegion(h, "bilinear");
	///   part.Dispose();
	///   // JlHomMat2D 派生自 JlData、不实现 IDisposable：矩阵对象不需要也不能 Dispose
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；<c>JlHomMat2D</c> 与 <c>JlTuple</c> 是 <c>JlData</c>
	///   系的托管壳、不实现 <c>IDisposable</c>，不要对它们写 <c>Dispose</c> 或 <c>using</c>。
	///   除默认值外 interpolation 可用字面量 [待实测]。</para>
	/// </remarks>
	public JlRegion ProjectiveTransRegion(JlHomMat2D homMat2D, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(477);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Apply an arbitrary affine 2D transformation to regions.
	/// </summary>
	/// <param name="homMat2D">Input transformation matrix.</param>
	/// <param name="interpolate">Should the transformation be done using interpolation? Default: "nearest_neighbor"</param>
	/// <returns>Transformed output region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对区域施加任意 2D 仿射变换（原生 id 478，矩阵 <c>Store</c>+<c>UnpinTuple</c>，
	///   <c>interpolate</c> 字符串透传）。<c>new JlHomMat2D()</c> 配 <c>VectorAngleToRigid</c>/
	///   <c>HomMat2dRotate</c> 等构造矩阵即可。</para>
	///   <para><b>与相邻算子的取舍</b>整数平移用 <c>MoveRegion</c>（id 481）、双轴缩放用
	///   <c>ZoomRegion</c>（id 480）更快也更不易错；带旋转、亚像素位移或任意线性变换才用本算子。
	///   透视需求见 <c>ProjectiveTransRegion</c>（id 477）。</para>
	///   <para><b>坑</b>变换后的面积≈原面积乘矩阵行列式绝对值，但栅格化误差不保证精确相等：
	///   旋转之后旧的特征值一律重算。<c>'nearest_neighbor'</c> 与插值选项的边缘像素数不同 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("parts.hobj");
	///   JlRegion part = image.Threshold(80.0, 255.0);
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorAngleToRigid(10.0, 10.0, 0.0, 30.0, 40.0, 0.5236);
	///   using JlRegion moved = part.AffineTransRegion(m, "nearest_neighbor");
	///   part.Dispose();
	///   // JlHomMat2D 派生自 JlData、不实现 IDisposable：矩阵对象不需要也不能 Dispose
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose，输入区域不被改写；<c>JlHomMat2D</c> 属 <c>JlData</c>
	///   系、不实现 <c>IDisposable</c>，对它写 <c>Dispose</c>/<c>using</c> 会编译失败。</para>
	/// </remarks>
	public JlRegion AffineTransRegion(JlHomMat2D homMat2D, string interpolate)
	{
		IntPtr proc = JlNativeApi.PreCall(478);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolate);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Reflect a region about an axis.
	/// </summary>
	/// <param name="mode">Axis of symmetry. Default: "row"</param>
	/// <param name="widthHeight">Twice the coordinate of the axis of symmetry. Default: 512</param>
	/// <returns>Reflected region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>关于一条轴镜像区域（原生 id 479）：<c>mode='row'</c> 为水平轴、
	///   <c>'column'</c> 为垂直轴 [待实测：其余字面量]；轴的位置在坐标 <c>widthHeight/2</c> 处——
	///   该参数是"轴坐标的两倍"，不是图像宽高，想让轴过画幅中线才传 2×行高（或 2×列宽）。</para>
	///   <para><b>与相邻算子的取舍</b>点对称（等价于旋转 180°）用 <c>TransposeRegion</c>（id 718）；
	///   任意轴反射用 <c>JlHomMat2D</c> 的反射矩阵走 <c>AffineTransRegion</c>。</para>
	///   <para><b>坑</b>镜像翻转手性：phi、方向性轮廓走向一类的量符号会反，面积/矩不变；
	///   变换前后的方向类特征不可直接对比。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlRegion(20.0, 60.0, 100.0, 200.0);
	///   using JlRegion flip = part.MirrorRegion("row", 512);   // 轴在 row=256
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；镜像到画幅外不落空，区域坐标可越界。</para>
	/// </remarks>
	public JlRegion MirrorRegion(string mode, int widthHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(479);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, widthHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Zoom a region.
	/// </summary>
	/// <param name="scaleWidth">Scale factor in x-direction. Default: 2.0</param>
	/// <param name="scaleHeight">Scale factor in y-direction. Default: 2.0</param>
	/// <returns>Zoomed region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>双轴缩放区域（原生 id 480，两系数经 <c>StoreD</c> 直写）：坐标按
	///   <c>scaleWidth</c>×<c>scaleHeight</c> 缩放后重新栅格化。缩放中心托管层未注明 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>需要以任意点为基准缩放、或与旋转复合时用
	///   <c>AffineTransRegion</c>（id 478）配 <c>HomMat2dScale</c>；纯等比拉伸本算子最省事。
	///   系数相等与否影响形状，但面积一律变，旧特征值作废。</para>
	///   <para><b>坑</b>放大后的斜边出现台阶、缩小时的细部可能整段消失——缩放是重建像素集合，
	///   不是无损变换；系数 ≤ 0 的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlRegion(20.0, 60.0, 100.0, 200.0);
	///   using JlRegion big = part.ZoomRegion(2.0, 2.0);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion ZoomRegion(double scaleWidth, double scaleHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(480);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, scaleWidth);
		JlNativeApi.StoreD(proc, 1, scaleHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Translate a region.
	/// </summary>
	/// <param name="row">Row coordinate of the vector by which the region is to be moved. Default: 30</param>
	/// <param name="column">Row coordinate of the vector by which the region is to be moved. Default: 30</param>
	/// <returns>Translated region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按整数向量平移区域（原生 id 481，两个参数经 <c>StoreI</c> 直写，
	///   只能是整数像素）：<c>row</c> 沿 y 向下为正、<c>column</c> 沿 x 向右为正。</para>
	///   <para><b>与相邻算子的取舍</b>亚像素位移或带旋转的搬动用 <c>AffineTransRegion</c>（id 478）；
	///   整数格平移用本算子既快又不引入重采样误差。点对称翻转另见 <c>TransposeRegion</c>（id 718）。</para>
	///   <para><b>坑</b>形状、面积、相对矩都不变，只有绝对位置（质心、包框、轮廓点）变——
	///   按绝对坐标做的 ROI 判断在平移后要重新取；平移出画幅不会报错，与图像求交时才暴露。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 90.0);
	///   using JlRegion shifted = roi.MoveRegion(30, -20);   // 下移 30、左移 20
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；元组输入逐对象各移各的。</para>
	/// </remarks>
	public JlRegion MoveRegion(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(481);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Find junctions and end points in a skeleton.
	/// </summary>
	/// <param name="juncPoints">Extracted junctions.</param>
	/// <returns>Extracted end points.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>从骨架里挑出两类点（原生 id 482，两个 iconic 输出各 <c>LoadNew</c>）：
	///   返回值是端点集合，<c>out</c> 的 <c>juncPoints</c> 是分叉点集合。</para>
	///   <para><b>前提</b>输入应是 <c>Skeleton</c>（id 476）或 Golay 细化后的 1 像素宽区域；
	///   拿实心块直接调用，输出的"分叉"没有意义。</para>
	///   <para><b>用途</b>端点+分叉点把骨架切成无分支段的基础：拿到这两类点后配合
	///   <c>SplitSkeletonRegion</c>（id 502）或 <c>SplitSkeletonLines</c>（id 501）逐段处理笔画、
	///   路网、焊缝。</para>
	///   <para><b>参数取向</b>返回值与 <c>out</c> 各一个新句柄，调用处必须写 <c>out</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("weld.hobj");
	///   JlRegion weld = image.Threshold(140.0, 255.0);
	///   using JlRegion skel = weld.Skeleton();
	///   using JlRegion ends = skel.JunctionsSkeleton(out JlRegion juncs);
	///   weld.Dispose();
	///   juncs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>毛刺多时端点数会爆，先 <c>Pruning</c>（id 714）再取点。</para>
	/// </remarks>
	public JlRegion JunctionsSkeleton(out JlRegion juncPoints)
	{
		IntPtr proc = JlNativeApi.PreCall(482);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out juncPoints);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   两个区域求交：只保留公共像素，常用来把目标裁进 ROI。
	/// </summary>
	/// <param name="region2">参与求交的区域（另一侧是本句柄）。</param>
	/// <returns>交集的新句柄；两个输入句柄都不被修改。</returns>
	/// <remarks>
	///   <para><b>约束</b>原生 id 483，<c>LoadNew</c> 出参。交集是有界区域的普通运算，
	///   不像 <c>Complement</c> 那样牵涉画幅；两输入完全不重叠时结果为空区域，
	///   对空区域继续 <c>AreaCenter</c> 等量测的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"减去"用 <c>Difference</c>；要"两边不一样的部分"用
	///   <c>SymmDifference</c>（id 492）。求交后再做元组运算时，先 <c>Union1</c> 再交一次
	///   通常比逐元素交 N 次省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("parts.hobj");
	///   JlRegion all = image.Threshold(80.0, 255.0);
	///   JlRegion roi = new JlRegion(30.0, 40.0, 220.0, 300.0);
	///   using JlRegion inside = all.Intersection(roi);
	///   all.Dispose();
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>region2</c> 由 <c>GC.KeepAlive</c> 保活到调用结束，返回后可自行 Dispose。</para>
	/// </remarks>
	public JlRegion Intersection(JlRegion region2)
	{
		IntPtr proc = JlNativeApi.PreCall(483);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region2);
		return obj;
	}

	/// <summary>
	///   Partition the image plane using given regions.
	/// </summary>
	/// <param name="mode">Mode of operation. Default: "mixed"</param>
	/// <returns>Output region containing the separating lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把"区域之间"的缝隙生成为区域（原生 id 484，<c>mode</c> 字符串透传，
	///   默认 "mixed"，其余取值 [待实测]）：输入是区域元组，输出句柄装着分隔带/间隙区域。</para>
	///   <para><b>用途</b>检查焊盘间距、隔离带宽度这类"间隙本身是对象"的场合；比逐对
	///   <c>SymmDifference</c>+<c>Intersection</c> 手工拼间隙省得多。</para>
	///   <para><b>与相邻算子的取舍</b>要"画幅减去目标"用 <c>Complement</c>（id 494）；
	///   要背景被目标分割成的块用 <c>BackgroundSeg</c>（id 495）；本算子专取目标对之间的区域。</para>
	///   <para><b>坑</b>间隙区域数量与配对方式有关，按序号取元素前先 <c>CountObj()</c> 核对；
	///   输入需先 <c>Connection</c> 拆成离散元素才有"对"可言。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pcb.hobj");
	///   JlRegion pads = image.Threshold(128.0, 255.0).Connection();
	///   using JlRegion gaps = pads.Interjacent("mixed");
	///   pads.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，输入不被改写。</para>
	/// </remarks>
	public JlRegion Interjacent(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(484);
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
	///   填掉区域内全部封闭的洞。
	/// </summary>
	/// <returns>无洞区域的新句柄；调用者句柄不变。</returns>
	/// <remarks>
	///   <para><b>什么时候别全填</b>环状件（垫圈、O 形圈）的孔是特征不是缺陷，
	///   填完面积统计直接翻倍；只填小孔用 <c>FillUpShape</c>。</para>
	///   <para><b>填孔改变拓扑</b>原本隔着孔相邻的两块填完后可能并成一块，
	///   填孔后又想逐个目标比较，要在填完之后再 <c>Connection</c>，顺序反了结果不同。</para>
	///   <para><b>重载选择</b>无重载。原生 id 485，<c>Store</c> 进、<c>LoadNew</c> 出；
	///   区域元组逐个处理，进几个对象出几个对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("cast.hobj");
	///   JlRegion part = image.Threshold(100.0, 255.0);
	///   using JlRegion solid = part.FillUp();
	///   int nBefore = part.Connection().CountObj();
	///   int nAfter = solid.Connection().CountObj();   // 填孔后可能变小
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>贴画幅边缘、没被完全围住的背景是否算洞 [待实测]。</para>
	/// </remarks>
	public JlRegion FillUp()
	{
		IntPtr proc = JlNativeApi.PreCall(485);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   只填充特征落在给定区间内的洞（孔由洞自身特征决定填不填）。
	/// </summary>
	/// <param name="feature">用于判定洞的形特征名。Default: "area"</param>
	/// <param name="min">洞特征的下限。Default: 1.0</param>
	/// <param name="max">洞特征的上限。Default: 100.0</param>
	/// <returns>按条件填孔后的新句柄；调用者句柄不变。</returns>
	/// <remarks>
	///   <para><b>与 <c>FillUp</c> 的取舍</b><c>FillUp</c> 无条件全填；本算子按洞的特征筛，
	///   典型用法是"只消灭小砂眼、保留工艺孔"。区间用 <c>JlTuple</c> 传入时可与
	///   <c>feature</c> 元组并行配对（一次按多个特征区间填）。</para>
	///   <para><b>易错</b>min/max 描述的是<b>洞</b>而不是区域；"area" 之外的可选特征名
	///   托管层没有枚举 [待实测]。</para>
	///   <para><b>重载选择</b>本重载原生 id 486，三个元组参数先 <c>Store</c> 固定、
	///   调用后 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("cast.hobj");
	///   JlRegion part = image.Threshold(100.0, 255.0);
	///   using JlRegion tidy = part.FillUpShape("area", 1.0, 30.0);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，区域元组逐个处理。</para>
	/// </remarks>
	public JlRegion FillUpShape(string feature, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(486);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, feature);
		JlNativeApi.Store(proc, 1, min);
		JlNativeApi.Store(proc, 2, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按洞特征区间填孔（单区间标量版）。
	/// </summary>
	/// <param name="feature">用于判定洞的形特征名。Default: "area"</param>
	/// <param name="min">洞特征的下限。Default: 1.0</param>
	/// <param name="max">洞特征的上限。Default: 100.0</param>
	/// <returns>按条件填孔后的新句柄；调用者句柄不变。</returns>
	/// <remarks>
	///   <para>定义与"填的是洞"的易错点见 <see cref="FillUpShape(string,JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 486）：本重载用 <c>StoreD</c> 直写上下限，没有元组固定
	///   与 <c>UnpinTuple</c>；单一特征、单一区间时应使用本重载。</para>
	/// </remarks>
	public JlRegion FillUpShape(string feature, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(486);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, feature);
		JlNativeApi.StoreD(proc, 1, min);
		JlNativeApi.StoreD(proc, 2, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Fill gaps between regions or split overlapping regions.
	/// </summary>
	/// <param name="forbiddenArea">Regions in which no expansion takes place.</param>
	/// <param name="iterations">Number of iterations. Default: "maximal"</param>
	/// <param name="mode">Expansion mode. Default: "image"</param>
	/// <returns>Expanded or separated regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>让区域生长填充间隙或对重叠区划界（原生 id 487）：每次迭代把区域
	///   外扩一圈，<c>forbiddenArea</c> 内不生长，两块生长前沿相遇处按先到归属划界。
	///   本重载 <c>iterations</c> 以元组 <c>Store</c> 固定、调用后 <c>UnpinTuple</c>，
	///   可传逐对象的圈数；传 "maximal" 直到长满 [待实测：元组版是否同样接受该字面量]。</para>
	///   <para><b>与相邻算子的取舍</b><c>ExpandGray</c>（id 499）在生长时还比较灰度，跨区域灰度
	///   漂移的场景必须用它；纯几何填缝用本算子。<c>mode</c> 默认 "image" 受画幅限制，
	///   其余取值 [待实测]。</para>
	///   <para><b>坑</b>生长结果与输入元素顺序无关但与 <c>forbiddenArea</c> 的内容强相关；
	///   生长后的面积/矩要全部重算。</para>
	///   <para><b>重载选择</b>整数圈数请用 <see cref="ExpandRegion(JlRegion,int,string)"/>（同一 id 487，
	///   <c>StoreI</c> 直写、无元组固定与 <c>UnpinTuple</c>），两重载差别仅此。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("grain.hobj");
	///   JlRegion grains = image.Threshold(90.0, 255.0).Connection();
	///   JlRegion walls = image.Threshold(0.0, 30.0);
	///   using JlRegion filled = grains.ExpandRegion(walls, 20, "image");
	///   grains.Dispose();
	///   walls.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；<c>forbiddenArea</c> 保活到调用结束。</para>
	/// </remarks>
	public JlRegion ExpandRegion(JlRegion forbiddenArea, JlTuple iterations, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(487);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, forbiddenArea);
		JlNativeApi.Store(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Fill gaps between regions or split overlapping regions.
	/// </summary>
	/// <param name="forbiddenArea">Regions in which no expansion takes place.</param>
	/// <param name="iterations">Number of iterations. Default: "maximal"</param>
	/// <param name="mode">Expansion mode. Default: "image"</param>
	/// <returns>Expanded or separated regions.</returns>
	/// <remarks>
	///   <para>生长与划界语义见 <see cref="ExpandRegion(JlRegion,JlTuple,string)"/>。两个重载同一
	///   原生算子（id 487）：本重载 <c>iterations</c> 为整数、<c>StoreI</c> 直写，无元组固定与
	///   <c>UnpinTuple</c>；所有对象统一生长固定圈数时用本重载。</para>
	/// </remarks>
	public JlRegion ExpandRegion(JlRegion forbiddenArea, int iterations, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(487);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, forbiddenArea);
		JlNativeApi.StoreI(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Clip a region relative to its smallest surrounding rectangle.
	/// </summary>
	/// <param name="top">Number of rows clipped at the top. Default: 1</param>
	/// <param name="bottom">Number of rows clipped at the bottom. Default: 1</param>
	/// <param name="left">Number of columns clipped at the left. Default: 1</param>
	/// <param name="right">Number of columns clipped at the right. Default: 1</param>
	/// <returns>Clipped regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以每个对象自己的最小包框为基准裁边（原生 id 488，四个参数
	///   <c>StoreI</c> 直写）：top/bottom 裁行数、left/right 裁列数，逐对象生效。</para>
	///   <para><b>与相邻算子的取舍</b><c>ClipRegion</c>（id 489）用绝对坐标统一裁所有对象；
	///   目标位置不定但都要"去掉包框边缘 N 圈"时用本算子，例如去掉粘连底纹后每个字符外溢
	///   一行的情况。两者都是破坏性裁剪（被裁掉的像素找不回来），先复制再裁。</para>
	///   <para><b>坑</b>裁剪量按对象各自的包框计，包框随 <c>Connection</c> 拆分结果变化；
	///   参数超过包框尺寸时该对象被裁空 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stamp.hobj");
	///   JlRegion stamps = image.Threshold(100.0, 255.0).Connection();
	///   using JlRegion inner = stamps.ClipRegionRel(2, 2, 2, 2);   // 每块各裁掉外圈 2 像素
	///   stamps.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；裁后面积、包框全部改变，特征重算。</para>
	/// </remarks>
	public JlRegion ClipRegionRel(int top, int bottom, int left, int right)
	{
		IntPtr proc = JlNativeApi.PreCall(488);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, top);
		JlNativeApi.StoreI(proc, 1, bottom);
		JlNativeApi.StoreI(proc, 2, left);
		JlNativeApi.StoreI(proc, 3, right);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Clip a region to a rectangle.
	/// </summary>
	/// <param name="row1">Row coordinate of the upper left corner of the rectangle. Default: 0</param>
	/// <param name="column1">Column coordinate of the upper left corner of the rectangle. Default: 0</param>
	/// <param name="row2">Row coordinate of the lower right corner of the rectangle. Default: 256</param>
	/// <param name="column2">Column coordinate of the lower right corner of the rectangle. Default: 256</param>
	/// <returns>Clipped regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域裁进绝对坐标的矩形（原生 id 489，四个角点参数 <c>StoreI</c>
	///   直写，均为整数坐标）：等价于与该矩形 <c>JlRegion(row1, column1, row2, column2)</c>
	///   求 <c>Intersection</c>，但少一次构造。</para>
	///   <para><b>与相邻算子的取舍</b>按各对象自身包框裁边用 <c>ClipRegionRel</c>（id 488）；
	///   固定视场裁掉镜头边缘畸变带用本算子。矩形角点为 row1≤row2、column1≤column2，
	///   反了的行为 [待实测]。</para>
	///   <para><b>坑</b>裁掉的像素不可恢复；坐标系 row 向下、column 向右，角点顺序别按
	///   (x1,y1,x2,y2) 的习惯传。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("film.hobj");
	///   JlRegion blobs = image.Threshold(128.0, 255.0);
	///   using JlRegion usable = blobs.ClipRegion(20, 16, 460, 624);   // 去掉黑边
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；元组输入逐对象裁，元素顺序保持。</para>
	/// </remarks>
	public JlRegion ClipRegion(int row1, int column1, int row2, int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(489);
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
	///   Rank operator for regions.
	/// </summary>
	/// <param name="width">Width of the filter mask. Default: 15</param>
	/// <param name="height">Height of the filter mask. Default: 15</param>
	/// <param name="number">Minimum number of points lying within the filter mask. Default: 70</param>
	/// <returns>Resulting region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b>邻域计数滤波（原生 id 490，三个参数 <c>StoreI</c> 直写）：对每个像素
	///   统计 <c>width</c>×<c>height</c> 掩膜内前景像素数，达到 <c>number</c> 才保留。
	///   <c>number</c> 接近掩膜总像素数时近似腐蚀，取小值时近似闭运算的去噪强度。</para>
	///   <para><b>与相邻算子的取舍</b><c>OpeningCircle</c>/<c>ClosingCircle</c> 有严格的结构元素语义、
	///   保形更好；本算子靠计数阈值调"去噪力度"，对椒盐状孤立像素和细毛刺很有效，
	///   代价是转角会被圆化。</para>
	///   <para><b>约束</b><c>number</c> 大于 <c>width×height</c> 时结果为空；掩膜尺寸为偶数时的
	///   中心对齐方式 [待实测]。滤波后面积、矩全部改变，旧特征值作废。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("cast.hobj");
	///   JlRegion speckled = image.Threshold(120.0, 255.0);
	///   using JlRegion denoised = speckled.RankRegion(9, 9, 40);
	///   speckled.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；大掩膜逐像素计数耗时高，能用 <c>Opening</c> 族解决就不要拉大掩膜。</para>
	/// </remarks>
	public JlRegion RankRegion(int width, int height, int number)
	{
		IntPtr proc = JlNativeApi.PreCall(490);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.StoreI(proc, 2, number);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把一个（或多个）区域按 4-连通拆成独立的连通分量，全部装在返回的同一个句柄里。
	/// </summary>
	/// <returns>装着全部分量的新句柄（区域元组）；用 <c>CountObj()</c> 取分量数。</returns>
	/// <remarks>
	///   <para><b>拆完原句柄还能用吗</b>能。<c>LoadNew</c> 返回新句柄（原生 id 491），
	///   调用者句柄不被改写；分量的存活也不依赖原句柄。</para>
	///   <para><b>返回值不是数组</b>C# 侧仍是单个 <c>JlRegion</c>。取第 i 个分量用本类索引器
	///   <c>blobs[i]</c>（<c>JlRegion</c> 上的索引器返回的正是 <c>JlRegion</c>）；
	///   判空要用 <c>CountObj()</c> 而不是 <c>null</c> 比较。</para>
	///   <para><b>拆分顺序的坑</b><c>SelectShape</c> 类筛选是对句柄内每个对象做的——
	///   必须先 <c>Connection</c> 再筛，否则粘连成一块的目标会以整体面积通过或被整体剔除。
	///   4-连通对角相触算两块；若希望对角相触合为一块，先做 <c>DilationRectangle1(3, 3)</c>
	///   一类的桥接再拆 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   using JlRegion blobs = image.Threshold(100.0, 255.0).Connection();
	///   int n = blobs.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>输入句柄（如 Threshold 的中间结果）用完即 <c>Dispose</c>；
	///   拆分不改变像素，分量总面积与原区域相同（<c>FillUp</c> 之后例外，见其文档）。</para>
	/// </remarks>
	public JlRegion Connection()
	{
		IntPtr proc = JlNativeApi.PreCall(491);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the symmetric difference of two regions.
	/// </summary>
	/// <param name="region2">Input region 2.</param>
	/// <returns>Resulting region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对称差：只保留属于两侧之一的像素（原生 id 492；两侧句柄分别
	///   <c>Store</c> 到控制参数 1、2，结果 <c>LoadNew</c>，与 <c>Difference</c>/id 493 的
	///   单侧减法同构）。</para>
	///   <para><b>与相邻算子的取舍</b><c>Difference</c> 有方向（A−B），对称差不看方向（A△B）：
	///   对比"算法分割结果 vs 人工标注"、模板与实物的差异区域，用对称差一次性拿到漏检+误检；
	///   只要误检用 A−B、只要漏检用 B−A。并集减去对称差即交集，等价 <c>Intersection</c>。</para>
	///   <para><b>坑</b>两侧若一个是原区域一个是腐蚀/膨胀产物，对称差的面积就是形态学改动量——
	///   可用来量化腐蚀深度，但改完特征必须重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion seg = new JlImage("wafer.hobj").Threshold(120.0, 255.0);
	///   JlRegion gold = new JlImage("wafer_gold.hobj").Threshold(120.0, 255.0);
	///   using JlRegion diff = seg.SymmDifference(gold);
	///   int wrong = diff.Area.Length;   // 差异像素规模粗查
	///   seg.Dispose();
	///   gold.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；<c>region2</c> 在返回前不得 Dispose。</para>
	/// </remarks>
	public JlRegion SymmDifference(JlRegion region2)
	{
		IntPtr proc = JlNativeApi.PreCall(492);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region2);
		return obj;
	}

	/// <summary>
	///   从本句柄的区域中挖掉 sub 里全部区域的并集，保留差集。
	/// </summary>
	/// <param name="sub">被减去的一侧；英文原文即"先对 sub 取并、再整体相减"。</param>
	/// <returns>差集的新句柄；两个输入句柄都不被修改。</returns>
	/// <remarks>
	///   <para><b>约束</b>原生 id 493，<c>LoadNew</c> 出参。输入是"区域 减 区域"，
	///   不是对象级的删除（那是 <c>ObjDiff</c>，id 558，删的是句柄内的元素而非像素）。</para>
	///   <para><b>与相邻算子的取舍</b>要保留重叠部分用 <c>Intersection</c>；要对称差用
	///   <c>SymmDifference</c>。用 <c>Difference</c> 挖 ROI 外区域比手动
	///   <c>Complement</c> 再 <c>Intersection</c> 少一次全平面运算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("wafer.hobj");
	///   JlRegion blobs = image.Threshold(120.0, 255.0);
	///   JlRegion mask = new JlRegion(40.0, 40.0, 440.0, 440.0);
	///   using JlRegion noMask = blobs.Difference(mask);   // 掩膜内的部分被挖掉
	///   blobs.Dispose();
	///   mask.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输出对象数遵循上游的对象计数广播规则，托管层无法确定 [待实测]；
	///   <c>sub</c> 侧先取并的细节以英文原文为准。</para>
	/// </remarks>
	public JlRegion Difference(JlRegion sub)
	{
		IntPtr proc = JlNativeApi.PreCall(493);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, sub);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(sub);
		return obj;
	}

	/// <summary>
	///   区域内外语义对调：补集覆盖整个平面，只挖掉输入区域占的像素。
	/// </summary>
	/// <returns>补集的新句柄；调用者句柄不变。</returns>
	/// <remarks>
	///   <para><b>前提：需要一个画幅参考</b>补集按全平面定义、没有天然边界（原生 id 494
	///   不接受任何尺寸参数），所以实际用法必须是"补完立即与 ROI 矩形
	///   <c>Intersection</c>"，让它落回可处理的范围。</para>
	///   <para><b>与相邻算子的取舍</b><c>Difference</c> 需要两个输入，本算子只有一个输入，
	///   "取反 ROI"用它；要"背景被分割成的一块块"用 <c>BackgroundSeg</c>（id 495），
	///   本算子得到的背景是连通的贴边大块 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("film.hobj");
	///   JlRegion defect = image.Threshold(200.0, 255.0);
	///   JlRegion roi = new JlRegion(0.0, 0.0, 479.0, 639.0);
	///   using JlRegion bg = defect.Complement().Intersection(roi);
	///   defect.Dispose();
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>空区域的补集为全平面，直接参与量测有溢出风险 [待实测]。</para>
	/// </remarks>
	public JlRegion Complement()
	{
		IntPtr proc = JlNativeApi.PreCall(494);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Determine the connected components of the background of given regions.
	/// </summary>
	/// <returns>Connected components of the background.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>取输入区域的背景（补集）并按连通性拆块（原生 id 495，一步完成
	///   "补集+拆分"）：返回句柄里是背景被目标分割成的各块区域。</para>
	///   <para><b>与相邻算子的取舍</b>手工等价是 <c>Complement()</c>（id 494）再 <c>Connection()</c>
	///   （id 491），但补集的无限延伸范围如何界定由本算子内部处理 [待实测]；只要"取反 ROI"
	///   不拆分时用 <c>Complement</c> 配矩形求交。要"目标之间的缝隙"用 <c>Interjacent</c>（id 484）。</para>
	///   <para><b>坑</b>结果是背景块，序号与目标块没有对应关系，别拿它的第 i 个元素当第 i 个目标的
	///   包围；贴画幅边缘的背景块是否保留 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("puzzle.hobj");
	///   JlRegion pieces = image.Threshold(100.0, 255.0);
	///   using JlRegion gaps = pieces.Connection().BackgroundSeg();
	///   pieces.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；背景块面积普遍远大于目标块，逐块量测前先按面积筛。</para>
	/// </remarks>
	public JlRegion BackgroundSeg()
	{
		IntPtr proc = JlNativeApi.PreCall(495);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Generate a region having a given Hamming distance.
	/// </summary>
	/// <param name="width">Width of the region to be changed. Default: 100</param>
	/// <param name="height">Height of the region to be changed. Default: 100</param>
	/// <param name="distance">Hamming distance between the old and new regions. Default: 1000</param>
	/// <returns>Regions having the required Hamming distance.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>生成一个与输入区域恰有 <c>distance</c> 个像素归属不同的新区域
	///   （原生 id 496，三个参数 <c>StoreI</c> 直写）：改动发生在 <c>width</c>×<c>height</c>
	///   的范围内，改哪些像素不指定（随机性质）。</para>
	///   <para><b>用途</b>为 <c>HammingDistance*</c> 族量测构造已知差异量的测试样本、做阈值敏感性
	///   验证；不是几何编辑工具，别拿它调整产品 ROI。</para>
	///   <para><b>约束</b><c>distance</c> 超出范围内可改动的像素数时的行为 [待实测]；同一输入
	///   两次调用结果不同（无种子参数，不可复现）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion baseRegion = new JlRegion(100.0, 80.0, 260.0, 400.0);
	///   using JlRegion perturbed = baseRegion.HammingChangeRegion(512, 512, 1000);
	///   baseRegion.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；改动后的面积/矩按新区域重算。</para>
	/// </remarks>
	public JlRegion HammingChangeRegion(int width, int height, int distance)
	{
		IntPtr proc = JlNativeApi.PreCall(496);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.StoreI(proc, 2, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Remove noise from a region.
	/// </summary>
	/// <param name="type">Mode of noise removal. Default: "n_4"</param>
	/// <returns>Less noisy regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按邻接关系去孤点（原生 id 497，<c>type</c> 字符串透传，默认
	///   "n_4"；另有 "n_8"、"n_isolated" 等取值 [待实测]）：没有邻居像素的孤立点被删除。</para>
	///   <para><b>与相邻算子的取舍</b><c>RankRegion</c>（id 490）能调去噪强度但会圆化转角；
	///   <c>Connection</c> 后按面积筛（<c>SelectShape</c>）删的是整个小连通块——本算子只删
	///   无邻居的单点，不伤及 2 像素以上的细线，是阈值化后清噪点的第一步标配。</para>
	///   <para><b>坑</b>判"孤立"按所取邻接定义的邻居计数，2 像素对角点在对角线纹理里会被误删；
	///   去噪后像素数变化，面积类特征重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("text_page.hobj");
	///   JlRegion raw = image.Threshold(0.0, 100.0);
	///   using JlRegion clean = raw.RemoveNoiseRegion("n_4");
	///   raw.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion RemoveNoiseRegion(string type)
	{
		IntPtr proc = JlNativeApi.PreCall(497);
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
	///   Transform the shape of a region.
	/// </summary>
	/// <param name="type">Type of transformation. Default: "convex"</param>
	/// <returns>Transformed regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域替换为理想形状（原生 id 498，<c>type</c> 字符串透传，默认
	///   "convex"）：凸包、最小外接椭圆/圆/矩形一类的代换，其余可用字面量清单托管层未枚举 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要凸包的轮廓点序列不要实心区域用 <c>GetRegionConvex</c>
	///   （id 622）；只要最小外接矩形用 <c>SmallestRectangle1</c>/<c>SmallestRectangle2</c> 直接出参数。
	///   本算子的价值是"替换后的区域仍可做后续布尔运算"。</para>
	///   <para><b>坑</b>凸包会填掉凹角、外接矩形会超出原轮廓——面积、偏心率全部改变，
	///   代换前的特征不可再引用；用代换后的形状算"凹度损失"（原面积减凸面积）是合法用法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("gear.hobj");
	///   JlRegion gear = image.Threshold(128.0, 255.0);
	///   using JlRegion convex = gear.ShapeTrans("convex");
	///   gear.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，逐对象代换、元素顺序保持。</para>
	/// </remarks>
	public JlRegion ShapeTrans(string type)
	{
		IntPtr proc = JlNativeApi.PreCall(498);
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
	///   Fill gaps between regions (depending on gray value or color) or split overlapping regions.
	/// </summary>
	/// <param name="image">Image (possibly multi-channel) for gray value or color comparison.</param>
	/// <param name="forbiddenArea">Regions in which no expansion takes place.</param>
	/// <param name="iterations">Number of iterations. Default: "maximal"</param>
	/// <param name="mode">Expansion mode. Default: "image"</param>
	/// <param name="threshold">Maximum difference between the gray value or color at the region's border and a candidate for expansion. Default: 32</param>
	/// <returns>Expanded or separated regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>带灰度判据的区域生长（原生 id 499）：像 <c>ExpandRegion</c>（id 487）
	///   一样迭代外扩，但每个候选像素要与区域边界的灰度（或多通道颜色）比较，差值超过
	///   <c>threshold</c> 就不越过——让区域在同类灰度内填缝、遇异类灰度停止。</para>
	///   <para><b>前提</b><c>image</c> 与区域必须出自同一幅图或同一坐标系（区域由该图 Threshold
	///   得来即是），尺寸不匹配时对应不上灰度；<c>image</c> 与 <c>forbiddenArea</c> 都以 iconic
	///   <c>Store</c> 传入并 <c>GC.KeepAlive</c> 保活。彩色图按通道比较 [待实测：通道数与 threshold 元组长度的配对]。</para>
	///   <para><b>与相邻算子的取舍</b>边界灰度漂移大时改 <c>ExpandGrayRef</c>（id 500）以固定参考值
	///   比较；与灰度无关的纯几何填缝用 <c>ExpandRegion</c>。</para>
	///   <para><b>重载选择</b>标量版见 <see cref="ExpandGray(JlImage,JlRegion,string,string,int)"/>：
	///   同一 id 499，差别仅在 <c>iterations</c> 走 <c>StoreS</c>（可传 "maximal"）、<c>threshold</c>
	///   走 <c>StoreI</c>，无元组固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("coins.hobj");
	///   JlRegion seeds = image.Threshold(140.0, 255.0).Connection();
	///   JlRegion keepOut = new JlRegion(0.0, 0.0, 20.0, 640.0);
	///   using JlRegion coins = seeds.ExpandGray(image, keepOut, 30, "image", 40);
	///   seeds.Dispose();
	///   keepOut.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；生长后所有区域特征重算。</para>
	/// </remarks>
	public JlRegion ExpandGray(JlImage image, JlRegion forbiddenArea, JlTuple iterations, string mode, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(499);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.Store(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.Store(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		JlNativeApi.UnpinTuple(threshold);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Fill gaps between regions (depending on gray value or color) or split overlapping regions.
	/// </summary>
	/// <param name="image">Image (possibly multi-channel) for gray value or color comparison.</param>
	/// <param name="forbiddenArea">Regions in which no expansion takes place.</param>
	/// <param name="iterations">Number of iterations. Default: "maximal"</param>
	/// <param name="mode">Expansion mode. Default: "image"</param>
	/// <param name="threshold">Maximum difference between the gray value or color at the region's border and a candidate for expansion. Default: 32</param>
	/// <returns>Expanded or separated regions.</returns>
	/// <remarks>
	///   <para>灰度生长语义与"区域必须配对同一幅图"的前提见
	///   <see cref="ExpandGray(JlImage,JlRegion,JlTuple,string,JlTuple)"/>。两个重载同一原生算子
	///   （id 499）：本重载 <c>iterations</c> 走 <c>StoreS</c>（默认 "maximal" 长满即此写法）、
	///   <c>threshold</c> 走 <c>StoreI</c> 直写单一阈值，无元组固定与 <c>UnpinTuple</c>；
	///   全体对象同一圈数、同一阈值时用本重载。</para>
	/// </remarks>
	public JlRegion ExpandGray(JlImage image, JlRegion forbiddenArea, string iterations, string mode, int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(499);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.StoreS(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreI(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Fill gaps between regions (depending on gray value or color) or split overlapping regions.
	/// </summary>
	/// <param name="image">Image (possibly multi-channel) for gray value or color comparison.</param>
	/// <param name="forbiddenArea">Regions in which no expansion takes place.</param>
	/// <param name="iterations">Number of iterations. Default: "maximal"</param>
	/// <param name="mode">Expansion mode. Default: "image"</param>
	/// <param name="refGray">Reference gray value or color for comparison. Default: 128</param>
	/// <param name="threshold">Maximum difference between the reference gray value or color and a candidate for expansion. Default: 32</param>
	/// <returns>Expanded or separated regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>ExpandGray</c>（id 499）同为灰度生长，判据不同（原生 id 500）：
	///   候选像素与固定参考值 <c>refGray</c> 比较、差值超 <c>threshold</c> 停止，而不是与区域
	///   自身边界比较。区域边界灰度本身不稳（生长初期边界质量差）时用本算子钉住目标灰度。</para>
	///   <para><b>前提</b><c>image</c> 与区域同一坐标系；<c>refGray</c>/<c>threshold</c> 在本重载以元组
	///   <c>Store</c> 固定、调用后逐个 <c>UnpinTuple</c>，彩色图需逐通道给参考值 [待实测：配对规则]。</para>
	///   <para><b>与相邻算子的取舍</b>背景均匀、只要"长到别再长"用 <c>ExpandRegion</c>（id 487）；
	///   目标与背景都无固定灰度、只有边界连续时才用 <c>ExpandGray</c>。</para>
	///   <para><b>重载选择</b>标量版见 <see cref="ExpandGrayRef(JlImage,JlRegion,string,string,int,int)"/>：
	///   同一 id 500，<c>StoreS</c>/<c>StoreI</c> 直写、无固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("strip.hobj");
	///   JlRegion seeds = image.Threshold(128.0, 255.0).Connection();
	///   JlRegion keepOut = new JlRegion(0.0, 0.0, 8.0, 512.0);
	///   using JlRegion grown = seeds.ExpandGrayRef(image, keepOut, new JlTuple(30.0), "image",
	///       new JlTuple(200.0), new JlTuple(30.0));
	///   seeds.Dispose();
	///   keepOut.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；生长后特征重算。</para>
	/// </remarks>
	public JlRegion ExpandGrayRef(JlImage image, JlRegion forbiddenArea, JlTuple iterations, string mode, JlTuple refGray, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(500);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Fill gaps between regions (depending on gray value or color) or split overlapping regions.
	/// </summary>
	/// <param name="image">Image (possibly multi-channel) for gray value or color comparison.</param>
	/// <param name="forbiddenArea">Regions in which no expansion takes place.</param>
	/// <param name="iterations">Number of iterations. Default: "maximal"</param>
	/// <param name="mode">Expansion mode. Default: "image"</param>
	/// <param name="refGray">Reference gray value or color for comparison. Default: 128</param>
	/// <param name="threshold">Maximum difference between the reference gray value or color and a candidate for expansion. Default: 32</param>
	/// <returns>Expanded or separated regions.</returns>
	/// <remarks>
	///   <para>与固定参考灰度比较的生长语义见
	///   <see cref="ExpandGrayRef(JlImage,JlRegion,JlTuple,string,JlTuple,JlTuple)"/>。两个重载同一
	///   原生算子（id 500）：本重载 <c>iterations</c> 走 <c>StoreS</c>（可传 "maximal"）、
	///   <c>refGray</c>/<c>threshold</c> 走 <c>StoreI</c> 直写单一值，无元组固定与 <c>UnpinTuple</c>；
	///   全体对象共用一个参考灰度时用本重载。</para>
	/// </remarks>
	public JlRegion ExpandGrayRef(JlImage image, JlRegion forbiddenArea, string iterations, string mode, int refGray, int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(500);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.StoreS(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreI(proc, 2, refGray);
		JlNativeApi.StoreI(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>
	///   Split lines represented by one pixel wide, non-branching lines.
	/// </summary>
	/// <param name="maxDistance">Maximum distance of the line points to the line segment connecting both end points. Default: 3</param>
	/// <param name="beginRow">Row coordinates of the start points of the output lines.</param>
	/// <param name="beginCol">Column coordinates of the start points of the output lines.</param>
	/// <param name="endRow">Row coordinates of the end points of the output lines.</param>
	/// <param name="endCol">Column coordinates of the end points of the output lines.</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 1 像素宽、无分叉的骨架段拆成直线段并直接给出端点坐标
	///   （原生 id 501）：四个 <c>out</c> 全部以 INTEGER 元组装载（<c>JlTuple.LoadNew</c>），
	///   <c>beginRow[i]</c>…<c>endCol[i]</c> 第 i 组平行描述一条线段的两个端点。</para>
	///   <para><b>与相邻算子的取舍</b><c>SplitSkeletonRegion</c>（id 502）输出的是线段"区域"，
	///   还要再取坐标；要直接喂给直线拟合/画矢量图用本算子。前提同样是输入为无分叉骨架
	///   （先 <c>Skeleton</c> + <c>Pruning</c> 或 <c>JunctionsSkeleton</c> 分叉处分拆）。</para>
	///   <para><b>参数取向</b>无返回值；四个 <c>out JlTuple</c> 一个不能少，调用处逐个写 <c>out</c>。
	///   <c>maxDistance</c> 是"线点偏离两端连线的容差"，调大则弯折线段被并成一条长直线，
	///   调小则输出段数增多。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stroke.hobj");
	///   JlRegion skel = image.Threshold(128.0, 255.0).Skeleton();
	///   skel.SplitSkeletonLines(3, out JlTuple br, out JlTuple bc, out JlTuple er, out JlTuple ec);
	///   int segs = br.Length;
	///   skel.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>四个端点元组各自独立持有原生内存，用完逐个 Dispose。</para>
	/// </remarks>
	public void SplitSkeletonLines(int maxDistance, out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol)
	{
		IntPtr proc = JlNativeApi.PreCall(501);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maxDistance);
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
	///   Split lines represented by one pixel wide, non-branching regions.
	/// </summary>
	/// <param name="maxDistance">Maximum distance of the line points to the line segment connecting both end points. Default: 3</param>
	/// <returns>Split lines.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 1 像素宽、无分叉的骨架按"偏离两端连线不超过 <c>maxDistance</c>"
	///   拆成多段，结果仍是区域元组（原生 id 502，<c>StoreI</c>+<c>LoadNew</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要每段的端点坐标用 <c>SplitSkeletonLines</c>（id 501，
	///   四个 INTEGER 元组输出）；要保住区域形态继续做布尔运算用本算子。分段标准一致，
	///   <c>maxDistance</c> 越大、越弯的笔画也并成一段。</para>
	///   <para><b>前提</b>输入必须是无分叉骨架：先 <c>Skeleton</c>（id 476）、<c>Pruning</c>（id 714），
	///   必要时在分叉点处 <c>Difference</c> 掉交叉像素再拆。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stroke.hobj");
	///   JlRegion skel = image.Threshold(128.0, 255.0).Skeleton();
	///   using JlRegion segs = skel.SplitSkeletonRegion(3);
	///   int n = segs.CountObj();
	///   skel.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>段序号由拆分算法决定、不可指定；对段做排序要再走 <c>SortRegion</c>。</para>
	/// </remarks>
	public JlRegion SplitSkeletonRegion(int maxDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(502);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maxDistance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把计数序列（直方图）变成阶梯状区域，用于把分布画成图形。
	/// </summary>
	/// <param name="histogram">各灰度级或各区间对应的计数（元组）。</param>
	/// <param name="row">阶梯图中心的行坐标。Default: 255</param>
	/// <param name="column">阶梯图中心的列坐标。Default: 255</param>
	/// <param name="scale">计数到像素高度的换算系数。Default: 1</param>
	/// <remarks>
	///   <para><b>这是"原地生成"一族</b>方法体第一步 <c>Dispose()</c>：本实例原先持有的区域
	///   被销毁，结果由 <c>Load</c> 直接写回本实例，无返回值。实例被 <c>Dispose</c> 后处于
	///   未初始化态（<c>IsInitialized()</c> 为 false），所以标准写法是
	///   <c>new JlRegion()</c> 后调用。</para>
	///   <para><b>与相邻算子的取舍</b>只用于可视化/构造图形；要做掩膜请回到
	///   <c>JlImage.Threshold</c>。原生 id 503。</para>
	///   <para><b>参数取向</b>全为入参，顺序 histogram、row、column、scale；后三者是 int。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion plot = new JlRegion();
	///   plot.GenRegionHisto(new JlTuple(3.0, 7.0, 12.0, 5.0), 200, 300, 4);
	///   </code>
	///   <para><b>资源与坑</b>同一实例连续调用两次 Gen 类方法是覆盖而非追加，
	///   scale 与计数范围决定图形是否越出预期画幅 [待实测]。</para>
	/// </remarks>
	public void GenRegionHisto(JlTuple histogram, int row, int column, int scale)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(503);
		JlNativeApi.Store(proc, 0, histogram);
		JlNativeApi.StoreI(proc, 1, row);
		JlNativeApi.StoreI(proc, 2, column);
		JlNativeApi.StoreI(proc, 3, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(histogram);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Eliminate runs of a given length.
	/// </summary>
	/// <param name="elimShorter">All runs which are shorter are eliminated. Default: 3</param>
	/// <param name="elimLonger">All runs which are longer are eliminated. Default: 1000</param>
	/// <returns>Clipped regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按游程（行内水平段）长度删像素（原生 id 504，两参数 <c>StoreI</c> 直写）：
	///   长度小于 <c>elimShorter</c> 或大于 <c>elimLonger</c> 的游程整体删除，默认 3/1000 即
	///   "只删短游程"。</para>
	///   <para><b>与相邻算子的取舍</b><c>RemoveNoiseRegion</c>（id 497）按孤点判据、<c>RankRegion</c>
	///   （id 490）按邻域计数，都是二维结构；本算子是一维的行内操作，专治扫描线状毛刺、
	///   保留竖向笔画——腐蚀会一起干掉的东西它能留下。</para>
	///   <para><b>坑</b>删除以游程为单位，一个连通块可能因个别游程被删而裂成多块，
	///   也可能整体解体；处理表格线时"长游程"阈值反向可用（删掉超长横线）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("scan.hobj");
	///   JlRegion noisy = image.Threshold(128.0, 255.0);
	///   using JlRegion tidy = noisy.EliminateRuns(4, 100000);
	///   noisy.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；游程可用 <c>GetRegionRuns</c>（id 620）自查。</para>
	/// </remarks>
	public JlRegion EliminateRuns(int elimShorter, int elimLonger)
	{
		IntPtr proc = JlNativeApi.PreCall(504);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, elimShorter);
		JlNativeApi.StoreI(proc, 1, elimLonger);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Calculate the difference of two object tuples.
	/// </summary>
	/// <param name="objectsSub">Object tuple 2.</param>
	/// <returns>Objects from Objects that are not part of ObjectsSub.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对象元组级的减法（原生 id 558）：从本句柄的元素列表中删去
	///   与 <c>objectsSub</c> 内容一致的元素，剩下的是"没被匹配走的对象"。删的是整元素，不是像素。</para>
	///   <para><b>与相邻算子的取舍</b>像素级的减法用 <c>Difference</c>（id 493）；按序号删元素用
	///   <c>RemoveObj</c>（<c>JlObject</c> 上，元组索引）。典型用法：两轮不同参数 <c>Connection</c>/
	///   <c>SelectShape</c> 之后，用本算子求"这一轮丢了哪些、多了哪些"。</para>
	///   <para><b>坑</b>匹配判据（逐像素相等还是允许容差）托管层无从得知 [待实测]，与
	///   <c>CompareObj</c>（id 573）一样对 epsilon 的立场需实测确认；剩余元素的序号会前移，
	///   依赖序号的后续操作要重排。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion all = image.Threshold(100.0, 255.0).Connection();
	///   JlRegion big = all.SelectShape("area", "and", 500.0, 99999999.0);
	///   using JlRegion small = all.ObjDiff(big);   // 大颗粒整元素被删，剩小颗粒
	///   all.Dispose();
	///   big.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；两输入不被改写。</para>
	/// </remarks>
	public JlRegion ObjDiff(JlRegion objectsSub)
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
	///   Paint regions into an image.
	/// </summary>
	/// <param name="image">Image in which the regions are to be painted.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把区域画到图像上并返回<strong>新</strong>图像（原生 id 561：区域
	///   <c>Store</c> 到控制参数 1、输入图像到 2，输出 <c>JlImage.LoadNew</c>）——传入的
	///   <c>image</c> 不被改写，可继续另作他用。</para>
	///   <para><b>约束</b>灰度值按输入图像的通道数配对：单通道给一个值，多通道需逐通道给值
	///   （本重载以元组 <c>Store</c>+<c>UnpinTuple</c> 传多值）；不匹配的行为 [待实测]。
	///   <c>type="fill"</c> 整体填充，"margin" 只画边界 [待实测：其余字面量]。
	///   输出像素类型跟随输入图像，浮点图上写不进超出精度的值。</para>
	///   <para><b>与相邻算子的取舍</b>要在原图上就地涂改、不想多一张图时用 <c>OverpaintRegion</c>
	///   （id 562）；要把整张区域转成独立二值图用 <c>RegionToBin</c>（id 471）。</para>
	///   <para><b>重载选择</b>标量版见 <see cref="PaintRegion(JlImage,double,string)"/>：同一 id 561，
	///   <c>StoreD</c> 直写、无固定与 <c>UnpinTuple</c>；单通道或各通道同值用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage canvas = new JlImage("byte", 640, 480);
	///   JlRegion roi = new JlRegion(60.0, 80.0, 200.0, 300.0);
	///   using JlImage painted = roi.PaintRegion(canvas, new JlTuple(255.0), "fill");
	///   canvas.Dispose();
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；<c>image</c> 保活到调用结束。</para>
	/// </remarks>
	public JlImage PaintRegion(JlImage image, JlTuple grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(561);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
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
	///   Paint regions into an image.
	/// </summary>
	/// <param name="image">Image in which the regions are to be painted.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <returns>Image containing the result.</returns>
	/// <remarks>
	///   <para>返回新图、不改输入等语义见 <see cref="PaintRegion(JlImage,JlTuple,string)"/>。
	///   两个重载同一原生算子（id 561）：本重载 <c>StoreD</c> 直写单一灰度值、无 <c>UnpinTuple</c>；
	///   多通道图需要逐通道不同值时不能用本重载。</para>
	/// </remarks>
	public JlImage PaintRegion(JlImage image, double grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(561);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   Overpaint regions in an image.
	/// </summary>
	/// <param name="image">Image in which the regions are to be painted.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <remarks>
	///   <para><b>功能说明</b>把区域直接涂进 <c>image</c> 本体（原生 id 562：区域 <c>Store</c> 到
	///   控制参数 2、图像到 1，全程无 <c>InitOCT</c>、无任何 LoadNew/Load）——<strong>没有返回值，
	///   被改写的是传入图像自己</strong>，原像素在覆盖处不可恢复。</para>
	///   <para><b>与相邻算子的取舍</b>还要留原图用 <c>PaintRegion</c>（id 561，出新图）；
	///   在采集图上连续叠加多个掩膜标注用本算子省内存。灰度值与通道数配对、
	///   <c>type</c> 取 "fill"/边界 [待实测：清单]。</para>
	///   <para><b>重载选择</b>标量版见 <see cref="OverpaintRegion(JlImage,double,string)"/>：同一 id 562，
	///   <c>StoreD</c> 直写、无 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("coat.hobj");
	///   JlRegion defect = image.Threshold(200.0, 255.0);
	///   defect.OverpaintRegion(image, 0.0, "fill");   // 把缺陷区在原图上涂黑
	///   defect.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>涂完之后若还要用"干净"的图，先 <c>JlImage.CopyImage()</c> 另存。</para>
	/// </remarks>
	public void OverpaintRegion(JlImage image, JlTuple grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(562);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   Overpaint regions in an image.
	/// </summary>
	/// <param name="image">Image in which the regions are to be painted.</param>
	/// <param name="grayval">Desired gray values of the regions. Default: 255.0</param>
	/// <param name="type">Paint regions filled or as boundaries. Default: "fill"</param>
	/// <remarks>
	///   <para>就地涂改、无返回值等语义见 <see cref="OverpaintRegion(JlImage,JlTuple,string)"/>。
	///   两个重载同一原生算子（id 562）：本重载 <c>StoreD</c> 直写单一灰度值、无 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void OverpaintRegion(JlImage image, double grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(562);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   Copy an iconic object in the Vision database.
	/// </summary>
	/// <param name="index">Starting index of the objects to be copied. Default: 1</param>
	/// <param name="numObj">Number of objects to be copied or -1. Default: 1</param>
	/// <returns>Copied objects.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>从本句柄的元素列表中切一段复制出来（原生 id 568，
	///   <c>index</c>/<c>numObj</c> 经 <c>StoreI</c> 直写）：<c>index</c> 起、取 <c>numObj</c> 个，
	///   <c>numObj=-1</c> 表示取到末尾；传超出列表范围的序号 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>这是"容器"操作：拷的是整元素，像素内容、序号语义与
	///   <c>SelectObj</c>（id 572）一致；区别在 <c>SelectObj</c> 按任意序号向量挑 reorder，
	///   <c>CopyObj</c> 只截连续区间。与 <c>Clone</c> 类整体拷贝也不同，可用它做"把第 3~7 个目标
	///   单独存档"。</para>
	///   <para><b>坑</b>序号作用于当前列表：任何 <c>Connection</c>/<c>SortRegion</c>/<c>Union1</c>
	///   都会让序号错位，按序号取用之前先固定住来源顺序。</para>
	///   <para><b>重载选择</b><c>JlObject.CopyObj</c>（同一 id 568）返回基类句柄；本成员只是把
	///   返回类型收窄为 <c>JlRegion</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion blobs = new JlImage("pellets.hobj").Threshold(100.0, 255.0).Connection();
	///   using JlRegion second = blobs.CopyObj(2, 1);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄（新元素副本）。</para>
	/// </remarks>
	public new JlRegion CopyObj(int index, int numObj)
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
	///   <para><b>功能说明</b>把两个对象元组首尾相接（原生 id 569）：本句柄的元素在前、
	///   <c>objects2</c> 的在后，全部装进一个新句柄；两侧输入不被改写。</para>
	///   <para><b>与相邻算子的取舍</b>这是容器操作——接的是"整元素"，与像素无关，重叠的
	///   区域拼完仍是两块；要像素融合用 <c>Union2</c>/<c>Union1</c>。把多个来源的检出结果合成
	///   一路处理（分别阈值化后合并目标）用它。</para>
	///   <para><b>坑</b>拼接改变了元素序号：后续 <c>SelectObj</c>/<c>CopyObj</c>/逐元素特征表
	///   的行号都跟着移动，"第 i 个目标"的语义在拼接前后不是同一批东西；多路拼接近似左结合，
	///   大批量循环拼接的耗时行为 [待实测]。</para>
	///   <para><b>重载选择</b><c>JlObject.ConcatObj</c> 返回基类句柄，本成员把形参与返回收窄为区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion bright = image.Threshold(180.0, 255.0).Connection();
	///   JlRegion dim = image.Threshold(120.0, 179.0).Connection();
	///   using JlRegion all = bright.ConcatObj(dim);
	///   int n = all.CountObj();
	///   bright.Dispose();
	///   dim.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；<c>objects2</c> 保活到调用结束。</para>
	/// </remarks>
	public JlRegion ConcatObj(JlRegion objects2)
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
	///   <para><b>功能说明</b>按序号向量从对象元组挑元素（原生 id 572）：本重载 <c>index</c>
	///   以元组 <c>Store</c> 固定、调用后 <c>UnpinTuple</c>，一次可挑多个，输出顺序按给定序号、
	///   同一序号可重复（重复即复制多份）。</para>
	///   <para><b>坑：序号对不上就全错</b>它认的是"当前列表里的第几个"，而 <c>Connection</c>
	///   的产出顺序由拆分算法决定、不由用户指定；上游阈值或参数一变，"第 3 个目标"就是另一个
	///   目标。跨帧/跨批次按序号存取前，先 <c>SortRegion</c>（id 703）定序。</para>
	///   <para><b>与相邻算子的取舍</b>截连续区间用 <c>CopyObj</c>（id 568）；删元素用
	///   <c>RemoveObj</c>（<c>JlObject</c>）；按特征筛用 <c>SelectShape</c>，不要手填序号。</para>
	///   <para><b>重载选择</b>单序号请用 <see cref="SelectObj(int)"/>：同一 id 572，<c>StoreI</c> 直写、
	///   无固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion blobs = new JlImage("pellets.hobj").Threshold(100.0, 255.0).Connection();
	///   using JlRegion picked = blobs.SelectObj(new JlTuple(1.0, 4.0));
	///   int n = picked.CountObj();
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；序号基准是否 1 起未经托管层注明 [待实测]。</para>
	/// </remarks>
	public new JlRegion SelectObj(JlTuple index)
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
	///   <para>序号语义与错位风险见 <see cref="SelectObj(JlTuple)"/>。两个重载同一原生算子
	///   （id 572）：本重载 <c>StoreI</c> 直写单一下标、无 <c>UnpinTuple</c>；只取一个元素时用本重载。</para>
	/// </remarks>
	public new JlRegion SelectObj(int index)
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
	///   <para><b>功能说明</b>比较两个对象元组是否"在容差内相似"（原生 id 573）：返回经
	///   <c>LoadI</c> 取出的标量 int（布尔型结果），<c>epsilon</c> 在本重载以元组 <c>Store</c>
	///   固定后 <c>UnpinTuple</c>，可逐属性给不同容差。</para>
	///   <para><b>与相邻算子的取舍</b><c>TestEqualObj</c>/<c>TestEqualRegion</c> 判"是否相同"，
	///   没有容差参数；对经过仿射、栅格化的结果用本算子配 epsilon，逐像素相等几乎不可能成立。
	///   epsilon 对区域对象具体约束什么量（坐标偏移上限？）托管层无从判断 [待实测]。</para>
	///   <para><b>坑</b>两侧元素个数不同时的比较规则 [待实测]；返回 0/1 而非概率，
	///   别把它当相似度分数用。</para>
	///   <para><b>重载选择</b>单一容差请用 <see cref="CompareObj(JlRegion,double)"/>：同一 id 573，
	///   <c>StoreD</c> 直写、无固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   JlRegion b = new JlRegion(10.0, 12.0, 50.0, 52.0);
	///   bool near = a.CompareObj(b, new JlTuple(1.0)) == 1;
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>不产生新句柄、不修改输入。</para>
	/// </remarks>
	public int CompareObj(JlRegion objects2, JlTuple epsilon)
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
	///   <para>容差比较语义见 <see cref="CompareObj(JlRegion,JlTuple)"/>。两个重载同一原生算子
	///   （id 573）：本重载 <c>StoreD</c> 直写单一容差、无元组固定与 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public int CompareObj(JlRegion objects2, double epsilon)
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
	///   Test whether a region is contained in another region.
	/// </summary>
	/// <param name="region2">Region for comparison.</param>
	/// <returns>Is Region1 contained in Region2?</returns>
	/// <remarks>
	///   <para><b>功能说明</b>逐对判断"本句柄第 i 个区域是否被 <c>region2</c> 第 i 个区域包含"
	///   （原生 id 574）：结果是 INTEGER 元组（<c>JlTuple.LoadNew</c>），每元素 0/1，
	///   不是标量布尔。</para>
	///   <para><b>与相邻算子的取舍</b>要"两个句柄整体是否相等"用 <c>TestEqualObj</c>（id 576）；
	///   要单点是否落在区域内是另一族 <c>TestRegionPoint*</c>；本算子做的是"区域对区域"的
	///   包含判定，典型用法：把检出结果与允许区域元组按同一顺序配对后核对是否越界。</para>
	///   <para><b>坑</b>按位置配对，两侧元素数不等或顺序不同即错配——先 <c>SortRegion</c> 定序；
	///   "包含"是否允许相等（子集=自身）[待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(0.0, 0.0, 100.0, 100.0);
	///   JlRegion item = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   JlTuple inside = item.TestSubsetRegion(roi);
	///   bool ok = inside.Length &gt; 0 &amp;&amp; inside[0] == 1.0;
	///   roi.Dispose();
	///   item.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组用完 Dispose；两输入不被改写。</para>
	/// </remarks>
	public JlTuple TestSubsetRegion(JlRegion region2)
	{
		IntPtr proc = JlNativeApi.PreCall(574);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region2);
		return tuple;
	}

	/// <summary>
	///   Test whether the regions of two objects are identical.
	/// </summary>
	/// <param name="regions2">Comparative regions.</param>
	/// <returns>Boolean result value.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>判断两侧区域是否逐像素一致，返回标量 int（0/1，<c>LoadI</c> 装载；
	///   原生 id 575）。</para>
	///   <para><b>与相邻算子的取舍</b><c>TestEqualObj</c>（id 576）比的是对象元组整体、
	///   <c>CompareObj</c>（id 573）带容差；本算子聚焦区域集合的相等。经过仿射或栅格化的
	///   结果对它几乎必然返回 0，验证重建正确性应改用容差比较。</para>
	///   <para><b>坑</b>句柄不同但内容相同返回 1（比较的是像素集合不是引用）；元组两侧的
	///   元素顺序是否参与比较 [待实测]。返回 int，判等要写 <c>== 1</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   JlRegion b = a.CopyObj(1, -1);
	///   bool same = a.TestEqualRegion(b) == 1;
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>不产生新句柄；两侧保活到调用结束。</para>
	/// </remarks>
	public int TestEqualRegion(JlRegion regions2)
	{
		IntPtr proc = JlNativeApi.PreCall(575);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return intValue;
	}

	/// <summary>
	///   比较两侧 iconic 元组是否整体相等，返回 0/1（不是逐元素的结果向量）。
	/// </summary>
	/// <param name="objects2">对照对象（可为区域元组）。</param>
	/// <returns>整等返回 1，否则 0（<c>int</c>，需与 1 比较而不是当布尔句柄用）。</returns>
	/// <remarks>
	///   <para><b>易错</b>比较在像素层面：句柄不同但内容相同返回 1；元组内顺序不同、个数不同时
	///   判不判等无法由托管层确定 [待实测]。本成员在 <c>JlObject</c> 上有同签名版本（同一原生 id 576），
	///   <c>JlRegion</c> 只是把形参收窄成区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域层的整等比较用 <c>TestEqualRegion</c>（id 575，本绑定里
	///   同样返回标量 int，逐元素语义 [待实测]）；要连续相似度用 <c>HammingDistanceNorm</c>（id 1635）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   JlRegion b = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   bool same = a.TestEqualObj(b) == 1;
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无新句柄、不修改任何输入；两侧 <c>KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public int TestEqualObj(JlRegion objects2)
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
	///   用顶点序列围出实心多边形区域（原地生成）。
	/// </summary>
	/// <param name="rows">各顶点的行坐标（元组，与 columns 并行配对）。</param>
	/// <param name="columns">各顶点的列坐标（元组）。</param>
	/// <remarks>
	///   <para><b>实心与描边的分界</b>本算子（id 583）填出多边形内部；只要轮廓线本身用
	///   <c>GenRegionPolygon</c>（id 584）。做检测掩膜要实心，量边界周长才用描边。</para>
	///   <para><b>原地生成</b>方法体先 <c>Dispose()</c> 再 <c>Load</c> 回本实例，无返回值；
	///   实例原有内容被销毁。</para>
	///   <para><b>参数取向</b><c>rows</c>/<c>columns</c> 均须至少 3 对顶点，首尾自动闭合；
	///   顶点数不足时的行为 [待实测]。坐标是先行后列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion poly = new JlRegion();
	///   poly.GenRegionPolygonFilled(new JlTuple(10.0, 10.0, 90.0, 60.0),
	///       new JlTuple(10.0, 80.0, 80.0, 90.0));
	///   </code>
	///   <para><b>资源与坑</b>两元组长度不一致时的广播规则 [待实测]。</para>
	/// </remarks>
	public void GenRegionPolygonFilled(JlTuple rows, JlTuple columns)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(583);
		JlNativeApi.Store(proc, 0, rows);
		JlNativeApi.Store(proc, 1, columns);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows);
		JlNativeApi.UnpinTuple(columns);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用顶点序列生成只描边的多边形区域（原地生成，不填内部）。
	/// </summary>
	/// <param name="rows">各顶点的行坐标（元组）。</param>
	/// <param name="columns">各顶点的列坐标（元组）。</param>
	/// <remarks>
	///   <para>与 <see cref="GenRegionPolygonFilled(JlTuple,JlTuple)"/> 的唯一实质差别是原生 id
	///   （584 对 583）：本算子只生成沿顶点的描边，内部不填充。描边线宽、斜线段是否断格
	///   [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion frame = new JlRegion();
	///   frame.GenRegionPolygon(new JlTuple(10.0, 10.0, 90.0, 90.0),
	///       new JlTuple(10.0, 80.0, 80.0, 10.0));
	///   </code>
	/// </remarks>
	public void GenRegionPolygon(JlTuple rows, JlTuple columns)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(584);
		JlNativeApi.Store(proc, 0, rows);
		JlNativeApi.Store(proc, 1, columns);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows);
		JlNativeApi.UnpinTuple(columns);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把一批离散像素并成区域：第 i 个点是 (rows[i], columns[i])（原地生成）。
	/// </summary>
	/// <param name="rows">各点的行坐标（整型元组）。</param>
	/// <param name="columns">各点的列坐标（整型元组，与 rows 并行配对）。</param>
	/// <remarks>
	///   <para><b>用途定位</b>它是"点的集合"，不是线段、也不是像素矩形；批量撒点
	///   （标定图案、缺陷位置回标）用它。重复坐标不增加面积（并集语义）。</para>
	///   <para><b>约束</b>原生 id 585；原地生成（<c>Dispose</c> 后 <c>Load</c> 回本实例）。
	///   两元组长度不一致时的广播规则 [待实测]。</para>
	///   <para><b>参数取向</b>坐标是整数像素索引，先行后列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion pts = new JlRegion();
	///   pts.GenRegionPoints(new JlTuple(10, 20, 30), new JlTuple(15, 25, 35));
	///   </code>
	/// </remarks>
	public void GenRegionPoints(JlTuple rows, JlTuple columns)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(585);
		JlNativeApi.Store(proc, 0, rows);
		JlNativeApi.Store(proc, 1, columns);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows);
		JlNativeApi.UnpinTuple(columns);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单像素区域（两个整数坐标版）。
	/// </summary>
	/// <param name="rows">该点的行坐标。Default: 100</param>
	/// <param name="columns">该点的列坐标。Default: 100</param>
	/// <remarks>
	///   <para>多点语义与原地生成见 <see cref="GenRegionPoints(JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 585）：本重载 <c>StoreI</c> 直写、不做元组固定与
	///   <c>UnpinTuple</c>；注意两个形参是"一个点的行列"，不是一维或二维的数量。
	///   单像素的 Area 是否等于 1 [待实测]。</para>
	/// </remarks>
	public void GenRegionPoints(int rows, int columns)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(585);
		JlNativeApi.StoreI(proc, 0, rows);
		JlNativeApi.StoreI(proc, 1, columns);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用游程编码（逐行的行段）直接构造区域（原地生成）。
	/// </summary>
	/// <param name="row">各游程所在行（整型元组）。</param>
	/// <param name="columnBegin">各游程起始列（整型元组）。</param>
	/// <param name="columnEnd">各游程结束列（是否含端点 [待实测]）。</param>
	/// <remarks>
	///   <para><b>为什么用它</b>这是区域的原生存储形式（游程三元组），批量导入外部标注
	///   或逐行手工修正掩膜时最直接，省掉几何换算。</para>
	///   <para><b>与相邻算子的取舍</b><c>GenRegionLine</c> 生成的是斜向线像素，游程是逐行
	///   水平段，不要拿 runs 拼斜线。</para>
	///   <para><b>参数取向</b>三个元组并行配对，一个三元组对应一行内的一段；
	///   <c>columnEnd &lt; columnBegin</c> 时的行为 [待实测]。原生 id 586，原地生成。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion band = new JlRegion();
	///   band.GenRegionRuns(new JlTuple(10, 11), new JlTuple(5, 3), new JlTuple(40, 42));
	///   </code>
	/// </remarks>
	public void GenRegionRuns(JlTuple row, JlTuple columnBegin, JlTuple columnEnd)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(586);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, columnBegin);
		JlNativeApi.Store(proc, 2, columnEnd);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(columnBegin);
		JlNativeApi.UnpinTuple(columnEnd);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个游程（三个整数版）。
	/// </summary>
	/// <param name="row">该游程所在行。Default: 100</param>
	/// <param name="columnBegin">该游程起始列。Default: 50</param>
	/// <param name="columnEnd">该游程结束列。Default: 200</param>
	/// <remarks>
	///   <para>游程语义与原地生成见 <see cref="GenRegionRuns(JlTuple,JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 586）：本重载 <c>StoreI</c> 直写一段，无元组固定与
	///   <c>UnpinTuple</c>；三个形参是"同一行内的一段"，不是三段。</para>
	/// </remarks>
	public void GenRegionRuns(int row, int columnBegin, int columnEnd)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(586);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, columnBegin);
		JlNativeApi.StoreI(proc, 2, columnEnd);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   以中心、倾角和两条半轴长生成任意朝向的矩形（原地生成）。
	/// </summary>
	/// <param name="row">中心的行坐标。</param>
	/// <param name="column">中心的列坐标。</param>
	/// <param name="phi">第一条边相对水平方向的转角（弧度）。Default: 0.0</param>
	/// <param name="length1">半轴长 1（沿 phi 方向）。Default: 100.0</param>
	/// <param name="length2">半轴长 2。Default: 20.0</param>
	/// <remarks>
	///   <para><b>最易错</b><c>length1</c>/<c>length2</c> 是<b>半</b>轴（与
	///   <c>SmallestRectangle2</c> 的出参同义、可原样回填）：把全长传进去会得到 4 倍面积的矩形。
	///   元组重载可一次并行生成多个矩形（英文原文即为复数 regions），各参数元组需等长
	///   （不等长时的广播 [待实测]）。</para>
	///   <para><b>与 <c>GenRectangle1</c> 的取舍</b>按 <c>SmallestRectangle2</c> 的结果为旋转件
	///   建掩膜用本算子；轴对齐 ROI 用 1 型，更快也更直观。phi 的旋转正方向与坐标原点约定
	///   托管层未注明 [待实测]。</para>
	///   <para><b>重载选择</b>本重载原生 id 587，五个元组 <c>Store</c> 固定、调用后逐个
	///   <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion strip = new JlRegion();
	///   strip.GenRectangle2(200.0, 300.0, 0.5236, 90.0, 5.0);   // 半长 90、半宽 5，约 30° 长带
	///   </code>
	/// </remarks>
	public void GenRectangle2(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(587);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, length1);
		JlNativeApi.Store(proc, 4, length2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个任意朝向矩形（标量版）。
	/// </summary>
	/// <param name="row">中心的行坐标。Default: 300.0</param>
	/// <param name="column">中心的列坐标。Default: 200.0</param>
	/// <param name="phi">第一条边相对水平方向的转角（弧度）。Default: 0.0</param>
	/// <param name="length1">半轴长 1（沿 phi 方向）。Default: 100.0</param>
	/// <param name="length2">半轴长 2。Default: 20.0</param>
	/// <remarks>
	///   <para>半轴约定与 <c>GenRectangle1</c> 取舍见
	///   <see cref="GenRectangle2(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>。两个重载同一
	///   原生算子（id 587）：本重载 <c>StoreD</c> 直写五个标量，无元组固定与 <c>UnpinTuple</c>；
	///   单矩形用本重载。</para>
	/// </remarks>
	public void GenRectangle2(double row, double column, double phi, double length1, double length2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(587);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, length1);
		JlNativeApi.StoreD(proc, 4, length2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   以左上、右下两个对角角点生成轴对齐矩形（原地生成，可一次多个）。
	/// </summary>
	/// <param name="row1">左上角行坐标。Default: 30.0</param>
	/// <param name="column1">左上角列坐标。Default: 20.0</param>
	/// <param name="row2">右下角行坐标。Default: 100.0</param>
	/// <param name="column2">右下角列坐标。Default: 200.0</param>
	/// <remarks>
	///   <para><b>与角点构造器等价</b><c>new JlRegion(row1, column1, row2, column2)</c>
	///   走同一原生 id 588；两种写法任选其一即可，不必混用。</para>
	///   <para><b>参数取向</b>元组重载一次并行生成多个矩形（英文原文为复数）；
	///   两角点所夹像素是否为闭区间 [待实测]。<c>row2 &lt; row1</c> 等异常输入的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>旋转 ROI 用 <c>GenRectangle2</c>；从已有区域取包框用
	///   <c>SmallestRectangle1</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion();
	///   roi.GenRectangle1(new JlTuple(10.0, 120.0), new JlTuple(10.0, 240.0),
	///       new JlTuple(50.0, 180.0), new JlTuple(80.0, 320.0));
	///   int n = roi.CountObj();
	///   </code>
	/// </remarks>
	public void GenRectangle1(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(588);
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
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个轴对齐矩形（标量版）。
	/// </summary>
	/// <param name="row1">左上角行坐标。Default: 30.0</param>
	/// <param name="column1">左上角列坐标。Default: 20.0</param>
	/// <param name="row2">右下角行坐标。Default: 100.0</param>
	/// <param name="column2">右下角列坐标。Default: 200.0</param>
	/// <remarks>
	///   <para>语义与角点构造器的等价关系见
	///   <see cref="GenRectangle1(JlTuple,JlTuple,JlTuple,JlTuple)"/>。两个重载同一原生算子
	///   （id 588）：本重载 <c>StoreD</c> 直写、无元组固定与 <c>UnpinTuple</c>；单个 ROI 用本重载。</para>
	/// </remarks>
	public void GenRectangle1(double row1, double column1, double row2, double column2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(588);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create a random region.
	/// </summary>
	/// <param name="width">Maximum horizontal expansion of random region. Default: 128</param>
	/// <param name="height">Maximum vertical expansion of random region. Default: 128</param>
	/// <remarks>
	///   <para><b>功能说明</b>生成一团随机形状的连通区域，横向不超过约 <c>width</c>、纵向不超过约
	///   <c>height</c>（原生 id 589，<c>StoreI</c> 直写）。原地生成：方法体先 <c>Dispose()</c> 再
	///   <c>Load</c> 回本实例，无返回值。</para>
	///   <para><b>无种子</b>签名中没有随机种子参数，两次调用结果必然不同——回归测试不要依赖
	///   它生成的具体形状，只断言规模、连通性等不变量。</para>
	///   <para><b>与相邻算子的取舍</b>要形状可控的随机圆/矩形/椭圆元组用 <c>GenRandomRegions</c>
	///   （id 599）；要可复现的测试掩膜，改用 <c>GenCircle</c> 族手工拼装。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion blob = new JlRegion();
	///   blob.GenRandomRegion(128, 128);
	///   int area = blob.Area.Length;   // 1 个对象
	///   </code>
	///   <para><b>资源与坑</b>同一实例反复调用是覆盖；生成位置分布规律 [待实测]。</para>
	/// </remarks>
	public void GenRandomRegion(int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(589);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成椭圆扇形：以长短半轴定椭圆、以起止角截取楔形（原地生成）。
	/// </summary>
	/// <param name="row">中心的行坐标。Default: 200.0</param>
	/// <param name="column">中心的列坐标。Default: 200.0</param>
	/// <param name="phi">长半径方向的转角（弧度）。Default: 0.0</param>
	/// <param name="radius1">长半轴。Default: 100.0</param>
	/// <param name="radius2">短半轴。Default: 60.0</param>
	/// <param name="startAngle">扇形起始角（弧度）。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角（弧度）。Default: 3.14159</param>
	/// <remarks>
	///   <para><b>易混</b>英文原文写明 <c>radius1</c>/<c>radius2</c> 是 longer/shorter radius，
	///   即椭圆的两条半轴，<b>不是</b>环带的内外半径；本算子给出的是实心楔形。</para>
	///   <para><b>参数取向</b>本重载（id 593）七个参数全部是 <c>JlTuple</c>，可一次并行生成
	///   多个扇形（英文原文为复数）；角度反向（start &gt; end）或跨 0 时的取角方向 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>圆扇形用 <c>GenCircleSector</c>（id 595）；
	///   整椭圆用 <c>GenEllipse</c>（id 594）。起始角是相对 <c>phi</c> 还是绝对方向 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion wedge = new JlRegion();
	///   wedge.GenEllipseSector(200.0, 300.0, 0.0, 100.0, 60.0, 0.0, 1.5708);
	///   </code>
	/// </remarks>
	public void GenEllipseSector(JlTuple row, JlTuple column, JlTuple phi, JlTuple radius1, JlTuple radius2, JlTuple startAngle, JlTuple endAngle)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(593);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, radius1);
		JlNativeApi.Store(proc, 4, radius2);
		JlNativeApi.Store(proc, 5, startAngle);
		JlNativeApi.Store(proc, 6, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(radius1);
		JlNativeApi.UnpinTuple(radius2);
		JlNativeApi.UnpinTuple(startAngle);
		JlNativeApi.UnpinTuple(endAngle);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个椭圆扇形（标量版）。
	/// </summary>
	/// <param name="row">中心的行坐标。Default: 200.0</param>
	/// <param name="column">中心的列坐标。Default: 200.0</param>
	/// <param name="phi">长半径方向的转角（弧度）。Default: 0.0</param>
	/// <param name="radius1">长半轴。Default: 100.0</param>
	/// <param name="radius2">短半轴。Default: 60.0</param>
	/// <param name="startAngle">扇形起始角（弧度）。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角（弧度）。Default: 3.14159</param>
	/// <remarks>
	///   <para>半轴含义与取角方向的存疑点见
	///   <see cref="GenEllipseSector(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 593）：本重载 <c>StoreD</c> 直写七个标量，无元组固定与
	///   <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void GenEllipseSector(double row, double column, double phi, double radius1, double radius2, double startAngle, double endAngle)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(593);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, radius1);
		JlNativeApi.StoreD(proc, 4, radius2);
		JlNativeApi.StoreD(proc, 5, startAngle);
		JlNativeApi.StoreD(proc, 6, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   由中心、转角和两条半轴生成实心椭圆区域（原地生成）。
	/// </summary>
	/// <param name="row">中心的行坐标。Default: 200.0</param>
	/// <param name="column">中心的列坐标。Default: 200.0</param>
	/// <param name="phi">长半径方向转角（弧度）。Default: 0.0</param>
	/// <param name="radius1">长半轴。Default: 100.0</param>
	/// <param name="radius2">短半轴。Default: 60.0</param>
	/// <remarks>
	///   <para><b>约束</b>英文原文即"longer/shorter radius"：按本接口 <c>radius1</c> 应填长半轴、
	///   <c>radius2</c> 填短半轴；若原生层并不强制 radius1 ≥ radius2 [待实测]。
	///   <c>phi</c> 弧度制；能否与 <c>SmallestRectangle2</c> 量出的 phi 直接互填 [待实测]。</para>
	///   <para><b>与 <c>GenCircle</c> 的取舍</b>等截面目标用圆（id 596）语义更清楚；
	///   椭圆用于正视投影下的斜置圆孔。</para>
	///   <para><b>重载选择</b>本重载原生 id 594，五个元组 <c>Store</c> 固定后 <c>UnpinTuple</c>，
	///   可一次并行生成多个椭圆（英文原文为复数）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion hole = new JlRegion();
	///   hole.GenEllipse(150.0, 200.0, 0.7854, 40.0, 25.0);   // 45° 斜置孔
	///   </code>
	/// </remarks>
	public void GenEllipse(JlTuple row, JlTuple column, JlTuple phi, JlTuple radius1, JlTuple radius2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(594);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, radius1);
		JlNativeApi.Store(proc, 4, radius2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(radius1);
		JlNativeApi.UnpinTuple(radius2);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个椭圆（标量版）。
	/// </summary>
	/// <param name="row">中心的行坐标。Default: 200.0</param>
	/// <param name="column">中心的列坐标。Default: 200.0</param>
	/// <param name="phi">长半径方向转角（弧度）。Default: 0.0</param>
	/// <param name="radius1">长半轴。Default: 100.0</param>
	/// <param name="radius2">短半轴。Default: 60.0</param>
	/// <remarks>
	///   <para>半轴与 phi 的注意点见 <see cref="GenEllipse(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 594）：本重载 <c>StoreD</c> 直写、无元组固定与
	///   <c>UnpinTuple</c>；单个椭圆用本重载。</para>
	/// </remarks>
	public void GenEllipse(double row, double column, double phi, double radius1, double radius2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(594);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, radius1);
		JlNativeApi.StoreD(proc, 4, radius2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   由圆心、半径与起止角生成圆扇形区域（原地生成）。
	/// </summary>
	/// <param name="row">圆心的行坐标。Default: 200.0</param>
	/// <param name="column">圆心的列坐标。Default: 200.0</param>
	/// <param name="radius">圆半径。Default: 100.5</param>
	/// <param name="startAngle">扇形起始角（弧度）。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角（弧度）。Default: 3.14159</param>
	/// <remarks>
	///   <para><b>约束</b>本接口只有一个 <c>radius</c>（"Radius of circle"），生成的是实心扇形；
	///   想要圆环扇区需自建两扇再 <c>Difference</c> [待实测：是否本就支持环带]。
	///   0° 对应哪个方向、角度正负 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>整圆用 <c>GenCircle</c>（id 596）；椭圆楔形用
	///   <c>GenEllipseSector</c>（id 593）。<c>endAngle - startAngle</c> 超过 2π 时的行为 [待实测]。</para>
	///   <para><b>重载选择</b>本重载原生 id 595，元组 <c>Store</c> 固定后 <c>UnpinTuple</c>，
	///   可一次并行生成多个扇形（英文原文为复数）。实例被原地写入（<c>Dispose</c> + <c>Load</c>）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion cam = new JlRegion();
	///   cam.GenCircleSector(240.0, 320.0, 150.0, -0.5236, 0.5236);   // ±30° 视场扇区
	///   </code>
	/// </remarks>
	public void GenCircleSector(JlTuple row, JlTuple column, JlTuple radius, JlTuple startAngle, JlTuple endAngle)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(595);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.Store(proc, 3, startAngle);
		JlNativeApi.Store(proc, 4, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(startAngle);
		JlNativeApi.UnpinTuple(endAngle);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个圆扇形（标量版）。
	/// </summary>
	/// <param name="row">圆心的行坐标。Default: 200.0</param>
	/// <param name="column">圆心的列坐标。Default: 200.0</param>
	/// <param name="radius">圆半径。Default: 100.5</param>
	/// <param name="startAngle">扇形起始角（弧度）。Default: 0.0</param>
	/// <param name="endAngle">扇形终止角（弧度）。Default: 3.14159</param>
	/// <remarks>
	///   <para>取角与环带的存疑点见
	///   <see cref="GenCircleSector(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>。两个重载同一原生算子
	///   （id 595）：本重载 <c>StoreD</c> 直写、无元组固定与 <c>UnpinTuple</c>；单个扇形用本重载。</para>
	/// </remarks>
	public void GenCircleSector(double row, double column, double radius, double startAngle, double endAngle)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(595);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.StoreD(proc, 3, startAngle);
		JlNativeApi.StoreD(proc, 4, endAngle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   由圆心与半径生成实心圆区域（原地生成，可一次多个）。
	/// </summary>
	/// <param name="row">圆心的行坐标。Default: 200.0</param>
	/// <param name="column">圆心的列坐标。Default: 200.0</param>
	/// <param name="radius">圆半径。Default: 100.5</param>
	/// <remarks>
	///   <para><b>等价写法</b><c>new JlRegion(row, column, radius)</c> 构造器走同一原生 id 596；
	///   拿到即弃的场景用构造器，复用同一实例反复写入时用本方法。实例被原地改写
	///   （<c>Dispose</c> 后 <c>Load</c>），旧内容销毁。</para>
	///   <para><b>易错</b>元组重载可一次生成多个圆（英文原文为复数）；半径 ≤ 0 的行为 [待实测]。
	///   圆心允许在画幅外，超界像素仍保留在区域里，与图像求交时才被裁掉 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("lens.hobj");
	///   JlRegion blob = image.Threshold(90.0, 255.0);
	///   JlRegion ring = new JlRegion();
	///   ring.GenCircle(240.0, 320.0, 10.0);
	///   using JlRegion annulus = blob.Difference(ring);
	///   blob.Dispose();
	///   ring.Dispose();
	///   </code>
	/// </remarks>
	public void GenCircle(JlTuple row, JlTuple column, JlTuple radius)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(596);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单个圆（标量版）。
	/// </summary>
	/// <param name="row">圆心的行坐标。Default: 200.0</param>
	/// <param name="column">圆心的列坐标。Default: 200.0</param>
	/// <param name="radius">圆半径。Default: 100.5</param>
	/// <remarks>
	///   <para>与圆形构造器的等价关系见 <see cref="GenCircle(JlTuple,JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 596）：本重载 <c>StoreD</c> 直写、无元组固定与
	///   <c>UnpinTuple</c>；单个圆用本重载。</para>
	/// </remarks>
	public void GenCircle(double row, double column, double radius)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(596);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create a checkered region.
	/// </summary>
	/// <param name="widthRegion">Largest occurring $x$ value of the region. Default: 511</param>
	/// <param name="heightRegion">Largest occurring $y$ value of the region. Default: 511</param>
	/// <param name="widthPattern">Width of a field of the checkerboard. Default: 64</param>
	/// <param name="heightPattern">Height of a field of the checkerboard. Default: 64</param>
	/// <remarks>
	///   <para><b>功能说明</b>生成棋盘格区域（原生 id 597，四个参数 <c>StoreI</c> 直写）：
	///   范围由最大坐标 <c>widthRegion</c>/<c>heightRegion</c> 界定（默认 511 即 0..511 共 512 列/行），
	///   每格尺寸 <c>widthPattern</c>×<c>heightPattern</c>。原地生成（<c>Dispose</c>+<c>Load</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要栅格线用 <c>GenGridRegion</c>（id 598）；要整幅均匀网格
	///   ROI 逐个取块，可对棋盘 <c>Connection</c>——但注意棋盘格对角相触时 4-连通会拆成更多块。</para>
	///   <para><b>坑</b>左上角格子的极性（黑/白谁起始）托管层未注明 [待实测]；参数给非整除尺寸时
	///   边缘半格的处理 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion board = new JlRegion();
	///   board.GenCheckerRegion(511, 511, 64, 64);
	///   </code>
	///   <para><b>资源与坑</b>结果面积约为范围一半，供掩膜时注意取补集即反相棋盘。</para>
	/// </remarks>
	public void GenCheckerRegion(int widthRegion, int heightRegion, int widthPattern, int heightPattern)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(597);
		JlNativeApi.StoreI(proc, 0, widthRegion);
		JlNativeApi.StoreI(proc, 1, heightRegion);
		JlNativeApi.StoreI(proc, 2, widthPattern);
		JlNativeApi.StoreI(proc, 3, heightPattern);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create a region from lines or pixels.
	/// </summary>
	/// <param name="rowSteps">Step width in line direction or zero. Default: 10</param>
	/// <param name="columnSteps">Step width in column direction or zero. Default: 10</param>
	/// <param name="type">Type of created pattern. Default: "lines"</param>
	/// <param name="width">Maximum width of pattern. Default: 512</param>
	/// <param name="height">Maximum height of pattern. Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>生成线栅格或点阵区域（原生 id 598）：<c>type='lines'</c> 为栅格线、
	///   点阵取值 [待实测]；<c>rowSteps</c>/<c>columnSteps</c> 在本重载以元组传入，可逐行/逐列给
	///   不等间距（0 或负值的语义 [待实测]），<c>width</c>/<c>height</c> 限出图案范围。
	///   原地生成（<c>Dispose</c>+<c>Load</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>棋盘是实心格、本算子是线/点；做分辨率测试卡、
	///   标定板掩膜、逐格 ROI 用本算子。要每格单独取用，线栅格没法直接 <c>Connection</c> 成格，
	///   改用 <c>GenCheckerRegion</c>（id 597）反推或逐格 <c>GenRectangle1</c>。</para>
	///   <para><b>重载选择</b>等间距请用 <see cref="GenGridRegion(int,int,string,int,int)"/>：同一 id 598，
	///   步长走 <c>StoreI</c> 直写、无元组固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion grid = new JlRegion();
	///   grid.GenGridRegion(new JlTuple(10.0, 14.0), new JlTuple(10.0, 14.0), "lines", 512, 512);
	///   </code>
	///   <para><b>资源与坑</b>线宽恒为 1 像素 [待实测]；栅格总面积大，逐像素遍历慎用。</para>
	/// </remarks>
	public void GenGridRegion(JlTuple rowSteps, JlTuple columnSteps, string type, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(598);
		JlNativeApi.Store(proc, 0, rowSteps);
		JlNativeApi.Store(proc, 1, columnSteps);
		JlNativeApi.StoreS(proc, 2, type);
		JlNativeApi.StoreI(proc, 3, width);
		JlNativeApi.StoreI(proc, 4, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowSteps);
		JlNativeApi.UnpinTuple(columnSteps);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create a region from lines or pixels.
	/// </summary>
	/// <param name="rowSteps">Step width in line direction or zero. Default: 10</param>
	/// <param name="columnSteps">Step width in column direction or zero. Default: 10</param>
	/// <param name="type">Type of created pattern. Default: "lines"</param>
	/// <param name="width">Maximum width of pattern. Default: 512</param>
	/// <param name="height">Maximum height of pattern. Default: 512</param>
	/// <remarks>
	///   <para>栅格语义见 <see cref="GenGridRegion(JlTuple,JlTuple,string,int,int)"/>。两个重载同一
	///   原生算子（id 598）：本重载步长为标量、<c>StoreI</c> 直写（等间距栅格），无元组固定与
	///   <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void GenGridRegion(int rowSteps, int columnSteps, string type, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(598);
		JlNativeApi.StoreI(proc, 0, rowSteps);
		JlNativeApi.StoreI(proc, 1, columnSteps);
		JlNativeApi.StoreS(proc, 2, type);
		JlNativeApi.StoreI(proc, 3, width);
		JlNativeApi.StoreI(proc, 4, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create random regions like circles, rectangles and ellipses.
	/// </summary>
	/// <param name="type">Type of regions to be created. Default: "circle"</param>
	/// <param name="widthMin">Minimum object characteristic, depending on type and value. Default: 10.0</param>
	/// <param name="widthMax">Maximum object characteristic, depending on type and value. Default: 20.0</param>
	/// <param name="heightMin">Minimum object characteristic, depending on type and value. Default: 10.0</param>
	/// <param name="heightMax">Maximum object characteristic, depending on type and value. Default: 30.0</param>
	/// <param name="phiMin">Minimum rotation angle of the region. Default: -0.7854</param>
	/// <param name="phiMax">Maximum rotation angle of the region. Default: 0.7854</param>
	/// <param name="numRegions">Number of regions. Default: 100</param>
	/// <param name="width">Maximum horizontal expansion of the centers. Default: 512</param>
	/// <param name="height">Maximum vertical expansion of the centers. Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>一次生成 <c>numRegions</c> 个随机区域（原生 id 599）：<c>type</c>
	///   选形状（默认 "circle"，矩形/椭圆对应字面量 [待实测]），尺寸在
	///   [<c>widthMin</c>,<c>widthMax</c>]×[<c>heightMin</c>,<c>heightMax</c>] 内取、转角在
	///   [<c>phiMin</c>,<c>phiMax</c>]（弧度）内取，中心散布在 <c>width</c>×<c>height</c> 内。
	///   本重载六个区间参数以元组 <c>Store</c> 固定、调用后逐个 <c>UnpinTuple</c>，
	///   可给逐对象不同的区间 [待实测：配对规则]。原地生成（<c>Dispose</c>+<c>Load</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要一团不规则连通体用 <c>GenRandomRegion</c>（id 589）；
	///   本算子产出的是几何可辨的圆/矩/椭元组，适合造筛选、排序、计数的测试数据。</para>
	///   <para><b>坑</b>无种子参数，两次运行数据不同；区域可能相互重叠，测"CountObj 应等于
	///   numRegions"之前要先确认画幅足够大，重叠/出界时数量是否保持 [待实测]。</para>
	///   <para><b>重载选择</b>统一区间用 <see cref="GenRandomRegions(string,double,double,double,double,double,double,int,int,int)"/>：
	///   同一 id 599，六个标量 <c>StoreD</c> 直写、无固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion dots = new JlRegion();
	///   dots.GenRandomRegions("circle", new JlTuple(10.0), new JlTuple(20.0), new JlTuple(10.0),
	///       new JlTuple(20.0), new JlTuple(-0.7854), new JlTuple(0.7854), 50, 512, 512);
	///   int n = dots.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>圆时 width/height 区间如何映射到半径 [待实测]。</para>
	/// </remarks>
	public void GenRandomRegions(string type, JlTuple widthMin, JlTuple widthMax, JlTuple heightMin, JlTuple heightMax, JlTuple phiMin, JlTuple phiMax, int numRegions, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(599);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.Store(proc, 1, widthMin);
		JlNativeApi.Store(proc, 2, widthMax);
		JlNativeApi.Store(proc, 3, heightMin);
		JlNativeApi.Store(proc, 4, heightMax);
		JlNativeApi.Store(proc, 5, phiMin);
		JlNativeApi.Store(proc, 6, phiMax);
		JlNativeApi.StoreI(proc, 7, numRegions);
		JlNativeApi.StoreI(proc, 8, width);
		JlNativeApi.StoreI(proc, 9, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(widthMin);
		JlNativeApi.UnpinTuple(widthMax);
		JlNativeApi.UnpinTuple(heightMin);
		JlNativeApi.UnpinTuple(heightMax);
		JlNativeApi.UnpinTuple(phiMin);
		JlNativeApi.UnpinTuple(phiMax);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create random regions like circles, rectangles and ellipses.
	/// </summary>
	/// <param name="type">Type of regions to be created. Default: "circle"</param>
	/// <param name="widthMin">Minimum object characteristic, depending on type and value. Default: 10.0</param>
	/// <param name="widthMax">Maximum object characteristic, depending on type and value. Default: 20.0</param>
	/// <param name="heightMin">Minimum object characteristic, depending on type and value. Default: 10.0</param>
	/// <param name="heightMax">Maximum object characteristic, depending on type and value. Default: 30.0</param>
	/// <param name="phiMin">Minimum rotation angle of the region. Default: -0.7854</param>
	/// <param name="phiMax">Maximum rotation angle of the region. Default: 0.7854</param>
	/// <param name="numRegions">Number of regions. Default: 100</param>
	/// <param name="width">Maximum horizontal expansion of the centers. Default: 512</param>
	/// <param name="height">Maximum vertical expansion of the centers. Default: 512</param>
	/// <remarks>
	///   <para>随机区域族的语义与"无种子不可复现"见
	///   <see cref="GenRandomRegions(string,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,int,int,int)"/>。
	///   两个重载同一原生算子（id 599）：本重载六个区间为标量、<c>StoreD</c> 直写，
	///   无元组固定与 <c>UnpinTuple</c>；全体对象共用一组区间时用本重载。</para>
	/// </remarks>
	public void GenRandomRegions(string type, double widthMin, double widthMax, double heightMin, double heightMax, double phiMin, double phiMax, int numRegions, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(599);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreD(proc, 1, widthMin);
		JlNativeApi.StoreD(proc, 2, widthMax);
		JlNativeApi.StoreD(proc, 3, heightMin);
		JlNativeApi.StoreD(proc, 4, heightMax);
		JlNativeApi.StoreD(proc, 5, phiMin);
		JlNativeApi.StoreD(proc, 6, phiMax);
		JlNativeApi.StoreI(proc, 7, numRegions);
		JlNativeApi.StoreI(proc, 8, width);
		JlNativeApi.StoreI(proc, 9, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Hesse 法线式（法向角 + 到原点距离）生成直线区域（原地生成）。
	/// </summary>
	/// <param name="orientation">法向矢量的方向（弧度）。Default: 0.0</param>
	/// <param name="distance">直线到坐标原点的距离。Default: 200</param>
	/// <remarks>
	///   <para><b>与相邻算子的取舍</b>拟合直线、画刻度一类的场合以法线式参数最稳
	///   （与线的长短无关）；两端点式用 <c>GenRegionLine</c>（id 601）。</para>
	///   <para><b>约束</b>原生 id 600；原地生成（<c>Dispose</c> + <c>Load</c>）。
	///   直线画到哪里为止、法向角相对行轴还是列轴、坐标原点位于图像哪一角 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion horiz = new JlRegion();
	///   horiz.GenRegionHline(0.0, 200.0);   // orientation/distance 均可用 JlTuple 批量
	///   </code>
	/// </remarks>
	public void GenRegionHline(JlTuple orientation, JlTuple distance)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(600);
		JlNativeApi.Store(proc, 0, orientation);
		JlNativeApi.Store(proc, 1, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(orientation);
		JlNativeApi.UnpinTuple(distance);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单条 Hesse 法线式直线区域（标量版）。
	/// </summary>
	/// <param name="orientation">法向矢量的方向（弧度）。Default: 0.0</param>
	/// <param name="distance">直线到坐标原点的距离。Default: 200</param>
	/// <remarks>
	///   <para>法线式的坐标系存疑点见 <see cref="GenRegionHline(JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 600）：本重载 <c>StoreD</c> 直写、无元组固定与
	///   <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void GenRegionHline(double orientation, double distance)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(600);
		JlNativeApi.StoreD(proc, 0, orientation);
		JlNativeApi.StoreD(proc, 1, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   由两端点生成线段像素区域（原地生成，可一次多条）。
	/// </summary>
	/// <param name="beginRow">起点行坐标。Default: 100</param>
	/// <param name="beginCol">起点列坐标。Default: 50</param>
	/// <param name="endRow">终点行坐标。Default: 150</param>
	/// <param name="endCol">终点列坐标。Default: 250</param>
	/// <remarks>
	///   <para><b>与相邻算子的取舍</b>本算子（id 601）长度由两端点决定；无界法线式用
	///   <c>GenRegionHline</c>（id 600）。要做测量参考线而非掩膜，轮廓族更合适（本文件外）。</para>
	///   <para><b>约束</b>原地生成（<c>Dispose</c> + <c>Load</c>）；端点为整数像素索引；
	///   大斜率时栅格化是否断格 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion rule = new JlRegion();
	///   rule.GenRegionLine(100, 50, 100, 250);   // 180 px 水平线
	///   int n = rule.Area;                        // 粗查线的像素规模（Area 即属性）
	///   </code>
	/// </remarks>
	public void GenRegionLine(JlTuple beginRow, JlTuple beginCol, JlTuple endRow, JlTuple endCol)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(601);
		JlNativeApi.Store(proc, 0, beginRow);
		JlNativeApi.Store(proc, 1, beginCol);
		JlNativeApi.Store(proc, 2, endRow);
		JlNativeApi.Store(proc, 3, endCol);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(beginRow);
		JlNativeApi.UnpinTuple(beginCol);
		JlNativeApi.UnpinTuple(endRow);
		JlNativeApi.UnpinTuple(endCol);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成单条线段区域（四个整数端点版）。
	/// </summary>
	/// <param name="beginRow">起点行坐标。Default: 100</param>
	/// <param name="beginCol">起点列坐标。Default: 50</param>
	/// <param name="endRow">终点行坐标。Default: 150</param>
	/// <param name="endCol">终点列坐标。Default: 250</param>
	/// <remarks>
	///   <para>多线与栅格化存疑点见 <see cref="GenRegionLine(JlTuple,JlTuple,JlTuple,JlTuple)"/>。
	///   两个重载同一原生算子（id 601）：本重载 <c>StoreI</c> 直写一条线的四个端点，
	///   无元组固定与 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void GenRegionLine(int beginRow, int beginCol, int endRow, int endCol)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(601);
		JlNativeApi.StoreI(proc, 0, beginRow);
		JlNativeApi.StoreI(proc, 1, beginCol);
		JlNativeApi.StoreI(proc, 2, endRow);
		JlNativeApi.StoreI(proc, 3, endCol);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create an empty region.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>把本实例改写为一个空区域（原生 id 603：无控制参数，
	///   方法体先 <c>Dispose()</c> 再 <c>Load</c> 回本实例）。空区域是"初始化但零像素"，
	///   与未初始化实例不同。</para>
	///   <para><b>用途</b>并集累加器的正确起点：<c>acc.GenEmptyRegion()</c> 后循环
	///   <c>acc = acc.Union2(more)</c>——用 <c>null</c> 起头会在第一轮炸掉，直接拿 uninitialized
	///   实例参与运算原生层会报错。</para>
	///   <para><b>与相邻操作的取舍</b>只要判空用 <c>CountObj()==0</c> 或面积元组长度；
	///   要清空整个句柄里全部元素，本算子会把 N 个元素压成 1 个空元素（元素数变化 [待实测]）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion acc = new JlRegion();
	///   acc.GenEmptyRegion();
	///   for (int i = 1; i &lt;= 4; i++)
	///   {
	///       JlRegion part = image.Threshold(100.0 * i, 255.0);
	///       acc = acc.Union2(part);
	///       part.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>无返回值；空区域参与 <c>Complement</c> 得到全平面，注意级联。</para>
	/// </remarks>
	public void GenEmptyRegion()
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(603);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Access the thickness of a region along the main axis.
	/// </summary>
	/// <param name="histogramm">Histogram of the thickness of the region along its main axis.</param>
	/// <returns>Thickness of the region along its main axis.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>沿区域主轴量"厚度"（原生 id 616）：返回值与 <c>out</c> 都以
	///   INTEGER 元组装载（<c>JlTuple.LoadNew</c>）——一个给主轴方向上的厚度序列，
	///   一个给这些厚度值本身的直方图。</para>
	///   <para><b>与相邻算子的取舍</b><c>DistanceTransform</c>（id 475）给出逐像素距离场，
	///   信息全但要自己归约；本算子直接给"这条线有多粗"的一维剖面，量焊缝、划痕、印刷线宽
	///   用它。两者对分叉骨架的处理差异 [待实测]。</para>
	///   <para><b>参数取向</b>返回值 1 个 + <c>out</c> 1 个，调用处必须写 <c>out</c>；
	///   主轴的判定依据（矩方向？）以实现为准 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("scratch.hobj");
	///   JlRegion scratch = image.Threshold(140.0, 255.0);
	///   JlTuple prof = scratch.GetRegionThickness(out JlTuple histo);
	///   scratch.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个元组都要 Dispose；直方图非零项个数即不同厚度档数 [待实测]。</para>
	/// </remarks>
	public JlTuple GetRegionThickness(out JlTuple histogramm)
	{
		IntPtr proc = JlNativeApi.PreCall(616);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out histogramm);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Polygon approximation of a region.
	/// </summary>
	/// <param name="tolerance">Maximum distance between the polygon and the edge of the region. Default: 5.0</param>
	/// <param name="rows">Line numbers of the base points of the contour.</param>
	/// <param name="columns">Column numbers of the base points of the contour.</param>
	/// <remarks>
	///   <para><b>功能说明</b>对区域轮廓做多边形逼近（原生 id 617）：<c>rows</c>/<c>columns</c>
	///   以 INTEGER 元组装载，给出基点的先行后列坐标；本重载 <c>tolerance</c> 以元组 <c>Store</c>
	///   固定后 <c>UnpinTuple</c>，可逐对象给不同容差 [待实测：与对象的配对方式]。</para>
	///   <para><b>与相邻算子的取舍</b>要逐像素轮廓序列用 <c>GetRegionContour</c>（id 619）、
	///   要链码用 <c>GetRegionChain</c>（id 621）；本算子给"少而准"的角点，
	///   容差即"多边形与真实边缘的最大偏离"，调大顶点变少。</para>
	///   <para><b>坑</b>输出是点序列，本身不再是区域——画回去要用
	///   <c>GenRegionPolygon</c>（id 584）/多边形填充版，别指望它自动闭合时把最后一段补上 [待实测]。
	///   多对象输入时各对象顶点连接在同一对元组里还是分块 [待实测]。</para>
	///   <para><b>重载选择</b>统一容差用 <see cref="GetRegionPolygon(double,out JlTuple,out JlTuple)"/>：
	///   同一 id 617，<c>StoreD</c> 直写、无固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("plate.hobj").Threshold(100.0, 255.0);
	///   part.GetRegionPolygon(new JlTuple(5.0), out JlTuple rows, out JlTuple cols);
	///   int n = rows.Length;
	///   rows.Dispose();
	///   cols.Dispose();
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无 iconic 输出，不修改输入。</para>
	/// </remarks>
	public void GetRegionPolygon(JlTuple tolerance, out JlTuple rows, out JlTuple columns)
	{
		IntPtr proc = JlNativeApi.PreCall(617);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, tolerance);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tolerance);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rows);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out columns);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Polygon approximation of a region.
	/// </summary>
	/// <param name="tolerance">Maximum distance between the polygon and the edge of the region. Default: 5.0</param>
	/// <param name="rows">Line numbers of the base points of the contour.</param>
	/// <param name="columns">Column numbers of the base points of the contour.</param>
	/// <remarks>
	///   <para>多边形逼近语义见 <see cref="GetRegionPolygon(JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 617）：本重载 <c>StoreD</c> 直写单一容差、无元组固定与
	///   <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void GetRegionPolygon(double tolerance, out JlTuple rows, out JlTuple columns)
	{
		IntPtr proc = JlNativeApi.PreCall(617);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, tolerance);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rows);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out columns);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Access the pixels of a region.
	/// </summary>
	/// <param name="rows">Line numbers of the pixels in the region</param>
	/// <param name="columns">Column numbers of the pixels in the region.</param>
	/// <remarks>
	///   <para><b>功能说明</b>把区域内全部像素展开成两个 INTEGER 元组（原生 id 618）：
	///   第 i 个像素是 (<c>rows[i]</c>, <c>columns[i]</c>)，先行后列、row=y 向下、column=x 向右。</para>
	///   <para><b>与相邻算子的取舍</b>这是最重的取法——规模随面积线性增长，百万像素的区域
	///   别这样导出；只要边界用 <c>GetRegionContour</c>（id 619），要紧凑表达用
	///   <c>GetRegionRuns</c>（id 620），判"某点在不在里面"根本不用取像素，
	///   <c>TestRegionPoint*</c> 是查询、本算子是枚举，二者别混。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 10.0, 20.0, 30.0);
	///   roi.GetRegionPoints(out JlTuple rows, out JlTuple cols);
	///   int n = rows.Length;
	///   rows.Dispose();
	///   cols.Dispose();
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无返回值、两个 <c>out</c>；像素排列次序 [待实测]。</para>
	/// </remarks>
	public void GetRegionPoints(out JlTuple rows, out JlTuple columns)
	{
		IntPtr proc = JlNativeApi.PreCall(618);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rows);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out columns);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Access the contour of an object.
	/// </summary>
	/// <param name="rows">Line numbers of the contour pixels.</param>
	/// <param name="columns">Column numbers of the contour pixels.</param>
	/// <remarks>
	///   <para><b>功能说明</b>给出区域边界像素的 INTEGER 坐标序列（原生 id 619，两个 <c>out</c>
	///   均 <c>JlTuple.LoadNew</c> INTEGER 装载）。这里的"轮廓"仍是像素集，不是 XLD 亚像素轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>要全部像素用 <c>GetRegionPoints</c>（id 618）、要边界"线"区域
	///   用 <c>Boundary</c>（id 715）、要角点近似用 <c>GetRegionPolygon</c>（id 617）；
	///   本算子适合对边界逐点算灰度剖面、检查边缘完整性。</para>
	///   <para><b>坑</b>多对象输入时边界序列如何分隔/是否首尾相接 [待实测]；内孔边界是否包含在内
	///   [待实测]——与 <c>Boundary</c> 的 inner/outer 语义对照着验证。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("ring.hobj").Threshold(100.0, 255.0);
	///   part.GetRegionContour(out JlTuple rows, out JlTuple cols);
	///   int n = rows.Length;
	///   rows.Dispose();
	///   cols.Dispose();
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无返回值；两个元组用完各自 Dispose。</para>
	/// </remarks>
	public void GetRegionContour(out JlTuple rows, out JlTuple columns)
	{
		IntPtr proc = JlNativeApi.PreCall(619);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rows);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out columns);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Access the runlength coding of a region.
	/// </summary>
	/// <param name="row">Line numbers of the chords.</param>
	/// <param name="columnBegin">Column numbers of the starting points of the chords.</param>
	/// <param name="columnEnd">Column numbers of the ending points of the chords.</param>
	/// <remarks>
	///   <para><b>功能说明</b>导出区域的游程编码（原生 id 620）：三个 INTEGER 元组并行，
	///   第 i 条游程是 <c>row[i]</c> 行上 <c>columnBegin[i]</c> 到 <c>columnEnd[i]</c> 的水平段。
	///   这就是区域的原生存储形式，比逐像素导出紧凑得多。</para>
	///   <para><b>与相邻算子的取舍</b>大区域导出/序列化用本算子；随机访问单像素归属还是留在
	///   区域句柄里用查询族。<c>GetRegionPoints</c>（id 618）的规模是 O(面积)，本算子是 O(游程数)。</para>
	///   <para><b>坑</b>与 <c>GenRegionRuns</c>（id 586）互为逆操作，端点是否含 <c>columnEnd</c>
	///   两边必须同一理解 [待实测]；游程按行、行内按列升序排列 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 5.0, 12.0, 40.0);
	///   roi.GetRegionRuns(out JlTuple row, out JlTuple begin, out JlTuple end);
	///   int nRuns = row.Length;
	///   row.Dispose();
	///   begin.Dispose();
	///   end.Dispose();
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三个 <c>out</c> 都要写 <c>out</c>、都要 Dispose。</para>
	/// </remarks>
	public void GetRegionRuns(out JlTuple row, out JlTuple columnBegin, out JlTuple columnEnd)
	{
		IntPtr proc = JlNativeApi.PreCall(620);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out columnBegin);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out columnEnd);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Contour of an object as chain code.
	/// </summary>
	/// <param name="row">Line of starting point.</param>
	/// <param name="column">Column of starting point.</param>
	/// <param name="chain">Direction code of the contour (from starting point).</param>
	/// <remarks>
	///   <para><b>功能说明</b>以链码给出区域轮廓（原生 id 621）：起点 (<c>row</c>,<c>column</c>)
	///   经 <c>LoadI</c> 是标量 int，方向序列 <c>chain</c> 以 INTEGER 元组装载。</para>
	///   <para><b>与相邻算子的取舍</b>比较两个轮廓的形变、做轮廓哈希用链码（平移不变、序列短）；
	///   要坐标点用 <c>GetRegionContour</c>（id 619）。链码字母表是 4 向还是 8 向、方向编号起点
	///   [待实测]，跨实现移植数据前必须核实。</para>
	///   <para><b>参数取向</b>3 个 <c>out</c>（前两个 <c>int</c>、第三个 <c>JlTuple</c>），
	///   类型不同别写错；多对象输入时 <c>row</c>/<c>column</c> 只能容纳一个起点，
	///   多轮廓如何降级 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("blob.hobj").Threshold(100.0, 255.0);
	///   part.GetRegionChain(out int r0, out int c0, out JlTuple chain);
	///   int steps = chain.Length;
	///   chain.Dispose();
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>起点随行/列变化即整套链码移位，先归一化再比较 [待实测]。</para>
	/// </remarks>
	public void GetRegionChain(out int row, out int column, out JlTuple chain)
	{
		IntPtr proc = JlNativeApi.PreCall(621);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out row);
		err = JlNativeApi.LoadI(proc, 1, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out chain);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Access convex hull as contour.
	/// </summary>
	/// <param name="rows">Line numbers of contour pixels.</param>
	/// <param name="columns">Column numbers of the contour pixels.</param>
	/// <remarks>
	///   <para><b>功能说明</b>输出凸包轮廓的 INTEGER 点序列（原生 id 622）——注意是"点序列"，
	///   不是凸区域本身。</para>
	///   <para><b>与相邻算子的取舍</b>要能继续做布尔运算的实心凸区域用
	///   <c>ShapeTrans("convex")</c>（id 498）；只要凸包顶点坐标（多边形拟合、CAD 回转）用本算子。
	///   两者一次调用都拿全 [待实测：点数与顶点简化程度]。</para>
	///   <para><b>坑</b>凹口多的轮廓凸包顶点少、噪声轮廓的凸包会被单个噪声点撑大——
	///   先 <c>RemoveNoiseRegion</c>（id 497）再取凸包；多对象时各凸包的连接方式 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("bracket.hobj").Threshold(100.0, 255.0);
	///   part.GetRegionConvex(out JlTuple rows, out JlTuple cols);
	///   int n = rows.Length;
	///   rows.Dispose();
	///   cols.Dispose();
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无返回值、两个 <c>out</c>；回转区域用 <c>GenRegionPolygonFilled</c>（id 583）。</para>
	/// </remarks>
	public void GetRegionConvex(out JlTuple rows, out JlTuple columns)
	{
		IntPtr proc = JlNativeApi.PreCall(622);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rows);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out columns);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

























	/// <summary>
	///   Sorting of regions with respect to their relative position.
	/// </summary>
	/// <param name="sortMode">Kind of sorting. Default: "first_point"</param>
	/// <param name="order">Increasing or decreasing sorting order. Default: "true"</param>
	/// <param name="rowOrCol">Sorting first with respect to row, then to column. Default: "row"</param>
	/// <returns>Sorted regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按位置关系重排对象元组（原生 id 703）：<c>sortMode</c> 在本重载以
	///   元组 <c>Store</c> 固定后 <c>UnpinTuple</c>，默认 "first_point"；<c>order</c> 控制升/降序，
	///   <c>rowOrCol</c> 决定先行后列还是反过来。返回重排后的新句柄。</para>
	///   <para><b>为什么需要它</b><c>Connection</c> 的产出顺序由拆分算法决定、用户无法指定，
	///   阈值或图像一变序号就漂移；任何后续按 <c>SelectObj</c>/<c>CopyObj</c> 序号取用的流程，
	///   都应先 <c>SortRegion</c> 定序，否则静默拿错目标。本库没有通用的 <c>SortObj</c>，
	///   区域族的定序手段就是本算子。</para>
	///   <para><b>坑</b>排序键的可选字面量清单托管层未枚举 [待实测]；等分位（同行两个目标）
	///   时的次级排序按 <c>rowOrCol</c> 说的"先×后×"展开 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion chars = new JlImage("text.hobj").Threshold(0.0, 100.0)
	///       .Connection().SortRegion("first_point", "true", "row");
	///   JlRegion third = chars.SelectObj(3);
	///   chars.Dispose();
	///   third.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；元素像素不变，只是顺序变。</para>
	/// </remarks>
	public JlRegion SortRegion(JlTuple sortMode, string order, string rowOrCol)
	{
		IntPtr proc = JlNativeApi.PreCall(703);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sortMode);
		JlNativeApi.StoreS(proc, 1, order);
		JlNativeApi.StoreS(proc, 2, rowOrCol);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sortMode);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}





	/// <summary>
	///   Prune the branches of a region.
	/// </summary>
	/// <param name="length">Length of the branches to be removed. Default: 2</param>
	/// <returns>Result of the pruning operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>剪掉骨架上长度小于 <c>length</c> 的毛刺分支（原生 id 714，
	///   <c>StoreI</c> 直写），主干保留。</para>
	///   <para><b>前提</b>它是对细化结果的后处理：输入应是 <c>Skeleton</c>（id 476）或
	///   Thinning 族的 1 像素宽产物，直接对实心区域用不是它的职责 [待实测：非骨架输入的表现]。</para>
	///   <para><b>与相邻算子的取舍</b><c>RemoveNoiseRegion</c>（id 497）删孤点、<c>RankRegion</c>
	///   （id 490）按邻域计数，都不认识"分支"这个概念；笔画交叉处生出的短毛刺只有本算子能剪。
	///   <c>length</c> 越大剪得越狠，超过真实笔画长度的参数会把目标剪没。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion skel = new JlImage("hand.hobj").Threshold(0.0, 100.0).Skeleton();
	///   using JlRegion tidy = skel.Pruning(5);
	///   skel.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；剪完后端点/分叉数变化，<c>JunctionsSkeleton</c> 要重跑。</para>
	/// </remarks>
	public JlRegion Pruning(int length)
	{
		IntPtr proc = JlNativeApi.PreCall(714);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, length);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把区域收缩为它的一圈边界像素，可选内边界或外边界。
	/// </summary>
	/// <param name="boundaryType">边界类型：取区域内侧像素还是外侧像素。Default: "inner"</param>
	/// <returns>边界区域的新句柄；调用者句柄不变。</returns>
	/// <remarks>
	///   <para><b>inner 与 outer 的实际差别</b>两种字面量都在 <c>StoreS</c> 里以字符串透传
	///   （原生 id 715），拼错不会在托管层被拦下。按语义理解 inner 的边界像素取自区域内部、
	///   面积不变，outer 向外扩一圈；孔洞边界是否同样保留 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>本库没有 <c>Contregion</c>（全仓检索不存在），
	///   想要"区域轮廓线"就用本成员。洞也要边时先 <c>FillUp</c> 再求边、用
	///   <c>Difference</c> 反推会多绕一步，直接 <c>Boundary</c> 即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stamp.hobj");
	///   JlRegion blobs = image.Threshold(128.0, 255.0);
	///   using JlRegion edge = blobs.Boundary("inner");
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>元组输入逐对象生成边界；边界区域像素数远小于原区域，
	///   下游按面积筛选的阈值要重设。其他 boundaryType 字面量 [待实测]。</para>
	/// </remarks>
	public JlRegion Boundary(string boundaryType)
	{
		IntPtr proc = JlNativeApi.PreCall(715);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, boundaryType);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Perform a closing after an opening with multiple structuring elements.
	/// </summary>
	/// <param name="structElements">Structuring elements.</param>
	/// <returns>Fitted regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对一串结构元素逐个做"先开后闭"并取拟合度最好者（原生 id 716，
	///   英文原文 closing-after-opening）：<c>structElements</c> 是结构元素<b>元组</b>（多个尺寸/形状），
	///   区域与哪些细结构相容就被哪些保留。</para>
	///   <para><b>与相邻算子的取舍</b><c>OpeningCircle</c>/<c>ClosingCircle</c> 只有单一圆形元素、
	///   一步开或闭；本算子用元素族做组合筛选，适合"保留与某几种笔画宽度匹配的几何"的场合，
	///   代价是每个元素都要跑一遍。</para>
	///   <para><b>坑</b>元素来自 <c>GenStructElements</c>（id 717）时注意参考点位置直接决定结果；
	///   开闭复合会移动小特征，位置敏感的量测要放到拟合之后重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion se = new JlRegion();
	///   se.GenStructElements("rect", 3, 3);
	///   JlRegion part = new JlImage("print.hobj").Threshold(0.0, 128.0);
	///   using JlRegion fitted = part.Fitting(se);
	///   part.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；"rect" 等字面量清单托管层未枚举 [待实测]。</para>
	/// </remarks>
	public JlRegion Fitting(JlRegion structElements)
	{
		IntPtr proc = JlNativeApi.PreCall(716);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElements);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElements);
		return obj;
	}

	/// <summary>
	///   Generate standard structuring elements.
	/// </summary>
	/// <param name="type">Type of structuring element to generate. Default: "noise"</param>
	/// <param name="row">Row coordinate of the reference point. Default: 1</param>
	/// <param name="column">Column coordinate of the reference point. Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b>生成标准结构元素（原生 id 717，默认 type "noise"）：原地生成
	///   （<c>Dispose</c>+<c>Load</c>），<c>row</c>/<c>column</c> 指定参考点在元素内的位置。</para>
	///   <para><b>参考点是命门</b>腐蚀/膨胀/击中以参考点对齐，同一个元素换个参考点结果完全不同：
	///   默认 1/1 是角点语义，想让元素绕中心作用就把参考点放到元素中心（尺寸的一半）。</para>
	///   <para><b>与相邻算子的取舍</b>规则圆形/矩形元素直接用 <c>ErosionCircle</c>/
	///   <c>DilationCircle</c>/<c>OpeningCircle</c> 一族免构造；本算子供 <c>Fitting</c>（id 716）、
	///   <c>Thinning</c>/<c>Thickening</c>（id 721/724）与 <c>HitOrMiss</c> 族提供自定义元素。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion se = new JlRegion();
	///   se.GenStructElements("noise", 1, 1);
	///   int n = se.CountObj();   // 元素数与 type 清单 [待实测]
	///   </code>
	///   <para><b>资源与坑</b>type 可用字面量托管层未枚举 [待实测]。</para>
	/// </remarks>
	public void GenStructElements(string type, int row, int column)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(717);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, row);
		JlNativeApi.StoreI(proc, 2, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Reflect a region about a point.
	/// </summary>
	/// <param name="row">Row coordinate of the reference point. Default: 0</param>
	/// <param name="column">Column coordinate of the reference point. Default: 0</param>
	/// <returns>Transposed region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>关于点 (<c>row</c>,<c>column</c>) 做点对称（原生 id 718，
	///   参考点 <c>StoreI</c> 整数直写）：像素 (r,c) 映到 (2·row−r, 2·column−c)，等价旋转 180°。</para>
	///   <para><b>与相邻算子的取舍</b>轴对称用 <c>MirrorRegion</c>（id 479）——一次镜像翻转手性，
	///   两次镜像（先横后纵）才等于本算子；任意角旋转走 <c>AffineTransRegion</c>。
	///   检查"转 180° 后是否重合"的中心对称件直接用它配 <c>TestEqualRegion</c>。</para>
	///   <para><b>坑</b>参考点是整数，奇偶对齐会影响落格；变换后质心变到对称点，绝对位置特征重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("rotor.hobj").Threshold(100.0, 255.0);
	///   using JlRegion turned = part.TransposeRegion(240, 320);
	///   bool centerSym = part.TestEqualRegion(turned) == 1;
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；turned 在比较后自行 Dispose。</para>
	/// </remarks>
	public JlRegion TransposeRegion(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(718);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Remove the result of a hit-or-miss operation from a region (sequential).
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "l"</param>
	/// <param name="iterations">Number of iterations. For 'f', 'f2', 'h' and 'i' the only useful value is 1. Default: 20</param>
	/// <returns>Result of the thinning operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>序列细化：按 Golay 字母表的元素逐个"击中即删"（原生 id 719）。
	///   与 <c>ThinningGolay</c>（id 720）并行一次删一圈不同，序列版按顺序连续施加同一元素
	///   <c>iterations</c> 次，每轮的删除结果立即影响下一轮，结果与元素顺序相关。</para>
	///   <para><b>前提与选择</b><c>golayElement</c> 默认 "l"；参数文档注明 'f'/'f2'/'h'/'i' 只对
	///   1 次迭代有意义，多迭代请换端点类元素。本重载 <c>iterations</c> 以元组 <c>Store</c>
	///   固定后 <c>UnpinTuple</c>，可逐对象给次数。</para>
	///   <para><b>与相邻算子的取舍</b>只要中轴用 <c>Skeleton</c>（id 476）；要端到端细化成 1 像素
	///   用 ThinningGolay 或本算子把 iterations 给足；细化会改变全部特征值，之后再量。</para>
	///   <para><b>重载选择</b>统一次数用 <see cref="ThinningSeq(string,int)"/>：同一 id 719，
	///   <c>StoreI</c> 直写、无固定与 <c>UnpinTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("maze.hobj").Threshold(128.0, 255.0);
	///   using JlRegion thin = part.ThinningSeq("l", new JlTuple(20.0));
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；迭代次数足够大时是否自动收敛 [待实测]。</para>
	/// </remarks>
	public JlRegion ThinningSeq(string golayElement, JlTuple iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(719);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.Store(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Remove the result of a hit-or-miss operation from a region (sequential).
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "l"</param>
	/// <param name="iterations">Number of iterations. For 'f', 'f2', 'h' and 'i' the only useful value is 1. Default: 20</param>
	/// <returns>Result of the thinning operator.</returns>
	/// <remarks>
	///   <para>序列细化语义见 <see cref="ThinningSeq(string,JlTuple)"/>。两个重载同一原生算子
	///   （id 719）：本重载 <c>iterations</c> 为标量、<c>StoreI</c> 直写，无元组固定与
	///   <c>UnpinTuple</c>。</para>
	/// </remarks>
	public JlRegion ThinningSeq(string golayElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(719);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Remove the result of a hit-or-miss operation from a region (using a Golay structuring element).
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "h"</param>
	/// <param name="rotation">Rotation of the Golay element. Depending on the element, not all rotations are valid. Default: 0</param>
	/// <returns>Result of the thinning operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>并行 Golay 细化：用选定元素+旋转角一次性对所有命中位置删除
	///   （原生 id 720，<c>golayElement</c> 默认 "h"、<c>rotation</c> 默认 0，<c>StoreS</c>+<c>StoreI</c> 直写）。</para>
	///   <para><b>与相邻算子的取舍</b>与 <c>ThinningSeq</c>（id 719）的分界是并行/顺序：并行版
	///   每轮对称处理、不偏向先扫到的位置，但一轮只能削一薄层，要"一次削到底"应循环调用
	///   本算子直到稳定 [待实测：迭代到收敛的判定]；顺序版单元素连打多次，端点保留性质不同。
	///   并非所有元素都有全部旋转角，非法组合的行为 [待实测]。</para>
	///   <para><b>前提</b>细化保拓扑不保尺寸，结果面积、矩、圆度全部重算；"h" 类元素单次有效，
	///   反复调用会持续腐蚀成点。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("circuit.hobj").Threshold(128.0, 255.0);
	///   using JlRegion thin = part.ThinningGolay("r", 0);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion ThinningGolay(string golayElement, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(720);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Remove the result of a hit-or-miss operation from a region.
	/// </summary>
	/// <param name="structElement1">Structuring element for the foreground.</param>
	/// <param name="structElement2">Structuring element for the background.</param>
	/// <param name="row">Row coordinate of the reference point. Default: 0</param>
	/// <param name="column">Column coordinate of the reference point. Default: 0</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Result of the thinning operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>通用细化（原生 id 721）：以两个自建结构元素定义击中条件——
	///   <c>structElement1</c> 对前景、<c>structElement2</c> 对背景（补集），命中者从区域移除，
	///   重复 <c>iterations</c> 次；参考点 (<c>row</c>,<c>column</c>) 对齐元素。</para>
	///   <para><b>与相邻算子的取舍</b>Golay 族（id 719/720）元素现成、免拼装，常规细化优先它们；
	///   本算子供自定义删除规则（保留特定局部形状）用。元素用 <c>GenStructElements</c>（id 717）
	///   或几何算子拼。</para>
	///   <para><b>坑</b>两个元素与参考点三者共同定义规则，参考点错位等于换了一个元素；
	///   iterations 给大可能整条消失，细化量先小步试。默认参考点 0/0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion se1 = new JlRegion(0.0, 0.0, 0.0, 2.0);
	///   JlRegion se2 = new JlRegion(1.0, 0.0, 1.0, 2.0);
	///   JlRegion part = new JlImage("wire.hobj").Threshold(128.0, 255.0);
	///   using JlRegion thin = part.Thinning(se1, se2, 1, 1, 1);
	///   part.Dispose();
	///   se1.Dispose();
	///   se2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；两个元素在返回前不得 Dispose。</para>
	/// </remarks>
	public JlRegion Thinning(JlRegion structElement1, JlRegion structElement2, int row, int column, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(721);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement1);
		JlNativeApi.Store(proc, 3, structElement2);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement1);
		GC.KeepAlive(structElement2);
		return obj;
	}

	/// <summary>
	///   Add the result of a hit-or-miss operation to a region (sequential).
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "h"</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Result of the thickening operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>序列增厚：对命中位置做"击中即加"（原生 id 722，
	///   <c>StoreS</c>+<c>StoreI</c> 直写），是 Thinning 族的对偶——细化的逆操作，
	///   在凹陷与孔洞边缘补回像素，序列施加 <c>iterations</c> 次。</para>
	///   <para><b>与相邻算子的取舍</b><c>ThickeningGolay</c>（id 723）并行一步；普通膨胀
	///   <c>DilationCircle</c> 无差别外扩、会破坏拓扑，本族只在满足击中条件的局部加像素、
	///   保拓扑。修补细化过度的断裂时先小次数试。</para>
	///   <para><b>坑</b>与膨胀一样改变全部特征值；增厚不限制在画幅内 [待实测]。
	///   默认元素 "h" 与旋转角的合法组合 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion skel = new JlImage("crack.hobj").Threshold(150.0, 255.0).Skeleton();
	///   using JlRegion mended = skel.ThickeningSeq("h", 2);
	///   skel.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion ThickeningSeq(string golayElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(722);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Add the result of a hit-or-miss operation to a region (using a Golay structuring element).
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "h"</param>
	/// <param name="rotation">Rotation of the Golay element. Depending on the element, not all rotations are valid. Default: 0</param>
	/// <returns>Result of the thickening operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>并行 Golay 增厚（原生 id 723）：选定元素+旋转角，一轮对所有命中
	///   空位补像素；与 <c>ThinningGolay</c>（id 720）严格对偶。</para>
	///   <para><b>与相邻算子的取舍</b>顺序版用 <c>ThickeningSeq</c>（id 722）；本算子一轮一层、
	///   对称处理。"细化后恢复一点"这类需求配 <c>Skeleton</c>+本算子，而不是回头做膨胀——
	///   膨胀不限位置会把骨架整体加粗。</para>
	///   <para><b>坑</b>元素/旋转角非法组合的行为 [待实测]；增厚后特征值全部重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion skel = new JlImage("line.hobj").Threshold(128.0, 255.0).Skeleton();
	///   using JlRegion fat = skel.ThickeningGolay("h", 0);
	///   skel.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion ThickeningGolay(string golayElement, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(723);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Add the result of a hit-or-miss operation to a region.
	/// </summary>
	/// <param name="structElement1">Structuring element for the foreground.</param>
	/// <param name="structElement2">Structuring element for the background.</param>
	/// <param name="row">Row coordinate of the reference point. Default: 16</param>
	/// <param name="column">Column coordinate of the reference point. Default: 16</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Result of the thickening operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>通用增厚（原生 id 724）：两个自建元素（前景掩膜 <c>structElement1</c>、
	///   背景掩膜 <c>structElement2</c>）+参考点 (<c>row</c>,<c>column</c>) 定义命中条件，
	///   命中处补进区域，重复 <c>iterations</c> 次；是 <c>Thinning</c>（id 721）的对偶。</para>
	///   <para><b>与相邻算子的取舍</b>Golay 族（id 722/723）现成免拼装优先；本算子仅在需要
	///   自定义补点规则时用。参考点默认 16/16——元素尺寸与参考点要配套，忘了改会整体偏移。</para>
	///   <para><b>坑</b>两个元素的相对位置决定规则，随便换其一都可能把"补缝"变成"糊死"；
	///   结果特征值重算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion se1 = new JlRegion(0.0, 0.0, 0.0, 1.0);
	///   JlRegion se2 = new JlRegion(1.0, 0.0, 1.0, 1.0);
	///   JlRegion part = new JlImage("trace.hobj").Threshold(128.0, 255.0);
	///   using JlRegion padded = part.Thickening(se1, se2, 0, 0, 1);
	///   part.Dispose();
	///   se1.Dispose();
	///   se2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；元素在返回前不得 Dispose。</para>
	/// </remarks>
	public JlRegion Thickening(JlRegion structElement1, JlRegion structElement2, int row, int column, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(724);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement1);
		JlNativeApi.Store(proc, 3, structElement2);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement1);
		GC.KeepAlive(structElement2);
		return obj;
	}

	/// <summary>
	///   Hit-or-miss operation for regions using the Golay alphabet (sequential).
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "h"</param>
	/// <returns>Result of the hit-or-miss operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>击中即中检测（原生 id 725）：输出"元素在区域内、且其背景部分
	///   在区域外"的参考点集合（一个新句柄，<c>golayElement</c> 默认 "h"）。</para>
	///   <para><b>与相邻算子的取舍</b><c>ThinningSeq</c>（id 719）内部同款检测但把命中点"从区域里
	///   删掉"；本算子只报告命中位置不动原区域——找角点、端点、交叉点位置用本算子，
	///   细化才用 Thinning 族。<c>HitOrMissGolay</c>（id 726）是并行版带旋转。</para>
	///   <para><b>坑</b>结果区域的坐标系是参考点坐标（不是元素覆盖范围），拿去和原图叠加显示时
	///   差一个参考点偏移；元素合法字面量与旋转约束 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("corner.hobj").Threshold(128.0, 255.0);
	///   using JlRegion hits = part.HitOrMissSeq("c");
	///   int nHits = hits.Area.Length;
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion HitOrMissSeq(string golayElement)
	{
		IntPtr proc = JlNativeApi.PreCall(725);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Hit-or-miss operation for regions using the Golay alphabet.
	/// </summary>
	/// <param name="golayElement">Structuring element from the Golay alphabet. Default: "h"</param>
	/// <param name="rotation">Rotation of the Golay element. Depending on the element, not all rotations are valid. Default: 0</param>
	/// <returns>Result of the hit-or-miss operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>并行 Golay 击中即中（原生 id 726）：一次报告所有命中参考点，
	///   <c>rotation</c> 选元素旋转角。</para>
	///   <para><b>与相邻算子的取舍</b>序列版 <c>HitOrMissSeq</c>（id 725）按序处理、
	///   本算子无迭代概念；完全自定义两掩膜用 <c>HitOrMiss</c>（id 727，本文件后文）。
	///   找四种角就选 "b" 系元素配 4 个旋转角跑 4 次 [待实测：字面量与角的对应]。</para>
	///   <para><b>坑</b>输出是点集区域，坐标语义同参考点位置；非法元素/旋转组合 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion part = new JlImage("frame.hobj").Threshold(128.0, 255.0);
	///   using JlRegion corners = part.HitOrMissGolay("b", 0);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄。</para>
	/// </remarks>
	public JlRegion HitOrMissGolay(string golayElement, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(726);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Hit-or-miss operation for regions.
	/// </summary>
	/// <param name="structElement1">Erosion mask for the input regions.</param>
	/// <param name="structElement2">Erosion mask for the complements of the input regions.</param>
	/// <param name="row">Row coordinate of the reference point. Default: 16</param>
	/// <param name="column">Column coordinate of the reference point. Default: 16</param>
	/// <returns>Result of the hit-or-miss operation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>双掩膜命中判断（原生 id 727）：把 <c>structElement1</c> 当作前景掩膜
	///   放进取中位置得一点、把 <c>structElement2</c> 当作背景掩膜放到同一参考位置后与输入区域的
	///   补集完全吻合，输出这些位置的集合；输入区域本身不变，返回新句柄。</para>
	///   <para><b>前提</b>两个掩膜通常成对来自 <c>GolayElements</c>（id 728）；参考点默认
	///   (16,16) 是按 33×33 的 Golay 模板中心配的，换成自制小模板时 <c>row</c>/<c>column</c>
	///   必须改成模板的真实参考点，否则结果整体平移。</para>
	///   <para><b>与相邻算子的取舍</b>比 <c>Erosion1</c> 多一个背景掩膜，能表达"此处有前景且
	///   彼处必须无前景"的端点/分叉判据；背景掩膜取空区域时是否退化为普通腐蚀 [待实测]。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c> 以 <c>StoreI</c> 作 INTEGER 控制参数传入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(100.0, 20.0, 100.0, 200.0);
	///   JlRegion seFg = new JlRegion(0.0, 0.0, 3.0).OpeningCircle(1.5);
	///   JlRegion seBg = new JlRegion(0.0, 0.0, 3.0);
	///   using JlRegion hits = obj.HitOrMiss(seFg, seBg, 2, 2);
	///   obj.Dispose();
	///   seFg.Dispose();
	///   seBg.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的新句柄用完 <c>Dispose</c>；命中结果逐区域独立计算。</para>
	/// </remarks>
	public JlRegion HitOrMiss(JlRegion structElement1, JlRegion structElement2, int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(727);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement1);
		JlNativeApi.Store(proc, 3, structElement2);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement1);
		GC.KeepAlive(structElement2);
		return obj;
	}

	/// <summary>
	///   Generate the structuring elements of the Golay alphabet.
	/// </summary>
	/// <param name="golayElement">Name of the structuring element. Default: "l"</param>
	/// <param name="rotation">Rotation of the Golay element. Depending on the element, not all rotations are valid. Default: 0</param>
	/// <param name="row">Row coordinate of the reference point. Default: 16</param>
	/// <param name="column">Column coordinate of the reference point. Default: 16</param>
	/// <returns>Structuring element for the background.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>生成 Golay 字母表的一个结构元（原生 id 728）。实现先
	///   <c>Dispose()</c> 掉当前句柄再 <c>Load(proc, 1)</c>：调用后 <c>this</c> 本身变成前景
	///   结构元（SE），返回的新句柄才是配套的背景结构元（SEB），两者一起喂给
	///   <c>HitOrMiss</c>/<c>ThinningGolay</c> 一族。</para>
	///   <para><b>前提</b><c>golayElement</c> 取 "l"/"e" 等字母；旋转只对方位对称的元素有效，
	///   非法组合的原生报错文本 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要整套细化流程直接用 <c>ThinningSeq</c>/<c>ThinningGolay</c>，
	///   它们内部自己取元素；本方法用于自制命中/细化判据。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c> 是返回模板的参考点（默认 (16,16) 对应
	///   33×33 模板中心）；<c>StoreS</c>/<c>StoreI</c> 直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion se = new JlRegion();
	///   using JlRegion seBg = se.GolayElements("l", 0, 16, 16);
	///   // 调用之后 se 已被替换为前景模板，可继续喂给 HitOrMiss
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>调用前 <c>this</c> 的旧内容即被释放，不要指望它还在；返回值独立
	///   句柄要 <c>Dispose</c>。</para>
	/// </remarks>
	public JlRegion GolayElements(string golayElement, int rotation, int row, int column)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(728);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, rotation);
		JlNativeApi.StoreI(proc, 2, row);
		JlNativeApi.StoreI(proc, 3, column);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		err = LoadNew(proc, 2, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Thinning of a region.
	/// </summary>
	/// <param name="iterations1">Number of iterations for the sequential thinning with the element 'l' of the Golay alphabet. Default: 100</param>
	/// <param name="iterations2">Number of iterations for the sequential thinning with the element 'e' of the Golay alphabet. Default: 1</param>
	/// <returns>Result of the skiz operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>顺序细化组合（原生 id 729）：先用 Golay 元素 "l" 迭代至多
	///   <c>iterations1</c> 次、再用 "e" 迭代至多 <c>iterations2</c> 次，产出逐区域 1 像素宽的
	///   骨架状区域；输入不变，返回新句柄。</para>
	///   <para><b>参数取向</b>本重载把两个迭代数当 <c>JlTuple</c> 用 <c>Store</c> 钉固定位、
	///   调用后 <c>UnpinTuple</c>；<see cref="MorphSkiz(int,int)"/> 用 <c>StoreI</c> 直写，同一 id。
	///   上限给小了细化不收敛、给大了只是白耗时间（无像素可删即停）。</para>
	///   <para><b>与相邻算子的取舍</b>要各向同性且带旋转遍历的用 <c>ThinningGolay</c>；要现成
	///   骨架用 <c>MorphSkeleton</c>（id 730）。细化后面积/轮廓全部失真，特征要回原区域算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stroke.hobj");
	///   JlRegion obj = image.Threshold(0.0, 100.0).Connection();
	///   using JlRegion thin = obj.MorphSkiz(new JlTuple(100.0), new JlTuple(1.0));
	///   obj.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；毛刺剪枝接 <c>Pruning</c>（id 714）。</para>
	/// </remarks>
	public JlRegion MorphSkiz(JlTuple iterations1, JlTuple iterations2)
	{
		IntPtr proc = JlNativeApi.PreCall(729);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, iterations1);
		JlNativeApi.Store(proc, 1, iterations2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations1);
		JlNativeApi.UnpinTuple(iterations2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Thinning of a region.
	/// </summary>
	/// <param name="iterations1">Number of iterations for the sequential thinning with the element 'l' of the Golay alphabet. Default: 100</param>
	/// <param name="iterations2">Number of iterations for the sequential thinning with the element 'e' of the Golay alphabet. Default: 1</param>
	/// <returns>Result of the skiz operator.</returns>
	/// <remarks>
	///   <para>顺序细化的语义与迭代数取法见
	///   <see cref="MorphSkiz(JlTuple,JlTuple)"/>。两个重载同一原生算子（id 729）：本重载把
	///   两个迭代数用 <c>StoreI</c> 直写为 INTEGER 控制参数，无钉固定与 <c>UnpinTuple</c>；
	///   单对迭代数（常规情形）用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stroke.hobj");
	///   JlRegion obj = image.Threshold(0.0, 100.0).Connection();
	///   using JlRegion thin = obj.MorphSkiz(100, 1);
	///   obj.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion MorphSkiz(int iterations1, int iterations2)
	{
		IntPtr proc = JlNativeApi.PreCall(729);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, iterations1);
		JlNativeApi.StoreI(proc, 1, iterations2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the morphological skeleton of a region.
	/// </summary>
	/// <returns>Resulting morphological skeleton.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>形态学骨架（原生 id 730）：对每个输入区域求 1 像素宽的拓扑骨架，
	///   保持连通性与端点/分叉结构；输入不变，逐区域返回新句柄元组。</para>
	///   <para><b>与相邻算子的取舍</b><c>Skeleton</c>（id 476）走的是距离变换式骨架、
	///   <c>MorphSkiz</c>（id 729）是 Golay 顺序细化，三者对毛刺与斜线段的处理各不相同，
	///   笔画类目标建议都试一遍再定 [待实测：三者输出差异]。骨架上再跑 <c>Pruning</c> 去毛刺。</para>
	///   <para><b>参数取向</b>无参数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("circuit.hobj");
	///   JlRegion obj = image.Threshold(0.0, 128.0).Connection();
	///   using JlRegion skel = obj.MorphSkeleton();
	///   obj.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>边界上 1 像素的凹凸会在骨架里长成假分支；返回句柄要
	///   <c>Dispose</c>。骨架的面积/矩无物理意义，别拿去当特征。</para>
	/// </remarks>
	public JlRegion MorphSkeleton()
	{
		IntPtr proc = JlNativeApi.PreCall(730);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   Compute the union of bottom_hat and top_hat.
	/// </summary>
	/// <param name="structElement">Structuring element (position-invariant).</param>
	/// <returns>Union of top hat and bottom hat.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>礼帽与黑礼帽的并集（原生 id 731）：<c>TopHat</c> 抓到区域中
	///   放不进结构元的凸出部分，<c>BottomHat</c> 抓到补集中被闭合出来的凹进部分，两者拼成
	///   全部"结构元装不下"的形状细节；输入不变，返回新句柄。</para>
	///   <para><b>前提</b><c>structElement</c> 是位置无关结构元——运算按参考点对齐展开，
	///   不是逐点平移模板。</para>
	///   <para><b>与相邻算子的取舍</b>只要凸起用 <c>TopHat</c>（id 733）、只要凹陷用
	///   <c>BottomHat</c>（id 732）；两类都要（比如检测毛边加缺口）才用本方法，省一次拼接。</para>
	///   <para><b>参数取向</b>结构元句柄 <c>Store</c> 为第二输入，<c>GC.KeepAlive</c> 保活到调用结束。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(100.0, 100.0, 0.3, 60.0, 40.0, 0.0, 6.28);
	///   JlRegion se = new JlRegion(100.0, 100.0, 12.0);
	///   using JlRegion detail = obj.MorphHat(se);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄要 <c>Dispose</c>；空区域输入返回空结果不报错 [待实测]。</para>
	/// </remarks>
	public JlRegion MorphHat(JlRegion structElement)
	{
		IntPtr proc = JlNativeApi.PreCall(731);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   Compute the bottom hat of regions.
	/// </summary>
	/// <param name="structElement">Structuring element (position independent).</param>
	/// <returns>Result of the bottom hat operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>黑礼帽（原生 id 732）：先对区域做 <c>Closing(structElement)</c>
	///   再减去原区域，得到的就是"结构元能盖住、但原区域缺着"的外部凹陷与缝隙；返回新句柄，
	///   输入不变。</para>
	///   <para><b>与相邻算子的取舍</b>找区域内部凸出用 <c>TopHat</c>（id 733），两边都要用
	///   <c>MorphHat</c>（id 731）。凹口宽度小于结构元尺寸时会被一并填掉，尺寸要按最小
	///   待检缺口定。</para>
	///   <para><b>参数取向</b>结构元尺寸决定"多小的凹不抓"，通常取圆或矩形模板。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(20.0, 20.0, 180.0, 180.0);
	///   JlRegion se = new JlRegion(100.0, 100.0, 8.0);
	///   using JlRegion dips = obj.BottomHat(se);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果位于原区域之外（补集侧），与 <c>Intersection</c> 类算子
	///   组合时注意坐标系同为 row 向下、column 向右。</para>
	/// </remarks>
	public JlRegion BottomHat(JlRegion structElement)
	{
		IntPtr proc = JlNativeApi.PreCall(732);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   Compute the top hat of regions.
	/// </summary>
	/// <param name="structElement">Structuring element (position independent).</param>
	/// <returns>Result of the top hat operator.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>礼帽（原生 id 733）：区域减去其 <c>Opening(structElement)</c>
	///   的结果，剩下的就是放不下结构元的凸出——毛刺、端点、局部变窄处；返回新句柄，输入不变。</para>
	///   <para><b>与相邻算子的取舍</b>找外侧凹陷用 <c>BottomHat</c>（id 732）；找内侧细颈/凸出
	///   用本方法。抓 1 像素毛刺用半径 1 的圆模板即可，模板一大只剩宏观轮廓变化。</para>
	///   <para><b>参数取向</b>结构元按第二输入 <c>Store</c> 传入并 <c>KeepAlive</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(20.0, 20.0, 180.0, 180.0);
	///   JlRegion se = new JlRegion(100.0, 100.0, 5.0);
	///   using JlRegion bumps = obj.TopHat(se);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄要 <c>Dispose</c>；结果全部落在原区域内，可直接
	///   <c>Difference</c> 回原区域做"去掉毛刺"的修补。</para>
	/// </remarks>
	public JlRegion TopHat(JlRegion structElement)
	{
		IntPtr proc = JlNativeApi.PreCall(733);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   Erode a region (using a reference point).
	/// </summary>
	/// <param name="structElement">Structuring element.</param>
	/// <param name="row">Row coordinate of the reference point. Default: 0</param>
	/// <param name="column">Column coordinate of the reference point. Default: 0</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Eroded regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>带参考点的腐蚀（原生 id 734）：把结构元平移使其参考点落在
	///   (<c>row</c>, <c>column</c>)，再做 Minkowski 差；迭代 <c>iterations</c> 次。返回新句柄，
	///   输入不变。</para>
	///   <para><b>前提</b>参考点是相对结构元自身坐标的像素索引（row 向下、column 向右）；
	///   和 <c>Erosion2</c> 的区别在运算定义——这里走 Minkowski 差的平移族语义 [待实测：两者输出是否恒等]。</para>
	///   <para><b>与相邻算子的取舍</b>结构元参考点本来就在 (0,0) 时用 <c>MinkowskiSub1</c>（id 735）
	///   更省事；非对称模板要从"尖端"蚀起时才需要本方法调参考点。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c>/<c>iterations</c> 全走 <c>StoreI</c> 整数直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(20.0, 20.0, 180.0, 180.0);
	///   JlRegion se = new JlRegion(0.0, 0.0, 0.0, 5.0, 3.0, 0.0, 6.28);
	///   using JlRegion eroded = obj.MinkowskiSub2(se, 0, 0, 1);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>腐蚀到空后继续迭代仍为空，不报错；返回句柄要 <c>Dispose</c>。</para>
	/// </remarks>
	public JlRegion MinkowskiSub2(JlRegion structElement, int row, int column, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(734);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   Erode a region.
	/// </summary>
	/// <param name="structElement">Structuring element.</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Eroded regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>Minkowski 差腐蚀（原生 id 735）：直接用结构元自带的参考点，
	///   迭代 <c>iterations</c> 次；返回新句柄，输入不变。</para>
	///   <para><b>与相邻算子的取舍</b>要临时挪参考点用 <c>MinkowskiSub2</c>（id 734）；
	///   常规圆/矩形腐蚀用 <c>ErosionCircle</c>/<c>ErosionRectangle1</c>，走各自的优化路径。</para>
	///   <para><b>参数取向</b><c>iterations</c> 为 <c>StoreI</c> 整数；传 0 或负数的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(20.0, 20.0, 180.0, 180.0);
	///   JlRegion se = new JlRegion(0.0, 0.0, 4.0);
	///   using JlRegion eroded = obj.MinkowskiSub1(se, 2);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>腐蚀后面积、矩、轮廓全部改变，特征须重算；返回句柄要 <c>Dispose</c>。</para>
	/// </remarks>
	public JlRegion MinkowskiSub1(JlRegion structElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(735);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   Dilate a region (using a reference point).
	/// </summary>
	/// <param name="structElement">Structuring element.</param>
	/// <param name="row">Row coordinate of the reference point.</param>
	/// <param name="column">Column coordinate of the reference point.</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Dilated regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>带参考点的膨胀（原生 id 736）：把结构元平移使其参考点落在
	///   (<c>row</c>, <c>column</c>) 后做 Minkowski 和，迭代 <c>iterations</c> 次；返回新句柄，
	///   输入不变。</para>
	///   <para><b>与相邻算子的取舍</b>不需要动参考点时用 <c>MinkowskiAdd1</c>（id 737）；
	///   各向同性外扩用 <c>DilationCircle</c>。膨胀会吞掉小于结构元一半间距的间隙，
	///   多区域输入先想清楚要不要在膨胀后再 <c>Connection</c>。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c>/<c>iterations</c> 走 <c>StoreI</c> 整数直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(20.0, 20.0, 100.0, 60.0);
	///   JlRegion se = new JlRegion(0.0, 0.0, 0.0, 6.0, 2.0, 0.0, 6.28);
	///   using JlRegion grown = obj.MinkowskiAdd2(se, 0, 0, 1);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果句柄要 <c>Dispose</c>；参考点给在模板边角上时扩张方向不对称，
	///   坐标会整体偏移。</para>
	/// </remarks>
	public JlRegion MinkowskiAdd2(JlRegion structElement, int row, int column, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(736);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   Perform a Minkowski addition on a region.
	/// </summary>
	/// <param name="structElement">Structuring element.</param>
	/// <param name="iterations">Number of iterations. Default: 1</param>
	/// <returns>Dilated regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>Minkowski 和膨胀（原生 id 737）：按结构元自带参考点做膨胀，
	///   迭代 <c>iterations</c> 次；返回新句柄，输入不变。</para>
	///   <para><b>与相邻算子的取舍</b>要挪参考点用 <c>MinkowskiAdd2</c>（id 736）；圆/矩形
	///   用 <c>DilationCircle</c>/<c>DilationRectangle1</c>。自制任意形状模板的加厚走本方法。</para>
	///   <para><b>参数取向</b><c>iterations</c> 为 <c>StoreI</c> 整数；0 或负值行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion obj = new JlRegion(20.0, 20.0, 100.0, 60.0);
	///   JlRegion se = new JlRegion(0.0, 0.0, 3.0);
	///   using JlRegion grown = obj.MinkowskiAdd1(se, 2);
	///   obj.Dispose();
	///   se.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>膨胀后面积/周长与矩全部改变，特征须重算；结果句柄要 <c>Dispose</c>。</para>
	/// </remarks>
	public JlRegion MinkowskiAdd1(JlRegion structElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(737);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   用矩形结构元做闭运算（先膨胀后腐蚀）。
	/// </summary>
	/// <param name="width">矩形结构元宽度（像素）。Default: 10</param>
	/// <param name="height">矩形结构元高度（像素）。Default: 10</param>
	/// <returns>闭运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>沿指定方向接合</b>宽高独立：想让断开的虚线沿列方向接上、又不把相邻两行虚线并成
	///   一片，就给 <c>width</c> 小、<c>height</c> 大的元素。圆盘做不到这种方向性。</para>
	///   <para><b>默认值</b>与 <c>OpeningRectangle1</c> 同为 10×10（腐蚀/膨胀的矩形版是 11×11）。
	///   偶数尺寸无正中像素，结果会偏半像素。</para>
	///   <para><b>合并风险</b>膨胀阶段先接合后腐蚀不会重新分开，凡涉及计数/面积，
	///   要先 <c>Connection</c> 再逐目标闭运算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("dash.hobj");
	///   JlRegion marks = image.Threshold(0.0, 100.0);
	///   using JlRegion joined = marks.ClosingRectangle1(1, 12);   // 沿行方向接虚线
	///   marks.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion ClosingRectangle1(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(738);
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
	///   用 Golay 字母表元素做闭运算（先膨胀后腐蚀，方向可选）。
	/// </summary>
	/// <param name="golayElement">Golay 字母表中的结构元。Default: "h"</param>
	/// <param name="rotation">元素旋转序号；并非每个字母都支持全部旋转。Default: 0</param>
	/// <returns>闭运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>没有 iterations</b>与 <c>DilationGolay</c>（754，含 <c>iterations</c>）不同，
	///   本算子只有 <c>(golayElement, rotation)</c>（id 739），一次只能补约一个元素宽的缝。
	///   缝更宽就改用 <c>ClosingCircle</c> 或 <c>ClosingRectangle1</c>；反复调本算子不等价于更大的
	///   结构元，会逐步把不该连的目标连起来。</para>
	///   <para><b>方向性补缝</b>沿笔画方向的元素可以接上断口而不把相邻笔画并块，这是闭运算里
	///   唯一可指定方向的入口（与 <c>OpeningGolay</c> 成对）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stroke.hobj");
	///   JlRegion glyphs = image.Threshold(0.0, 120.0);
	///   using JlRegion mended = glyphs.ClosingGolay("h", 0);
	///   glyphs.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion ClosingGolay(string golayElement, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(739);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   闭运算（先膨胀后腐蚀）：填掉小于圆盘的内孔与窄缝，外部尺寸基本不变。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。传元组可一次算多个半径。Default: 3.5</param>
	/// <returns>闭运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>与开运算相反的方向</b>开运算去"多余的小结构"，闭运算补"缺失的小结构"：
	///   目标内部的砂眼、被高光打断的窄缝、印刷断笔，都用小于其尺度的圆盘闭运算补回，
	///   且因为随后做一次同尺寸腐蚀，外部轮廓位置基本不动。</para>
	///   <para><b>它会顺带合并近邻</b>膨胀阶段会把间距小于约 2×radius 的两块连通起来，
	///   腐蚀阶段不会把它们重新分开。因此"先闭运算再 <c>Connection</c> 计数"会少数；
	///   若既要计数又要补孔，先 <c>Connection</c> 再对每个目标分别闭运算。</para>
	///   <para><b>不要拿它当补洞</b>闭运算只补小于元素的孔；要填全部背景洞（不管多大）用
	///   <c>FillUp</c> 一类的全洞填充，代价与语义都不同。</para>
	///   <para><b>重载选择</b>与 <c>ClosingCircle(double)</c> 同一原生算子（id 740）：本重载经
	///   <c>Store</c> 固定元组并在调用后 <c>UnpinTuple</c>，double 版用 <c>StoreD</c> 直写；
	///   半径确定时用 double 版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stamp.hobj");
	///   JlRegion ink = image.Threshold(0.0, 110.0);
	///   using JlRegion solid = ink.ClosingCircle(2.5);   // 补断笔，不放大字身
	///   ink.Dispose();
	///   </code>
	///   <para><b>默认值提示</b><c>ClosingRectangle1</c>/<c>OpeningRectangle1</c> 默认 10×10，
	///   而腐蚀/膨胀的矩形版默认 11×11。</para>
	/// </remarks>
	public JlRegion ClosingCircle(JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(740);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(radius);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   闭运算（单半径版）。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。Default: 3.5</param>
	/// <returns>闭运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para>定义、合并近邻的风险、与 <c>FillUp</c> 的分工见
	///   <see cref="ClosingCircle(JlTuple)"/>。两个重载同一原生算子（id 740），
	///   本版本用 <c>StoreD</c> 直写 double，不做元组固定与解固定，半径确定时应当用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("stamp.hobj");
	///   JlRegion ink = image.Threshold(0.0, 110.0);
	///   using JlRegion solid = ink.ClosingCircle(2.5);
	///   ink.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion ClosingCircle(double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(740);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用自定义区域作结构元做闭运算（平移不变形式）。
	/// </summary>
	/// <param name="structElement">结构元区域（平移不变）。</param>
	/// <returns>闭运算结果的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para><b>与 <c>Opening</c> 成对</b>两者都只此一个入口（id 741 / 746）：本族结构元是平移不变的，
	///   所以不存在 <c>Closing2</c> 之类的参考点重载，不必去找。</para>
	///   <para><b>何时用自定义元素</b>要按工件的真实间隙形状补缝时（例如只补某方向的接头），
	///   自建元素比圆盘/矩形准。代价是两次全区域形态学操作，比固定形状版慢。</para>
	///   <para><b>合并风险</b>闭运算会把能被元素跨过的间隙接成一块，接完不再分开；
	///   涉及计数或面积时先 <c>Connection</c> 再逐目标处理。</para>
	///   <para><b>结构元生命周期</b>作为第二个 iconic 句柄传入并由 <c>GC.KeepAlive</c> 保活，
	///   调用之后才可 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("joint.hobj");
	///   JlRegion parts = image.Threshold(90.0, 255.0);
	///   JlRegion element = new JlRegion(0.0, 0.0, 3.0, 14.0);   // 只跨水平方向
	///   using JlRegion closed = parts.Closing(element);
	///   element.Dispose();
	///   parts.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion Closing(JlRegion structElement)
	{
		IntPtr proc = JlNativeApi.PreCall(741);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   以自定义结构元做开运算类操作（原生 id 742）；上游原文写的是"分离重叠区域"，与名字不一致。
	/// </summary>
	/// <param name="structElement">结构元区域（平移不变形式）。</param>
	/// <returns>结果区域的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para><b>名字与语义冲突，别按名字选</b>本成员的英文原文摘要是 <c>Separate overlapping regions</c>
	///   （分离重叠的区域），但方法名叫 <c>OpeningSeg</c>、<c>&lt;returns&gt;</c> 又写 "Opened regions"——
	///   上游文档本身就自相矛盾。它与 <c>Opening</c>（746）的 C# 签名完全相同
	///   （<c>JlRegion Opening*(JlRegion structElement)</c>），只是打向不同的原生算子。</para>
	///   <para><b>怎么取舍</b>做常规开运算去噪请用 <c>OpeningCircle</c>/<c>OpeningRectangle1</c>
	///   或 <c>Opening</c>；只有确认目标是"拆开互相重叠的区域"时才试本成员。
	///   两者在原生侧的确切差异无法由托管层判定 [待实测]，不要用"猜等价"的方式互换。</para>
	///   <para><b>结构元生命周期</b>结构元作为第二个 iconic 句柄传入并由 <c>GC.KeepAlive</c> 保活，
	///   调用之后才可 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("coins.hobj");
	///   JlRegion blobs = image.Threshold(110.0, 255.0);
	///   JlRegion element = new JlRegion(0.0, 0.0, 20.0, 20.0);
	///   using JlRegion opened = blobs.OpeningSeg(element);
	///   element.Dispose();
	///   blobs.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion OpeningSeg(JlRegion structElement)
	{
		IntPtr proc = JlNativeApi.PreCall(742);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   用 Golay 字母表元素做开运算（先腐蚀后膨胀，方向可选）。
	/// </summary>
	/// <param name="golayElement">Golay 字母表中的结构元。Default: "h"</param>
	/// <param name="rotation">元素旋转序号；并非每个字母都支持全部旋转。Default: 0</param>
	/// <returns>开运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>没有 iterations</b>与 <c>ErosionGolay</c>/<c>DilationGolay</c> 不同（它们第三个参数是
	///   <c>iterations</c>），本算子只有 <c>(golayElement, rotation)</c>（原生 id 743），
	///   即一次开运算只推进一个元素宽度。要清理更大尺度请改用 <c>OpeningCircle</c> 或
	///   <c>OpeningRectangle1</c>，不要指望"多调几次"能等价——反复开运算会逐步侵蚀细结构。</para>
	///   <para><b>方向选择性</b>这是开运算里唯一能指定方向的入口：目标是一排彼此轻触的平行笔画时，
	///   沿笔画方向的元素能把粘连点断开而不截断笔画本身；圆盘开运算是各向同性的，做不到。</para>
	///   <para><b>合法 rotation 随字母变化</b>见 <c>ErosionGolay</c> 的同条说明 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("bars.hobj");
	///   JlRegion bars = image.Threshold(100.0, 255.0);
	///   using JlRegion opened = bars.OpeningGolay("h", 0);
	///   bars.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion OpeningGolay(string golayElement, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(743);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用矩形结构元做开运算（先腐蚀后膨胀）。
	/// </summary>
	/// <param name="width">矩形结构元宽度（像素）。Default: 10</param>
	/// <param name="height">矩形结构元高度（像素）。Default: 10</param>
	/// <returns>开运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>各向异性清理</b>宽高独立，因此可以"只沿一个方向吃"：
	///   给 <c>width</c> 大而 <c>height</c> 小的元素，会保留横贯的长条、抹掉竖向的细连接；
	///   反过来则保竖弃横。表格线、栅格、条码这类规则结构用矩形开运算比圆盘准。</para>
	///   <para><b>默认值</b>本算子与 <c>ClosingRectangle1</c> 是 10×10，而
	///   <c>ErosionRectangle1</c>/<c>DilationRectangle1</c> 是 11×11；两种默认都合法。
	///   偶数尺寸无正中像素，结果会偏半像素。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("grid.hobj");
	///   JlRegion lines = image.Threshold(0.0, 90.0);
	///   using JlRegion keepHoriz = lines.OpeningRectangle1(15, 1);   // 只留横向长线
	///   lines.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion OpeningRectangle1(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(744);
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
	///   开运算（先腐蚀后膨胀）：抹掉小于圆盘的结构，同时基本保持其余部分的尺寸与位置。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。传元组可一次算多个半径。Default: 3.5</param>
	/// <returns>开运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>为什么去噪该用它而不是 <c>ErosionCircle</c></b>腐蚀会把所有目标一起缩小，
	///   开运算在腐蚀之后补一次同尺寸膨胀，因此小于约 2×radius 的孤点、毛刺、细丝被清除，
	///   而较大目标的轮廓位置近似不变。凡是要保留尺寸的下采样预处理，用开运算。</para>
	///   <para><b>选择判据</b>开运算只保证"能被该圆盘整个放进去的像素保留"：
	///   宽度和长度都要够，细长目标照样会被打断。目标本身是细杆时，改用小半径或
	///   <c>OpeningRectangle1</c>（沿目标走向给长轴）。</para>
	///   <para><b>本族形状不对称（易踩）</b>开运算有 <c>OpeningCircle</c>/<c>OpeningRectangle1</c>/
	///   <c>OpeningGolay</c>/<c>Opening</c>/<c>OpeningSeg</c>，但<b>没有</b> <c>OpeningSeq</c>；
	///   且 <c>OpeningGolay</c> 只有 <c>(golayElement, rotation)</c>，比
	///   <c>ErosionGolay</c> 少一个 <c>iterations</c>。别按腐蚀族的直觉去找不存在的重载。</para>
	///   <para><b>默认值</b>本族的 <c>OpeningRectangle1</c>/<c>ClosingRectangle1</c> 默认 10×10，
	///   而 <c>ErosionRectangle1</c>/<c>DilationRectangle1</c> 默认 11×11；两者都合法，不要互相"纠正"。</para>
	///   <para><b>重载选择</b>与 <c>OpeningCircle(double)</c> 同一原生算子（id 745）：本重载经
	///   <c>Store</c> 固定元组并在调用后 <c>UnpinTuple</c>，double 版用 <c>StoreD</c> 直写；
	///   半径确定时用 double 版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("wafers.hobj");
	///   JlRegion raw = image.Threshold(120.0, 255.0);
	///   using JlRegion cleaned = raw.OpeningCircle(1.5);   // 去 dust 点，不缩目标
	///   int n = cleaned.Connection().CountObj();
	///   raw.Dispose();
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入不被修改。</para>
	///   <para><b>待确认</b>开运算后目标面积的漂移量、以及 radius ≤ 0 的行为 [待实测]。</para>
	/// </remarks>
	public JlRegion OpeningCircle(JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(745);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(radius);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   开运算（单半径版）。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。Default: 3.5</param>
	/// <returns>开运算结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para>定义、去噪取舍与本族形状差异见 <see cref="OpeningCircle(JlTuple)"/>。
	///   两个重载同一原生算子（id 745），本版本用 <c>StoreD</c> 直写 double，不做元组固定与解固定，
	///   半径确定时应当用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("wafers.hobj");
	///   JlRegion raw = image.Threshold(120.0, 255.0);
	///   using JlRegion cleaned = raw.OpeningCircle(1.5);
	///   raw.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion OpeningCircle(double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(745);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用自定义区域作结构元做开运算（平移不变形式）。
	/// </summary>
	/// <param name="structElement">结构元区域（平移不变）。</param>
	/// <returns>开运算结果的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para><b>为什么开运算没有锚点重载</b>上游对结构元参数的原文标注是 <i>position-invariant</i>
	///   （平移不变）。这正是腐蚀/膨胀有 <c>Erosion2</c>/<c>Dilation2</c> 参考点版本、
	///   而开/闭运算只有 <c>Opening</c>/<c>Closing</c> 一个入口的原因：闭包性质使锚点选择被抵消掉，
	///   所以不要去找不存在的 <c>Opening2</c>。</para>
	///   <para><b>何时用自定义元素</b>需要按工件实际形状清理时（例如元素就取目标的标准轮廓的补形），
	///   比自己拼圆盘/矩形更准。注意自定义元素的开运算代价是两次全区域形态学操作。</para>
	///   <para><b>与 <c>OpeningSeg</c> 的关系</b>两者 C# 签名相同、原生 id 不同（746 / 742），
	///   差异无法由托管层判定 [待实测]；常规去噪用本成员。</para>
	///   <para><b>结构元生命周期</b>作为第二个 iconic 句柄传入并由 <c>GC.KeepAlive</c> 保活，
	///   调用之后才可 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("seals.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0);
	///   JlRegion element = new JlRegion(0.0, 0.0, 6.0, 6.0);
	///   using JlRegion opened = blobs.Opening(element);
	///   element.Dispose();
	///   blobs.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion Opening(JlRegion structElement)
	{
		IntPtr proc = JlNativeApi.PreCall(746);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   反复应用同一个 Golay 字母表元素腐蚀区域（顺序迭代版）。
	/// </summary>
	/// <param name="golayElement">Golay 字母表中的结构元。Default: "h"</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>腐蚀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>与 <c>ErosionGolay</c> 的分工</b>本算子（原生 id 747）没有 <c>rotation</c> 形参，
	///   而 <c>ErosionGolay</c>（748）有：前者把"每次迭代用哪个方向的元素"交给实现自行安排，
	///   后者要求调用者自己指定。[待实测] <c>erosion_seq</c> 的常规语义是按顺序轮换元素方向，
	///   使多次迭代的等效结构元趋近圆盘——这正是大半径 <c>ErosionCircle</c> 的常用提速替代。</para>
	///   <para><b>何时用它</b>需要腐蚀掉宽度约 <c>iterations</c> 像素的细丝/毛刺时，比 <c>ErosionCircle</c>
	///   更可控：结果形状由元素方向决定，而不是被圆盘的各向同性削边。</para>
	///   <para><b>代价</b>每次迭代都是一遍全区域扫描，成本随 <c>iterations</c> 线性增长；
	///   区域很大时优先用 <c>OpeningCircle</c>/<c>ErosionCircle</c> 一次到位。[待实测]</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("wire.hobj");
	///   JlRegion wires = image.Threshold(90.0, 255.0);
	///   using JlRegion thick = wires.ErosionSeq("h", 3);   // 吃掉约 3 像素宽的细丝
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入区域不受影响，两者都需自行 Dispose。</para>
	/// </remarks>
	public JlRegion ErosionSeq(string golayElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(747);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 Golay 字母表元素腐蚀区域，旋转方向由调用者指定。
	/// </summary>
	/// <param name="golayElement">Golay 字母表中的结构元。Default: "h"</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <param name="rotation">元素旋转序号；并非每个字母都支持全部旋转。Default: 0</param>
	/// <returns>腐蚀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>与 <c>ErosionSeq</c> 的分工</b>747 把方向安排交给实现，本算子（748）把
	///   <c>rotation</c> 交给你。要的是"只沿一个方向吃掉"时用本重载，例如去掉单向拖尾的毛刺、
	///   或从参考边起算收缩量。</para>
	///   <para><b>合法旋转取决于字母</b>不同元素可用的 <c>rotation</c> 范围不一样，越界行为是报错
	///   还是取模 [待实测]；换元素时不要沿用同一个 rotation 值。</para>
	///   <para><b>等效尺寸</b>每次迭代只推进一个元素宽度，最终收缩量约等于
	///   <c>iterations</c> × 元素半径，因而可近似 <c>ErosionCircle</c> 但形状沿元素方向。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("text.hobj");
	///   JlRegion ink = image.Threshold(0.0, 120.0);
	///   using JlRegion thinned = ink.ErosionGolay("h", 1, 0);        // 单方向削一刀
	///   using JlRegion deeper = thinned.ErosionGolay("h", 1, 2);     // 换对面方向
	///   ink.Dispose();
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入区域不被修改；链式调用时中间结果也要 Dispose。</para>
	/// </remarks>
	public JlRegion ErosionGolay(string golayElement, int iterations, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(748);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.StoreI(proc, 2, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用矩形结构元腐蚀区域：横纵方向可分别设定收缩量。
	/// </summary>
	/// <param name="width">矩形结构元宽度（像素）。Default: 11</param>
	/// <param name="height">矩形结构元高度（像素）。Default: 11</param>
	/// <returns>腐蚀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>默认值不是笔误</b>本算子与 <c>DilationRectangle1</c> 的默认是 11×11，
	///   而 <c>OpeningRectangle1</c> / <c>ClosingRectangle1</c> 的默认是 10×10——同一族里两种取值，
	///   移植代码时不要互相"对齐"。奇数尺寸才有正中像素，腐蚀后中心保持对齐；
	///   给偶数宽高会让结果整体偏移半像素，与 <c>Threshold</c> 原轮廓比对时表现为系统性误差。</para>
	///   <para><b>各向异性</b>与圆盘不同，<c>width</c> 与 <c>height</c> 独立：抹掉水平条纹只给
	///   <c>height</c> 加量、<c>width</c> 留小值即可，这是 <c>ErosionCircle</c> 做不到的方向选择性。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("mesh.hobj");
	///   JlRegion dark = image.Threshold(0.0, 100.0);
	///   using JlRegion holes = dark.ErosionRectangle1(1, 5);   // 只吃横向细缝
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入区域不被修改。</para>
	/// </remarks>
	public JlRegion ErosionRectangle1(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(749);
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
	///   用圆形结构元腐蚀区域：只保留能整只塞下该圆盘的位置，边界向内收缩 radius 像素。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。传元组可一次算多个半径。Default: 3.5</param>
	/// <returns>腐蚀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>作用对象</b>本算子处理二值区域。灰度图要另走灰度形态学一族；手上是 <c>JlImage</c>
	///   时先 <c>Threshold</c> 转区域，否则得不到想要的结果而没有报错提示。</para>
	///   <para><b>重载选择</b>与 <c>ErosionCircle(double)</c> 是同一个原生算子（id 750），唯一区别是
	///   控制参数的写入方式：本重载用 <c>Store</c> 固定元组、调用后再 <c>UnpinTuple</c>；double 重载
	///   用 <c>StoreD</c> 直写，没有固定/解固定开销。<b>半径固定时一律用 double 版</b>，
	///   只有需要一次比较多个半径时才用本重载。</para>
	///   <para><b>尺寸陷阱</b>腐蚀会真实改变面积与轮廓位置：若后续要做尺寸测量或轮廓拟合，
	///   不能用它做去噪预处理，应改用 <c>OpeningCircle</c>（腐蚀后膨胀，保尺度）或 <c>ClosingCircle</c>。</para>
	///   <para><b>取值</b>半径大于目标最短半边长会把目标整体吃掉；从"要抹掉的最细结构"反推半径，
	///   而不是从"想缩多少"正推。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pcb.hobj");
	///   JlRegion copper = image.Threshold(128.0, 255.0);
	///   using JlRegion necks = copper.ErosionCircle(2.0);        // 断开 4 像素内的细颈
	///   JlRegion blobs = necks.Connection();
	///   int n = blobs.CountObj();                                 // 断成了几块
	///   blobs.Dispose();
	///   copper.Dispose();
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入的 <c>this</c> 不被修改，两者都需自行 Dispose。</para>
	///   <para><b>待确认</b>radius ≤ 0 的行为、以及多半径元组与多输入区域的展开规则 [待实测]。</para>
	/// </remarks>
	public JlRegion ErosionCircle(JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(750);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(radius);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用圆形结构元腐蚀区域（单半径版）。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。Default: 3.5</param>
	/// <returns>腐蚀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para>语义、半径取值与尺寸陷阱见 <see cref="ErosionCircle(JlTuple)"/>。两个重载走同一个
	///   原生算子（id 750），本版本用 <c>StoreD</c> 直接写 double，不做元组固定与解固定，
	///   因此<b>半径确定时应当用它</b>，不要为了少写一个变量而构造 <c>JlTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pcb.hobj");
	///   JlRegion copper = image.Threshold(128.0, 255.0);
	///   using JlRegion eroded = copper.ErosionCircle(2.0);
	///   </code>
	/// </remarks>
	public JlRegion ErosionCircle(double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(750);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用自定义结构元腐蚀区域，并显式指定结构元上的参考点（锚点）。
	/// </summary>
	/// <param name="structElement">结构元区域。</param>
	/// <param name="row">参考点的行坐标（y，相对结构元自身坐标）。Default: 0</param>
	/// <param name="column">参考点的列坐标（x，相对结构元自身坐标）。Default: 0</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>腐蚀结果的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para><b>与 <c>Erosion1</c> 的唯一区别</b>多了 <c>row</c>/<c>column</c> 两个整型控制参数
	///   （原生 id 751 对 752）。<c>Erosion1</c> 无法说明"结构元上哪个像素算原点"，
	///   因此不对称元素的结果会随元素画法而不可控；需要方向性的腐蚀时用本重载。</para>
	///   <para><b>锚点决定吃掉哪一侧</b>默认 (0,0)。把锚点放在元素的左上角，腐蚀效果是"从右下收缩"；
	///   放在元素质心则是各向均匀收缩。构造结构元时若用的是 <c>new JlRegion(row1, column1, row2, column2)</c>
	///   这类以左上/右下两角定义的区域，其坐标原点并不在几何中心——最常见的错误就是把锚点写成
	///   包围盒角点，导致整体偏移半个元素尺寸。</para>
	///   <para><b>坐标约定</b>本库统一 row = y（向下为正）、column = x（向右为正），与像素数组一致；
	///   不要把 <c>(x, y)</c> 习惯直接填进 <c>row</c>,<c>column</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("print.hobj");
	///   JlRegion ink = image.Threshold(0.0, 120.0);
	///
	///   JlRegion element = new JlRegion(0.0, 0.0, 0.0, 6.0);   // 6 像素长的水平探针
	///   using JlRegion eroded = ink.Erosion2(element, 0, 6, 1);  // 锚在最右端：只从左侧吃掉
	///   element.Dispose();
	///   ink.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion Erosion2(JlRegion structElement, int row, int column, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(751);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   用自定义区域作结构元腐蚀区域。
	/// </summary>
	/// <param name="structElement">结构元区域。</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>腐蚀结果的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para><b>何时必须用它</b>圆盘/矩形（<c>ErosionCircle</c> / <c>ErosionRectangle1</c>）覆盖不了
	///   非凸、带方向的结构：例如只允许沿 45° 生长的元素、或带缺口的印刷元素。这时把元素本身
	///   做成一个 <c>JlRegion</c>（<c>GenRegion1</c>/<c>GenRectangle1</c> + <c>Union2</c>/<c>Difference</c>）
	///   传进来，比用大圆盘腐蚀再回填便宜且形状准确。</para>
	///   <para><b>没有参考点</b>本重载（原生 id 752）不接受锚点；需要指定结构元上哪个像素为原点时用
	///   <c>Erosion2</c>（751）。结构元应以自身坐标原点为中心构造，否则腐蚀方向会整体偏移。</para>
	///   <para><b>结构元的生命周期</b>实现里结构元作为第二个 iconic 句柄传入，并用 <c>GC.KeepAlive</c>
	///   保活到原生调用结束，因此调用之后即可 Dispose；但<b>不能在调用前释放</b>，
	///   提前 Dispose 会拿到底层已失效句柄。</para>
	///   <para><b>代价</b>自定义元素的腐蚀无法走圆盘/矩形的快速路径，元素像素数越多越慢；
	///   能用 <c>ErosionCircle</c> 表达的不要用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("grid.hobj");
	///   JlRegion target = image.Threshold(60.0, 255.0);
	///
	///   JlRegion element = new JlRegion(10.0, 20.0, 40.0, 22.0);   // 一条横向窄元素
	///   using JlRegion eroded = target.Erosion1(element, 1);
	///   element.Dispose();
	///   target.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion Erosion1(JlRegion structElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(752);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   反复应用 Golay 字母表元素膨胀区域（顺序迭代版）。
	/// </summary>
	/// <param name="golayElement">Golay 字母表中的结构元。Default: "h"</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>膨胀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para>迭代机制与 <c>ErosionSeq</c> 同（原生 id 753，元素方向由实现安排）。</para>
	///   <para><b>用它做大跨度外扩</b>要外扩十几像素时，<c>DilationCircle</c> 一次到位更直观，
	///   但元素成本随半径上升；<c>iterations</c> 路线每轮只推进一个元素宽度，大跨度时更省。[待实测]</para>
	///   <para><b>粘连距离由它决定</b>膨胀量就是两目标被并成一对的临界间距：
	///   <c>iterations</c> 取 N 后，间距小于约 2N 的块会合并。按"允许合并的最大间距"反推 N，
	///   不要凭手感加次数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("dots.hobj");
	///   JlRegion dots = image.Threshold(128.0, 255.0);
	///   using JlRegion merged = dots.DilationSeq("h", 6);   // 间距约 12 像素内的点并成簇
	///   using JlRegion clusters = merged.Connection();
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入不被修改；膨胀后的尺寸与形状不可用于量测。</para>
	/// </remarks>
	public JlRegion DilationSeq(string golayElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(753);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 Golay 字母表元素膨胀区域，旋转方向由调用者指定。
	/// </summary>
	/// <param name="golayElement">Golay 字母表中的结构元。Default: "h"</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <param name="rotation">元素旋转序号；并非每个字母都支持全部旋转。Default: 0</param>
	/// <returns>膨胀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>方向性生长</b>与 <c>DilationSeq</c>（753）的差别是有 <c>rotation</c>（本算子 754）：
	///   只想让目标朝某一侧长（例如把刻度线向基准边延长、而不影响另一侧的相邻目标）时用它，
	///   各向同性的圆盘膨胀做不到。</para>
	///   <para><b>合法 rotation 随字母变化</b>见 <c>ErosionGolay</c> 的同条说明；不同字母可换用同一个
	///   rotation 序号。越界是报错还是回绕 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("scale.hobj");
	///   JlRegion ticks = image.Threshold(0.0, 90.0);
	///   using JlRegion grown = ticks.DilationGolay("h", 4, 1);   // 只朝基准边延长
	///   ticks.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion DilationGolay(string golayElement, int iterations, int rotation)
	{
		IntPtr proc = JlNativeApi.PreCall(754);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, golayElement);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.StoreI(proc, 2, rotation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用矩形结构元膨胀区域：横纵方向可分别设定外扩量。
	/// </summary>
	/// <param name="width">矩形结构元宽度（像素）。Default: 11</param>
	/// <param name="height">矩形结构元高度（像素）。Default: 11</param>
	/// <returns>膨胀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>各向异性外扩</b>这是把目标"只沿一个方向延长"的最省事的写法：例如把指示线朝标签
	///   方向拉长（<c>height</c> 小、<c>width</c> 大），或把一维编码条沿行方向补齐。
	///   圆盘版做不到这种选择性。</para>
	///   <para><b>默认值</b>与 <c>ErosionRectangle1</c> 同为 11×11；注意
	///   <c>OpeningRectangle1</c>/<c>ClosingRectangle1</c> 是 10×10，两种默认值都合法，不要互相"纠正"。
	///   给偶数宽高会让结果偏移半像素（奇数才有正中像素），外扩 ROI 时表现为系统性不对称。</para>
	///   <para><b>代价</b>矩形膨胀对大尺寸比圆盘慢（无快速路径），超过十几像素的外扩优先考虑
	///   <c>DilationCircle</c> 或 <c>DilationSeq</c>。[待实测]</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("labels.hobj");
	///   JlRegion leader = image.Threshold(0.0, 80.0);
	///   using JlRegion extended = leader.DilationRectangle1(21, 1);   // 只沿列方向延长 20 像素
	///   leader.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion DilationRectangle1(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(755);
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
	///   用圆形结构元膨胀区域：每个像素向外扩张 radius 像素，空隙被填、邻近块会粘连。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。传元组可一次算多个半径。Default: 3.5</param>
	/// <returns>膨胀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para><b>顺序会改变结论</b>膨胀使原本分离的两块并成一块：<c>Connection()</c> 之前膨胀，
	///   块数会变；之后膨胀则块数不变但每块边界外扩。做"计数"与做"外扩 ROI"是两种相反的顺序，
	///   照搬别人的流水线最常错在这里。</para>
	///   <para><b>尺寸不可用</b>膨胀后 <c>Area</c>/<c>SmallestRectangle1</c>/<c>轮廓拟合</c> 的结果
	///   都会系统性偏大（各方向多出 radius）。凡是要上报尺寸的量测，必须在膨胀之前的区域上做。</para>
	///   <para><b>与 <c>ErosionCircle</c> 配对</b>单独膨胀只用于放大目标或外扩取样的 ROI；
	///   要去噪而不改变目标尺寸，用 <c>OpeningCircle</c>（先腐蚀后膨胀）。</para>
	///   <para><b>重载选择</b>与 <c>DilationCircle(double)</c> 同一原生算子（id 756）：本重载经
	///   <c>Store</c> 固定元组并在调用后 <c>UnpinTuple</c>；double 版用 <c>StoreD</c> 直写，
	///   半径确定时应使用 double 版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("parts.hobj");
	///   JlRegion parts = image.Threshold(100.0, 255.0);
	///   JlRegion blobs = parts.Connection();
	///   int nBefore = blobs.CountObj();
	///
	///   using JlRegion merged = blobs.DilationCircle(4.0);   // 把 8 像素内的碎片并回主体
	///   using JlRegion recount = merged.Connection();
	///   int nAfter = recount.CountObj();
	///   </code>
	///   <para><b>句柄</b>返回新句柄，输入不被修改；链式中间结果需逐个 Dispose。</para>
	///   <para><b>待确认</b>radius ≤ 0 的行为与多半径元组的展开规则 [待实测]。</para>
	/// </remarks>
	public JlRegion DilationCircle(JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(756);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(radius);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用圆形结构元膨胀区域（单半径版）。
	/// </summary>
	/// <param name="radius">圆盘结构元半径（像素）。Default: 3.5</param>
	/// <returns>膨胀结果的新句柄；调用者区域不变。</returns>
	/// <remarks>
	///   <para>粘连风险、尺寸偏大与顺序问题见 <see cref="DilationCircle(JlTuple)"/>。
	///   两个重载走同一原生算子（id 756），本版本用 <c>StoreD</c> 直写 double，
	///   不做元组固定与解固定，半径确定时应当用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("parts.hobj");
	///   JlRegion roi = image.Threshold(100.0, 255.0);
	///   using JlRegion grow = roi.DilationCircle(6.0);   // 取样的 ROI 向外留 6 像素余量
	///   roi.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion DilationCircle(double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(756);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用自定义结构元膨胀区域，并显式指定结构元上的参考点（锚点）。
	/// </summary>
	/// <param name="structElement">结构元区域。</param>
	/// <param name="row">参考点的行坐标（y，相对结构元自身坐标）。Default: 0</param>
	/// <param name="column">参考点的列坐标（x，相对结构元自身坐标）。Default: 0</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>膨胀结果的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para>锚点语义、row/column 坐标约定与常见错位见 <c>Erosion2</c>（本算子原生 id 757，
	///   两者只差膨胀/腐蚀方向）。</para>
	///   <para><b>膨胀时锚点的意义更直观</b>目标沿结构元从锚点向外的形状生长：一根水平探针的锚点
	///   放在左端，目标只向右延长；放在中点则左右各延长一半。做"向基准边补齐"这类单向操作时，
	///   锚点位置就是业务语义本身。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("table.hobj");
	///   JlRegion cells = image.Threshold(120.0, 255.0);
	///
	///   JlRegion probe = new JlRegion(0.0, 0.0, 0.0, 9.0);      // 9 像素长水平探针
	///   using JlRegion rightGrow = cells.Dilation2(probe, 0, 0, 1);  // 锚在左端：只向右补
	///   probe.Dispose();
	///   cells.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion Dilation2(JlRegion structElement, int row, int column, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(757);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>
	///   用自定义区域作结构元膨胀区域。
	/// </summary>
	/// <param name="structElement">结构元区域。</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>膨胀结果的新句柄；调用者区域与结构元均不被修改。</returns>
	/// <remarks>
	///   <para>机制、结构元句柄生命周期与"无参考点"限制同 <c>Erosion1</c>（本算子原生 id 758）：
	///   结构元作为第二个 iconic 句柄传入，调用之后才能 Dispose。</para>
	///   <para><b>能定义出圆盘给不出的生长形状</b>例如十字元素只沿行列方向生长、留出对角缝隙，
	///   用于把断开的笔画接上而不把相邻两列字并起来；带缺口的元素可做"只补某个方向"的修补。</para>
	///   <para><b>代价</b>元素像素数直接决定耗时，能用 <c>DilationRectangle1</c> 表达的就不要自建元素。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("text.hobj");
	///   JlRegion strokes = image.Threshold(0.0, 100.0);
	///
	///   JlRegion cross = new JlRegion(4.0, 0.0, 6.0, 12.0);      // 横杠
	///   cross = cross.Union2(new JlRegion(0.0, 4.0, 12.0, 6.0));  // 竖杠 → 十字元素
	///   using JlRegion joined = strokes.Dilation1(cross, 1);
	///   cross.Dispose();
	///   strokes.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion Dilation1(JlRegion structElement, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(758);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, structElement);
		JlNativeApi.StoreI(proc, 0, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(structElement);
		return obj;
	}

	/// <summary>把区域与输入图像的灰度值组合，为每个输入区域生成一路带灰度的图像。</summary>
	/// <param name="image">提供像素灰度的输入图像（叠加到区域上的通道）。</param>
	/// <returns>新的图像句柄数组：每个输入区域对应一路输出，区域形状保留、像素值取自 <c>image</c>；用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>add_channels</c>（原生 id 1092）：<c>this</c> 提供区域（第一路输入，<c>Store</c> 索引 1），<c>image</c> 提供灰度（索引 2），输出按 <c>InitOCT</c> 装为 JlImage；区域本身不变，返回新句柄。</para>
	///   <para><b>约束或前提</b>要求 <c>image</c> 为单通道灰度图；输出路数等于 <c>this</c> 内区域元素个数，区域元组为空时无有效输出 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想得到区域灰度统计量（均值等）用 <c>RegionToMean</c>；要把区域连同原始灰度一起交给后续处理才用本方法，它不改变区域几何。</para>
	///   <para><b>参数取向</b>区域与图像均以 <c>Store</c> 作图标输入，无标量直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion r = img.Threshold(128.0, 255.0);
	///   JlImage composed = r.AddChannels(img);
	///   r.Dispose();
	///   img.Dispose();
	///   composed.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新图像句柄，与 <c>this</c>、<c>image</c> 生命周期无关，须各自 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage AddChannels(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(1092);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}







	/// <summary>从一组法法式直线中挑出落入区域最好的那些，返回其直线区域与筛选后的法式参数。</summary>
	/// <param name="angleIn">各输入直线法向量的角度，单位弧度。</param>
	/// <param name="distIn">各输入直线到原点的距离。</param>
	/// <param name="lineWidth">直线的宽度（像素）。Default: 7</param>
	/// <param name="thresh">判定"落入区域"所需的线上点数下限。Default: 100</param>
	/// <param name="angleOut">被选中直线的法向角度（弧度），与 <c>distOut</c> 一一对应。</param>
	/// <param name="distOut">被选中直线到原点的距离。</param>
	/// <returns>由被选中直线构成的区域数组（新句柄，用毕需 <c>Dispose</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>select_matching_lines</c>（原生 id 1103）：把 <c>angleIn</c>/<c>distIn</c> 描述的一组法法式（NF）直线按 <c>lineWidth</c> 展成条带，统计落入 <c>this</c> 内的点数，点数达 <c>thresh</c> 者入选；输出为入选直线的区域数组及对应的 <c>angleOut</c>/<c>distOut</c>。输入区域不变。</para>
	///   <para><b>约束或前提</b>角度以弧度、法线式（<c>rho = x*cos(angle)+y*sin(angle)</c> 约定）给出，与 <c>LinesImage</c> 一族一致；<c>angleIn</c> 与 <c>distIn</c> 长度须相等，逐条直线配对。</para>
	///   <para><b>与相邻算子的取舍</b>本重载吃/吐 <c>JlTuple</c>（整批直线）；只处理单条直线用 <c>SelectMatchingLines(double,double,int,int,out double,out double)</c>，后者只读/写第一值、多值会被丢弃。要的是 XLD 而非区域请改用 XLD 侧算子。</para>
	///   <para><b>参数取向</b>本重载把 <c>angleIn</c>/<c>distIn</c> 用 <c>Store</c> 钉固定元组、调用后 <c>UnpinTuple</c>；<c>lineWidth</c>/<c>thresh</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；两个 out 以 <c>LoadNew(JlTupleType.DOUBLE)</c> 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 200.0);
	///   JlTuple angleIn = new double[] { 0.0 };
	///   JlTuple distIn = new double[] { 20.0 };
	///   JlTuple angleOut;
	///   JlTuple distOut;
	///   JlRegion matched = r.SelectMatchingLines(angleIn, distIn, 7, 100, out angleOut, out distOut);
	///   r.Dispose();
	///   matched.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与两个 out 元组均为新句柄/新对象，须各自释放；无直线达标时返回空区域。</para>
	/// </remarks>
	public JlRegion SelectMatchingLines(JlTuple angleIn, JlTuple distIn, int lineWidth, int thresh, out JlTuple angleOut, out JlTuple distOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1103);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, angleIn);
		JlNativeApi.Store(proc, 1, distIn);
		JlNativeApi.StoreI(proc, 2, lineWidth);
		JlNativeApi.StoreI(proc, 3, thresh);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(angleIn);
		JlNativeApi.UnpinTuple(distIn);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out angleOut);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distOut);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>标量重载：对单条法法式直线判断是否落入区域，输出该直线的区域与筛选后的法式参数。</summary>
	/// <param name="angleIn">输入直线法向量的角度，单位弧度。</param>
	/// <param name="distIn">输入直线到原点的距离。</param>
	/// <param name="lineWidth">直线的宽度（像素）。Default: 7</param>
	/// <param name="thresh">判定"落入区域"所需的线上点数下限。Default: 100</param>
	/// <param name="angleOut">被选中时的法向角度（弧度）。</param>
	/// <param name="distOut">被选中时到原点的距离。</param>
	/// <returns>由被选中直线构成的区域数组（新句柄，用毕需 <c>Dispose</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="M:JLVisionLib.JlRegion.SelectMatchingLines(JLVisionLib.JlTuple,JLVisionLib.JlTuple,System.Int32,System.Int32,JLVisionLib.JlTuple@,JLVisionLib.JlTuple@)"/> 同一原生算子（id 1103），语义为按 <c>lineWidth</c> 把直线展成条带、落入 <c>this</c> 的点数达 <c>thresh</c> 者入选。</para>
	///   <para><b>约束或前提</b>角度以弧度、法线式给出；本重载只处理一条直线的输入/输出。</para>
	///   <para><b>与相邻算子的取舍</b>成批直线用 <c>JlTuple</c> 重载；本重载用 <c>StoreD</c>/<c>LoadD</c> 直写直读 DOUBLE，省掉钉固定与 <c>UnpinTuple</c>，但只取第一个值——传入多值会被静默丢弃。</para>
	///   <para><b>参数取向</b><c>angleIn</c>/<c>distIn</c> 以 <c>StoreD</c> 写入、<c>lineWidth</c>/<c>thresh</c> 以 <c>StoreI</c> 写入；两个 out 以 <c>LoadD</c> 读回 DOUBLE。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 200.0);
	///   double angleOut;
	///   double distOut;
	///   JlRegion matched = r.SelectMatchingLines(0.0, 20.0, 7, 100, out angleOut, out distOut);
	///   r.Dispose();
	///   matched.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须 <c>Dispose</c>；直线不达标时返回空区域、out 值无意义 [待实测]。</para>
	/// </remarks>
	public JlRegion SelectMatchingLines(double angleIn, double distIn, int lineWidth, int thresh, out double angleOut, out double distOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1103);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, angleIn);
		JlNativeApi.StoreD(proc, 1, distIn);
		JlNativeApi.StoreI(proc, 2, lineWidth);
		JlNativeApi.StoreI(proc, 3, thresh);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadD(proc, 0, err, out angleOut);
		err = JlNativeApi.LoadD(proc, 1, err, out distOut);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   计算一条线段与本句柄各区域之间最近/最远的像素距离（元组版，每个区域一对结果）。
	/// </summary>
	/// <param name="row1">线段第一端点行坐标元组。</param>
	/// <param name="column1">线段第一端点列坐标元组。</param>
	/// <param name="row2">线段第二端点行坐标元组。</param>
	/// <param name="column2">线段第二端点列坐标元组。</param>
	/// <param name="distanceMin">输出：最近距离（DOUBLE 元组，逐区域）。</param>
	/// <param name="distanceMax">输出：最远距离（DOUBLE 元组，逐区域）。</param>
	/// <remarks>
	///   <para><b>与 <c>DistanceLr</c>/<c>DistancePr</c> 的分界</b>本算子 id 1306 是<b>线段</b>
	///   （两端点之间）；<c>DistanceLr</c>（1307）两端无限延伸；<c>DistancePr</c>（1308）是点。
	///   目标超出线段的横向范围时，Sr 会算出到端点的距离而 Lr 不会——这是选错时最常见的偏差。</para>
	///   <para><b>约束</b>输入是本句柄内的区域元组，输出逐区域对齐；空区域时输出值 [待实测]。
	///   距离按像素栅格计算，精度有限 [待实测]。</para>
	///   <para><b>参数取向</b>void + 两个 <c>out JlTuple</c>；坐标元组与区域元组的配对广播规则 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("board.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0).Connection();
	///   blobs.DistanceSr(50.0, 40.0, 250.0, 40.0,
	///       out JlTuple dMin, out JlTuple dMax);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本重载四个坐标元组 <c>Store</c> 固定、调用后 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void DistanceSr(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1306);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   线段到区域的最远/最近距离（单区域标量版）。
	/// </summary>
	/// <param name="row1">线段第一端点行坐标。</param>
	/// <param name="column1">线段第一端点列坐标。</param>
	/// <param name="row2">线段第二端点行坐标。</param>
	/// <param name="column2">线段第二端点列坐标。</param>
	/// <param name="distanceMin">输出：最近距离。</param>
	/// <param name="distanceMax">输出：最远距离。</param>
	/// <remarks>
	///   <para>线段/直线/点的分界与空区域行为见
	///   <see cref="DistanceSr(JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1306）：本重载 <c>StoreD</c> 直写坐标、<c>LoadD</c> 读标量，
	///   全程无元组固定与 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void DistanceSr(double row1, double column1, double row2, double column2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1306);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   计算一条<b>无限长</b>直线与各区域间的最远/最近距离（元组版）。
	/// </summary>
	/// <param name="row1">直线上第一点行坐标元组。</param>
	/// <param name="column1">直线上第一点列坐标元组。</param>
	/// <param name="row2">直线上第二点行坐标元组。</param>
	/// <param name="column2">直线上第二点列坐标元组。</param>
	/// <param name="distanceMin">输出：最近距离（DOUBLE 元组）。</param>
	/// <param name="distanceMax">输出：最远距离（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para>与 <c>DistanceSr</c> 的选择规则、空区域与栅格精度见
	///   <see cref="DistanceSr(JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>：
	///   本算子（id 1307）把两点当作无限直线上两点，目标伸出"线段"范围时距离不按端点截断。
	///   两重载同 id，本重载经 <c>Store</c>/<c>UnpinTuple</c>；标量需求用 double 版。
	///   两点重合时的行为 [待实测]。</para>
	/// </remarks>
	public void DistanceLr(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1307);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   无限直线到区域的距离（标量版）。
	/// </summary>
	/// <param name="row1">直线上第一点行坐标。</param>
	/// <param name="column1">直线上第一点列坐标。</param>
	/// <param name="row2">直线上第二点行坐标。</param>
	/// <param name="column2">直线上第二点列坐标。</param>
	/// <param name="distanceMin">输出：最近距离。</param>
	/// <param name="distanceMax">输出：最远距离。</param>
	/// <remarks>
	///   <para>直线语义与 <c>DistanceSr</c> 的选择见
	///   <see cref="DistanceLr(JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1307）：本重载 <c>StoreD</c>/<c>LoadD</c>，无元组固定与
	///   <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void DistanceLr(double row1, double column1, double row2, double column2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1307);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   计算一个（批）点到各区域的最远/最近距离（元组版）。
	/// </summary>
	/// <param name="row">点行坐标元组。</param>
	/// <param name="column">点列坐标元组。</param>
	/// <param name="distanceMin">输出：最近距离（DOUBLE 元组）。</param>
	/// <param name="distanceMax">输出：最远距离（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para>本质是 <c>DistanceSr</c> 两端点重合的特例（id 1308 独立）；点在线段范围内外的
	///   差异问题在这里不存在。空区域与栅格精度见
	///   <see cref="DistanceSr(JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/> [待实测]。
	///   判"点在区域内"用 <c>TestRegionPoints</c>（id 2192）而不是本算子。
	///   本重载经 <c>Store</c>/<c>UnpinTuple</c>。</para>
	/// </remarks>
	public void DistancePr(JlTuple row, JlTuple column, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1308);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   点到区域的距离（标量版）。
	/// </summary>
	/// <param name="row">点行坐标。</param>
	/// <param name="column">点列坐标。</param>
	/// <param name="distanceMin">输出：最近距离。</param>
	/// <param name="distanceMax">输出：最远距离。</param>
	/// <remarks>
	///   <para>点距语义与 <c>TestRegionPoints</c> 的分界见
	///   <see cref="DistancePr(JlTuple,JlTuple,out JlTuple,out JlTuple)"/>。两个重载同一原生算子
	///   （id 1308）：本重载 <c>StoreD</c>/<c>LoadD</c>，无元组固定与 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public void DistancePr(double row, double column, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1308);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>统计各区域内"原图减去均值滤波图"的噪声幅值分布。</summary>
	/// <param name="image">对应的输入图像（单通道灰度）。</param>
	/// <param name="filterSize">求噪声所用的均值滤波器尺寸。Default: 21</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），逐区域给出噪声分布量；数值元组，用完可不显式 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>noise_distribution_mean</c>（原生 id 1379）：先用 <c>filterSize</c> 的均值滤波估计平滑背景，再取 <c>image</c> 与其差作为噪声图，统计落在 <c>this</c> 各区域内的噪声分布；输入区域不变。</para>
	///   <para><b>约束或前提</b>要求 <c>image</c> 为单通道灰度图，区域须覆盖到待评估噪声的像素；<c>filterSize</c> 过小会把真实结构当噪声、过大则估计偏平。</para>
	///   <para><b>与相邻算子的取舍</b>要单个标量噪声度用 <c>NoiseStd</c>/<c>RegionGaussDistribution</c> 一族更省；要逐区域完整分布才用本方法。常用于自动定噪声阈值。</para>
	///   <para><b>参数取向</b>区域与图像以 <c>Store</c> 作图标输入，<c>filterSize</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew(JlTupleType.DOUBLE)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion r = img.Threshold(128.0, 255.0);
	///   JlTuple dist = r.NoiseDistributionMean(img, 21);
	///   r.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新元组对象，随 GC 回收；空区域时分布无定义 [待实测]。</para>
	/// </remarks>
	public JlTuple NoiseDistributionMean(JlImage image, int filterSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1379);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreI(proc, 0, filterSize);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>按隶属度函数计算区域边界的模糊熵（边界不确定度），逐区域返回。</summary>
	/// <param name="image">提供模糊隶属度值的输入图像（单通道灰度）。</param>
	/// <param name="apar">隶属度函数的灰度起点。Default: 0</param>
	/// <param name="cpar">隶属度函数的灰度终点。Default: 255</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），逐区域的模糊熵；数值元组，可不显式 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>fuzzy_entropy_region</c>（原生 id 1396）：把灰度落在 <c>[apar,cpar]</c> 的像素线性映射为 <c>[0,1]</c> 隶属度，据此衡量 <c>this</c> 各区域边界"既非完全内也非完全外"的模糊程度，越大表示边界越不确定。输入区域不变。</para>
	///   <para><b>约束或前提</b><c>image</c> 应为单通道灰度且其值代表隶属度/置信度；<c>apar &lt; cpar</c>，否则映射退化。区域须与 <c>image</c> 坐标对齐。</para>
	///   <para><b>与相邻算子的取舍</b>只要边界长度用 <see cref="FuzzyPerimeter(JlImage,int,int)"/>；要经典（非模糊）轮廓用 <c>Perimeter</c>。模糊族用于置信度图上的软边界质量评估。</para>
	///   <para><b>参数取向</b>区域与图像以 <c>Store</c> 作图标输入，<c>apar</c>/<c>cpar</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew(JlTupleType.DOUBLE)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion r = img.Threshold(128.0, 255.0);
	///   JlTuple ent = r.FuzzyEntropy(img, 0, 255);
	///   r.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新元组对象；退化/空区域的熵无意义 [待实测]。</para>
	/// </remarks>
	public JlTuple FuzzyEntropy(JlImage image, int apar, int cpar)
	{
		IntPtr proc = JlNativeApi.PreCall(1396);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreI(proc, 0, apar);
		JlNativeApi.StoreI(proc, 1, cpar);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>按隶属度函数计算区域边界的模糊周长，逐区域返回。</summary>
	/// <param name="image">提供模糊隶属度值的输入图像（单通道灰度）。</param>
	/// <param name="apar">隶属度函数的灰度起点。Default: 0</param>
	/// <param name="cpar">隶属度函数的灰度终点。Default: 255</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），逐区域的模糊周长（像素）；数值元组，可不显式 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>fuzzy_perimeter</c>（原生 id 1397）：以灰度落在 <c>[apar,cpar]</c> 线性映射出的隶属度加权边界像素，得到软边界的长度估计，隶属度越低（越像背景）的边界贡献越小。输入区域不变。</para>
	///   <para><b>约束或前提</b>要求 <c>image</c> 单通道、代表隶属度；<c>apar &lt; cpar</c>；区域与图像坐标须对齐。</para>
	///   <para><b>与相邻算子的取舍</b>要硬边界长度用 <c>Perimeter</c>；要边界模糊程度（非长度）用 <see cref="FuzzyEntropy(JlImage,int,int)"/>。本方法适合在置信度图上得到比经典周长更鲁棒的边界量。</para>
	///   <para><b>参数取向</b>区域与图像以 <c>Store</c> 作图标输入，<c>apar</c>/<c>cpar</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew(JlTupleType.DOUBLE)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion r = img.Threshold(128.0, 255.0);
	///   JlTuple per = r.FuzzyPerimeter(img, 0, 255);
	///   r.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新元组对象；单位随隶属度加权，不等同像素计数 [待实测]。</para>
	/// </remarks>
	public JlTuple FuzzyPerimeter(JlImage image, int apar, int cpar)
	{
		IntPtr proc = JlNativeApi.PreCall(1397);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreI(proc, 0, apar);
		JlNativeApi.StoreI(proc, 1, cpar);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>用各区域的平均灰度回填区域，得到分片着色的结果图像。</summary>
	/// <param name="image">提供原始灰度的输入图像（单通道）。</param>
	/// <returns>新的图像句柄：每个 <c>this</c> 内区域被整体涂成其灰度均值；用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>region_to_mean</c>（原生 id 1415）：对每个输入区域求 <c>image</c> 在其内的平均灰度，再把该区域所有像素都填成这个均值，区域外像素保持原值。常用于把分割结果"去噪成色块"。输入区域不变。</para>
	///   <para><b>约束或前提</b>要求 <c>image</c> 单通道且与区域坐标对齐；多个区域重叠时按处理顺序后者覆盖前者 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要标量均值用 <c>Intensity</c>/<c>AreaCenter</c> 统计族；要得到可见的均匀色块才用本方法，它产出图像而非数值。</para>
	///   <para><b>参数取向</b>区域与图像均以 <c>Store</c> 作图标输入，无标量控制参数；输出 <c>JlImage.LoadNew</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion r = img.Threshold(128.0, 255.0);
	///   JlImage painted = r.RegionToMean(img);
	///   r.Dispose();
	///   img.Dispose();
	///   painted.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新图像句柄，与 <c>this</c>/<c>image</c> 无关，须各自 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage RegionToMean(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(1415);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>借助梯度幅值图把断裂的边缘区域沿强梯度方向补接，最多延长指定点数。</summary>
	/// <param name="gradient">边缘梯度（幅值）图像，单通道。</param>
	/// <param name="minAmplitude">参与补接的最低梯度幅值。Default: 16</param>
	/// <param name="maxGapLength">允许被延长以弥合缺口的最大点数。Default: 3</param>
	/// <returns>新的区域句柄，含补接后的边缘；用毕需 <c>Dispose</c>。输入区域不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>close_edges_length</c>（原生 id 1494）：把 <c>this</c> 视作边缘区域，沿 <c>gradient</c> 中幅值不低于 <c>minAmplitude</c> 的方向逐点延长边缘端点，单次最多延长 <c>maxGapLength</c> 个点以弥合缺口，输出一张更连续的区域。它作用于区域（非 XLD）。</para>
	///   <para><b>约束或前提</b><c>gradient</c> 必须与 <c>this</c> 同尺寸、且其像素代表幅值；<c>maxGapLength</c> 太小则缺口仍不闭合，太大易把邻近无关边缘错误粘连。</para>
	///   <para><b>与相邻算子的取舍</b>不需显式控制延长点数时用 <see cref="CloseEdges(JlImage,int)"/>（id 1495），它由幅值自动定步长；本重载适合缺口尺度已知、需精确限制补接长度的场合。</para>
	///   <para><b>参数取向</b>区域与梯度图以 <c>Store</c> 作图标输入，<c>minAmplitude</c>/<c>maxGapLength</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew</c> 新区域句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion edges = img.Threshold(200.0, 255.0);
	///   JlRegion closed = edges.CloseEdgesLength(img, 16, 3);
	///   edges.Dispose();
	///   img.Dispose();
	///   closed.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，须 <c>Dispose</c>；无缺口可闭时结果与输入近似但不共享句柄。</para>
	/// </remarks>
	public JlRegion CloseEdgesLength(JlImage gradient, int minAmplitude, int maxGapLength)
	{
		IntPtr proc = JlNativeApi.PreCall(1494);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, gradient);
		JlNativeApi.StoreI(proc, 0, minAmplitude);
		JlNativeApi.StoreI(proc, 1, maxGapLength);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(gradient);
		return obj;
	}

	/// <summary>借助梯度幅值图自动补接断裂边缘区域，无需指定最大延长点数。</summary>
	/// <param name="edgeImage">边缘梯度（幅值）图像，单通道。</param>
	/// <param name="minAmplitude">参与补接的最低梯度幅值（量化步长）。Default: 16</param>
	/// <returns>新的区域句柄，含补接后的边缘；用毕需 <c>Dispose</c>。输入区域不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>close_edges</c>（原生 id 1495）：与 <see cref="CloseEdgesLength(JlImage,int,int)"/> 同族，差别在于步长由 <c>minAmplitude</c> 自动确定，逐档延长 <c>this</c> 中边缘端点至幅值不满足条件为止。输入区域不变，返回新区域。</para>
	///   <para><b>约束或前提</b><c>edgeImage</c> 须与 <c>this</c> 同尺寸、单通道；<c>minAmplitude</c> 越大补接越保守、缺口越难闭合，过小易过度粘连。</para>
	///   <para><b>与相邻算子的取舍</b>需要精确控制"最多延多少点"用 <c>CloseEdgesLength</c>；本方法适合缺口尺度不定、只希望按梯度强弱自动弥合的场合。</para>
	///   <para><b>参数取向</b>区域与梯度图以 <c>Store</c> 作图标输入，<c>minAmplitude</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew</c> 新区域句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion edges = img.Threshold(200.0, 255.0);
	///   JlRegion closed = edges.CloseEdges(img, 16);
	///   edges.Dispose();
	///   img.Dispose();
	///   closed.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须 <c>Dispose</c>；对区域型边缘有效，XLD 边缘请改走 XLD 侧闭合算子。</para>
	/// </remarks>
	public JlRegion CloseEdges(JlImage edgeImage, int minAmplitude)
	{
		IntPtr proc = JlNativeApi.PreCall(1495);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, edgeImage);
		JlNativeApi.StoreI(proc, 0, minAmplitude);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(edgeImage);
		return obj;
	}

	/// <summary>把 <c>SerializeRegion</c> 得到的字节流还原到当前句柄（原地改写）。</summary>
	/// <param name="serializedItemHandle">序列化字节数组（非原生句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>deserialize_region</c>（原生 id 1572）：先 <c>Dispose()</c> 掉当前句柄内容，再由字节流 <c>Load</c> 出区域，调用后 <c>this</c> 本身即为反序列化结果，无返回值。</para>
	///   <para><b>约束或前提</b><c>serializedItemHandle</c> 必须来自 <see cref="SerializeRegion"/>；传入空/损坏数组时由原生层报错 [待实测]。调用前 <c>this</c> 的旧内容被丢弃，别指望保留。</para>
	///   <para><b>与相邻算子的取舍</b>跨进程/落盘用 <c>WriteRegion</c>/<c>ReadRegion</c>（文件路径）；内存内传递或随对象序列化用本方法与 <c>SerializeRegion</c> 一对。</para>
	///   <para><b>参数取向</b>以 <c>Store</c> 把字节缓冲作为图标输入，输出经 <c>Load</c> 原地写入 <c>this</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion src = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   byte[] bytes = src.SerializeRegion();
	///   src.Dispose();
	///   JlRegion r = new JlRegion();
	///   r.DeserializeRegion(bytes);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写、不返回新句柄；<c>this</c> 生命周期不变，用完照常 <c>Dispose</c>。</para>
	/// </remarks>
	public void DeserializeRegion(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1572);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把当前区域序列化成内存字节数组，供 <c>DeserializeRegion</c> 或网络传输。</summary>
	/// <returns>序列化后的字节数组（托管内存，非原生句柄，无需 <c>Dispose</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>serialize_region</c>（原生 id 1573）：把 <c>this</c> 的几何编码为一段字节流并返回；区域本身不变。与 <see cref="DeserializeRegion(byte[])"/> 成对使用。</para>
	///   <para><b>约束或前提</b>仅覆盖区域对象；<c>this</c> 为 UNDEF 句柄时的输出无意义 [待实测]。字节流与运行时/版本相关，别当作长期落盘格式。</para>
	///   <para><b>与相邻算子的取舍</b>要存成 .hobj 文件用 <c>WriteRegion</c>；内存内复制/跨进程传句柄用本方法。需要深拷贝回句柄就 <c>DeserializeRegion</c>。</para>
	///   <para><b>参数取向</b><c>this</c> 以 <c>Store</c> 作图标输入，输出用 <c>JlSerializationBuffer.LoadBytes</c> 读为 <c>byte[]</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   byte[] bytes = r.SerializeRegion();
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回托管 <c>byte[]</c>，随 GC 回收；不要误当成句柄去 <c>Dispose</c>。</para>
	/// </remarks>
	public byte[] SerializeRegion()
	{
		IntPtr proc = JlNativeApi.PreCall(1573);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>把区域（元组）写入 .hobj 文件，与 <c>ReadRegion</c> 成对。</summary>
	/// <param name="fileName">区域文件名（含路径）。Default: "region.hobj"</param>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>write_region</c>（原生 id 1574）：把 <c>this</c> 内的全部区域元素序列化到指定 .hobj 文件；区域本身不变，无返回值。</para>
	///   <para><b>约束或前提</b>路径需可写，文件已存在会被覆盖；<c>this</c> 含多元素时会一并写入，读回时得到一个区域元组。文件不可写时的原生报错文本 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>内存内传递用 <c>SerializeRegion</c>/<c>DeserializeRegion</c>；跨机/长期保存用本方法。</para>
	///   <para><b>参数取向</b><c>this</c> 以 <c>Store</c> 作图标输入，<c>fileName</c> 以 <c>StoreS</c> 写入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   r.WriteRegion("region.hobj");
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地写出、不产生新句柄；<c>this</c> 用毕照常 <c>Dispose</c>。</para>
	/// </remarks>
	public void WriteRegion(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1574);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>从 .hobj 文件读入区域，原地写入当前句柄，与 <c>WriteRegion</c> 成对。</summary>
	/// <param name="fileName">区域文件路径。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>read_region</c>（原生 id 1577）：实现先 <c>Dispose()</c> 再 <c>Load</c>——调用后 <c>this</c> 即成为从 <c>fileName</c> 反序列化出的区域（元组），文件里的所有区域元素一并读入。</para>
	///   <para><b>约束或前提</b>调用前 <c>this</c> 的旧内容立即失效；文件不存在或格式非区域时的原生报错文本 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>内存内字节流用 <c>DeserializeRegion</c>；本方法面向磁盘 .hobj。JlImage 的对应方法是 <c>ReadImage</c>，别混用。</para>
	///   <para><b>参数取向</b><c>fileName</c> 以 <c>StoreS</c> 写入，输出 <c>InitOCT</c> 后由 <c>Load</c> 原地装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion();
	///   r.ReadRegion("region.hobj");
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写，无新句柄返回；若需保留旧内容请先 <c>Clone</c> 或换一个新句柄再调用。</para>
	/// </remarks>
	public void ReadRegion(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1577);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>计算逐区域的旋转不变中心矩 PSI2/PSI3/PSI4，用于形状描述。</summary>
	/// <param name="PSI2">2 阶归一化中心不变矩。</param>
	/// <param name="PSI3">2 阶归一化中心不变矩。</param>
	/// <param name="PSI4">2 阶归一化中心不变矩。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），该算子的第 0 个矩输出；与 <c>PSI2</c>/<c>PSI3</c>/<c>PSI4</c> 同为逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_central_invar</c>（原生 id 1613）：先取重心作原点算中心矩，再归一化成随平移、旋转、面积变化不敏感的不变量 <c>PSI2</c>/<c>PSI3</c>/<c>PSI4</c>，适合做形状分类特征。输入区域不变。</para>
	///   <para><b>约束或前提</b>退化区域（面积 0、单像素）归一化会除以近零量，结果不稳定 [待实测]；形态学/仿射改变形状后必须重算，旧矩不会自动更新。</para>
	///   <para><b>与相邻算子的取舍</b>要未归一化的普通中心矩用 <c>MomentsRegionCentral</c>；要 3 阶不变量用 <c>MomentsRegion3rdInvar</c>。本方法是 2 阶、且带不变性归一化。</para>
	///   <para><b>参数取向</b>本重载把四个输出都以 <c>JlTuple.LoadNew(DOUBLE)</c> 逐区域装载；标量重载 <c>MomentsRegionCentralInvar(out double,out double,out double)</c> 同 id 但用 <c>LoadD</c> 只读第 1 个区域的值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple psi2;
	///   JlTuple psi3;
	///   JlTuple psi4;
	///   JlTuple m0 = r.MomentsRegionCentralInvar(out psi2, out psi3, out psi4);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与三个 out 元组均为新对象；用标量重载处理区域元组会静默丢弃第 2 个区域起的数据。</para>
	/// </remarks>
	public JlTuple MomentsRegionCentralInvar(out JlTuple PSI2, out JlTuple PSI3, out JlTuple PSI4)
	{
		IntPtr proc = JlNativeApi.PreCall(1613);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out PSI2);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out PSI3);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out PSI4);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域的旋转不变中心矩 PSI2/PSI3/PSI4（同 id 1613）。</summary>
	/// <param name="PSI2">第 1 个区域的 2 阶归一化中心不变矩。</param>
	/// <param name="PSI3">第 1 个区域的 2 阶归一化中心不变矩。</param>
	/// <param name="PSI4">第 1 个区域的 2 阶归一化中心不变矩。</param>
	/// <returns>该算子第 0 个矩输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegionCentralInvar(out JlTuple,out JlTuple,out JlTuple)</c> 同一原生算子（id 1613），语义为旋转不变的中心矩归一化量。</para>
	///   <para><b>约束或前提</b>退化区域结果不稳定 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组里不止一个区域时，本重载用 <c>LoadD</c> 只读每路输出的第一个值，第 2 个区域起的数据被静默丢弃——要整批请用 <c>JlTuple</c> 重载。</para>
	///   <para><b>参数取向</b>四个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double psi2;
	///   double psi3;
	///   double psi4;
	///   double m0 = r.MomentsRegionCentralInvar(out psi2, out psi3, out psi4);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；真正的坑是多区域元组时只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegionCentralInvar(out double PSI2, out double PSI3, out double PSI4)
	{
		IntPtr proc = JlNativeApi.PreCall(1613);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out PSI2);
		err = JlNativeApi.LoadD(proc, 2, err, out PSI3);
		err = JlNativeApi.LoadD(proc, 3, err, out PSI4);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>计算以重心为原点的中心矩 i2/i3/i4（未归一化，随旋转而变）。</summary>
	/// <param name="i2">2 阶中心矩。</param>
	/// <param name="i3">2 阶中心矩。</param>
	/// <param name="i4">3 阶中心矩。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），该算子第 0 个中心矩输出；逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_central</c>（原生 id 1614）：把坐标原点平移到区域重心后求各阶矩，消除了平移影响，但仍随旋转、面积改变。输入区域不变。</para>
	///   <para><b>约束或前提</b>退化区域重心无定义时结果不稳定 [待实测]；形状改变后需重算。</para>
	///   <para><b>与相邻算子的取舍</b>要旋转/尺度不变的形状特征用 <c>MomentsRegionCentralInvar</c>（id 1613）；要更高阶用 <c>MomentsRegion3rd</c>。本方法给出原始中心矩本身。</para>
	///   <para><b>参数取向</b>本重载四个输出均以 <c>JlTuple.LoadNew(DOUBLE)</c> 逐区域装载；标量重载用 <c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple i2;
	///   JlTuple i3;
	///   JlTuple i4;
	///   JlTuple m0 = r.MomentsRegionCentral(out i2, out i3, out i4);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与三个 out 元组均为新对象；单值重载处理多区域元组会丢数据。</para>
	/// </remarks>
	public JlTuple MomentsRegionCentral(out JlTuple i2, out JlTuple i3, out JlTuple i4)
	{
		IntPtr proc = JlNativeApi.PreCall(1614);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out i2);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out i3);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out i4);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域以重心为原点的中心矩 i2/i3/i4（同 id 1614）。</summary>
	/// <param name="i2">第 1 个区域的 2 阶中心矩。</param>
	/// <param name="i3">第 1 个区域的 2 阶中心矩。</param>
	/// <param name="i4">第 1 个区域的 3 阶中心矩。</param>
	/// <returns>该算子第 0 个中心矩输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegionCentral(out JlTuple,out JlTuple,out JlTuple)</c> 同一原生算子（id 1614），语义为以重心为原点的中心矩。</para>
	///   <para><b>约束或前提</b>退化区域结果不稳定 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时，本重载用 <c>LoadD</c> 只读每路第一个值，其余静默丢弃——整批请用 <c>JlTuple</c> 重载。</para>
	///   <para><b>参数取向</b>四个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double i2;
	///   double i3;
	///   double i4;
	///   double m0 = r.MomentsRegionCentral(out i2, out i3, out i4);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegionCentral(out double i2, out double i3, out double i4)
	{
		IntPtr proc = JlNativeApi.PreCall(1614);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out i2);
		err = JlNativeApi.LoadD(proc, 2, err, out i3);
		err = JlNativeApi.LoadD(proc, 3, err, out i4);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>计算逐区域的 3 阶旋转不变矩 m12/m03/m30。</summary>
	/// <param name="m12">3 阶不变矩（随列变化分量）。</param>
	/// <param name="m03">3 阶不变矩（随列变化分量）。</param>
	/// <param name="m30">3 阶不变矩（随行变化分量）。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），该算子第 0 个矩输出；逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_3rd_invar</c>（原生 id 1615）：在重心系下求 3 阶矩并归一化为旋转不变量 <c>m12</c>/<c>m03</c>/<c>m30</c>，比 2 阶不变矩能刻画更多不对称形状细节。输入区域不变。</para>
	///   <para><b>约束或前提</b>退化区域/小面积区域归一化数值不稳 [待实测]；形状改变后需重算。</para>
	///   <para><b>与相邻算子的取舍</b>2 阶不变矩用 <c>MomentsRegionCentralInvar</c>（id 1613）；未归一化的 3 阶矩用 <c>MomentsRegion3rd</c>（id 1616）。本方法是 3 阶且带不变性。</para>
	///   <para><b>参数取向</b>本重载四个输出均以 <c>JlTuple.LoadNew(DOUBLE)</c> 逐区域装载；标量重载用 <c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple m12;
	///   JlTuple m03;
	///   JlTuple m30;
	///   JlTuple m0 = r.MomentsRegion3rdInvar(out m12, out m03, out m30);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与三个 out 元组均为新对象；单值重载处理多区域元组会丢数据。</para>
	/// </remarks>
	public JlTuple MomentsRegion3rdInvar(out JlTuple m12, out JlTuple m03, out JlTuple m30)
	{
		IntPtr proc = JlNativeApi.PreCall(1615);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out m12);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out m03);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out m30);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域的 3 阶旋转不变矩 m12/m03/m30（同 id 1615）。</summary>
	/// <param name="m12">第 1 个区域的 3 阶不变矩（列分量）。</param>
	/// <param name="m03">第 1 个区域的 3 阶不变矩（列分量）。</param>
	/// <param name="m30">第 1 个区域的 3 阶不变矩（行分量）。</param>
	/// <returns>该算子第 0 个矩输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegion3rdInvar(out JlTuple,out JlTuple,out JlTuple)</c> 同一原生算子（id 1615），语义为 3 阶旋转不变矩。</para>
	///   <para><b>约束或前提</b>退化区域结果不稳 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时，本重载用 <c>LoadD</c> 只读每路第一个值，其余静默丢弃——整批用 <c>JlTuple</c> 重载。</para>
	///   <para><b>参数取向</b>四个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double m12;
	///   double m03;
	///   double m30;
	///   double m0 = r.MomentsRegion3rdInvar(out m12, out m03, out m30);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegion3rdInvar(out double m12, out double m03, out double m30)
	{
		IntPtr proc = JlNativeApi.PreCall(1615);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out m12);
		err = JlNativeApi.LoadD(proc, 2, err, out m03);
		err = JlNativeApi.LoadD(proc, 3, err, out m30);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>计算以重心为原点的 3 阶中心矩 m12/m03/m30（未归一化，随旋转而变）。</summary>
	/// <param name="m12">3 阶中心矩（列相关分量）。</param>
	/// <param name="m03">3 阶中心矩（列相关分量）。</param>
	/// <param name="m30">3 阶中心矩（行相关分量）。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），该算子第 0 个矩输出；逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_3rd</c>（原生 id 1616）：在重心系下求 3 阶矩 <c>m12</c>/<c>m03</c>/<c>m30</c>，消除平移影响但保留旋转/尺度依赖。输入区域不变。</para>
	///   <para><b>约束或前提</b>退化区域重心无定义时结果不稳 [待实测]；形状改变后需重算。</para>
	///   <para><b>与相邻算子的取舍</b>要旋转不变量用 <c>MomentsRegion3rdInvar</c>（id 1615）；要 2 阶中心矩用 <c>MomentsRegionCentral</c>（id 1614）。本方法是未归一化的 3 阶中心矩。</para>
	///   <para><b>参数取向</b>本重载四个输出均以 <c>JlTuple.LoadNew(DOUBLE)</c> 逐区域装载；标量重载用 <c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple m12;
	///   JlTuple m03;
	///   JlTuple m30;
	///   JlTuple m0 = r.MomentsRegion3rd(out m12, out m03, out m30);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与三个 out 元组均为新对象；单值重载处理多区域元组会丢数据。</para>
	/// </remarks>
	public JlTuple MomentsRegion3rd(out JlTuple m12, out JlTuple m03, out JlTuple m30)
	{
		IntPtr proc = JlNativeApi.PreCall(1616);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out m12);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out m03);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out m30);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域以重心为原点的 3 阶中心矩 m12/m03/m30（同 id 1616）。</summary>
	/// <param name="m12">第 1 个区域的 3 阶中心矩（列相关分量）。</param>
	/// <param name="m03">第 1 个区域的 3 阶中心矩（列相关分量）。</param>
	/// <param name="m30">第 1 个区域的 3 阶中心矩（行相关分量）。</param>
	/// <returns>该算子第 0 个矩输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegion3rd(out JlTuple,out JlTuple,out JlTuple)</c> 同一原生算子（id 1616），语义为未归一化的 3 阶中心矩。</para>
	///   <para><b>约束或前提</b>退化区域结果不稳 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时，本重载用 <c>LoadD</c> 只读每路第一个值，其余静默丢弃——整批用 <c>JlTuple</c> 重载。</para>
	///   <para><b>参数取向</b>四个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double m12;
	///   double m03;
	///   double m30;
	///   double m0 = r.MomentsRegion3rd(out m12, out m03, out m30);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegion3rd(out double m12, out double m03, out double m30)
	{
		IntPtr proc = JlNativeApi.PreCall(1616);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out m12);
		err = JlNativeApi.LoadD(proc, 2, err, out m03);
		err = JlNativeApi.LoadD(proc, 3, err, out m30);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   求各区域的最小面积任意朝向包框，输出中心、转角与两条半轴长。
	/// </summary>
	/// <param name="row">输出：包框中心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">输出：包框中心列坐标（DOUBLE 元组）。</param>
	/// <param name="phi">输出：包框转角（弧度，DOUBLE 元组）。</param>
	/// <param name="length1">输出：半轴长 1（DOUBLE 元组）。</param>
	/// <param name="length2">输出：半轴长 2（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para><b>与 <c>SmallestRectangle1</c> 的取舍</b>对旋转摆放的工件，1 型给出的宽高是
	///   斜放的轴对齐包框，尺寸随摆放角变化、量不了工件真实长宽；选 2 型。反过来：只需要
	///   快速 ROI 裁剪时选 1 型（整数角点、开销更低），别为用不上的角度信息买单。</para>
	///   <para><b>出参约定</b><c>length1</c>/<c>length2</c> 是<b>半</b>轴，与
	///   <c>GenRectangle2</c> 入参同义、可原样回填；phi 的旋转正方向与参考轴托管层未注明 [待实测]。</para>
	///   <para><b>参数取向</b>五个 <c>out JlTuple</c>，逐区域对齐；空区域时的值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("screws.hobj");
	///   JlRegion blobs = image.Threshold(110.0, 255.0).Connection();
	///   blobs.SmallestRectangle2(out JlTuple row, out JlTuple column, out JlTuple phi,
	///       out JlTuple len1, out JlTuple len2);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原生 id 1617，本重载出参以 DOUBLE 元组装载。</para>
	/// </remarks>
	public void SmallestRectangle2(out JlTuple row, out JlTuple column, out JlTuple phi, out JlTuple length1, out JlTuple length2)
	{
		IntPtr proc = JlNativeApi.PreCall(1617);
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
	///   最小面积任意朝向包框（单区域标量版）。
	/// </summary>
	/// <param name="row">输出：包框中心行坐标。</param>
	/// <param name="column">输出：包框中心列坐标。</param>
	/// <param name="phi">输出：包框转角（弧度）。</param>
	/// <param name="length1">输出：半轴长 1。</param>
	/// <param name="length2">输出：半轴长 2。</param>
	/// <remarks>
	///   <para>与 1 型的取舍、半轴约定见
	///   <see cref="SmallestRectangle2(out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1617）：本重载逐出参 <c>LoadD</c> 取标量，省掉五个
	///   <c>JlTuple</c> 对象；句柄内是区域元组时只会取到第一组值 [待实测]。</para>
	/// </remarks>
	public void SmallestRectangle2(out double row, out double column, out double phi, out double length1, out double length2)
	{
		IntPtr proc = JlNativeApi.PreCall(1617);
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
	///   求各区域的轴对齐最小外接矩形，输出左上、右下两个角点（整型坐标）。
	/// </summary>
	/// <param name="row1">输出：左上角行坐标（INTEGER 元组）。</param>
	/// <param name="column1">输出：左上角列坐标（INTEGER 元组）。</param>
	/// <param name="row2">输出：右下角行坐标（INTEGER 元组）。</param>
	/// <param name="column2">输出：右下角列坐标（INTEGER 元组）。</param>
	/// <remarks>
	///   <para><b>整数是本算子的特点</b>两个重载都以 INTEGER 装载（元组版声明了
	///   <c>JlTupleType.INTEGER</c>，标量版走 <c>LoadI</c>），与 <c>GenRectangle1</c>、
	///   区域构造器的 double 角点互填时注意隐式转换。</para>
	///   <para><b>与 <c>SmallestRectangle2</c> 的取舍</b>目标会转角度时，1 型的宽高随摆放角
	///   浮动，只能当裁剪框不能当尺寸测量；此时用 2 型。只要 ROI 就用 1 型，别付角度的代价。</para>
	///   <para><b>参数取向</b>出参顺序 row1、column1、row2、column2（先行后列交替）；
	///   角点是否包含区域极值像素本身（闭区间）[待实测]；空区域时的值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("labels.hobj");
	///   JlRegion blobs = image.Threshold(90.0, 255.0).Connection();
	///   blobs.SmallestRectangle1(out JlTuple row1, out JlTuple col1, out JlTuple row2, out JlTuple col2);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原生 id 1618，只读量测、不产生区域句柄。</para>
	/// </remarks>
	public void SmallestRectangle1(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1618);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out row1);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out column1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out row2);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   轴对齐最小外接矩形（单区域整数版）。
	/// </summary>
	/// <param name="row1">输出：左上角行坐标。</param>
	/// <param name="column1">输出：左上角列坐标。</param>
	/// <param name="row2">输出：右下角行坐标。</param>
	/// <param name="column2">输出：右下角列坐标。</param>
	/// <remarks>
	///   <para>闭区间与空区域等存疑点见
	///   <see cref="SmallestRectangle1(out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1618）：本重载四个 <c>LoadI</c> 直取标量、不建
	///   <c>JlTuple</c>，单区域 ROI 用它。</para>
	/// </remarks>
	public void SmallestRectangle1(out int row1, out int column1, out int row2, out int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1618);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out row1);
		err = JlNativeApi.LoadI(proc, 1, err, out column1);
		err = JlNativeApi.LoadI(proc, 2, err, out row2);
		err = JlNativeApi.LoadI(proc, 3, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   求各区域的最小外接圆：中心与半径，保证罩住全部像素。
	/// </summary>
	/// <param name="row">输出：外接圆圆心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">输出：外接圆圆心列坐标（DOUBLE 元组）。</param>
	/// <param name="radius">输出：外接圆半径（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para><b>与 <c>InnerCircle</c> 的取舍</b>本算子量"外面要多大的圆才装得下"（外接，
	///   由离圆心最远的像素定径）；<c>InnerCircle</c>（id 1654）量"区域内最大的圆"（内切，
	///   反映区域最厚实的一坨）。用错方向的后果：拿外接圆半径去估目标粗细会系统性偏大。</para>
	///   <para><b>约束</b>圆心不保证落在区域内部（凹形外接圆必然如此）[待实测]；
	///   空区域的输出 [待实测]。原生 id 1619。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("coins.hobj");
	///   JlRegion coins = image.Threshold(120.0, 255.0).Connection();
	///   coins.SmallestCircle(out JlTuple row, out JlTuple column, out JlTuple radius);
	///   coins.Dispose();
	///   </code>
	/// </remarks>
	public void SmallestCircle(out JlTuple row, out JlTuple column, out JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1619);
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
	///   最小外接圆（标量版）。
	/// </summary>
	/// <param name="row">输出：外接圆圆心行坐标。</param>
	/// <param name="column">输出：外接圆圆心列坐标。</param>
	/// <param name="radius">输出：外接圆半径。</param>
	/// <remarks>
	///   <para>外接与内切的取舍见
	///   <see cref="SmallestCircle(out JlTuple,out JlTuple,out JlTuple)"/>。两个重载同一原生算子
	///   （id 1619）：本重载三个 <c>LoadD</c> 直取标量、不建 <c>JlTuple</c>；单区域用它。</para>
	/// </remarks>
	public void SmallestCircle(out double row, out double column, out double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1619);
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
	///   按与"标准图案"区域的位置关系特征过滤：每个候选区域对照 pattern 算一个关系值。
	/// </summary>
	/// <param name="pattern">参考区域（作为第二个 iconic 句柄传入）。</param>
	/// <param name="feature">位置关系特征名（元组）。Default: "covers"</param>
	/// <param name="min">关系值下限（元组）。Default: 50.0</param>
	/// <param name="max">关系值上限（元组）。Default: 100.0</param>
	/// <returns>满足关系的区域新句柄；输入与 pattern 均不被修改。</returns>
	/// <remarks>
	///   <para><b>它不是 <c>SelectShape</c></b>那里区间作用在候选自身形状上；这里区间作用在
	///   "候选与 pattern 的关系"上（默认 "covers" 的百分比含义托管层未给出 [待实测]）。
	///   判"在不在 ROI 内"用本算子；判"胖瘦圆方"用 <c>SelectShape</c>。</para>
	///   <para><b>易错</b>元组版（id 1620）的 feature/min/max 是三个元组、并行配对，
	///   没有 <c>operation</c> 参数，别把 <c>SelectShape</c> 的调用改个名传进来。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("cells.hobj");
	///   JlRegion cells = image.Threshold(100.0, 255.0).Connection();
	///   JlRegion roi = new JlRegion(60.0, 60.0, 240.0, 240.0);
	///   using JlRegion inside = cells.SelectShapeProto(roi, new JlTuple("covers"),
	///       new JlTuple(90.0), new JlTuple(100.0));
	///   cells.Dispose();
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>pattern</c> 侧 <c>Store</c>+<c>KeepAlive</c>，返回后才可 Dispose。</para>
	/// </remarks>
	public JlRegion SelectShapeProto(JlRegion pattern, JlTuple feature, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(1620);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, pattern);
		JlNativeApi.Store(proc, 0, feature);
		JlNativeApi.Store(proc, 1, min);
		JlNativeApi.Store(proc, 2, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(feature);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(pattern);
		return obj;
	}

	/// <summary>
	///   按与标准图案的位置关系过滤（单特征标量版）。
	/// </summary>
	/// <param name="pattern">参考区域（作为第二个 iconic 句柄传入）。</param>
	/// <param name="feature">位置关系特征名。Default: "covers"</param>
	/// <param name="min">关系值下限。Default: 50.0</param>
	/// <param name="max">关系值上限。Default: 100.0</param>
	/// <returns>满足关系的区域新句柄；输入与 pattern 均不被修改。</returns>
	/// <remarks>
	///   <para>与 <c>SelectShape</c> 的分界见
	///   <see cref="SelectShapeProto(JlRegion,JlTuple,JlTuple,JlTuple)"/>。两个重载同一原生算子
	///   （id 1620）：本重载 <c>StoreS</c>/<c>StoreD</c> 直写，无元组固定与 <c>UnpinTuple</c>。</para>
	/// </remarks>
	public JlRegion SelectShapeProto(JlRegion pattern, string feature, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(1620);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, pattern);
		JlNativeApi.StoreS(proc, 0, feature);
		JlNativeApi.StoreD(proc, 1, min);
		JlNativeApi.StoreD(proc, 2, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(pattern);
		return obj;
	}

	/// <summary>按特征名批量计算区域形状特征，返回展平的 DOUBLE 值元组。</summary>
	/// <param name="features">特征名字符串元组（如 <c>"area"</c>、<c>"row"</c>、<c>"circularity"</c> 等）。Default: "area"</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），逐（区域 × 特征）的值；数值元组可不显式 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>region_features</c>（原生 id 1621）：对 <c>this</c> 的每个区域逐一求 <c>features</c> 列出的形状特征，输出为 DOUBLE 元组。输入区域不变。</para>
	///   <para><b>约束或前提</b><c>features</c> 虽声明为 <c>JlTuple</c> 但内容是特征名字符串，写错的名字透传给原生层会报错 [待实测]；输出元素个数 = 区域数 × 特征数，读取时须自行按序对齐。</para>
	///   <para><b>与相邻算子的取舍</b>只需面积/重心用 <c>AreaCenter</c> 更快；要在特征区间上筛区域用 <c>SelectShape</c>。本方法负责"取值"不负责"筛选"。</para>
	///   <para><b>参数取向</b><c>features</c> 以 <c>Store</c> 钉固定字符串元组、调用后 <c>UnpinTuple</c>；输出 <c>LoadNew(DOUBLE)</c>。标量重载 <c>RegionFeatures(string)</c> 同 id 但 <c>StoreS</c>/<c>LoadD</c>，只读第 1 个值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple feats = new string[] { "area", "circularity" };
	///   JlTuple vals = r.RegionFeatures(feats);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新元组；形态学/仿射改变形状后旧值作废，须重算。</para>
	/// </remarks>
	public JlTuple RegionFeatures(JlTuple features)
	{
		IntPtr proc = JlNativeApi.PreCall(1621);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, features);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(features);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：算单个特征名、只返回第 1 个区域-特征组合的 DOUBLE 值（同 id 1621）。</summary>
	/// <param name="features">单个特征名字符串。Default: "area"</param>
	/// <returns>该算子输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>RegionFeatures(JlTuple)</c> 同一原生算子（id 1621），语义为按特征名取形状特征值。</para>
	///   <para><b>约束或前提</b>特征名拼错透传原生层报错 [待实测]；本重载假定只要一个值。</para>
	///   <para><b>与相邻算子的取舍</b>要多个特征名或整批区域的值，用 <c>JlTuple</c> 重载；本重载用 <c>StoreS</c>/<c>LoadD</c>，多值会被静默丢弃只留第 1 个。</para>
	///   <para><b>参数取向</b><c>features</c> 以 <c>StoreS</c> 直写字符串、输出以 <c>LoadD</c> 读回第一个 DOUBLE，无固定元组开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double area = r.RegionFeatures("area");
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于区域/特征多时只拿到首值。</para>
	/// </remarks>
	public double RegionFeatures(string features)
	{
		IntPtr proc = JlNativeApi.PreCall(1621);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, features);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   按一个或多个形状特征的区间过滤区域元组，返回满足条件的子集（保持原顺序）。
	/// </summary>
	/// <param name="features">要检查的形特征名（元组=多特征联合判定）。Default: "area"</param>
	/// <param name="operation">多特征间的逻辑连接。Default: "and"</param>
	/// <param name="min">各特征下限（与 features 并行配对的元组）。Default: 150.0</param>
	/// <param name="max">各特征上限（元组）。Default: 99999.0</param>
	/// <returns>满足条件的区域组成的新句柄；输入句柄不变。</returns>
	/// <remarks>
	///   <para><b>顺序要求</b>筛选发生在拆分之后：输入若是粘连成一块的区域，特征按整块算，
	///   小目标会被整块留下或整块丢掉——先 <c>Connection()</c> 再本算子。</para>
	///   <para><b>易错</b><c>operation</c> 是"多特征怎么联"，不是特征名；特征名与
	///   <c>operation</c> 的可选字面量清单托管层未枚举，拼错的字符串直接透传给原生层
	///   [待实测]。min 大于 max 或负值时的行为 [待实测]。</para>
	///   <para><b>取舍</b>要"最接近某标准形状的那批"用 <c>SelectShapeStd</c>（id 1634）；
	///   与标准图案的位置关系比对用 <c>SelectShapeProto</c>；只要数值不要筛选用
	///   <c>AreaCenter</c>/<c>RegionFeatures</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0).Connection();
	///   using JlRegion good = blobs.SelectShape(new JlTuple("area"), "and",
	///       new JlTuple(300.0), new JlTuple(99999999.0));
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本重载（id 1622）对三个元组做 <c>Store</c>/<c>UnpinTuple</c>；
	///   返回 0 个对象时句柄仍有效，判空用 <c>CountObj()</c>。</para>
	/// </remarks>
	public JlRegion SelectShape(JlTuple features, string operation, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(1622);
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
	///   按单个特征区间过滤区域元组（标量版）。
	/// </summary>
	/// <param name="features">要检查的形特征名。Default: "area"</param>
	/// <param name="operation">多特征间的逻辑连接（单特征时仍必填）。Default: "and"</param>
	/// <param name="min">特征下限。Default: 150.0</param>
	/// <param name="max">特征上限。Default: 99999.0</param>
	/// <returns>满足条件的区域组成的新句柄；输入句柄不变。</returns>
	/// <remarks>
	///   <para>"先 <c>Connection</c> 后筛选"的顺序要求见
	///   <see cref="SelectShape(JlTuple,string,JlTuple,JlTuple)"/>。两个重载同一原生算子
	///   （id 1622）：本重载用 <c>StoreS</c>/<c>StoreD</c> 直写，全程无元组固定与
	///   <c>UnpinTuple</c>；单特征单区间用本重载。</para>
	/// </remarks>
	public JlRegion SelectShape(string features, string operation, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(1622);
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

	/// <summary>给出区域游程编码（run-length）特征：游程数、存储因子、每行游程数、平均游程长、所需字节数。</summary>
	/// <param name="KFactor">相对正方形的存储因子（越大表示越省存）。</param>
	/// <param name="LFactor">每行平均游程数。</param>
	/// <param name="meanLength">游程平均长度。</param>
	/// <param name="bytes">编码该区域所需字节数（INTEGER）。</param>
	/// <returns>新 <c>JlTuple</c>（INTEGER），逐区域的游程总数；与四个 out 同为区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>runlength_features</c>（原生 id 1623）：把区域按行游程编码后统计其压缩特征，用于估计区域的存储/描述复杂度。输入区域不变。</para>
	///   <para><b>约束或前提</b>游程数随区域形状剧烈变化（细长/碎片化区域游程多）；这些量是编码统计，不等同于面积/周长。</para>
	///   <para><b>与相邻算子的取舍</b>要几何形状量用 <c>RegionFeatures</c>；要评估区域存储代价或做游程相关分析才用本方法。</para>
	///   <para><b>参数取向</b>返回值与 <c>bytes</c> 以 <c>LoadNew(INTEGER)</c> 装载，其余三路以 <c>LoadNew(DOUBLE)</c>；标量重载 <c>RunlengthFeatures(out double,out double,out double,out int)</c> 同 id 但用 <c>LoadD</c>/<c>LoadI</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple kFactor;
	///   JlTuple lFactor;
	///   JlTuple meanLength;
	///   JlTuple bytes;
	///   JlTuple nRuns = r.RunlengthFeatures(out kFactor, out lFactor, out meanLength, out bytes);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个输出均为新元组；退化/空区域各项可能为 0 [待实测]。</para>
	/// </remarks>
	public JlTuple RunlengthFeatures(out JlTuple KFactor, out JlTuple LFactor, out JlTuple meanLength, out JlTuple bytes)
	{
		IntPtr proc = JlNativeApi.PreCall(1623);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out KFactor);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out LFactor);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out meanLength);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out bytes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域的游程编码特征（同 id 1623），返回其游程数。</summary>
	/// <param name="KFactor">第 1 个区域的存储因子（DOUBLE）。</param>
	/// <param name="LFactor">第 1 个区域的每行平均游程数（DOUBLE）。</param>
	/// <param name="meanLength">第 1 个区域的游程平均长度（DOUBLE）。</param>
	/// <param name="bytes">第 1 个区域所需编码字节数（INTEGER）。</param>
	/// <returns>第 1 个区域的游程总数（<c>int</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>RunlengthFeatures(out JlTuple,out JlTuple,out JlTuple,out JlTuple)</c> 同一原生算子（id 1623），语义为游程编码统计。</para>
	///   <para><b>约束或前提</b>本重载假定 <c>this</c> 只有一个区域；退化/空区域各值可能为 0 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时用 <c>JlTuple</c> 重载；本重载用 <c>LoadI</c>/<c>LoadD</c> 只读每路第一个值，其余静默丢弃。</para>
	///   <para><b>参数取向</b>返回值与 <c>bytes</c> 以 <c>LoadI</c> 读回 INTEGER，三路 DOUBLE 以 <c>LoadD</c> 读回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double kFactor;
	///   double lFactor;
	///   double meanLength;
	///   int bytes;
	///   int nRuns = r.RunlengthFeatures(out kFactor, out lFactor, out meanLength, out bytes);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public int RunlengthFeatures(out double KFactor, out double LFactor, out double meanLength, out int bytes)
	{
		IntPtr proc = JlNativeApi.PreCall(1623);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out KFactor);
		err = JlNativeApi.LoadD(proc, 2, err, out LFactor);
		err = JlNativeApi.LoadD(proc, 3, err, out meanLength);
		err = JlNativeApi.LoadI(proc, 4, err, out bytes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>找出两批区域中相互距离不超过阈值的邻近区域对，返回各自 1 基索引。</summary>
	/// <param name="regions2">作为对照的第二批区域。</param>
	/// <param name="maxDistance">判定为邻近的最大距离（像素）。Default: 1</param>
	/// <param name="regionIndex2">命中的 <c>regions2</c> 元素序号（1 基），与返回值一一对应。</param>
	/// <returns>新 <c>JlTuple</c>（INTEGER）：命中的 <c>this</c>（Regions1）元素序号（1 基）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>find_neighbors</c>（原生 id 1624）：把 <c>this</c>（Regions1）与 <c>regions2</c>（Regions2）两批区域逐对比较，间距 ≤ <c>maxDistance</c> 者成对记录，返回并行的两个索引数组。输入区域不变。</para>
	///   <para><b>约束或前提</b>两批区域须在同一坐标系；索引是 1 基（<c>select_obj</c> 约定），可直接喂 <see cref="SelectObj(JlTuple)"/>。距离以区域间最短像素距计 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只判断两个区域是否相交/距离用 <c>TestRegionRelationship</c>；本方法面向"整批里哪些互相靠得近"的配对关系。</para>
	///   <para><b>参数取向</b>两批区域以 <c>Store</c> 作图标输入，<c>maxDistance</c> 以 <c>StoreI</c> 写入；两个索引输出均以 <c>LoadNew(INTEGER)</c> 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion set1 = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion set2 = new JlRegion(50.0, 50.0, 70.0, 70.0);
	///   JlTuple regionIndex2;
	///   JlTuple regionIndex1 = set1.FindNeighbors(set2, 20, out regionIndex2);
	///   set1.Dispose();
	///   set2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个索引元组为新对象；无邻近对时返回空元组，配对须保证 <c>maxDistance</c> 足够大。</para>
	/// </remarks>
	public JlTuple FindNeighbors(JlRegion regions2, int maxDistance, out JlTuple regionIndex2)
	{
		IntPtr proc = JlNativeApi.PreCall(1624);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.StoreI(proc, 0, maxDistance);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out regionIndex2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>计算逐区域的 2 阶相对不变矩 PHI2。</summary>
	/// <param name="PHI2">2 阶相对不变矩（对面积归一的量）。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），该算子第 0 个矩输出；逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_2nd_rel_invar</c>（原生 id 1625）：给出随平移、旋转不变且按面积归一的 2 阶矩 <c>PHI2</c>，用于与尺寸无关的形状取向描述。输入区域不变。</para>
	///   <para><b>约束或前提</b>退化/极小区域归一化不稳 [待实测]；形状改变后需重算。</para>
	///   <para><b>与相邻算子的取舍</b>要 2 阶主/次不变矩用 <c>MomentsRegion2ndInvar</c>（id 1626）；要未归一的 2 阶矩用 <c>MomentsRegion2nd</c>（id 1627）。</para>
	///   <para><b>参数取向</b>两输出均以 <c>JlTuple.LoadNew(DOUBLE)</c> 装载；标量重载 <c>MomentsRegion2ndRelInvar(out double)</c> 同 id 但 <c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple phi2;
	///   JlTuple m0 = r.MomentsRegion2ndRelInvar(out phi2);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 out 元组均为新对象；单值重载处理多区域元组会丢数据。</para>
	/// </remarks>
	public JlTuple MomentsRegion2ndRelInvar(out JlTuple PHI2)
	{
		IntPtr proc = JlNativeApi.PreCall(1625);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out PHI2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域的 2 阶相对不变矩 PHI2（同 id 1625）。</summary>
	/// <param name="PHI2">第 1 个区域的 2 阶相对不变矩。</param>
	/// <returns>该算子第 0 个矩输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegion2ndRelInvar(out JlTuple)</c> 同一原生算子（id 1625），语义为 2 阶相对不变矩。</para>
	///   <para><b>约束或前提</b>退化/极小区域结果不稳 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时用 <c>JlTuple</c> 重载；本重载用 <c>LoadD</c> 只读每路第一个值，其余丢弃。</para>
	///   <para><b>参数取向</b>两个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double phi2;
	///   double m0 = r.MomentsRegion2ndRelInvar(out phi2);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegion2ndRelInvar(out double PHI2)
	{
		IntPtr proc = JlNativeApi.PreCall(1625);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out PHI2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>计算逐区域 2 阶旋转不变矩 m20/m02。</summary>
	/// <param name="m20">2 阶不变矩（行相关分量）。</param>
	/// <param name="m02">2 阶不变矩（列相关分量）。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE），该算子第 0 个矩输出；逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_2nd_invar</c>（原生 id 1626）：给出对平移、旋转不敏感的 2 阶矩 <c>m20</c>/<c>m02</c>（与主轴二次矩相关），可作形状分类特征。输入区域不变。</para>
	///   <para><b>约束或前提</b>退化区域归一化不稳 [待实测]；形状改变后需重算。</para>
	///   <para><b>与相邻算子的取舍</b>要单一相对不变量用 <c>MomentsRegion2ndRelInvar</c>（id 1625）；要含主轴长度 <c>ia</c>/<c>ib</c> 的未归一 2 阶矩用 <c>MomentsRegion2nd</c>（id 1627）。</para>
	///   <para><b>参数取向</b>三输出均以 <c>JlTuple.LoadNew(DOUBLE)</c> 装载；标量重载用 <c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple m20;
	///   JlTuple m02;
	///   JlTuple m0 = r.MomentsRegion2ndInvar(out m20, out m02);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 out 元组均为新对象；单值重载处理多区域元组会丢数据。</para>
	/// </remarks>
	public JlTuple MomentsRegion2ndInvar(out JlTuple m20, out JlTuple m02)
	{
		IntPtr proc = JlNativeApi.PreCall(1626);
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

	/// <summary>标量重载：取第 1 个区域的 2 阶旋转不变矩 m20/m02（同 id 1626）。</summary>
	/// <param name="m20">第 1 个区域的 2 阶不变矩（行相关分量）。</param>
	/// <param name="m02">第 1 个区域的 2 阶不变矩（列相关分量）。</param>
	/// <returns>该算子第 0 个矩输出的第 1 个值（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegion2ndInvar(out JlTuple,out JlTuple)</c> 同一原生算子（id 1626），语义为 2 阶旋转不变矩。</para>
	///   <para><b>约束或前提</b>退化区域结果不稳 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时用 <c>JlTuple</c> 重载；本重载用 <c>LoadD</c> 只读每路第一个值，其余丢弃。</para>
	///   <para><b>参数取向</b>三个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double m20;
	///   double m02;
	///   double m0 = r.MomentsRegion2ndInvar(out m20, out m02);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegion2ndInvar(out double m20, out double m02)
	{
		IntPtr proc = JlNativeApi.PreCall(1626);
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

	/// <summary>计算 2 阶中心矩 m11/m20/m02 及等价椭圆的主/次轴长 ia/ib。</summary>
	/// <param name="m20">2 阶中心矩（行相关分量）。</param>
	/// <param name="m02">2 阶中心矩（列相关分量）。</param>
	/// <param name="ia">与区域二阶矩等价的椭圆长半轴（主轴长度，像素）。</param>
	/// <param name="ib">等价椭圆短半轴（次轴长度，像素）。</param>
	/// <returns>新 <c>JlTuple</c>（DOUBLE）：惯性积 <c>m11</c>（过重心、平行于坐标轴的轴之积）；逐区域数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>moments_region_2nd</c>（原生 id 1627）：以重心为原点算 2 阶矩，并解出等价椭圆的轴长 <c>ia</c>/<c>ib</c>（描述伸展程度与方向）与惯性积 <c>m11</c>（返回值）。输入区域不变。</para>
	///   <para><b>约束或前提</b><c>ia</c>/<c>ib</c> 是"由二阶矩反推的椭圆"尺寸，不是实际外接框；退化区域（面积 0）时轴长无意义 [待实测]；形状改变后需重算。</para>
	///   <para><b>与相邻算子的取舍</b>要归一后的不变矩用 <c>MomentsRegion2ndInvar</c>（id 1626）；要主轴角用 <c>OrientationRegion</c>/<c>EllipticAxis</c> 一族。本方法给的是未归一的 2 阶矩与轴长。</para>
	///   <para><b>参数取向</b>五个输出均以 <c>JlTuple.LoadNew(DOUBLE)</c> 装载；标量重载 <c>MomentsRegion2nd(out double,...)</c> 同 id 但 <c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple m20;
	///   JlTuple m02;
	///   JlTuple ia;
	///   JlTuple ib;
	///   JlTuple m11 = r.MomentsRegion2nd(out m20, out m02, out ia, out ib);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值即惯性积 <c>m11</c>，连同四个 out 均为新对象；单值重载处理多区域元组会丢数据。</para>
	/// </remarks>
	public JlTuple MomentsRegion2nd(out JlTuple m20, out JlTuple m02, out JlTuple ia, out JlTuple ib)
	{
		IntPtr proc = JlNativeApi.PreCall(1627);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out m20);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out m02);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out ia);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out ib);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量重载：取第 1 个区域的 2 阶中心矩与等价椭圆轴长 m20/m02/ia/ib（同 id 1627）。</summary>
	/// <param name="m20">第 1 个区域的 2 阶中心矩（行相关分量）。</param>
	/// <param name="m02">第 1 个区域的 2 阶中心矩（列相关分量）。</param>
	/// <param name="ia">第 1 个区域的等价椭圆长半轴（像素）。</param>
	/// <param name="ib">第 1 个区域的等价椭圆短半轴（像素）。</param>
	/// <returns>第 1 个区域的惯性积 <c>m11</c>（<c>double</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>MomentsRegion2nd(out JlTuple,out JlTuple,out JlTuple,out JlTuple)</c> 同一原生算子（id 1627），语义为 2 阶中心矩与主轴长度。</para>
	///   <para><b>约束或前提</b>退化区域轴长无意义 [待实测]；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时用 <c>JlTuple</c> 重载；本重载用 <c>LoadD</c> 只读每路第一个值，其余丢弃。</para>
	///   <para><b>参数取向</b>五个输出均以 <c>JlNativeApi.LoadD</c> 读回 DOUBLE 标量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   double m20;
	///   double m02;
	///   double ia;
	///   double ib;
	///   double m11 = r.MomentsRegion2nd(out m20, out m02, out ia, out ib);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public double MomentsRegion2nd(out double m20, out double m02, out double ia, out double ib)
	{
		IntPtr proc = JlNativeApi.PreCall(1627);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out m20);
		err = JlNativeApi.LoadD(proc, 2, err, out m02);
		err = JlNativeApi.LoadD(proc, 3, err, out ia);
		err = JlNativeApi.LoadD(proc, 4, err, out ib);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   逐对求两组区域轮廓间的最小距离，并给出取到最小距离的那对最近点坐标。
	/// </summary>
	/// <param name="regions2">对照区域元组。</param>
	/// <param name="row1">输出：近点在 regions1 侧的行坐标（INTEGER 元组）。</param>
	/// <param name="column1">输出：近点在 regions1 侧的列坐标（INTEGER 元组）。</param>
	/// <param name="row2">输出：近点在 regions2 侧的行坐标（INTEGER 元组）。</param>
	/// <param name="column2">输出：近点在 regions2 侧的列坐标（INTEGER 元组）。</param>
	/// <returns>最小距离元组（DOUBLE，逐对）。</returns>
	/// <remarks>
	///   <para><b>成对规则</b>按英文原文是 "two regions each" 的逐对运算；两侧对象数不等时
	///   如何配对无法由托管层确定 [待实测]。重叠区域的最小距离是否为 0 [待实测]。</para>
	///   <para><b>与 <c>DistanceRrMinDil</c> 的取舍</b>只要"隔多远"的判定用 Dil（id 1629）
	///   更省；这里额外付了四个输出换回最近点坐标，间距需要标注到图上时才值。</para>
	///   <para><b>参数取向</b>返回距离 + 4 个 <c>out</c>；距离 DOUBLE、坐标 INTEGER，
	///   说明最近点取的是像素索引。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("gaps.hobj");
	///   JlRegion left = image.Threshold(120.0, 180.0).Connection();
	///   JlRegion right = image.Threshold(181.0, 255.0).Connection();
	///   double d = left.DistanceRrMin(right, out JlTuple r1, out JlTuple c1,
	///       out JlTuple r2, out JlTuple c2);
	///   left.Dispose();
	///   right.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原生 id 1628；两侧句柄 <c>KeepAlive</c> 到调用结束。
	///   注意此处 <c>d</c> 是 <c>JlTuple</c>，转标量需隐式转换或取 <c>[0]</c>。</para>
	/// </remarks>
	public JlTuple DistanceRrMin(JlRegion regions2, out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1628);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out row1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out column1);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out row2);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>
	///   两区域间最小距离与最近点（标量版）。
	/// </summary>
	/// <param name="regions2">对照区域。</param>
	/// <param name="row1">输出：近点在 regions1 侧的行坐标。</param>
	/// <param name="column1">输出：近点在 regions1 侧的列坐标。</param>
	/// <param name="row2">输出：近点在 regions2 侧的行坐标。</param>
	/// <param name="column2">输出：近点在 regions2 侧的列坐标。</param>
	/// <returns>最小距离（double 标量）。</returns>
	/// <remarks>
	///   <para>成对规则与取舍见
	///   <see cref="DistanceRrMin(JlRegion,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1628）：本重载 <c>LoadD</c>/<c>LoadI</c> 直取标量；
	///   句柄内含多个区域时只能落一个标量的限制在签名上就体现了。</para>
	/// </remarks>
	public double DistanceRrMin(JlRegion regions2, out int row1, out int column1, out int row2, out int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1628);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadI(proc, 1, err, out row1);
		err = JlNativeApi.LoadI(proc, 2, err, out column1);
		err = JlNativeApi.LoadI(proc, 3, err, out row2);
		err = JlNativeApi.LoadI(proc, 4, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return doubleValue;
	}

	/// <summary>
	///   用膨胀思路估算两组区域间的最小距离：返回 INTEGER 距离元组，不带最近点坐标。
	/// </summary>
	/// <param name="regions2">对照区域元组。</param>
	/// <returns>逐对的最小距离（<c>JlTuple</c>，INTEGER 装载）。</returns>
	/// <remarks>
	///   <para><b>整数是它的本质限制</b>实现里明确 <c>JlTupleType.INTEGER</c>（对
	///   <c>DistanceRrMin</c> 的 DOUBLE），距离按整像素计。要坐标或更细的量测值用
	///   <c>DistanceRrMin</c>（id 1628）。</para>
	///   <para><b>何时划算</b>大规模初筛"哪些对靠得太近"：拿本方法一次性出整距离向量再比较，
	///   比逐对调 <c>DistanceRrMin</c> 省；膨胀式估算的耗时分布 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("layout.hobj");
	///   JlRegion pads = image.Threshold(128.0, 255.0).Connection();
	///   JlRegion keepOut = image.Threshold(200.0, 255.0).Connection();
	///   JlTuple gaps = pads.DistanceRrMinDil(keepOut);
	///   pads.Dispose();
	///   keepOut.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原生 id 1629；空区域参与时的值 [待实测]。</para>
	/// </remarks>
	public JlTuple DistanceRrMinDil(JlRegion regions2)
	{
		IntPtr proc = JlNativeApi.PreCall(1629);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>求区域边界上相距最远的两点及其距离（卡尺直径）。</summary>
	/// <param name="row1">第 1 个极值点的行坐标（INTEGER 像素索引）。</param>
	/// <param name="column1">第 1 个极值点的列坐标（INTEGER 像素索引）。</param>
	/// <param name="row2">第 2 个极值点的行坐标（INTEGER 像素索引）。</param>
	/// <param name="column2">第 2 个极值点的列坐标（INTEGER 像素索引）。</param>
	/// <param name="diameter">两极值点间距离（DOUBLE，像素）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>diameter_region</c>（原生 id 1630）：在 <c>this</c> 各区域的边界点上找欧氏距离最大的一对，输出两端点像素坐标与其距离，作为区域"最大宽度/长度"。输入区域不变，结果为 out 参数、无返回值。</para>
	///   <para><b>约束或前提</b>端点取的是边界像素、坐标为整数索引，故直径是离散近似；退化/单像素区域两点重合、直径 0。</para>
	///   <para><b>与相邻算子的取舍</b>要外接矩形尺寸用 <c>SmallestRectangle1/2</c>；要沿主轴的轴长用 <c>MomentsRegion2nd</c> 的 <c>ia/ib</c>。本方法给的是任意方向的真实最大跨度。</para>
	///   <para><b>参数取向</b>四个坐标以 <c>LoadNew(INTEGER)</c>、<c>diameter</c> 以 <c>LoadNew(DOUBLE)</c> 逐区域装载；标量重载 <c>DiameterRegion(out int,out int,out int,out int,out double)</c> 同 id 但用 <c>LoadI</c>/<c>LoadD</c> 只读第 1 个区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple row1;
	///   JlTuple column1;
	///   JlTuple row2;
	///   JlTuple column2;
	///   JlTuple diameter;
	///   r.DiameterRegion(out row1, out column1, out row2, out column2, out diameter);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个 out 均为新元组；形状改变后需重算。</para>
	/// </remarks>
	public void DiameterRegion(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2, out JlTuple diameter)
	{
		IntPtr proc = JlNativeApi.PreCall(1630);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out row1);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out column1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out row2);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out column2);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out diameter);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>标量重载：取第 1 个区域的卡尺直径两极值点与距离（同 id 1630）。</summary>
	/// <param name="row1">第 1 个极值点行坐标（<c>int</c>）。</param>
	/// <param name="column1">第 1 个极值点列坐标（<c>int</c>）。</param>
	/// <param name="row2">第 2 个极值点行坐标（<c>int</c>）。</param>
	/// <param name="column2">第 2 个极值点列坐标（<c>int</c>）。</param>
	/// <param name="diameter">两极值点距离（<c>double</c>，像素）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>DiameterRegion(out JlTuple,...)</c> 同一原生算子（id 1630），语义为边界最远两点及距离。</para>
	///   <para><b>约束或前提</b>退化区域直径 0；本重载假定 <c>this</c> 只有一个区域。</para>
	///   <para><b>与相邻算子的取舍</b>区域元组多于一个时用 <c>JlTuple</c> 重载；本重载用 <c>LoadI</c>/<c>LoadD</c> 只读每路第一个值，其余丢弃。</para>
	///   <para><b>参数取向</b>四个坐标以 <c>LoadI</c> 读回 <c>int</c>、<c>diameter</c> 以 <c>LoadD</c> 读回 <c>double</c>；无返回值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   int row1;
	///   int column1;
	///   int row2;
	///   int column2;
	///   double diameter;
	///   r.DiameterRegion(out row1, out column1, out row2, out column2, out diameter);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多区域元组只拿到首元素值。</para>
	/// </remarks>
	public void DiameterRegion(out int row1, out int column1, out int row2, out int column2, out double diameter)
	{
		IntPtr proc = JlNativeApi.PreCall(1630);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out row1);
		err = JlNativeApi.LoadI(proc, 1, err, out column1);
		err = JlNativeApi.LoadI(proc, 2, err, out row2);
		err = JlNativeApi.LoadI(proc, 3, err, out column2);
		err = JlNativeApi.LoadD(proc, 4, err, out diameter);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>判断像素点是否落在区域内，返回 0/1 整数判定。</summary>
	/// <param name="row">待测点的行索引（元组=多点）。Default: 100</param>
	/// <param name="column">待测点的列索引（元组=多点）。Default: 100</param>
	/// <returns>布尔判定（1=在区域内，0=不在），按 <c>INTEGER</c> 读回。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>test_region_point</c>（原生 id 1631）：对 <c>this</c> 与坐标 (row,column) 做包含判定。区域不变。</para>
	///   <para><b>约束或前提</b>坐标为像素索引、闭区间含边界；点在区域外返回 0。</para>
	///   <para><b>与相邻算子的取舍</b>要"哪些区域包含该点"用 <see cref="GetRegionIndex(int,int)"/>；要区域间关系用 <c>TestRegionRelationship</c>。本方法是"点是否在此区域"。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c> 以 <c>Store</c> 钉固定、调用后 <c>UnpinTuple</c>；返回用 <c>LoadI</c> 只读第一个结果——传多点时仅第 1 点的判定被取回，其余丢弃。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple row = new double[] { 20.0 };
	///   JlTuple column = new double[] { 20.0 };
	///   int inside = r.TestRegionPoint(row, column);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回标量 int、非句柄；单点判定用 <see cref="TestRegionPoint(int,int)"/> 更直观。</para>
	/// </remarks>
	public int TestRegionPoint(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1631);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>标量重载：判断单个像素点是否落在区域内，返回 0/1（同 id 1631）。</summary>
	/// <param name="row">待测点的行索引。Default: 100</param>
	/// <param name="column">待测点的列索引。Default: 100</param>
	/// <returns>布尔判定（1=在区域内，0=不在）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>TestRegionPoint(JlTuple,JlTuple)</c> 同一原生算子（id 1631），语义为点包含判定。</para>
	///   <para><b>约束或前提</b>坐标为像素索引、含边界；区域外返回 0。</para>
	///   <para><b>与相邻算子的取舍</b>单个点用本重载（<c>StoreI</c>/<c>LoadI</c> 直写、无固定元组开销）；要一次测多点用 <c>JlTuple</c> 重载，但它也只回第 1 点的值。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c> 以 <c>StoreI</c> 作 INTEGER 控制参数，返回以 <c>LoadI</c> 读回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   int inside = r.TestRegionPoint(20, 20);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回标量 int、非句柄；无区域元组时按 <c>this</c> 首个区域判定。</para>
	/// </remarks>
	public int TestRegionPoint(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(1631);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>返回区域元组中包含给定像素的那些区域的 1 基序号。</summary>
	/// <param name="row">待测像素的行索引。Default: 100</param>
	/// <param name="column">待测像素的列索引。Default: 100</param>
	/// <returns>新 <c>JlTuple</c>（INTEGER）：命中区域的元素序号（1 基）；无命中时为空元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>get_region_index</c>（原生 id 1632）：在 <c>this</c> 这个区域数组里逐区域测该像素是否被覆盖，收集命中的序号。输入区域不变。</para>
	///   <para><b>约束或前提</b>序号是 1 基，可直接喂 <see cref="SelectObj(JlTuple)"/>；对单区域对象命中即返回 [1]。</para>
	///   <para><b>与相邻算子的取舍</b>只要序号用本方法；要直接拿到那些区域本身用 <see cref="SelectRegionPoint(int,int)"/>。单点单区域是否包含用 <c>TestRegionPoint</c>。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew(INTEGER)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlTuple idx = r.GetRegionIndex(20, 20);
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新元组对象；判空用其长度或 <c>CountObj()</c> 辅助。</para>
	/// </remarks>
	public JlTuple GetRegionIndex(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(1632);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>挑出区域元组中包含给定像素的那些区域，返回新的区域句柄。</summary>
	/// <param name="row">待测像素的行索引。Default: 100</param>
	/// <param name="column">待测像素的列索引。Default: 100</param>
	/// <returns>由命中区域组成的新 <c>JlRegion</c> 句柄（保持原顺序）；用毕需 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>select_region_point</c>（原生 id 1633）：对 <c>this</c> 逐区域测该像素是否被覆盖，把命中的区域拼成一个新的区域元组返回。输入区域不变。</para>
	///   <para><b>约束或前提</b>依赖上游元素顺序，经 <c>Connection</c> 等得到的顺序不稳定时筛选结果次序也随之变 [待实测]；无命中时返回 0 元素句柄（判空用 <c>CountObj()</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要序号而非区域本身用 <see cref="GetRegionIndex(int,int)"/>；单个点是否属于某区域用 <c>TestRegionPoint</c>。</para>
	///   <para><b>参数取向</b><c>row</c>/<c>column</c> 以 <c>StoreI</c> 作 INTEGER 控制参数；输出 <c>LoadNew</c> 新区域句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion r = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion sel = r.SelectRegionPoint(20, 20);
	///   r.Dispose();
	///   sel.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，与 <c>this</c> 不共享生命周期，须各自 <c>Dispose</c>。</para>
	/// </remarks>
	public JlRegion SelectRegionPoint(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(1633);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按上游内置的标准形状模板筛选区域：percent 是相似度门槛，不是面积区间。
	/// </summary>
	/// <param name="shape">标准形状名。Default: "max_area"</param>
	/// <param name="percent">与标准形状的相似度门槛。Default: 70.0</param>
	/// <returns>达标区域的新句柄；输入句柄不变。</returns>
	/// <remarks>
	///   <para><b>与 <c>SelectShape</c> 的分工</b>已知数值区间（"面积 300 以上"）用
	///   <c>SelectShape</c>；"最接近圆/方/最大面积的那批"交给本算子的标准形状比对，
	///   免自己推阈值。可选 shape 字面量托管层只给出默认值 "max_area" [待实测]。</para>
	///   <para><b>易错</b>percent 越高筛得越严；percent 语义（相似度百分比）按英文原文
	///   "Similarity measure" 理解，换算细节 [待实测]。筛选同样要求先 <c>Connection</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0).Connection();
	///   using JlRegion round = blobs.SelectShapeStd("max_area", 90.0);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原生 id 1634，<c>StoreS</c>/<c>StoreD</c> 直写、无元组操作；
	///   返回新句柄。</para>
	/// </remarks>
	public JlRegion SelectShapeStd(string shape, double percent)
	{
		IntPtr proc = JlNativeApi.PreCall(1634);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, shape);
		JlNativeApi.StoreD(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按指定方式对齐两批区域后计算汉明距离与相似度。</summary>
	/// <param name="regions2">作为对照的第二批区域。</param>
	/// <param name="norm">归一化/对齐方式（字符串元组）。Default: "center"</param>
	/// <param name="similarity">相似度（DOUBLE，逐对），与返回值一一对应。</param>
	/// <returns>新 <c>JlTuple</c>（INTEGER）：逐区域对的汉明距离（不同像素数）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>hamming_distance_norm</c>（原生 id 1635）：先按 <c>norm</c> 把 <c>this</c>（Regions1）与 <c>regions2</c>（Regions2）对齐（如按重心归一），再统计两区域不重合像素数（汉明距离）及其派生相似度。输入区域不变。</para>
	///   <para><b>约束或前提</b><c>norm</c> 声明为 <c>JlTuple</c> 但内容是方式字符串；两批区域按元素配对，元素数不等时的行为 [待实测]。距离随区域面积增大而增大，跨尺寸比较请用相似度或各自归一。</para>
	///   <para><b>与相邻算子的取舍</b>要形状相似性判据用 <c>select_shape_std</c>/<c>TestRegionRelationship</c>；本方法专给"逐像素差异量"，常用于对齐后的一致性度量。</para>
	///   <para><b>参数取向</b><c>norm</c> 以 <c>Store</c> 钉固定字符串元组、调用后 <c>UnpinTuple</c>；汉明距离 <c>LoadNew(INTEGER)</c>、相似度 <c>LoadNew(DOUBLE)</c>。标量重载 <c>HammingDistanceNorm(JlRegion,string,out double)</c> 同 id 但 <c>StoreS</c>/<c>LoadI</c>/<c>LoadD</c> 只读第 1 对。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion b = new JlRegion(12.0, 12.0, 32.0, 32.0);
	///   JlTuple norm = new string[] { "center" };
	///   JlTuple similarity;
	///   JlTuple dist = a.HammingDistanceNorm(b, norm, out similarity);
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 out 元组均为新对象；完全重叠时汉明距离 0。</para>
	/// </remarks>
	public JlTuple HammingDistanceNorm(JlRegion regions2, JlTuple norm, out JlTuple similarity)
	{
		IntPtr proc = JlNativeApi.PreCall(1635);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.Store(proc, 0, norm);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(norm);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out similarity);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>标量重载：对齐后取第 1 对区域的汉明距离与相似度（同 id 1635）。</summary>
	/// <param name="regions2">作为对照的第二批区域。</param>
	/// <param name="norm">归一化/对齐方式（单个字符串）。Default: "center"</param>
	/// <param name="similarity">第 1 对区域的相似度（<c>double</c>）。</param>
	/// <returns>第 1 对区域的汉明距离（<c>int</c>，不同像素数）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>HammingDistanceNorm(JlRegion,JlTuple,out JlTuple)</c> 同一原生算子（id 1635），语义为对齐后逐像素差异度量。</para>
	///   <para><b>约束或前提</b>两区域按首元素配对；本重载假定只要第一对的量。</para>
	///   <para><b>与相邻算子的取舍</b>成批比较用 <c>JlTuple</c> 重载；本重载 <c>StoreS</c>/<c>LoadI</c>/<c>LoadD</c> 只读第一对，多对数据被丢弃。</para>
	///   <para><b>参数取向</b><c>norm</c> 以 <c>StoreS</c> 直写字符串，汉明距离以 <c>LoadI</c>、相似度以 <c>LoadD</c> 读回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(10.0, 10.0, 30.0, 30.0);
	///   JlRegion b = new JlRegion(12.0, 12.0, 32.0, 32.0);
	///   double similarity;
	///   int dist = a.HammingDistanceNorm(b, "center", out similarity);
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无句柄分配；坑在于多对区域只拿到第一对结果。</para>
	/// </remarks>
	public int HammingDistanceNorm(JlRegion regions2, string norm, out double similarity)
	{
		IntPtr proc = JlNativeApi.PreCall(1635);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.StoreS(proc, 0, norm);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out similarity);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return intValue;
	}

	/// <summary>
	///   Hamming distance between two regions.
	/// </summary>
	/// <param name="regions2">Comparative regions.</param>
	/// <param name="similarity">Similarity of two regions.</param>
	/// <returns>Hamming distance of two regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Hamming 距离 between two 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>点线几何量测</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions2 = ...;
	///   JlRegion obj = ...;
	///   var result = obj.HammingDistance(regions2, out JlTuple similarity);
	///   </code>
	/// </remarks>
	public JlTuple HammingDistance(JlRegion regions2, out JlTuple similarity)
	{
		IntPtr proc = JlNativeApi.PreCall(1636);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out similarity);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>
	///   Hamming distance between two regions.
	/// </summary>
	/// <param name="regions2">Comparative regions.</param>
	/// <param name="similarity">Similarity of two regions.</param>
	/// <returns>Hamming distance of two regions.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Hamming 距离 between two 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>点线几何量测</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions2 = ...;
	///   JlRegion obj = ...;
	///   var result = obj.HammingDistance(regions2, out double similarity);
	///   </code>
	/// </remarks>
	public int HammingDistance(JlRegion regions2, out double similarity)
	{
		IntPtr proc = JlNativeApi.PreCall(1636);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out similarity);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return intValue;
	}

	/// <summary>
	///   Shape features derived from the ellipse parameters.
	/// </summary>
	/// <param name="bulkiness">Calculated shape feature.</param>
	/// <param name="structureFactor">Calculated shape feature.</param>
	/// <returns>Shape feature (in case of a circle = 1.0).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape features derived 从 椭圆 参数。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Eccentricity(out JlTuple bulkiness, out JlTuple structureFactor);
	///   </code>
	/// </remarks>
	public JlTuple Eccentricity(out JlTuple bulkiness, out JlTuple structureFactor)
	{
		IntPtr proc = JlNativeApi.PreCall(1637);
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
	///   Shape features derived from the ellipse parameters.
	/// </summary>
	/// <param name="bulkiness">Calculated shape feature.</param>
	/// <param name="structureFactor">Calculated shape feature.</param>
	/// <returns>Shape feature (in case of a circle = 1.0).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape features derived 从 椭圆 参数。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Eccentricity(out double bulkiness, out double structureFactor);
	///   </code>
	/// </remarks>
	public double Eccentricity(out double bulkiness, out double structureFactor)
	{
		IntPtr proc = JlNativeApi.PreCall(1637);
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
	///   Calculate the Euler number.
	/// </summary>
	/// <returns>Calculated Euler number.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 Euler number。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.EulerNumber();
	///   </code>
	/// </remarks>
	public JlTuple EulerNumber()
	{
		IntPtr proc = JlNativeApi.PreCall(1638);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Orientation of a region.
	/// </summary>
	/// <returns>Orientation of region (arc measure).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Orientation 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.OrientationRegion();
	///   </code>
	/// </remarks>
	public JlTuple OrientationRegion()
	{
		IntPtr proc = JlNativeApi.PreCall(1639);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Calculate the parameters of the equivalent ellipse.
	/// </summary>
	/// <param name="rb">Secondary radius (normalized to the area).</param>
	/// <param name="phi">Angle between main radius and x-axis in radians.</param>
	/// <returns>Main radius (normalized to the area).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 参数 equivalent 椭圆。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.EllipticAxis(out JlTuple rb, out JlTuple phi);
	///   </code>
	/// </remarks>
	public JlTuple EllipticAxis(out JlTuple rb, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1640);
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
	///   Calculate the parameters of the equivalent ellipse.
	/// </summary>
	/// <param name="rb">Secondary radius (normalized to the area).</param>
	/// <param name="phi">Angle between main radius and x-axis in radians.</param>
	/// <returns>Main radius (normalized to the area).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 参数 equivalent 椭圆。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.EllipticAxis(out double rb, out double phi);
	///   </code>
	/// </remarks>
	public double EllipticAxis(out double rb, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1640);
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
	///   Pose relation of regions.
	/// </summary>
	/// <param name="regions2">Comparative regions</param>
	/// <param name="direction">Desired neighboring relation. Default: "left"</param>
	/// <param name="regionIndex2">Indices in the input tuples (Regions1 or ParRef{Regions2}), respectively.</param>
	/// <returns>Indices in the input tuples (Regions1 or ParRef{Regions2}), respectively.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>位姿 relation 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions2 = ...;
	///   JlRegion obj = ...;
	///   var result = obj.SelectRegionSpatial(regions2, "left", out JlTuple regionIndex2);
	///   </code>
	/// </remarks>
	public JlTuple SelectRegionSpatial(JlRegion regions2, string direction, out JlTuple regionIndex2)
	{
		IntPtr proc = JlNativeApi.PreCall(1641);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.StoreS(proc, 0, direction);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out regionIndex2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>
	///   Pose relation of regions with regard to
	/// </summary>
	/// <param name="regions2">Comparative regions.</param>
	/// <param name="percent">Percentage of the area of the comparative region which must be located left/right or  Default: 50</param>
	/// <param name="regionIndex2">Indices of the regions in the tuple of the input regions which fulfill the pose relation.</param>
	/// <param name="relation1">Vertical pose relation in which RegionIndex2[n] stands with RegionIndex1[n].</param>
	/// <param name="relation2">Horizontal pose relation in which RegionIndex2[n] stands with RegionIndex1[n].</param>
	/// <returns>Indices of the regions in the tuple of the input regions which fulfill the pose relation.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>位姿 relation 区域 使用 regard。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion regions2 = ...;
	///   JlRegion obj = ...;
	///   var result = obj.SpatialRelation(regions2, 50, out JlTuple regionIndex2, out JlTuple relation1, out JlTuple relation2);
	///   </code>
	/// </remarks>
	public JlTuple SpatialRelation(JlRegion regions2, int percent, out JlTuple regionIndex2, out JlTuple relation1, out JlTuple relation2)
	{
		IntPtr proc = JlNativeApi.PreCall(1642);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, regions2);
		JlNativeApi.StoreI(proc, 0, percent);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out regionIndex2);
		err = JlTuple.LoadNew(proc, 2, err, out relation1);
		err = JlTuple.LoadNew(proc, 3, err, out relation2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions2);
		return tuple;
	}

	/// <summary>
	///   Shape factor for the convexity of a region.
	/// </summary>
	/// <returns>Convexity of the input region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape factor 用于 convexity 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Convexity();
	///   </code>
	/// </remarks>
	public JlTuple Convexity()
	{
		IntPtr proc = JlNativeApi.PreCall(1643);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Contour length of a region.
	/// </summary>
	/// <returns>Contour length of the input region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>轮廓 length 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Contlength();
	///   </code>
	/// </remarks>
	public JlTuple Contlength()
	{
		IntPtr proc = JlNativeApi.PreCall(1644);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Number of connection components and holes
	/// </summary>
	/// <param name="numHoles">Number of holes of a region.</param>
	/// <returns>Number of connection components of a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Number 连通域分析 components 和 holes。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.ConnectAndHoles(out JlTuple numHoles);
	///   </code>
	/// </remarks>
	public JlTuple ConnectAndHoles(out JlTuple numHoles)
	{
		IntPtr proc = JlNativeApi.PreCall(1645);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out numHoles);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Number of connection components and holes
	/// </summary>
	/// <param name="numHoles">Number of holes of a region.</param>
	/// <returns>Number of connection components of a region.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Number 连通域分析 components 和 holes。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.ConnectAndHoles(out int numHoles);
	///   </code>
	/// </remarks>
	public int ConnectAndHoles(out int numHoles)
	{
		IntPtr proc = JlNativeApi.PreCall(1645);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadI(proc, 1, err, out numHoles);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Shape factor for the rectangularity of a region.
	/// </summary>
	/// <returns>Rectangularity of the input region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape factor 用于 rectangularity 区域。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Rectangularity();
	///   </code>
	/// </remarks>
	public JlTuple Rectangularity()
	{
		IntPtr proc = JlNativeApi.PreCall(1646);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape factor for the compactness of a region.
	/// </summary>
	/// <returns>Compactness of the input region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape factor 用于 compactness 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>区域或轮廓特征计算</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Compactness();
	///   </code>
	/// </remarks>
	public JlTuple Compactness()
	{
		IntPtr proc = JlNativeApi.PreCall(1647);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape factor for the circularity (similarity to a circle) of a region.
	/// </summary>
	/// <returns>Circularity of the input region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape factor for the circularity (similarity to a circle) of a region。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Circularity();
	///   </code>
	/// </remarks>
	public JlTuple Circularity()
	{
		IntPtr proc = JlNativeApi.PreCall(1648);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Compute the area of holes of regions.
	/// </summary>
	/// <returns>Area(s) of holes of the region(s).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>计算 area holes 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>区域或轮廓特征计算</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.AreaHoles();
	///   </code>
	/// </remarks>
	public JlTuple AreaHoles()
	{
		IntPtr proc = JlNativeApi.PreCall(1649);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   各区域的像素面积与几何重心：面积走返回值，重心行、列按顺序走两个 out。
	/// </summary>
	/// <param name="row">输出：重心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">输出：重心列坐标（DOUBLE 元组）。</param>
	/// <returns>面积（INTEGER 装载的 <c>JlTuple</c>，逐区域）。</returns>
	/// <remarks>
	///   <para><b>出参顺序必须按签名记</b>原生出参布局是 [0]=面积、[1]=行、[2]=列；
	///   面积不在 out 里而在返回值里。实现里 row 先于 column 装载，写反行列不会报错、
	///   只会把坐标换轴。</para>
	///   <para><b>类型不对称</b>面积是 INTEGER（像素计数），重心是 DOUBLE——像素中心约定下
	///   重心可以落在两像素之间 [待实测其精确参考点]。</para>
	///   <para><b>与 <c>AreaCenterGray</c> 的取舍</b>几何重心对像素一视同仁；
	///   要灰度加权（亮斑定位）用 id 1683 的灰度版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("pellets.hobj");
	///   JlRegion blobs = image.Threshold(100.0, 255.0).Connection();
	///   JlTuple area = blobs.AreaCenter(out JlTuple row, out JlTuple column);
	///   blobs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原生 id 1650；环形区域的重心可落在孔内（不在区域内），
	///   拿它当抓取点前先 <c>TestRegionPoints</c> 验证 [待实测]。</para>
	/// </remarks>
	public JlTuple AreaCenter(out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1650);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   面积与重心（标量版）：单区域时最省事的重载。
	/// </summary>
	/// <param name="row">输出：重心行坐标。</param>
	/// <param name="column">输出：重心列坐标。</param>
	/// <returns>面积（int 标量）。</returns>
	/// <remarks>
	///   <para>出参布局（面积在返回值、行先列后）见 <see cref="AreaCenter(out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1650）：本重载 <c>LoadI</c>/<c>LoadD</c> 直取标量、不建
	///   <c>JlTuple</c>；句柄内含多个区域时的取值行为 [待实测]。</para>
	/// </remarks>
	public int AreaCenter(out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1650);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row);
		err = JlNativeApi.LoadD(proc, 2, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Distribution of runs needed for runlength encoding of a region.
	/// </summary>
	/// <param name="background">Length distribution of the background.</param>
	/// <returns>Length distribution of the region (foreground).</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Distribution of runs needed for runlength encoding of a region。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.RunlengthDistribution(out JlTuple background);
	///   </code>
	/// </remarks>
	public JlTuple RunlengthDistribution(out JlTuple background)
	{
		IntPtr proc = JlNativeApi.PreCall(1651);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out background);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape factors from contour.
	/// </summary>
	/// <param name="sigma">Standard deviation of Distance.</param>
	/// <param name="roundness">Shape factor for roundness.</param>
	/// <param name="sides">Number of polygon sides.</param>
	/// <returns>Mean distance from the center.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape factors 从 轮廓。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Roundness(out JlTuple sigma, out JlTuple roundness, out JlTuple sides);
	///   </code>
	/// </remarks>
	public JlTuple Roundness(out JlTuple sigma, out JlTuple roundness, out JlTuple sides)
	{
		IntPtr proc = JlNativeApi.PreCall(1652);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out sigma);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out roundness);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out sides);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Shape factors from contour.
	/// </summary>
	/// <param name="sigma">Standard deviation of Distance.</param>
	/// <param name="roundness">Shape factor for roundness.</param>
	/// <param name="sides">Number of polygon sides.</param>
	/// <returns>Mean distance from the center.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Shape factors 从 轮廓。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   var result = obj.Roundness(out double sigma, out double roundness, out double sides);
	///   </code>
	/// </remarks>
	public double Roundness(out double sigma, out double roundness, out double sides)
	{
		IntPtr proc = JlNativeApi.PreCall(1652);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out sigma);
		err = JlNativeApi.LoadD(proc, 2, err, out roundness);
		err = JlNativeApi.LoadD(proc, 3, err, out sides);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   Largest inner rectangle of a region.
	/// </summary>
	/// <param name="row1">Row coordinate of the upper left corner point.</param>
	/// <param name="column1">Column coordinate of the upper left corner point.</param>
	/// <param name="row2">Row coordinate of the lower right corner point.</param>
	/// <param name="column2">Column coordinate of the lower right corner point.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Largest inner 矩形 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>点线几何量测</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   obj.InnerRectangle1(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2);
	///   </code>
	/// </remarks>
	public void InnerRectangle1(out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1653);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out row1);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out column1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out row2);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Largest inner rectangle of a region.
	/// </summary>
	/// <param name="row1">Row coordinate of the upper left corner point.</param>
	/// <param name="column1">Column coordinate of the upper left corner point.</param>
	/// <param name="row2">Row coordinate of the lower right corner point.</param>
	/// <param name="column2">Column coordinate of the lower right corner point.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Largest inner 矩形 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>点线几何量测</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlRegion obj = ...;
	///   obj.InnerRectangle1(out int row1, out int column1, out int row2, out int column2);
	///   </code>
	/// </remarks>
	public void InnerRectangle1(out int row1, out int column1, out int row2, out int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1653);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out row1);
		err = JlNativeApi.LoadI(proc, 1, err, out column1);
		err = JlNativeApi.LoadI(proc, 2, err, out row2);
		err = JlNativeApi.LoadI(proc, 3, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   区域内最大可容纳的圆（内切最大圆）：圆心、半径逐区域输出。
	/// </summary>
	/// <param name="row">输出：内切圆圆心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">输出：内切圆圆心列坐标（DOUBLE 元组）。</param>
	/// <param name="radius">输出：内切圆半径（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para><b>它量什么</b>半径对应区域"最厚"的去处，是 <c>SmallestCircle</c>（外接，
	///   id 1619）的另一半；两者半径之比还能粗看实心程度。圆心不保证唯一
	///   （矩形中心附近有整片等价位置）[待实测]。</para>
	///   <para><b>约束</b>与图像尺寸无关——内切圆总是有界的，不受 <c>Complement</c>
	///   那类画幅问题影响；空区域的输出 [待实测]。</para>
	///   <para><b>参数取向</b>void + 3 个 <c>out</c>，全部 DOUBLE 元组，逐区域对齐。
	///   原生 id 1654。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("seal.hobj");
	///   JlRegion blob = image.Threshold(100.0, 255.0);
	///   blob.InnerCircle(out JlTuple row, out JlTuple column, out JlTuple radius);
	///   blob.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>只读量测，不产生句柄；找细颈/最窄通道要的是距离变换
	///   最小值方向，别拿本算子替代。</para>
	/// </remarks>
	public void InnerCircle(out JlTuple row, out JlTuple column, out JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1654);
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
	///   最大内切圆（标量版）。
	/// </summary>
	/// <param name="row">输出：内切圆圆心行坐标。</param>
	/// <param name="column">输出：内切圆圆心列坐标。</param>
	/// <param name="radius">输出：内切圆半径。</param>
	/// <remarks>
	///   <para>量测含义与外接圆的分工见 <see cref="InnerCircle(out JlTuple,out JlTuple,out JlTuple)"/>。
	///   两个重载同一原生算子（id 1654）：本重载三个 <c>LoadD</c> 直取标量、不建
	///   <c>JlTuple</c>；单区域用它。</para>
	/// </remarks>
	public void InnerCircle(out double row, out double column, out double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1654);
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
	///   Determine a histogram of features along all threshold values.
	/// </summary>
	/// <param name="image">Gray value image.</param>
	/// <param name="feature">Feature to be examined. Default: "convexity"</param>
	/// <param name="row">Row of the pixel which the region must contain. Default: 256</param>
	/// <param name="column">Column of the pixel which the region must contain. Default: 256</param>
	/// <param name="relativeHisto">Relative distribution of the feature.</param>
	/// <returns>Absolute distribution of the feature.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>确定 histogram features along all 阈值分割 值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlRegion obj = ...;
	///   var result = obj.ShapeHistoPoint(image, "convexity", 256, 256, out JlTuple relativeHisto);
	///   </code>
	/// </remarks>
	public JlTuple ShapeHistoPoint(JlImage image, string feature, int row, int column, out JlTuple relativeHisto)
	{
		IntPtr proc = JlNativeApi.PreCall(1666);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreS(proc, 0, feature);
		JlNativeApi.StoreI(proc, 1, row);
		JlNativeApi.StoreI(proc, 2, column);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out relativeHisto);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   Determine a histogram of features along all threshold values.
	/// </summary>
	/// <param name="image">Gray value image.</param>
	/// <param name="feature">Feature to be examined. Default: "connected_components"</param>
	/// <param name="relativeHisto">Relative distribution of the feature.</param>
	/// <returns>Absolute distribution of the feature.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>确定 histogram features along all 阈值分割 值。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlRegion obj = ...;
	///   var result = obj.ShapeHistoAll(image, "connected_components", out JlTuple relativeHisto);
	///   </code>
	/// </remarks>
	public JlTuple ShapeHistoAll(JlImage image, string feature, out JlTuple relativeHisto)
	{
		IntPtr proc = JlNativeApi.PreCall(1667);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreS(proc, 0, feature);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out relativeHisto);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   Calculates gray value features for a set of regions.
	/// </summary>
	/// <param name="image">Gray value image.</param>
	/// <param name="features">Names of the features. Default: "mean"</param>
	/// <returns>Values of the features.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Calculates 灰度值 features 用于 设置 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlRegion obj = ...;
	///   var result = obj.GrayFeatures(image, "mean");
	///   </code>
	/// </remarks>
	public JlTuple GrayFeatures(JlImage image, JlTuple features)
	{
		IntPtr proc = JlNativeApi.PreCall(1668);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 0, features);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(features);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   Calculates gray value features for a set of regions.
	/// </summary>
	/// <param name="image">Gray value image.</param>
	/// <param name="features">Names of the features. Default: "mean"</param>
	/// <returns>Values of the features.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Calculates 灰度值 features 用于 设置 区域。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlRegion obj = ...;
	///   var result = obj.GrayFeatures(image, "mean");
	///   </code>
	/// </remarks>
	public double GrayFeatures(JlImage image, string features)
	{
		IntPtr proc = JlNativeApi.PreCall(1668);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreS(proc, 0, features);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return doubleValue;
	}

	/// <summary>
	///   Select regions based on gray value features.
	/// </summary>
	/// <param name="image">Gray value image.</param>
	/// <param name="features">Names of the features. Default: "mean"</param>
	/// <param name="operation">Logical connection of features. Default: "and"</param>
	/// <param name="min">Lower limit(s) of features or 'min'. Default: 128.0</param>
	/// <param name="max">Upper limit(s) of features or 'max'. Default: 255.0</param>
	/// <returns>Regions having features within the limits.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>选择 区域 based 在 灰度值 features。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlRegion obj = ...;
	///   var result = obj.SelectGray(image, "mean", "and", 128.0, 255.0);
	///   </code>
	/// </remarks>
	public JlRegion SelectGray(JlImage image, JlTuple features, string operation, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(1669);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   Select regions based on gray value features.
	/// </summary>
	/// <param name="image">Gray value image.</param>
	/// <param name="features">Names of the features. Default: "mean"</param>
	/// <param name="operation">Logical connection of features. Default: "and"</param>
	/// <param name="min">Lower limit(s) of features or 'min'. Default: 128.0</param>
	/// <param name="max">Upper limit(s) of features or 'max'. Default: 255.0</param>
	/// <returns>Regions having features within the limits.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>选择 区域 based 在 灰度值 features。</para>
	///   <para><b>典型场景</b></para>
	///   <para>颜色空间与通道处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlImage image = ...;
	///   JlRegion obj = ...;
	///   var result = obj.SelectGray(image, "mean", "and", 128.0, 255.0);
	///   </code>
	/// </remarks>
	public JlRegion SelectGray(JlImage image, string features, string operation, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(1669);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreS(proc, 0, features);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.StoreD(proc, 2, min);
		JlNativeApi.StoreD(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	///   区域灰度极值（元组版）：逐区域返回稳健 min/max/range 三条 DOUBLE 元组。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="percent">忽略极端值的容忍百分比（元组；逐域还是共用 [待实测]）。Default: 0</param>
	/// <param name="min">各区域（稳健）最小灰度。</param>
	/// <param name="max">各区域（稳健）最大灰度。</param>
	/// <param name="range">各区域 max − min。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1670，与标量重载同一算子；三条输出
	///   <c>LoadNew</c>+DOUBLE 整段装载、与本句柄区域元组逐位对齐。</para>
	///   <para><b>约束或前提</b>percent 经 <c>Store</c> 钉固定、调用后 <c>UnpinTuple</c>；
	///   口径与标量版相同 [待实测]。某域为空区域时该域元素的值 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只处理一个域用
	///   <see cref="MinMaxGray(JlImage,double,out double,out double,out double)"/>，
	///   免去建元组；要"逐域亮度均匀性"直接比 range 元组即可，不必回算 Intensity。</para>
	///   <para><b>参数取向</b>void 返回，min/max/range 全走 out。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("ocr.hobj");
	///   JlRegion glyphs = img.Threshold(50.0, 255.0).Connection();
	///   glyphs.MinMaxGray(img, new JlTuple(3.0), out JlTuple min, out JlTuple max, out JlTuple range);
	///   glyphs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组免释放；图像与区域 <c>KeepAlive</c> 到调用结束；
	///   percent=3.0 这类容忍值能防单像素噪声顶爆极值。</para>
	/// </remarks>
	public void MinMaxGray(JlImage image, JlTuple percent, out JlTuple min, out JlTuple max, out JlTuple range)
	{
		IntPtr proc = JlNativeApi.PreCall(1670);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   区域灰度极值（标量版）：percent=0 时给真 min/max；percent&gt;0 时给掐尾后的"稳健"极值。多区域时只读第一个域。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="percent">忽略极端值的容忍百分比（原文：低于绝对最大值/高于绝对最小值的百分比；精确口径 [待实测]）。Default: 0</param>
	/// <param name="min">（稳健）最小灰度。</param>
	/// <param name="max">（稳健）最大灰度。</param>
	/// <param name="range">max − min。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1670：单区域一趟出三值，range 免手减。
	///   percent 的计量口径（按面积占比还是按直方图计数、双侧各掐多少）[待实测]。</para>
	///   <para><b>约束或前提</b>三个输出都 <c>LoadD</c> 只读第一值，多区域元组会静默
	///   丢其余域，逐域统计须用元组重载
	///   <see cref="MinMaxGray(JlImage,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；
	///   区域必须非空。</para>
	///   <para><b>与相邻算子的取舍</b>要均值/散布用 <c>Intensity</c>；要动态范围压缩
	///   前的上下界，percent 给 1~3 比真极值稳得多（一个噪声亮点就能把 max 顶爆）。</para>
	///   <para><b>参数取向</b>void 返回，min/max/range 全走 out；percent 是控制参数
	///   <c>StoreD</c> 直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("ocr.hobj");
	///   JlRegion glyph = img.Threshold(50.0, 255.0);
	///   glyph.MinMaxGray(img, 0.0, out double min, out double max, out double range);
	///   glyph.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；返回的是灰度
	///   数值（DOUBLE 装载），int2 图也能给到 16 位量程。</para>
	/// </remarks>
	public void MinMaxGray(JlImage image, double percent, out double min, out double max, out double range)
	{
		IntPtr proc = JlNativeApi.PreCall(1670);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   区域灰度均值与偏差（元组版）：逐区域返回均值元组，out 各域偏差元组。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="deviation">各区域灰度散布度（DOUBLE 元组）。</param>
	/// <returns>各区域灰度均值（DOUBLE 元组，新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1671：两输出按 <c>LoadNew</c>+DOUBLE 整段装载，
	///   与本句柄区域元组逐位对齐；先 <c>Connection()</c> 再统计时，域序=结果序。</para>
	///   <para><b>约束或前提</b>单区域也用本重载会返回 1 元素元组，取值前注意长度；
	///   空区域（0 像素）对应元素的口径 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要第一域的均值+偏差用
	///   <see cref="Intensity(JlImage,out double)"/>（<c>LoadD</c> 直取、不建元组）；
	///   要灰度端点用 <c>MinMaxGray</c>，要更多统计量用 <c>GrayFeatures</c>。</para>
	///   <para><b>参数取向</b>均值走返回值、偏差走 out，与标量版一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("coins.hobj");
	///   JlRegion coins = img.Threshold(100.0, 255.0).Connection();
	///   JlTuple mean = coins.Intensity(img, out JlTuple deviation);
	///   coins.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组免释放；图像与区域 <c>KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public JlTuple Intensity(JlImage image, out JlTuple deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(1671);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   区域灰度均值与偏差（标量版）：返回均值，out 标准差型偏差；多区域时只读第一个值。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="deviation">区域内灰度散布度（是否按像素数归一的样本标准差 [待实测]）。</param>
	/// <returns>区域内灰度均值（double 标量）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1671：一趟得到"这片多亮 + 多花"两个数，常配成
	///   mean±k·deviation 做自适应阈值或合格带判断。</para>
	///   <para><b>约束或前提</b>两个输出都 <c>LoadD</c> 只取第一值——本句柄为多区域
	///   元组时会静默丢弃其余域的结果，逐域统计须用元组重载
	///   <see cref="Intensity(JlImage,out JlTuple)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>背景有斜变时 mean/deviation 都被斜面拉高，改用
	///   <c>PlaneDeviation</c>；只要 min/max 端点用 <c>MinMaxGray</c>；要按特征名取
	///   更宽的菜单用 <c>GrayFeatures</c>。</para>
	///   <para><b>参数取向</b>均值走返回值、偏差走 out，均 DOUBLE。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("coins.hobj");
	///   JlRegion coin = img.Threshold(100.0, 255.0);
	///   double mean = coin.Intensity(img, out double deviation);
	///   coin.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>空区域（0 像素）时两个值 [待实测]；图像与区域
	///   <c>KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public double Intensity(JlImage image, out double deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(1671);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return doubleValue;
	}

	/// <summary>
	///   指定灰度区间的直方图（元组界版）：返回整条直方图的 INTEGER 元组，out 给出实际 bin 宽。
	/// </summary>
	/// <param name="image">输入单通道图像。</param>
	/// <param name="min">统计区间下界（元组，通常单值；多值语义 [待实测]）。Default: 0</param>
	/// <param name="max">统计区间上界（元组，与 min 成对）。Default: 255</param>
	/// <param name="numBins">bin 数量。Default: 256</param>
	/// <param name="binSize">实际灰度/bin 宽度（单个 double，非逐域数组）。</param>
	/// <returns>各 bin 的像素计数（INTEGER 元组，新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1672：与标量界重载同一算子，但这里直方图按
	///   <c>LoadNew</c> 整段装载，能拿到完整分布；binSize 仍是单值——多区间时哪个
	///   bin 宽被报出 [待实测]。</para>
	///   <para><b>约束或前提</b>只统计区域覆盖且落在 [min,max] 的灰度；区间外像素
	///   被丢弃（不是并入边缘 bin）[待实测]。min/max 钉固定后调用、随即解固定。</para>
	///   <para><b>与相邻算子的取舍</b>要"绝对量程+自动分箱"用 <c>GrayHistoAbs</c>；
	///   本算子用于跨批次对齐同一分箱方案（如比较两批产品的 64-bin 曲线）。</para>
	///   <para><b>参数取向</b>直方图走返回值（INTEGER 装载），binSize 走 out（DOUBLE）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("parts.hobj");
	///   JlRegion part = img.Threshold(80.0, 200.0);
	///   JlTuple histo = part.GrayHistoRange(img, new JlTuple(0.0), new JlTuple(255.0), 64, out double binSize);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>bin 数 × binSize 决定曲线横轴；结果元组免释放，
	///   图像与区域 <c>KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public JlTuple GrayHistoRange(JlImage image, JlTuple min, JlTuple max, int numBins, out double binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1672);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   指定灰度区间的直方图（标量界版）。警告：返回的 int 只是第一个 bin 的计数，拿不到整条直方图。
	/// </summary>
	/// <param name="image">输入单通道图像。</param>
	/// <param name="min">直方图统计区间下界。Default: 0</param>
	/// <param name="max">直方图统计区间上界。Default: 255</param>
	/// <param name="numBins">bin 数量。Default: 256</param>
	/// <param name="binSize">实际灰度/bin 宽度 = (max−min)/numBins 的返回值 [待实测]。</param>
	/// <returns>仅直方图首 bin 的像素计数（<c>LoadI</c> 只读第一值）——要整条直方图请用元组重载。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1672：把区域内落在 [min,max] 的灰度均分进
	///   numBins 个 bin。标量重载的返回值签名决定了它只能吐出 INTEGER 输出的第一个
	///   元素，这是真坑：除"统计最小灰度附近有多少像素"外没有实用价值。</para>
	///   <para><b>约束或前提</b>区间外的像素不计入；bin 边界（首 bin 是否含 min、
	///   末 bin 是否含 max）[待实测]。区域必须与图像同坐标系。</para>
	///   <para><b>与相邻算子的取舍</b>要整条直方图务必用
	///   <see cref="GrayHistoRange(JlImage,JlTuple,JlTuple,int,out double)"/>；不想管
	///   区间、自动全量程用 <c>GrayHisto</c>/<c>GrayHistoAbs</c>。</para>
	///   <para><b>参数取向</b>min/max 经 <c>StoreD</c>、numBins 经 <c>StoreI</c> 直写；
	///   binSize 是唯一的 out。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("parts.hobj");
	///   JlRegion part = img.Threshold(80.0, 200.0);
	///   int firstBin = part.GrayHistoRange(img, 0.0, 255.0, 64, out double binSize);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；把 firstBin 当
	///   "直方图"用是静默错误。</para>
	/// </remarks>
	public int GrayHistoRange(JlImage image, double min, double max, int numBins, out double binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1672);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
		return intValue;
	}

	/// <summary>
	///   二维联合直方图：统计区域内两个单通道图像灰度对的共现次数，结果以图像形式返回。
	/// </summary>
	/// <param name="imageCol">充当直方图一个轴（"col"轴）的单通道图像。</param>
	/// <param name="imageRow">充当另一轴的单通道图像，须与 imageCol 同尺寸。</param>
	/// <returns>承载二维直方图的新 <c>JlImage</c> 句柄（像素值=共现计数 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1673：把区域内每个像素的两通道灰度 (a,b) 投到
	///   二维网格上计数，典型用于颜色空间（如 H/S 两分量）的目标配色统计。哪个参数
	///   对应输出图像的哪个轴，文档名（Col/Row）与直觉可能相反 [待实测]。</para>
	///   <para><b>约束或前提</b>两输入应为同尺寸的单通道图；多通道图直接传入的行为
	///   [待实测]——稳妥做法是先各自取出一个通道。区域外的像素不计入。</para>
	///   <para><b>与相邻算子的取舍</b>单通道分布用 <c>GrayHisto</c> 族（返回元组更轻）；
	///   只有"两变量相关性"需求才上本算子，因为输出是张图，读取峰值还要再做一步。</para>
	///   <para><b>参数取向</b>唯一输出走返回值，按 <c>JlImage.LoadNew</c> 装载为新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("bottle.hobj");
	///   JlRegion sample = new JlRegion(10.0, 10.0, 110.0, 110.0);
	///   using JlImage histo2d = sample.Histo2dim(img, img);   // 演示:同通道自相关
	///   sample.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输出是图像句柄，用完必须 Dispose；输入图像与区域
	///   <c>KeepAlive</c> 到调用结束；输出图像尺寸与灰度类型 [待实测]。</para>
	/// </remarks>
	public JlImage Histo2dim(JlImage imageCol, JlImage imageRow)
	{
		IntPtr proc = JlNativeApi.PreCall(1673);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageCol);
		JlNativeApi.Store(proc, 3, imageRow);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageCol);
		GC.KeepAlive(imageRow);
		return obj;
	}

	/// <summary>
	///   区域灰度直方图（元组量化版）：量化步长以 JlTuple 传入，便于与逐区域参数量化配对。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="quantization">量化步长元组；多区域时与区域逐位对应还是共用首值 [待实测]。Default: 1.0</param>
	/// <returns>各 bin 的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1674，与标量版同一算子；量化值经 <c>Store</c>
	///   钉固定后 <c>UnpinTuple</c>。适合"每个域灰度跨度不同、想自适应 bin 宽"的批量统计。</para>
	///   <para><b>约束或前提</b>多区域输入时直方图如何拼接 [待实测]；bin 起点与
	///   byte/int2/real 图的分箱规则见标量版
	///   <see cref="GrayHistoAbs(JlImage,double)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>全图统一一个步长用标量版（省钉固定）；要区间+
	///   bin 数控制用 <c>GrayHistoRange</c>。</para>
	///   <para><b>参数取向</b>单输出走返回值，INTEGER 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("parts.hobj");
	///   JlRegion part = img.Threshold(80.0, 200.0);
	///   JlTuple histo = part.GrayHistoAbs(img, new JlTuple(4.0));
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；结果元组免释放。</para>
	/// </remarks>
	public JlTuple GrayHistoAbs(JlImage image, JlTuple quantization)
	{
		IntPtr proc = JlNativeApi.PreCall(1674);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 0, quantization);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(quantization);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   区域灰度直方图（绝对计数、可选量化步长）：返回各灰度区间的像素计数。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="quantization">量化步长：多少灰度差并入同一 bin。Default: 1.0</param>
	/// <returns>各 bin 的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1674：与 <c>GrayHisto</c> 的差别是暴露量化步长
	///   且不产相对频数。real/int2 图灰度跨度大时，quantization 放大（如 16.0）可把
	///   元组压到可画的长度；byte 图传 &lt;1 的值会怎样 [待实测]。</para>
	///   <para><b>约束或前提</b>只统计区域覆盖像素；bin 起点（从最小灰度还是从 0 起）
	///   [待实测]。区域为空时返回空/全零元组 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"固定区间+固定 bin 数"（如 0~255 分 256 份）
	///   用 <c>GrayHistoRange</c>；要顺带归一化分布用 <c>GrayHisto</c>。</para>
	///   <para><b>参数取向</b>单一输出走返回值，INTEGER 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("parts.hobj");
	///   JlRegion part = img.Threshold(80.0, 200.0);
	///   JlTuple histo = part.GrayHistoAbs(img, 4.0);   // 每 4 级灰度并一个 bin
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>quantization 用 <c>StoreD</c> 直写；图像与区域
	///   <c>KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public JlTuple GrayHistoAbs(JlImage image, double quantization)
	{
		IntPtr proc = JlNativeApi.PreCall(1674);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreD(proc, 0, quantization);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   区域灰度直方图：一次同时给出绝对频数（返回值，INTEGER）与归一化相对频数（out，DOUBLE）。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="relativeHisto">各灰度级频数除以区域像素数后的相对分布（DOUBLE 元组）。</param>
	/// <returns>各灰度级的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1675：只统计区域覆盖的像素；两条输出同一次调用
	///   装载，省去自己除面积。元组下标对应哪个灰度值（是否从 0 起、real 图的分箱）
	///   [待实测]。</para>
	///   <para><b>约束或前提</b>区域为空时两个元组的形态 [待实测]；byte 图之外类型
	///   （int2/real）分箱方式不同 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要绝对计数用 <c>GrayHistoAbs</c>（少一条输出、
	///   可自选量化步长）；要固定灰度区间+指定 bin 数用 <c>GrayHistoRange</c>；
	///   本算子适合"绝对+相对都要"的常规场合。</para>
	///   <para><b>参数取向</b>绝对值走返回值（INTEGER 装载）、相对值走 out（DOUBLE 装载），
	///   量纲不同别混用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("parts.hobj");
	///   JlRegion part = img.Threshold(80.0, 200.0);
	///   JlTuple absHisto = part.GrayHisto(img, out JlTuple relHisto);
	///   part.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；结果元组免释放。</para>
	/// </remarks>
	public JlTuple GrayHisto(JlImage image, out JlTuple relativeHisto)
	{
		IntPtr proc = JlNativeApi.PreCall(1675);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out relativeHisto);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   区域灰度熵与各向异性（元组版）：逐区域返回熵的 DOUBLE 元组，out 各域的对称性度量。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="anisotropy">各区域灰度分布对称性度量（DOUBLE 元组）。</param>
	/// <returns>各区域灰度熵（DOUBLE 元组，新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1676，与标量重载同一算子；两个输出按 <c>LoadNew</c>
	///   整段装载，与本句柄区域元组逐位对齐——先 <c>Connection()</c> 再比较各域熵时，
	///   域序即结果序。</para>
	///   <para><b>约束或前提</b>熵的口径（底数、分箱）与标量版相同 [待实测]；
	///   单域用 <see cref="EntropyGray(JlImage,out double)"/> 更省。</para>
	///   <para><b>与相邻算子的取舍</b>要整条直方图用 <c>GrayHisto</c>；要按"麻不麻"
	///   筛域就把本元组喂给 <c>TupleSelect</c>/<c>SelectObj</c> 一类组合。</para>
	///   <para><b>参数取向</b>熵走返回值、anisotropy 走 out，均 DOUBLE。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("texture.hobj");
	///   JlRegion tiles = img.Threshold(30.0, 225.0).Connection();
	///   JlTuple entropy = tiles.EntropyGray(img, out JlTuple aniso);
	///   tiles.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组免手动释放；图像与区域 <c>KeepAlive</c> 到
	///   调用结束。</para>
	/// </remarks>
	public JlTuple EntropyGray(JlImage image, out JlTuple anisotropy)
	{
		IntPtr proc = JlNativeApi.PreCall(1676);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out anisotropy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   区域灰度熵与各向异性（标量版）：返回信息熵，out 分布对称性度量；多区域时只读第一个值。
	/// </summary>
	/// <param name="image">参与统计的灰度图像。</param>
	/// <param name="anisotropy">灰度分布对称性度量（计算式 [待实测]）。</param>
	/// <returns>区域内灰度的信息熵（double 标量）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1676：对区域内灰度直方图求熵——平坦均匀趋低、
	///   纹理杂乱趋高，可当"这片区域麻不麻"的标量特征。底数/是否归一化 [待实测]。</para>
	///   <para><b>约束或前提</b><c>LoadD</c> 只读第一值，多区域元组会静默丢其余结果；
	///   直方图的分箱数由区域跨度与内部实现决定 [待实测]，同图不同阈值范围时熵值
	///   会随分箱变化。</para>
	///   <para><b>与相邻算子的取舍</b>要整条分布用 <c>GrayHisto</c>；要"对比度"用
	///   <c>Intensity</c> 的 deviation。需要在不同纹理间做单值排序才用熵。</para>
	///   <para><b>参数取向</b>熵走返回值、anisotropy 走 out，两者均 DOUBLE 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("texture.hobj");
	///   JlRegion tile = new JlRegion(0.0, 0.0, 63.0, 63.0);
	///   double entropy = tile.EntropyGray(img, out double aniso);
	///   tile.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；空区域（0 像素）
	///   时的返回值 [待实测]。</para>
	/// </remarks>
	public double EntropyGray(JlImage image, out double anisotropy)
	{
		IntPtr proc = JlNativeApi.PreCall(1676);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out anisotropy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return doubleValue;
	}




	/// <summary>
	///   区域灰度一阶混合矩与平面拟合（元组版）：5 个 out 各是一条 DOUBLE 元组，逐区域对齐。
	/// </summary>
	/// <param name="image">提供灰度的图像，与区域同坐标系。</param>
	/// <param name="MRow">各区域沿行方向的一阶混合矩。</param>
	/// <param name="MCol">各区域沿列方向的一阶混合矩。</param>
	/// <param name="alpha">各区域拟合平面的行斜率分量。</param>
	/// <param name="beta">各区域拟合平面的列斜率分量。</param>
	/// <param name="mean">各区域平面常数项=平均灰度。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1680，与标量重载同一算子；五个输出都按
	///   <c>LoadNew</c>+DOUBLE 整段装载，元素序 = 本句柄区域元组序，逐域一一对应。</para>
	///   <para><b>约束或前提</b>拟合参考点与斜率符号约定见
	///   <see cref="MomentsGrayPlane(JlImage,out double,out double,out double,out double,out double)"/>
	///   [待实测]；单区域用本重载浪费——直接调标量版。</para>
	///   <para><b>与相邻算子的取舍</b>批量"起伏超阈值剔除"用 <c>PlaneDeviation</c> 一行
	///   比较即可；要逐域背景重建（mean/alpha/beta 回代求平面）才需要本算子全套输出。</para>
	///   <para><b>参数取向</b>void 返回、5 个 out，全部 DOUBLE 元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("wafer.hobj");
	///   JlRegion dies = img.Threshold(60.0, 200.0).Connection();
	///   dies.MomentsGrayPlane(img, out JlTuple mRow, out JlTuple mCol, out JlTuple alpha, out JlTuple beta, out JlTuple mean);
	///   dies.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组免手动释放；图像与区域 <c>KeepAlive</c> 到
	///   调用结束。</para>
	/// </remarks>
	public void MomentsGrayPlane(JlImage image, out JlTuple MRow, out JlTuple MCol, out JlTuple alpha, out JlTuple beta, out JlTuple mean)
	{
		IntPtr proc = JlNativeApi.PreCall(1680);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   区域灰度一阶混合矩与平面拟合（标量版）：5 个 out 给出 MRow/MCol/alpha/beta/mean，多区域时全部只读第一个值。
	/// </summary>
	/// <param name="image">提供灰度的图像，与区域同坐标系。</param>
	/// <param name="MRow">沿行方向的一阶混合矩（灰度加权）。</param>
	/// <param name="MCol">沿列方向的一阶混合矩（灰度加权）。</param>
	/// <param name="alpha">拟合平面沿行方向的斜率分量。</param>
	/// <param name="beta">拟合平面沿列方向的斜率分量。</param>
	/// <param name="mean">拟合平面常数项=区域内平均灰度。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1680：对区域灰度做最小二乘平面
	///   g ≈ mean + alpha·(Δrow) + beta·(Δcol) 拟合 [待实测:参考点取质心还是外接框角]，
	///   并给出两个一阶混合矩。斜率符号与图像 y 轴向下的约定耦合，换算物理倾角前先定号。</para>
	///   <para><b>约束或前提</b>无返回值，一切结果走 out；每个 <c>LoadD</c> 只读第一值，
	///   多区域元组请改用元组重载，否则其余区域的平面参数被静默丢弃。</para>
	///   <para><b>与相邻算子的取舍</b>只要"偏离平面多少"这一个数用 <c>PlaneDeviation</c>；
	///   要平面本身（测倾角、做背景扣除参数）才用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("wafer.hobj");
	///   JlRegion die = img.Threshold(60.0, 200.0);
	///   die.MomentsGrayPlane(img, out double mRow, out double mCol, out double alpha, out double beta, out double mean);
	///   die.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；斜率单位是
	///   "灰度 / 像素"。</para>
	/// </remarks>
	public void MomentsGrayPlane(JlImage image, out double MRow, out double MCol, out double alpha, out double beta, out double mean)
	{
		IntPtr proc = JlNativeApi.PreCall(1680);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   区域灰度对拟合平面的偏离度：剔除斜面背景后的"起伏量"，逐区域返回 DOUBLE 元组。
	/// </summary>
	/// <param name="image">待测灰度图像，与区域同坐标系。</param>
	/// <returns>每区域一个偏离度值（DOUBLE 元组；是否为标准差口径 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1681：先在区域内把灰度近似成一个斜平面
	///   （即 <c>MomentsGrayPlane</c> 的 alpha/beta/mean），再给出现实灰度对该平面的
	///   总偏离。光照渐变/镀层斜坡背景下，它比均值或直方图宽度更稳，专门用于
	///   "背景有斜度、缺陷看局部起伏"的检测。</para>
	///   <para><b>约束或前提</b>区域太小时平面拟合自由度不足，数值意义变弱 [待实测]；
	///   多区域元组时逐域出值，顺序即元组元素序。</para>
	///   <para><b>与相邻算子的取舍</b>要平面的三个系数本身（做倾角测量）用
	///   <c>MomentsGrayPlane</c>；只要均匀背景下的对比度用 <c>Intensity</c> 的
	///   deviation。背景是斜面才轮到本算子。</para>
	///   <para><b>参数取向</b>唯一输出走返回值，无 out。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("wafer.hobj");
	///   JlRegion dies = img.Threshold(60.0, 200.0).Connection();
	///   JlTuple dev = dies.PlaneDeviation(img);
	///   dies.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域 <c>KeepAlive</c> 到调用结束；结果元组
	///   不实现 IDisposable。</para>
	/// </remarks>
	public JlTuple PlaneDeviation(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(1681);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   灰度加权椭圆轴（元组版）：逐区域返回长半轴 ra，out 各区域的短半轴 rb 与主轴倾角 phi。
	/// </summary>
	/// <param name="image">提供权重的灰度图像。</param>
	/// <param name="rb">各区域短半轴长（DOUBLE 元组，像素）。</param>
	/// <param name="phi">各区域主轴与 x 轴夹角（DOUBLE 元组，弧度制 [待实测]）。</param>
	/// <returns>各区域长半轴长 ra（DOUBLE 元组，新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1682：灰度作权重的二阶矩椭圆分解，输出按
	///   <c>LoadNew</c> 整段装载，与本句柄区域元组逐位对齐；上游 <c>Connection()</c>
	///   顺序不稳则对齐静默错位。</para>
	///   <para><b>约束或前提</b>单区域时三个元组各只有 1 个元素；全黑区域权重趋零，
	///   轴/角数值不稳定 [待实测]。图像与区域须同坐标系。</para>
	///   <para><b>与相邻算子的取舍</b>只测一个域用标量重载
	///   <see cref="EllipticAxisGray(JlImage,out double,out double)"/>，不建元组；
	///   忽略亮度用 <c>EllipticAxis</c>。</para>
	///   <para><b>参数取向</b>ra 返回值、rb/phi 走 out，三者在原生侧是独立输出通道。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("solder.hobj");
	///   JlRegion spots = img.Threshold(120.0, 255.0).Connection();
	///   JlTuple ra = spots.EllipticAxisGray(img, out JlTuple rb, out JlTuple phi);
	///   spots.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果 <c>JlTuple</c> 无需释放；图像句柄 <c>KeepAlive</c>
	///   到调用结束，返回前不要 Dispose。</para>
	/// </remarks>
	public JlTuple EllipticAxisGray(JlImage image, out JlTuple rb, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1682);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rb);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   灰度加权椭圆轴（标量版）：返回长半轴 ra，out 短半轴 rb 与主轴倾角 phi；多区域时只读第一个值。
	/// </summary>
	/// <param name="image">提供权重的灰度图像。</param>
	/// <param name="rb">短半轴长（像素）。</param>
	/// <param name="phi">主轴与 x 轴夹角（弧度制 [待实测]，正方向与值域未在托管层注明）。</param>
	/// <returns>长半轴长 ra（像素，double 标量）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1682：用区域内像素灰度作权重求二阶矩，再分解出
	///   等效椭圆的轴长与朝向——亮的一侧把"重心椭圆"拉向自己。</para>
	///   <para><b>约束或前提</b><c>LoadD</c> 只取第一个结果，本句柄为多区域元组时其余
	///   区域被静默丢弃；区域整体偏黑（权重和趋零）时轴与角不稳定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要形状朝向用几何版 <c>EllipticAxis</c>
	///   （不看灰度，二值轮廓即定轴向）；灰度版适合条纹焊点这类亮度主轴明显、
	///   黑白轮廓却对称的目标。</para>
	///   <para><b>参数取向</b>返回 ra、out rb/phi；与元组版同一 id，元组版逐域出结果。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("solder.hobj");
	///   JlRegion spot = img.Threshold(120.0, 255.0);
	///   double ra = spot.EllipticAxisGray(img, out double rb, out double phi);
	///   spot.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域句柄 <c>KeepAlive</c> 到调用结束；
	///   轴长单位是像素，与 <c>AreaCenter</c> 同坐标系。</para>
	/// </remarks>
	public double EllipticAxisGray(JlImage image, out double rb, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1682);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out rb);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return doubleValue;
	}

	/// <summary>
	///   在灰度图上求区域的灰度质心与"体积"：返回值是像素灰度之和，不是面积。
	/// </summary>
	/// <param name="image">作为权重来源的灰度图像。</param>
	/// <param name="row">输出：灰度质心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">输出：灰度质心列坐标（DOUBLE 元组）。</param>
	/// <returns>各区域的灰度体积（DOUBLE 元组）。</returns>
	/// <remarks>
	///   <para><b>与 <c>AreaCenter</c> 的取舍</b>几何版对像素一视同仁；亮斑的灰度质心
	///   明显偏向亮侧——定位发光目标要加权就用本算子，只要覆盖范围用 1650。</para>
	///   <para><b>约束</b><c>image</c> 与本句柄区域必须同坐标系，区域超出图像部分的权重
	///   取法 [待实测]；区域内灰度接近全黑时质心数值不稳定（分母趋零）[待实测]。</para>
	///   <para><b>参数取向</b>返回体积 + out 行、列；行先列后，与 1650 布局相同。
	///   原生 id 1683。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage image = new JlImage("solder.hobj");
	///   JlRegion spots = image.Threshold(150.0, 255.0).Connection();
	///   JlTuple volume = spots.AreaCenterGray(image, out JlTuple row, out JlTuple column);
	///   spots.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>image 句柄与区域同样 <c>KeepAlive</c> 到调用结束；
	///   返回值为新元组，不产生区域句柄。</para>
	/// </remarks>
	public JlTuple AreaCenterGray(JlImage image, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1683);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   灰度质心与体积（标量版）。
	/// </summary>
	/// <param name="image">作为权重来源的灰度图像。</param>
	/// <param name="row">输出：灰度质心行坐标。</param>
	/// <param name="column">输出：灰度质心列坐标。</param>
	/// <returns>灰度体积（double 标量）。</returns>
	/// <remarks>
	///   <para>加权语义与坐标系约束见
	///   <see cref="AreaCenterGray(JlImage,out JlTuple,out JlTuple)"/>。两个重载同一原生算子
	///   （id 1683）：本重载 <c>LoadD</c>×3 直取标量、不建 <c>JlTuple</c>；单区域用它。</para>
	/// </remarks>
	public double AreaCenterGray(JlImage image, out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1683);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row);
		err = JlNativeApi.LoadD(proc, 2, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return doubleValue;
	}

	/// <summary>
	///   灰度投影：把区域内像素灰度按行/列累加，返回水平投影，out 垂直投影。
	/// </summary>
	/// <param name="image">灰度来源图像，区域在其上取灰度值。</param>
	/// <param name="mode">投影统计方式，字符串透传原生层；托管层未枚举取值 ["simple" 之外的可选值待实测]。Default: "simple"</param>
	/// <param name="vertProjection">Vertical gray value projection (per column).</param>
	/// <returns>水平投影（按行）DOUBLE 元组，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1684：对区域覆盖的灰度图求两个方向的投影曲线，
	///   常用于找条码边界/栅格周期定位。区域提供"算哪些像素"，图像提供"值多大"——
	///   与纯几何投影（按像素个数计）不同，亮区会拉高曲线。</para>
	///   <para><b>约束或前提</b><c>image</c> 与本区域必须同坐标系同尺寸，区域越出图像
	///   部分的计入方式 [待实测]。投影向量元素与绝对行/列号的对齐规则（从 0 还是从
	///   外接框起算）[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>本库没有纯几何（按像素计数）投影包装，想排除
	///   亮度影响可对同区域先 <c>ReduceDomain</c> 后自行计数；要逐域标量统计用
	///   <c>GrayFeatures</c>。本算子一次给出两条完整曲线。</para>
	///   <para><b>参数取向</b>水平投影走返回值、垂直走 out；均 DOUBLE <c>JlTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 128, 128);
	///   JlRegion band = new JlRegion(10.0, 0.0, 118.0, 127.0);
	///   JlTuple horiz = band.GrayProjections(img, "simple", out JlTuple vert);
	///   band.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>图像与区域句柄都 <c>KeepAlive</c> 到调用结束；
	///   mode 拼写错误是否报错还是静默回退默认 [待实测]。</para>
	/// </remarks>
	public JlTuple GrayProjections(JlImage image, string mode, out JlTuple vertProjection)
	{
		IntPtr proc = JlNativeApi.PreCall(1684);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out vertProjection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}











	/// <summary>
	///   外接正矩形三量（元组版）：对本句柄的每个区域各求高/宽/长宽比，逐域返回。
	/// </summary>
	/// <param name="width">各区域外接矩形宽度（INTEGER 元组，像素计数）。</param>
	/// <param name="ratio">各区域长宽比（DOUBLE 元组）；分子分母次序 [待实测]。</param>
	/// <returns>各区域外接矩形高度（INTEGER 元组，新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2001，与标量重载同一算子；区别在于三个输出都按
	///   <c>LoadNew</c> 整段装载，区域数 : 结果数 = 1 : 1 逐域对齐，顺序即本句柄元组的
	///   元素序——上游 <c>Connection()</c> 顺序不稳定时这里会跟着错位。</para>
	///   <para><b>与相邻算子的取舍</b>只测一个区域用
	///   <see cref="HeightWidthRatio(out int, out double)"/>，省去建元组；要角点坐标用
	///   <c>SmallestRectangle1</c>。</para>
	///   <para><b>参数取向</b>高走返回值、宽与比走 out；返回的 INTEGER、out 的 ratio
	///   为 DOUBLE——比例信息只在浮点侧，整型侧无精度损失问题。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 128, 128);
	///   JlRegion parts = img.Threshold(128.0, 255.0).Connection();
	///   JlTuple height = parts.HeightWidthRatio(out JlTuple width, out JlTuple ratio);
	///   parts.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果均为 <c>JlTuple</c>（不实现 IDisposable，无需手动释放）；
	///   边界像素是否 +1 计入 [待实测]。</para>
	/// </remarks>
	public JlTuple HeightWidthRatio(out JlTuple width, out JlTuple ratio)
	{
		IntPtr proc = JlNativeApi.PreCall(2001);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out ratio);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   外接正矩形三量（标量版）：返回高，out 宽与长宽比；多区域时只读第一个值。
	/// </summary>
	/// <param name="width">外接矩形宽度（整型像素数）。</param>
	/// <param name="ratio">长宽比（DOUBLE）；分子分母次序 [待实测]。</param>
	/// <returns>外接矩形高度（整型像素数）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2001：对坐标轴平行的最小包围矩形求高、宽、比。
	///   高与宽按 <c>LoadI</c> 以整数装载（像素计数），比按 <c>LoadD</c> 为浮点。</para>
	///   <para><b>约束或前提</b><c>LoadI</c> 只取第一个结果——本句柄若持有多区域元组，
	///   其余区域的外接框被静默丢弃；逐域求框请改用元组重载
	///   <see cref="HeightWidthRatio(out JlTuple, out JlTuple)"/> 或先拆开再循环。</para>
	///   <para><b>与相邻算子的取舍</b>要四个角点坐标用 <c>SmallestRectangle1</c> 一类
	///   算子；要旋转矩形的长短轴用 <c>smallest_rectangle2</c> 族；只要"够不够方"的
	///   过滤条件时用本算子的 ratio 最直接。</para>
	///   <para><b>参数取向</b>返回值=高，out 顺序先宽后比，与元组版一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 20.0, 60.0, 90.0);
	///   int height = roi.HeightWidthRatio(out int width, out double ratio);
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>边界像素是否 +1 计入（如高 = row2−row1 还是 +1）
	///   [待实测]；退化单像素区域时 ratio 的分母处理 [待实测]。</para>
	/// </remarks>
	public int HeightWidthRatio(out int width, out double ratio)
	{
		IntPtr proc = JlNativeApi.PreCall(2001);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadI(proc, 1, err, out width);
		err = JlNativeApi.LoadD(proc, 2, err, out ratio);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   对象元组插入：把 objectsInsert 的元素插入本区域元组的 index 位置，返回加长后的新元组。
	/// </summary>
	/// <param name="objectsInsert">要插入的区域（可为多元素元组，整体并入）。</param>
	/// <param name="index">插入位置（插到该位置元素之前）；索引基数及"末尾追加"的写法未在托管层枚举 [待实测]。</param>
	/// <returns>插入后的新区域对象元组句柄；两个输入均不被原地修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2003。结果长度 = 原长度 + objectsInsert 元素数，
	///   原有其余元素顺序保持。</para>
	///   <para><b>约束或前提</b>index 传错位置不会报错、只会静默改变元组排序——凡下游
	///   依赖序号（如按 <c>Connection()</c> 顺序取域）都要重新核对。</para>
	///   <para><b>与相邻算子的取舍</b>只在尾部拼接用 <see cref="ConcatObj(JlRegion)"/>
	///   更直白；要"覆盖某位"用 <c>ReplaceObj</c>；插入才需要本算子。</para>
	///   <para><b>参数取向</b>objectsInsert 经 <c>Store</c>+<c>KeepAlive</c>，index 用
	///   <c>StoreI</c> 直写；结果为新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion a = new JlRegion(0.0, 0.0, 20.0, 20.0);
	///   JlRegion b = new JlRegion(30.0, 30.0, 50.0, 50.0);
	///   using JlRegion both = a.InsertObj(b, 1);
	///   int n = both.CountObj();
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；插入后原变量与结果共享元素对象与否
	///   [待实测]，Dispose 输入前确认结果仍可用。</para>
	/// </remarks>
	public JlRegion InsertObj(JlRegion objectsInsert, int index)
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
	///   对象元组批量删除：按索引元组所列位置一次性移除各元素，返回剩余元素的新元组。
	/// </summary>
	/// <param name="index">要移除的各元素位置索引（整型元组）。</param>
	/// <returns>删除后的新区域对象元组句柄；本句柄不被原地修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2005，与标量重载同一算子；index 钉固定后调用、
	///   随即 <c>UnpinTuple</c>。一次删多位置比循环调标量版少进原生层多次。</para>
	///   <para><b>约束或前提</b>index 内是否有顺序要求、重复索引如何处理、越界索引是
	///   报错还是忽略 [待实测]；剩余元素整体前移，下游按序号取域的逻辑要同步更新。</para>
	///   <para><b>与相邻算子的取舍</b>反过来"只留这几个"用 <c>SelectObj</c> 或下标
	///   访问 <c>this[index]</c>；删单个位置用 <see cref="RemoveObj(int)"/> 省钉固定。</para>
	///   <para><b>参数取向</b>返回 <c>LoadNew</c> 新句柄，需释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 128, 128);
	///   JlRegion all = img.Threshold(128.0, 255.0).Connection();
	///   using JlRegion rest = all.RemoveObj(new JlTuple(0, 2));   // 删掉第 0、2 位 [待实测:索引基数]
	///   all.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>删完后原元组 <c>all</c> 仍是独立句柄，记得各自 Dispose；
	///   区域句柄 <c>KeepAlive</c> 到调用结束前不得释放。</para>
	/// </remarks>
	public new JlRegion RemoveObj(JlTuple index)
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
	///   对象元组删除（标量索引版）：从本区域元组移除第 index 个元素，返回剩余元素组成的新元组。
	/// </summary>
	/// <param name="index">要移除的元素位置；索引基数与越界行为未在托管层枚举 [待实测]。</param>
	/// <returns>删除后的新区域对象元组句柄；本句柄不被原地修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2005，结果长度 = 原长度 − 1（索引有效时）。
	///   索引经 <c>StoreI</c> 直写，无钉固定。</para>
	///   <para><b>约束或前提</b>删除会使其后所有元素的序号整体前移——依赖
	///   <c>Connection()</c> 输出顺序的下游筛选要在删除后重新对号，否则会静默错位。</para>
	///   <para><b>与相邻算子的取舍</b>删多个位置用 <see cref="RemoveObj(JlTuple)"/> 一次
	///   完成；只取某一位不必删，直接下标访问即可。</para>
	///   <para><b>参数取向</b>返回 <c>LoadNew</c> 新句柄，需释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 128, 128);
	///   JlRegion all = img.Threshold(128.0, 255.0).Connection();
	///   using JlRegion rest = all.RemoveObj(0);            // 去掉第一个连通域
	///   all.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>被移除元素与剩余元素共享底层对象与否 [待实测]；
	///   区域句柄 <c>KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public new JlRegion RemoveObj(int index)
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
	///   对象元组替换（索引元组版）：把本区域元组中 index 所列各位置换成 objectsReplace 的元素，返回新元组。
	/// </summary>
	/// <param name="objectsReplace">提供替换元素的区域元组。</param>
	/// <param name="index">被替换位置的索引元组，与 objectsReplace 元素逐位对应。</param>
	/// <returns>替换后的新区域对象元组句柄；两个输入都不被原地修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2006。一次调用可覆盖多个位置：index 第 k 个位置
	///   换成 objectsReplace 第 k 个元素（对应规则 [待实测]，用 <c>CountObj()</c> 核对长度）。</para>
	///   <para><b>约束或前提</b>index 与 objectsReplace 都先钉固定、调用后
	///   <c>UnpinTuple(index)</c>；objectsReplace 全程 <c>KeepAlive</c>。</para>
	///   <para><b>与相邻算子的取舍</b>单个位置用标量索引重载
	///   <see cref="ReplaceObj(JlRegion,int)"/>，免去钉固定开销；整段重排则考虑用
	///   <c>ConcatObj</c> 一类重建容器，而非逐位替换。</para>
	///   <para><b>参数取向</b>结果为 <c>LoadNew</c> 的新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion parts = new JlRegion(0.0, 0.0, 20.0, 20.0);
	///   JlRegion patch = new JlRegion(30.0, 30.0, 50.0, 50.0);
	///   using JlRegion swapped = parts.ReplaceObj(patch, new JlTuple(0));
	///   parts.Dispose();
	///   patch.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；索引基数（0/1 基）与越界位置行为
	///   [待实测]，错位会静默替换错对象。</para>
	/// </remarks>
	public JlRegion ReplaceObj(JlRegion objectsReplace, JlTuple index)
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
	///   对象元组替换（标量索引版）：把本区域元组第 index 个元素换成 objectsReplace，返回新元组。
	/// </summary>
	/// <param name="objectsReplace">用于替换的区域（本身也可为多元素元组，展开后并入）。</param>
	/// <param name="index">被替换元素的位置索引；0 基还是 1 基、越界行为未在托管层枚举 [待实测]。</param>
	/// <returns>替换后的新区域对象元组句柄；本句柄与 objectsReplace 均不被原地修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2006，与 <c>JlTuple</c> 索引重载同一算子；本重载
	///   索引经 <c>StoreI</c> 直写，少一次钉固定。</para>
	///   <para><b>约束或前提</b>替换不改元素总数以外的结构语义：只换一位时结果长度
	///   不变（objectsReplace 为单区域时）。索引基数的核对办法：先用
	///   <c>CountObj()</c> 数长度，再替换后对比。</para>
	///   <para><b>与相邻算子的取舍</b>要"插入并拉长"用 <c>InsertObj</c>；要"删掉某位"
	///   用 <c>RemoveObj</c>；本算子保持长度只做覆盖。</para>
	///   <para><b>参数取向</b>返回新句柄；objectsReplace 被 <c>KeepAlive</c>，调用返回前
	///   不可 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion parts = new JlRegion(0.0, 0.0, 20.0, 20.0);
	///   JlRegion patch = new JlRegion(30.0, 30.0, 50.0, 50.0);
	///   using JlRegion swapped = parts.ReplaceObj(patch, 0);
	///   parts.Dispose();
	///   patch.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄需释放；输入变量可在调用返回后各自 Dispose，
	///   已装入结果元组的对象不受影响 [待实测]。</para>
	/// </remarks>
	public JlRegion ReplaceObj(JlRegion objectsReplace, int index)
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
	///   用本句柄区域裁剪 XLD 轮廓，返回落在区域内的轮廓段。
	/// </summary>
	/// <param name="contour">输入轮廓（第二个 iconic 句柄）。</param>
	/// <param name="mode">求交模式（字符串透传给原生层）。Default: "lines"</param>
	/// <returns>裁剪后轮廓的新句柄（<c>JlXLDCont</c>）；区域与轮廓均不被修改。</returns>
	/// <remarks>
	///   <para><b>与 <c>Intersection</c> 的分界</b>那边是区域∩区域；这里输出的是轮廓对象，
	///   参与方一侧必须是 <c>JlXLDCont</c>。mode 可选字面量托管层未枚举 [待实测]。</para>
	///   <para><b>参数取向</b>原生 id 2183；结果经 <c>JlXLDCont.LoadNew</c> 装载为新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 10.0, 100.0, 100.0);
	///   JlXLDCont line = new JlXLDCont(new JlTuple(5.0, 150.0), new JlTuple(5.0, 150.0));
	///   using JlXLDCont kept = roi.IntersectionRegionContourXld(line, "lines");
	///   roi.Dispose();
	///   line.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>contour</c> 侧 <c>Store</c>+<c>KeepAlive</c>，调用返回前
	///   不得 Dispose。</para>
	/// </remarks>
	public JlXLDCont IntersectionRegionContourXld(JlXLDCont contour, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(2183);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
		return obj;
	}

	/// <summary>
	///   批量内点测试：row/column 逐元素配成点集，逐点返回 1/0 的 INTEGER 元组。
	/// </summary>
	/// <param name="row">各被测点的行坐标元组（向下为正）。Default: 100</param>
	/// <param name="column">各被测点的列坐标元组（向右为正），与 row 逐元素配对。Default: 100</param>
	/// <returns>新 <c>JlTuple</c>，每点一个 0/1 整型值；两输入长度不等时的配对规则 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>一次原生调用测完整个点集（id 2192），比逐点调标量重载
	///   省掉每次进出原生层的开销；判定在行程编码上进行，不产生中间区域句柄。</para>
	///   <para><b>约束或前提</b>坐标须与本区域同一像素坐标系；小数坐标按何种规则取整
	///   [待实测]，建议调用方先自行取整。本句柄为多区域元组时输出与区域×点的展开
	///   关系 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想问"某一个点在不在内"用
	///   <see cref="TestRegionPoints(int,int)"/>（无钉固定开销）；要"点集落在哪些连通域内"
	///   则先 <c>Connection()</c> 再逐域查询。</para>
	///   <para><b>参数取向</b>row/column 经 <c>Store</c> 钉固定位、调用后 <c>UnpinTuple</c>；
	///   结果按 INTEGER 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 20.0, 60.0, 90.0);
	///   JlTuple inside = roi.TestRegionPoints(new JlTuple(30.0, 5.0), new JlTuple(50.0, 5.0));
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>JlTuple</c> 不实现 IDisposable，结果无需手动释放；
	///   区域句柄 <c>GC.KeepAlive</c> 到调用结束。</para>
	/// </remarks>
	public JlTuple TestRegionPoints(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(2192);
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
	///   单点内点测试：点 (row, column) 落在本区域内返回 1，否则返回 0。
	/// </summary>
	/// <param name="row">被测点的行坐标（像素行，向下为正）。Default: 100</param>
	/// <param name="column">被测点的列坐标（像素列，向右为正）。Default: 100</param>
	/// <returns>0/1 整型标志（不是 bool）；空区域恒返回 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>直接在区域的行程编码上判 membership，不生成任何句柄、
	///   不做栅格化。原生 id 2192。</para>
	///   <para><b>约束或前提</b>坐标是图像绝对像素编号，与区域同坐标系；本重载只测
	///   一个点，且 <c>LoadI</c> 只读第一个结果值——若本句柄其实持有多区域对象元组，
	///   测试针对哪一路、其余区域结果如何处置 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>批量点查询用
	///   <see cref="TestRegionPoints(JlTuple,JlTuple)"/>，一次原生调用完成全部点，别逐点
	///   循环调本重载；单矩形 ROI 判内外直接比较行列数值更快，不必进原生层。</para>
	///   <para><b>参数取向</b>返回值即结果，row/column 经 <c>StoreI</c> 直写控制参数，
	///   无元组钉固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlRegion roi = new JlRegion(10.0, 20.0, 60.0, 90.0);
	///   int inside = roi.TestRegionPoints(30, 50);
	///   roi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>区域句柄被 <c>GC.KeepAlive</c> 到原生调用结束，返回后再
	///   Dispose 输入。</para>
	/// </remarks>
	public int TestRegionPoints(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(2192);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}
}
