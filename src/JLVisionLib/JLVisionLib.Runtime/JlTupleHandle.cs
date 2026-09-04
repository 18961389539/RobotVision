using System;

namespace JLVisionLib;

internal class JlTupleHandle : JlTupleImplementation
{
	protected JlHandle[] h;

	public override JlHandle[] JlArr
	{
		get
		{
			return h;
		}
		set
		{
			SetArray(value, copy: false);
		}
	}

	public override JlTupleType Type => JlTupleType.JlANDLE;

	protected override Array CreateArray(int size)
	{
		JlHandle[] array = new JlHandle[size];
		for (int i = 0; i < size; i++)
		{
			array[i] = new JlHandle();
		}
		return array;
	}

	protected override void NotifyArrayUpdate()
	{
		h = (JlHandle[])data;
	}

	public JlTupleHandle(JlHandle h)
	{
		SetArray(new JlHandle[1]
		{
			new JlHandle(h)
		}, copy: false);
	}

	public JlTupleHandle(JlHandle[] h, bool copy)
	{
		if (copy)
		{
			JlHandle[] array = new JlHandle[h.Length];
			for (int i = 0; i < h.Length; i++)
			{
				array[i] = new JlHandle(h[i]);
			}
			SetArray(array, copy: false);
		}
		else
		{
			SetArray(h, copy: false);
		}
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
		new JlTupleElementsHandle(this, indices).setH(elements.JlArr);
	}

	public override void Dispose()
	{
		for (int i = 0; i < base.Length; i++)
		{
			if (h[i] != null)
			{
				h[i].Dispose();
			}
		}
	}

	public override JlHandle[] ToHArr()
	{
		JlHandle[] array = new JlHandle[iLength];
		CopyToHArr(array, 0);
		return array;
	}

	public override object[] ToOArr()
	{
		object[] array = new object[iLength];
		CopyToOArr(array, 0);
		return array;
	}

	public override int CopyToHArr(JlHandle[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			dst[i + offset] = new JlHandle(h[i]);
		}
		return iLength;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			dst[i + offset] = new JlHandle(h[i]);
		}
		return iLength;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return impl.CopyToHArr(h, offset);
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
		for (int i = 0; i < iLength; i++)
		{
			JlNativeApi.JlCkP(proc, JlNativeApi.SetH(tuple, i, h[i]));
		}
	}

	public static int Load(IntPtr tuple, out JlTupleHandle data)
	{
		int num = 2;
		JlNativeApi.GetTupleLength(tuple, out var length);
		JlHandle[] array = new JlHandle[length];
		for (int i = 0; i < length; i++)
		{
			if (!JlNativeApi.IsFailure(num))
			{
				num = JlNativeApi.GetH(tuple, i, out array[i]);
			}
		}
		data = new JlTupleHandle(array, copy: false);
		return num;
	}
}
