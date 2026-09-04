using System;
using System.ComponentModel;

namespace JLVisionLib;

	/// <summary>原生库调用错误异常：携带原始错误码与错误文本（见 GetErrorNumber/GetErrorText）。</summary>
public class JlException : ApplicationException
{
	private int err = 2;

	private JlTuple user_data;

	private const int ErrCodeUserException = 30000;

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = new JlException(0, "value");
	///   </code>
	/// </remarks>
	public JlException(int err, string sInfo, Exception inner)
		: this(sInfo, inner)
	{
		this.err = err;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = new JlException(0);
	///   </code>
	/// </remarks>
	public JlException(int err, string sInfo)
		: this(err, sInfo, null)
	{
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = new JlException("value");
	///   </code>
	/// </remarks>
	public JlException(string sInfo, Exception inner)
		: base(sInfo, inner)
	{
		err = -1;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = new JlException();
	///   </code>
	/// </remarks>
	public JlException(string sInfo)
		: base(sInfo)
	{
		err = -1;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = new JlException();
	///   </code>
	/// </remarks>
	public JlException()
	{
		err = -1;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = new JlException();
	///   </code>
	/// </remarks>
	public JlException(JlTuple tuple)
		: this(tuple[0], tuple[1].O.ToString())
	{
		int num = 2;
		if (err >= 30000)
		{
			num = 1;
		}
		if (num <= tuple.TupleLength() - 1)
		{
			user_data = tuple.TupleSelectRange(num, tuple.TupleLength() - 1);
		}
	}

	/// <summary>返回异常携带的原生错误码。</summary>
	[Obsolete("GetErrorNumber is deprecated, please use GetErrorCode instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public int GetErrorNumber()
	{
		return err;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取ErrorCode。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = ...;
	///   var result = obj.GetErrorCode();
	///   </code>
	/// </remarks>
	public int GetErrorCode()
	{
		return err;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>ToH元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = ...;
	///   obj.ToHTuple(out JlTuple exception);
	///   </code>
	/// </remarks>
	public void ToHTuple(out JlTuple exception)
	{
		exception = new JlTuple();
		exception[0] = GetErrorCode();
		if ((long)GetErrorCode() < 30000L)
		{
			exception[1] = GetErrorMessage();
		}
		if (user_data != null)
		{
			exception = exception.TupleConcat(user_data);
		}
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取异常数据。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple exception = ...;
	///   JlTuple name = ...;
	///   JlException.GetExceptionData(exception, name, out JlTuple value);
	///   </code>
	/// </remarks>
	public static void GetExceptionData(JlTuple exception, JlTuple name, out JlTuple value)
	{
		value = new JlTuple();
		bool flag = exception.TupleLength() > 0 && exception[0].Type == JlTupleType.INTEGER && exception[0].I >= 30000;
		int num = name.TupleLength();
		for (int i = 0; i < num; i++)
		{
			if (name[i].Type != JlTupleType.STRING)
			{
				throw new JlOperatorException(0, "JlOperatorException.GetExceptionData(): wrong type of input parameter 'name'.");
			}
			int num2;
			switch (name[i].S)
			{
			case "error_code":
				num2 = 0;
				goto IL_01a5;
			case "add_error_code":
				num2 = -1;
				goto IL_01a5;
			case "user_data":
				if (num != 1)
				{
					value = new JlTuple();
					throw new JlOperatorException(0, "JlOperatorException.GetExceptionData(): slot 'user_data' onparameter 'Name' cannot be requested together with other slots.");
				}
				num2 = (flag ? 1 : 2);
				if (num2 <= exception.TupleLength() - 1)
				{
					value = value.TupleConcat(exception.TupleSelectRange(num2, exception.TupleLength() - 1));
				}
				return;
			case "error_msg":
			case "error_message":
				num2 = 1;
				goto IL_01a5;
			case "add_error_msg":
			case "add_error_message":
				num2 = -1;
				goto IL_01a5;
			case "proc_line":
			case "program_line":
				num2 = -1;
				goto IL_01a5;
			case "operator":
				num2 = -1;
				goto IL_01a5;
			case "call_stack_depth":
				num2 = -1;
				goto IL_01a5;
			case "procedure":
				num2 = -1;
				goto IL_01a5;
			default:
				{
					value = new JlTuple();
					throw new JlOperatorException(0, "JlOperatorException.GetExceptionData(): wrong value of input parameter 'name'.");
				}
				IL_01a5:
				if (num2 == -1)
				{
					value = value.TupleConcat("");
				}
				else if (flag && num2 != 0)
				{
					value = value.TupleConcat("User defined exception");
				}
				else
				{
					value = value.TupleConcat(exception[num2]);
				}
				break;
			}
		}
	}

	/// <summary>返回异常描述文本（等价 Message）。</summary>
	[Obsolete("GetErrorText is deprecated, please use GetErrorMessage instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public string GetErrorText()
	{
		return Message;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取ErrorMessage。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlException obj = ...;
	///   var result = obj.GetErrorMessage();
	///   </code>
	/// </remarks>
	public string GetErrorMessage()
	{
		return Message;
	}
}
