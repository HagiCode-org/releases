[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

Set-StrictMode -Version 2.0; $ErrorActionPreference = "Stop"; $ConfirmPreference = "None"; trap { Write-Error $_ -ErrorAction Continue; exit 1 }
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent

$LocalSecretsFile = if ($env:HAGICODE_LOCAL_SECRETS_FILE) { $env:HAGICODE_LOCAL_SECRETS_FILE } else { "$PSScriptRoot\.env.secrets.local" }
if (-not $env:GITHUB_ACTIONS -and (Test-Path $LocalSecretsFile)) {
    Get-Content $LocalSecretsFile | ForEach-Object {
        $line = $_.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith("#")) { return }
        $separatorIndex = $line.IndexOf("=")
        if ($separatorIndex -lt 1) { return }
        $name = $line.Substring(0, $separatorIndex).Trim()
        $value = $line.Substring($separatorIndex + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        [Environment]::SetEnvironmentVariable($name, $value)
    }
    Write-Output "Loaded local secrets override from $LocalSecretsFile"
}

if ($env:BUILD_ENGINE -eq "nuke") {
    Write-Error "Nuke build engine is no longer supported for repos/hagicode-release; use PyBuild/Invoke."
    exit 1
}

$PythonBin = if ($env:PYTHON_EXE) { $env:PYTHON_EXE } elseif (Get-Command python3 -ErrorAction SilentlyContinue) { "python3" } else { "python" }
Write-Output "Using PyBuild/Invoke build engine"
Push-Location $PSScriptRoot
try {
    & $PythonBin -m pybuild.entry @BuildArguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
