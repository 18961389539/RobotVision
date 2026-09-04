using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace JLVisionLib;

/// <summary>JLVision 向量基类：用于支持 JlDevelop 导出代码的向量运算，并向使用向量参数的过程传参。</summary>
/// <remarks>
///   <para><b>嵌套模型</b>向量是嵌套容器：维度 N 的向量，每个元素是维度 N-1 的子向量；
///   维度为 0 的“叶”不持子列表，具体负载（元组/对象）由子类承载。</para>
///   <para><b>使用边界</b>本类型为导出/传参设计，<b>不推荐</b>当通用容器用；
///   通用场景请使用 <c>List&lt;T&gt;</c> 等标准集合。</para>
///   <para><b>实例化</b>本类为抽象类，只能创建 <see cref="JlTupleVector"/> 或 <see cref="JlObjectVector"/> 的实例。</para>
///   <para><b>所有权约定</b>子向量归向量所有并由它释放：Clear/Remove/Dispose 逐个 Dispose 元素；
///   索引器写入会深拷贝新值并释放被替换的旧元素；免拷贝转移用 <see cref="JlVector.TransferOwnership"/>。</para>
/// </remarks>
public abstract class JlVector : ICloneable, IDisposable
{
	internal int mDimension;

		/// <summary>子向量列表（维度 &gt; 0 时持有；叶向量为 null）。</summary>
		/// <remarks>本字段兼作并发访问的锁对象——Length/索引器/At 等读取均在 <c>lock(mVector)</c> 内进行。</remarks>
	protected List<JlVector> mVector;

	/// <summary>声明维度：0=叶向量（不持子列表）；N=该向量容纳 N-1 维子向量。</summary>
	/// <remarks>维度在构造时固定，不随元素增删而变化。</remarks>
	public int Dimension => mDimension;

	/// <summary>当前子向量个数；叶向量（维度≤0）恒为 0。</summary>
	/// <remarks>读取以 <c>mVector</c> 为锁，线程安全。</remarks>
	public int Length
	{
		get
		{
			if (mDimension <= 0)
			{
				return 0;
			}
			lock (mVector)
			{
				return mVector.Count;
			}
		}
	}

	/// <summary>按下标访问子向量（读或写）。</summary>
	/// <remarks>读取越界会自动扩容（用默认子向量填充）——不想扩容的读请用 <see cref="JlVector.At"/>；
	/// 写入会深拷贝新值并释放被替换的旧元素。返回的是内部子向量的引用，需独立改动时先 Clone。</remarks>
	public JlVector this[int index]
	{
		get
		{
			if (mDimension < 1 || index < 0)
			{
				throw new JlVectorAccessException("Index out of range");
			}
			AssertSize(index);
			lock (mVector)
			{
				return mVector[index];
			}
		}
		set
		{
			if (mDimension < 1 || index < 0)
			{
				throw new JlVectorAccessException("Index out of range");
			}
			if (value.Dimension != mDimension - 1)
			{
				throw new JlVectorAccessException("Vector dimension mismatch");
			}
			AssertSize(index);
			JlVector hVector;
			lock (mVector)
			{
				hVector = mVector[index];
				mVector[index] = value.Clone();
			}
			hVector.Dispose();
		}
	}

	/// <summary>按指定维度构造向量。</summary>
	/// <param name="dimension">维度：0=叶；大于 0 时创建空的子向量列表。</param>
	/// <remarks>负数抛 <see cref="JlVectorAccessException"/>；子向量列表按需扩容。</remarks>
	protected JlVector(int dimension)
	{
		if (dimension < 0)
		{
			throw new JlVectorAccessException("Invalid vector dimension " + dimension);
		}
		mDimension = dimension;
		mVector = ((dimension > 0) ? new List<JlVector>() : null);
	}

	/// <summary>拷贝构造：复制维度并深拷贝（Clone）所有子向量元素。</summary>
	/// <param name="vector">源向量。</param>
	/// <remarks>构造后本向量与原向量完全独立。</remarks>
	protected JlVector(JlVector vector)
		: this(vector.Dimension)
	{
		if (mDimension > 0)
		{
			mVector.Capacity = vector.Length;
			for (int i = 0; i < vector.Length; i++)
			{
				mVector.Add(vector[i].Clone());
			}
		}
	}

	/// <summary>把 <paramref name="source"/> 的子向量列表整体转移给本向量（source 随即置空），避免深拷贝。</summary>
	/// <param name="source">源向量；与本向量维度必须一致，可为 null（仅做释放）。</param>
	/// <remarks>转移前本向量原有内容先 Dispose；源为叶或维度不符抛 <see cref="JlVectorAccessException"/>。</remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void TransferOwnership(JlVector source)
	{
		if (source == this)
		{
			return;
		}
		if (source != null && source.Dimension != Dimension)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		Dispose();
		if (source != null)
		{
			if (mDimension <= 0)
			{
				throw new JlVectorAccessException("TransferOwnership not implemented for leaf");
			}
			mVector = source.mVector;
			source.mVector = new List<JlVector>();
			GC.ReRegisterForFinalize(this);
		}
	}

