using System;

namespace JLVisionLib;

	/// <summary>以元组为内部存储的数据包装：经 RawData/索引器访问，可隐式转 JlTuple；供 Data 类对象的装载与数组拼接使用。</summary>
public class JlData
{
	internal JlTuple tuple;

	/// <summary>
	///   Provides access to the internally used tuple data
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>访问内部使用的元组数据</para>
	///   <para><b>典型场景</b></para>
	///   <para>访问内部数据或按下标取值</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlData obj = ...;
	///   var value = obj.RawData;
	///   </code>
	/// </remarks>
	public JlTuple RawData
	{
		get
		{
			return tuple;
		}
		set
		{
			tuple = new JlTuple(value);
		}
	}

	/// <summary>
	///   Provides access to the value at the specified index
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>按索引访问元素</para>
	///   <para><b>典型场景</b></para>
	///   <para>按索引取出对象数组中的单个元素</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlData obj = ...;
	///   var item = obj[0];
	///   </code>
	/// </remarks>
	public JlTupleElements this[int index]
	{
		get
		{
			return tuple[index];
		}
		set
		{
			tuple[index] = value;
		}
	}

	internal JlData()
	{
		tuple = new JlTuple();
	}

	internal JlData(JlTuple t)
	{
		tuple = t;
	}

	internal JlData(JlData data)
	{
		tuple = data.tuple;
	}

	internal static JlTuple ConcatArray(JlData[] data)
	{
		JlTuple hTuple = new JlTuple();
		for (int i = 0; i < data.Length; i++)
		{
			hTuple = hTuple.TupleConcat(data[i].tuple);
		}
		return hTuple;
	}

	internal void UnpinTuple()
	{
		tuple.UnpinTuple();
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		tuple.Store(proc, parIndex);
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		return tuple.Load(proc, parIndex, err);
	}

	internal int Load(IntPtr proc, int parIndex, JlTupleType type, int err)
	{
		return tuple.Load(proc, parIndex, type, err);
	}

	/// <summary>
	///   将 JlData 隐式转换为 JlTuple。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>将 JlData 隐式转换为 JlTuple。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlData data = ...;
	///   JlTuple tuple = data;
	///   </code>
	/// </remarks>
	public static implicit operator JlTuple(JlData data)
	{
		return data.tuple;
	}
}
