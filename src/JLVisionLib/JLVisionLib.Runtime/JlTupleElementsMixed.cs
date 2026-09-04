using System;

namespace JLVisionLib;

/// <summary>
///   混合类型元组（<see cref="JlTupleType.MIXED"/>）的元素访问实现，允许读写不同类型（int/long/double/string/JlHandle）的容器元素。
/// </summary>
/// <remarks>
///   <para>
///     <b>功能说明</b>：与同构元组的兄弟类（JlTupleElementsString / JlTupleElementsHandle）不同，混合元组的每个下标可独立持有任意受支持类型，
///     底层数据保存在 <c>source.OArr</c>——一个 <c>object[]</c> 装箱数组。读写时按目标元素的实际运行时类型做相应装箱/拆箱转换。
///   </para>
///   <para>
///     <b>典型场景</b>：既要求兼容异构数据、又希望以强类型数组方式（I/L/D/S/H/O）访问片段时使用。
///   </para>
///   <para>
///     <b>资源与坑</b>：这是类型最灵活的实现，因此也最容易读/写错类型。各 getX 会先把 <c>getType()</c> 判定为的类型显式转换到目标类型；
///     下标所指元素类型与请求不符时（如 JlHandle 被当作数值读取）会抛 <see cref="JlTupleAccessException"/>。
///     单个下标返回的容器会持有一个或多个下标（indices 数组），见各个成员注释。
///   </para>
/// </remarks>
internal class JlTupleElementsMixed : JlTupleElementsImplementation
{
	/// <summary>
	///   以单个下标构造混合类型元素的访问器。
	/// </summary>
	/// <param name="source">底层混合元组实例，其 <c>OArr</c> 提供装箱元素数组。</param>
	/// <param name="index">所指向的单一元素下标。</param>
	internal JlTupleElementsMixed(JlTupleMixed source, int index)
		: base(source, index)
	{
	}

	/// <summary>
	///   以多个下标构造混合类型元素的访问器。
	/// </summary>
	/// <param name="source">底层混合元组实例，其 <c>OArr</c> 提供装箱元素数组。</param>
	/// <param name="indices">所指向的一组元素下标；为 null 或空数组时表示空访问。</param>
	internal JlTupleElementsMixed(JlTupleMixed source, int[] indices)
		: base(source, indices)
	{
	}

	/// <summary>
	///   将当前下标的元素读取为 int 值数组。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>类型约束</b>：<c>getType()</c> 判定为 <see cref="JlTupleType.INTEGER"/> 时按 int 拆箱；判定为 <see cref="JlTupleType.LONG"/> 时先将装箱的
	///     long 强转 int（<c>(int)(long)</c>）再返回。
	///   </para>
	///   <para>
	///     <b>越界错误</b>：当元素实际为 JlHandle 时，不允许把句柄隐式当作数值，抛出提示 <c>Use *.H.Handle to get IntPtr value</c> 的
	///     <see cref="JlTupleAccessException"/>；当元素为 string / double / mixed（取值不一致）时抛 <c>Mixed tuple does not contain integer</c> 异常。
	///   </para>
	///   <para><b>null 返回</b>：indices 为 null 时返回 null。</para>
	/// </remarks>
	public override int[] getI()
	{
		if (indices == null)
		{
			return null;
		}
		switch (getType())
		{
		case JlTupleType.INTEGER:
		{
			int[] array2 = new int[indices.Length];
			for (int j = 0; j < indices.Length; j++)
			{
				array2[j] = (int)source.OArr[indices[j]];
			}
			return array2;
		}
		case JlTupleType.LONG:
		{
			int[] array = new int[indices.Length];
			for (int i = 0; i < indices.Length; i++)
			{
				array[i] = (int)(long)source.OArr[indices[i]];
			}
			return array;
		}
		case JlTupleType.JlANDLE:
			throw new JlTupleAccessException("Implicit access to handle as number is not allowed. Use *.H.Handle to get IntPtr value.");
		default:
			throw new JlTupleAccessException(source, "Mixed tuple does not contain integer " + ((indices.Length == 1) ? ("value at index " + indices[0]) : "values at given indices"));
		}
	}

