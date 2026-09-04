using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JLVisionLib;

internal class JlTupleInt64 : JlTupleImplementation
{
	protected long[] l;

	public override long[] LArr
	{
		get
		{
			return l;
		}
		set
		{
			SetArray(value, copy: false);
		}
	}

	public override JlTupleType Type => JlTupleType.LONG;

	protected override Array CreateArray(int size)
	{
		return new long[size];
	}

	protected override void NotifyArrayUpdate()
	{
		l = (long[])data;
	}

	internal override void PinTuple()
	{
		Monitor.Enter(this);
		if (pinCount == 0)
		{
			pinHandle = GCHandle.Alloc(l, GCHandleType.Pinned);
		}
		pinCount++;
		Monitor.Exit(this);
	}

	public JlTupleInt64(long l)
	{
		SetArray(new long[1] { l }, copy: false);
	}

	public JlTupleInt64(long[] l, bool copy)
	{
		SetArray(l, copy);
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
		new JlTupleElementsInt64(this, indices).setL(elements.LArr);
	}

	public override int[] ToIArr()
	{
		int[] array = new int[iLength];
		for (int i = 0; i < iLength; i++)
		{
			array[i] = (int)l[i];
		}
		return array;
	}

	public override long[] ToLArr()
	{
		return (long[])ToArray(typeL);
	}

	public override double[] ToDArr()
	{
		return (double[])ToArray(typeD);
	}

	public override float[] ToFArr()
	{
		return (float[])ToArray(typeF);
	}

	public override IntPtr[] ToIPArr()
	{
		if (!JlNativeApi.isPlatform64)
		{
			base.ToIPArr();
		}
		IntPtr[] array = new IntPtr[iLength];
		for (int i = 0; i < iLength; i++)
		{
			array[i] = new IntPtr(l[i]);
		}
		return array;
	}

	public override int CopyToLArr(long[] dst, int offset)
	{
		Array.Copy(l, 0, dst, offset, iLength);
		return iLength;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			dst[i + offset] = l[i];
		}
		return iLength;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return impl.CopyToLArr(l, offset);
	}

	public override void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.JlCkP(proc, JlNativeApi.GetInputTuple(proc, parIndex, out var tuple));
		StoreData(proc, tuple);
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
		PinTuple();
		if (!JlNativeApi.isPlatform64)
		{
			JlNativeApi.JlCkP(proc, JlNativeApi.CreateElementsOfType(tuple, base.Length, JlTupleType.INTEGER));
			for (int i = 0; i < base.Length; i++)
			{
				JlNativeApi.SetL(tuple, i, l[i]);
			}
		}
		else
		{
			JlNativeApi.SetLArrPtr(tuple, l, iLength);
		}
	}

	public static int Load(IntPtr tuple, out JlTupleInt64 data)
	{
		JlNativeApi.GetTupleLength(tuple, out var length);
		long[] longArray = new long[length];
		int lArr = JlNativeApi.GetLArr(tuple, longArray);
		data = new JlTupleInt64(longArray, copy: false);
		return lArr;
	}
}
