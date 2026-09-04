using System;

namespace JLVisionLib;

/// <summary>元组某元素（或一组元素）的类型化访问器。</summary>
/// <remarks>
///   <para><b>功能说明</b>：从 <see cref="JlTuple"/> 按下标取出的单一/成组元素视图。同一内存位置可根据需要
///   以多种类型口径读取——<c>int</c>（<see cref="I"/>）、<c>long</c>（<see cref="L"/>）、<c>double</c>（<see cref="D"/>）、
///   <c>string</c>（<see cref="S"/>）、句柄（<see cref="H"/>）等。</para>
///   <para><b>类型匹配</b>：读取要求元素与目标类型兼容（整数可读 32/64 位、数值可读 <c>double</c> 等）；
///   不匹配会抛 <see cref="JlTupleAccessException"/>。写入时若直接赋值的类型与元组当前存储类型不一致，
///   会自动把元组<b>惰性转换为混合（mixed）类型</b>后再写入，见 <see cref="ConvertToMixed"/>。</para>
///   <para><b>典型场景</b>：<c>JlTuple()[下标]</c> 的返回类型，可直接参与算术/逻辑运算（已重载运算符），
///   也可经隐式转换直接当作 <c>int</c>/<c>double</c>/<c>string</c> 等使用。</para>
///   <para><b>资源与坑</b>：单元素属性（<see cref="I"/>/<see cref="D"/> 等）返回标量；成组属性（<see cref="IArr"/>/
///   <see cref="DArr"/> 等）返回数组。<see cref="IP"/> 按平台位数决定用 32 位还是 64 位整数承载指针。</para>
/// </remarks>
public class JlTupleElements
{
	private JlTuple parent;

	private JlTupleElementsImplementation elements;

	/// <summary>
	///   以 32 位整数读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public int I
	{
		get
		{
			return elements.I[0];
		}
		set
		{
			int[] i = new int[1] { value };
			try
			{
				elements.I = i;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.I = i;
			}
		}
	}

	/// <summary>
	///   以 32 位整数数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）。写入的一一对应到被选中的各下标。</remarks>
	public int[] IArr
	{
		get
		{
			return elements.I;
		}
		set
		{
			try
			{
				elements.I = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.I = value;
			}
		}
	}

	/// <summary>
	///   以 64 位整数读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public long L
	{
		get
		{
			return elements.L[0];
		}
		set
		{
			long[] l = new long[1] { value };
			try
			{
				elements.L = l;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.L = l;
			}
		}
	}

	/// <summary>
	///   以 64 位整数数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）。写入的一一对应到被选中的各下标。</remarks>
	public long[] LArr
	{
		get
		{
			return elements.L;
		}
		set
		{
			try
			{
				elements.L = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.L = value;
			}
		}
	}

	/// <summary>
	///   以 double 读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为数值数据；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public double D
	{
		get
		{
			return elements.D[0];
		}
		set
		{
			double[] d = new double[1] { value };
			try
			{
				elements.D = d;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.D = d;
			}
		}
	}

	/// <summary>
	///   以 double 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为数值数据。写入的一一对应到被选中的各下标。</remarks>
	public double[] DArr
	{
		get
		{
			return elements.D;
		}
		set
		{
			try
			{
				elements.D = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.D = value;
			}
		}
	}

	/// <summary>
	///   以字符串读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为字符串数据；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public string S
	{
		get
		{
			return elements.S[0];
		}
		set
		{
			string[] s = new string[1] { value };
			try
			{
				elements.S = s;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.S = s;
			}
		}
	}

	/// <summary>
	///   以字符串数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为字符串数据。写入的一一对应到被选中的各下标。</remarks>
	public string[] SArr
	{
		get
		{
			return elements.S;
		}
		set
		{
			try
			{
				elements.S = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.S = value;
			}
		}
	}

	/// <summary>
	///   以句柄（<see cref="JlHandle"/>）读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为句柄数据；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public JlHandle H
	{
		get
		{
			return elements.H[0];
		}
		set
		{
			JlHandle[] h = new JlHandle[1] { value };
			try
			{
				elements.H = h;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.H = h;
			}
		}
	}