	/// <summary>
	///   将当前下标的元素读取为 long 值数组。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>类型约束</b>：<c>getType()</c> 判定为 <see cref="JlTupleType.INTEGER"/> 时先将 int 拆箱再赋值给 long（宽度提升）；
	///     判定为 <see cref="JlTupleType.LONG"/> 时直接按 long 拆箱。
	///   </para>
	///   <para>
	///     <b>越界错误</b>：元素为 JlHandle 时拒绝隐式转数值并抛异常（见 <see cref="getI"/>）；
	///     元素为 string / double / mixed 时抛 <c>Mixed tuple does not contain integer</c> 异常。
	///   </para>
	///   <para><b>null 返回</b>：indices 为 null 时返回 null。</para>
	/// </remarks>
	public override long[] getL()
	{
		if (indices == null)
		{
			return null;
		}
		switch (getType())
		{
		case JlTupleType.INTEGER:
		{
			long[] array2 = new long[indices.Length];
			for (int j = 0; j < indices.Length; j++)
			{
				array2[j] = (int)source.OArr[indices[j]];
			}
			return array2;
		}
		case JlTupleType.LONG:
		{
			long[] array = new long[indices.Length];
			for (int i = 0; i < indices.Length; i++)
			{
				array[i] = (long)source.OArr[indices[i]];
			}
			return array;
		}
		case JlTupleType.JlANDLE:
			throw new JlTupleAccessException("Implicit access to handle as number is not allowed. Use *.H.Handle to get IntPtr value.");
		default:
			throw new JlTupleAccessException(source, "Mixed tuple does not contain integer " + ((indices.Length == 1) ? ("value at index " + indices[0]) : "values at given indices"));
		}
	}

	/// <summary>
	///   将当前下标的元素读取为 double 值数组。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>类型约束</b>：<c>getType()</c> 判定为 <see cref="JlTupleType.DOUBLE"/> 时按 double 拆箱；
	///     判定为 <see cref="JlTupleType.INTEGER"/> 时先按 int 拆箱再提升为 double；
	///     判定为 <see cref="JlTupleType.LONG"/> 时先按 long 拆箱再提升为 double。
	///   </para>
	///   <para>
	///     <b>越界错误</b>：元素为 string / handle / mixed 时抛 <c>Mixed tuple does not contain numeric</c> 异常。
	///     （与 getI/getL 不同，getD 不处理 JlHandle 特例，handle 走默认分支报错。）
	///   </para>
	///   <para><b>null 返回</b>：indices 为 null 时返回 null。</para>
	/// </remarks>
	public override double[] getD()
	{
		if (indices == null)
		{
			return null;
		}
		switch (getType())
		{
		case JlTupleType.DOUBLE:
		{
			double[] array2 = new double[indices.Length];
			for (int j = 0; j < indices.Length; j++)
			{
				array2[j] = (double)source.OArr[indices[j]];
			}
			return array2;
		}
		case JlTupleType.INTEGER:
		{
			double[] array = new double[indices.Length];
			for (int i = 0; i < indices.Length; i++)
			{
				array[i] = (int)source.OArr[indices[i]];
			}
			return array;
		}
		case JlTupleType.LONG:
		{
			double[] array3 = new double[indices.Length];
			for (int k = 0; k < indices.Length; k++)
			{
				array3[k] = (long)source.OArr[indices[k]];
			}
			return array3;
		}
		default:
			throw new JlTupleAccessException(source, "Mixed tuple does not contain numeric " + ((indices.Length == 1) ? ("value at index " + indices[0]) : "values at given indices"));
		}
	}

	/// <summary>
	///   将当前下标的元素读取为 string 值数组。
	/// </summary>
	/// <remarks>
	///   <para><b>类型约束</b>：仅当 <c>getType()</c> 判定为 <see cref="JlTupleType.STRING"/> 时按 string 拆箱读取。</para>
	///   <para><b>越界错误</b>：元素为数值 / handle / mixed 时抛 <c>Mixed tuple does not contain string</c> 异常。</para>
	///   <para><b>null 返回</b>：indices 为 null 时返回 null。</para>
	/// </remarks>
	public override string[] getS()
	{
		if (indices == null)
		{
			return null;
		}
		if (getType() == JlTupleType.STRING)
		{
			string[] array = new string[indices.Length];
			for (int i = 0; i < indices.Length; i++)
			{
				array[i] = (string)source.OArr[indices[i]];
			}
			return array;
		}
		throw new JlTupleAccessException(source, "Mixed tuple does not contain string " + ((indices.Length == 1) ? ("value at index " + indices[0]) : "values at given indices"));
	}

