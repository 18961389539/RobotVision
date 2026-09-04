using System;

namespace JLVisionLib;

/// <summary>
/// 一组下标命中的元组元素读写视图的基类，由各子类约束具体元素类型。
/// </summary>
/// <remarks>
/// <para><b>功能说明</b>：本类把"对 <see cref="source"/> 元组中由 <see cref="indices"/> 命中的一组元素进行读/写"的通用逻辑抽象出来。
/// 读取经六个 <c>getX</c> 虚方法，写入先经 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验数组长度再走 <c>setX</c> 虚方法。</para>
/// <para><b>典型场景</b>：由元组索引/切片（<see cref="JlTuple"/> 的 <c>this[int[]]</c> / <c>this[int]</c>）构造具体子类
/// （如 <see cref="JlTupleElementsInt32"/>），供上层按类型批量读写选定元素。</para>
/// <para><b>资源与坑</b>：基类六个 <c>getX</c> / <c>setX</c> 默认抛 <see cref="JlTupleAccessException"/> 表示"类型不匹配"，
/// 子类仅覆盖其支持的类型映射；写入支持广播规则（见 <see cref="IsValidArrayForSetX"/>）。</para>
/// </remarks>
internal class JlTupleElementsImplementation
{
	/// <summary>命中的目标下标集合；长度即元素个数（空下标则视为空元组视图）。</summary>
	/// <remarks>
	/// <para><b>资源与坑</b>：<see cref="Length"/> 与 <see cref="getType"/> 均以其长度为依据（下标个数为 0 时视为空元组，见 <see cref="JlTupleType.EMPTY"/>）；</para>
	/// <para>子类 setX 的广播校验（<see cref="IsValidArrayForSetX"/>）亦据此长度判断。</para>
	/// </remarks>
	protected int[] indices;

	/// <summary>被访问的底层元组实现；所有读/写最终都转发到它。无参构造时可为 null。</summary>
	protected JlTupleImplementation source;

	/// <summary>按下标读取或写入 int 元素数组。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：读取经 <see cref="getI"/> 返回 int 元素数组；写入先 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验
	/// 数组长度与下标匹配，再经 <see cref="setI"/> 回写。</para>
	/// <para><b>资源与坑</b>：本视图元素非 int 类型时，读取或写入会抛 <see cref="JlTupleAccessException"/>；写入数组支持广播规则。</para>
	/// <para><b>示例</b>：</para>
	/// <code>
	/// var t  = new JlTuple(10, 20, 30);
	/// var els = t[new[] { 0, 2 }];   // 下标 0、2 处 int 元素的视图
	/// els.I = new[] { 15, 25 };       // 按位写入：下标0=15、下标2=25
	/// els.I = new[] { 7 };           // 广播：长度1，下标0、2 均写为 7
	/// int[] back = els.I;            // 读出 { 7, 7 }
	/// </code>
	/// </remarks>
	public int[] I
	{
		get
		{
			return getI();
		}
		set
		{
			source.AssertSize(indices);
			setI(value);
		}
	}

	/// <summary>按下标读取或写入 long 元素数组。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：读取经 <see cref="getL"/> 返回 long 元素数组；写入先 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验再经 <see cref="setL"/> 回写。</para>
	/// <para><b>资源与坑</b>：本视图元素非 long 类型时，读取或写入会抛 <see cref="JlTupleAccessException"/>；写入数组长度为 1 时广播到所有下标。</para>
	/// </remarks>
	public long[] L
	{
		get
		{
			return getL();
		}
		set
		{
			source.AssertSize(indices);
			setL(value);
		}
	}

	/// <summary>按下标读取或写入 double 元素数组。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：读取经 <see cref="getD"/> 返回 double 元素数组；写入先 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验再经 <see cref="setD"/> 回写。</para>
	/// <para><b>资源与坑</b>：本视图元素非 double 类型时，读取或写入会抛 <see cref="JlTupleAccessException"/>；写入数组长度为 1 时广播到所有下标。</para>
	/// </remarks>
	public double[] D
	{
		get
		{
			return getD();
		}
		set
		{
			source.AssertSize(indices);
			setD(value);
		}
	}

	/// <summary>按下标读取或写入 string 元素数组。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：读取经 <see cref="getS"/> 返回 string 元素数组；写入先 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验再经 <see cref="setS"/> 回写。</para>
	/// <para><b>资源与坑</b>：本视图元素非 string 类型时，读取或写入会抛 <see cref="JlTupleAccessException"/>；写入数组长度为 1 时广播到所有下标。</para>
	/// </remarks>
	public string[] S
	{
		get
		{
			return getS();
		}
		set
		{
			source.AssertSize(indices);
			setS(value);
		}
	}

