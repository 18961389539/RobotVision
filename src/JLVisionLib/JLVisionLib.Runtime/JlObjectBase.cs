using System;
using System.ComponentModel;

namespace JLVisionLib;

	/// <summary>图标对象的基础类型：管理原生对象句柄与对象语义类型（image/region/XLD…），JlObject 系列由此派生。</summary>
public class JlObjectBase : IDisposable
{
	/// <summary>Represents an uninitialized Vision object key</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static readonly IntPtr UNDEF = IntPtr.Zero;

	internal static readonly IntPtr UNDEF2 = new IntPtr(1);

	internal IntPtr key = UNDEF;

	private bool suppressedFinalization;

	/// <summary>Returns the Vision ID for this iconic object</summary>
	/// <remarks>Caller must ensure that object is kept alive</remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IntPtr Key => key;

	internal JlObjectBase()
		: this(UNDEF, copy: false)
	{
	}

	internal JlObjectBase(IntPtr key, bool copy)
	{
		if (copy && key != UNDEF && key != UNDEF2)
		{
			this.key = JlNativeApi.CopyObject(key);
		}
		else
		{
			this.key = ((key == UNDEF2) ? UNDEF : key);
		}
	}

	internal JlObjectBase(JlObjectBase obj)
		: this(obj.key, copy: true)
	{
		GC.KeepAlive(obj);
	}

	/// <summary>
	///   Returns true if the iconic object has been initialized.
	/// </summary>
	/// <remarks>
	///   An object will be uninitialized when creating it with a
	///   no-argument constructor or after calling Dispose();
	/// </remarks>
	public bool IsInitialized()
	{
		return key != UNDEF;
	}

	/// <summary>
	///   Returns a new Vision ID referencing this iconic object, which will
	///   remain valid even after this object is disposed (and vice versa).
	///   This is only useful if the ID shall be used in another language
	///   interface (in fact, the key needs to be externally disposed,
	///   a feature not even offered by the .NET language interface).
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IntPtr CopyKey()
	{
		IntPtr result = JlNativeApi.CopyObject(key);
		GC.KeepAlive(this);
		return result;
	}

	/// <summary>把 <paramref name="source"/> 的原生对象句柄整体转移给本对象（source 随即为空），避免复制；source 可为 null（仅释放本对象）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void TransferOwnership(JlObjectBase source)
	{
		if (source != this)
		{
			Dispose();
			if (source != null)
			{
				key = source.key;
				source.key = UNDEF;
				suppressedFinalization = false;
				GC.ReRegisterForFinalize(this);
			}
		}
	}

	/// <summary>终结器：兜底释放对象句柄（勿直接调用）。</summary>
	~JlObjectBase()
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
		if (key != UNDEF)
		{
			JlNativeApi.ClearObject(key);
			key = UNDEF;
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

	/// <summary>Releases the resources used by this tool object</summary>
	public virtual void Dispose()
	{
		Dispose(disposing: true);
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.JlCkP(proc, JlNativeApi.SetInputObject(proc, parIndex, key));
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		if (key != UNDEF)
		{
			throw new JlException("Undisposed object instance when loading output parameter");
		}
		if (JlNativeApi.IsFailure(err))
		{
			return err;
		}
		err = JlNativeApi.GetOutputObject(proc, parIndex, out key);
		if (suppressedFinalization)
		{
			suppressedFinalization = false;
			GC.ReRegisterForFinalize(this);
		}
		return err;
	}
}
