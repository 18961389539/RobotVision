using System;

namespace JLVisionLib;

/// <summary>
///   This exception is thrown whenever an error occurs during
///   vector operations
/// </summary>
public class JlVectorAccessException : JlException
{
	private static string BuildMessage(JlVector sender, string sInfo)
	{
		string text = sInfo;
		if (sender != null)
		{
			text = "'" + text + "' when accessing '" + sender.ToString() + "'";
		}
		return text;
	}

	internal JlVectorAccessException(JlVector sender, string sInfo, Exception inner)
		: base(BuildMessage(sender, sInfo), null)
	{
	}

	internal JlVectorAccessException(JlVector sender, string sInfo)
		: this(sender, sInfo, null)
	{
	}

	internal JlVectorAccessException(JlVector sender)
		: this(sender, "Illegal operation on vector")
	{
	}

	internal JlVectorAccessException(string sInfo, Exception inner)
		: this(null, sInfo, inner)
	{
	}

	internal JlVectorAccessException(string sInfo)
		: this(null, sInfo)
	{
	}

	internal JlVectorAccessException()
		: this((JlVector)null)
	{
	}
}