	/// <summary>按下标读取或写入 JlHandle 元素数组。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：读取经 <see cref="getH"/> 返回 JlHandle 元素数组；写入先 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验再经 <see cref="setH"/> 回写。</para>
	/// <para><b>资源与坑</b>：本视图元素非 JlHandle 类型时，读取或写入会抛 <see cref="JlTupleAccessException"/>；写入数组长度为 1 时广播到所有下标。</para>
	/// </remarks>
	public JlHandle[] H
	{
		get
		{
			return getH();
		}
		set
		{
			source.AssertSize(indices);
			setH(value);
		}
	}

	/// <summary>按下标读取或写入 object（异构）元素数组。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：读取经 <see cref="getO"/> 返回装箱后的 object 元素数组；写入先 <see cref="JlTupleImplementation.AssertSize(int[])"/> 校验再经 <see cref="setO"/> 回写。</para>
	/// <para><b>资源与坑</b>：本视图元素既非 MIXED 异构元组也非 object 直接承载时，读取或写入会抛 <see cref="JlTupleAccessException"/>；写入数组长度为 1 时广播到所有下标。</para>
	/// </remarks>
	public object[] O
	{
		get
		{
			return getO();
		}
		set
		{
			source.AssertSize(indices);
			setO(value);
		}
	}

	/// <summary>本视图所承载元素的类型标识。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：直接转发 <see cref="getType"/>；空下标（<c>indices.Length == 0</c>）返回 <see cref="JlTupleType.EMPTY"/>，否则由子类返回具体类型。</para>
	/// <para><b>资源与坑</b>：基类默认实现于非空下标时抛 <see cref="JlTupleAccessException"/>，子类须覆盖。</para>
	/// </remarks>
	public JlTupleType Type => getType();

	/// <summary>命中的下标个数，即本视图对应的元素个数。</summary>
	/// <remarks><para><b>功能说明</b>：等于 <see cref="indices"/> 的 Length；空下标时为 0。</para></remarks>
	public int Length => indices.Length;

	/// <summary>构造一个不带底层元组的空视图。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：初始化 <see cref="source"/> 为 null、<see cref="indices"/> 为空数组；此时对 I/L/D/S/H/O 的读写会因此抛 <see cref="JlTupleAccessException"/>。</para>
	/// <para><b>典型场景</b>：用于表示"无元素/空处理结果"的占位视图。</para>
	/// </remarks>
	public JlTupleElementsImplementation()
	{
		source = null;
		indices = new int[0];
	}

	/// <summary>用单个下标构造指向 <see cref="source"/> 中单一元素的视图。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：等价于访问该元组的单个元素，内部把 <paramref name="index"/> 包装为长度为 1 的下标数组。</para>
	/// <para><b>资源与坑</b>：无下标越界检查，越界访问由底层写入/读取时抛出运行库异常。</para>
	/// </remarks>
	/// <param name="source">被访问的元组实现。</param>
	/// <param name="index">目标元素在该元组中的单个下标。</param>
	public JlTupleElementsImplementation(JlTupleImplementation source, int index)
	{
		this.source = source;
		indices = new int[1] { index };
	}

	/// <summary>释放本视图持有的资源。</summary>
	/// <remarks><para><b>功能说明</b>：基类为空实现；子类若持有需释放的资源（如句柄）可在此覆盖清理。</para></remarks>
	public virtual void Dispose()
	{
	}

	/// <summary>用下标数组构造指向 <see cref="source"/> 中多个元素的视图。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：按 <paramref name="indices"/> 指定的每个下标逐一对应一个元素，下标个数即元素个数；下标数组直接以引用方式保存。</para>
	/// <para><b>资源与坑</b>：不复制下标数组，调用方后续修改该数组将影响本视图；无越界检查。</para>
	/// </remarks>
	/// <param name="source">被访问的元组实现。</param>
	/// <param name="indices">目标元素下标数组。</param>
	public JlTupleElementsImplementation(JlTupleImplementation source, int[] indices)
	{
		this.source = source;
		this.indices = indices;
	}

	/// <summary>返回本视图命中的下标集合。</summary>
	/// <remarks><para><b>功能说明</b>：直接返回 <see cref="indices"/> 引用，未做复制。</para></remarks>
	public int[] getIndices()
	{
		return indices;
	}