	/// <summary>
	///   以句柄数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为句柄数据。写入的一一对应到被选中的各下标。</remarks>
	public JlHandle[] JlArr
	{
		get
		{
			return elements.H;
		}
		set
		{
			try
			{
				elements.H = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.H = value;
			}
		}
	}

	/// <summary>
	///   以 object 读取/写入该元素。
	/// </summary>
	/// <remarks>元素可为任意类型，读取时数值会被装箱；写入时会按实际装箱类型自动选择对应的类型化赋值路径。</remarks>
	public object O
	{
		get
		{
			return elements.O[0];
		}
		set
		{
			if (elements is JlTupleElementsMixed)
			{
				elements.O[0] = value;
				return;
			}
			switch (JlTupleImplementation.GetObjectType(value))
			{
			case 1:
				I = (int)value;
				break;
			case 129:
				L = (long)value;
				break;
			case 2:
				D = (double)value;
				break;
			case 32898:
				F = (float)value;
				break;
			case 4:
				S = (string)value;
				break;
			case 16:
				H = (JlHandle)value;
				break;
			case 32900:
				IP = (IntPtr)value;
				break;
			default:
				throw new JlTupleAccessException("Attempting to assign object containing invalid type");
			}
		}
	}

	/// <summary>
	///   以 object 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素可为任意类型，读取时数值会被装箱。写入时会按各元素的装箱类型选择类型化赋值路径。</remarks>
	public object[] OArr
	{
		get
		{
			return elements.O;
		}
		set
		{
			if (elements is JlTupleElementsMixed)
			{
				elements.O = value;
				return;
			}
			switch (JlTupleImplementation.GetObjectsType(value))
			{
			case 1:
				IArr = Array.ConvertAll(value, ObjectToInt);
				break;
			case 129:
				LArr = Array.ConvertAll(value, ObjectToLong);
				break;
			case 2:
				DArr = Array.ConvertAll(value, ObjectToDouble);
				break;
			case 32898:
				FArr = Array.ConvertAll(value, ObjectToFloat);
				break;
			case 4:
				SArr = Array.ConvertAll(value, ObjectToString);
				break;
			case 16:
				JlArr = Array.ConvertAll(value, ObjectToHandle);
				break;
			case 32900:
				IPArr = Array.ConvertAll(value, ObjectToIntPtr);
				break;
			default:
				throw new JlTupleAccessException("Attempting to assign object containing invalid type");
			}
		}
	}

	/// <summary>
	///   以 float 读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为数值数据；以 float 口径读取会有精度损失（内部按 double 存储）。读取对单个元素生效。</remarks>
	public float F
	{
		get
		{
			return (float)D;
		}
		set
		{
			D = value;
		}
	}

	/// <summary>
	///   以 float 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为数值数据；读取/写入以 float 口径进行，存在精度损失。各值一一对应到被选中的下标。</remarks>
	public float[] FArr
	{
		get
		{
			double[] dArr = DArr;
			float[] array = new float[dArr.Length];
			for (int i = 0; i < dArr.Length; i++)
			{
				array[i] = (float)dArr[i];
			}
			return array;
		}
		set
		{
			double[] array = new double[value.Length];
			for (int i = 0; i < value.Length; i++)
			{
				array[i] = value[i];
			}
			DArr = array;
		}
	}

	/// <summary>
	///   以 IntPtr 读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为代表指针的整数，且需匹配当前平台的 <see cref="IntPtr.Size"/>（64 位平台用 64 位整数、32 位平台用 32 位整数）。</remarks>
	public IntPtr IP
	{
		get
		{
			if (JlNativeApi.isPlatform64)
			{
				if (Type == JlTupleType.LONG || Type == JlTupleType.JlANDLE)
				{
					return new IntPtr(L);
				}
			}
			else if (Type == JlTupleType.INTEGER || Type == JlTupleType.JlANDLE)
			{
				return new IntPtr(I);
			}
			throw new JlTupleAccessException("Value does not represent a pointer on this platform");
		}
		set
		{
			if (Type == JlTupleType.JlANDLE)
			{
				value = H.Handle;
			}
			if (JlNativeApi.isPlatform64)
			{
				L = value.ToInt64();
			}
			else
			{
				I = value.ToInt32();
			}
		}
	}

