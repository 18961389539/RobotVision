using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace JLVisionLib;

/// <summary>JLVision 原生库（JLVisionCore.dll）的全部互操作入口：算子调用协议、控制元组编解码与运行库配置。</summary>
/// <remarks>
///   <para><b>调用协议（三段式）</b>每个封装方法都以 <see cref="PreCall(int)"/> 按算子 id 创建实例（proc 句柄），
///   随后用 <see cref="StoreI"/> 等 Store* 族写入输入控制参数、必要时 <see cref="InitOCT"/> 预声明输出槽、
///   <see cref="CallProcedure"/> 执行原生算子，用 Load* 族 / JlTuple.LoadNew 读回输出，最后 <see cref="PostCall"/>
///   清理实例并把错误码转抛为 <see cref="JlOperatorException"/>。标量数值与字符串都经“控制元组”编解码。</para>
///   <para><b>错误码约定</b>返回码 2 表示成功（见 <c>Jl_MSG_OK</c>）；≥1000 视为算子/调用错误，由 PostCall 抛异常。</para>
///   <para><b>字符串编码与内存</b>原生侧按运行库当前编码（ANSI 或 UTF-8）收发包尾 NUL 字符串，经
///   <see cref="ToNativeGlobalEncoding"/> / <see cref="FromNativeEncoding"/> 中转；中转的 HGlobal 内存
///   由调用方在写入或读取完成后释放。</para>
///   <para><b>使用边界</b>本类成员虽公开，但除运行库配置类（UseSpinLock 等）外均为封装层内部调用点，
///   标记了 EditorBrowsable(Never) 的成员不宜由业务代码直接调用。</para>
/// </remarks>
[SuppressUnmanagedCodeSecurity]
public class JlNativeApi
{
	/// <summary>JLVision 原生库使用的内存分配器类型。控制原生侧对象/元组内存由系统分配器还是 mimalloc 分配。</summary>
	/// <remarks>须在库内首次分配前设置（见 <see cref="SetMemoryAllocatorType"/>）。</remarks>
	public enum JlMemoryAllocatorType
	{
		/// <summary>未设置 / 非法分配器。</summary>
		Invalid = -1,
		/// <summary>系统默认分配器（malloc/free）。</summary>
		System,
		/// <summary>mimalloc 高性能分配器。</summary>
		/// <remarks>多线程高并发场景下通常优于系统分配器。</remarks>
		MiMalloc
	}

	/// <summary>算子执行进度回调委托。</summary>
	/// <param name="id">关联的窗口/回调标识（原生侧传入）。</param>
	/// <param name="operatorName">正在执行的算子名。</param>
	/// <param name="progress">进度，0~1。</param>
	/// <param name="message">进度消息。</param>
	/// <remarks>供需要长耗时算子进度反馈的宿主注册使用。</remarks>
	public delegate void JlProgressBarCallback(IntPtr id, string operatorName, double progress, string message);

	/// <summary>低级错误回调委托：接收未经格式化的原生错误文本。</summary>
	/// <param name="err">原生侧产生的错误字符串。</param>
	/// <remarks>典型用于接管未被算子调用链抛出的底层错误。</remarks>
	public delegate void JlLowLevelErrorCallback(string err);

	/// <summary>句柄清理回调委托：释放关联的原生资源。</summary>
	/// <param name="ptr">待清理的原生句柄/指针。</param>
	/// <remarks>注册到原生侧、在对应资源释放时被回调用。</remarks>
	public delegate void JlClearProcCallBack(IntPtr ptr);



	private const string NativeLib = "JLVisionCore";

	private const CallingConvention NativeCall = CallingConvention.Cdecl;

	/// <summary>是否运行于 64 位平台（<c>IntPtr</c> 宽度大于 4 字节）。</summary>
	/// <remarks>决定指针类参数（IP）在原生 INTEGER 元组中以 32 位还是 64 位整数承载。</remarks>
	public static readonly bool isPlatform64 = IntPtr.Size > 4;

	/// <summary>是否运行于 Windows（按平台枚举判定，非 Unix 视为 Windows）。</summary>
	/// <remarks>静态构造器会尝试从应用程序基目录加载 <c>JLVisionCore.dll</c> 以支持隐式调用，加载失败不阻断。</remarks>
	public static readonly bool isWindows = testWindows();

	internal const int Jl_MSG_OK = 2;

