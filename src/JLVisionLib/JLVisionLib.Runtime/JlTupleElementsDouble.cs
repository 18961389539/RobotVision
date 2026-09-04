namespace JLVisionLib;

/// <summary>针对 <see cref="JlTupleDouble"/> 的按索引/多索引读取与写入实现（内部类）。</summary>
/// <remarks>
///   <para><b>功能说明</b>：在双精度元组之上，按单个下标或一组下标对元素做读取（<see cref="getD"/>/<see cref="getO"/>）
///   与写入（<see cref="setD"/>）。<c>indices</c> 为 <c>null</c> 时表示"整段"，读取返回 <c>null</c>。</para>
///   <para><b>写入广播语义</b>：<see cref="setD"/> 在赋值数组长度为 1 时把该单值广播到所有被选中下标，
///   否则按位置一一对应；元素类型口径始终为 <see cref="JlTupleType.DOUBLE"/>。</para>
/// </remarks>
internal class JlTupleElementsDouble : JlTupleElementsImplementation
{
	/// <summary>以单个下标构造针对源元组元素的访问器。</summary>
	internal JlTupleElementsDouble(JlTupleDouble source, int index)
		: base(source, index)
	{
	}

	/// <summary>以一组下标构造针对源元组元素的访问器（<c>indices</c> 为 null 表示整段）。</summary>
	internal JlTupleElementsDouble(JlTupleDouble source, int[] indices)
		: base(source, indices)
	{
	}

	/// <summary>读取被选中下标处的 double 值数组；整段访问（<c>indices == null</c>）时返回 <c>null</c>。</summary>
	public override double[] getD()
	{
		if (indices == null)
		{
			return null;
		}
		double[] array = new double[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.DArr[indices[i]];
		}
		return array;
	}

	public override void setD(double[] d)
	{
		if (IsValidArrayForSetX(d))
		{
			bool flag = d.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				source.DArr[indices[i]] = d[(!flag) ? i : 0];
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
			array[i] = source.DArr[indices[i]];
		}
		return array;
	}

	public override JlTupleType getType()
	{
		return JlTupleType.DOUBLE;
	}
}
