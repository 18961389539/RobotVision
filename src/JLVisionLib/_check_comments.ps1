$c = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes('e:\JLVisionLib\JLVisionLib.Runtime\JlTuple.cs'))
$names = 'TupleLessEqual','TupleLess','TupleGreaterEqual','TupleGreater','TupleNotEqual','TupleEqual','TupleXor','TupleOr','TupleAnd','TupleBnot','TupleBxor','TupleBor','TupleBand','TupleRsh','TupleLsh','TupleAdd','TupleSub','TupleMult','TupleDiv','TuplePow','TupleNeg'
foreach ($n in $names) {
  $p = "[System.Text.RegularExpressions]::"
}
# Just print any template-bad markers remaining
Write-Output '=== Remaining mojibake/template markers (算子, 元组X。, "计算 logical", "Shift 元组") ==='
$patterns = @('算子','元组','计算 logical','Shift 元组','测试, whether','元组Less','元组Greater','Add two 元组','Subtract two 元组','Divide two 元组','Multiply two 元组','Compute the','Test whether','Test, whether')
foreach ($p in $patterns) {
  $m = [System.Text.RegularExpressions]::Regex.Matches($c, $p)
  Write-Output ($p + ' : ' + $m.Count)
}