param(
    [switch]$Run,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Der C#-Compiler von .NET Framework 4.x wurde nicht gefunden.'
}
$outputDirectory = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$executable = Join-Path $outputDirectory 'SpaceMouseBridge.exe'
$sources = @('Native.cs', 'AutoStart.cs', 'Input.cs', 'AppTypography.cs', 'AppTheme.cs', 'ThemeTests.cs', 'ApplicationMappings.cs', 'ApplicationProfiles.cs', 'Mappings.cs', 'MappingEditor.cs', 'PremiereJog.cs', 'PremiereEditor.cs', 'PremiereBindings.cs', 'PremiereMappingEditor.cs', 'PremiereMenu.cs', 'PremiereBindingTests.cs', 'Program.cs', 'SelfTest.cs', 'MappingTests.cs', 'ProfileTests.cs', 'JogTests.cs') |
    ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ `
    "/win32manifest:$PSScriptRoot\app.manifest" `
    "/win32icon:$PSScriptRoot\assets\orbit-cap.ico" `
    "/resource:$PSScriptRoot\assets\orbit-cap.ico,SpaceMouseBridge.OrbitCap.ico" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Xml.dll `
    "/out:$executable" $sources
if ($LASTEXITCODE -ne 0) {
    throw "Kompilierung fehlgeschlagen (Exitcode $LASTEXITCODE)."
}
Write-Host "Erstellt: $executable"
if ($Test) {
    $process = Start-Process -FilePath $executable -ArgumentList '--self-test' -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "Selbsttests fehlgeschlagen (Exitcode $($process.ExitCode))."
    }
    Write-Host 'Selbsttests fuer Decoder, Gesten, Anwendungsprofile, Mappings, Tastaturausgabe und XML-Sicherung erfolgreich.'
}
if ($Run) {
    $process = Start-Process -FilePath $executable -PassThru
    Write-Host "Diagnosefenster gestartet (PID $($process.Id))."
}
