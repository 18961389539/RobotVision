using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JLVisionLib;

internal class JlTupleInt32 : JlTupleImplementation
{
	protected int[] i;

	public override int[] IArr
	{
		get
		{
			return i;
		}
		set
		{
			SetArray(value, copy: false);
		}
	}

	public override JlTupleType Type => JlTupleType.INTEGER;

	protected override Array CreateArray(int size)
	{
		return new int[size];
	}

	protected override void NotifyArrayUpdate()
	{
		i = (int[])data;
	}

	internal override void PinTuple()
	{
		Monitor.Enter(this);
		if (pinCount == 0)
		{
			pinHandle = GCHandle.Alloc(i, GCHandleType.Pinned);
		}
		pinCount++;
		Monitor.Exit(this);
	}

	public JlTupleInt32(int i)
	{
		SetArray(new int[1] { i }, copy: false);
	}

	public JlTupleInt32(int[] i, bool copy)
	{
		SetArray(i, copy);
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
		new JlTupleElementsInt32(this, indices).setI(elements.IArr);
	}

	public override int[] ToIArr()
	{
		return (int[])ToArray(typeI);
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
		if (JlNativeApi.isPlatform64)
		{
			base.ToIPArr();
		}
		IntPtr[] array = new IntPtr[iLength];
		for (int i = 0; i < iLength; i++)
		{
			array[i] = new IntPtr(this.i[i]);
		}
		return array;
	}

	public override int CopyToIArr(int[] dst, int offset)
	{
		Array.Copy(data, 0, dst, offset, iLength);
		return iLength;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			dst[i + offset] = this.i[i];
		}
		return iLength;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return impl.CopyToIArr(i, offset);
	}

	public override void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.JlCkP(proc, JlNativeApi.GetInputTuple(proc, parIndex, out var tuple));
		StoreData(proc, tuple);
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
		PinTuple();
		if (JlNativeApi.isPlatform64)
		{
			JlNativeApi.JlCkP(proc, JlNativeApi.CreateElementsOfType(tuple, base.Length, JlTupleType.INTEGER));
			for (int i = 0; i < base.Length; i++)
			{
				JlNativeApi.SetI(tuple, i, this.i[i]);
			}
		}
		else
		{
			JlNativeApi.SetIArrPtr(tuple, this.i, iLength);
		}
	}

	public static int Load(IntPtr tuple, out JlTupleInt32 data)
	{
		JlNativeApi.GetTupleLength(tuple, out var length);
		int[] intArray = new int[length];
		int iArr = JlNativeApi.GetIArr(tuple, intArray);
		data = new JlTupleInt32(intArray, copy: false);
		return iArr;
	}
}