	/// <summary>断言本向量维度等于 <paramref name="dimension"/>，否则抛 <see cref="JlVectorAccessException"/>。</summary>
	/// <param name="dimension">期望维度。</param>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void AssertDimension(int dimension)
	{
		if (mDimension != dimension)
		{
			throw new JlVectorAccessException("Expected vector dimension " + dimension);
		}
	}

	private void AssertSize(int index)
	{
		if (mVector == null)
		{
			return;
		}
		lock (mVector)
		{
			int count = mVector.Count;
			if (index >= count)
			{
				mVector.Capacity = index + 1;
				for (int i = count; i <= index; i++)
				{
					mVector.Add(GetDefaultElement());
				}
			}
		}
	}

	/// <summary>创建并返回一个默认空子向量（维度=本向量-1），用于越界读取/插入时的自动扩容填充。</summary>
	/// <returns>新的默认子向量。</returns>
	/// <remarks>由子类按元素类型实现。</remarks>
	protected abstract JlVector GetDefaultElement();

	/// <summary>按下标只读访问子向量：越界抛 <see cref="JlVectorAccessException"/>（不做自动扩容）。</summary>
	/// <remarks>返回内部子向量引用，需独立改动时先 Clone。</remarks>
	public JlVector At(int index)
	{
		if (mDimension < 1 || index < 0 || index >= Length)
		{
			throw new JlVectorAccessException("Index out of range");
		}
		lock (mVector)
		{
			return mVector[index];
		}
	}

	/// <summary>逐元素比较实现：维度与长度一致，且每个对应子向量 <c>VectorEqual</c> 时返回 true。</summary>
	/// <param name="vector">待比较向量。</param>
	/// <remarks>叶级（元素为标量/句柄）的比较语义由子类覆写决定。</remarks>
	protected virtual bool EqualsImpl(JlVector vector)
	{
		if (vector.Dimension != Dimension)
		{
			return false;
		}
		if (vector.Length != Length)
		{
			return false;
		}
		if (mDimension > 0)
		{
			for (int i = 0; i < Length; i++)
			{
				if (!this[i].VectorEqual(vector[i]))
				{
					return false;
				}
			}
		}
		return true;
	}

	/// <summary>判断两向量相等：类型相同、维度与长度一致、且逐元素 <c>VectorEqual</c>。</summary>
	public bool VectorEqual(JlVector vector)
	{
		if ((object)vector.GetType() != GetType())
		{
			return false;
		}
		return EqualsImpl(vector);
	}

	/// <summary>拼接实现。</summary>
	/// <param name="vector">待拼入向量，维度须与本向量一致。</param>
	/// <param name="append">true=在本向量上追加并返回 this；false=深拷贝本向量后拼接并返回新向量。</param>
	/// <param name="clone">true=元素深拷贝；false=元素按引用并入（调用方须保证其生命周期）。</param>
	/// <returns>拼接结果向量。</returns>
	protected JlVector ConcatImpl(JlVector vector, bool append, bool clone)
	{
		if (mDimension < 1 || vector.Dimension != mDimension)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		JlVector hVector = (append ? this : Clone());
		hVector.mVector.Capacity = Length + vector.Length;
		for (int i = 0; i < vector.Length; i++)
		{
			hVector.mVector.Add(clone ? vector[i].Clone() : vector[i]);
		}
		return hVector;
	}

	/// <summary>拼接两个向量为新向量（元素深拷贝），本向量与入参不变。</summary>
	public JlVector Concat(JlVector vector)
	{
		return ConcatImpl(vector, append: false, clone: true);
	}

	/// <summary>拼接出包含两个向量元素的新向量（可选手动指定是否深拷贝元素）。</summary>
	/// <param name="vector">待拼入向量。</param>
	/// <param name="clone">true=元素深拷贝；false=vector 的元素按引用并入。</param>
	/// <returns>新向量，本向量与入参保持不变。</returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Concat(JlVector vector, bool clone)
	{
		return ConcatImpl(vector, append: false, clone);
	}

	/// <summary>把 vector 的元素深拷贝后追加到本向量末尾，返回 this。</summary>
	public JlVector Append(JlVector vector)
	{
		return ConcatImpl(vector, append: true, clone: true);
	}

