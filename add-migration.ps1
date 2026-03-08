<#
Utility script for creating new EF Core migrations with the correct projects and paths.
Usage:
    .\add-migration.ps1 -Name "MyMigrationName"

This navigates into the Infrastructure project, runs `dotnet ef migrations add`,
pointing at Web as the startup project and placing migrations in Data/Migrations.
#>
param (
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Name
)

Write-Host "Adding migration '$Name'..."

Push-Location "brain\src\Infrastructure"
try {
    dotnet ef migrations add $Name --startup-project ..\Web --output-dir Data/Migrations
} finally {
    Pop-Location
}

Write-Host "Migration command finished."