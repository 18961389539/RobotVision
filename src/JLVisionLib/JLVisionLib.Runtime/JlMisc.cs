using System;

namespace JLVisionLib;

/// <summary>Class grouping methods belonging to no other Vision class.</summary>
public class JlMisc
{
	/// <summary>把任意类型的元组写入文件（库原生元组格式）。</summary>
	/// <param name="tuple">承载任意数值/字符串数据的元组，整组钉住传给原生侧。</param>
	/// <param name="fileName">目标文件完整路径（字符串直写 StoreS）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把任意类型元组（数值/字符串/句柄）落盘为文件（具体编码由原生侧决定），对应原生算子 id 218。元组以引用方式钉住传入（Store 后 UnpinTuple），文件名走字符串参数（StoreS），调用结束前不得改写该元组。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面（把序列化辅助、几何、卡尔曼等无归属工具混在一起的静态类）；本库内部对 JlMisc 没有任何调用者，其产物文件也不被任何内部算子回读。与 <see cref="ReadTuple"/> 配成一对，本库仅此一对元组文件读写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple data = new double[] { 1.0, 2.0, 3.0 };
	///   JlMisc.WriteTuple(data, "data.tup");
	///   </code>
	///   <para><b>资源与坑</b>写入是整文件覆盖（非追加），路径不可写或目录不存在时抛原生错误；<c>fileName</c> 的编码/换行由原生侧决定，跨平台读回建议始终用 <see cref="ReadTuple"/>。</para>
	/// </remarks>
	public static void WriteTuple(JlTuple tuple, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(218);
		JlNativeApi.Store(proc, 0, tuple);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tuple);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>从文件读取一个元组并返回新元组（库原生元组格式）。</summary>
	/// <param name="fileName">要读取的目标文件完整路径。</param>
	/// <returns>文件内容装载出的新元组，可承载任意数值/字符串数据。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>读取原生元组格式的文件并装载为托管元组返回，对应原生算子 id 219；本库仅此一对元组文件读写（与 <see cref="WriteTuple"/> 配对）。</para>
	///   <para><b>资源与坑</b>文件路径不可读或不存在时抛原生错误；文件的编码/换行由原生侧决定，跨平台写入建议始终用 <see cref="WriteTuple"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple data = JlMisc.ReadTuple("data.tup");
	///   </code>
	/// </remarks>
	public static JlTuple ReadTuple(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(219);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}







