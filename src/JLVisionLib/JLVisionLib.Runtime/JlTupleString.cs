using System;

namespace JLVisionLib;

internal class JlTupleString : JlTupleImplementation
{
	protected string[] s;

	public override string[] SArr
	{
		get
		{
			return s;
		}
		set
		{
			SetArray(value, copy: false);
		}
	}

	public override JlTupleType Type => JlTupleType.STRING;

	protected override Array CreateArray(int size)
	{
		string[] array = new string[size];
		for (int i = 0; i < size; i++)
		{
			array[i] = "";
		}
		return array;
	}

	protected override void NotifyArrayUpdate()
	{
		s = (string[])data;
	}

	public JlTupleString(string s)
	{
		SetArray(new string[1] { s }, copy: false);
	}

	public JlTupleString(string[] s, bool copy)
	{
		SetArray(s, copy);
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
		new JlTupleElementsString(this, indices).setS(elements.SArr);
	}

	public override string[] ToSArr()
	{
		return (string[])ToArray(typeS);
	}

	public override int CopyToSArr(string[] dst, int offset)
	{
		Array.Copy(s, 0, dst, offset, iLength);
		return iLength;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			dst[i + offset] = s[i];
		}
		return iLength;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return impl.CopyToSArr(s, offset);
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
		for (int i = 0; i < iLength; i++)
		{
			JlNativeApi.JlCkP(proc, JlNativeApi.SetS(tuple, i, s[i], force_utf8: false));
		}
	}

	public static int Load(IntPtr tuple, out JlTupleString data, bool force_utf8)
	{
		int num = 2;
		JlNativeApi.GetTupleLength(tuple, out var length);
		string[] array = new string[length];
		for (int i = 0; i < length; i++)
		{
			if (!JlNativeApi.IsFailure(num))
			{
				num = JlNativeApi.GetS(tuple, i, out array[i], force_utf8);
			}
		}
		data = new JlTupleString(array, copy: false);
		return num;
	}
}
