using System;
using System.ComponentModel;

namespace JLVisionLib;

	/// <summary>所有句柄类对象的基类：统一持有原生句柄（IntPtr）并提供 Dispose/终结器生命周期管理；UNDEF 表示空句柄。</summary>
public class JlHandleBase : IDisposable
{
	/// <summary>Represents an uninitialized handle instance</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static readonly IntPtr UNDEF = IntPtr.Zero;

	/// <summary>全局空句柄哨兵（句柄为 UNDEF 的 JlHandle 实例），表示“无句柄”。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static readonly JlHandle JlNULL = new JlHandle();

	private IntPtr mHandle;

	private bool suppressedFinalization;

	/// <summary>Returns the Vision ID for the handle</summary>
	/// <remarks>Caller must ensure that input handle is kept alive</remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IntPtr Handle
	{
		get
		{
			return mHandle;
		}
		set
		{
			SetHandleInternal(value, copy: true);
		}
	}

	internal JlHandleBase()
		: this(UNDEF)
	{
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	internal JlHandleBase(IntPtr handle)
	{
		Handle = handle;
	}

	private void SetHandleInternal(IntPtr handle, bool copy)
	{
		ClearHandleInternal();
		if (suppressedFinalization)
		{
			suppressedFinalization = false;
			GC.ReRegisterForFinalize(this);
		}
		if (handle != UNDEF)
		{
			mHandle = (copy ? JlNativeApi.CopyHandle(handle) : handle);
		}
	}

	internal JlHandleBase(JlHandleBase handle)
	{
		SetHandleInternal(handle, copy: true);
	}

	/// <summary>
	///   Returns true if the handle has been initialized.
	/// </summary>
	/// <remarks>
	///   A handle will be uninitialized when creating it with a
	///   no-argument constructor or after calling Dispose();
	/// </remarks>
	public bool IsInitialized()
	{
		return JlNativeApi.HandleIsValid(mHandle);
	}

	/// <summary>终结器：未显式 Dispose 时兜底释放句柄（勿直接调用）。</summary>
	~JlHandleBase()
	{
		try
		{
			Dispose(disposing: false);
		}
		catch (Exception)
		{
		}
	}

	private void Dispose(bool disposing)
	{
		if (mHandle != UNDEF)
		{
			ClearHandleInternal();
			mHandle = UNDEF;
		}
		if (disposing)
		{
			GC.SuppressFinalize(this);
			suppressedFinalization = true;
		}
		GC.KeepAlive(this);
	}

	void IDisposable.Dispose()
	{
		Dispose(disposing: true);
	}

	/// <summary>Releases the resources used by this handle object</summary>
	public virtual void Dispose()
	{
		Dispose(disposing: true);
	}

	/// <summary>
	///   Invalidates the handle but keeps the Vision handle alive, which
	///   only makes sense if the handle is used externally and cleared later,
	///   e.g. by an JlOperatorSet based module or another language interface.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void InvalidateWithoutDispose()
	{
		Dispose();
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.StoreH(proc, parIndex, mHandle);
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		if (mHandle != UNDEF)
		{
			throw new JlException("Undisposed handle instance when loading output parameter");
		}
		if (JlNativeApi.IsFailure(err))
		{
			return err;
		}
		err = JlNativeApi.LoadH(proc, parIndex, err, out var handleValue);
		SetHandleInternal(handleValue.Handle, copy: true);
		handleValue.Dispose();
		return err;
	}

	/// <summary>释放当前持有的原生句柄并重置为 UNDEF（内部 Dispose 路径调用）。</summary>
	protected virtual void ClearHandleInternal()
	{
		if (mHandle != UNDEF)
		{
			JlNativeApi.ClearHandle(mHandle);
			mHandle = UNDEF;
		}
	}

	/// <summary>
	///   Provides a simple string representation of the handle id
	///   as hex number, which is mainly useful for debug outputs (to
	///   see if two handles are identical)
	/// </summary>
	public override string ToString()
	{
		if (mHandle == UNDEF)
		{
			return "";
		}
		return "H" + mHandle.ToInt64().ToString("X");
	}

	/// <summary>断言底层句柄的语义类型与 <paramref name="sem_type"/> 一致（如 image/region/xld…），不一致抛异常。</summary>
	protected internal void AssertSemType(string sem_type)
	{
		if (mHandle != UNDEF)
		{
			string handleSemType = JlNativeApi.GetHandleSemType(mHandle);
			if (!sem_type.Equals(handleSemType))
			{
				throw new JlException("Invalid handle instance passed");
			}
		}
		GC.KeepAlive(this);
	}

	/// <summary>把句柄数组包装为句柄元组（<see cref="JlTuple"/>，按 JlHandle 数组装载）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static JlTuple ConcatArray(JlHandleBase[] handles)
	{
		return new JlTuple(handles as JlHandle[]);
	}

	/// <summary>Cast to IntPtr returns Vision ID of handle resources</summary>
	/// <remarks>Caller must ensure that input object is kept alive</remarks>
	public static implicit operator IntPtr(JlHandleBase handle)
	{
		return handle?.mHandle ?? UNDEF;
	}
}
