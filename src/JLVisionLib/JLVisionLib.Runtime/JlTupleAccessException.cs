using System;

namespace JLVisionLib;

/// <summary>
///   对元组（Tuple）进行非法访问时抛出的异常。
/// </summary>
/// <remarks>
///   <para><b>功能说明</b>：当对元组的访问操作不合法时抛出——典型场景包括：<c>string</c>/<c>JlTupleElements</c>
///   下标越界、以与元组实际存储类型不符的方式取值（如对字符串元组取 <c>int</c>）、对未初始化或空元组取值等。</para>
///   <para><b>典型场景</b>：元组内部在发生访问/类型错误时构造并抛出；调用方一般无需自行捕获，
///   由上层统一包装为 <see cref="JlException"/> 体系处理。</para>
///   <para><b>说明</b>：构造函数均为 <c>internal</c>，本异常不出现在公共 API 的构造接口中；
///   携带可选的触发源 <see cref="JlTupleImplementation"/> 与描述信息，用于拼装更易读的错误消息。</para>
/// </remarks>
public class JlTupleAccessException : JlException
{
	/// <summary>把触发源与描述信息拼装成错误消息：若存在触发源，则形如 "'&lt;描述&gt;' when accessing '&lt;元组&gt;'"。</summary>
	private static string BuildMessage(JlTupleImplementation sender, string sInfo)
	{
		string text = sInfo;
		if (sender != null)
		{
			text = "'" + text + "' when accessing '" + sender.ToString() + "'";
		}
		return text;
	}

	internal JlTupleAccessException(JlTupleImplementation sender, string sInfo, Exception inner)
		: base(BuildMessage(sender, sInfo), null)
	{
	}

	internal JlTupleAccessException(JlTupleImplementation sender, string sInfo)
		: this(sender, sInfo, null)
	{
	}

	internal JlTupleAccessException(JlTupleImplementation sender)
		: this(sender, "Illegal operation on Tuple")
	{
	}

	internal JlTupleAccessException(string sInfo, Exception inner)
		: this(null, sInfo, inner)
	{
	}

	internal JlTupleAccessException(string sInfo)
		: this(null, sInfo)
	{
	}

	internal JlTupleAccessException()
		: this((JlTupleImplementation)null)
	{
	}
}
