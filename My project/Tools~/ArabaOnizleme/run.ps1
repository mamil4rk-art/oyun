$p = $PSScriptRoot
$b = dotnet build "$p\preview.csproj" -nologo -v q | Out-String
if ($LASTEXITCODE -ne 0) { $b; exit 1 }
New-Item -ItemType Directory -Force "$p\out" | Out-Null
dotnet run --project "$p\preview.csproj" --no-build -- "$p\out" | Select-String -Pattern "EKSİK|NaN|Toplam|Bitti"
Add-Type -AssemblyName System.Drawing
Get-ChildItem "$p\out\*.bmp" | ForEach-Object {
    $img = [System.Drawing.Image]::FromFile($_.FullName)
    $img.Save(($_.FullName -replace '\.bmp$', '.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $img.Dispose()
}
