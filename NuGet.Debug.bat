@echo off

set LOCAL_NUGET_FEED_DIRECTORY=%1
if not DEFINED LOCAL_NUGET_FEED_DIRECTORY goto WrongParameters

for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"

rem Trim lead zeros
set /a YY=1%YY%+1%YY%-2%YY%
set /a MM=1%MM%+1%MM%-2%MM%
set /a DD=1%DD%+1%DD%-2%DD%
set /a HH=1%HH%+1%HH%-2%HH%
set /a Min=1%Min%+1%Min%-2%Min%
set /a Sec=1%Sec%+1%Sec%-2%Sec%

set major=9999
set suffix=LocalDebug

set version=%major%.%YY%%MM%.%DD%%HH%.%Min%%Sec%-%suffix%
echo version: "%version%"

dotnet build --no-restore -p:Version=%version%

for /R ..\ %%i IN (*%version%.nupkg) DO (
echo Copying %%i package
    xcopy "%%i" "%LOCAL_NUGET_FEED_DIRECTORY%"
)

goto end

:WrongParameters
echo Wrong parameters number are specified!
echo Usage: batch.bat LOCAL_NUGET_FEED_DIRECTORY
goto end

:end

@echo on