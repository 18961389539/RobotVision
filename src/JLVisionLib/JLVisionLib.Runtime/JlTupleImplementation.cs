using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JLVisionLib;

internal abstract class JlTupleImplementation
{
	public const int INTEGER = 1;

	public const int DOUBLE = 2;

	public const int STRING = 4;

	public const int JlANDLE = 16;

	public const int ANY_ELEM = 23;

	public const int MIXED = 8;

	public const int ANY_TUPLE = 31;

	public const int LONG = 129;

	public const int FLOAT = 32898;

	public const int INTPTR = 32900;

	public const int BAN_IN_MIXED = 32768;

	protected Array data;

	protected int iLength;

	internal GCHandle pinHandle;

	internal int pinCount;

	protected Type typeI = typeof(int);

	protected Type typeL = typeof(long);

	protected Type typeD = typeof(double);

	protected Type typeS = typeof(string);

	protected Type typeH = typeof(JlHandle);

	protected Type typeO = typeof(object);

	protected Type typeF = typeof(float);

	protected Type typeIP = typeof(IntPtr);

	protected int Capacity => data.Length;

	public int Length => iLength;

	public virtual JlTupleType Type
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
	}

	public virtual int[] IArr
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
		set
		{
			throw new JlTupleAccessException(this);
		}
	}

	public virtual long[] LArr
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
		set
		{
			throw new JlTupleAccessException(this);
		}
	}

	public virtual double[] DArr
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
		set
		{
			throw new JlTupleAccessException(this);
		}
	}

	public virtual string[] SArr
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
		set
		{
			throw new JlTupleAccessException(this);
		}
	}

	public virtual JlHandle[] JlArr
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
		set
		{
			throw new JlTupleAccessException(this);
		}
	}

	public virtual object[] OArr
	{
		get
		{
			throw new JlTupleAccessException(this);
		}
		set
		{
			throw new JlTupleAccessException(this);
		}
	}

	public static int GetObjectType(object o)
	{
		if (o is int)
		{
			return 1;
		}
		if (o is long)
		{
			return 129;
		}
		if (o is double)
		{
			return 2;
		}
		if (o is float)
		{
			return 32898;
		}
		if (o is string)
		{
			return 4;
		}
		if (o is JlHandle)
		{
			return 16;
		}
		if (o is IntPtr)
		{
			return 32900;
		}
		if (o == null)
		{
			return 4;
		}
		return 31;
	}

	public static int GetObjectsType(object[] o)
	{
		if (o == null)
		{
			return 31;
		}
		int num = 31;
		int num2 = 31;
		for (int i = 0; i < o.Length; i++)
		{
			if (o[i] is int)
			{
				num = 1;
			}
			if (o[i] is long)
			{
				num = 129;
			}
			if (o[i] is double)
			{
				num = 2;
			}
			if (o[i] is float)
			{
				num = 32898;
			}
			if (o[i] is string)
			{
				num = 4;
			}
			if (o[i] is JlHandle)
			{
				num = 16;
			}
			if (o[i] is IntPtr)
			{
				num = 32900;
			}
			if (i == 0)
			{
				num2 = num;
			}
			else if (num != num2)
			{
				return 8;
			}
		}
		return num2;
	}

	public static JlTupleImplementation CreateInstanceForType(JlTupleType type, int size = 0)
	{
		return type switch
		{
			JlTupleType.EMPTY => JlTupleVoid.EMPTY, 
			JlTupleType.INTEGER => new JlTupleInt32(new int[size], copy: false), 
			JlTupleType.LONG => new JlTupleInt64(new long[size], copy: false), 
			JlTupleType.DOUBLE => new JlTupleDouble(new double[size], copy: false), 
			JlTupleType.STRING => new JlTupleString(new string[size], copy: false), 
			JlTupleType.JlANDLE => new JlTupleHandle(new JlHandle[size], copy: false), 
			JlTupleType.MIXED => new JlTupleMixed(new object[size], copy: false), 
			_ => throw new JlTupleAccessException("Unknown JlTupleType requested in TupleImplementation.CreateInstanceForType"), 
		};
	}

	internal virtual void PinTuple()
	{
	}

	internal void UnpinTuple()
	{
		Monitor.Enter(this);
		if (pinCount > 0)
		{
			pinCount--;
			if (pinCount == 0)
			{
				pinHandle.Free();
			}
		}
		Monitor.Exit(this);
	}

	protected abstract Array CreateArray(int size);

	protected void SetArray(Array source, bool copy)
	{
		if (source == null)
		{
			source = CreateArray(0);
		}
		if (copy)
		{
			data = CreateArray(source.Length);
			Array.Copy(source, data, source.Length);
		}
		else
		{
			data = source;
		}
		iLength = data.Length;
		NotifyArrayUpdate();
	}

	protected virtual void NotifyArrayUpdate()
	{
	}

	public virtual void Dispose()
	{
	}

	public virtual void AssertSize(int index)
	{
		if (index >= iLength)
		{
			if (index >= data.Length)
			{
				Array sourceArray = data;
				data = CreateArray(Math.Max(10, 2 * index));
				Array.Copy(sourceArray, data, iLength);
				NotifyArrayUpdate();
			}
			iLength = index + 1;
		}
	}

	public virtual void AssertSize(int[] indices)
	{
		int num;
		if (indices.Length == 0)
		{
			num = 0;
		}
		else
		{
			num = indices[0];
			foreach (int num2 in indices)
			{
				if (num2 > num)
				{
					num = num2;
				}
			}
		}
		AssertSize(num);
	}

	public virtual JlTupleElements GetElement(int index, JlTuple parent)
	{
		throw new JlTupleAccessException(this);
	}

	public virtual JlTupleElements GetElements(int[] indices, JlTuple parent)
	{
		if (indices == null || indices.Length == 0)
		{
			return new JlTupleElements();
		}
		throw new JlTupleAccessException(this);
	}

	public virtual void SetElements(int[] indices, JlTupleElements elements)
	{
		if (indices == null || indices.Length == 0)
		{
			return;
		}
		throw new JlTupleAccessException(this);
	}

	protected Array ToArray(Type t)
	{
		Array array = Array.CreateInstance(t, iLength);
		Array.Copy(data, array, iLength);
		return array;
	}

	public virtual int[] ToIArr()
	{
		throw new JlTupleAccessException(this, "Cannot convert to int array");
	}

	public virtual long[] ToLArr()
	{
		throw new JlTupleAccessException(this, "Cannot convert to long array");
	}

	public virtual double[] ToDArr()
	{
		throw new JlTupleAccessException(this, "Cannot convert to double array");
	}

	public virtual string[] ToSArr()
	{
		string[] array = new string[iLength];
		for (int i = 0; i < iLength; i++)
		{
			array[i] = data.GetValue(i).ToString();
		}
		return array;
	}

	public virtual JlHandle[] ToHArr()
	{
		throw new JlTupleAccessException(this, "Cannot convert to handle array");
	}

	public virtual object[] ToOArr()
	{
		return (object[])ToArray(typeO);
	}

	public virtual float[] ToFArr()
	{
		throw new JlTupleAccessException(this, "Cannot convert to float array");
	}

	public virtual IntPtr[] ToIPArr()
	{
		throw new JlTupleAccessException(this, "Values in tuple do not represent pointers on this platform");
	}

	public virtual int CopyToIArr(int[] dst, int offset)
	{
		Array array = ToArray(typeI);
		Array.Copy(array, 0, dst, offset, array.Length);
		return array.Length;
	}

	public virtual int CopyToLArr(long[] dst, int offset)
	{
		Array array = ToArray(typeL);
		Array.Copy(array, 0, dst, offset, array.Length);
		return array.Length;
	}

	public virtual int CopyToDArr(double[] dst, int offset)
	{
		Array array = ToArray(typeD);
		Array.Copy(array, 0, dst, offset, array.Length);
		return array.Length;
	}

	public virtual int CopyToSArr(string[] dst, int offset)
	{
		Array array = ToArray(typeS);
		Array.Copy(array, 0, dst, offset, array.Length);
		return array.Length;
	}

	public virtual int CopyToHArr(JlHandle[] dst, int offset)
	{
		Array array = ToArray(typeH);
		Array.Copy(array, 0, dst, offset, array.Length);
		return array.Length;
	}

	public virtual int CopyToOArr(object[] dst, int offset)
	{
		Array.Copy(data, 0, dst, offset, iLength);
		return data.Length;
	}

	public abstract int CopyFrom(JlTupleImplementation impl, int offset);

	public virtual void Store(IntPtr proc, int parIndex)
	{
		JlTupleType type;
		switch (Type)
		{
		case JlTupleType.INTEGER:
		case JlTupleType.LONG:
			type = JlTupleType.INTEGER;
			break;
		case JlTupleType.DOUBLE:
		case JlTupleType.STRING:
		case JlTupleType.MIXED:
		case JlTupleType.JlANDLE:
			type = Type;
			break;
		default:
			type = JlTupleType.MIXED;
			break;
		}
		JlNativeApi.JlCkP(proc, JlNativeApi.CreateInputTuple(proc, parIndex, iLength, type, out var tuple));
		StoreData(proc, tuple);
	}

	protected abstract void StoreData(IntPtr proc, IntPtr tuple);

	public static int Load(IntPtr proc, int parIndex, JlTupleType type, out JlTupleImplementation data)
	{
		JlNativeApi.GetOutputTuple(proc, parIndex, handleType: false, out var tuple);
		return LoadData(tuple, type, out data, force_utf8: false);
	}

	public static int LoadData(IntPtr tuple, JlTupleType type, out JlTupleImplementation data, bool force_utf8)
	{
		int result = 2;
		if (tuple == IntPtr.Zero)
		{
			data = JlTupleVoid.EMPTY;
			return result;
		}
		JlNativeApi.GetTupleTypeScanElem(tuple, out var type2);
		switch (type2)
		{
		case 31:
			data = JlTupleVoid.EMPTY;
			type = JlTupleType.EMPTY;
			break;
		case 1:
			if (JlNativeApi.isPlatform64)
			{
				result = JlTupleInt64.Load(tuple, out var hTupleInt);
				data = hTupleInt;
			}
			else
			{
				result = JlTupleInt32.Load(tuple, out var hTupleInt2);
				data = hTupleInt2;
			}
			type = JlTupleType.INTEGER;
			break;
		case 2:
		{
			result = JlTupleDouble.Load(tuple, out var hTupleDouble);
			data = hTupleDouble;
			type = JlTupleType.DOUBLE;
			break;
		}
		case 4:
		{
			result = JlTupleString.Load(tuple, out var hTupleString, force_utf8);
			data = hTupleString;
			type = JlTupleType.STRING;
			break;
		}
		case 16:
		{
			result = JlTupleHandle.Load(tuple, out var hTupleHandle);
			data = hTupleHandle;
			type = JlTupleType.JlANDLE;
			break;
		}
		case 23:
		{
			result = JlTupleMixed.Load(tuple, out var hTupleMixed, force_utf8);
			data = hTupleMixed;
			type = JlTupleType.MIXED;
			break;
		}
		default:
			data = JlTupleVoid.EMPTY;
			result = 7002;
			break;
		}
		return result;
	}
}
