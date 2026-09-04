using System;

namespace JLVisionLib;

internal class JlTupleMixed : JlTupleImplementation
{
	protected object[] o;

	public override object[] OArr
	{
		get
		{
			return o;
		}
		set
		{
			SetArray(value, copy: false);
		}
	}

	public override JlTupleType Type => JlTupleType.MIXED;

	protected override Array CreateArray(int size)
	{
		object[] array = new object[size];
		for (int i = 0; i < size; i++)
		{
			array[i] = 0;
		}
		return array;
	}

	protected override void NotifyArrayUpdate()
	{
		o = (object[])data;
	}

	public JlTupleMixed(JlTupleImplementation data)
		: this(data.ToOArr(), copy: false)
	{
	}

	public JlTupleMixed(object o)
		: this(new object[1] { o }, copy: false)
	{
	}

	public JlTupleMixed(object[] o, bool copy)
	{
		if (copy)
		{
			object[] array = new object[o.Length];
			for (int i = 0; i < o.Length; i++)
			{
				if (o[i] != null)
				{
					int objectType = JlTupleImplementation.GetObjectType(o[i]);
					if (objectType == 31 || (objectType & 0x8000) > 0)
					{
						throw new JlTupleAccessException("Encountered invalid data types when creating JlTuple");
					}
					if (objectType == 16)
					{
						array[i] = new JlHandle((JlHandle)o[i]);
					}
					else
					{
						array[i] = o[i];
					}
				}
			}
			SetArray(array, copy: false);
			return;
		}
		for (int j = 0; j < o.Length; j++)
		{
			if (o[j] != null)
			{
				int objectType2 = JlTupleImplementation.GetObjectType(o[j]);
				if (objectType2 == 31 || (objectType2 & 0x8000) > 0)
				{
					throw new JlTupleAccessException("Encountered invalid data types when creating JlTuple");
				}
			}
		}
		SetArray(o, copy: false);
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
		new JlTupleElementsMixed(this, indices).setO(elements.OArr);
	}

	public override void Dispose()
	{
		for (int i = 0; i < base.Length; i++)
		{
			if (GetElementType(i) == JlTupleType.JlANDLE)
			{
				((JlHandle)o[i]).Dispose();
			}
		}
	}

	public JlTupleType GetElementType(int index)
	{
		return (JlTupleType)JlTupleImplementation.GetObjectType(o[index]);
	}

	public JlTupleType GetElementType(int[] indices)
	{
		if (indices == null || indices.Length == 0)
		{
			return JlTupleType.EMPTY;
		}
		JlTupleType objectType = (JlTupleType)JlTupleImplementation.GetObjectType(o[indices[0]]);
		for (int i = 1; i < indices.Length; i++)
		{
			if (JlTupleImplementation.GetObjectType(o[indices[i]]) != (int)objectType)
			{
				return JlTupleType.MIXED;
			}
		}
		return objectType;
	}

	public override JlHandle[] ToHArr()
	{
		for (int i = 0; i < base.Length; i++)
		{
			if (GetElementType(i) != JlTupleType.JlANDLE)
			{
				throw new JlTupleAccessException(this, "Copy of mixed tuple is only allowed with handle types");
			}
		}
		JlHandle[] array = new JlHandle[base.Length];
		for (int j = 0; j < base.Length; j++)
		{
			array[j] = new JlHandle((JlHandle)o[j]);
		}
		return array;
	}

	public override object[] ToOArr()
	{
		object[] array = new object[iLength];
		CopyToOArr(array, 0);
		return array;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		for (int i = 0; i < iLength; i++)
		{
			if (o[i] is JlHandle)
			{
				dst[i + offset] = new JlHandle((JlHandle)o[i]);
			}
			else
			{
				dst[i + offset] = o[i];
			}
		}
		return iLength;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return impl.CopyToOArr(o, offset);
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
		for (int i = 0; i < iLength; i++)
		{
			switch (JlTupleImplementation.GetObjectType(o[i]))
			{
			case 1:
				JlNativeApi.JlCkP(proc, JlNativeApi.SetI(tuple, i, (int)o[i]));
				break;
			case 129:
				JlNativeApi.JlCkP(proc, JlNativeApi.SetL(tuple, i, (long)o[i]));
				break;
			case 2:
				JlNativeApi.JlCkP(proc, JlNativeApi.SetD(tuple, i, (double)o[i]));
				break;
			case 4:
				JlNativeApi.JlCkP(proc, JlNativeApi.SetS(tuple, i, (string)o[i], force_utf8: false));
				break;
			case 16:
				JlNativeApi.JlCkP(proc, JlNativeApi.SetH(tuple, i, (JlHandle)o[i]));
				break;
			}
		}
	}

	public static int Load(IntPtr tuple, out JlTupleMixed data, bool force_utf8)
	{
		int num = 2;
		JlNativeApi.GetTupleLength(tuple, out var length);
		object[] array = new object[length];
		for (int i = 0; i < length; i++)
		{
			if (JlNativeApi.IsFailure(num))
			{
				continue;
			}
			JlNativeApi.GetElementType(tuple, i, out var type);
			switch (type)
			{
			case JlTupleType.INTEGER:
				if (JlNativeApi.isPlatform64)
				{
					num = JlNativeApi.GetL(tuple, i, out var longValue);
					array[i] = longValue;
				}
				else
				{
					num = JlNativeApi.GetI(tuple, i, out var intValue);
					array[i] = intValue;
				}
				break;
			case JlTupleType.DOUBLE:
			{
				num = JlNativeApi.GetD(tuple, i, out var doubleValue);
				array[i] = doubleValue;
				break;
			}
			case JlTupleType.STRING:
			{
				num = JlNativeApi.GetS(tuple, i, out var stringValue, force_utf8);
				array[i] = stringValue;
				break;
			}
			case JlTupleType.JlANDLE:
			{
				num = JlNativeApi.GetH(tuple, i, out var handle);
				array[i] = handle;
				break;
			}
			}
		}
		data = new JlTupleMixed(array, copy: false);
		return num;
	}
}