	/// <summary>把 3D 点的球坐标（经度/纬度/半径）换算为直角坐标 x/y/z。</summary>
	/// <param name="longitude">经度元组，逐点配对参与换算。单位约定见 remarks [待实测]</param>
	/// <param name="latitude">纬度元组，与 longitude 等长。</param>
	/// <param name="radius">半径（距离球心的距离，单位与输出坐标一致）元组。</param>
	/// <param name="equatPlaneNormal">赤道面法向量（指向北极），轴名带符号串。Default: "-y"</param>
	/// <param name="zeroMeridian">赤道面内指向零子午线的坐标轴，轴名带符号串。Default: "-z"</param>
	/// <param name="x">换算后 x 坐标（DOUBLE 装载）。</param>
	/// <param name="y">换算后 y 坐标（DOUBLE 装载）。</param>
	/// <param name="z">换算后 z 坐标（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>按"赤道面法向 + 零子午线轴向"两个字符串约定，把球坐标（经度、纬度、半径）换算成直角坐标 x/y/z，对应原生算子 id 996。三个坐标参数按 DOUBLE 装载（LoadNew + JlTupleType.DOUBLE），即输出恒为浮点，即便输入是整型元组。输入元组钉住传入、调用后解钉。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面（3D 计算与 2D 几何、卡尔曼等混在一起），本库内部对它没有任何调用者。本库已不提供 3D 类型族（<c>JlHomMat3D</c> 等已删除），但本算子只做纯数值元组换算，不依赖那些类型。经度/纬度用弧度还是角度、正方向约定无法由代码判定 [待实测]；<c>equatPlaneNormal</c>/<c>zeroMeridian</c> 是形如 <c>"-y"</c>/<c>"-z"</c> 的轴名带符号串，取值合法性由原生侧校验。</para>
	///   <para><b>与相邻算子的取舍</b>反向换算用 <see cref="ConvertPoint3dCartToSpher(JlTuple,JlTuple,JlTuple,string,string,out JlTuple,out JlTuple)"/>；需要的是刚体变换而非坐标换算是位姿类接口的职责，不在本门面内。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple longitude = new double[] { 0.0, 90.0 };
	///   JlTuple latitude = new double[] { 30.0, 30.0 };
	///   JlTuple radius = new double[] { 1.0, 1.0 };
	///   JlMisc.ConvertPoint3dSpherToCart(longitude, latitude, radius, "-y", "-z", out JlTuple x, out JlTuple y, out JlTuple z);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 不实现 IDisposable，x/y/z 无需释放；radius 为负时的行为 [待实测]。</para>
	/// </remarks>
	public static void ConvertPoint3dSpherToCart(JlTuple longitude, JlTuple latitude, JlTuple radius, string equatPlaneNormal, string zeroMeridian, out JlTuple x, out JlTuple y, out JlTuple z)
	{
		IntPtr proc = JlNativeApi.PreCall(996);
		JlNativeApi.Store(proc, 0, longitude);
		JlNativeApi.Store(proc, 1, latitude);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.StoreS(proc, 3, equatPlaneNormal);
		JlNativeApi.StoreS(proc, 4, zeroMeridian);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(longitude);
		JlNativeApi.UnpinTuple(latitude);
		JlNativeApi.UnpinTuple(radius);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out x);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out y);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out z);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把 3D 点的球坐标（经度/纬度/半径）换算为直角坐标 x/y/z（标量版）。</summary>
	/// <param name="longitude">经度标量，单位约定见 remarks [待实测]。Default: 0</param>
	/// <param name="latitude">纬度标量。</param>
	/// <param name="radius">半径（距离球心的距离，单位与输出坐标一致）。Default: 0</param>
	/// <param name="equatPlaneNormal">赤道面法向量（指向北极），轴名带符号串。Default: "-y"</param>
	/// <param name="zeroMeridian">赤道面内指向零子午线的坐标轴，轴名带符号串。Default: "-z"</param>
	/// <param name="x">换算后 x 坐标。</param>
	/// <param name="y">换算后 y 坐标。</param>
	/// <param name="z">换算后 z 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b>按"赤道面法向 + 零子午线轴向"两个字符串约定，把单个点的球坐标（经度、纬度、半径）换算成直角坐标 x/y/z，对应原生算子 id 996（与本类中的元组版 <see cref="ConvertPoint3dSpherToCart(JlTuple,JlTuple,JlTuple,string,string,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）。本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>经度/纬度用弧度还是角度、正方向约定无法由代码判定 [待实测]；半径取负的行为 [待实测]。</para>
	/// </remarks>
	public static void ConvertPoint3dSpherToCart(double longitude, double latitude, double radius, string equatPlaneNormal, string zeroMeridian, out double x, out double y, out double z)
	{
		IntPtr proc = JlNativeApi.PreCall(996);
		JlNativeApi.StoreD(proc, 0, longitude);
		JlNativeApi.StoreD(proc, 1, latitude);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.StoreS(proc, 3, equatPlaneNormal);
		JlNativeApi.StoreS(proc, 4, zeroMeridian);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out x);
		err = JlNativeApi.LoadD(proc, 1, err, out y);
		err = JlNativeApi.LoadD(proc, 2, err, out z);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把 3D 点直角坐标 x/y/z 换算为球坐标；经度走返回值，纬度与半径走 out。</summary>
	/// <param name="x">x 坐标元组。</param>
	/// <param name="y">y 坐标元组，与 x 等长。</param>
	/// <param name="z">z 坐标元组，与 x 等长。</param>
	/// <param name="equatPlaneNormal">赤道面法向量（指向北极），须与正向换算同值。Default: "-y"</param>
	/// <param name="zeroMeridian">零子午线对应的坐标轴，须与正向换算同值。Default: "-z"</param>
	/// <param name="latitude">纬度（DOUBLE 装载）。</param>
	/// <param name="radius">半径（DOUBLE 装载，与输入坐标同单位）。</param>
	/// <returns>经度（DOUBLE 装载的新元组）；单位约定 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把直角坐标 x/y/z 换算回球坐标，对应原生算子 id 997。注意出参分配不对称：经度走<b>返回值</b>，纬度与半径走 out 参数，三者都按 DOUBLE 装载。输入元组钉住传入、调用后解钉。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面，本库内部没有任何调用者；本库已不提供 3D 类型族（<c>JlHomMat3D</c> 等已删除），本算子为纯数值元组换算不受影响。经纬度的弧度/角度约定与反变换一致，无法由代码判定 [待实测]。<c>equatPlaneNormal</c>/<c>zeroMeridian</c> 须与当初正向换算所用相同，否则得到另一套球坐标。</para>
	///   <para><b>与相邻算子的取舍</b>正向换算用 <see cref="ConvertPoint3dSpherToCart(JlTuple,JlTuple,JlTuple,string,string,out JlTuple,out JlTuple,out JlTuple)"/>；原点处的点（x=y=z=0）半径为 0、经纬度不定 [待实测]。</para>
	///   <para><b>参数取向</b>返回值=经度，out=纬度、半径，与签名一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple x = new double[] { 1.0, 0.0 };
	///   JlTuple y = new double[] { 0.0, 0.0 };
	///   JlTuple z = new double[] { 0.0, 1.0 };
	///   JlTuple longitude = JlMisc.ConvertPoint3dCartToSpher(x, y, z, "-y", "-z", out JlTuple latitude, out JlTuple radius);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放；忘记接收返回值就丢失经度是本接口签名带来的典型误用。</para>
	/// </remarks>
	public static JlTuple ConvertPoint3dCartToSpher(JlTuple x, JlTuple y, JlTuple z, string equatPlaneNormal, string zeroMeridian, out JlTuple latitude, out JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(997);
		JlNativeApi.Store(proc, 0, x);
		JlNativeApi.Store(proc, 1, y);
		JlNativeApi.Store(proc, 2, z);
		JlNativeApi.StoreS(proc, 3, equatPlaneNormal);
		JlNativeApi.StoreS(proc, 4, zeroMeridian);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(x);
		JlNativeApi.UnpinTuple(y);
		JlNativeApi.UnpinTuple(z);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out latitude);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out radius);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>把 3D 点直角坐标 x/y/z 换算为球坐标；经度走返回值，纬度与半径走 out（标量版）。</summary>
	/// <param name="x">x 坐标。</param>
	/// <param name="y">y 坐标。</param>
	/// <param name="z">z 坐标。</param>
	/// <param name="equatPlaneNormal">赤道面法向量（指向北极），须与正向换算同值。Default: "-y"</param>
	/// <param name="zeroMeridian">零子午线对应的坐标轴，须与正向换算同值。Default: "-z"</param>
	/// <param name="latitude">纬度（标量装载）。</param>
	/// <param name="radius">半径（标量装载，与输入坐标同单位）。</param>
	/// <returns>经度；单位约定 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把单个点的直角坐标换算回球坐标，对应原生算子 id 997（与本类元组版 <see cref="ConvertPoint3dCartToSpher(JlTuple,JlTuple,JlTuple,string,string,out JlTuple,out JlTuple)"/> 同一算子）。本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>参数取向</b>返回值=经度，out=纬度、半径，与本签名一致；忘记接收返回值就丢失经度是本接口签名带来的典型误用。</para>
	///   <para><b>资源与坑</b>经纬度的弧度/角度约定与反变换一致，无法由代码判定 [待实测]；原点处（x=y=z=0）半径为 0、经纬度不定 [待实测]。</para>
	/// </remarks>
	public static double ConvertPoint3dCartToSpher(double x, double y, double z, string equatPlaneNormal, string zeroMeridian, out double latitude, out double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(997);
		JlNativeApi.StoreD(proc, 0, x);
		JlNativeApi.StoreD(proc, 1, y);
		JlNativeApi.StoreD(proc, 2, z);
		JlNativeApi.StoreS(proc, 3, equatPlaneNormal);
		JlNativeApi.StoreS(proc, 4, zeroMeridian);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out latitude);
		err = JlNativeApi.LoadD(proc, 2, err, out radius);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>读取卡尔曼滤波的描述文件，得到初始模型/测量参数与状态维数。</summary>
	/// <param name="fileName">卡尔曼描述文件路径。Default: "kalman.init"</param>
	/// <param name="model">输出模型元组（DOUBLE 装载）：行主序展平拼接的 A、C、Q，可选 G、u，必要时 L。</param>
	/// <param name="measurement">输出测量矩阵元组（DOUBLE 装载）：行主序展平的噪声协方差矩阵 R。</param>
	/// <param name="prediction">输出外推元组（DOUBLE 装载）：行主序展平的外推误差协方差 P 与初始状态估计 x 拼接。</param>
	/// <returns>状态/测量/控制三维数三元组（INTEGER 装载的新元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>从描述文件读取卡尔曼滤波的整套初始参数并装载为元组，对应原生算子 id 1053。返回值是 [状态维数, 测量维数, 控制维数] 三元组，按 INTEGER 装载。</para>
	///   <para><b>资源与坑</b>文件缺失或格式不合法时抛原生错误；描述文件的键名/矩阵拼接顺序无法由代码判定 [待实测]。要在线调参文件时用 <see cref="UpdateKalman"/>，逐拍递推用 <see cref="FilterKalman"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple dimension = JlMisc.ReadKalman("kalman.init", out JlTuple model, out JlTuple measurement, out JlTuple prediction);
	///   </code>
	/// </remarks>
	public static JlTuple ReadKalman(string fileName, out JlTuple model, out JlTuple measurement, out JlTuple prediction)
	{
		IntPtr proc = JlNativeApi.PreCall(1053);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out model);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out measurement);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out prediction);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>读取卡尔曼滤波更新文件，把其中的参数并入当前模型/测量参数。</summary>
	/// <param name="fileName">卡尔曼更新文件路径。Default: "kalman.updt"</param>
	/// <param name="dimensionIn">当前维度三元组 [状态维数, 测量维数, 控制维数]（INTEGER 语义）。Default: [3,1,0]</param>
	/// <param name="modelIn">行主序展平拼接的 A、C、Q，可选 G、u，必要时 L。Default: [1.0,1.0,0.5,0.0,1.0,1.0,0.0,0.0,1.0,1.0,0.0,0.0,54.3,37.9,48.0,37.9,34.3,42.5,48.0,42.5,43.7]</param>
	/// <param name="measurementIn">行主序展平的噪声协方差矩阵 R。Default: [1,2]</param>
	/// <param name="modelOut">并入文件内容后的新模型元组（DOUBLE 装载）。</param>
	/// <param name="measurementOut">并入文件内容后的新 R 矩阵元组（DOUBLE 装载）。</param>
	/// <returns>并入后的维度三元组（INTEGER 装载的新元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>读取卡尔曼滤波"更新文件"并把其中的模型/测量参数并入传入的当前参数，对应原生算子 id 1054。维度三元组按 INTEGER 装载，模型与测量矩阵按 DOUBLE 装载；A、C、Q（及可选 G、u、L）与 R 均为行主序展平的一维数组。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面（卡尔曼参数管理与本库其它能力混在一起）；本库内部没有任何调用者，也没有把卡尔曼矩阵包装成类型——维数、行主序展平、矩阵拼接全部由调用方自己维护，本方法仅做文件合并。更新文件的格式/键名无法由代码判定 [待实测]。dimensionIn 与 modelIn/measurementIn 的长度必须自洽（n×m 矩阵要填 n*m 个元素），原生侧对越界访问的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>从零初始化整套参数用 <see cref="ReadKalman"/>；逐拍的状态递推用 <see cref="FilterKalman"/>；本方法只用于"调参文件覆盖了哪些矩阵"这种离线合并场景。</para>
	///   <para><b>参数取向</b>返回值为新维度三元组，out 为新模型/新测量，与签名一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple dimension = new int[] { 3, 1, 0 };
	///   JlTuple model = new double[] { 1.0, 1.0, 0.5, 0.0, 1.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 0.0, 54.3, 37.9, 48.0, 37.9, 34.3, 42.5, 48.0, 42.5, 43.7 };
	///   JlTuple measurement = new double[] { 1.0, 2.0 };
	///   JlTuple dimensionOut = JlMisc.UpdateKalman("kalman.updt", dimension, model, measurement, out JlTuple modelOut, out JlTuple measurementOut);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable；输入元组钉住传入（Store+UnpinTuple），标量开销比 double 版略高但可获得多值维度；返回值被丢弃时新维度信息即丢失。</para>
	/// </remarks>
	public static JlTuple UpdateKalman(string fileName, JlTuple dimensionIn, JlTuple modelIn, JlTuple measurementIn, out JlTuple modelOut, out JlTuple measurementOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1054);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.Store(proc, 1, dimensionIn);
		JlNativeApi.Store(proc, 2, modelIn);
		JlNativeApi.Store(proc, 3, measurementIn);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(dimensionIn);
		JlNativeApi.UnpinTuple(modelIn);
		JlNativeApi.UnpinTuple(measurementIn);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out modelOut);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out measurementOut);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>用卡尔曼滤波递推估计系统当前状态；返回下一拍的外推对。</summary>
	/// <param name="dimension">维度三元组 [状态维数, 测量维数, 控制维数]。Default: [3,1,0]</param>
	/// <param name="model">行主序展平拼接的 A、C、Q，可选 G、u，必要时 L。Default: [1.0,1.0,0.5,0.0,1.0,1.0,0.0,0.0,1.0,1.0,0.0,0.0,54.3,37.9,48.0,37.9,34.3,42.5,48.0,42.5,43.7]</param>
	/// <param name="measurement">行主序展平的 R 矩阵 + 测量向量 y 拼接。Default: [1.2,1.0]</param>
	/// <param name="predictionIn">行主序展平的外推误差协方差 P* + 外推向量 x* 拼接。Default: [0.0,0.0,0.0,0.0,180.5,0.0,0.0,0.0,100.0,0.0,100.0,0.0]</param>
	/// <param name="estimate">更新后误差协方差 P~ 与状态估计 x~ 的行主序拼接（DOUBLE 装载）。</param>
	/// <returns>下一拍用的外推协方差 P* 与外推向量 x* 的拼接（DOUBLE 装载的新元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>卡尔曼一步递推：用外推状态 x*/P* 与本轮测量 y（连同 R）算出更新后的估计 x~/P~，并回传下一拍所需的外推对，对应原生算子 id 1055。返回值与 estimate 均按 DOUBLE 装载；各矩阵（A、C、Q、R、P）行主序展平并与各自向量（y、x*）首尾拼接在同一个元组里——拼接顺序错了不会报错，只会静默得错误结果 [待实测：拼接边界由维数三元组决定]。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面，本库内部没有任何调用者，也没有滤波器状态对象——P/x 的"接着上一步走"完全靠调用方把上一拍返回值原样喂回来，顺序断档即失效。dimension=[状态维,测量维,控制维]，控制维非 0 时模型需额外携带 G、u（及 L），所需元素个数由维数决定，代码内不校验。</para>
	///   <para><b>与相邻算子的取舍</b>参数文件合并用 <see cref="UpdateKalman"/>；只想初始化用 <see cref="ReadKalman"/>；本方法才是每帧调用的递推。与简单滑动平均相比，卡尔曼要求你能给出噪声协方差，给不出就退化为调参玄学。</para>
	///   <para><b>参数取向</b>返回值=新外推对（预测下一拍用），out estimate=本拍估计，与签名一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple dimension = new int[] { 3, 1, 0 };
	///   JlTuple model = new double[] { 1.0, 1.0, 0.5, 0.0, 1.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 0.0, 54.3, 37.9, 48.0, 37.9, 34.3, 42.5, 48.0, 42.5, 43.7 };
	///   JlTuple measurement = new double[] { 1.2, 1.0 };
	///   JlTuple predictionIn = new double[] { 0.0, 0.0, 0.0, 0.0, 180.5, 0.0, 0.0, 0.0, 100.0, 0.0, 100.0, 0.0 };
	///   JlTuple predictionOut = JlMisc.FilterKalman(dimension, model, measurement, predictionIn, out JlTuple estimate);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable；本拍忽略返回值 predictionOut 会导致下一拍外推没有增量信息。</para>
	/// </remarks>
	public static JlTuple FilterKalman(JlTuple dimension, JlTuple model, JlTuple measurement, JlTuple predictionIn, out JlTuple estimate)
	{
		IntPtr proc = JlNativeApi.PreCall(1055);
		JlNativeApi.Store(proc, 0, dimension);
		JlNativeApi.Store(proc, 1, model);
		JlNativeApi.Store(proc, 2, measurement);
		JlNativeApi.Store(proc, 3, predictionIn);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(dimension);
		JlNativeApi.UnpinTuple(model);
		JlNativeApi.UnpinTuple(measurement);
		JlNativeApi.UnpinTuple(predictionIn);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out estimate);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>生成用于校正标定的棋盘格 PostScript 图形文件。</summary>
	/// <param name="width">棋盘格图案宽度（米，不含外框两格）。Default: 0.17</param>
	/// <param name="numSquares">每行/每列的方格数。Default: 17</param>
	/// <param name="gridFile">输出 PostScript 文件名。Default: "rectification_grid.ps"</param>
	/// <remarks>
	///   <para><b>功能说明</b>生成一份可用打印机输出的棋盘格 PostScript 文件，供标定棋盘格时打印，对应原生算子 id 1105；三个参数均以标量直写，无返回值。</para>
	///   <para><b>资源与坑</b>目标路径不可写时抛原生错误；打印后按实际量测的网格点，配合 <see cref="GenArbitraryDistortionMap"/> 生成畸变映射图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMisc.CreateRectificationGrid(0.17, 17, "rectification_grid.ps");
	///   </code>
	/// </remarks>
	public static void CreateRectificationGrid(double width, int numSquares, string gridFile)
	{
		IntPtr proc = JlNativeApi.PreCall(1105);
		JlNativeApi.StoreD(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, numSquares);
		JlNativeApi.StoreS(proc, 2, gridFile);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>由实测网格点生成"任意畸变图像 ↔ 校正图像"的投影映射图。</summary>
	/// <param name="gridSpacing">校正图像中网格点间距（像素，整型直写）。</param>
	/// <param name="row">畸变图像中网格点的 row 坐标元组。</param>
	/// <param name="column">畸变图像中网格点的 column 坐标元组，与 row 等长。</param>
	/// <param name="gridWidth">点网格宽度（网格点数，整型直写）。</param>
	/// <param name="imageWidth">待校正图像的宽度（像素）。</param>
	/// <param name="imageHeight">待校正图像的高度（像素）。</param>
	/// <param name="mapType">映射/插值类型串。Default: "bilinear"</param>
	/// <returns>承载映射数据的新 JlImage 句柄（非原地改写，用毕须释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>由"标定图上网格点在畸变图像中的位置"生成一张投影映射图，供后续按图校正任意畸变，对应原生算子 id 1108。gridSpacing/gridWidth/imageWidth/imageHeight 以整型直写（StoreI），row/column 以元组钉住传入；映射数据以新句柄返回（JlImage.LoadNew，非原地改写）。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面（标定辅助与几何/卡尔曼混在一起），本库内部没有任何调用者。row/column 必须是同一网格逐点展开的等长元组，且与 gridWidth、imageWidth/imageHeight 自洽；<c>mapType</c> 的合法取值集与插值细节无法由代码判定 [待实测]。本库不提供 3D/显示能力，映射图只能配合本库仍存在的图像变换算子使用。</para>
	///   <para><b>与相邻算子的取舍</b>规则网格畸变优先用 <c>JlXLDPoly.GenGridRectificationMap</c>（本库仍在，按正交网格点拟合）；本方法面向"任意畸变"——网格点可不规则时用 <see cref="CreateRectificationGrid"/> 打图、量测出实际点位后再生成映射。网格点越密内存越大，够用即止。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = new double[] { 10.0, 10.0, 210.0, 210.0 };
	///   JlTuple column = new double[] { 10.0, 210.0, 10.0, 210.0 };
	///   JlImage map = JlMisc.GenArbitraryDistortionMap(200, row, column, 2, 640, 480, "bilinear");
	///   map.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新 JlImage 句柄，用毕须 Dispose；输入 row/column 在原生调用完成前不得释放（钉住由包装层处理）。</para>
	/// </remarks>
	public static JlImage GenArbitraryDistortionMap(int gridSpacing, JlTuple row, JlTuple column, int gridWidth, int imageWidth, int imageHeight, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1108);
		JlNativeApi.StoreI(proc, 0, gridSpacing);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.StoreI(proc, 3, gridWidth);
		JlNativeApi.StoreI(proc, 4, imageWidth);
		JlNativeApi.StoreI(proc, 5, imageHeight);
		JlNativeApi.StoreS(proc, 6, mapType);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}

	/// <summary>把点正投影到由两定点定义的无限长直线上，返回垂足坐标。</summary>
	/// <param name="row">待投影点的 row 坐标元组。</param>
	/// <param name="column">待投影点的 column 坐标元组。</param>
	/// <param name="row1">直线第一定点的 row 坐标元组。</param>
	/// <param name="column1">直线第一定点的 column 坐标元组。</param>
	/// <param name="row2">直线第二定点的 row 坐标元组。</param>
	/// <param name="column2">直线第二定点的 column 坐标元组。</param>
	/// <param name="rowProj">垂足 row 坐标（DOUBLE 装载）。</param>
	/// <param name="colProj">垂足 column 坐标（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把点 (row,column) 正投影到过 (row1,column1)-(row2,column2) 的无限长直线上，输出投影点，对应原生算子 id 1277。输出按 DOUBLE 装载；六个输入均可逐点配对广播成多条投影。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组（与卡尔曼、序列化等混装）；经 Grep 核实本库内部对它没有任何调用者。坐标为图像坐标系：row=y 向下为正、column=x 向右为正，距离单位是像素。直线两定点重合（退化直线）时投影结果不可信 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"点到线段的距离"用 <see cref="DistancePs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>（线段不外延）；要垂足坐标才用本方法。标量版 double 重载无钉固定开销，单点计算优先它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = new double[] { 5.0 };
	///   JlTuple column = new double[] { 5.0 };
	///   JlTuple row1 = new double[] { 0.0 };
	///   JlTuple column1 = new double[] { 0.0 };
	///   JlTuple row2 = new double[] { 10.0 };
	///   JlTuple column2 = new double[] { 0.0 };
	///   JlMisc.ProjectionPl(row, column, row1, column1, row2, column2, out JlTuple rowProj, out JlTuple colProj);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void ProjectionPl(JlTuple row, JlTuple column, JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2, out JlTuple rowProj, out JlTuple colProj)
	{
		IntPtr proc = JlNativeApi.PreCall(1277);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, row1);
		JlNativeApi.Store(proc, 3, column1);
		JlNativeApi.Store(proc, 4, row2);
		JlNativeApi.Store(proc, 5, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowProj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out colProj);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把点正投影到由两定点定义的无限长直线上，返回垂足坐标（标量版）。</summary>
	/// <param name="row">待投影点的 row 坐标。</param>
	/// <param name="column">待投影点的 column 坐标。</param>
	/// <param name="row1">直线第一定点的 row 坐标。</param>
	/// <param name="column1">直线第一定点的 column 坐标。</param>
	/// <param name="row2">直线第二定点的 row 坐标。</param>
	/// <param name="column2">直线第二定点的 column 坐标。</param>
	/// <param name="rowProj">垂足 row 坐标。</param>
	/// <param name="colProj">垂足 column 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把单个点正投影到过 (row1,column1)-(row2,column2) 的无限长直线上，输出垂足，对应原生算子 id 1277（与本类元组版 <see cref="ProjectionPl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/> 同一算子）。本重载全部以标量直写（StoreD），无钉固定元组开销，单点计算优先它。</para>
	///   <para><b>资源与坑</b>坐标为图像坐标系（row 向下、column 向右），单位像素；直线两定点重合（退化直线）时垂足结果不可信 [待实测]。</para>
	/// </remarks>
	public static void ProjectionPl(double row, double column, double row1, double column1, double row2, double column2, out double rowProj, out double colProj)
	{
		IntPtr proc = JlNativeApi.PreCall(1277);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, row1);
		JlNativeApi.StoreD(proc, 3, column1);
		JlNativeApi.StoreD(proc, 4, row2);
		JlNativeApi.StoreD(proc, 5, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out rowProj);
		err = JlNativeApi.LoadD(proc, 1, err, out colProj);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>求椭圆周上指定角度处点的坐标（元组版）。</summary>
	/// <param name="angle">所求点对应的角度元组（弧度）。Default: 0</param>
	/// <param name="row">椭圆中心 row 坐标。</param>
	/// <param name="column">椭圆中心 column 坐标。</param>
	/// <param name="phi">椭圆主轴朝向（弧度）。</param>
	/// <param name="radius1">长半轴长度。</param>
	/// <param name="radius2">短半轴长度。</param>
	/// <param name="rowPoint">所求点的 row 坐标（DOUBLE 装载）。</param>
	/// <param name="colPoint">所求点的 column 坐标（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>按角度元组逐点算出椭圆周上的坐标，对应原生算子 id 1278；两个输出均按 DOUBLE 装载，输入 angle 元组钉住传入。</para>
	///   <para><b>资源与坑</b>坐标系以 row 为第一轴；半径与角度的组合发生退化（radius 为 0）时结果未定义 [待实测]。单点计算可用下面的 double 重载，无钉固定开销。</para>
	/// </remarks>
	public static void GetPointsEllipse(JlTuple angle, double row, double column, double phi, double radius1, double radius2, out JlTuple rowPoint, out JlTuple colPoint)
	{
		IntPtr proc = JlNativeApi.PreCall(1278);
		JlNativeApi.Store(proc, 0, angle);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, radius1);
		JlNativeApi.StoreD(proc, 5, radius2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(angle);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowPoint);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out colPoint);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>求椭圆周上指定角度处点的坐标（标量版）。</summary>
	/// <param name="angle">所求点对应的角度（弧度）。Default: 0</param>
	/// <param name="row">椭圆中心 row 坐标。</param>
	/// <param name="column">椭圆中心 column 坐标。</param>
	/// <param name="phi">椭圆主轴朝向（弧度）。</param>
	/// <param name="radius1">长半轴长度。</param>
	/// <param name="radius2">短半轴长度。</param>
	/// <param name="rowPoint">所求点的 row 坐标。</param>
	/// <param name="colPoint">所求点的 column 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b>求单个椭圆周上指定角度处的坐标，对应原生算子 id 1278（与本类元组版 <see cref="GetPointsEllipse(JlTuple,double,double,double,double,double,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>半径或角度退化时结果未定义 [待实测]。</para>
	/// </remarks>
	public static void GetPointsEllipse(double angle, double row, double column, double phi, double radius1, double radius2, out double rowPoint, out double colPoint)
	{
		IntPtr proc = JlNativeApi.PreCall(1278);
		JlNativeApi.StoreD(proc, 0, angle);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, radius1);
		JlNativeApi.StoreD(proc, 5, radius2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out rowPoint);
		err = JlNativeApi.LoadD(proc, 1, err, out colPoint);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>求两条直线（各由两点确定）的交点，并给出是否平行的判定。</summary>
	/// <param name="rowA1">直线 A 第一定点的 row 坐标元组。</param>
	/// <param name="columnA1">直线 A 第一定点的 column 坐标元组。</param>
	/// <param name="rowA2">直线 A 第二定点的 row 坐标元组。</param>
	/// <param name="columnA2">直线 A 第二定点的 column 坐标元组。</param>
	/// <param name="rowB1">直线 B 第一定点的 row 坐标元组。</param>
	/// <param name="columnB1">直线 B 第一定点的 column 坐标元组。</param>
	/// <param name="rowB2">直线 B 第二定点的 row 坐标元组。</param>
	/// <param name="columnB2">直线 B 第二定点的 column 坐标元组。</param>
	/// <param name="row">交点 row 坐标（DOUBLE 装载；平行时不可信）。</param>
	/// <param name="column">交点 column 坐标（DOUBLE 装载；平行时不可信）。</param>
	/// <param name="isParallel">平行标志（INTEGER 装载，非 0 表示平行）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>求两直线（各由两点确定）的交点，对应原生算子 id 1279。交点 row/column 按 DOUBLE 装载，isParallel 按 INTEGER 装载（非 0 = 平行），三者在多组配对输入下逐条给出。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，经 Grep 核实本库内部没有任何调用者；坐标为图像坐标系（row 向下、column 向右），单位像素。平行时 row/column 的取值不可信，必须先查 isParallel 再用交点。直线自身退化（两点重合）时结果未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>两直线夹角用 <see cref="AngleLl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>；交点落在哪条线的"线段范围内"本方法不判断——它按无限长直线求交。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowA1 = new double[] { 0.0 };
	///   JlTuple columnA1 = new double[] { 0.0 };
	///   JlTuple rowA2 = new double[] { 10.0 };
	///   JlTuple columnA2 = new double[] { 10.0 };
	///   JlTuple rowB1 = new double[] { 0.0 };
	///   JlTuple columnB1 = new double[] { 10.0 };
	///   JlTuple rowB2 = new double[] { 10.0 };
	///   JlTuple columnB2 = new double[] { 0.0 };
	///   JlMisc.IntersectionLl(rowA1, columnA1, rowA2, columnA2, rowB1, columnB1, rowB2, columnB2, out JlTuple row, out JlTuple column, out JlTuple isParallel);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void IntersectionLl(JlTuple rowA1, JlTuple columnA1, JlTuple rowA2, JlTuple columnA2, JlTuple rowB1, JlTuple columnB1, JlTuple rowB2, JlTuple columnB2, out JlTuple row, out JlTuple column, out JlTuple isParallel)
	{
		IntPtr proc = JlNativeApi.PreCall(1279);
		JlNativeApi.Store(proc, 0, rowA1);
		JlNativeApi.Store(proc, 1, columnA1);
		JlNativeApi.Store(proc, 2, rowA2);
		JlNativeApi.Store(proc, 3, columnA2);
		JlNativeApi.Store(proc, 4, rowB1);
		JlNativeApi.Store(proc, 5, columnB1);
		JlNativeApi.Store(proc, 6, rowB2);
		JlNativeApi.Store(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowA1);
		JlNativeApi.UnpinTuple(columnA1);
		JlNativeApi.UnpinTuple(rowA2);
		JlNativeApi.UnpinTuple(columnA2);
		JlNativeApi.UnpinTuple(rowB1);
		JlNativeApi.UnpinTuple(columnB1);
		JlNativeApi.UnpinTuple(rowB2);
		JlNativeApi.UnpinTuple(columnB2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out isParallel);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>求两条直线（各由两点确定）的交点，并给出是否平行的判定（标量版）。</summary>
	/// <param name="rowA1">直线 A 第一定点的 row 坐标。</param>
	/// <param name="columnA1">直线 A 第一定点的 column 坐标。</param>
	/// <param name="rowA2">直线 A 第二定点的 row 坐标。</param>
	/// <param name="columnA2">直线 A 第二定点的 column 坐标。</param>
	/// <param name="rowB1">直线 B 第一定点的 row 坐标。</param>
	/// <param name="columnB1">直线 B 第一定点的 column 坐标。</param>
	/// <param name="rowB2">直线 B 第二定点的 row 坐标。</param>
	/// <param name="columnB2">直线 B 第二定点的 column 坐标。</param>
	/// <param name="row">交点 row 坐标（平行时不可信）。</param>
	/// <param name="column">交点 column 坐标（平行时不可信）。</param>
	/// <param name="isParallel">平行标志（非 0 表示平行）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>求两条由两点确定的无限长直线的交点，对应原生算子 id 1279（与本类元组版 <see cref="IntersectionLl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD/StoreI），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>坐标为图像坐标系（row 向下、column 向右），单位像素。平行时交点 row/column 不可信，必须先查 isParallel 再用交点。</para>
	/// </remarks>
	public static void IntersectionLl(double rowA1, double columnA1, double rowA2, double columnA2, double rowB1, double columnB1, double rowB2, double columnB2, out double row, out double column, out int isParallel)
	{
		IntPtr proc = JlNativeApi.PreCall(1279);
		JlNativeApi.StoreD(proc, 0, rowA1);
		JlNativeApi.StoreD(proc, 1, columnA1);
		JlNativeApi.StoreD(proc, 2, rowA2);
		JlNativeApi.StoreD(proc, 3, columnA2);
		JlNativeApi.StoreD(proc, 4, rowB1);
		JlNativeApi.StoreD(proc, 5, columnB1);
		JlNativeApi.StoreD(proc, 6, rowB2);
		JlNativeApi.StoreD(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		err = JlNativeApi.LoadI(proc, 2, err, out isParallel);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算从横轴（column 方向）转到两点连线的角度 [rad]。</summary>
	/// <param name="row1">直线第一定点的 row 坐标元组。</param>
	/// <param name="column1">直线第一定点的 column 坐标元组。</param>
	/// <param name="row2">直线第二定点的 row 坐标元组。</param>
	/// <param name="column2">直线第二定点的 column 坐标元组。</param>
	/// <returns>转角元组（DOUBLE 装载，弧度制）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>返回从横轴（column 方向）转到两点连线的角度，单位弧度，对应原生算子 id 1309。输出按 DOUBLE 装载，多组配对输入逐条给出。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。因 row 向下为正，旋转正方向在屏幕上表现为顺时针；取值区间（如 [0,2π) 或 (-π,π]）无法由代码判定 [待实测]。两点重合时角度未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"两条线之间的夹角"用 <see cref="AngleLl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>，不要拿两次 AngleLx 相减再自行归一化。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row1 = new double[] { 0.0 };
	///   JlTuple column1 = new double[] { 0.0 };
	///   JlTuple row2 = new double[] { 10.0 };
	///   JlTuple column2 = new double[] { 10.0 };
	///   JlTuple phi = JlMisc.AngleLx(row1, column1, row2, column2);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlTuple AngleLx(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1309);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>计算从横轴（column 方向）转到两点连线的角度 [rad]（标量版）。</summary>
	/// <param name="row1">直线第一定点的 row 坐标。</param>
	/// <param name="column1">直线第一定点的 column 坐标。</param>
	/// <param name="row2">直线第二定点的 row 坐标。</param>
	/// <param name="column2">直线第二定点的 column 坐标。</param>
	/// <returns>转角（弧度）标量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>返回从横轴（column 方向）转到两点连线的角度，单位弧度，对应原生算子 id 1309（与本类元组版 <see cref="AngleLx(JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>取值区间与旋转正方向约定（row 向下为正导致的翻转）无法由代码判定 [待实测]；两点重合时角度未定义 [待实测]。</para>
	/// </remarks>
	public static double AngleLx(double row1, double column1, double row2, double column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1309);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>计算两条直线（各由两点确定）之间的夹角 [rad]。</summary>
	/// <param name="rowA1">直线 A 第一定点的 row 坐标元组。</param>
	/// <param name="columnA1">直线 A 第一定点的 column 坐标元组。</param>
	/// <param name="rowA2">直线 A 第二定点的 row 坐标元组。</param>
	/// <param name="columnA2">直线 A 第二定点的 column 坐标元组。</param>
	/// <param name="rowB1">直线 B 第一定点的 row 坐标元组。</param>
	/// <param name="columnB1">直线 B 第一定点的 column 坐标元组。</param>
	/// <param name="rowB2">直线 B 第二定点的 row 坐标元组。</param>
	/// <param name="columnB2">直线 B 第二定点的 column 坐标元组。</param>
	/// <returns>夹角元组（DOUBLE 装载，弧度制）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>返回两条直线（各由两点确定）之间的夹角，单位弧度，对应原生算子 id 1310；输出按 DOUBLE 装载，支持逐点配对的多元组输入。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。夹角是否区分方向（有向角/无向角）、取值区间无法由代码判定 [待实测]；平行与垂直时各自返回什么值也应以实测为准。任一方向量退化（两点重合）结果未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要"线对水平轴的倾角"用 <see cref="AngleLx(JlTuple,JlTuple,JlTuple,JlTuple)"/>；要判平行优先 <see cref="IntersectionLl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 的 isParallel（INTEGER，语义明确），别拿角度比较浮点。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowA1 = new double[] { 0.0 };
	///   JlTuple columnA1 = new double[] { 0.0 };
	///   JlTuple rowA2 = new double[] { 10.0 };
	///   JlTuple columnA2 = new double[] { 0.0 };
	///   JlTuple rowB1 = new double[] { 0.0 };
	///   JlTuple columnB1 = new double[] { 0.0 };
	///   JlTuple rowB2 = new double[] { 0.0 };
	///   JlTuple columnB2 = new double[] { 10.0 };
	///   JlTuple phi = JlMisc.AngleLl(rowA1, columnA1, rowA2, columnA2, rowB1, columnB1, rowB2, columnB2);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlTuple AngleLl(JlTuple rowA1, JlTuple columnA1, JlTuple rowA2, JlTuple columnA2, JlTuple rowB1, JlTuple columnB1, JlTuple rowB2, JlTuple columnB2)
	{
		IntPtr proc = JlNativeApi.PreCall(1310);
		JlNativeApi.Store(proc, 0, rowA1);
		JlNativeApi.Store(proc, 1, columnA1);
		JlNativeApi.Store(proc, 2, rowA2);
		JlNativeApi.Store(proc, 3, columnA2);
		JlNativeApi.Store(proc, 4, rowB1);
		JlNativeApi.Store(proc, 5, columnB1);
		JlNativeApi.Store(proc, 6, rowB2);
		JlNativeApi.Store(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowA1);
		JlNativeApi.UnpinTuple(columnA1);
		JlNativeApi.UnpinTuple(rowA2);
		JlNativeApi.UnpinTuple(columnA2);
		JlNativeApi.UnpinTuple(rowB1);
		JlNativeApi.UnpinTuple(columnB1);
		JlNativeApi.UnpinTuple(rowB2);
		JlNativeApi.UnpinTuple(columnB2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>计算两条直线（各由两点确定）之间的夹角 [rad]（标量版）。</summary>
	/// <param name="rowA1">直线 A 第一定点的 row 坐标。</param>
	/// <param name="columnA1">直线 A 第一定点的 column 坐标。</param>
	/// <param name="rowA2">直线 A 第二定点的 row 坐标。</param>
	/// <param name="columnA2">直线 A 第二定点的 column 坐标。</param>
	/// <param name="rowB1">直线 B 第一定点的 row 坐标。</param>
	/// <param name="columnB1">直线 B 第一定点的 column 坐标。</param>
	/// <param name="rowB2">直线 B 第二定点的 row 坐标。</param>
	/// <param name="columnB2">直线 B 第二定点的 column 坐标。</param>
	/// <returns>两条直线的夹角（弧度）标量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>返回两条由两点确定的直线之间的夹角，单位弧度，对应原生算子 id 1310（与本类元组版 <see cref="AngleLl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>夹角是否区分方向（有向/无向）、取值区间无法由代码判定 [待实测]；方向量退化（两点重合）时结果未定义 [待实测]。</para>
	/// </remarks>
	public static double AngleLl(double rowA1, double columnA1, double rowA2, double columnA2, double rowB1, double columnB1, double rowB2, double columnB2)
	{
		IntPtr proc = JlNativeApi.PreCall(1310);
		JlNativeApi.StoreD(proc, 0, rowA1);
		JlNativeApi.StoreD(proc, 1, columnA1);
		JlNativeApi.StoreD(proc, 2, rowA2);
		JlNativeApi.StoreD(proc, 3, columnA2);
		JlNativeApi.StoreD(proc, 4, rowB1);
		JlNativeApi.StoreD(proc, 5, columnB1);
		JlNativeApi.StoreD(proc, 6, rowB2);
		JlNativeApi.StoreD(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>计算线段到（无限长）直线的最大/最小距离。</summary>
	/// <param name="rowA1">线段 A 第一端点的 row 坐标元组。</param>
	/// <param name="columnA1">线段 A 第一端点的 column 坐标元组。</param>
	/// <param name="rowA2">线段 A 第二端点的 row 坐标元组。</param>
	/// <param name="columnA2">线段 A 第二端点的 column 坐标元组。</param>
	/// <param name="rowB1">直线 B 第一定点的 row 坐标元组。</param>
	/// <param name="columnB1">直线 B 第一定点的 column 坐标元组。</param>
	/// <param name="rowB2">直线 B 第二定点的 row 坐标元组。</param>
	/// <param name="columnB2">直线 B 第二定点的 column 坐标元组。</param>
	/// <param name="distanceMin">最小距离（DOUBLE 装载；相交时为 0）。</param>
	/// <param name="distanceMax">最大距离（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>同时给出"线段 A（rowA1,columnA1)-(rowA2,columnA2) 到无限长直线 B"的最大与最小距离（像素），对应原生算子 id 1311；两个输出均按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。线段不外延、直线外延——A 与 B 相交时 distanceMin 为 0，max 取线段端点中较远者的垂距；混合语义容易误用。退化线段（两点重合）时按点处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>两线段之间用 <see cref="DistanceSs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>；点到线段用 <see cref="DistancePs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>；本方法即使只要最短距离也必须接一个 max 出参。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowA1 = new double[] { 0.0 };
	///   JlTuple columnA1 = new double[] { 0.0 };
	///   JlTuple rowA2 = new double[] { 10.0 };
	///   JlTuple columnA2 = new double[] { 0.0 };
	///   JlTuple rowB1 = new double[] { 5.0 };
	///   JlTuple columnB1 = new double[] { -5.0 };
	///   JlTuple rowB2 = new double[] { 5.0 };
	///   JlTuple columnB2 = new double[] { 5.0 };
	///   JlMisc.DistanceSl(rowA1, columnA1, rowA2, columnA2, rowB1, columnB1, rowB2, columnB2, out JlTuple distanceMin, out JlTuple distanceMax);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void DistanceSl(JlTuple rowA1, JlTuple columnA1, JlTuple rowA2, JlTuple columnA2, JlTuple rowB1, JlTuple columnB1, JlTuple rowB2, JlTuple columnB2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1311);
		JlNativeApi.Store(proc, 0, rowA1);
		JlNativeApi.Store(proc, 1, columnA1);
		JlNativeApi.Store(proc, 2, rowA2);
		JlNativeApi.Store(proc, 3, columnA2);
		JlNativeApi.Store(proc, 4, rowB1);
		JlNativeApi.Store(proc, 5, columnB1);
		JlNativeApi.Store(proc, 6, rowB2);
		JlNativeApi.Store(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowA1);
		JlNativeApi.UnpinTuple(columnA1);
		JlNativeApi.UnpinTuple(rowA2);
		JlNativeApi.UnpinTuple(columnA2);
		JlNativeApi.UnpinTuple(rowB1);
		JlNativeApi.UnpinTuple(columnB1);
		JlNativeApi.UnpinTuple(rowB2);
		JlNativeApi.UnpinTuple(columnB2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算线段到（无限长）直线的最大/最小距离（标量版）。</summary>
	/// <param name="rowA1">线段 A 第一端点的 row 坐标。</param>
	/// <param name="columnA1">线段 A 第一端点的 column 坐标。</param>
	/// <param name="rowA2">线段 A 第二端点的 row 坐标。</param>
	/// <param name="columnA2">线段 A 第二端点的 column 坐标。</param>
	/// <param name="rowB1">直线 B 第一定点的 row 坐标。</param>
	/// <param name="columnB1">直线 B 第一定点的 column 坐标。</param>
	/// <param name="rowB2">直线 B 第二定点的 row 坐标。</param>
	/// <param name="columnB2">直线 B 第二定点的 column 坐标。</param>
	/// <param name="distanceMin">最小距离（相交时为 0）。</param>
	/// <param name="distanceMax">最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>同时给出"线段 A 到无限长直线 B"的最大与最小距离（像素），对应原生算子 id 1311（与本类元组版 <see cref="DistanceSl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>线段不外延、直线外延——A 与 B 相交时 distanceMin 为 0，max 取线段端点中较远者的垂距；混合语义容易误用。</para>
	/// </remarks>
	public static void DistanceSl(double rowA1, double columnA1, double rowA2, double columnA2, double rowB1, double columnB1, double rowB2, double columnB2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1311);
		JlNativeApi.StoreD(proc, 0, rowA1);
		JlNativeApi.StoreD(proc, 1, columnA1);
		JlNativeApi.StoreD(proc, 2, rowA2);
		JlNativeApi.StoreD(proc, 3, columnA2);
		JlNativeApi.StoreD(proc, 4, rowB1);
		JlNativeApi.StoreD(proc, 5, columnB1);
		JlNativeApi.StoreD(proc, 6, rowB2);
		JlNativeApi.StoreD(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算两条线段之间的最大/最小距离。</summary>
	/// <param name="rowA1">线段 A 第一端点的 row 坐标元组。</param>
	/// <param name="columnA1">线段 A 第一端点的 column 坐标元组。</param>
	/// <param name="rowA2">线段 A 第二端点的 row 坐标元组。</param>
	/// <param name="columnA2">线段 A 第二端点的 column 坐标元组。</param>
	/// <param name="rowB1">线段 B 第一端点的 row 坐标元组。</param>
	/// <param name="columnB1">线段 B 第一端点的 column 坐标元组。</param>
	/// <param name="rowB2">线段 B 第二端点的 row 坐标元组。</param>
	/// <param name="columnB2">线段 B 第二端点的 column 坐标元组。</param>
	/// <param name="distanceMin">最小距离（DOUBLE 装载；相交/接触时为 0）。</param>
	/// <param name="distanceMax">最大距离（DOUBLE 装载；必在端点对上）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>给出两条线段之间的最小与最大距离（像素）：min 在相交/接触时为 0，max 必落在某对端点上，对应原生算子 id 1312；两输出均按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。两侧都是线段（不外延），与 DistanceSl/DistancePl 的"直线"语义不同；退化线段按点处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>一侧要按无限长直线算用 <see cref="DistanceSl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>；点与线段用 <see cref="DistancePs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowA1 = new double[] { 0.0 };
	///   JlTuple columnA1 = new double[] { 0.0 };
	///   JlTuple rowA2 = new double[] { 10.0 };
	///   JlTuple columnA2 = new double[] { 0.0 };
	///   JlTuple rowB1 = new double[] { 0.0 };
	///   JlTuple columnB1 = new double[] { 5.0 };
	///   JlTuple rowB2 = new double[] { 10.0 };
	///   JlTuple columnB2 = new double[] { 5.0 };
	///   JlMisc.DistanceSs(rowA1, columnA1, rowA2, columnA2, rowB1, columnB1, rowB2, columnB2, out JlTuple distanceMin, out JlTuple distanceMax);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void DistanceSs(JlTuple rowA1, JlTuple columnA1, JlTuple rowA2, JlTuple columnA2, JlTuple rowB1, JlTuple columnB1, JlTuple rowB2, JlTuple columnB2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1312);
		JlNativeApi.Store(proc, 0, rowA1);
		JlNativeApi.Store(proc, 1, columnA1);
		JlNativeApi.Store(proc, 2, rowA2);
		JlNativeApi.Store(proc, 3, columnA2);
		JlNativeApi.Store(proc, 4, rowB1);
		JlNativeApi.Store(proc, 5, columnB1);
		JlNativeApi.Store(proc, 6, rowB2);
		JlNativeApi.Store(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowA1);
		JlNativeApi.UnpinTuple(columnA1);
		JlNativeApi.UnpinTuple(rowA2);
		JlNativeApi.UnpinTuple(columnA2);
		JlNativeApi.UnpinTuple(rowB1);
		JlNativeApi.UnpinTuple(columnB1);
		JlNativeApi.UnpinTuple(rowB2);
		JlNativeApi.UnpinTuple(columnB2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算两条线段之间的最大/最小距离（标量版）。</summary>
	/// <param name="rowA1">线段 A 第一端点的 row 坐标。</param>
	/// <param name="columnA1">线段 A 第一端点的 column 坐标。</param>
	/// <param name="rowA2">线段 A 第二端点的 row 坐标。</param>
	/// <param name="columnA2">线段 A 第二端点的 column 坐标。</param>
	/// <param name="rowB1">线段 B 第一端点的 row 坐标。</param>
	/// <param name="columnB1">线段 B 第一端点的 column 坐标。</param>
	/// <param name="rowB2">线段 B 第二端点的 row 坐标。</param>
	/// <param name="columnB2">线段 B 第二端点的 column 坐标。</param>
	/// <param name="distanceMin">最小距离（相交/接触时为 0）。</param>
	/// <param name="distanceMax">最大距离（必在端点对上）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>给出两条线段之间的最小与最大距离（像素）：min 在相交/接触时为 0，max 必落在某对端点上，对应原生算子 id 1312（与本类元组版 <see cref="DistanceSs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>两侧都是线段（不外延），与 DistanceSl/DistancePl 的"直线"语义不同，勿混用。</para>
	/// </remarks>
	public static void DistanceSs(double rowA1, double columnA1, double rowA2, double columnA2, double rowB1, double columnB1, double rowB2, double columnB2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1312);
		JlNativeApi.StoreD(proc, 0, rowA1);
		JlNativeApi.StoreD(proc, 1, columnA1);
		JlNativeApi.StoreD(proc, 2, rowA2);
		JlNativeApi.StoreD(proc, 3, columnA2);
		JlNativeApi.StoreD(proc, 4, rowB1);
		JlNativeApi.StoreD(proc, 5, columnB1);
		JlNativeApi.StoreD(proc, 6, rowB2);
		JlNativeApi.StoreD(proc, 7, columnB2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算点到线段的最大/最小距离。</summary>
	/// <param name="row">待测点的 row 坐标元组。</param>
	/// <param name="column">待测点的 column 坐标元组。</param>
	/// <param name="row1">线段第一端点的 row 坐标元组。</param>
	/// <param name="column1">线段第一端点的 column 坐标元组。</param>
	/// <param name="row2">线段第二端点的 row 坐标元组。</param>
	/// <param name="column2">线段第二端点的 column 坐标元组。</param>
	/// <param name="distanceMin">最小距离（DOUBLE 装载）。</param>
	/// <param name="distanceMax">最大距离（DOUBLE 装载，到较远端点）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>点到线段的最小/最大距离（像素）：垂足落在线段内时 min 为垂距，否则为到较近端点的距离；max 必为到某一端点的距离。对应原生算子 id 1313，两输出均按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。线段两点重合时退化为点点距离 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>把线段当无限长直线量垂距（单一返回值）用 <see cref="DistancePl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>；要垂足坐标用 <see cref="ProjectionPl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = new double[] { 5.0 };
	///   JlTuple column = new double[] { 3.0 };
	///   JlTuple row1 = new double[] { 0.0 };
	///   JlTuple column1 = new double[] { 0.0 };
	///   JlTuple row2 = new double[] { 10.0 };
	///   JlTuple column2 = new double[] { 0.0 };
	///   JlMisc.DistancePs(row, column, row1, column1, row2, column2, out JlTuple distanceMin, out JlTuple distanceMax);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void DistancePs(JlTuple row, JlTuple column, JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1313);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, row1);
		JlNativeApi.Store(proc, 3, column1);
		JlNativeApi.Store(proc, 4, row2);
		JlNativeApi.Store(proc, 5, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算点到线段的最大/最小距离（标量版）。</summary>
	/// <param name="row">待测点的 row 坐标。</param>
	/// <param name="column">待测点的 column 坐标。</param>
	/// <param name="row1">线段第一端点的 row 坐标。</param>
	/// <param name="column1">线段第一端点的 column 坐标。</param>
	/// <param name="row2">线段第二端点的 row 坐标。</param>
	/// <param name="column2">线段第二端点的 column 坐标。</param>
	/// <param name="distanceMin">最小距离。</param>
	/// <param name="distanceMax">最大距离（到较远端点）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>点到线段的最小/最大距离（像素）：垂足落在线段内时 min 为垂距，否则为到较近端点的距离；max 必为到某一端点的距离。对应原生算子 id 1313（与本类元组版 <see cref="DistancePs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>把线段当无限长直线量垂距用 <see cref="DistancePl(double,double,double,double,double,double)"/>；线段两点重合时退化为点点距离 [待实测]。</para>
	/// </remarks>
	public static void DistancePs(double row, double column, double row1, double column1, double row2, double column2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1313);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, row1);
		JlNativeApi.StoreD(proc, 3, column1);
		JlNativeApi.StoreD(proc, 4, row2);
		JlNativeApi.StoreD(proc, 5, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>计算点到（无限长）直线的垂直距离。</summary>
	/// <param name="row">待测点的 row 坐标元组。</param>
	/// <param name="column">待测点的 column 坐标元组。</param>
	/// <param name="row1">直线第一定点的 row 坐标元组。</param>
	/// <param name="column1">直线第一定点的 column 坐标元组。</param>
	/// <param name="row2">直线第二定点的 row 坐标元组。</param>
	/// <param name="column2">直线第二定点的 column 坐标元组。</param>
	/// <returns>垂距元组（DOUBLE 装载，像素）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>点到无限长直线的垂直距离（像素），对应原生算子 id 1314；返回按 DOUBLE 装载。老模板的英文 returns 误写作 "Distance between the points"，实为点到线距离。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。直线由两点外延定义，垂足不在线段内也照算；直线两点重合时行为未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>目标是"线段"时用 <see cref="DistancePs(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple)"/>，否则会把"点到线段延长线"的距离当成目标值——这是本组最常见的用错；只需点点距离用 <see cref="DistancePp(JlTuple,JlTuple,JlTuple,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = new double[] { 5.0 };
	///   JlTuple column = new double[] { 3.0 };
	///   JlTuple row1 = new double[] { 0.0 };
	///   JlTuple column1 = new double[] { 0.0 };
	///   JlTuple row2 = new double[] { 10.0 };
	///   JlTuple column2 = new double[] { 0.0 };
	///   JlTuple dist = JlMisc.DistancePl(row, column, row1, column1, row2, column2);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlTuple DistancePl(JlTuple row, JlTuple column, JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1314);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, row1);
		JlNativeApi.Store(proc, 3, column1);
		JlNativeApi.Store(proc, 4, row2);
		JlNativeApi.Store(proc, 5, column2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>计算点到（无限长）直线的垂直距离（标量版）。</summary>
	/// <param name="row">待测点的 row 坐标。</param>
	/// <param name="column">待测点的 column 坐标。</param>
	/// <param name="row1">直线第一定点的 row 坐标。</param>
	/// <param name="column1">直线第一定点的 column 坐标。</param>
	/// <param name="row2">直线第二定点的 row 坐标。</param>
	/// <param name="column2">直线第二定点的 column 坐标。</param>
	/// <returns>垂距（像素）标量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>点到无限长直线的垂直距离（像素），对应原生算子 id 1314（与本类元组版 <see cref="DistancePl(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。老模板的英文 returns 误写作 "Distance between the points"，实为点到线距离。</para>
	///   <para><b>资源与坑</b>直线由两点外延定义，垂足不在线段内也照算；它量的是到"直线"而非"线段"的距离，目标是线段时请用 DistancePs；两点重合时行为未定义 [待实测]。</para>
	/// </remarks>
	public static double DistancePl(double row, double column, double row1, double column1, double row2, double column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1314);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, row1);
		JlNativeApi.StoreD(proc, 3, column1);
		JlNativeApi.StoreD(proc, 4, row2);
		JlNativeApi.StoreD(proc, 5, column2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>计算两点之间的欧氏距离（像素），支持逐点配对的元组输入。</summary>
	/// <param name="row1">第一个点的 row 坐标（y，向下为正，像素）。</param>
	/// <param name="column1">第一个点的 column 坐标（x，向右为正，像素）。</param>
	/// <param name="row2">第二个点的 row 坐标。</param>
	/// <param name="column2">第二个点的 column 坐标。</param>
	/// <returns>两点欧氏距离，按 DOUBLE 装载的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1315；四个输入元组钉住传入（Store+UnpinTuple），标量 double 重载直写无钉固定开销。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，经 Grep 核实本库内部没有任何调用者。结果是像素距离，不含物理尺度换算——世界单位需自己乘像素当量。</para>
	///   <para><b>与相邻算子的取舍</b>批量"中心到中心"量测用本方法最省事；点到线段用 DistancePs，点到无限长直线用 DistancePl。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row1 = new double[] { 0.0 };
	///   JlTuple column1 = new double[] { 0.0 };
	///   JlTuple row2 = new double[] { 3.0 };
	///   JlTuple column2 = new double[] { 4.0 };
	///   JlTuple dist = JlMisc.DistancePp(row1, column1, row2, column2);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlTuple DistancePp(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1315);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>计算两点之间的欧氏距离（像素标量版）。</summary>
	/// <param name="row1">第一个点的 row 坐标（y，向下为正，像素）。</param>
	/// <param name="column1">第一个点的 column 坐标（x，向右为正，像素）。</param>
	/// <param name="row2">第二个点的 row 坐标。</param>
	/// <param name="column2">第二个点的 column 坐标。</param>
	/// <returns>两点欧氏距离（像素）标量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>计算两点欧氏距离，对应原生算子 id 1315（与本类元组版 <see cref="DistancePp(JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>结果是像素距离，不含物理尺度换算——世界单位需自己乘像素当量。</para>
	/// </remarks>
	public static double DistancePp(double row1, double column1, double row2, double column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1315);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>查询 smooth_image 滤波器的系数与核宽度信息。</summary>
	/// <param name="filter">滤波器名称。Default: "deriche2"</param>
	/// <param name="alpha">滤波器参数：较小的值对应更强的平滑（gauss 时相反）。Default: 0.5</param>
	/// <param name="coeffs">输出系数：gauss 滤波器时为 1D 冲激响应"正半"的系数（INTEGER 装载）。</param>
	/// <returns>滤波器核宽度（约 size × size 像素）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>查询指定平滑滤波器对应的核宽度，并可选地拿到 gauss 滤波器的 1D 半幅系数，对应原生算子 id 1358；核宽度走返回值（int），coeffs 走 out 且按 INTEGER 装载。</para>
	///   <para><b>资源与坑</b>不同 <c>filter</c> 所支持的取值与 <c>alpha</c> 合法区间无法由代码判定 [待实测]；仅 gauss 才给出系数，其余滤波器 coeffs 行为以实测为准。</para>
	/// </remarks>
	public static int InfoSmooth(string filter, double alpha, out JlTuple coeffs)
	{
		IntPtr proc = JlNativeApi.PreCall(1358);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out coeffs);
		JlNativeApi.PostCall(proc, err);
		return intValue;
	}

	/// <summary>生成高斯噪声分布元组。</summary>
	/// <param name="sigma">高斯噪声分布的标准差。Default: 2.0</param>
	/// <returns>高斯噪声分布元组（DOUBLE 装载）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>生成一列服从高斯分布的噪声数据，返回按 DOUBLE 装载的新元组，对应原生算子 id 1382；本库不提供把该分布直接铺到图像上的算子，只返回数值序列。</para>
	///   <para><b>资源与坑</b>序列长度与采样由原生侧决定，无法由本文件代码判定 [待实测]。</para>
	/// </remarks>
	public static JlTuple GaussDistribution(double sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1382);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>生成椒盐噪声分布元组（多值版本）。</summary>
	/// <param name="percentSalt">盐（白噪声像素）占比元组（百分比）。Default: 5.0</param>
	/// <param name="percentPepper">椒（黑噪声像素）占比元组（百分比）。Default: 5.0</param>
	/// <returns>椒盐噪声分布元组（DOUBLE 装载）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按盐椒占比生成一列椒盐噪声数据，返回按 DOUBLE 装载的新元组，对应原生算子 id 1383；两个输入元组钉住传入，逐点配对可一次生成多份分布。</para>
	///   <para><b>资源与坑</b>序列长度与"盐椒"枚举方式由原生侧决定，无法由本文件代码判定 [待实测]。单组占比用下面的 double 重载，无钉固定元组开销。</para>
	/// </remarks>
	public static JlTuple SpDistribution(JlTuple percentSalt, JlTuple percentPepper)
	{
		IntPtr proc = JlNativeApi.PreCall(1383);
		JlNativeApi.Store(proc, 0, percentSalt);
		JlNativeApi.Store(proc, 1, percentPepper);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percentSalt);
		JlNativeApi.UnpinTuple(percentPepper);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>生成椒盐噪声分布元组（标量版）。</summary>
	/// <param name="percentSalt">盐（白噪声像素）占比（百分比）。Default: 5.0</param>
	/// <param name="percentPepper">椒（黑噪声像素）占比（百分比）。Default: 5.0</param>
	/// <returns>椒盐噪声分布元组（DOUBLE 装载）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按单组盐椒占比生成一列椒盐噪声数据，返回按 DOUBLE 装载的新元组，对应原生算子 id 1383（与本类元组版 <see cref="SpDistribution(JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>序列长度与"盐椒"枚举方式由原生侧决定，无法由本文件代码判定 [待实测]。</para>
	/// </remarks>
	public static JlTuple SpDistribution(double percentSalt, double percentPepper)
	{
		IntPtr proc = JlNativeApi.PreCall(1383);
		JlNativeApi.StoreD(proc, 0, percentSalt);
		JlNativeApi.StoreD(proc, 1, percentPepper);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}







	/// <summary>查询 edges_image 边沿滤波器的系数与核宽度信息。</summary>
	/// <param name="filter">边沿算子名称。Default: "lanser2"</param>
	/// <param name="mode">1D 边沿滤波（"edge"）或 1D 平滑滤波（"smooth"）。Default: "edge"</param>
	/// <param name="alpha">滤波器参数：较小的值对应更强平滑、细节更少（canny 时相反）。Default: 0.5</param>
	/// <param name="coeffs">输出系数（INTEGER 装载）：Canny 为 1D 冲激响应"正半"的系数，其余为对应非递归滤波器的系数。</param>
	/// <returns>滤波器核宽度（像素）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>查询指定边沿算子的滤波核宽度，并可取得对应系数，对应原生算子 id 1486；核宽度走返回值（int），coeffs 走 out 且按 INTEGER 装载。</para>
	///   <para><b>资源与坑</b><c>filter</c>/<c>mode</c> 支持的取值集合与 <c>alpha</c> 合法区间无法由代码判定 [待实测]。</para>
	/// </remarks>
	public static int InfoEdges(string filter, string mode, double alpha, out JlTuple coeffs)
	{
		IntPtr proc = JlNativeApi.PreCall(1486);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out coeffs);
		JlNativeApi.PostCall(proc, err);
		return intValue;
	}

	/// <summary>复制文件到新的位置。</summary>
	/// <param name="sourceFile">源文件路径。</param>
	/// <param name="destinationFile">目标文件路径。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把文件从源路径复制到目标路径，对应原生算子 id 1558；两个参数以字符串直写，无返回值。</para>
	///   <para><b>资源与坑</b>目标路径不可写、源不存在时会抛原生错误；目标已存在时是覆盖还是报错 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMisc.CopyFile("src.dat", "dst.dat");
	///   </code>
	/// </remarks>
	public static void CopyFile(string sourceFile, string destinationFile)
	{
		IntPtr proc = JlNativeApi.PreCall(1558);
		JlNativeApi.StoreS(proc, 0, sourceFile);
		JlNativeApi.StoreS(proc, 1, destinationFile);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>设置当前工作目录。</summary>
	/// <param name="dirName">要设为当前工作目录的目录名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把进程当前工作目录切换到指定目录，对应原生算子 id 1559；参数以字符串直写，无返回值。此后相对路径（如 <see cref="WriteTuple"/> 的文件名）以新目录为基准。</para>
	///   <para><b>资源与坑</b>目录不存在或不可访问时抛原生错误。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMisc.SetCurrentDir(@"C:\work");
	///   </code>
	/// </remarks>
	public static void SetCurrentDir(string dirName)
	{
		IntPtr proc = JlNativeApi.PreCall(1559);
		JlNativeApi.StoreS(proc, 0, dirName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>获取当前工作目录。</summary>
	/// <returns>当前工作目录名。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>返回进程当前工作目录，对应原生算子 id 1560；返回值以字符串装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   string dir = JlMisc.GetCurrentDir();
	///   </code>
	/// </remarks>
	public static string GetCurrentDir()
	{
		IntPtr proc = JlNativeApi.PreCall(1560);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		return stringValue;
	}

	/// <summary>删除一个空目录。</summary>
	/// <param name="dirName">要删除的目录名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>删除指定的空目录，对应原生算子 id 1561；参数以字符串直写，无返回值。</para>
	///   <para><b>资源与坑</b>仅能删除空目录，非空或不存在时会抛原生错误。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMisc.RemoveDir(@"C:\work\temp");
	///   </code>
	/// </remarks>
	public static void RemoveDir(string dirName)
	{
		IntPtr proc = JlNativeApi.PreCall(1561);
		JlNativeApi.StoreS(proc, 0, dirName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>创建目录。</summary>
	/// <param name="dirName">要创建的目录名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>创建指定目录，对应原生算子 id 1562；参数以字符串直写，无返回值。</para>
	///   <para><b>资源与坑</b>目录已存在或路径不可写时会抛原生错误；是否递归创建多级目录 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMisc.MakeDir(@"C:\work\data");
	///   </code>
	/// </remarks>
	public static void MakeDir(string dirName)
	{
		IntPtr proc = JlNativeApi.PreCall(1562);
		JlNativeApi.StoreS(proc, 0, dirName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>列出目录中的所有文件（选项元组版）。</summary>
	/// <param name="directory">要列出的目录名。</param>
	/// <param name="options">处理选项元组（如 "files"）。Default: "files"</param>
	/// <returns>找到的文件（及目录）名元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>列出指定目录下的条目名，返回按字符串装载的新元组，对应原生算子 id 1563；options 元组钉住传入。</para>
	///   <para><b>资源与坑</b>目录不存在或不可访问时会抛原生错误；单选项用下面的 string 重载更省事。</para>
	/// </remarks>
	public static JlTuple ListFiles(string directory, JlTuple options)
	{
		IntPtr proc = JlNativeApi.PreCall(1563);
		JlNativeApi.StoreS(proc, 0, directory);
		JlNativeApi.Store(proc, 1, options);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(options);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>列出目录中的所有文件（选项字符串版）。</summary>
	/// <param name="directory">要列出的目录名。</param>
	/// <param name="options">处理选项串（如 "files"）。Default: "files"</param>
	/// <returns>找到的文件（及目录）名元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>列出指定目录下的条目名，返回按字符串装载的新元组，对应原生算子 id 1563（与本类元组版 <see cref="ListFiles(string,JlTuple)"/> 同一算子）；本重载选项以字符串直写（StoreS），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>目录不存在或不可访问时会抛原生错误。</para>
	/// </remarks>
	public static JlTuple ListFiles(string directory, string options)
	{
		IntPtr proc = JlNativeApi.PreCall(1563);
		JlNativeApi.StoreS(proc, 0, directory);
		JlNativeApi.StoreS(proc, 1, options);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>删除一个文件。</summary>
	/// <param name="fileName">要删除的文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>删除指定的文件，对应原生算子 id 1564；参数以字符串直写，无返回值。</para>
	///   <para><b>资源与坑</b>文件不存在或不可删除时会抛原生错误。要判断存在性先看 <see cref="FileExists"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMisc.DeleteFile("data.dat");
	///   </code>
	/// </remarks>
	public static void DeleteFile(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1564);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>检查文件是否存在。</summary>
	/// <param name="fileName">要检查的文件名。Default: "/bin/cc"</param>
	/// <returns>布尔整数：非 0 表示文件存在，0 表示不存在。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>检查指定文件是否存在，返回布尔整数（int，LoadI 装载），对应原生算子 id 1565；路径不存在返回 0，而不是抛异常。</para>
	///   <para><b>资源与坑</b>对无效路径/无权限的表现以实测为准 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   int exists = JlMisc.FileExists("data.dat");
	///   </code>
	/// </remarks>
	public static int FileExists(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1565);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		return intValue;
	}

	/// <summary>从成组线段中挑出最长的 num 条。</summary>
	/// <param name="rowBeginIn">输入线段起点 row 坐标元组。</param>
	/// <param name="colBeginIn">输入线段起点 column 坐标元组。</param>
	/// <param name="rowEndIn">输入线段终点 row 坐标元组。</param>
	/// <param name="colEndIn">输入线段终点 column 坐标元组。</param>
	/// <param name="num">期望输出的线段条数（上限；整型直写）。Default: 10</param>
	/// <param name="rowBeginOut">输出线段起点 row 坐标（INTEGER 装载，端点取整）。</param>
	/// <param name="colBeginOut">输出线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="rowEndOut">输出线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="colEndOut">输出线段终点 column 坐标（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>从成组线段里挑出最长的 num 条，对应原生算子 id 1655。四组输出按<b>INTEGER</b>装载——端点坐标被取整，亚像素信息在这一步就丢了，这是它和相邻选择族共有的坑。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的线段筛选组，本库内部没有任何调用者。输入四个端点元组必须等长（一线一元素）；num 非正或大于线段数的行为 [待实测]；输出按长度排序还是保持输入顺序 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>按阈值条件（长度/角度区间）筛选用 <see cref="SelectLines(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,string,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；还要保留落选项时用 PartitionLines；本方法只回答"最长的 N 条"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple colBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple rowEndIn = new double[] { 3.0, 6.0 };
	///   JlTuple colEndIn = new double[] { 4.0, 6.0 };
	///   JlMisc.SelectLinesLongest(rowBeginIn, colBeginIn, rowEndIn, colEndIn, 1, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放；端点为浮点测量值时，优先用 LinePosition 自行按 length 排序以避免取整。</para>
	/// </remarks>
	public static void SelectLinesLongest(JlTuple rowBeginIn, JlTuple colBeginIn, JlTuple rowEndIn, JlTuple colEndIn, int num, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1655);
		JlNativeApi.Store(proc, 0, rowBeginIn);
		JlNativeApi.Store(proc, 1, colBeginIn);
		JlNativeApi.Store(proc, 2, rowEndIn);
		JlNativeApi.Store(proc, 3, colEndIn);
		JlNativeApi.StoreI(proc, 4, num);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBeginIn);
		JlNativeApi.UnpinTuple(colBeginIn);
		JlNativeApi.UnpinTuple(rowEndIn);
		JlNativeApi.UnpinTuple(colEndIn);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rowBeginOut);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out colBeginOut);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out rowEndOut);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out colEndOut);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>按特征区间把成组线段分成"满足/不满足"两堆（多特征元组版）。</summary>
	/// <param name="rowBeginIn">输入线段起点 row 坐标元组。</param>
	/// <param name="colBeginIn">输入线段起点 column 坐标元组。</param>
	/// <param name="rowEndIn">输入线段终点 row 坐标元组。</param>
	/// <param name="colEndIn">输入线段终点 column 坐标元组。</param>
	/// <param name="feature">特征名元组，如 "length"，第 i 个特征配第 i 组上下限。</param>
	/// <param name="operation">特征间组合方式（"and"/"or"）。</param>
	/// <param name="min">各特征下限，数值或特殊串 "min"。Default: "min"</param>
	/// <param name="max">各特征上限，数值或特殊串 "max"。Default: "max"</param>
	/// <param name="rowBeginOut">通过线段起点 row 坐标（INTEGER 装载，端点取整）。</param>
	/// <param name="colBeginOut">通过线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="rowEndOut">通过线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="colEndOut">通过线段终点 column 坐标（INTEGER 装载）。</param>
	/// <param name="failRowBOut">未通过线段起点 row 坐标（INTEGER 装载）。</param>
	/// <param name="failColBOut">未通过线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="failRowEOut">未通过线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="failColEOut">未通过线段终点 column 坐标（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>按特征区间把成组线段二分为"满足/不满足"两堆，对应原生算子 id 1656；八个输出全部按 INTEGER 装载（端点取整）。本重载的 feature/min/max 是元组，可一次给多个特征各配上下限，operation 决定特征间是 "and" 还是 "or" 组合。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的线段筛选组，本库内部没有任何调用者。feature、min、max 三者长度必须一致（第 i 个特征配第 i 组上下限）；min/max 除数值外还可填特殊串 <c>"min"</c>/<c>"max"</c> 表示"不设限"，可混用 [待实测]。两堆输出保持输入相对顺序 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>不需要落选项时用 SelectLines（少一半出参）；只要"最长的 N 条"用 SelectLinesLongest；单特征单区间可直接用下面的 string 重载，更直观。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple colBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple rowEndIn = new double[] { 3.0, 6.0 };
	///   JlTuple colEndIn = new double[] { 4.0, 6.0 };
	///   JlTuple feature = new string[] { "length" };
	///   JlTuple min = new double[] { 5.0 };
	///   JlTuple max = new double[] { 100.0 };
	///   JlMisc.PartitionLines(rowBeginIn, colBeginIn, rowEndIn, colEndIn, feature, "and", min, max, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut, out JlTuple failRowBOut, out JlTuple failColBOut, out JlTuple failRowEOut, out JlTuple failColEOut);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable；落选项也要分析时才值得付 8 个出参的成本。</para>
	/// </remarks>
	public static void PartitionLines(JlTuple rowBeginIn, JlTuple colBeginIn, JlTuple rowEndIn, JlTuple colEndIn, JlTuple feature, string operation, JlTuple min, JlTuple max, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut, out JlTuple failRowBOut, out JlTuple failColBOut, out JlTuple failRowEOut, out JlTuple failColEOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1656);
		JlNativeApi.Store(proc, 0, rowBeginIn);
		JlNativeApi.Store(proc, 1, colBeginIn);
		JlNativeApi.Store(proc, 2, rowEndIn);
		JlNativeApi.Store(proc, 3, colEndIn);
		JlNativeApi.Store(proc, 4, feature);
		JlNativeApi.StoreS(proc, 5, operation);
		JlNativeApi.Store(proc, 6, min);
		JlNativeApi.Store(proc, 7, max);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBeginIn);
		JlNativeApi.UnpinTuple(colBeginIn);
		JlNativeApi.UnpinTuple(rowEndIn);
		JlNativeApi.UnpinTuple(colEndIn);
		JlNativeApi.UnpinTuple(feature);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rowBeginOut);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out colBeginOut);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out rowEndOut);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out colEndOut);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out failRowBOut);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out failColBOut);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out failRowEOut);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.INTEGER, err, out failColEOut);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>按单个特征名与上下限把线段分成"满足/不满足"两堆（字符串版）。</summary>
	/// <param name="rowBeginIn">输入线段起点 row 坐标元组。</param>
	/// <param name="colBeginIn">输入线段起点 column 坐标元组。</param>
	/// <param name="rowEndIn">输入线段终点 row 坐标元组。</param>
	/// <param name="colEndIn">输入线段终点 column 坐标元组。</param>
	/// <param name="feature">单个特征名串，如 "length"。</param>
	/// <param name="operation">组合方式（"and"/"or"；单特征时不生效）。</param>
	/// <param name="min">下限串（数值串或特殊串 "min"）。Default: "min"</param>
	/// <param name="max">上限串（数值串或特殊串 "max"）。Default: "max"</param>
	/// <param name="rowBeginOut">通过线段起点 row 坐标（INTEGER 装载，端点取整）。</param>
	/// <param name="colBeginOut">通过线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="rowEndOut">通过线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="colEndOut">通过线段终点 column 坐标（INTEGER 装载）。</param>
	/// <param name="failRowBOut">未通过线段起点 row 坐标（INTEGER 装载）。</param>
	/// <param name="failColBOut">未通过线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="failRowEOut">未通过线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="failColEOut">未通过线段终点 column 坐标（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组版 <c>PartitionLines</c> 同一原生算子（id 1656），区别仅在参数以字符串直写（StoreS，不钉固定元组）：单个特征名 + 单组上下限。输出同样按 INTEGER 装载，端点取整。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的线段筛选组，本库内部没有任何调用者。feature 的合法特征名集合（length/angle 等）无法由代码判定 [待实测]；min/max 传特殊串 <c>"min"</c>/<c>"max"</c> 表示该侧不设限。</para>
	///   <para><b>与相邻算子的取舍</b>多特征组合必须用元组重载（本重载一次只问一个条件）；只要通过堆、不需要落选项时用 SelectLines。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple colBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple rowEndIn = new double[] { 3.0, 6.0 };
	///   JlTuple colEndIn = new double[] { 4.0, 6.0 };
	///   JlMisc.PartitionLines(rowBeginIn, colBeginIn, rowEndIn, colEndIn, "length", "and", "min", "max", out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut, out JlTuple failRowBOut, out JlTuple failColBOut, out JlTuple failRowEOut, out JlTuple failColEOut);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void PartitionLines(JlTuple rowBeginIn, JlTuple colBeginIn, JlTuple rowEndIn, JlTuple colEndIn, string feature, string operation, string min, string max, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut, out JlTuple failRowBOut, out JlTuple failColBOut, out JlTuple failRowEOut, out JlTuple failColEOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1656);
		JlNativeApi.Store(proc, 0, rowBeginIn);
		JlNativeApi.Store(proc, 1, colBeginIn);
		JlNativeApi.Store(proc, 2, rowEndIn);
		JlNativeApi.Store(proc, 3, colEndIn);
		JlNativeApi.StoreS(proc, 4, feature);
		JlNativeApi.StoreS(proc, 5, operation);
		JlNativeApi.StoreS(proc, 6, min);
		JlNativeApi.StoreS(proc, 7, max);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBeginIn);
		JlNativeApi.UnpinTuple(colBeginIn);
		JlNativeApi.UnpinTuple(rowEndIn);
		JlNativeApi.UnpinTuple(colEndIn);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rowBeginOut);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out colBeginOut);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out rowEndOut);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out colEndOut);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out failRowBOut);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out failColBOut);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out failRowEOut);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.INTEGER, err, out failColEOut);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>按特征区间筛选线段，只返回通过堆（多特征元组版）。</summary>
	/// <param name="rowBeginIn">输入线段起点 row 坐标元组。</param>
	/// <param name="colBeginIn">输入线段起点 column 坐标元组。</param>
	/// <param name="rowEndIn">输入线段终点 row 坐标元组。</param>
	/// <param name="colEndIn">输入线段终点 column 坐标元组。</param>
	/// <param name="feature">特征名元组，如 "length"。Default: "length"</param>
	/// <param name="operation">特征间组合方式。Default: "and"</param>
	/// <param name="min">各特征下限，数值或特殊串 "min"。Default: "min"</param>
	/// <param name="max">各特征上限，数值或特殊串 "max"。Default: "max"</param>
	/// <param name="rowBeginOut">通过线段起点 row 坐标（INTEGER 装载，端点取整）。</param>
	/// <param name="colBeginOut">通过线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="rowEndOut">通过线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="colEndOut">通过线段终点 column 坐标（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>按特征区间筛选线段、只回通过堆，对应原生算子 id 1657；四个输出按 INTEGER 装载（端点取整）。本重载 feature/min/max 为元组，可多特征各配上下限，operation 给特征间 "and"/"or" 组合。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的线段筛选组，本库内部没有任何调用者。四个端点输入元组必须等长；feature 合法名集合与 min/max 特殊串行为同 PartitionLines，无法由代码判定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>还要落选项用 PartitionLines；纯按长度取前 N 用 SelectLinesLongest；单条件筛选用下面的 string 重载（无钉固开销）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple colBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple rowEndIn = new double[] { 3.0, 6.0 };
	///   JlTuple colEndIn = new double[] { 4.0, 6.0 };
	///   JlTuple feature = new string[] { "length" };
	///   JlTuple min = new double[] { 4.0 };
	///   JlTuple max = new string[] { "max" };
	///   JlMisc.SelectLines(rowBeginIn, colBeginIn, rowEndIn, colEndIn, feature, "and", min, max, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void SelectLines(JlTuple rowBeginIn, JlTuple colBeginIn, JlTuple rowEndIn, JlTuple colEndIn, JlTuple feature, string operation, JlTuple min, JlTuple max, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1657);
		JlNativeApi.Store(proc, 0, rowBeginIn);
		JlNativeApi.Store(proc, 1, colBeginIn);
		JlNativeApi.Store(proc, 2, rowEndIn);
		JlNativeApi.Store(proc, 3, colEndIn);
		JlNativeApi.Store(proc, 4, feature);
		JlNativeApi.StoreS(proc, 5, operation);
		JlNativeApi.Store(proc, 6, min);
		JlNativeApi.Store(proc, 7, max);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBeginIn);
		JlNativeApi.UnpinTuple(colBeginIn);
		JlNativeApi.UnpinTuple(rowEndIn);
		JlNativeApi.UnpinTuple(colEndIn);
		JlNativeApi.UnpinTuple(feature);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rowBeginOut);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out colBeginOut);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out rowEndOut);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out colEndOut);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>按单个特征名与上下限筛选线段，只返回通过堆（字符串版）。</summary>
	/// <param name="rowBeginIn">输入线段起点 row 坐标元组。</param>
	/// <param name="colBeginIn">输入线段起点 column 坐标元组。</param>
	/// <param name="rowEndIn">输入线段终点 row 坐标元组。</param>
	/// <param name="colEndIn">输入线段终点 column 坐标元组。</param>
	/// <param name="feature">单个特征名串。Default: "length"</param>
	/// <param name="operation">组合方式（单特征时不生效）。Default: "and"</param>
	/// <param name="min">下限串（数值串或特殊串 "min"）。Default: "min"</param>
	/// <param name="max">上限串（数值串或特殊串 "max"）。Default: "max"</param>
	/// <param name="rowBeginOut">通过线段起点 row 坐标（INTEGER 装载，端点取整）。</param>
	/// <param name="colBeginOut">通过线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="rowEndOut">通过线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="colEndOut">通过线段终点 column 坐标（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组版同一原生算子（id 1657）：单个特征名、单组上下限，全部以字符串直写（StoreS，无钉固定元组开销）；四个输出仍按 INTEGER 装载（端点取整）。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的线段筛选组，本库内部没有任何调用者。<c>"min"</c>/<c>"max"</c> 作为特殊串表示该侧不设限；feature 合法名集合无法由代码判定 [待实测]。若以字面量实参调用，编译器按重载解析会选中本重载（string 恒等转换优先于隐式 JlTuple 转换）。</para>
	///   <para><b>与相邻算子的取舍</b>多特征组合必须用元组重载；还要落选项用 PartitionLines；取最长 N 条用 SelectLinesLongest。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple colBeginIn = new double[] { 0.0, 5.0 };
	///   JlTuple rowEndIn = new double[] { 3.0, 6.0 };
	///   JlTuple colEndIn = new double[] { 4.0, 6.0 };
	///   JlMisc.SelectLines(rowBeginIn, colBeginIn, rowEndIn, colEndIn, "length", "and", "min", "max", out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void SelectLines(JlTuple rowBeginIn, JlTuple colBeginIn, JlTuple rowEndIn, JlTuple colEndIn, string feature, string operation, string min, string max, out JlTuple rowBeginOut, out JlTuple colBeginOut, out JlTuple rowEndOut, out JlTuple colEndOut)
	{
		IntPtr proc = JlNativeApi.PreCall(1657);
		JlNativeApi.Store(proc, 0, rowBeginIn);
		JlNativeApi.Store(proc, 1, colBeginIn);
		JlNativeApi.Store(proc, 2, rowEndIn);
		JlNativeApi.Store(proc, 3, colEndIn);
		JlNativeApi.StoreS(proc, 4, feature);
		JlNativeApi.StoreS(proc, 5, operation);
		JlNativeApi.StoreS(proc, 6, min);
		JlNativeApi.StoreS(proc, 7, max);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBeginIn);
		JlNativeApi.UnpinTuple(colBeginIn);
		JlNativeApi.UnpinTuple(rowEndIn);
		JlNativeApi.UnpinTuple(colEndIn);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out rowBeginOut);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out colBeginOut);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out rowEndOut);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out colEndOut);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>由线段两端点求中点（重心）、欧氏长度与方向角。</summary>
	/// <param name="rowBegin">各线段起点 row 坐标元组。</param>
	/// <param name="colBegin">各线段起点 column 坐标元组。</param>
	/// <param name="rowEnd">各线段终点 row 坐标元组。</param>
	/// <param name="colEnd">各线段终点 column 坐标元组。</param>
	/// <param name="rowCenter">中点 row 坐标（DOUBLE 装载）。</param>
	/// <param name="colCenter">中点 column 坐标（DOUBLE 装载）。</param>
	/// <param name="length">线段欧氏长度（DOUBLE 装载，像素）。</param>
	/// <param name="phi">方向角（DOUBLE 装载），约定 [待实测]。</param>
	/// <remarks>
	///   <para><b>功能说明</b>由线段两端点算中点（重心）、欧氏长度与方向角，对应原生算子 id 1658；四个输出全部按 DOUBLE 装载，输入元组钉住传入。注意"重心"就是两端点平均，不是任何像素加权结果。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组（与 LineOrientation 功能重叠，见取舍），本库内部没有任何调用者。length 单位像素；phi 的方向约定（取值区间、正方向）无法由代码判定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要方向角时用 <see cref="LineOrientation(JlTuple,JlTuple,JlTuple,JlTuple)"/>（单次调用、少两个出参）；两端点重合时 length=0、phi 未定义 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBegin = new double[] { 0.0 };
	///   JlTuple colBegin = new double[] { 0.0 };
	///   JlTuple rowEnd = new double[] { 6.0 };
	///   JlTuple colEnd = new double[] { 8.0 };
	///   JlMisc.LinePosition(rowBegin, colBegin, rowEnd, colEnd, out JlTuple rowCenter, out JlTuple colCenter, out JlTuple length, out JlTuple phi);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void LinePosition(JlTuple rowBegin, JlTuple colBegin, JlTuple rowEnd, JlTuple colEnd, out JlTuple rowCenter, out JlTuple colCenter, out JlTuple length, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1658);
		JlNativeApi.Store(proc, 0, rowBegin);
		JlNativeApi.Store(proc, 1, colBegin);
		JlNativeApi.Store(proc, 2, rowEnd);
		JlNativeApi.Store(proc, 3, colEnd);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBegin);
		JlNativeApi.UnpinTuple(colBegin);
		JlNativeApi.UnpinTuple(rowEnd);
		JlNativeApi.UnpinTuple(colEnd);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowCenter);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out colCenter);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out length);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>由线段两端点求中点、欧氏长度与方向角（标量版）。</summary>
	/// <param name="rowBegin">线段起点 row 坐标。</param>
	/// <param name="colBegin">线段起点 column 坐标。</param>
	/// <param name="rowEnd">线段终点 row 坐标。</param>
	/// <param name="colEnd">线段终点 column 坐标。</param>
	/// <param name="rowCenter">中点 row 坐标。</param>
	/// <param name="colCenter">中点 column 坐标。</param>
	/// <param name="length">线段欧氏长度（像素）。</param>
	/// <param name="phi">方向角，约定 [待实测]。</param>
	/// <remarks>
	///   <para><b>功能说明</b>由线段两端点算中点（两端点平均）、欧氏长度与方向角，对应原生算子 id 1658（与本类元组版 <see cref="LinePosition(JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>length 单位像素；phi 方向约定无法由代码判定 [待实测]；两端点重合时 length=0、phi 未定义。</para>
	/// </remarks>
	public static void LinePosition(double rowBegin, double colBegin, double rowEnd, double colEnd, out double rowCenter, out double colCenter, out double length, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1658);
		JlNativeApi.StoreD(proc, 0, rowBegin);
		JlNativeApi.StoreD(proc, 1, colBegin);
		JlNativeApi.StoreD(proc, 2, rowEnd);
		JlNativeApi.StoreD(proc, 3, colEnd);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out rowCenter);
		err = JlNativeApi.LoadD(proc, 1, err, out colCenter);
		err = JlNativeApi.LoadD(proc, 2, err, out length);
		err = JlNativeApi.LoadD(proc, 3, err, out phi);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>由线段两端点求方向角。</summary>
	/// <param name="rowBegin">各线段起点 row 坐标元组。</param>
	/// <param name="colBegin">各线段起点 column 坐标元组。</param>
	/// <param name="rowEnd">各线段终点 row 坐标元组。</param>
	/// <param name="colEnd">各线段终点 column 坐标元组。</param>
	/// <returns>方向角元组（DOUBLE 装载，弧度制），取值区间约定 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>由线段两端点算方向角，对应原生算子 id 1659；输出按 DOUBLE 装载，输入元组钉住传入。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 2D 点线几何组，本库内部没有任何调用者。弧度制；取值区间与正方向（row 向下为正带来的翻转）无法由代码判定 [待实测]。两点重合时结果未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>与 <see cref="LinePosition(JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 底层是同族功能：要中点/长度选 LinePosition，只要角度用本方法；对任意两线的夹角用 AngleLl。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rowBegin = new double[] { 0.0 };
	///   JlTuple colBegin = new double[] { 0.0 };
	///   JlTuple rowEnd = new double[] { 1.0 };
	///   JlTuple colEnd = new double[] { 1.0 };
	///   JlTuple phi = JlMisc.LineOrientation(rowBegin, colBegin, rowEnd, colEnd);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlTuple LineOrientation(JlTuple rowBegin, JlTuple colBegin, JlTuple rowEnd, JlTuple colEnd)
	{
		IntPtr proc = JlNativeApi.PreCall(1659);
		JlNativeApi.Store(proc, 0, rowBegin);
		JlNativeApi.Store(proc, 1, colBegin);
		JlNativeApi.Store(proc, 2, rowEnd);
		JlNativeApi.Store(proc, 3, colEnd);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBegin);
		JlNativeApi.UnpinTuple(colBegin);
		JlNativeApi.UnpinTuple(rowEnd);
		JlNativeApi.UnpinTuple(colEnd);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>由线段两端点求方向角（标量版）。</summary>
	/// <param name="rowBegin">线段起点 row 坐标。</param>
	/// <param name="colBegin">线段起点 column 坐标。</param>
	/// <param name="rowEnd">线段终点 row 坐标。</param>
	/// <param name="colEnd">线段终点 column 坐标。</param>
	/// <returns>方向角（弧度）标量，取值区间约定 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>由线段两端点算方向角，对应原生算子 id 1659（与本类元组版 <see cref="LineOrientation(JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>弧度制；取值区间与正方向（row 向下为正带来的翻转）无法由代码判定 [待实测]；两点重合时结果未定义 [待实测]。</para>
	/// </remarks>
	public static double LineOrientation(double rowBegin, double colBegin, double rowEnd, double colEnd)
	{
		IntPtr proc = JlNativeApi.PreCall(1659);
		JlNativeApi.StoreD(proc, 0, rowBegin);
		JlNativeApi.StoreD(proc, 1, colBegin);
		JlNativeApi.StoreD(proc, 2, rowEnd);
		JlNativeApi.StoreD(proc, 3, colEnd);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>用圆弧与直线段逼近一条轮廓链（基础版，只给轮廓点）。</summary>
	/// <param name="row">轮廓点 row 坐标元组。</param>
	/// <param name="column">轮廓点 column 坐标元组。</param>
	/// <param name="arcCenterRow">圆弧圆心的 row 坐标（INTEGER 装载）。</param>
	/// <param name="arcCenterCol">圆弧圆心的 column 坐标（INTEGER 装载）。</param>
	/// <param name="arcAngle">圆弧张角（DOUBLE 装载）。</param>
	/// <param name="arcBeginRow">圆弧起点 row 坐标（INTEGER 装载）。</param>
	/// <param name="arcBeginCol">圆弧起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="lineBeginRow">直线段起点 row 坐标（INTEGER 装载）。</param>
	/// <param name="lineBeginCol">直线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="lineEndRow">直线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="lineEndCol">直线段终点 column 坐标（INTEGER 装载）。</param>
	/// <param name="order">分割顺序：线段为 0、圆弧段为 1（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把一整条轮廓点链用圆弧与直线段逐段逼近并输出各部分几何，对应原生算子 id 1660。输出除 arcAngle 按 DOUBLE 装载外，其余均按 INTEGER 装载（坐标被取整）。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的轮廓逼近组，本库内部没有任何调用者；row/column 必须等长（一对点描述一个轮廓顶点）。逼近的阈值/容许误差不可在此调整，需要调参用 <see cref="ApproxChain"/>。</para>
	///   <para><b>资源与坑</b>各部分输出通过不同出参返回，编号下标之间并不一定对齐到同一条边，使用时需结合 order 判读 [待实测]。</para>
	/// </remarks>
	public static void ApproxChainSimple(JlTuple row, JlTuple column, out JlTuple arcCenterRow, out JlTuple arcCenterCol, out JlTuple arcAngle, out JlTuple arcBeginRow, out JlTuple arcBeginCol, out JlTuple lineBeginRow, out JlTuple lineBeginCol, out JlTuple lineEndRow, out JlTuple lineEndCol, out JlTuple order)
	{
		IntPtr proc = JlNativeApi.PreCall(1660);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
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
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out arcCenterRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out arcCenterCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out arcAngle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out arcBeginRow);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out arcBeginCol);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out lineBeginRow);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out lineBeginCol);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.INTEGER, err, out lineEndRow);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.INTEGER, err, out lineEndCol);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.INTEGER, err, out order);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>用圆弧与直线段逼近一条轮廓链（可调参数版）。</summary>
	/// <param name="row">轮廓点 row 坐标元组。Default: 32</param>
	/// <param name="column">轮廓点 column 坐标元组。Default: 32</param>
	/// <param name="minWidthCoord">坐标平滑用的 Gauss 算子最小宽度（须大于 0.4）。Default: 0.5</param>
	/// <param name="maxWidthCoord">坐标平滑用的 Gauss 算子最大宽度。Default: 2.4</param>
	/// <param name="threshStart">接受一个拐点的曲率阈值下限（相对当前最大曲率）。Default: 0.3</param>
	/// <param name="threshEnd">接受一个拐点的曲率阈值上限。Default: 0.9</param>
	/// <param name="threshStep">阈值递增步长。Default: 0.2</param>
	/// <param name="minWidthSmooth">平滑曲率函数所用 Gauss 算子的最小宽度（须大于 0.4）。Default: 0.5</param>
	/// <param name="maxWidthSmooth">平滑曲率函数所用 Gauss 算子的最大宽度。Default: 2.4</param>
	/// <param name="minWidthCurve">用于曲率判定的曲线区域最小宽度。Default: 2</param>
	/// <param name="maxWidthCurve">用于曲率判定的曲线区域最大宽度。Default: 12</param>
	/// <param name="weight1">逼近精度的权重系数。Default: 1.0</param>
	/// <param name="weight2">大分段的权重系数。Default: 1.0</param>
	/// <param name="weight3">小分段的权重系数。Default: 1.0</param>
	/// <param name="arcCenterRow">圆弧圆心的 row 坐标（INTEGER 装载）。</param>
	/// <param name="arcCenterCol">圆弧圆心的 column 坐标（INTEGER 装载）。</param>
	/// <param name="arcAngle">圆弧张角（DOUBLE 装载）。</param>
	/// <param name="arcBeginRow">圆弧起点 row 坐标（INTEGER 装载）。</param>
	/// <param name="arcBeginCol">圆弧起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="lineBeginRow">直线段起点 row 坐标（INTEGER 装载）。</param>
	/// <param name="lineBeginCol">直线段起点 column 坐标（INTEGER 装载）。</param>
	/// <param name="lineEndRow">直线段终点 row 坐标（INTEGER 装载）。</param>
	/// <param name="lineEndCol">直线段终点 column 坐标（INTEGER 装载）。</param>
	/// <param name="order">分割顺序：线段为 0、圆弧段为 1（INTEGER 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>按一组平滑/曲率/权重参数把轮廓点链用圆弧与直线段逐段逼近并输出各部分几何，对应原生算子 id 1661。输出除 arcAngle 按 DOUBLE 装载外，其余均按 INTEGER 装载（坐标被取整）。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的轮廓逼近组，本库内部没有任何调用者；row/column 须等长。诸阈值/宽度/权重的合法取值范围与相互约束无法由代码判定 [待实测]，超出合理区间时行为以实测为准。</para>
	///   <para><b>资源与坑</b>各部分输出通过不同出参返回，编号下标之间并不一定对齐到同一条边，使用时需结合 order 判读 [待实测]；不需要精细调参时用更简单的 <see cref="ApproxChainSimple"/>。</para>
	/// </remarks>
	public static void ApproxChain(JlTuple row, JlTuple column, double minWidthCoord, double maxWidthCoord, double threshStart, double threshEnd, double threshStep, double minWidthSmooth, double maxWidthSmooth, int minWidthCurve, int maxWidthCurve, double weight1, double weight2, double weight3, out JlTuple arcCenterRow, out JlTuple arcCenterCol, out JlTuple arcAngle, out JlTuple arcBeginRow, out JlTuple arcBeginCol, out JlTuple lineBeginRow, out JlTuple lineBeginCol, out JlTuple lineEndRow, out JlTuple lineEndCol, out JlTuple order)
	{
		IntPtr proc = JlNativeApi.PreCall(1661);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, minWidthCoord);
		JlNativeApi.StoreD(proc, 3, maxWidthCoord);
		JlNativeApi.StoreD(proc, 4, threshStart);
		JlNativeApi.StoreD(proc, 5, threshEnd);
		JlNativeApi.StoreD(proc, 6, threshStep);
		JlNativeApi.StoreD(proc, 7, minWidthSmooth);
		JlNativeApi.StoreD(proc, 8, maxWidthSmooth);
		JlNativeApi.StoreI(proc, 9, minWidthCurve);
		JlNativeApi.StoreI(proc, 10, maxWidthCurve);
		JlNativeApi.StoreD(proc, 11, weight1);
		JlNativeApi.StoreD(proc, 12, weight2);
		JlNativeApi.StoreD(proc, 13, weight3);
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
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out arcCenterRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out arcCenterCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out arcAngle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out arcBeginRow);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out arcBeginCol);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out lineBeginRow);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out lineBeginCol);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.INTEGER, err, out lineEndRow);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.INTEGER, err, out lineEndCol);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.INTEGER, err, out order);
		JlNativeApi.PostCall(proc, err);
	}





	/// <summary>计算 3D 点到"由线上两点定义的直线"的最短距离。</summary>
	/// <param name="pointX">待测点 x 坐标元组。</param>
	/// <param name="pointY">待测点 y 坐标元组。</param>
	/// <param name="pointZ">待测点 z 坐标元组。</param>
	/// <param name="point1X">直线第一定点 x 坐标元组。</param>
	/// <param name="point1Y">直线第一定点 y 坐标元组。</param>
	/// <param name="point1Z">直线第一定点 z 坐标元组。</param>
	/// <param name="point2X">直线第二定点 x 坐标元组。</param>
	/// <param name="point2Y">直线第二定点 y 坐标元组。</param>
	/// <param name="point2Z">直线第二定点 z 坐标元组。</param>
	/// <returns>最短距离元组（DOUBLE 装载，单位与输入坐标一致）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>3D 点到"由线上两点定义的直线"的最短距离，对应原生算子 id 2140；输出按 DOUBLE 装载，九个输入元组钉住传入、逐点配对可算多组。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 3D 点线几何组；经 Grep 核实本库内部没有任何调用者。本库已不提供 3D 类型族（<c>JlHomMat3D</c> 等已删除），但本方法为纯数值计算不受影响；坐标单位由调用方自定（与 pose/模型一致，通常为米）。直线按两点外延，非线段 [待实测：是否支持线段模式]。</para>
	///   <para><b>与相邻算子的取舍</b>线以 Plücker 坐标（方向+力矩向量）持有时用 <see cref="DistancePointPlueckerLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>，可免去每次存两个点。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple pointX = new double[] { 1.0 };
	///   JlTuple pointY = new double[] { 1.0 };
	///   JlTuple pointZ = new double[] { 0.0 };
	///   JlTuple point1X = new double[] { 0.0 };
	///   JlTuple point1Y = new double[] { 0.0 };
	///   JlTuple point1Z = new double[] { 0.0 };
	///   JlTuple point2X = new double[] { 0.0 };
	///   JlTuple point2Y = new double[] { 1.0 };
	///   JlTuple point2Z = new double[] { 0.0 };
	///   JlTuple dist = JlMisc.DistancePointLine(pointX, pointY, pointZ, point1X, point1Y, point1Z, point2X, point2Y, point2Z);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable；线两点重合时结果未定义 [待实测]。</para>
	/// </remarks>
	public static JlTuple DistancePointLine(JlTuple pointX, JlTuple pointY, JlTuple pointZ, JlTuple point1X, JlTuple point1Y, JlTuple point1Z, JlTuple point2X, JlTuple point2Y, JlTuple point2Z)
	{
		IntPtr proc = JlNativeApi.PreCall(2140);
		JlNativeApi.Store(proc, 0, pointX);
		JlNativeApi.Store(proc, 1, pointY);
		JlNativeApi.Store(proc, 2, pointZ);
		JlNativeApi.Store(proc, 3, point1X);
		JlNativeApi.Store(proc, 4, point1Y);
		JlNativeApi.Store(proc, 5, point1Z);
		JlNativeApi.Store(proc, 6, point2X);
		JlNativeApi.Store(proc, 7, point2Y);
		JlNativeApi.Store(proc, 8, point2Z);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(pointX);
		JlNativeApi.UnpinTuple(pointY);
		JlNativeApi.UnpinTuple(pointZ);
		JlNativeApi.UnpinTuple(point1X);
		JlNativeApi.UnpinTuple(point1Y);
		JlNativeApi.UnpinTuple(point1Z);
		JlNativeApi.UnpinTuple(point2X);
		JlNativeApi.UnpinTuple(point2Y);
		JlNativeApi.UnpinTuple(point2Z);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>计算 3D 点到"由两点定义的直线"的最短距离（标量版）。</summary>
	/// <param name="pointX">待测点 x 坐标。</param>
	/// <param name="pointY">待测点 y 坐标。</param>
	/// <param name="pointZ">待测点 z 坐标。</param>
	/// <param name="point1X">直线第一定点 x 坐标。</param>
	/// <param name="point1Y">直线第一定点 y 坐标。</param>
	/// <param name="point1Z">直线第一定点 z 坐标。</param>
	/// <param name="point2X">直线第二定点 x 坐标。</param>
	/// <param name="point2Y">直线第二定点 y 坐标。</param>
	/// <param name="point2Z">直线第二定点 z 坐标。</param>
	/// <returns>最短距离标量（单位与输入坐标一致）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>3D 点到"由线上两点定义的直线"的最短距离，对应原生算子 id 2140（与本类元组版 <see cref="DistancePointLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>直线按两点外延（非线段）；坐标单位由调用方自定；线两点重合时结果未定义 [待实测]。</para>
	/// </remarks>
	public static double DistancePointLine(double pointX, double pointY, double pointZ, double point1X, double point1Y, double point1Z, double point2X, double point2Y, double point2Z)
	{
		IntPtr proc = JlNativeApi.PreCall(2140);
		JlNativeApi.StoreD(proc, 0, pointX);
		JlNativeApi.StoreD(proc, 1, pointY);
		JlNativeApi.StoreD(proc, 2, pointZ);
		JlNativeApi.StoreD(proc, 3, point1X);
		JlNativeApi.StoreD(proc, 4, point1Y);
		JlNativeApi.StoreD(proc, 5, point1Z);
		JlNativeApi.StoreD(proc, 6, point2X);
		JlNativeApi.StoreD(proc, 7, point2Y);
		JlNativeApi.StoreD(proc, 8, point2Z);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>计算 3D 点到 Plücker 坐标直线的最短距离。</summary>
	/// <param name="pointX">待测点 x 坐标元组。</param>
	/// <param name="pointY">待测点 y 坐标元组。</param>
	/// <param name="pointZ">待测点 z 坐标元组。</param>
	/// <param name="lineDirectionX">直线方向向量 x 分量元组。</param>
	/// <param name="lineDirectionY">直线方向向量 y 分量元组。</param>
	/// <param name="lineDirectionZ">直线方向向量 z 分量元组。</param>
	/// <param name="lineMomentX">直线力矩向量 x 分量元组。</param>
	/// <param name="lineMomentY">直线力矩向量 y 分量元组。</param>
	/// <param name="lineMomentZ">直线力矩向量 z 分量元组。</param>
	/// <returns>最短距离元组（DOUBLE 装载，单位与输入坐标一致）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>3D 点到 Plücker 坐标表示的直线的最短距离：线由方向向量 + 力矩向量共 6 个分量给出，对应原生算子 id 2141；输出按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 3D 点线几何组，本库内部没有任何调用者；本库已不提供 3D 类型族但本方法纯数值。力矩向量与方向向量的耦合关系（moment = point × direction，及方向向量是否须单位化）无法由代码判定 [待实测]，传入不满足 Plücker 约束的向量时结果不可信。</para>
	///   <para><b>与相邻算子的取舍</b>手里是"线上两点"用 <see cref="DistancePointLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>；要先把 Plücker 拆开再算用 <see cref="PlueckerLineToPointDirection(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>，但绕这一圈不如直接调本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple pointX = new double[] { 1.0 };
	///   JlTuple pointY = new double[] { 1.0 };
	///   JlTuple pointZ = new double[] { 0.0 };
	///   JlTuple lineDirectionX = new double[] { 0.0 };
	///   JlTuple lineDirectionY = new double[] { 1.0 };
	///   JlTuple lineDirectionZ = new double[] { 0.0 };
	///   JlTuple lineMomentX = new double[] { 0.0 };
	///   JlTuple lineMomentY = new double[] { 0.0 };
	///   JlTuple lineMomentZ = new double[] { 0.0 };
	///   JlTuple dist = JlMisc.DistancePointPlueckerLine(pointX, pointY, pointZ, lineDirectionX, lineDirectionY, lineDirectionZ, lineMomentX, lineMomentY, lineMomentZ);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static JlTuple DistancePointPlueckerLine(JlTuple pointX, JlTuple pointY, JlTuple pointZ, JlTuple lineDirectionX, JlTuple lineDirectionY, JlTuple lineDirectionZ, JlTuple lineMomentX, JlTuple lineMomentY, JlTuple lineMomentZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2141);
		JlNativeApi.Store(proc, 0, pointX);
		JlNativeApi.Store(proc, 1, pointY);
		JlNativeApi.Store(proc, 2, pointZ);
		JlNativeApi.Store(proc, 3, lineDirectionX);
		JlNativeApi.Store(proc, 4, lineDirectionY);
		JlNativeApi.Store(proc, 5, lineDirectionZ);
		JlNativeApi.Store(proc, 6, lineMomentX);
		JlNativeApi.Store(proc, 7, lineMomentY);
		JlNativeApi.Store(proc, 8, lineMomentZ);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(pointX);
		JlNativeApi.UnpinTuple(pointY);
		JlNativeApi.UnpinTuple(pointZ);
		JlNativeApi.UnpinTuple(lineDirectionX);
		JlNativeApi.UnpinTuple(lineDirectionY);
		JlNativeApi.UnpinTuple(lineDirectionZ);
		JlNativeApi.UnpinTuple(lineMomentX);
		JlNativeApi.UnpinTuple(lineMomentY);
		JlNativeApi.UnpinTuple(lineMomentZ);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>计算 3D 点到 Plücker 坐标直线的最短距离（标量版）。</summary>
	/// <param name="pointX">待测点 x 坐标。</param>
	/// <param name="pointY">待测点 y 坐标。</param>
	/// <param name="pointZ">待测点 z 坐标。</param>
	/// <param name="lineDirectionX">直线方向向量 x 分量。</param>
	/// <param name="lineDirectionY">直线方向向量 y 分量。</param>
	/// <param name="lineDirectionZ">直线方向向量 z 分量。</param>
	/// <param name="lineMomentX">直线力矩向量 x 分量。</param>
	/// <param name="lineMomentY">直线力矩向量 y 分量。</param>
	/// <param name="lineMomentZ">直线力矩向量 z 分量。</param>
	/// <returns>最短距离标量（单位与输入坐标一致）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>3D 点到 Plücker 坐标表示的直线的最短距离：线由方向向量 + 力矩向量共 6 个分量给出，对应原生算子 id 2141（与本类元组版 <see cref="DistancePointPlueckerLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>力矩向量与方向向量的耦合关系（moment = point × direction）无法由代码判定 [待实测]，传入不满足 Plücker 约束的向量时结果不可信。</para>
	/// </remarks>
	public static double DistancePointPlueckerLine(double pointX, double pointY, double pointZ, double lineDirectionX, double lineDirectionY, double lineDirectionZ, double lineMomentX, double lineMomentY, double lineMomentZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2141);
		JlNativeApi.StoreD(proc, 0, pointX);
		JlNativeApi.StoreD(proc, 1, pointY);
		JlNativeApi.StoreD(proc, 2, pointZ);
		JlNativeApi.StoreD(proc, 3, lineDirectionX);
		JlNativeApi.StoreD(proc, 4, lineDirectionY);
		JlNativeApi.StoreD(proc, 5, lineDirectionZ);
		JlNativeApi.StoreD(proc, 6, lineMomentX);
		JlNativeApi.StoreD(proc, 7, lineMomentY);
		JlNativeApi.StoreD(proc, 8, lineMomentZ);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		return doubleValue;
	}

	/// <summary>把 Plücker 坐标直线转换为"线上一点 + 方向向量"表示。</summary>
	/// <param name="lineDirectionX">直线方向向量 x 分量元组。</param>
	/// <param name="lineDirectionY">直线方向向量 y 分量元组。</param>
	/// <param name="lineDirectionZ">直线方向向量 z 分量元组。</param>
	/// <param name="lineMomentX">直线力矩向量 x 分量元组。</param>
	/// <param name="lineMomentY">直线力矩向量 y 分量元组。</param>
	/// <param name="lineMomentZ">直线力矩向量 z 分量元组。</param>
	/// <param name="pointX">所求线上点 x 坐标（DOUBLE 装载；取点规则 [待实测]）。</param>
	/// <param name="pointY">所求线上点 y 坐标（DOUBLE 装载）。</param>
	/// <param name="pointZ">所求线上点 z 坐标（DOUBLE 装载）。</param>
	/// <param name="directionX">直线方向 x 分量（DOUBLE 装载）。</param>
	/// <param name="directionY">直线方向 y 分量（DOUBLE 装载）。</param>
	/// <param name="directionZ">直线方向 z 分量（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 Plücker 坐标（方向向量 + 力矩向量）的直线拆成"点 + 方向"表示，对应原生算子 id 2144；六个输出均按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 3D 点线几何组，本库内部没有任何调用者。返回的点是线上哪一点（原点向直线作垂线的垂足？任取一点？）无法由代码判定 [待实测]——不要假设它给出某个特定点（如离原点最近的点），拿到结果后应自行验证。输入方向向量须非零。</para>
	///   <para><b>与相邻算子的取舍</b>要两个点用 <see cref="PlueckerLineToPoints(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；反向转换用 PointDirectionToPlueckerLine。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple lineDirectionX = new double[] { 0.0 };
	///   JlTuple lineDirectionY = new double[] { 1.0 };
	///   JlTuple lineDirectionZ = new double[] { 0.0 };
	///   JlTuple lineMomentX = new double[] { 0.0 };
	///   JlTuple lineMomentY = new double[] { 0.0 };
	///   JlTuple lineMomentZ = new double[] { 0.0 };
	///   JlMisc.PlueckerLineToPointDirection(lineDirectionX, lineDirectionY, lineDirectionZ, lineMomentX, lineMomentY, lineMomentZ, out JlTuple pointX, out JlTuple pointY, out JlTuple pointZ, out JlTuple directionX, out JlTuple directionY, out JlTuple directionZ);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void PlueckerLineToPointDirection(JlTuple lineDirectionX, JlTuple lineDirectionY, JlTuple lineDirectionZ, JlTuple lineMomentX, JlTuple lineMomentY, JlTuple lineMomentZ, out JlTuple pointX, out JlTuple pointY, out JlTuple pointZ, out JlTuple directionX, out JlTuple directionY, out JlTuple directionZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2144);
		JlNativeApi.Store(proc, 0, lineDirectionX);
		JlNativeApi.Store(proc, 1, lineDirectionY);
		JlNativeApi.Store(proc, 2, lineDirectionZ);
		JlNativeApi.Store(proc, 3, lineMomentX);
		JlNativeApi.Store(proc, 4, lineMomentY);
		JlNativeApi.Store(proc, 5, lineMomentZ);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(lineDirectionX);
		JlNativeApi.UnpinTuple(lineDirectionY);
		JlNativeApi.UnpinTuple(lineDirectionZ);
		JlNativeApi.UnpinTuple(lineMomentX);
		JlNativeApi.UnpinTuple(lineMomentY);
		JlNativeApi.UnpinTuple(lineMomentZ);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out pointX);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out pointY);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out pointZ);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out directionX);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out directionY);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out directionZ);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把 Plücker 坐标直线转换为"线上一点 + 方向向量"表示（标量版）。</summary>
	/// <param name="lineDirectionX">直线方向向量 x 分量。</param>
	/// <param name="lineDirectionY">直线方向向量 y 分量。</param>
	/// <param name="lineDirectionZ">直线方向向量 z 分量。</param>
	/// <param name="lineMomentX">直线力矩向量 x 分量。</param>
	/// <param name="lineMomentY">直线力矩向量 y 分量。</param>
	/// <param name="lineMomentZ">直线力矩向量 z 分量。</param>
	/// <param name="pointX">所求线上点 x 坐标（取点规则 [待实测]）。</param>
	/// <param name="pointY">所求线上点 y 坐标。</param>
	/// <param name="pointZ">所求线上点 z 坐标。</param>
	/// <param name="directionX">直线方向 x 分量。</param>
	/// <param name="directionY">直线方向 y 分量。</param>
	/// <param name="directionZ">直线方向 z 分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 Plücker 坐标（方向向量 + 力矩向量）的直线拆成"点 + 方向"表示，对应原生算子 id 2144（与本类元组版 <see cref="PlueckerLineToPointDirection(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>返回的点是线上哪一点无法由代码判定 [待实测]，不要假设它给出某个特定点；输入方向向量须非零。</para>
	/// </remarks>
	public static void PlueckerLineToPointDirection(double lineDirectionX, double lineDirectionY, double lineDirectionZ, double lineMomentX, double lineMomentY, double lineMomentZ, out double pointX, out double pointY, out double pointZ, out double directionX, out double directionY, out double directionZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2144);
		JlNativeApi.StoreD(proc, 0, lineDirectionX);
		JlNativeApi.StoreD(proc, 1, lineDirectionY);
		JlNativeApi.StoreD(proc, 2, lineDirectionZ);
		JlNativeApi.StoreD(proc, 3, lineMomentX);
		JlNativeApi.StoreD(proc, 4, lineMomentY);
		JlNativeApi.StoreD(proc, 5, lineMomentZ);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out pointX);
		err = JlNativeApi.LoadD(proc, 1, err, out pointY);
		err = JlNativeApi.LoadD(proc, 2, err, out pointZ);
		err = JlNativeApi.LoadD(proc, 3, err, out directionX);
		err = JlNativeApi.LoadD(proc, 4, err, out directionY);
		err = JlNativeApi.LoadD(proc, 5, err, out directionZ);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把 Plücker 坐标直线转换为线上两点的表示。</summary>
	/// <param name="lineDirectionX">直线方向向量 x 分量元组。</param>
	/// <param name="lineDirectionY">直线方向向量 y 分量元组。</param>
	/// <param name="lineDirectionZ">直线方向向量 z 分量元组。</param>
	/// <param name="lineMomentX">直线力矩向量 x 分量元组。</param>
	/// <param name="lineMomentY">直线力矩向量 y 分量元组。</param>
	/// <param name="lineMomentZ">直线力矩向量 z 分量元组。</param>
	/// <param name="point1X">第一个线上点 x 坐标（DOUBLE 装载；取点规则 [待实测]）。</param>
	/// <param name="point1Y">第一个线上点 y 坐标（DOUBLE 装载）。</param>
	/// <param name="point1Z">第一个线上点 z 坐标（DOUBLE 装载）。</param>
	/// <param name="point2X">第二个线上点 x 坐标（DOUBLE 装载）。</param>
	/// <param name="point2Y">第二个线上点 y 坐标（DOUBLE 装载）。</param>
	/// <param name="point2Z">第二个线上点 z 坐标（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 Plücker 坐标的直线展开为线上两个点（各 3 分量），对应原生算子 id 2145；六个输出均按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 3D 点线几何组，本库内部没有任何调用者。这两个点的选取规则（是否含离原点最近点、间距多少）无法由代码判定 [待实测]，不要拿它们当"端点"做线段运算。</para>
	///   <para><b>与相邻算子的取舍</b>只要"点+方向"用 <see cref="PlueckerLineToPointDirection(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；反向转换用 PointsToPlueckerLine。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple lineDirectionX = new double[] { 0.0 };
	///   JlTuple lineDirectionY = new double[] { 1.0 };
	///   JlTuple lineDirectionZ = new double[] { 0.0 };
	///   JlTuple lineMomentX = new double[] { 0.0 };
	///   JlTuple lineMomentY = new double[] { 0.0 };
	///   JlTuple lineMomentZ = new double[] { 0.0 };
	///   JlMisc.PlueckerLineToPoints(lineDirectionX, lineDirectionY, lineDirectionZ, lineMomentX, lineMomentY, lineMomentZ, out JlTuple point1X, out JlTuple point1Y, out JlTuple point1Z, out JlTuple point2X, out JlTuple point2Y, out JlTuple point2Z);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void PlueckerLineToPoints(JlTuple lineDirectionX, JlTuple lineDirectionY, JlTuple lineDirectionZ, JlTuple lineMomentX, JlTuple lineMomentY, JlTuple lineMomentZ, out JlTuple point1X, out JlTuple point1Y, out JlTuple point1Z, out JlTuple point2X, out JlTuple point2Y, out JlTuple point2Z)
	{
		IntPtr proc = JlNativeApi.PreCall(2145);
		JlNativeApi.Store(proc, 0, lineDirectionX);
		JlNativeApi.Store(proc, 1, lineDirectionY);
		JlNativeApi.Store(proc, 2, lineDirectionZ);
		JlNativeApi.Store(proc, 3, lineMomentX);
		JlNativeApi.Store(proc, 4, lineMomentY);
		JlNativeApi.Store(proc, 5, lineMomentZ);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(lineDirectionX);
		JlNativeApi.UnpinTuple(lineDirectionY);
		JlNativeApi.UnpinTuple(lineDirectionZ);
		JlNativeApi.UnpinTuple(lineMomentX);
		JlNativeApi.UnpinTuple(lineMomentY);
		JlNativeApi.UnpinTuple(lineMomentZ);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out point1X);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out point1Y);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out point1Z);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out point2X);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out point2Y);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out point2Z);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把 Plücker 坐标直线转换为线上两点的表示（标量版）。</summary>
	/// <param name="lineDirectionX">直线方向向量 x 分量。</param>
	/// <param name="lineDirectionY">直线方向向量 y 分量。</param>
	/// <param name="lineDirectionZ">直线方向向量 z 分量。</param>
	/// <param name="lineMomentX">直线力矩向量 x 分量。</param>
	/// <param name="lineMomentY">直线力矩向量 y 分量。</param>
	/// <param name="lineMomentZ">直线力矩向量 z 分量。</param>
	/// <param name="point1X">第一个线上点 x 坐标（取点规则 [待实测]）。</param>
	/// <param name="point1Y">第一个线上点 y 坐标。</param>
	/// <param name="point1Z">第一个线上点 z 坐标。</param>
	/// <param name="point2X">第二个线上点 x 坐标。</param>
	/// <param name="point2Y">第二个线上点 y 坐标。</param>
	/// <param name="point2Z">第二个线上点 z 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 Plücker 坐标的直线展开为线上两个点（各 3 分量），对应原生算子 id 2145（与本类元组版 <see cref="PlueckerLineToPoints(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>这两个点的选取规则无法由代码判定 [待实测]，不要拿它们当"端点"做线段运算。</para>
	/// </remarks>
	public static void PlueckerLineToPoints(double lineDirectionX, double lineDirectionY, double lineDirectionZ, double lineMomentX, double lineMomentY, double lineMomentZ, out double point1X, out double point1Y, out double point1Z, out double point2X, out double point2Y, out double point2Z)
	{
		IntPtr proc = JlNativeApi.PreCall(2145);
		JlNativeApi.StoreD(proc, 0, lineDirectionX);
		JlNativeApi.StoreD(proc, 1, lineDirectionY);
		JlNativeApi.StoreD(proc, 2, lineDirectionZ);
		JlNativeApi.StoreD(proc, 3, lineMomentX);
		JlNativeApi.StoreD(proc, 4, lineMomentY);
		JlNativeApi.StoreD(proc, 5, lineMomentZ);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out point1X);
		err = JlNativeApi.LoadD(proc, 1, err, out point1Y);
		err = JlNativeApi.LoadD(proc, 2, err, out point1Z);
		err = JlNativeApi.LoadD(proc, 3, err, out point2X);
		err = JlNativeApi.LoadD(proc, 4, err, out point2Y);
		err = JlNativeApi.LoadD(proc, 5, err, out point2Z);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把"线上一点 + 方向向量"表示的直线转换为 Plücker 坐标。</summary>
	/// <param name="pointX">线上点 x 坐标元组。</param>
	/// <param name="pointY">线上点 y 坐标元组。</param>
	/// <param name="pointZ">线上点 z 坐标元组。</param>
	/// <param name="directionX">方向向量 x 分量元组（须非零）。</param>
	/// <param name="directionY">方向向量 y 分量元组。</param>
	/// <param name="directionZ">方向向量 z 分量元组。</param>
	/// <param name="lineDirectionX">输出方向向量 x 分量（DOUBLE 装载）。</param>
	/// <param name="lineDirectionY">输出方向向量 y 分量（DOUBLE 装载）。</param>
	/// <param name="lineDirectionZ">输出方向向量 z 分量（DOUBLE 装载）。</param>
	/// <param name="lineMomentX">输出力矩向量 x 分量（DOUBLE 装载）。</param>
	/// <param name="lineMomentY">输出力矩向量 y 分量（DOUBLE 装载）。</param>
	/// <param name="lineMomentZ">输出力矩向量 z 分量（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把"线上一点 + 方向向量"的直线合成 Plücker 坐标（输出方向向量与力矩向量各 3 分量），对应原生算子 id 2146；六个输出按 DOUBLE 装载。输出的方向向量不一定等于输入方向（可能已单位化）[待实测]。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 3D 点线几何组，本库内部没有任何调用者。方向向量不得为零向量 [待实测：零向量行为]。</para>
	///   <para><b>与相邻算子的取舍</b>手里是两点而非方向时先用 <see cref="PointsToPlueckerLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple pointX = new double[] { 0.0 };
	///   JlTuple pointY = new double[] { 0.0 };
	///   JlTuple pointZ = new double[] { 0.0 };
	///   JlTuple directionX = new double[] { 1.0 };
	///   JlTuple directionY = new double[] { 0.0 };
	///   JlTuple directionZ = new double[] { 0.0 };
	///   JlMisc.PointDirectionToPlueckerLine(pointX, pointY, pointZ, directionX, directionY, directionZ, out JlTuple lineDirectionX, out JlTuple lineDirectionY, out JlTuple lineDirectionZ, out JlTuple lineMomentX, out JlTuple lineMomentY, out JlTuple lineMomentZ);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void PointDirectionToPlueckerLine(JlTuple pointX, JlTuple pointY, JlTuple pointZ, JlTuple directionX, JlTuple directionY, JlTuple directionZ, out JlTuple lineDirectionX, out JlTuple lineDirectionY, out JlTuple lineDirectionZ, out JlTuple lineMomentX, out JlTuple lineMomentY, out JlTuple lineMomentZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2146);
		JlNativeApi.Store(proc, 0, pointX);
		JlNativeApi.Store(proc, 1, pointY);
		JlNativeApi.Store(proc, 2, pointZ);
		JlNativeApi.Store(proc, 3, directionX);
		JlNativeApi.Store(proc, 4, directionY);
		JlNativeApi.Store(proc, 5, directionZ);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(pointX);
		JlNativeApi.UnpinTuple(pointY);
		JlNativeApi.UnpinTuple(pointZ);
		JlNativeApi.UnpinTuple(directionX);
		JlNativeApi.UnpinTuple(directionY);
		JlNativeApi.UnpinTuple(directionZ);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out lineDirectionX);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out lineDirectionY);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out lineDirectionZ);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out lineMomentX);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out lineMomentY);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out lineMomentZ);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把"线上一点 + 方向向量"表示的直线转换为 Plücker 坐标（标量版）。</summary>
	/// <param name="pointX">线上点 x 坐标。</param>
	/// <param name="pointY">线上点 y 坐标。</param>
	/// <param name="pointZ">线上点 z 坐标。</param>
	/// <param name="directionX">方向向量 x 分量（须非零）。</param>
	/// <param name="directionY">方向向量 y 分量。</param>
	/// <param name="directionZ">方向向量 z 分量。</param>
	/// <param name="lineDirectionX">输出方向向量 x 分量。</param>
	/// <param name="lineDirectionY">输出方向向量 y 分量。</param>
	/// <param name="lineDirectionZ">输出方向向量 z 分量。</param>
	/// <param name="lineMomentX">输出力矩向量 x 分量。</param>
	/// <param name="lineMomentY">输出力矩向量 y 分量。</param>
	/// <param name="lineMomentZ">输出力矩向量 z 分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把"线上一点 + 方向向量"的直线合成 Plücker 坐标（输出方向向量与力矩向量各 3 分量），对应原生算子 id 2146（与本类元组版 <see cref="PointDirectionToPlueckerLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。</para>
	///   <para><b>资源与坑</b>方向向量不得为零向量 [待实测：零向量行为]；输出方向向量不一定等于输入方向（可能已单位化）[待实测]。</para>
	/// </remarks>
	public static void PointDirectionToPlueckerLine(double pointX, double pointY, double pointZ, double directionX, double directionY, double directionZ, out double lineDirectionX, out double lineDirectionY, out double lineDirectionZ, out double lineMomentX, out double lineMomentY, out double lineMomentZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2146);
		JlNativeApi.StoreD(proc, 0, pointX);
		JlNativeApi.StoreD(proc, 1, pointY);
		JlNativeApi.StoreD(proc, 2, pointZ);
		JlNativeApi.StoreD(proc, 3, directionX);
		JlNativeApi.StoreD(proc, 4, directionY);
		JlNativeApi.StoreD(proc, 5, directionZ);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out lineDirectionX);
		err = JlNativeApi.LoadD(proc, 1, err, out lineDirectionY);
		err = JlNativeApi.LoadD(proc, 2, err, out lineDirectionZ);
		err = JlNativeApi.LoadD(proc, 3, err, out lineMomentX);
		err = JlNativeApi.LoadD(proc, 4, err, out lineMomentY);
		err = JlNativeApi.LoadD(proc, 5, err, out lineMomentZ);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把线上两点表示的直线转换为 Plücker 坐标。</summary>
	/// <param name="point1X">第一个线上点 x 坐标元组。</param>
	/// <param name="point1Y">第一个线上点 y 坐标元组。</param>
	/// <param name="point1Z">第一个线上点 z 坐标元组。</param>
	/// <param name="point2X">第二个线上点 x 坐标元组。</param>
	/// <param name="point2Y">第二个线上点 y 坐标元组。</param>
	/// <param name="point2Z">第二个线上点 z 坐标元组。</param>
	/// <param name="lineDirectionX">输出方向向量 x 分量（DOUBLE 装载）。</param>
	/// <param name="lineDirectionY">输出方向向量 y 分量（DOUBLE 装载）。</param>
	/// <param name="lineDirectionZ">输出方向向量 z 分量（DOUBLE 装载）。</param>
	/// <param name="lineMomentX">输出力矩向量 x 分量（DOUBLE 装载）。</param>
	/// <param name="lineMomentY">输出力矩向量 y 分量（DOUBLE 装载）。</param>
	/// <param name="lineMomentZ">输出力矩向量 z 分量（DOUBLE 装载）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>由线上两点（point1、point2）构造 Plücker 坐标（方向向量 + 力矩向量共 6 分量），对应原生算子 id 2148；六个输出按 DOUBLE 装载。两点顺序决定方向向量的朝向。</para>
	///   <para><b>约束或前提</b>属 JlMisc 杂项门面的 3D 点线几何组，本库内部没有任何调用者。两点重合时方向为零向量，结果未定义 [待实测]；moment 与两点的叉积关系由原生侧实现，无法由本文件代码核对 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>已是"点+方向"时用 <see cref="PointDirectionToPlueckerLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；只要距离不要中间表示时用 DistancePointLine。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple point1X = new double[] { 0.0 };
	///   JlTuple point1Y = new double[] { 0.0 };
	///   JlTuple point1Z = new double[] { 0.0 };
	///   JlTuple point2X = new double[] { 1.0 };
	///   JlTuple point2Y = new double[] { 0.0 };
	///   JlTuple point2Z = new double[] { 0.0 };
	///   JlMisc.PointsToPlueckerLine(point1X, point1Y, point1Z, point2X, point2Y, point2Z, out JlTuple lineDirectionX, out JlTuple lineDirectionY, out JlTuple lineDirectionZ, out JlTuple lineMomentX, out JlTuple lineMomentY, out JlTuple lineMomentZ);
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 系不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public static void PointsToPlueckerLine(JlTuple point1X, JlTuple point1Y, JlTuple point1Z, JlTuple point2X, JlTuple point2Y, JlTuple point2Z, out JlTuple lineDirectionX, out JlTuple lineDirectionY, out JlTuple lineDirectionZ, out JlTuple lineMomentX, out JlTuple lineMomentY, out JlTuple lineMomentZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2148);
		JlNativeApi.Store(proc, 0, point1X);
		JlNativeApi.Store(proc, 1, point1Y);
		JlNativeApi.Store(proc, 2, point1Z);
		JlNativeApi.Store(proc, 3, point2X);
		JlNativeApi.Store(proc, 4, point2Y);
		JlNativeApi.Store(proc, 5, point2Z);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(point1X);
		JlNativeApi.UnpinTuple(point1Y);
		JlNativeApi.UnpinTuple(point1Z);
		JlNativeApi.UnpinTuple(point2X);
		JlNativeApi.UnpinTuple(point2Y);
		JlNativeApi.UnpinTuple(point2Z);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out lineDirectionX);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out lineDirectionY);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out lineDirectionZ);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out lineMomentX);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out lineMomentY);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out lineMomentZ);
		JlNativeApi.PostCall(proc, err);
	}

	/// <summary>把线上两点表示的直线转换为 Plücker 坐标（标量版）。</summary>
	/// <param name="point1X">第一个线上点 x 坐标。</param>
	/// <param name="point1Y">第一个线上点 y 坐标。</param>
	/// <param name="point1Z">第一个线上点 z 坐标。</param>
	/// <param name="point2X">第二个线上点 x 坐标。</param>
	/// <param name="point2Y">第二个线上点 y 坐标。</param>
	/// <param name="point2Z">第二个线上点 z 坐标。</param>
	/// <param name="lineDirectionX">输出方向向量 x 分量。</param>
	/// <param name="lineDirectionY">输出方向向量 y 分量。</param>
	/// <param name="lineDirectionZ">输出方向向量 z 分量。</param>
	/// <param name="lineMomentX">输出力矩向量 x 分量。</param>
	/// <param name="lineMomentY">输出力矩向量 y 分量。</param>
	/// <param name="lineMomentZ">输出力矩向量 z 分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>由线上两点构造 Plücker 坐标（方向向量 + 力矩向量共 6 分量），对应原生算子 id 2148（与本类元组版 <see cref="PointsToPlueckerLine(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/> 同一算子）；本重载全部以标量直写（StoreD），无钉固定元组开销。两点顺序决定方向向量的朝向。</para>
	///   <para><b>资源与坑</b>两点重合时方向为零向量，结果未定义 [待实测]。</para>
	/// </remarks>
	public static void PointsToPlueckerLine(double point1X, double point1Y, double point1Z, double point2X, double point2Y, double point2Z, out double lineDirectionX, out double lineDirectionY, out double lineDirectionZ, out double lineMomentX, out double lineMomentY, out double lineMomentZ)
	{
		IntPtr proc = JlNativeApi.PreCall(2148);
		JlNativeApi.StoreD(proc, 0, point1X);
		JlNativeApi.StoreD(proc, 1, point1Y);
		JlNativeApi.StoreD(proc, 2, point1Z);
		JlNativeApi.StoreD(proc, 3, point2X);
		JlNativeApi.StoreD(proc, 4, point2Y);
		JlNativeApi.StoreD(proc, 5, point2Z);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out lineDirectionX);
		err = JlNativeApi.LoadD(proc, 1, err, out lineDirectionY);
		err = JlNativeApi.LoadD(proc, 2, err, out lineDirectionZ);
		err = JlNativeApi.LoadD(proc, 3, err, out lineMomentX);
		err = JlNativeApi.LoadD(proc, 4, err, out lineMomentY);
		err = JlNativeApi.LoadD(proc, 5, err, out lineMomentZ);
		JlNativeApi.PostCall(proc, err);
	}
}