	/// <summary>把 <paramref name="vector"/> 的元素追加到本向量末尾并返回 this。</summary>
	/// <param name="vector">待追加向量，维度须与本向量一致。</param>
	/// <param name="clone">true=元素深拷贝后追加；false=按引用并入。</param>
	/// <returns>本向量（追加后）。</returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Append(JlVector vector, bool clone)
	{
		return ConcatImpl(vector, append: true, clone);
	}

	/// <summary>在 index 处插入子向量元素。</summary>
	/// <param name="index">插入位置；负数抛 <see cref="JlVectorAccessException"/>，越界自动扩容。</param>
	/// <param name="vector">待插入向量，维度须=本向量-1。</param>
	/// <param name="clone">true=深拷贝后插入；false=按引用插入。</param>
	protected void InsertImpl(int index, JlVector vector, bool clone)
	{
		if (mDimension < 1 || vector.Dimension != mDimension - 1)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		if (index < 0)
		{
			throw new JlVectorAccessException("Index out of range");
		}
		AssertSize(index - 1);
		lock (mVector)
		{
			mVector.Insert(index, clone ? vector.Clone() : vector);
		}
	}

	/// <summary>在指定下标插入一个子向量（深拷贝），返回 this。</summary>
	public JlVector Insert(int index, JlVector vector)
	{
		InsertImpl(index, vector, clone: true);
		return this;
	}

	/// <summary>在 index 处插入一个子向量并返回 this。</summary>
	/// <param name="index">插入位置。</param>
	/// <param name="vector">待插入的子向量（维度=本向量-1）。</param>
	/// <param name="clone">true=深拷贝；false=按引用插入。</param>
	/// <returns>本向量。</returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Insert(int index, JlVector vector, bool clone)
	{
		InsertImpl(index, vector, clone);
		return this;
	}

	/// <summary>移除 index 处的子向量并释放（Dispose）它。</summary>
	/// <param name="index">待移除位置；越界时静默忽略。</param>
	/// <remarks>释放被移除元素是所有权约定的一部分，勿在外部重复 Dispose。</remarks>
	protected void RemoveImpl(int index)
	{
		if (mDimension < 1)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		if (index >= 0 && index < Length)
		{
			lock (mVector)
			{
				mVector[index].Dispose();
				mVector.RemoveAt(index);
			}
		}
	}

	/// <summary>移除指定下标的子向量并释放之，返回 this。</summary>
	public JlVector Remove(int index)
	{
		RemoveImpl(index);
		return this;
	}

	/// <summary>清空全部子向量并逐个 Dispose（Clear 与 Dispose 共用的实现）。</summary>
	/// <remarks>叶向量（维度&lt;1）调用抛 <see cref="JlVectorAccessException"/>。</remarks>
	protected virtual void ClearImpl()
	{
		if (mDimension < 1)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		lock (mVector)
		{
			for (int i = 0; i < Length; i++)
			{
				mVector[i].Dispose();
			}
			mVector.Clear();
		}
	}

	/// <summary>清空全部子向量并逐个释放，返回 this。</summary>
	public JlVector Clear()
	{
		ClearImpl();
		return this;
	}

	/// <summary>深拷贝本向量的实现钩子（由子类实现）。</summary>
	/// <returns>内容完全独立的新向量。</returns>
	protected abstract JlVector CloneImpl();

	object ICloneable.Clone()
	{
		return CloneImpl();
	}

	/// <summary>深拷贝本向量（<c>ICloneable.Clone</c> 与单参 <c>Clone()</c> 的统一实现入口）。</summary>
	/// <returns>内容完全独立的新向量。</returns>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Clone()
	{
		return CloneImpl();
	}

	/// <summary>释放叶负载资源的钩子：叶向量的 <see cref="JlVector.Dispose()"/> 会调用本方法。</summary>
	/// <remarks>非叶向量由 Clear 逐元素释放，不走本方法；基类默认为空实现。</remarks>
	protected virtual void DisposeLeafObject()
	{
	}

	/// <summary>释放本向量：非叶等价于 Clear（清空并逐个 Dispose 子向量）；叶则调用 <see cref="JlVector.DisposeLeafObject"/>。</summary>
	/// <remarks>先执行 <c>GC.SuppressFinalize</c>，防止终结器重复释放。</remarks>
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		if (mDimension > 0)
		{
			Clear();
		}
		else
		{
			DisposeLeafObject();
		}
	}

	/// <summary>返回向量的文本表示（嵌套 <c>{...}</c>），主要用于调试输出。</summary>
	public override string ToString()
	{
		if (mDimension <= 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{");
		for (int i = 0; i < Length; i++)
		{
			if (i != 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(this[i].ToString());
		}
		stringBuilder.Append("}");
		return stringBuilder.ToString();
	}
}