	/// <summary>
	///   将当前下标的元素读取为 <see cref="JlHandle"/> 值数组。
	/// </summary>
	/// <remarks>
	///   <para><b>类型约束</b>：仅当 <c>getType()</c> 判定为 <see cref="JlTupleType.JlANDLE"/> 时按 JlHandle 拆箱读取。</para>
	///   <para><b>越界错误</b>：元素为数值 / string / mixed 时抛 <c>Mixed tuple does not contain handle</c> 异常。</para>
	///   <para><b>null 返回</b>：indices 为 null 时返回 null。</para>
	/// </remarks>
	public override JlHandle[] getH()
	{
		if (indices == null)
		{
			return null;
		}
		if (getType() == JlTupleType.JlANDLE)
		{
			JlHandle[] array = new JlHandle[indices.Length];
			for (int i = 0; i < indices.Length; i++)
			{
				array[i] = (JlHandle)source.OArr[indices[i]];
			}
			return array;
		}
		throw new JlTupleAccessException(source, "Mixed tuple does not contain handle " + ((indices.Length == 1) ? ("value at index " + indices[0]) : "values at given indices"));
	}

	/// <summary>
	///   将当前下标的元素原样读取为装箱 object 数组，不做任何类型校验。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>功能说明</b>：<see cref="getO"/> 是所有读取中最宽松的入口，直接把 <c>source.OArr</c> 中对应下标的装箱元素原样取出，
	///     元素可为任意受支持类型（int/long/double/string/JlHandle），不依 <c>getType()</c> 做校验。
	///     因此它不会抛类型不匹配异常，适合忽略具体类型做通用读取。
	///   </para>
	///   <para><b>null 返回</b>：indices 为 null 时返回 null。</para>
	/// </remarks>
	public override object[] getO()
	{
		if (indices == null)
		{
			return null;
		}
		object[] array = new object[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			array[i] = source.OArr[indices[i]];
		}
		return array;
	}

	/// <summary>
	///   释放指定下标处的元素（若其可释放）。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>功能说明</b>：若下标处的对象实现了 <see cref="IDisposable"/>（对混合元组而言，通常是 JlHandle 或其句柄值），则调用其 Dispose。
	///     该方法是所有 setX 在覆盖旧值前调用的内部辅助，用于回收被替换元素持有的资源，避免句柄/资源泄漏。
	///   </para>
	/// </remarks>
	/// <param name="index">要释放的下标。</param>
	protected void DisposeElement(int index)
	{
		if (source.OArr[index] is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	///   将 int 值数组写入当前下标的元素。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>广播规则</b>：长度为 1 的输入数组会广播应用到所有下标；否则长度必须等于当前下标个数，否则 <see cref="JlTupleElementsImplementation.IsValidArrayForSetX"/>
	///     抛 <c>Number of values must be one or match number of indexed elements</c> 异常；null 输入则被忽略。
	///   </para>
	///   <para><b>装箱存储</b>：写入前对旧元素调用 <see cref="DisposeElement"/>，随后把 int 值装箱存入 <c>source.OArr</c>，元素类型变为 <see cref="JlTupleType.INTEGER"/>。</para>
	/// </remarks>
	public override void setI(int[] i)
	{
		if (IsValidArrayForSetX(i))
		{
			bool flag = i.Length == 1;
			for (int j = 0; j < indices.Length; j++)
			{
				DisposeElement(indices[j]);
				source.OArr[indices[j]] = i[(!flag) ? j : 0];
			}
		}
	}

	/// <summary>
	///   将 long 值数组写入当前下标的元素。
	/// </summary>
	/// <remarks>
	///   <para><b>广播规则</b>：同 <see cref="setI"/>（长度 1 广播，否则须等于下标个数，null 忽略）。</para>
	///   <para><b>装箱存储</b>：写入前对旧元素调用 <see cref="DisposeElement"/>，随后把 long 值装箱存入 <c>source.OArr</c>，元素类型变为 <see cref="JlTupleType.LONG"/>。</para>
	/// </remarks>
	public override void setL(long[] l)
	{
		if (IsValidArrayForSetX(l))
		{
			bool flag = l.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				DisposeElement(indices[i]);
				source.OArr[indices[i]] = l[(!flag) ? i : 0];
			}
		}
	}

	/// <summary>
	///   将 double 值数组写入当前下标的元素。
	/// </summary>
	/// <remarks>
	///   <para><b>广播规则</b>：同 <see cref="setI"/>（长度 1 广播，否则须等于下标个数，null 忽略）。</para>
	///   <para><b>装箱存储</b>：写入前对旧元素调用 <see cref="DisposeElement"/>，随后把 double 值装箱存入 <c>source.OArr</c>，元素类型变为 <see cref="JlTupleType.DOUBLE"/>。</para>
	/// </remarks>
	public override void setD(double[] d)
	{
		if (IsValidArrayForSetX(d))
		{
			bool flag = d.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				DisposeElement(indices[i]);
				source.OArr[indices[i]] = d[(!flag) ? i : 0];
			}
		}
	}

