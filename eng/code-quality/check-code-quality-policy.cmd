@echo off
setlocal
if not defined PROSCENIUM_QUALITY_GATE_RUNNER goto direct
if not exist "%PROSCENIUM_QUALITY_GATE_RUNNER%" mkdir "%PROSCENIUM_QUALITY_GATE_RUNNER%"
dotnet run --artifacts-path "%PROSCENIUM_QUALITY_GATE_RUNNER%\artifacts" --project "%~dp0CodeQualityPolicy.csproj" -- %*
exit /b %ERRORLEVEL%

:direct
dotnet run --project "%~dp0CodeQualityPolicy.csproj" -- %*
exit /b %ERRORLEVEL%