	/// <summary>按下标读取 int 元素并作为 int 数组返回。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配），子类对承载或可转换为 int 的元组覆盖实现。</para>
	/// </remarks>
	/// <returns>读取到的 int 元素数组。</returns>
	public virtual int[] getI()
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>按下标读取并转为 long 元素数组返回。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时通常做 int→long 宽度提升。</para>
	/// </remarks>
	/// <returns>读取到的 long 元素数组。</returns>
	public virtual long[] getL()
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>按下标读取并转为 double 元素数组返回。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时通常做数值型提升为 double。</para>
	/// </remarks>
	/// <returns>读取到的 double 元素数组。</returns>
	public virtual double[] getD()
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>按下标读取 string 元素并作为 string 数组返回。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类对 string 元组覆盖实现。</para>
	/// </remarks>
	/// <returns>读取到的 string 元素数组。</returns>
	public virtual string[] getS()
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>按下标读取 JlHandle 元素并作为数组返回。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类对承载句柄的元组覆盖实现。</para>
	/// </remarks>
	/// <returns>读取到的 JlHandle 元素数组。</returns>
	public virtual JlHandle[] getH()
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>按下标读取元素并以装箱后的 object 数组返回。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时通常将元素装箱为 object 返回。</para>
	/// </remarks>
	/// <returns>读取到的 object 元素数组。</returns>
	public virtual object[] getO()
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>将 int 元素数组写回元组中被命中的下标位置。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时先经 <see cref="IsValidArrayForSetX"/> 校验长度再逐下标写入。</para>
	/// <para><b>广播规则</b>：写入数组长度为 1 时，该值广播应用到所有目标下标；长度须等于下标个数或为 1，否则抛 <see cref="JlTupleAccessException"/>。</para>
	/// </remarks>
	/// <param name="i">待写入的 int 元素数组。</param>
	public virtual void setI(int[] i)
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>将 long 元素数组写回元组中被命中的下标位置。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时先经 <see cref="IsValidArrayForSetX"/> 校验长度。</para>
	/// <para><b>广播规则</b>：长度为 1 时广播到所有下标；子类按位写回 int 元素时可能对超出 int 范围的值做截断。</para>
	/// </remarks>
	/// <param name="l">待写入的 long 元素数组。</param>
	public virtual void setL(long[] l)
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>将 double 元素数组写回元组中被命中的下标位置。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时先经 <see cref="IsValidArrayForSetX"/> 校验长度。</para>
	/// <para><b>广播规则</b>：长度为 1 时广播到所有下标。</para>
	/// </remarks>
	/// <param name="d">待写入的 double 元素数组。</param>
	public virtual void setD(double[] d)
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>将 string 元素数组写回元组中被命中的下标位置。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时先经 <see cref="IsValidArrayForSetX"/> 校验长度。</para>
	/// <para><b>广播规则</b>：长度为 1 时广播到所有下标。</para>
	/// </remarks>
	/// <param name="s">待写入的 string 元素数组。</param>
	public virtual void setS(string[] s)
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>将 JlHandle 元素数组写回元组中被命中的下标位置。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时先经 <see cref="IsValidArrayForSetX"/> 校验长度。</para>
	/// <para><b>广播规则</b>：长度为 1 时广播到所有下标。</para>
	/// </remarks>
	/// <param name="h">待写入的 JlHandle 元素数组。</param>
	public virtual void setH(JlHandle[] h)
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>将 object（异构）元素数组写回元组中被命中的下标位置。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：基类默认抛 <see cref="JlTupleAccessException"/>（类型不匹配）；子类覆盖时先经 <see cref="IsValidArrayForSetX"/> 校验长度。</para>
	/// <para><b>广播规则</b>：长度为 1 时广播到所有下标。</para>
	/// </remarks>
	/// <param name="o">待写入的 object 元素数组。</param>
	public virtual void setO(object[] o)
	{
		throw new JlTupleAccessException(source);
	}

	/// <summary>校验 setX 写入数组的长度是否与目标下标匹配。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：<paramref name="a"/> 为 null 时返回 false；长度为 1 或恰等于 <see cref="indices"/> 长度时返回 true；否则抛 <see cref="JlTupleAccessException"/>。</para>
	/// <para><b>广播规则</b>：长度为 1 的数组视为广播——该值将应用到所有目标下标，故可接受；这是广播语义的判定核心。</para>
	/// </remarks>
	/// <param name="a">待校验的写入数组；可为五种元素类型数组中任意一种。</param>
	/// <returns>数组长度合法（为 1 或等于下标个数）返回 true，为 null 返回 false；长度不合法抛 <see cref="JlTupleAccessException"/>。</returns>
	protected bool IsValidArrayForSetX(Array a)
	{
		if (a == null)
		{
			return false;
		}
		if (a.Length != 1 && a.Length != indices.Length)
		{
			throw new JlTupleAccessException(source, "Number of values must be one or match number of indexed elements");
		}
		return true;
	}

	/// <summary>返回本视图所承载元素的类型标识。</summary>
	/// <remarks>
	/// <para><b>功能说明</b>：下标为空（<c>indices.Length == 0</c>）时返回 <see cref="JlTupleType.EMPTY"/>，表示空元组视图；否则基类默认抛 <see cref="JlTupleAccessException"/>，由子类对具体类型返回对应枚举。</para>
	/// <para><b>资源与坑</b>：非空下标的实际类型判定依赖子类覆盖，基类不自行推断。</para>
	/// </remarks>
	/// <returns>空视图返回 <see cref="JlTupleType.EMPTY"/>；否则由子类返回具体类型。</returns>
	public virtual JlTupleType getType()
	{
		if (indices.Length == 0)
		{
			return JlTupleType.EMPTY;
		}
		throw new JlTupleAccessException(source);
	}
}