	/// <summary>
	///   将 string 值数组写入当前下标的元素。
	/// </summary>
	/// <remarks>
	///   <para><b>广播规则</b>：同 <see cref="setI"/>（长度 1 广播，否则须等于下标个数，null 忽略）。</para>
	///   <para><b>装箱存储</b>：写入前对旧元素调用 <see cref="DisposeElement"/>，随后把 string 值存入 <c>source.OArr</c>（string 为引用类型无需装箱），元素类型变为 <see cref="JlTupleType.STRING"/>。</para>
	/// </remarks>
	public override void setS(string[] s)
	{
		if (IsValidArrayForSetX(s))
		{
			bool flag = s.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				DisposeElement(indices[i]);
				source.OArr[indices[i]] = s[(!flag) ? i : 0];
			}
		}
	}

	/// <summary>
	///   释放本访问器（只抑制终结器并保持对象存活，不真正释放元素）。
	/// </summary>
	/// <remarks>
	///   <para><b>资源与坑</b>：混合元组元素的真正释放由底层元组 <see cref="JlTupleMixed.Dispose"/> 统一遍历完成；
	///   此处仅调用 GC.SuppressFinalize 与 GC.KeepAlive。若需在覆盖单个元素时回收资源，应依靠 setX 内部的 <see cref="DisposeElement"/>。</para>
	/// </remarks>
	public override void Dispose()
	{
		GC.SuppressFinalize(this);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   将 <see cref="JlHandle"/> 值数组写入当前下标的元素。
	/// </summary>
	/// <remarks>
	///   <para><b>广播规则</b>：同 <see cref="setI"/>（长度 1 广播，否则须等于下标个数，null 忽略）。</para>
	///   <para>
	///     <b>装箱存储</b>：写入前对旧元素调用 <see cref="DisposeElement"/>；每个 JlHandle 通过 <c>new JlHandle(...)</c> 复制一份再存入
	///     <c>source.OArr</c>（混合元组所有 setH 一律复制句柄，避免别名共享），元素类型变为 <see cref="JlTupleType.JlANDLE"/>。
	///   </para>
	/// </remarks>
	public override void setH(JlHandle[] h)
	{
		if (IsValidArrayForSetX(h))
		{
			bool flag = h.Length == 1;
			for (int i = 0; i < indices.Length; i++)
			{
				DisposeElement(indices[i]);
				source.OArr[indices[i]] = new JlHandle(h[(!flag) ? i : 0]);
			}
		}
	}

	/// <summary>
	///   将任意 object 值数组写入当前下标的元素。
	/// </summary>
	/// <remarks>
	///   <para><b>广播规则</b>：同 <see cref="setI"/>（长度 1 广播，否则须等于下标个数，null 忽略）。</para>
	///   <para>
	///     <b>典型场景</b>：<see cref="setO"/> 是最通用的写入入口，可用于拷贝或构造异构片段。
	///   </para>
	///   <para>
	///     <b>类型处理</b>：写入前对旧元素调用 <see cref="DisposeElement"/>；当元素的 <see cref="JlTupleImplementation.GetObjectType"/> 返回值等于 16（即为 JlHandle）时，
	///     会先 <c>new JlHandle((JlHandle)obj)</c> 复制一份再存（与 <see cref="setH"/> 一致），避免与外部对象共享同一句柄。
	///   </para>
	/// </remarks>
	public override void setO(object[] o)
	{
		if (!IsValidArrayForSetX(o))
		{
			return;
		}
		bool flag = o.Length == 1;
		for (int i = 0; i < indices.Length; i++)
		{
			DisposeElement(indices[i]);
			object obj = o[(!flag) ? i : 0];
			if (JlTupleImplementation.GetObjectType(obj) == 16)
			{
				obj = new JlHandle((JlHandle)obj);
			}
			source.OArr[indices[i]] = obj;
		}
	}

	/// <summary>
	///   获取当前下标范围所描述元素类型。
	/// </summary>
	/// <remarks>
	///   <para>
	///     <b>功能说明</b>：委托底层 <see cref="JlTupleMixed.GetElementType(int[])"/> 判定：indices 为 null / 空时返回
	///     <see cref="JlTupleType.EMPTY"/>；若所有下标所指元素类型一致，返回该公共类型；
	///     否则返回 <see cref="JlTupleType.MIXED"/>（即本访问器实际上是混合类型元素的通用实现，getType 结果取决于所指元素，而非固定为 MIXED）。
	///   </para>
	///   <para><b>资源与坑</b>：各 getX/setX 的类型校验都以该返回值（而非固定的 JlTupleType.MIXED）为依据，因此对单个同类型下标可能直接读写成功。</para>
	/// </remarks>
	public override JlTupleType getType()
	{
		return ((JlTupleMixed)source).GetElementType(indices);
	}
}