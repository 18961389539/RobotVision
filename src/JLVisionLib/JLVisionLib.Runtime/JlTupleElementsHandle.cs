namespace JLVisionLib;

/// <summary>
/// 句柄元组的元素访问句柄实现类，负责按一组下标读写句柄元组中的 JlHandle 元素。
/// <para><b>功能说明</b>：将一组下标（indices）与源句柄元组（source）绑定，通过
/// <c>getX</c>/<c>setX</c> 系列方法按下标批量读写被选中的元素；句柄元组中每个槽位均为
/// <see cref="JlHandle"/>，因此本类的 <see cref="getType"/> 恒返回
/// <see cref="JlTupleType.JlANDLE"/>。</para>
/// </summary>
internal class JlTupleElementsHandle : JlTupleElementsImplementation
{
	/// <summary>
	/// 使用单个下标构造句柄元组的元素访问句柄。
	/// </summary>
	/// <param name="source">源句柄元组。</param>
	/// <param name="index">要访问的单个元素下标。</param>
	internal JlTupleElementsHandle(JlTupleHandle source, int index)
		: base(source, index)
	{
	}

	/// <summary>
	/// 使用多个下标构造句柄元组的元素访问句柄。
	/// </summary>
	/// <param name="source">源句柄元组。</param>
	/// <param name="indices">要访问的元素下标数组。</param>
	internal JlTupleElementsHandle(JlTupleHandle source, int[] indices)
		: base(source, indices)
	{
	}

	/// <summary>
	/// 按选中下标读取句柄元素数组。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：返回新数组，其元素依次为源元组在下标
	/// <c>indices[i]</c> 处的 <see cref="JlHandle"/> 引用（非克隆）。</para>
	/// <para><b>资源与坑</b>：当 <see cref="JlTupleElementsImplementation.indices"/>
	/// 为 null 时返回 null（与空句柄语义一致），否则始终返回与选中下标个数等长的数组。</para>
	/// </remarks>
	/// <returns>选中下标的句柄数组；该元素访问句柄未绑定下标时返回 null。</returns>
	public override JlHandle[] getH()
	{
		if (indices == null)
		{
			return null;
		}
		JlHandle[] array = new JlHandle[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.JlArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 尝试将句柄元素按整数读取。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：句柄是原生指针的弱引用，不允许隐式转换为整数。</para>
	/// <para><b>资源与坑</b>：当 <see cref="JlTupleElementsImplementation.indices"/>
	/// 为 null 时返回 null；否则抛 <see cref="JlTupleAccessException"/>，提示应使用
	/// <c>*.H.Handle</c> 获取 IntPtr 值。</para>
	/// </remarks>
	/// <returns>此方法不返回有效结果；仅在元素访问句柄未绑定下标时返回 null。</returns>
	public override int[] getI()
	{
		if (indices == null)
		{
			return null;
		}
		throw new JlTupleAccessException("Implicit access to handle as number is not allowed. Use *.H.Handle to get IntPtr value.");
	}

	/// <summary>
	/// 尝试将句柄元素按长整数读取。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：句柄是原生指针的弱引用，不允许隐式转换为整数。</para>
	/// <para><b>资源与坑</b>：当 <see cref="JlTupleElementsImplementation.indices"/>
	/// 为 null 时返回 null；否则抛 <see cref="JlTupleAccessException"/>，提示应使用
	/// <c>*.H.Handle</c> 获取 IntPtr 值。</para>
	/// </remarks>
	/// <returns>此方法不返回有效结果；仅在元素访问句柄未绑定下标时返回 null。</returns>
	public override long[] getL()
	{
		if (indices == null)
		{
			return null;
		}
		throw new JlTupleAccessException("Implicit access to handle as number is not allowed. Use *.H.Handle to get IntPtr value.");
	}

	/// <summary>
	/// 按广播规则向选中的下标批量写入句柄元素。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：<paramref name="h"/> 长度为 1 时，该值广播应用到所有被选中的
	/// 下标；否则 <paramref name="h"/> 长度必须与选中下标个数一致，逐位对应写入。</para>
	/// <para><b>资源与坑</b>：写入前会先 Dispose 被覆盖位置原有的 <see cref="JlHandle"/>
	/// （若非空），随后存入 <c>new JlHandle(...)</c> 的副本，避免与入参共享同一句柄实例。
	/// 当 <paramref name="h"/> 为 null 时静默返回；当长度既不为 1 也不等于选中下标个数时，
	/// <see cref="JlTupleElementsImplementation.IsValidArrayForSetX"/> 会抛
	/// <see cref="JlTupleAccessException"/>。</para>
	/// </remarks>
	/// <param name="h">要写入的句柄数组（长度须为 1 或等于选中下标个数）。</param>
	public override void setH(JlHandle[] h)
	{
		if (!IsValidArrayForSetX(h))
		{
			return;
		}
		bool flag = h.Length == 1;
		for (int i = 0; i < indices.Length; i++)
		{
			if (source.JlArr[indices[i]] != null)
			{
				source.JlArr[indices[i]].Dispose();
			}
			source.JlArr[indices[i]] = new JlHandle(h[(!flag) ? i : 0]);
		}
	}

	/// <summary>
	/// 按选中下标读取句柄元素（以 object 数组形式返回）。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：返回新数组，其元素依次为源元组在下标
	/// <c>indices[i]</c> 处的 <see cref="JlHandle"/>（以 object 装箱存放）。</para>
	/// <para><b>资源与坑</b>：当 <see cref="JlTupleElementsImplementation.indices"/>
	/// 为 null 时返回 null，否则始终返回与选中下标个数等长的数组。</para>
	/// </remarks>
	/// <returns>选中下标的 object 元素数组；该元素访问句柄未绑定下标时返回 null。</returns>
	public override object[] getO()
	{
		if (indices == null)
		{
			return null;
		}
		object[] array = new object[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.JlArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	/// 返回本元素访问句柄元素的类型枚举。
	/// </summary>
	/// <remarks>
	/// <para><b>功能说明</b>：句柄元组的槽位全部为 <see cref="JlHandle"/>，
	/// 故恒返回 <see cref="JlTupleType.JlANDLE"/>。</para>
	/// </remarks>
	/// <returns>恒为 <see cref="JlTupleType.JlANDLE"/>。</returns>
	public override JlTupleType getType()
	{
		return JlTupleType.JlANDLE;
	}
}
