@echo off
chcp 65001 >nul
echo Dang khoi dong VIGO... (can cai .NET 8 SDK: https://dotnet.microsoft.com/download)
start "" http://localhost:5077
dotnet run --project src\Vigo.Game -c Release
pause
