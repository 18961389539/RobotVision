namespace JLVisionLib;

/// <summary>
/// 面向 32 位整数（int）元组元素的元素访问器实现。
/// </summary>
/// <remarks>
/// <para><b>功能说明</b>：本类是 <see cref="JlTupleInt32"/> 的元素视图，负责按 <c>indices</c> 描述的下标集合，
/// 从底层 <c>source.IArr</c>（int 数组）中批量读写 <see cref="int"/> 元素，并据各访问器的目标类型做宽度提升或截断。</para>
/// <para>它继承自 <see cref="JlTupleElementsImplementation"/>，因此 setX 遵循广播规则、getX 在 <c>indices</c> 为 null 时返回 null，
/// 这些通用语义由基类统一定义，本文件只覆盖与本类型兼容的映射。</para>
/// <para><b>典型场景</b>：内部构造元素处理器（<see cref="JlTupleImplementation"/> 系列）时统一管理下标与类型，供元组索引/切片访问复用。</para>
/// </remarks>
internal class JlTupleElementsInt32 : JlTupleElementsImplementation
{
	/// <summary>
	/// 用单个下标构造指向 <see cref="JlTupleInt32"/> 单个 int 元素的访问器。
	/// </summary>
	/// <remarks>
	/// <para>底层将 <paramref name="index"/> 包装为长度为 1 的下标数组，等价于访问该元组的单一元素。</para>
	/// <para>两参数版构造函数选择：单个下标时使用 <paramref name="index"/> 下标，否则使用下标数组，二者不可混用。</para>
	/// </remarks>
	/// <param name="source">被访问的 int 元组。</param>
	/// <param name="index">目标元素在元组中的单个下标。</param>
	internal JlTupleElementsInt32(JlTupleInt32 source, int index)
		: base(source, index)
	{
	}

	/// <summary>
	/// 用下标数组构造指向 <see cref="JlTupleInt32"/> 多个 int 元素的访问器。
	/// </summary>
	/// <remarks>
	/// <para>按 <paramref name="indices"/> 指定的每个下标逐一对应一个 int 元素；下标个数即元素个数。</para>
	/// </remarks>
	/// <param name="source">被访问的 int 元组。</param>
	/// <param name="indices">目标元素下标数组。</param>
	internal JlTupleElementsInt32(JlTupleInt32 source, int[] indices)
		: base(source, indices)
	{
	}

	/// <summary>
	/// 按下标读取 int 元素并作为 int 数组返回。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：逐个按 <c>indices</c> 下标从 <c>source.IArr</c> 读取 int 值，返回与下标个数等长的 int 数组。</para>
	/// <para><b>资源与坑</b>：当 <c>indices</c> 为 null 时返回 null，而非空数组；索引越界会抛出运行时异常。</para>
	/// </remarks>
	/// <returns>读取到的 int 元素数组；<c>indices</c> 为 null 时返回 null。</returns>
	public override int[] getI()
	{
		if (indices == null)
		{
			return null;
		}
		int[] array = new int[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.IArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 将 int 数组写回元组的 int 元素下标位置。
	/// </summary>
	/// <remarks>
	/// <para><b>广播规则</b>：<paramref name="i"/> 长度为 1 时该值应用到所有目标下标；长度等于下标个数时按位对应写入；
	/// 其他长度（或为 null）由基类按 <see cref="JlTupleElementsImplementation.IsValidArrayForSetX"/> 校验并抛 <see cref="JlTupleAccessException"/>。</para>
	/// <para><b>类型兼容</b>：目标元素为 int，故本方法为精确写入，无精度损失。</para>
	/// </remarks>
	/// <param name="i">待写入的 int 元素数组。</param>
	public override void setI(int[] i)
	{
		if (IsValidArrayForSetX(i))
		{
			bool flag = i.Length == 1;
			for (int j = 0; j < indices.Length; j++)
			{
				source.IArr[indices[j]] = i[(!flag) ? j : 0];
			}
		}
	}

	/// <summary>
	/// 按下标读取 int 元素并经拓宽转换为 long 数组返回。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：从 <c>source.IArr</c> 读取 int 值，再拓宽为 long 返回，属安全提升、无精度损失。</para>
	/// <para><b>资源与坑</b>：<c>indices</c> 为 null 时返回 null；读出的范围受 int 取值范围（32 位）约束。</para>
	/// </remarks>
	/// <returns>读取到的 long 元素数组；<c>indices</c> 为 null 时返回 null。</returns>
	public override long[] getL()
	{
		if (indices == null)
		{
			return null;
		}
		long[] array = new long[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.IArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 将 long 数组按下标写回元组的 int 元素位置。
	/// </summary>
	/// <remarks>
	/// <para><b>广播规则</b>：<paramref name="l"/> 长度为 1 时该值应用到所有目标下标，否则须等于下标个数。</para>
	/// <para><b>资源与坑</b>：写入时执行 <c>(int)l[..]</c> 强转，long 值超出 int 取值范围会被截断（例如 2^31 及以上/负数溢出），存在精度损失；
	/// 写入后元素以 int 存储，再次通过 getL() 读回的范围将退回 int 范围内。</para>
	/// <para>长度或 null 校验由基类 <see cref="JlTupleElementsImplementation.IsValidArrayForSetX"/> 负责并抛 <see cref="JlTupleAccessException"/>。</para>
	/// </remarks>
	/// <param name="l">待写入的 long 元素数组。</param>
	public override void setL(long[] l)
	{
		if (IsValidArrayForSetX(l))
		{
			bool flag = l.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				source.IArr[indices[i]] = (int)l[(!flag) ? i : 0];
			}
		}
	}

	/// <summary>
	/// 按下标读取 int 元素并经拓宽转换为 double 数组返回。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：从 <c>source.IArr</c> 读取 int 值并提升为 double 返回；int→double 宽度提升，int 的 32 位整数可被 double 精确表示。</para>
	/// <para><b>资源与坑</b>：<c>indices</c> 为 null 时返回 null；不会做向 int 的缩小转换。</para>
	/// </remarks>
	/// <returns>读取到的 double 元素数组；<c>indices</c> 为 null 时返回 null。</returns>
	public override double[] getD()
	{
		if (indices == null)
		{
			return null;
		}
		double[] array = new double[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.IArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 按下标读取 int 元素并以装箱后的 object 数组返回。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：从 <c>source.IArr</c> 读取 int 值并装箱为 object 返回，便于统一处理异构元素。</para>
	/// <para><b>资源与坑</b>：<c>indices</c> 返回 null 时为 null；每个元素都发生一次 box 操作。</para>
	/// </remarks>
	/// <returns>读取到的 object 元素数组；<c>indices</c> 为 null 时返回 null。</returns>
	public override object[] getO()
	{
		if (indices == null)
		{
			return null;
		}
		object[] array = new object[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.IArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 返回本元素访问器对应的类型标识。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：本类承载的是 32 位整数元素，固定返回 <see cref="JlTupleType.INTEGER"/>。</para>
	/// <para>与基类默认实现（空下标返回 EMPTY，否则抛 <see cref="JlTupleAccessException"/>）不同，本类始终报告整数类型。</para>
	/// </remarks>
	/// <returns>恒为 <see cref="JlTupleType.INTEGER"/>。</returns>
	public override JlTupleType getType()
	{
		return JlTupleType.INTEGER;
	}
}