	/// <summary>
	///   以 IntPtr 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为代表指针的整数，且匹配当前平台的 <see cref="IntPtr.Size"/>。</remarks>
	public IntPtr[] IPArr
	{
		get
		{
			if (JlNativeApi.isPlatform64 && Type == JlTupleType.LONG)
			{
				IntPtr[] array = new IntPtr[LArr.Length];
				for (int i = 0; i < LArr.Length; i++)
				{
					array[i] = new IntPtr(LArr[i]);
				}
				return array;
			}
			if (Type == JlTupleType.INTEGER)
			{
				IntPtr[] array2 = new IntPtr[IArr.Length];
				for (int j = 0; j < IArr.Length; j++)
				{
					array2[j] = new IntPtr(IArr[j]);
				}
				return array2;
			}
			throw new JlTupleAccessException("Value does not represent a pointer on this platform");
		}
		set
		{
			if (JlNativeApi.isPlatform64)
			{
				long[] array = new long[value.Length];
				for (int i = 0; i < value.Length; i++)
				{
					array[i] = value[i].ToInt64();
				}
				LArr = array;
			}
			else
			{
				int[] array2 = new int[value.Length];
				for (int j = 0; j < value.Length; j++)
				{
					array2[j] = value[j].ToInt32();
				}
				IArr = array2;
			}
		}
	}

	/// <summary>该元素的实际数据类型。</summary>
	public JlTupleType Type => elements.Type;

	/// <summary>该访问器覆盖的元素个数（单元素访问时为 1）。</summary>
	internal int Length => elements.Length;

