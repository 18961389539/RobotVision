namespace JLVisionLib;

/// <summary>
///   The Vision vector classes are intended to support the export of
///   JlDevelop code that uses vectors, and to pass vector arguments to
///   procedures that use vector parameters. They are not intended to be
///   used as generic container classes in user code. For this purpose,
///   consider using standard container classes such as List&lt;T&gt;.
/// </summary>
public class JlTupleVector : JlVector
{
	private JlTuple mTuple;

	/// <summary>
	///   Access to the tuple value for leaf vectors (dimension 0)
	/// </summary>
	public JlTuple T
	{
		get
		{
			AssertDimension(0);
			return mTuple;
		}
		set
		{
			AssertDimension(0);
			if (value == null)
			{
				throw new JlVectorAccessException("Null tuple not allowed in vector");
			}
			mTuple.Dispose();
			mTuple = new JlTuple(value);
		}
	}

	/// <summary>
	///   Access to subvector at specified index. The vector will be
	///   enlarged to accommodate index, even in read access. The internal
	///   reference is returned to allow modifications of vector state. For
	///   read access, preferrably use the member function At(index).
	/// </summary>
	public new JlTupleVector this[int index]
	{
		get
		{
			return (JlTupleVector)base[index];
		}
		set
		{
			base[index] = value;
		}
	}

	/// <summary>
	///   Create empty vector of specified dimension. In case of dimension
	///   0 a leaf vector for an empty tuple is created
	/// </summary>
	public JlTupleVector(int dimension)
		: base(dimension)
	{
		mTuple = ((dimension <= 0) ? new JlTuple() : null);
	}

	/// <summary>
	///   Create leaf vector of dimension 0 for the specified tuple
	/// </summary>
	public JlTupleVector(JlTuple tuple)
		: base(0)
	{
		if (tuple == null)
		{
			throw new JlVectorAccessException("Null tuple not allowed in vector");
		}
		mTuple = new JlTuple(tuple);
	}

	/// <summary>
	///   Create 1-dimensional vector by splitting input tuple into
	///   blocks of fixed size (except possibly for the last block).
	///   This corresponds to convert_tuple_to_vector_1d in JlDevelop.
	/// </summary>
	public JlTupleVector(JlTuple tuple, int blockSize)
		: base(1)
	{
		if (blockSize <= 0)
		{
			throw new JlVectorAccessException("Invalid block size in vector constructor");
		}
		int num = tuple.Length / blockSize;
		mVector.Capacity = ((num * blockSize < tuple.Length) ? (num + 1) : num);
		int i;
		for (i = 0; i < tuple.Length - blockSize; i += blockSize)
		{
			mVector.Add(new JlTupleVector(tuple.TupleSelectRange(i, i + blockSize - 1)));
		}
		if (i < tuple.Length)
		{
			mVector.Add(new JlTupleVector(tuple.TupleSelectRange(i, tuple.Length - 1)));
		}
	}

	/// <summary>
	///   Create copy of tuple vector
	/// </summary>
	public JlTupleVector(JlTupleVector vector)
		: base(vector)
	{
		if (mDimension <= 0)
		{
			mTuple = new JlTuple(vector.mTuple);
		}
	}

	/// <summary>创建维度为 本向量-1 的默认空子向量（叶级负载由元组 T属性承载），供扩容填充。</summary>
	protected override JlVector GetDefaultElement()
	{
		return new JlTupleVector(mDimension - 1);
	}

	/// <summary>
	///   Read access to subvector at specified index. An exception
	///   will be raised if index is out of range. The returned data
	///   is a copy and may be stored safely.
	/// </summary>
	public new JlTupleVector At(int index)
	{
		return (JlTupleVector)base.At(index);
	}

	/// <summary>相等比较实现：非叶走基类逐元素比较；叶级比较元组内容（TupleEqual）或对象相等性。</summary>
	protected override bool EqualsImpl(JlVector vector)
	{
		if (mDimension >= 1)
		{
			return base.EqualsImpl(vector);
		}
		return ((JlTupleVector)vector).T.TupleEqual(T);
	}

	/// <summary>
	///   Returns true if vector has same dimension, lengths, and elements
	/// </summary>
	public bool VectorEqual(JlTupleVector vector)
	{
		return EqualsImpl(vector);
	}

	/// <summary>
	///   Concatenate two vectors, creating new vector
	/// </summary>
	public JlTupleVector Concat(JlTupleVector vector)
	{
		return (JlTupleVector)ConcatImpl(vector, append: false, clone: true);
	}

	/// <summary>
	///   Append vector to this vector
	/// </summary>
	public JlTupleVector Append(JlTupleVector vector)
	{
		return (JlTupleVector)ConcatImpl(vector, append: true, clone: true);
	}

	/// <summary>
	///   Insert vector at specified index
	/// </summary>
	public JlTupleVector Insert(int index, JlTupleVector vector)
	{
		InsertImpl(index, vector, clone: true);
		return this;
	}

	/// <summary>
	///   Remove element at specified index from this vector
	/// </summary>
	public new JlTupleVector Remove(int index)
	{
		RemoveImpl(index);
		return this;
	}

	/// <summary>
	///   Remove all elements from this vector
	/// </summary>
	public new JlTupleVector Clear()
	{
		ClearImpl();
		return this;
	}

	/// <summary>
	///    Create an independent copy of this vector
	/// </summary>
	public new JlTupleVector Clone()
	{
		return (JlTupleVector)CloneImpl();
	}

	/// <summary>深拷贝本向量：叶负载一并复制。</summary>
	protected override JlVector CloneImpl()
	{
		return new JlTupleVector(this);
	}

	/// <summary>释放叶级负载（元组或对象）。</summary>
	protected override void DisposeLeafObject()
	{
		if (mDimension <= 0)
		{
			mTuple.Dispose();
		}
	}

	private int CountHTuples()
	{
		if (mDimension > 1)
		{
			int num = 0;
			for (int i = 0; i < base.Length; i++)
			{
				num += this[i].CountHTuples();
			}
			return num;
		}
		if (mDimension > 0)
		{
			return base.Length;
		}
		return 1;
	}

	private void CollectHTuples(JlTuple[] tuples, ref int index)
	{
		if (mDimension > 1)
		{
			for (int i = 0; i < base.Length; i++)
			{
				this[i].CollectHTuples(tuples, ref index);
			}
		}
		else if (mDimension > 0)
		{
			for (int j = 0; j < base.Length; j++)
			{
				tuples[index++] = this[j].mTuple;
			}
		}
		else
		{
			tuples[index++] = mTuple;
		}
	}

	/// <summary>
	///   Concatenates all tuples stored in the vector
	/// </summary>
	public JlTuple ConvertVectorToTuple()
	{
		JlTuple[] tuples = new JlTuple[CountHTuples()];
		int index = 0;
		CollectHTuples(tuples, ref index);
		return new JlTuple().TupleConcat(tuples);
	}

	/// <summary>
	///   Provides a simple string representation of the vector,
	///   which is mainly useful for debug outputs.
	/// </summary>
	public override string ToString()
	{
		if (mDimension <= 0)
		{
			return mTuple.ToString();
		}
		return base.ToString();
	}
}
