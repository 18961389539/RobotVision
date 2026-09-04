using System.ComponentModel;

namespace JLVisionLib;

/// <summary>
///   The Vision vector classes are intended to support the export of
///   JlDevelop code that uses vectors, and to pass vector arguments to
///   procedures that use vector parameters. They are not intended to be
///   used as generic container classes in user code. For this purpose,
///   consider using standard container classes such as List&lt;T&gt;.
/// </summary>
public class JlObjectVector : JlVector
{
	private JlObject mObject;

	/// <summary>
	///   Access to the object value for leaf vectors (dimension 0).
	///   Ownership of object resides with object vector and it will
	///   be disposed when the vector is disposed. Copy the object
	///   to create an object that will survive a vector dispose.
	///   When storing an object in the vector, it will be
	///   copied automatically.
	/// </summary>
	public JlObject O
	{
		get
		{
			AssertDimension(0);
			return mObject;
		}
		set
		{
			AssertDimension(0);
			if (value == null || !value.IsInitialized())
			{
				throw new JlVectorAccessException("Uninitialized object not allowed in vector");
			}
			mObject.Dispose();
			mObject = new JlObject(value);
		}
	}

	/// <summary>
	///   Access to subvector at specified index. The vector will be
	///   enlarged to accommodate index, even in read access. The internal
	///   reference is returned to allow modifications of vector state. For
	///   read access, preferrably use the member function At(index).
	/// </summary>
	public new JlObjectVector this[int index]
	{
		get
		{
			return (JlObjectVector)base[index];
		}
		set
		{
			base[index] = value;
		}
	}

	/// <summary>
	///   Create empty vector of specified dimension. In case of dimension
	///   0 a leaf vector for an empty object is created
	/// </summary>
	public JlObjectVector(int dimension)
		: base(dimension)
	{
		mObject = ((dimension <= 0) ? GenEmptyObj() : null);
	}

	/// <summary>
	///   Create leaf vector of dimension 0 for the specified object
	/// </summary>
	public JlObjectVector(JlObject obj)
		: base(0)
	{
		if (obj == null || !obj.IsInitialized())
		{
			throw new JlVectorAccessException("Uninitialized object not allowed in vector");
		}
		mObject = new JlObject(obj);
	}

	/// <summary>
	///   Create copy of object vector
	/// </summary>
	public JlObjectVector(JlObjectVector vector)
		: base(vector)
	{
		if (mDimension <= 0)
		{
			mObject = new JlObject(vector.mObject);
		}
	}

	private static JlObject GenEmptyObj()
	{
		JlObject hObject = new JlObject();
		hObject.GenEmptyObj();
		return hObject;
	}

	/// <summary>创建维度为 本向量-1 的默认空子向量（叶级负载由对象 O属性承载），供扩容填充。</summary>
	protected override JlVector GetDefaultElement()
	{
		return new JlObjectVector(mDimension - 1);
	}

	/// <summary>
	///   Read access to subvector at specified index. An exception
	///   will be raised if index is out of range. The returned data
	///   is a copy and may be stored safely.
	/// </summary>
	public new JlObjectVector At(int index)
	{
		return (JlObjectVector)base.At(index);
	}

	/// <summary>相等比较实现：非叶走基类逐元素比较；叶级比较元组内容（TupleEqual）或对象相等性。</summary>
	protected override bool EqualsImpl(JlVector vector)
	{
		if (mDimension >= 1)
		{
			return base.EqualsImpl(vector);
		}
		return ((JlObjectVector)vector).O.TestEqualObj(O) != 0;
	}

	/// <summary>
	///   Returns true if vector has same dimension, lengths, and elements
	/// </summary>
	public bool VectorEqual(JlObjectVector vector)
	{
		return EqualsImpl(vector);
	}

	/// <summary>
	///   Concatenate two vectors, creating new vector
	/// </summary>
	public JlObjectVector Concat(JlObjectVector vector)
	{
		return (JlObjectVector)ConcatImpl(vector, append: false, clone: true);
	}

	/// <summary>返回强类型 <see cref="JlObjectVector"/> 的新向量 = 本向量与 vector 拼接（clone 控制元素是否深拷贝，语义同基类）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObjectVector Concat(JlObjectVector vector, bool clone)
	{
		return (JlObjectVector)ConcatImpl(vector, append: false, clone);
	}

	/// <summary>
	///   Append vector to this vector
	/// </summary>
	public JlObjectVector Append(JlObjectVector vector)
	{
		return (JlObjectVector)ConcatImpl(vector, append: true, clone: true);
	}

	/// <summary>把 vector 元素追加到本向量末尾并返回强类型 this（clone 控制深拷贝）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObjectVector Append(JlObjectVector vector, bool clone)
	{
		return (JlObjectVector)ConcatImpl(vector, append: true, clone);
	}

	/// <summary>
	///   Insert vector at specified index
	/// </summary>
	public JlObjectVector Insert(int index, JlObjectVector vector)
	{
		InsertImpl(index, vector, clone: true);
		return this;
	}

	/// <summary>在本向量 index 处插入 vector 并返回强类型 this（clone 控制深拷贝）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObjectVector Insert(int index, JlObjectVector vector, bool clone)
	{
		InsertImpl(index, vector, clone);
		return this;
	}

	/// <summary>
	///   Remove element at specified index from this vector
	/// </summary>
	public new JlObjectVector Remove(int index)
	{
		RemoveImpl(index);
		return this;
	}

	/// <summary>
	///   Remove all elements from this vector
	/// </summary>
	public new JlObjectVector Clear()
	{
		ClearImpl();
		return this;
	}

	/// <summary>
	///    Create an independent copy of this vector
	/// </summary>
	public new JlObjectVector Clone()
	{
		return (JlObjectVector)CloneImpl();
	}

	/// <summary>深拷贝本向量：叶负载一并复制。</summary>
	protected override JlVector CloneImpl()
	{
		return new JlObjectVector(this);
	}

	/// <summary>释放叶级负载（元组或对象）。</summary>
	protected override void DisposeLeafObject()
	{
		if (mDimension <= 0)
		{
			mObject.Dispose();
		}
	}

	/// <summary>
	///   Provides a simple string representation of the vector,
	///   which is mainly useful for debug outputs.
	/// </summary>
	public override string ToString()
	{
		if (mDimension <= 0)
		{
			return mObject.Key.ToString();
		}
		return base.ToString();
	}
}
