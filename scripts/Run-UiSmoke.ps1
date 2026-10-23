<#
.SYNOPSIS
	Runs the real-UI smoke tests: the built program is started off-screen, driven through UI Automation and photographed.

.DESCRIPTION
	The tests of FalloutScriptStudio.UiTests start the real FalloutScriptStudio.exe (a private copy of its build output) with the UI test mode
	(FSS_UI_TEST=1): every window of the program sits far outside of the monitors, never takes the focus and has no taskbar button, so the
	developer can keep working while the suite runs. The program is driven through UI Automation and each window is captured with PrintWindow.

	The tests are skipped unless FSS_RUN_UI_TESTS=1, which this script sets. A plain "dotnet test" of the solution therefore never starts the program.

	Output (default _test_files\work\ui-smoke):
	  report.txt                one line per step, with the captured windows and the findings
	  <test>\<capture>.png      the picture of every window that was captured
	  <test>\<capture>.txt      the UI Automation tree of the window (names, types, bounds)

	Run it again to replace the previous output. The folder is inside _test_files\work, which is git-ignored.

.PARAMETER Configuration
	Debug (default) or Release: the build of the program that is tested.

.PARAMETER Filter
	A "dotnet test --filter" expression, for example "FullyQualifiedName~DialogueTests" to run one class.

.PARAMETER OutputDirectory
	Where the pictures and the report go (FSS_UI_SMOKE_OUT). Default: _test_files\work\ui-smoke.

.PARAMETER Keep
	Keeps the data folders of the program (settings, log) under _test_files\work for a post mortem (FSS_UI_SMOKE_KEEP=1).

.PARAMETER NoBuild
	Does not build: the program and the tests must have been built in the same configuration.

.EXAMPLE
	.\scripts\Run-UiSmoke.ps1
	.\scripts\Run-UiSmoke.ps1 -Configuration Release -Filter "FullyQualifiedName~EditorTests"
#>
[CmdletBinding()]
param(
	[ValidateSet('Debug', 'Release')]
	[string]$Configuration = 'Debug',

	[string]$Filter,

	[string]$OutputDirectory,

	[switch]$Keep,

	[switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$tests = Join-Path $root 'FalloutScriptStudio.UiTests'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	throw 'The .NET SDK was not found. Install the .NET 10 SDK.'
}

if (-not $OutputDirectory) {
	$OutputDirectory = Join-Path $root '_test_files\work\ui-smoke'
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

# A run replaces the previous pictures.
if (Test-Path $OutputDirectory) {
	Remove-Item $OutputDirectory -Recurse -Force
}

$previous = @{}
foreach ($name in 'FSS_RUN_UI_TESTS', 'FSS_UI_SMOKE_OUT', 'FSS_UI_SMOKE_KEEP') {
	$previous[$name] = [Environment]::GetEnvironmentVariable($name)
}

try {
	$env:FSS_RUN_UI_TESTS = '1'
	$env:FSS_UI_SMOKE_OUT = $OutputDirectory
	if ($Keep) {
		$env:FSS_UI_SMOKE_KEEP = '1'
	}

	$arguments = @('test', $tests, '--configuration', $Configuration)
	if ($NoBuild) {
		$arguments += '--no-build'
	}

	if ($Filter) {
		$arguments += @('--filter', $Filter)
	}

	Write-Host "Running the UI smoke tests ($Configuration). The program's windows stay off-screen; you can keep working." -ForegroundColor Cyan
	& dotnet @arguments
	$code = $LASTEXITCODE
}
finally {
	foreach ($name in $previous.Keys) {
		[Environment]::SetEnvironmentVariable($name, $previous[$name])
	}
}

if (Test-Path $OutputDirectory) {
	$pictures = @(Get-ChildItem $OutputDirectory -Filter *.png -Recurse).Count
	Write-Host "Report:   $(Join-Path $OutputDirectory 'report.txt')" -ForegroundColor Green
	Write-Host "Pictures: $pictures PNG file(s) under $OutputDirectory" -ForegroundColor Green
}

exit $code