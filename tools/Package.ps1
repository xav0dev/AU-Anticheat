param(
 [Parameter(Mandatory=$true)][string]$LoaderDirectory,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [Parameter(Mandatory=$true)][string]$StageDirectory
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot=Split-Path $PSScriptRoot -Parent
$loaderRoot=(Resolve-Path -LiteralPath $LoaderDirectory).Path
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
$stageRoot=[IO.Path]::GetFullPath($StageDirectory)
if((Test-Path -LiteralPath $outputRoot) -or (Test-Path -LiteralPath $stageRoot)){throw 'Use NEW output and staging directories. Existing releases are not overwritten.'}
New-Item -ItemType Directory -Path $outputRoot,$stageRoot | Out-Null
$dll=Join-Path $projectRoot 'source/bin/Release/net6.0/AU-Anticheat.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Build Release first.'}
$documents=@('README.md','DOCUMENTATION.md','LICENSES.md','POLICY.md','LICENSE','VERIFICATION.txt')
function Copy-Tree([string]$from,[string]$to) {
 New-Item -ItemType Directory -Path $to -Force | Out-Null
 foreach($file in Get-ChildItem -LiteralPath $from -Recurse -File -Force) {
  $relative=[IO.Path]::GetRelativePath($from,$file.FullName)
  if($relative -match '(^|[\\/])(bin|obj|\.git|\.vs|\.idea)([\\/]|$)'){continue}
  if($file.Extension -in @('.dll','.pdb','.zip','.log','.cfg','.user','.suo')){throw "Unexpected private/generated file: $relative"}
  $destination=Join-Path $to $relative
  New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
  Copy-Item -LiteralPath $file.FullName -Destination $destination
 }
}
function Copy-Docs([string]$to) {
 New-Item -ItemType Directory -Path $to -Force | Out-Null
 foreach($name in $documents){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $to $name)}
 foreach($folder in @('assets','licenses')){Copy-Tree (Join-Path $projectRoot $folder) (Join-Path $to $folder)}
}
Copy-Item -LiteralPath $dll -Destination (Join-Path $outputRoot 'AU-Anticheat.dll')
Copy-Docs $outputRoot
$sourceStage=Join-Path $stageRoot 'Source'
Copy-Docs $sourceStage
foreach($folder in @('source','services','tools')){Copy-Tree (Join-Path $projectRoot $folder) (Join-Path $sourceStage $folder)}
foreach($name in @('AU-Anticheat.sln','.gitignore')){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $sourceStage $name)}
[IO.Compression.ZipFile]::CreateFromDirectory($sourceStage,(Join-Path $outputRoot 'AU-Anticheat-1.0.0-beta.2-Source.zip'),[IO.Compression.CompressionLevel]::Optimal,$false)
$testStage=Join-Path $stageRoot 'Tests'
Copy-Tree $sourceStage $testStage
foreach($folder in @('tests','ui-tests','lobby-tests','role-tests','movement-tests','service-tests')){Copy-Tree (Join-Path $projectRoot $folder) (Join-Path $testStage $folder)}
[IO.Compression.ZipFile]::CreateFromDirectory($testStage,(Join-Path $outputRoot 'AU-Anticheat-1.0.0-beta.2-Tests.zip'),[IO.Compression.CompressionLevel]::Optimal,$false)
foreach($arch in @('x64','x86')) {
 $install=Join-Path $stageRoot "Install-$arch"
 $loader=Join-Path $loaderRoot "BepInEx-IL2CPP-$arch-788.zip"
 # Inspect archive paths before extraction, and only extract to a new directory.
 $zip=[IO.Compression.ZipFile]::OpenRead($loader)
 try {
  foreach($entry in $zip.Entries) {
   $path=[IO.Path]::GetFullPath((Join-Path $install $entry.FullName))
   if(!$path.StartsWith($install+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe loader archive path'}
  }
 } finally {$zip.Dispose()}
 [IO.Compression.ZipFile]::ExtractToDirectory($loader,$install)
 $pluginFolder=Join-Path $install 'BepInEx/plugins/AU-Anticheat'
 New-Item -ItemType Directory -Path $pluginFolder -Force | Out-Null
 Copy-Item -LiteralPath $dll -Destination (Join-Path $pluginFolder 'AU-Anticheat.dll')
 Copy-Docs (Join-Path $install 'AU-Anticheat-docs')
 $suffix=if($arch -eq 'x86'){'x86-UNVERIFIED'}else{'x64'}
 [IO.Compression.ZipFile]::CreateFromDirectory($install,(Join-Path $outputRoot "AU-Anticheat-1.0.0-beta.2-Install-$suffix.zip"),[IO.Compression.CompressionLevel]::Optimal,$false)
}
Get-ChildItem -LiteralPath $outputRoot -File | Select-Object Name,Length
