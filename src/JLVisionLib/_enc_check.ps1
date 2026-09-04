$b = [System.IO.File]::ReadAllBytes('e:\JLVisionLib\JLVisionLib.Runtime\JlTuple.cs')
Write-Output ('TotalBytes: ' + $b.Length)
$c = [System.Text.Encoding]::UTF8.GetString($b)
$i = $c.IndexOf('TupleGreaterEqual(JlTuple')
Write-Output ('idx: ' + $i)
if ($i -lt 0) { $i = [System.Text.Encoding]::UTF8.GetString($b).Length - 400 }
$start = [Math]::Max(0, $i - 300)
$seg = $c.Substring($start, 500)
Write-Output '---UTF8 decode segment---'
Write-Output $seg
$hasMoji = $c.IndexOf([char]0xFFFD)
Write-Output ('hasReplacementChar: ' + $hasMoji)