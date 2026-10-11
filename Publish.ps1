<#
.SYNOPSIS
	Builds Fallout Script Studio as two standalone executables in the "publish" folder.

.DESCRIPTION
	FalloutScriptStudio.exe      The program alone (framework-dependent, a few MB). It needs the .NET 10 Desktop Runtime (x86) on the computer.
	FalloutScriptStudio-NET.exe  The program with the .NET runtime inside (about 70 MB). Nothing has to be installed.

	Each file is a single executable. The data the editor reads at run time ("descriptions" and "resources") is copied next to
	them once and is shared by both executables. The previous "publish" folder is replaced only when both builds succeeded.

	FalloutScriptStudio.ParserHost.exe   The 32-bit process that runs the optional native parser.dll of the sfall compiler (about 12 MB,
	                                     self-contained and trimmed). It sits next to both executables and is shared by them.

	The application and the parser host are built for x86 because parser.dll is a 32-bit library.

.EXAMPLE
	.\Publish.ps1
	.\Publish.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
	[ValidateSet('Release', 'Debug')]
	[string]$Configuration = 'Release',

	[ValidateSet('win-x86')]
	[string]$Runtime = 'win-x86'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'FalloutScriptStudio\FalloutScriptStudio.csproj'
$hostProject = Join-Path $root 'FalloutScriptStudio.ParserHost\FalloutScriptStudio.ParserHost.csproj'
$output = Join-Path $root 'publish'
$staging = Join-Path $root 'publish.tmp'

# Both builds produce "FalloutScriptStudio.exe" (the assembly name); the second one is renamed when it is moved to "publish".
$variants = @(
	@{ Name = 'FalloutScriptStudio.exe'; SelfContained = $false },
	@{ Name = 'FalloutScriptStudio-NET.exe'; SelfContained = $true }
)

# Run-time data folders that sit next to the executables (copied from the first build).
$dataFolders = @('descriptions', 'resources')

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	throw 'The .NET SDK was not found. Install the .NET 10 SDK.'
}

function Remove-Folder([string]$path) {
	if (Test-Path $path) {
		Remove-Item $path -Recurse -Force
	}
}

# The builds go to a temporary folder: the previous publish is replaced only when the new ones succeeded.
Remove-Folder $staging

try {
	foreach ($variant in $variants) {
		$selfContained = if ($variant.SelfContained) { 'true' } else { 'false' }
		$folder = Join-Path $staging ([System.IO.Path]::GetFileNameWithoutExtension($variant.Name))

		$arguments = @(
			'publish', $project,
			'--configuration', $Configuration,
			'--runtime', $Runtime,
			'--self-contained', $selfContained,
			'--output', $folder,
			'-p:PublishSingleFile=true',
			'-p:DebugType=None',
			'-p:DebugSymbols=false'
		)

		if ($variant.SelfContained) {
			# The WPF native libraries are packed into the file and unpacked on first start.
			$arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
			$arguments += '-p:EnableCompressionInSingleFile=true'
		}

		Write-Host "Publishing $($variant.Name) ($Configuration, $Runtime, self-contained: $selfContained)..." -ForegroundColor Cyan
		& dotnet @arguments
		if ($LASTEXITCODE -ne 0) {
			throw "dotnet publish failed with exit code $LASTEXITCODE for $($variant.Name)."
		}
	}

	# The parser host (a 32-bit process that runs parser.dll, so that a crash of that library cannot take the editor down) is shared by
	# both executables and sits next to them. It is self-contained and trimmed, because the same file has to work for the executable
	# that needs no installed runtime.
	Write-Host "Publishing FalloutScriptStudio.ParserHost.exe ($Configuration, $Runtime, self-contained, trimmed)..." -ForegroundColor Cyan
	$hostFolder = Join-Path $staging 'ParserHost'
	& dotnet publish $hostProject `
		--configuration $Configuration `
		--runtime $Runtime `
		--self-contained true `
		--output $hostFolder `
		-p:PublishSingleFile=true `
		-p:IncludeNativeLibrariesForSelfExtract=true `
		-p:EnableCompressionInSingleFile=true `
		-p:PublishTrimmed=true `
		-p:TrimMode=partial `
		-p:SuppressTrimAnalysisWarnings=true `
		-p:DebugType=None `
		-p:DebugSymbols=false
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet publish failed with exit code $LASTEXITCODE for FalloutScriptStudio.ParserHost.exe."
	}

	# Start from a clean folder so that no file of an older publish is left behind.
	Remove-Folder $output
	New-Item -ItemType Directory -Path $output | Out-Null

	foreach ($variant in $variants) {
		$folder = Join-Path $staging ([System.IO.Path]::GetFileNameWithoutExtension($variant.Name))
		Move-Item (Join-Path $folder 'FalloutScriptStudio.exe') (Join-Path $output $variant.Name)
	}

	Move-Item (Join-Path $hostFolder 'FalloutScriptStudio.ParserHost.exe') (Join-Path $output 'FalloutScriptStudio.ParserHost.exe')

	$firstFolder = Join-Path $staging ([System.IO.Path]::GetFileNameWithoutExtension($variants[0].Name))
	foreach ($data in $dataFolders) {
		$source = Join-Path $firstFolder $data
		if (Test-Path $source) {
			Copy-Item $source (Join-Path $output $data) -Recurse
		}
	}
}
finally {
	Remove-Folder $staging

	# The self-contained, trimmed builds above share the "bin" and "obj" folders of the normal build. Left there, they make the next
	# ordinary build run the parser host with the wrong runtime settings, so the folders of this configuration are cleared (the next
	# "dotnet build" recreates them).
	foreach ($name in @('FalloutScriptStudio', 'FalloutScriptStudio.Core', 'FalloutScriptStudio.ParserHost')) {
		# The x86 projects put their output under a platform folder (bin\x86\Release), the others directly under bin\Release.
		foreach ($platform in @('', 'x86\')) {
			Remove-Folder (Join-Path $root "$name\bin\$platform$Configuration")
			Remove-Folder (Join-Path $root "$name\obj\$platform$Configuration")
		}
	}
}

foreach ($variant in $variants) {
	$exe = Get-Item (Join-Path $output $variant.Name)
	$size = [math]::Round($exe.Length / 1MB, 1)
	Write-Host "Done: $($exe.FullName) ($size MB)" -ForegroundColor Green
}
