# Packs the WebGL build (unity/Builds/WebGL) into the archive the Yandex Games console accepts — the same as pack.py,
# for machines without Python. Checks: index.html in the archive root, archive <= 100 MB, file names ASCII without
# spaces, index.html with relative paths only (no absolute URLs, no leading "/" except /sdk.js).
# Writes unity/Builds/Rustwave_yandex.zip. Usage: powershell -ExecutionPolicy Bypass -File tools/yandex/pack.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Join-Path $PSScriptRoot "..\..\unity\Builds" | Resolve-Path
$build = Join-Path $root "WebGL"
$out = Join-Path $root "Rustwave_yandex.zip"
$maxMb = 100

function Fail($message) { Write-Host "ERROR: $message"; exit 1 }

$index = Join-Path $build "index.html"
if (-not (Test-Path $index)) { Fail "no index.html in $build (build first: Tools > Build WebGL Player)" }
$html = Get-Content $index -Raw -Encoding UTF8
foreach ($m in [regex]::Matches($html, '(?:src|href)\s*=\s*"([^"]+)"')) {
    $url = $m.Groups[1].Value
    if ($url -match '^(https?:)?//' -or ($url.StartsWith("/") -and $url -ne "/sdk.js")) { Fail "index.html references a non-relative path: $url" }
}

$files = Get-ChildItem $build -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($build.Length + 1).Replace("\", "/")
    if ($rel -notmatch '^[A-Za-z0-9._/\-]+$') { Fail "file name not allowed by Yandex (spaces / non-ASCII): $rel" }
    [pscustomobject]@{ Rel = $rel; Path = $_.FullName }
} | Sort-Object Rel

if (Test-Path $out) { Remove-Item $out }
# Build files are already Brotli-compressed: store them; compress the rest.
$zip = [System.IO.Compression.ZipFile]::Open($out, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in $files) {
        $level = if ($f.Rel -match '\.(unityweb|br|gz|png|jpg)$') { [System.IO.Compression.CompressionLevel]::NoCompression }
                 else { [System.IO.Compression.CompressionLevel]::Optimal }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.Path, $f.Rel, $level) | Out-Null
    }
} finally { $zip.Dispose() }

$read = [System.IO.Compression.ZipFile]::OpenRead($out)
try {
    if (-not ($read.Entries | Where-Object FullName -eq "index.html")) { Fail "index.html is not in the archive root" }
    foreach ($e in $read.Entries) { "{0,8:N2} MB  {1}" -f ($e.Length / 1MB), $e.FullName }
    $count = $read.Entries.Count
} finally { $read.Dispose() }
$size = (Get-Item $out).Length / 1MB
"{0}: {1:N1} MB, {2} files, index.html in the root" -f $out, $size, $count
if ($size -gt $maxMb) { Fail ("archive is {0:N1} MB, Yandex allows {1} MB" -f $size, $maxMb) }
