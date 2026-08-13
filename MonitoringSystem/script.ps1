
$connStr = 'Server=localhost;Database=Panamon_AC;Integrated Security=True;TrustServerCertificate=True;'
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = 'SELECT SDate, ShiftMode, TotalUnit, MachineCode, Product_Id FROM OEESN WHERE MONTH(SDate) = 7 AND YEAR(SDate) = 2026 AND DAY(SDate) = 6 ORDER BY SDate'
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host $reader['SDate'] -NoNewline; Write-Host '|' -NoNewline; Write-Host $reader['ShiftMode'] -NoNewline; Write-Host '|' -NoNewline; Write-Host $reader['TotalUnit'] -NoNewline; Write-Host '|' -NoNewline; Write-Host $reader['MachineCode'] -NoNewline; Write-Host '|' -NoNewline; Write-Host $reader['Product_Id']
}
$conn.Close()

