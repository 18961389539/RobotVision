namespace JLVisionLib;

/// <summary>
///   Enumeration of tuple types, as returned by JlTuple.Type
/// </summary>
public enum JlTupleType
{
	/// <summary>Tuple is empty</summary>
	EMPTY = 31,
	/// <summary>Tuple is represented by an array of System.Int32</summary>
	INTEGER = 1,
	/// <summary>Tuple is represented by an array of System.Int64</summary>
	LONG = 129,
	/// <summary>Tuple is represented by an array of System.Double</summary>
	DOUBLE = 2,
	/// <summary>Tuple is represented by an array of strings</summary>
	STRING = 4,
	/// <summary>Tuple is represented by an array of JlHandle values</summary>
	JlANDLE = 16,
	/// <summary>Tuple is represented by an object array of boxed values.</summary>
	MIXED = 8
}
