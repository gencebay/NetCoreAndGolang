[CmdletBinding()]
param (
    [Parameter(Mandatory = $True, Position = 1)][string]$name,
    [Parameter(Mandatory = $True)][string]$binPath
)

function InstallService([string]$name, [string]$binPath) {

    if (!$name) {
        throw "Service name required"
    }

    New-Service -Name "$name" -BinaryPathName "$binPath" -StartupType Automatic
    Start-Service -Name "$name"
}

function UninstallService([string]$name) {

    if (!$name) {
        throw "Service name required"
    }

    $service = Get-WmiObject -Class Win32_Service -Filter "Name='$name'"
    if ($service) {
        $service.delete();
    }   
}

InstallService $name $binPath