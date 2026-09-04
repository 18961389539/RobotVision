using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents a generic instance of a handle.</summary>
[Serializable]
public class JlHandle : JlHandleBase, ISerializable, ICloneable
{
	/// <summary>构造持有 UNDEF（空）句柄的未初始化实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>从原生句柄构造本类实例（内部路径使用）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>从 <see cref="JlHandle"/> 句柄包装构造本类实例。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlHandle obj)
	{
		obj = new JlHandle(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlHandle[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlHandle[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlHandle(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeHandle();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>反序列化构造器：实现 ISerializable 时由运行时调用，从序列化流还原实例（句柄）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle(SerializationInfo info, StreamingContext context)
	{
		DeserializeHandle((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>Serialize object to binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>序列化。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   obj.Serialize(stream);
	///   </code>
	/// </remarks>
	public virtual void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeHandle(), stream);
	}

	/// <summary>Deserialize object from binary stream in Vision format</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>反序列化。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   var result = JlHandle.Deserialize(stream);
	///   </code>
	/// </remarks>
	public static JlHandle Deserialize(Stream stream)
	{
		JlHandle hHandle = new JlHandle();
		hHandle.DeserializeHandle(JlSerializationBuffer.ReadFromStream(stream));
		return hHandle;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>克隆。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象容器操作：复制、拼接或按索引存取</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   var result = obj.Clone();
	///   </code>
	/// </remarks>
	public JlHandle Clone()
	{
		byte[] data = SerializeHandle();
		JlHandle obj = new JlHandle();
		obj.DeserializeHandle(data);
		return obj;
	}

	/// <summary>
	///   Clear the content of a handle.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>释放 content 句柄。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   obj.ClearHandle();
	///   </code>
	/// </remarks>
	public void ClearHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2011);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Deserialize a serialized item.
	/// </summary>
	/// <param name="serializedItem">Handle containing the serialized item to be deserialized.</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>反序列化 serialized item。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   obj.DeserializeHandle(serializedItem);
	///   </code>
	/// </remarks>
	public void DeserializeHandle(byte[] serializedItem)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(2012);
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItem);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   Serialize the content of a handle.
	/// </summary>
	/// <returns>Handle containing the serialized item.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>序列化 content 句柄。</para>
	///   <para><b>典型场景</b></para>
	///   <para>对象在内存中的序列化传递</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   var result = obj.SerializeHandle();
	///   </code>
	/// </remarks>
	public byte[] SerializeHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2015);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   Test if a tuple is serializable.
	/// </summary>
	/// <returns>Boolean value indicating if the input can be serialized.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>测试 if 元组 is serializable。</para>
	///   <para><b>典型场景</b></para>
	///   <para>元组数值与集合运算</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   var result = obj.TupleIsSerializable();
	///   </code>
	/// </remarks>
	public int TupleIsSerializable()
	{
		IntPtr proc = JlNativeApi.PreCall(2018);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Check if a handle is valid.
	/// </summary>
	/// <returns>The validity of the handle, 1 or 0.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Check if 句柄 is valid。</para>
	///   <para><b>典型场景</b></para>
	///   <para>元组数值与集合运算</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   var result = obj.TupleIsValidHandle();
	///   </code>
	/// </remarks>
	public JlTuple TupleIsValidHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2020);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   Return the semantic type of a tuple.
	/// </summary>
	/// <returns>Semantic type of the input tuple as a string.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回 semantic type 元组。</para>
	///   <para><b>典型场景</b></para>
	///   <para>元组数值与集合运算</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlHandle obj = ...;
	///   var result = obj.TupleSemType();
	///   </code>
	/// </remarks>
	public string TupleSemType()
	{
		IntPtr proc = JlNativeApi.PreCall(2021);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}
}
