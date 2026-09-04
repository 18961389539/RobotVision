using System;

namespace JLVisionLib;

internal class JlTupleVoid : JlTupleImplementation
{
	public static JlTupleVoid EMPTY = new JlTupleVoid();

	public override JlTupleType Type => JlTupleType.EMPTY;

	protected override Array CreateArray(int size)
	{
		return new int[0];
	}

	private JlTupleVoid()
	{
		SetArray(null, copy: false);
	}

	public override void AssertSize(int index)
	{
		throw new JlTupleAccessException(this);
	}

	public override int[] ToIArr()
	{
		return new int[0];
	}

	public override long[] ToLArr()
	{
		return new long[0];
	}

	public override double[] ToDArr()
	{
		return new double[0];
	}

	public override float[] ToFArr()
	{
		return new float[0];
	}

	public override IntPtr[] ToIPArr()
	{
		return new IntPtr[0];
	}

	public override int CopyToIArr(int[] dst, int offset)
	{
		return 0;
	}

	public override int CopyToLArr(long[] dst, int offset)
	{
		return 0;
	}

	public override int CopyToDArr(double[] dst, int offset)
	{
		return 0;
	}

	public override int CopyToSArr(string[] dst, int offset)
	{
		return 0;
	}

	public override int CopyToHArr(JlHandle[] dst, int offset)
	{
		return 0;
	}

	public override int CopyToOArr(object[] dst, int offset)
	{
		return 0;
	}

	public override int CopyFrom(JlTupleImplementation impl, int offset)
	{
		return 0;
	}

	protected override void StoreData(IntPtr proc, IntPtr tuple)
	{
	}
}
