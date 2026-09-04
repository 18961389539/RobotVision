namespace JLVisionLib;

internal class JlTupleElementsInt64 : JlTupleElementsImplementation
{
	/// <summary>
	/// 以单个元素视图创建 64 位整数元素封装。
	/// </summary>
	/// <remarks>
	/// <para>底层的 <c>source</c> 与 <c>index</c> 传给基类的单元素构造函数，<c>indices</c> 被初始化为仅含该下标的一维数组。</para>
	/// </remarks>
	/// <param name="source">承载 64 位整数数组的源元组。</param>
	/// <param name="index">本视图锁定的单个元素下标。</param>
	internal JlTupleElementsInt64(JlTupleInt64 source, int index)
		: base(source, index)
	{
	}

	/// <summary>
	/// 以多个下标创建 64 位整数元素封装。
	/// </summary>
	/// <remarks>
	/// <para>底层的 <c>source</c> 与 <c>indices</c> 原样传给基类多元素构造函数。</para>
	/// </remarks>
	/// <param name="source">承载 64 位整数数组的源元组。</param>
	/// <param name="indices">本视图锁定的下标序列，可为 <see langword="null"/>。</param>
	internal JlTupleElementsInt64(JlTupleInt64 source, int[] indices)
		: base(source, indices)
	{
	}

	/// <summary>
	/// 将锁定的 64 位整数元素以 <see cref="int"/> 数组形式读出。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：按 <c>indices</c> 顺序读取 <c>source</c> 的 <c>LArr</c>，并把每个 <see cref="long"/> 强制转换为 <see cref="int"/>。</para>
	/// <para><b>资源与坑</b>：当 <c>indices</c> 为 <see langword="null"/> 时直接返回 <see langword="null"/>；当元素值超出 <see cref="int"/> 范围（|值| 大于 2147483647）时会发生截断丢失高位，返回结果不再等于原值。</para>
	/// </remarks>
	/// <returns>每个元素按 <see cref="long"/>→<see cref="int"/> 取整截断后的数组；<c>indices</c> 为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
	public override int[] getI()
	{
		if (indices == null)
		{
			return null;
		}
		int[] array = new int[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = (int)source.LArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 将输入的 <see cref="int"/> 值写入锁定的 64 位整数元素。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：先把输入的 <see cref="int"/> 隐式提升为 <see cref="long"/> 后写入 <c>source</c> 的 <c>LArr</c>。</para>
	/// <para><b>广播规则</b>：长度为 1 的数组应用到所有下标；否则数组长度必须与下标个数匹配，否则由基类抛出 <see cref="JlTupleAccessException"/>（校验见 <c>IsValidArrayForSetX</c>）。</para>
	/// </remarks>
	/// <param name="i">待写入的 <see cref="int"/> 数组（长度需为一或与下标个数相等）。</param>
	public override void setI(int[] i)
	{
		if (IsValidArrayForSetX(i))
		{
			bool flag = i.Length == 1;
			for (int j = 0; j < indices.Length; j++)
			{
				source.LArr[indices[j]] = i[(!flag) ? j : 0];
			}
		}
	}

	/// <summary>
	/// 将锁定的 64 位整数元素以 <see cref="long"/> 数组形式原样读出。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：按 <c>indices</c> 顺序读取 <c>source</c> 的 <c>LArr</c>，是当前视图最自然的读取方式，不做任何类型转换。</para>
	/// <para><b>资源与坑</b>：当 <c>indices</c> 为 <see langword="null"/> 时直接返回 <see langword="null"/>。</para>
	/// </remarks>
	/// <returns>按各元素原值返回的数组；<c>indices</c> 为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
	public override long[] getL()
	{
		if (indices == null)
		{
			return null;
		}
		long[] array = new long[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.LArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 将输入的 <see cref="long"/> 值写入锁定的 64 位整数元素。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：按 <c>indices</c> 顺序把 <c>l</c> 写入 <c>source</c> 的 <c>LArr</c>，是当前视图最自然的写入方式。</para>
	/// <para><b>广播规则</b>：长度为 1 的数组应用到所有下标；否则数组长度必须与下标个数匹配，否则由基类抛出 <see cref="JlTupleAccessException"/>（校验见 <c>IsValidArrayForSetX</c>）。</para>
	/// </remarks>
	/// <param name="l">待写入的 <see cref="long"/> 数组（长度需为一或与下标个数相等）。</param>
	public override void setL(long[] l)
	{
		if (IsValidArrayForSetX(l))
		{
			bool flag = l.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				source.LArr[indices[i]] = l[(!flag) ? i : 0];
			}
		}
	}

	/// <summary>
	/// 将锁定的 64 位整数元素以 <see cref="double"/> 数组形式读出。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：按 <c>indices</c> 顺序读取 <c>source</c> 的 <c>LArr</c>，并把每个 <see cref="long"/> 隐式转换为 <see cref="double"/>。</para>
	/// <para><b>资源与坑</b>：当 <c>indices</c> 为 <see langword="null"/> 时直接返回 <see langword="null"/>；受 <see cref="double"/> 精度限制（约 53 位有效二进制位），绝对值超过 2^53 的整数值在转换后可能无法精确表示。</para>
	/// </remarks>
	/// <returns>每个元素转为 <see cref="double"/> 后的数组；<c>indices</c> 为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
	public override double[] getD()
	{
		if (indices == null)
		{
			return null;
		}
		double[] array = new double[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.LArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 将锁定的 64 位整数元素以装箱的 <see cref="object"/> 数组形式读出。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：按 <c>indices</c> 顺序读取 <c>source</c> 的 <c>LArr</c>，将每个 <see cref="long"/> 装箱为 <see cref="object"/>。</para>
	/// <para><b>资源与坑</b>：当 <c>indices</c> 为 <see langword="null"/> 时直接返回 <see langword="null"/>；装箱产生额外分配，适合仅在需要统一类型容器时使用。</para>
	/// </remarks>
	/// <returns>每个元素装箱为 <see cref="object"/> 后的数组；<c>indices</c> 为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
	public override object[] getO()
	{
		if (indices == null)
		{
			return null;
		}
		object[] array = new object[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.LArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 返回本元素视图对应的元组类型为 <see cref="JlTupleType.LONG"/>。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：覆盖基类虚方法，固定返回 <see cref="JlTupleType.LONG"/>，声明本视图承载的是 64 位整数。对本类支持的 <c>getX/setX</c>（I/L/D/O）不会走到基类默认的类型不匹配异常路径。</para>
	/// </remarks>
	/// <returns>恒为 <see cref="JlTupleType.LONG"/>。</returns>
	public override JlTupleType getType()
	{
		return JlTupleType.LONG;
	}
}