	internal JlTupleElements()
	{
		parent = null;
		elements = new JlTupleElementsImplementation();
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt32 source, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt32(source, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt32 source, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt32(source, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt64 tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt64(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt64 tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt64(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleDouble tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsDouble(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleDouble tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsDouble(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleString tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsString(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleString tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsString(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleHandle tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsHandle(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleHandle tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsHandle(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleMixed tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsMixed(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleMixed tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsMixed(tupleImp, indices);
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为int；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static int ObjectToInt(object o)
	{
		return (int)o;
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为long；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static long ObjectToLong(object o)
	{
		return (long)o;
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为double；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static double ObjectToDouble(object o)
	{
		return (double)o;
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为float；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static float ObjectToFloat(object o)
	{
		return (float)o;
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为string；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static string ObjectToString(object o)
	{
		return (string)o;
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为JlHandle 句柄；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static JlHandle ObjectToHandle(object o)
	{
		return (JlHandle)o;
	}

	/// <summary>把装箱标量 <c>o</c> 强转（cast）为IntPtr；类型不匹配时抛 <see cref="InvalidCastException"/>。</summary>
	public static IntPtr ObjectToIntPtr(object o)
	{
		return (IntPtr)o;
	}

	internal void ConvertToMixed()
	{
		if (elements is JlTupleElementsMixed)
		{
			throw new JlTupleAccessException();
		}
		elements = parent.ConvertToMixed(elements.getIndices());
	}

	/// <summary>将本元素与 int 标量做相加运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>将本元素与 long 标量做相加运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>将本元素与 float 标量做相加运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>将本元素与 double 标量做相加运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>将本元素与 string 标量做相加运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做相加运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做相加运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator +(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple + t2;
	}

	/// <summary>将本元素与 int 标量做相减运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>将本元素与 long 标量做相减运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>将本元素与 float 标量做相减运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>将本元素与 double 标量做相减运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>将本元素与 string 标量做相减运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做相减运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做相减运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator -(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple - t2;
	}

	/// <summary>将本元素与 int 标量做相乘运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>将本元素与 long 标量做相乘运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>将本元素与 float 标量做相乘运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>将本元素与 double 标量做相乘运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>将本元素与 string 标量做相乘运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做相乘运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做相乘运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator *(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple * t2;
	}

	/// <summary>将本元素与 int 标量做相除运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>将本元素与 long 标量做相除运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>将本元素与 float 标量做相除运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>将本元素与 double 标量做相除运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>将本元素与 string 标量做相除运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做相除运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做相除运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator /(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple / t2;
	}

	/// <summary>将本元素与 int 标量做取余运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>将本元素与 long 标量做取余运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>将本元素与 float 标量做取余运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>将本元素与 double 标量做取余运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>将本元素与 string 标量做取余运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做取余运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做取余运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator %(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple % t2;
	}

	/// <summary>将本元素与 int 标量做按位与运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>将本元素与 long 标量做按位与运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>将本元素与 float 标量做按位与运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>将本元素与 double 标量做按位与运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>将本元素与 string 标量做按位与运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做按位与运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做按位与运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator &(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple & t2;
	}

	/// <summary>将本元素与 int 标量做按位或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>将本元素与 long 标量做按位或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>将本元素与 float 标量做按位或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>将本元素与 double 标量做按位或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>将本元素与 string 标量做按位或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做按位或运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做按位或运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator |(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple | t2;
	}

	/// <summary>将本元素与 int 标量做按位异或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>将本元素与 long 标量做按位异或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>将本元素与 float 标量做按位异或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>将本元素与 double 标量做按位异或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>将本元素与 string 标量做按位异或运算：两侧先转为 <see cref="JlTuple"/>（标量自动广播为单元素元组）再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>将本元素与 另一元素视图 JlTupleElements 做按位异或运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>将本元素与 JlTuple 元组做按位异或运算：两侧先转为 <see cref="JlTuple"/>再按 <see cref="JlTuple"/> 逐元素语义执行，返回结果 <see cref="JlTuple"/>，本元素不被修改。</summary>
	public static JlTuple operator ^(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple ^ t2;
	}

	/// <summary>比较本元素与 int 标量是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)int</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>比较本元素与 long 标量是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)long</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>比较本元素与 float 标量是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)float</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>比较本元素与 double 标量是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)double</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>比较本元素与 string 标量是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)string</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>比较本元素与 另一元素视图 JlTupleElements 是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)JlTupleElements</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>比较本元素与 JlTuple 元组是否满足“小于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt; (JlTuple)JlTuple</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple < t2;
	}

	/// <summary>比较本元素与 int 标量是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)int</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>比较本元素与 long 标量是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)long</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>比较本元素与 float 标量是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)float</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>比较本元素与 double 标量是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)double</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>比较本元素与 string 标量是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)string</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>比较本元素与 另一元素视图 JlTupleElements 是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)JlTupleElements</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>比较本元素与 JlTuple 元组是否满足“大于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt; (JlTuple)JlTuple</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple > t2;
	}

	/// <summary>比较本元素与 int 标量是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)int</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>比较本元素与 long 标量是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)long</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>比较本元素与 float 标量是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)float</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>比较本元素与 double 标量是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)double</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>比较本元素与 string 标量是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)string</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>比较本元素与 另一元素视图 JlTupleElements 是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)JlTupleElements</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>比较本元素与 JlTuple 元组是否满足“小于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &lt;= (JlTuple)JlTuple</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator <=(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple <= t2;
	}

	/// <summary>比较本元素与 int 标量是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)int</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>比较本元素与 long 标量是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)long</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>比较本元素与 float 标量是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)float</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>比较本元素与 double 标量是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)double</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>比较本元素与 string 标量是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)string</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>比较本元素与 另一元素视图 JlTupleElements 是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)JlTupleElements</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>比较本元素与 JlTuple 元组是否满足“大于等于”关系，返回 <c>bool</c>：语义等价 <c>(JlTuple)本元素 &gt;= (JlTuple)JlTuple</c>，逐元素规则见 <see cref="JlTuple"/> 对应运算符文档。</summary>
	public static bool operator >=(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple >= t2;
	}

	/// <summary>隐式读出为 <c>bool</c>：按元素 64 位整型口径非零判定（<c>L != 0</c>）。</summary>
	public static implicit operator bool(JlTupleElements hte)
	{
		return hte.L != 0;
	}

	/// <summary>隐式读出为 <c>int</c>：取元素 32 位整型值（等价 <see cref="JlTupleElements.I"/>）。</summary>
	public static implicit operator int(JlTupleElements hte)
	{
		return hte.I;
	}

	/// <summary>隐式读出为 <c>long</c>：取元素 64 位整型值（等价 <see cref="JlTupleElements.L"/>）。</summary>
	public static implicit operator long(JlTupleElements hte)
	{
		return hte.L;
	}

	/// <summary>隐式读出为 <c>IntPtr</c>：取元素平台指针值（等价 <see cref="JlTupleElements.IP"/>，位宽按平台）。</summary>
	public static implicit operator IntPtr(JlTupleElements hte)
	{
		return hte.IP;
	}

	/// <summary>隐式读出为 <c>double</c>：取元素双精度值（等价 <see cref="JlTupleElements.D"/>）。</summary>
	public static implicit operator double(JlTupleElements hte)
	{
		return hte.D;
	}

	/// <summary>隐式读出为 <c>string</c>：取元素字符串值（等价 <see cref="JlTupleElements.S"/>）。</summary>
	public static implicit operator string(JlTupleElements hte)
	{
		return hte.S;
	}

	/// <summary>隐式把int 标量 <c>i</c>构造成单元素 <see cref="JlTuple"/> 并返回其元素视图（等价 <c>new JlTuple(i)[0]</c>），便于从标量直接获得元素访问器。</summary>
	public static implicit operator JlTupleElements(int i)
	{
		return new JlTuple(i)[0];
	}

	/// <summary>隐式把long 标量 <c>l</c>构造成单元素 <see cref="JlTuple"/> 并返回其元素视图（等价 <c>new JlTuple(l)[0]</c>），便于从标量直接获得元素访问器。</summary>
	public static implicit operator JlTupleElements(long l)
	{
		return new JlTuple(l)[0];
	}

	/// <summary>隐式把<c>IntPtr</c> <c>ip</c>构造成单元素 <see cref="JlTuple"/> 并返回其元素视图（等价 <c>new JlTuple(ip)[0]</c>），便于从标量直接获得元素访问器。</summary>
	public static implicit operator JlTupleElements(IntPtr ip)
	{
		return new JlTuple(ip)[0];
	}

	/// <summary>隐式把double 标量 <c>d</c>构造成单元素 <see cref="JlTuple"/> 并返回其元素视图（等价 <c>new JlTuple(d)[0]</c>），便于从标量直接获得元素访问器。</summary>
	public static implicit operator JlTupleElements(double d)
	{
		return new JlTuple(d)[0];
	}

	/// <summary>隐式把string 标量 <c>s</c>构造成单元素 <see cref="JlTuple"/> 并返回其元素视图（等价 <c>new JlTuple(s)[0]</c>），便于从标量直接获得元素访问器。</summary>
	public static implicit operator JlTupleElements(string s)
	{
		return new JlTuple(s)[0];
	}

	/// <summary>隐式把<see cref="JlHandle"/> <c>h</c>构造成单元素 <see cref="JlTuple"/> 并返回其元素视图（等价 <c>new JlTuple(new JlTupleHandle(new JlHandle[1]{h}, copy:false))[0]</c>），便于从标量直接获得元素访问器（句柄不复制，所有权随原引用）。</summary>
	public static implicit operator JlTupleElements(JlHandle h)
	{
		return new JlTuple(new JlTupleHandle(new JlHandle[1] { h }, copy: false));
	}
}
