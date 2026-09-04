namespace JLVisionLib;

internal class JlTupleElementsString : JlTupleElementsImplementation
{
	internal JlTupleElementsString(JlTupleString source, int index)
		: base(source, index)
	{
	}

	internal JlTupleElementsString(JlTupleString source, int[] indices)
		: base(source, indices)
	{
	}

	public override string[] getS()
	{
		if (indices == null)
		{
			return null;
		}
		string[] array = new string[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.SArr[indices[i]];
		}
		return array;
	}

	public override void setS(string[] s)
	{
		if (IsValidArrayForSetX(s))
		{
			bool flag = s.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				source.SArr[indices[i]] = s[(!flag) ? i : 0];
			}
		}
	}

	public override object[] getO()
	{
		if (indices == null)
		{
			return null;
		}
		object[] array = new object[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.SArr[indices[i]];
		}
		return array;
	}

	public override JlTupleType getType()
	{
		return JlTupleType.STRING;
	}
}
