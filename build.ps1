param (
    [Parameter(Mandatory=$true)]
    [string]$GameManagedPath,
    [Parameter(Mandatory=$true)]
    [string]$BepInExCorePath,
    [Parameter(Mandatory=$true)]
    [string]$CompilerPath,
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = (Join-Path $PSScriptRoot 'output')
)

$ErrorActionPreference = 'Stop'

# Resolve and validate mandatory paths
$GameManagedPath = Resolve-Path -LiteralPath $GameManagedPath
$BepInExCorePath = Resolve-Path -LiteralPath $BepInExCorePath
$CompilerPath   = Resolve-Path -LiteralPath $CompilerPath

# Validate referenced DLLs
$references = @()
$references += '/reference:' + (Join-Path $GameManagedPath 'Unity.Postprocessing.Runtime.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.AssetBundleModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'Assembly-CSharp.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'Rewired_Core.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'Unity.TextMeshPro.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.CoreModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.AudioModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.AnimationModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.ParticleSystemModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.UI.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.UIModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.IMGUIModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.InputModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.InputLegacyModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.TextRenderingModule.dll')
$references += '/reference:' + (Join-Path $GameManagedPath 'UnityEngine.ImageConversionModule.dll')
$references += '/reference:' + (Join-Path $BepInExCorePath 'BepInEx.dll')
$references += '/reference:' + (Join-Path $BepInExCorePath '0Harmony.dll')

foreach ($ref in $references) {
    $path = $ref -replace '/reference:', ''
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Referenced DLL not found: $path"
    }
}

# Validate package path
$packagePath = Join-Path $PSScriptRoot 'Thunderstore'
if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Package path does not exist: $packagePath"
}

# Create output directory if it does not exist
if (-not (Test-Path -LiteralPath $OutputPath)) {
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
}

# Read version from manifest.json
$manifestPath = Join-Path $packagePath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in package path: $manifestPath"
}
$manifestContent = Get-Content -Raw -LiteralPath $manifestPath
$manifest = $manifestContent | ConvertFrom-Json
$version = $manifest.version_number

# Compile the plugin DLL
$pluginOutput = Join-Path $packagePath 'plugins\QuickSpraySelector\QuickSpraySelector.dll'
$pluginDir = Split-Path -Parent $pluginOutput
if (-not (Test-Path -LiteralPath $pluginDir)) {
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
}

$sourceFiles = @(
    'Plugin.cs',
    'BackgroundBlur.cs',
    'SelectorCameraMotion.cs',
    'PresentationTiming.cs',
    'CameraMotionState.cs',
    'Patches.cs',
    'NativeSprayPose.cs',
    'SelectorPaintCloud.cs',
    'SelectorForegroundDots.cs',
    'SelectionAudio.cs',
    'WheelSelection.cs',
    'WheelAimInput.cs',
    'WheelStyle.cs',
    'PaintVisuals.cs',
    'WirkleMotion.cs',
    'WheelMotion.cs',
    'GameButtonHints.cs',
    'LastGraffitiStore.cs'
) | ForEach-Object { Join-Path $PSScriptRoot $_ }

$arguments = @(
    '/nologo',
    '/target:library',
    '/optimize+',
    ('/out:' + $pluginOutput),
    ('/resource:' + (Join-Path $PSScriptRoot 'assets\dance-pointer.png') + ',QuickPickGraffiti.dance-pointer.png')
) + $references + $sourceFiles

& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }

# Package the plugin
$zipPath = Join-Path $OutputPath ('QuickSpraySelector-' + $version + '.zip')
Compress-Archive -Path (Join-Path $packagePath '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

Write-Output "Built $pluginOutput"
Write-Output "Packaged $zipPath"
