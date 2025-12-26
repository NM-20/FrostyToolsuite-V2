# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/FrostyEditor/*" -Force -Recurse
dotnet publish "./FrostyEditor.csproj" -c Release -o "$env:RELOADEDIIMODS/FrostyEditor" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location