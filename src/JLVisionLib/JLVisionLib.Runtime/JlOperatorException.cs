using System;
using System.ComponentModel;

namespace JLVisionLib;

	/// <summary>由算子调用链在原生调用失败时抛出的异常：携带错误码与出错上下文。</summary>
public class JlOperatorException : JlException
{
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlOperatorException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException obj = new JlOperatorException(0, sInfo, sInfo);
	///   </code>
	/// </remarks>
	public JlOperatorException(int err, string sInfo, Exception inner)
		: base(err, (sInfo == "") ? JlNativeApi.GetErrorMessage(err) : sInfo, inner)
	{
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlOperatorException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException obj = new JlOperatorException(0);
	///   </code>
	/// </remarks>
	public JlOperatorException(int err, string sInfo)
		: this(err, sInfo, null)
	{
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建 JlOperatorException 实例。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException obj = new JlOperatorException();
	///   </code>
	/// </remarks>
	public JlOperatorException(int err)
		: this(err, "")
	{
	}

	/// <summary>返回异常描述文本（含出错算子上下文）。</summary>
	[Obsolete("GetErrorText is deprecated, please use GetErrorMessage instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public new string GetErrorText()
	{
		return JlNativeApi.GetErrorMessage(GetErrorCode());
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取ErrorMessage。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException obj = ...;
	///   var result = obj.GetErrorMessage();
	///   </code>
	/// </remarks>
	public new string GetErrorMessage()
	{
		return JlNativeApi.GetErrorMessage(GetErrorCode());
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取ExtendedErrorCode。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException obj = ...;
	///   var result = obj.GetExtendedErrorCode();
	///   </code>
	/// </remarks>
	public long GetExtendedErrorCode()
	{
		return 0L;
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>获取ExtendedErrorMessage。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException obj = ...;
	///   var result = obj.GetExtendedErrorMessage();
	///   </code>
	/// </remarks>
	public string GetExtendedErrorMessage()
	{
		return "";
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>throw算子。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException.throwOperator(0, "value");
	///   </code>
	/// </remarks>
	public static void throwOperator(int err, string logicalName)
	{
		if (JlNativeApi.IsFailure(err))
		{
			throw new JlOperatorException(err, JlNativeApi.GetErrorMessage(err) + " in operator " + logicalName);
		}
	}

	internal static void throwOperator(int err, int procIndex)
	{
		if (JlNativeApi.IsFailure(err))
		{
			string logicalName = JlNativeApi.GetLogicalName(procIndex);
			throw new JlOperatorException(err, JlNativeApi.GetErrorMessage(err) + " in operator " + logicalName);
		}
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>throwInfo。</para>
	///   <para><b>典型场景</b></para>
	///   <para>异常捕获与错误码处理</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlOperatorException.throwInfo(0, "value");
	///   </code>
	/// </remarks>
	public static void throwInfo(int err, string sInfo)
	{
		throw new JlOperatorException(err, sInfo + ":\n" + JlNativeApi.GetErrorMessage(err) + "\n");
	}
}