	internal const int Jl_MSG_TRUE = 2;

	internal const int Jl_MSG_FALSE = 3;

	internal const int Jl_MSG_VOID = 4;

	internal const int Jl_MSG_FAIL = 5;

	static JlNativeApi()
	{
		if (!isWindows)
		{
			return;
		}
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NativeLib + ".dll");
		if (File.Exists(path))
		{
			LoadLibrary(path);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	private JlNativeApi()
	{
	}

	private static bool testWindows()
	{
		int platform = (int)Environment.OSVersion.Platform;
		if (platform != 4)
		{
			return platform != 128;
		}
		return false;
	}
	/// <summary>切换原生库许可错误提示行为（HLIDoLicenseError）。</summary>
	/// <param name="state">是否启用许可错误处理。</param>
	/// <remarks>由宿主在库使用前调用，控制无许可时的表现。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIDoLicenseError")]
	public static extern void DoLicenseError([MarshalAs(UnmanagedType.Bool)] bool state);
	/// <summary>切换原生库内部同步为自旋锁（HLIUseSpinLock）。</summary>
	/// <param name="state">true=使用自旋锁，false=回退系统锁。</param>
	/// <remarks>高频短小算子调用场景下可降低锁开销。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIUseSpinLock")]
	public static extern void UseSpinLock([MarshalAs(UnmanagedType.Bool)] bool state);
	/// <summary>启动/停用原生库工作线程池（HLIStartUpThreadPool）。</summary>
	/// <param name="state">是否启动线程池。</param>
	/// <remarks>并行算子依赖该线程池，须在并发调用前启动。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIStartUpThreadPool")]
	public static extern void StartUpThreadPool([MarshalAs(UnmanagedType.Bool)] bool state);


	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIIsUTF8Encoding")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsUTF8Encoding();
	/// <summary>查询原生库当前内存分配器类型。</summary>
	/// <returns>当前生效的 <see cref="JlMemoryAllocatorType"/>。</returns>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.StdCall, EntryPoint = "HGetMemoryAllocatorType")]
	public static extern JlMemoryAllocatorType GetMemoryAllocatorType();
	/// <summary>设置原生库内存分配器类型。</summary>
	/// <param name="allocator">目标分配器；传 <see cref="JlMemoryAllocatorType.Invalid"/> 不生效。</param>
	/// <remarks>应在库首次分配内存之前调用。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.StdCall, EntryPoint = "HSetMemoryAllocatorType")]
	public static extern void SetMemoryAllocatorType(JlMemoryAllocatorType allocator);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetSerializedSize(IntPtr ptr, out ulong size);

	internal static int GetSerializedSize(byte[] header, out ulong size)
	{
		GCHandle gCHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
		int result = HLIGetSerializedSize(gCHandle.AddrOfPinnedObject(), out size);
		gCHandle.Free();
		return result;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLILock")]
	internal static extern void Lock();

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIUnlock")]
	internal static extern void Unlock();













	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLICreateProcedure")]
	private static extern int CreateProcedure(int procIndex, out IntPtr proc);
	/// <summary>执行已建好的原生算子实例（HLICallProcedure）。</summary>
	/// <param name="proc">由 <see cref="PreCall(int)"/> 创建的实例句柄。</param>
	/// <returns>执行错误码：2=成功；失败码由 <see cref="PostCall"/> 转抛。</returns>
	/// <remarks>调用前须已完成全部输入写入与输出槽声明。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLICallProcedure")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int CallProcedure(IntPtr proc);
	/// <summary>销毁原生算子实例并回收资源（HLIDestroyProcedure）。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="procResult">本次调用的错误码，供销毁阶段一并传播。</param>
	/// <returns>销毁本身的错误码。</returns>
	/// <remarks>由 <see cref="PostCall"/> 收尾调用，一般无需直接调用。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIDestroyProcedure")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DestroyProcedure(IntPtr proc, int procResult);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr HLIGetLogicalName(IntPtr proc);

	internal static string GetLogicalName(IntPtr proc)
	{
		return Marshal.PtrToStringAnsi(HLIGetLogicalName(proc));
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLILogicalName")]
	private static extern IntPtr HLIGetLogicalName(int procIndex);

	internal static string GetLogicalName(int procIndex)
	{
		return Marshal.PtrToStringAnsi(HLIGetLogicalName(procIndex));
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetProcIndex")]
	private static extern int GetProcIndex(IntPtr proc);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetErrorMessage(int err, IntPtr buffer);

	internal static string GetErrorMessage(int err)
	{
		IntPtr intPtr = Marshal.AllocHGlobal(1024);
		HLIGetErrorMessage(err, intPtr);
		string result = FromNativeEncoding(intPtr, force_utf8: false);
		Marshal.FreeHGlobal(intPtr);
		return result;
	}
	/// <summary>创建算子实例，返回其实例句柄（proc）。</summary>
	/// <param name="procIndex">原生算子 id（封装层为每个算子分配的编号）。</param>
	/// <returns>新算子实例句柄。</returns>
	/// <remarks>每个封装调用的起点；创建失败会抛 <see cref="JlOperatorException"/>。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IntPtr PreCall(int procIndex)
	{
		int num = CreateProcedure(procIndex, out var proc);
		if (num != 2)
		{
			JlOperatorException.throwInfo(num, "Could not create a new operator instance for id " + procIndex);
		}
		return proc;
	}
	/// <summary>算子调用收尾：清空输入输出槽、销毁实例，并转抛错误。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="procResult"><see cref="CallProcedure"/> 返回的错误码。</param>
	/// <remarks>销毁错误与调用错误都会被检查并以 <see cref="JlOperatorException"/> 抛出；procIndex 无法取得时以 “Unknown” 呈现。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void PostCall(IntPtr proc, int procResult)
	{
		int procIndex = GetProcIndex(proc);
		HLIClearAllIOCT(proc);
		int err = DestroyProcedure(proc, procResult);
		if (procIndex >= 0)
		{
			JlOperatorException.throwOperator(err, procIndex);
			JlOperatorException.throwOperator(procResult, procIndex);
		}
		else
		{
			JlOperatorException.throwOperator(err, "Unknown");
			JlOperatorException.throwOperator(procResult, "Unknown");
		}
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetInputObject")]
	internal static extern int SetInputObject(IntPtr proc, int parIndex, IntPtr key);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetOutputObject")]
	internal static extern int GetOutputObject(IntPtr proc, int parIndex, out IntPtr key);

	internal static void ClearObject(IntPtr key)
	{
		IntPtr proc = PreCall(570);
		JlCkP(proc, SetInputObject(proc, 1, key));
		int procResult = CallProcedure(proc);
		PostCall(proc, procResult);
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLICopyObject(IntPtr keyIn, out IntPtr keyOut);

	internal static IntPtr CopyObject(IntPtr key)
	{
		IntPtr proc = PreCall(568);
		JlCkP(proc, SetInputObject(proc, 1, key));
		StoreI(proc, 0, 1);
		StoreI(proc, 1, -1);
		int num = CallProcedure(proc);
		if (!IsFailure(num))
		{
			num = GetOutputObject(proc, 1, out key);
		}
		PostCall(proc, num);
		return key;
	}

	internal static string GetObjClass(IntPtr key)
	{
		JlTuple tuple = "object";
		IntPtr proc = PreCall(579);
		JlCkP(proc, SetInputObject(proc, 1, key));
		InitOCT(proc, 0);
		int num = CallProcedure(proc);
		if (!IsFailure(num))
		{
			num = JlTuple.LoadNew(proc, 0, num, out tuple);
		}
		PostCall(proc, num);
		if (tuple.Length <= 0)
		{
			return "any";
		}
		return tuple.S;
	}

	internal static void AssertObjectClass(IntPtr key, string assertClass)
	{
		if (key != JlObjectBase.UNDEF)
		{
			string objClass = GetObjClass(key);
			if (!objClass.StartsWith(assertClass) && objClass != "any")
			{
				throw new JlException("Iconic object type mismatch (expected " + assertClass + ", got " + objClass + ")");
			}
		}
	}
	/// <summary>创建空的原生控制元组句柄（HLICreateTuple）。</summary>
	/// <param name="tuple">输出的元组句柄。</param>
	/// <returns>错误码（2=成功）。</returns>
	/// <remarks>独立编解码用；用完必须 <see cref="DestroyTuple"/>。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLICreateTuple")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int CreateTuple(out IntPtr tuple);
	/// <summary>为某输出槽初始化输出控制元组 OCT（HLIInitOCT）。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出参数槽位。</param>
	/// <remarks>对每个期望的算子输出控制参数，在 <see cref="CallProcedure"/> 前调用一次，使原生侧有可写入的容器。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIInitOCT")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int InitOCT(IntPtr proc, int parIndex);
	/// <summary>清空实例的全部输入/输出控制元组（HLIClearAllIOCT）。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <returns>错误码（2=成功）。</returns>
	/// <remarks>由 <see cref="PostCall"/> 在销毁前调用。</remarks>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int HLIClearAllIOCT(IntPtr proc);
	/// <summary>销毁由 <see cref="CreateTuple"/> 创建的原生元组（HLIDestroyTuple）。</summary>
	/// <param name="tuple">元组句柄。</param>
	/// <returns>错误码（2=成功）。</returns>

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIDestroyTuple")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DestroyTuple(IntPtr tuple);
	/// <summary>把托管 <see cref="JlTuple"/> 的完整内容写入已存在的原生元组句柄。</summary>
	/// <param name="tupleHandle">目标原生元组。</param>
	/// <param name="tuple">源元组；其元素按类型逐段拷贝（LONG 以 INTEGER 槽承载 64 位整数）。</param>
	/// <remarks>封装层把托管输入整体压入原生侧的辅助入口。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreTuple(IntPtr tupleHandle, JlTuple tuple)
	{
		JlTupleType type = ((tuple.Type == JlTupleType.LONG) ? JlTupleType.INTEGER : tuple.Type);
		JlCheckNative(CreateElementsOfType(tupleHandle, tuple.Length, type));
		switch (tuple.Type)
		{
		case JlTupleType.INTEGER:
			JlCheckNative(SetIArr(tupleHandle, tuple.IArr));
			break;
		case JlTupleType.LONG:
			JlCheckNative(SetLArr(tupleHandle, tuple.LArr));
			break;
		case JlTupleType.DOUBLE:
			JlCheckNative(SetDArr(tupleHandle, tuple.DArr));
			break;
		case JlTupleType.STRING:
		{
			string[] sArr = tuple.SArr;
			for (int k = 0; k < tuple.Length; k++)
			{
				JlCheckNative(SetS(tupleHandle, k, sArr[k], force_utf8: true));
			}
			break;
		}
		case JlTupleType.JlANDLE:
		{
			JlHandle[] jlArr = tuple.JlArr;
			for (int j = 0; j < tuple.Length; j++)
			{
				JlCheckNative(SetH(tupleHandle, j, jlArr[j]));
			}
			break;
		}
		case JlTupleType.MIXED:
		{
			object[] oArr = tuple.data.OArr;
			for (int i = 0; i < tuple.Length; i++)
			{
				switch (JlTupleImplementation.GetObjectType(oArr[i]))
				{
				case 1:
					JlCheckNative(SetI(tupleHandle, i, (int)oArr[i]));
					break;
				case 129:
					JlCheckNative(SetL(tupleHandle, i, (long)oArr[i]));
					break;
				case 2:
					JlCheckNative(SetD(tupleHandle, i, (double)oArr[i]));
					break;
				case 4:
					JlCheckNative(SetS(tupleHandle, i, (string)oArr[i], force_utf8: true));
					break;
				case 16:
					JlCheckNative(SetH(tupleHandle, i, (JlHandle)oArr[i]));
					break;
				}
			}
			break;
		}
		}
	}
	/// <summary>把原生元组句柄整体读回为托管 <see cref="JlTuple"/>。</summary>
	/// <param name="tupleHandle">源原生元组。</param>
	/// <returns>按 MIXED 语义装载的新元组，元素类型原样保留。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static JlTuple LoadTuple(IntPtr tupleHandle)
	{
		JlTupleImplementation.LoadData(tupleHandle, JlTupleType.MIXED, out var data, force_utf8: true);
		return new JlTuple(data);
	}

	private static void JlCheckNative(int err)
	{
		if (IsFailure(err))
		{
			throw new JlOperatorException(err);
		}
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetInputTuple")]
	internal static extern int GetInputTuple(IntPtr proc, int parIndex, out IntPtr tuple);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int HLICreateElementsOfType(IntPtr tuple, int length, JlTupleType type);

	internal static int CreateElementsOfType(IntPtr tuple, int length, JlTupleType type)
	{
		JlTupleType type2 = ((type == JlTupleType.EMPTY) ? JlTupleType.MIXED : type);
		return HLICreateElementsOfType(tuple, length, type2);
	}

	internal static int CreateInputTuple(IntPtr proc, int parIndex, int length, JlTupleType type, out IntPtr tuple)
	{
		int inputTuple = GetInputTuple(proc, parIndex, out tuple);
		if (!IsFailure(inputTuple))
		{
			return CreateElementsOfType(tuple, length, type);
		}
		return inputTuple;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetOutputTuple")]
	internal static extern int GetOutputTuple(IntPtr proc, int parIndex, [MarshalAs(UnmanagedType.Bool)] bool handleType, out IntPtr tuple);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetTupleLength")]
	internal static extern int GetTupleLength(IntPtr tuple, out int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetTupleTypeScanElem")]
	internal static extern int GetTupleTypeScanElem(IntPtr tuple, out int type);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetElementType")]
	internal static extern int GetElementType(IntPtr tuple, int index, out JlTupleType type);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetI")]
	internal static extern int SetI(IntPtr tuple, int index, int intValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetL")]
	internal static extern int SetL(IntPtr tuple, int index, long longValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetD")]
	internal static extern int SetD(IntPtr tuple, int index, double doubleValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int HLISetS(IntPtr tuple, int index, IntPtr stringValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetH")]
	internal static extern int SetH(IntPtr tuple, int index, IntPtr handleValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLICopyHandle(IntPtr handle, out IntPtr handleCopy);

	internal static IntPtr CopyHandle(IntPtr handle)
	{
		JlCheckNative(HLICopyHandle(handle, out var handleCopy));
		return handleCopy;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIClearHandle")]
	internal static extern int ClearHandle(IntPtr handle);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIHandleToHlong(IntPtr handle, out IntPtr handleLong);

	internal static IntPtr HandleToHlong(IntPtr handle)
	{
		JlCheckNative(HLIHandleToHlong(handle, out var handleLong));
		return handleLong;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIHandleIsValid(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] out bool is_valid);

	internal static bool HandleIsValid(IntPtr handle)
	{
		JlCheckNative(HLIHandleIsValid(handle, out var is_valid));
		return is_valid;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetHandleSemType(IntPtr handle, out IntPtr sem_type);

	internal static string GetHandleSemType(IntPtr handle)
	{
		JlCheckNative(HLIGetHandleSemType(handle, out var sem_type));
		return FromNativeEncoding(sem_type, force_utf8: false);
	}
	/// <summary>把托管字符串编码到 HGlobal 原生内存。</summary>
	/// <param name="dotnet">托管字符串。</param>
	/// <param name="force_utf8">true=强制按 UTF-8；false=依运行库当前编码（UTF-8 或 ANSI）。</param>
	/// <returns>指向编码结果（含结尾 NUL）的 HGlobal 指针，由调用方负责 <c>Marshal.FreeHGlobal</c>。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IntPtr ToNativeGlobalEncoding(string dotnet, bool force_utf8)
	{
		if (!force_utf8 && !IsUTF8Encoding())
		{
			return Marshal.StringToHGlobalAnsi(dotnet);
		}
		return ToHGlobalUtf8Encoding(dotnet);
	}
	/// <summary>把托管字符串按 UTF-8（含结尾 NUL）编码到 HGlobal 原生内存。</summary>
	/// <param name="dotnet">托管字符串。</param>
	/// <returns>新分配的 HGlobal 指针，由调用方释放。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IntPtr ToHGlobalUtf8Encoding(string dotnet)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(dotnet);
		int num = Marshal.SizeOf(bytes.GetType().GetElementType()) * bytes.Length;
		IntPtr intPtr = Marshal.AllocHGlobal(num + 1);
		Marshal.Copy(bytes, 0, intPtr, bytes.Length);
		Marshal.WriteByte(intPtr, num, 0);
		return intPtr;
	}

	internal static int SetS(IntPtr tuple, int index, string dotnet_string, bool force_utf8)
	{
		if (dotnet_string == null)
		{
			dotnet_string = "";
		}
		IntPtr intPtr = ToNativeGlobalEncoding(dotnet_string, force_utf8);
		int result = HLISetS(tuple, index, intPtr);
		Marshal.FreeHGlobal(intPtr);
		return result;
	}

	internal static int SetIP(IntPtr tuple, int index, IntPtr intPtrValue)
	{
		if (isPlatform64)
		{
			return SetL(tuple, index, intPtrValue.ToInt64());
		}
		return SetI(tuple, index, intPtrValue.ToInt32());
	}
	/// <summary>将 <c>int</c> 标量作为单元素输入控制元组写入槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="intValue">待写入值。</param>
	/// <remarks>槽位元组自动创建/覆写；封装方法按算子签名顺序调用本族方法。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreI(IntPtr proc, int parIndex, int intValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.INTEGER, out var tuple));
		SetI(tuple, 0, intValue);
	}
	/// <summary>将 <c>long</c> 标量作为单元素输入控制元组写入槽位 parIndex（以 INTEGER 槽承载 64 位整数）。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="longValue">待写入值。</param>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreL(IntPtr proc, int parIndex, long longValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.INTEGER, out var tuple));
		SetL(tuple, 0, longValue);
	}
	/// <summary>将 <c>double</c> 标量作为单元素输入控制元组写入槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="doubleValue">待写入值。</param>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreD(IntPtr proc, int parIndex, double doubleValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.DOUBLE, out var tuple));
		SetD(tuple, 0, doubleValue);
	}
	/// <summary>将 <c>string</c> 标量作为单元素输入控制元组写入槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="stringValue">待写入值；null 视为空串。</param>
	/// <remarks>按运行库当前编码写入（不强制 UTF-8）。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreS(IntPtr proc, int parIndex, string stringValue)
	{
		if (stringValue == null)
		{
			stringValue = "";
		}
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.STRING, out var tuple));
		JlCkP(proc, SetS(tuple, 0, stringValue, force_utf8: false));
	}
	/// <summary>将句柄写入单元素输入控制元组（HANDLE 元素）槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="handleValue">原生句柄值。</param>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreH(IntPtr proc, int parIndex, IntPtr handleValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.JlANDLE, out var tuple));
		JlCkP(proc, SetH(tuple, 0, handleValue));
	}
	/// <summary>将指针写入单元素输入控制元组槽位 parIndex，宽度随平台（32/64 位）。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="intPtrValue">待写入指针。</param>
	/// <remarks>64 位平台以 long 元素承载，32 位平台以 int 元素承载。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreIP(IntPtr proc, int parIndex, IntPtr intPtrValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.INTEGER, out var tuple));
		SetIP(tuple, 0, intPtrValue);
	}
	/// <summary>将整个 <see cref="JlTuple"/> 作为输入控制参数写入槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="tupleValue">源元组；null 视为空元组。</param>
	/// <remarks>写入涉及钉住托管数组时，调用方须在 <see cref="CallProcedure"/> 后调用 <see cref="UnpinTuple"/>。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Store(IntPtr proc, int parIndex, JlTuple tupleValue)
	{
		if (tupleValue == null)
		{
			tupleValue = new JlTuple();
		}
		tupleValue.Store(proc, parIndex);
	}
	/// <summary>将 <see cref="JlHandle"/> 作为句柄输入控制参数写入槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入控制参数槽位（0 起）。</param>
	/// <param name="handleValue">句柄；null 视为空句柄。</param>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Store(IntPtr proc, int parIndex, JlHandle handleValue)
	{
		if (handleValue == null)
		{
			handleValue = new JlHandle();
		}
		handleValue.Store(proc, parIndex);
	}
	/// <summary>将图标对象（region/image/XLD 等）句柄写入其输入对象槽位 parIndex。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输入对象参数槽位。</param>
	/// <param name="objectValue">对象；null 视为空对象。</param>
	/// <remarks>对象参数与数值控制参数各自独立编号。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Store(IntPtr proc, int parIndex, JlObjectBase objectValue)
	{
		if (objectValue == null)
		{
			objectValue = new JlObjectBase();
		}
		objectValue.Store(proc, parIndex);
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetIArr")]
	internal static extern int SetIArr(IntPtr tuple, int[] intArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetIArrPtr")]
	internal static extern int SetIArrPtr(IntPtr tuple, int[] intArray, int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetLArr")]
	internal static extern int SetLArr(IntPtr tuple, long[] longArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetLArrPtr")]
	internal static extern int SetLArrPtr(IntPtr tuple, long[] longArray, int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetDArr")]
	internal static extern int SetDArr(IntPtr tuple, double[] doubleArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetDArrPtr")]
	internal static extern int SetDArrPtr(IntPtr tuple, double[] doubleArray, int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetI")]
	internal static extern int GetI(IntPtr tuple, int index, out int intValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetL")]
	internal static extern int GetL(IntPtr tuple, int index, out long longValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int HLIGetH(IntPtr tuple, int index, out IntPtr longValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetD")]
	internal static extern int GetD(IntPtr tuple, int index, out double doubleValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetS(IntPtr tuple, int index, out IntPtr stringPtr);
	/// <summary>把以 NUL 结尾的原生字符串解码为托管 <c>string</c>。</summary>
	/// <param name="Vision">原生字符串指针。</param>
	/// <param name="force_utf8">true=强制按 UTF-8 解码；false=依运行库当前编码。</param>
	/// <returns>解码后的托管字符串。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static string FromNativeEncoding(IntPtr Vision, bool force_utf8)
	{
		if (force_utf8 || IsUTF8Encoding())
		{
			int i;
			for (i = 0; Marshal.ReadByte(Vision, i) != 0; i++)
			{
			}
			byte[] array = new byte[i];
			Marshal.Copy(Vision, array, 0, array.Length);
			return Encoding.UTF8.GetString(array);
		}
		return Marshal.PtrToStringAnsi(Vision);
	}

	internal static int GetS(IntPtr tuple, int index, out string stringValue, bool force_utf8)
	{
		stringValue = string.Empty;
		int num = HLIGetS(tuple, index, out var stringPtr);
		if (num != 2)
		{
			return num;
		}
		stringValue = FromNativeEncoding(stringPtr, force_utf8);
		if (stringValue == null)
		{
			stringValue = "";
			return 5;
		}
		return 2;
	}

	internal static int GetH(IntPtr tuple, int index, out JlHandle handle)
	{
		int result = HLIGetH(tuple, index, out var longValue);
		handle = new JlHandle(longValue);
		return result;
	}

	internal static int GetIP(IntPtr tuple, int index, out IntPtr intPtrValue)
	{
		int result;
		if (isPlatform64)
		{
			result = GetL(tuple, index, out var longValue);
			intPtrValue = new IntPtr(longValue);
		}
		else
		{
			result = GetI(tuple, index, out var intValue);
			intPtrValue = new IntPtr(intValue);
		}
		return result;
	}

	private static int JlCkSingle(IntPtr tuple, JlTupleType expectedType)
	{
		int length = 0;
		if (tuple != IntPtr.Zero)
		{
			GetTupleLength(tuple, out length);
		}
		if (length > 0)
		{
			GetElementType(tuple, 0, out var type);
			if (type != expectedType)
			{
				return 7002;
			}
			return 2;
		}
		return 7001;
	}
	/// <summary>从输出控制槽读取单元素为 <c>int</c>。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出控制参数槽位。</param>
	/// <param name="err">上层已累积的错误码。</param>
	/// <param name="intValue">读出的值；失败时为 -1。</param>
	/// <returns>透传的最终错误码（2=成功）。</returns>
	/// <remarks>err 已失败时直接短路返回默认值；元素为 double 时自动截断取整。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadI(IntPtr proc, int parIndex, int err, out int intValue)
	{
		if (IsFailure(err))
		{
			intValue = -1;
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.INTEGER);
		if (err != 2)
		{
			err = JlCkSingle(tuple, JlTupleType.DOUBLE);
			if (err != 2)
			{
				intValue = -1;
				return err;
			}
			double doubleValue = -1.0;
			err = GetD(tuple, 0, out doubleValue);
			intValue = (int)doubleValue;
			return err;
		}
		return GetI(tuple, 0, out intValue);
	}
	/// <summary>从输出控制槽读取单元素为 <c>long</c>。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出控制参数槽位。</param>
	/// <param name="err">上层已累积的错误码。</param>
	/// <param name="longValue">读出的值；失败时为 -1。</param>
	/// <returns>透传的最终错误码（2=成功）。</returns>
	/// <remarks>err 已失败时直接短路；元素为 double 时自动截断取整。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadL(IntPtr proc, int parIndex, int err, out long longValue)
	{
		if (IsFailure(err))
		{
			longValue = -1L;
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.INTEGER);
		if (err != 2)
		{
			err = JlCkSingle(tuple, JlTupleType.DOUBLE);
			if (err != 2)
			{
				longValue = -1L;
				return err;
			}
			double doubleValue = -1.0;
			err = GetD(tuple, 0, out doubleValue);
			longValue = (long)doubleValue;
			return err;
		}
		return GetL(tuple, 0, out longValue);
	}
	/// <summary>从输出控制槽读取单元素为 <c>double</c>。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出控制参数槽位。</param>
	/// <param name="err">上层已累积的错误码。</param>
	/// <param name="doubleValue">读出的值；失败时为 -1.0。</param>
	/// <returns>透传的最终错误码（2=成功）。</returns>
	/// <remarks>元素为 int 时自动提升为 double。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadD(IntPtr proc, int parIndex, int err, out double doubleValue)
	{
		if (IsFailure(err))
		{
			doubleValue = -1.0;
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.DOUBLE);
		if (err != 2)
		{
			err = JlCkSingle(tuple, JlTupleType.INTEGER);
			if (err != 2)
			{
				doubleValue = -1.0;
				return err;
			}
			int intValue = -1;
			err = GetI(tuple, 0, out intValue);
			doubleValue = intValue;
			return err;
		}
		return GetD(tuple, 0, out doubleValue);
	}
	/// <summary>从输出控制槽读取单元素为 <c>string</c>。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出控制参数槽位。</param>
	/// <param name="err">上层已累积的错误码。</param>
	/// <param name="stringValue">读出的字符串；失败或类型不符为空串。</param>
	/// <returns>透传的最终错误码（2=成功）。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadS(IntPtr proc, int parIndex, int err, out string stringValue)
	{
		if (IsFailure(err))
		{
			stringValue = "";
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.STRING);
		if (err != 2)
		{
			stringValue = "";
			return err;
		}
		return GetS(tuple, 0, out stringValue, force_utf8: false);
	}
	/// <summary>从输出控制槽读取单元素为 <c>IntPtr</c>，宽度随平台。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出控制参数槽位。</param>
	/// <param name="err">上层已累积的错误码。</param>
	/// <param name="intPtrValue">读出的指针；失败时为 <c>IntPtr.Zero</c>。</param>
	/// <returns>透传的最终错误码（2=成功）。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadIP(IntPtr proc, int parIndex, int err, out IntPtr intPtrValue)
	{
		if (IsFailure(err))
		{
			intPtrValue = IntPtr.Zero;
			return err;
		}
		GetOutputTuple(proc, parIndex, handleType: false, out var tuple);
		err = JlCkSingle(tuple, JlTupleType.INTEGER);
		if (err != 2)
		{
			intPtrValue = IntPtr.Zero;
			return err;
		}
		return GetIP(tuple, 0, out intPtrValue);
	}
	/// <summary>从输出对象槽读取 <see cref="JlHandle"/>。</summary>
	/// <param name="proc">实例句柄。</param>
	/// <param name="parIndex">输出对象参数槽位。</param>
	/// <param name="err">上层已累积的错误码。</param>
	/// <param name="handleValue">读出的句柄包装；失败为空句柄。</param>
	/// <returns>透传的最终错误码（2=成功）。</returns>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadH(IntPtr proc, int parIndex, int err, out JlHandle handleValue)
	{
		if (IsFailure(err))
		{
			handleValue = new JlHandle();
			return err;
		}
		GetOutputTuple(proc, parIndex, handleType: true, out var tuple);
		err = JlCkSingle(tuple, JlTupleType.JlANDLE);
		if (err != 2)
		{
			handleValue = new JlHandle();
			return err;
		}
		return GetH(tuple, 0, out handleValue);
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetIArr")]
	internal static extern int GetIArr(IntPtr tuple, [Out] int[] intArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetLArr")]
	internal static extern int GetLArr(IntPtr tuple, [Out] long[] longArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetDArr")]
	internal static extern int GetDArr(IntPtr tuple, [Out] double[] doubleArray);
	/// <summary>解除输入元组在写入期间被钉住的托管数组（null 安全）。</summary>
	/// <param name="tuple">写入过的元组；可传 null。</param>
	/// <remarks>钉住使 GC 在原生调用期间不移动数组；每个钉过的元组调用后都要解钉，否则数组无法回收。</remarks>

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void UnpinTuple(JlTuple tuple)
	{
		tuple?.UnpinTuple();
	}







	internal static bool IsError(int err)
	{
		return err >= 1000;
	}

	internal static bool IsFailure(int err)
	{
		if (err != 2)
		{
			return err != 2;
		}
		return false;
	}

	internal static void JlCkP(IntPtr proc, int err)
	{
		if (IsFailure(err))
		{
			PostCall(proc, err);
		}
	}
}
