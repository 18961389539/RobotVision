using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JLVisionLib;

/// <summary>以 <c>double[]</c> 为底层存储的元组实现（内部类）。</summary>
/// <remarks>
///   <para><b>功能说明</b>：是 <see cref="JlTupleType.DOUBLE"/> 类型元组的具体实现，数据保存在字段 <c>d</c>
///   （与基类的 <c>data</c> 对象经由 <see cref="NotifyArrayUpdate"/> 保持同步）。</para>
///   <para><b>典型场景</b>：由 <c>JlTuple</c> 按需创建，用于承载双精度数值序列；面向用户的操作应走
///   <c>JlTuple</c> / <see cref="JlTupleElements"/>，本类主要供内部传参与编解码使用。</para>
///   <para><b>资源与坑</b>：<see cref="PinTuple"/> 用 <see cref="GCHandle"/> 固定数组以便向原生层传指针，
///   因此调用方需在原生调用完成后调用 <see cref="JlNativeApi.UnpinTuple"/>，
///   否则数组无法被 GC 回收。</para>
/// </remarks>
internal class JlTupleDouble : JlTupleImplementation
{
	protected double[] d;

	public override double[] DArr
	{
		get
		{
			return d;
		}
		set
		{
			SetArray(value, copy: false);
		}
	}

	public override JlTupleType Type => JlTupleType.DOUBLE;

	protected override Array CreateArray(int size)
	{
		return new double[size];
	}

	protected override void NotifyArrayUpdate()
	{
		d = (double[])data;
	}

	public JlTupleDouble(double d)
	{
		SetArray(new double[1] { d }, copy: false);
	}

	public JlTupleDouble(double[] d, bool copy)
	{
		SetArray(d, copy);
	}

	public JlTupleDouble(float[] f)
	{
		SetArray(f, copy: true);
	}

	internal override void PinTuple()
	{
		Monitor.Enter(this);
		if (pinCount == 0)
		{
			pinHandle = GCHandle.Alloc(d, GCHandleType.Pinned);
		}
		pinCount++;
		Monitor.Exit(this);
	}

	public override JlTupleElements GetElement(int index, JlTuple parent)
	{
		return new JlTupleElements(parent, this, index);
	}

	public override JlTupleElements GetElements(int[] indices, JlTuple parent)
	{
		if (indices == null || indices.Length == 0)
		{
			return new JlTupleElements();
		}
		return new JlTupleElements(parent, this, indices);
	}

	public override void SetElements(int[] indices, JlTupleElements elements)
	{
		new JlTupleElementsDouble(this, indices).setD(elements.DArr);
	}

	public override double[] ToDArr()
	{
		return (double[])ToArray(typeD);
	}

	public override float[] ToFArr()
	{
		float[] array = new float[iLength];
		for (int i = 0; i < iLength; i++)
		{
			array[i] = (float)d[i];
		}
		return array;
		// 注：由 double 转 float 会丢失精度；仅用于对接以 float 存数的原生/第三方接口。
	}

	public override int CopyToDArr(double[] dst, int offset)
	{
		Array.Copy(d, 0, dst, offset, iLength);
		return iLength;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			dst[i + offset] = d[i];
		}
		return iLength;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return impl.CopyToDArr(d, offset);
	}

	public override void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.JlCkP(proc, JlNativeApi.GetInputTuple(proc, parIndex, out var tuple));
		StoreData(proc, tuple);
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
		PinTuple();
		JlNativeApi.SetDArrPtr(tuple, d, iLength);
	}

	public static int Load(IntPtr tuple, out JlTupleDouble data)
	{
		JlNativeApi.GetTupleLength(tuple, out var length);
		double[] doubleArray = new double[length];
		int dArr = JlNativeApi.GetDArr(tuple, doubleArray);
		data = new JlTupleDouble(doubleArray, copy: false);
		return dArr;
	}
}
