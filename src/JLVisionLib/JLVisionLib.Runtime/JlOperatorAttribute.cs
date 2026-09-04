using System;

namespace JLVisionLib;

/// <summary>标记算子（Operator）包装方法，携带其对应的原生逻辑名称。</summary>
/// <remarks>
///   <para><b>功能说明</b>：标注一个静态方法是对某个原生 Vision 算子的托管包装，
///   并通过 <c>LogicalName</c> 记录该算子在原生运行时的逻辑名称（logical name）。</para>
///   <para><b>典型场景</b>：需要按名称查找、注册或反射地动态调用某个算子时，
///   读取本特性即可拿到逻辑名，避免在多处硬编码算子字符串。</para>
///   <para><b>约束</b>：仅允许标注在方法上；一个方法至多标注一次，且不可由子类继承重复标注。</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class JlOperatorAttribute : Attribute
{
	private string logicalName;

	/// <summary>该包装方法对应的原生算子逻辑名称。</summary>
	public virtual string LogicalName => logicalName;

	/// <summary>以逻辑名称构造特性实例。</summary>
	/// <param name="logicalName">原生算子的逻辑名称。</param>
	public JlOperatorAttribute(string logicalName)
	{
		this.logicalName = logicalName;
	}
}
