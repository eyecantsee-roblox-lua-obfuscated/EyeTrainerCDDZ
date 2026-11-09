@echo off
setlocal EnableDelayedExpansion

rem ============================================================
rem  Builds CDDZTrainerMod.dll against your installed game + Mod-Bot.
rem  Usage:   build.bat                    (uses the default Steam path below)
rem           build.bat "D:\Games\Clone Drone in the Danger Zone"
rem ============================================================

set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Clone Drone in the Danger Zone"
if not "%~1"=="" set "GAME_DIR=%~1"

set "DATA_DIR=%GAME_DIR%\Clone Drone in the Danger Zone_Data"
set "MANAGED=%DATA_DIR%\Managed"
set "HERE=%~dp0"
set "OUT=%HERE%out"
set "RSP=%OUT%\refs.rsp"

if not exist "%MANAGED%\Assembly-CSharp.dll" (
    echo [ERROR] Could not find the game's Managed folder:
    echo         !MANAGED!
    echo         Pass your game folder as the first argument, or edit GAME_DIR in this file.
    exit /b 1
)

rem Mod-Bot's ModLibrary.dll normally sits in Managed (Mod-Bot's injector puts it there);
rem older installs keep it in a ModBot folder. Look in all the usual places.
set "MODLIB="
if exist "%MANAGED%\ModLibrary.dll" set "MODLIB=%MANAGED%\ModLibrary.dll"
if not defined MODLIB if exist "%GAME_DIR%\ModBot\ModLibrary.dll" set "MODLIB=%GAME_DIR%\ModBot\ModLibrary.dll"
if not defined MODLIB if exist "%DATA_DIR%\ModBot\ModLibrary.dll" set "MODLIB=%DATA_DIR%\ModBot\ModLibrary.dll"
if not defined MODLIB if exist "%HERE%libs\ModLibrary.dll" set "MODLIB=%HERE%libs\ModLibrary.dll"
if not defined MODLIB (
    echo [ERROR] Could not find ModLibrary.dll. Looked in:
    echo         !MANAGED!
    echo         !GAME_DIR!\ModBot
    echo         !DATA_DIR!\ModBot
    echo         !HERE!libs   ^(you can drop a copy here^)
    echo         Open the Mod-Bot launcher and make sure Mod-Bot is installed into the game.
    exit /b 1
)
echo Using ModLibrary: !MODLIB!

rem Built-in Windows C# compiler (supports C# 5, which is all this source uses).
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. Install the .NET Framework 4.x developer pack or Visual Studio.
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

rem Reference everything in Managed through a response file (the list is too long for one command line).
if exist "%RSP%" del "%RSP%"
if exist "%MANAGED%\mscorlib.dll" echo /nostdlib+>>"%RSP%"
for %%f in ("%MANAGED%\*.dll") do echo /r:"%%f">>"%RSP%"
if /i not "!MODLIB!"=="%MANAGED%\ModLibrary.dll" echo /r:"!MODLIB!">>"%RSP%"

echo Compiling...
"%CSC%" /nologo /noconfig /target:library /optimize+ /nowarn:618,612,1701,1702,1607 /out:"%OUT%\CDDZTrainerMod.dll" @"%RSP%" "%HERE%src\*.cs"
if errorlevel 1 (
    echo.
    echo [BUILD FAILED] Your a dumbass.
    exit /b 1
)

copy /y "%HERE%ModInfo.json" "%OUT%\ModInfo.json" >nul

rem Install into the game's mods folder if it exists.
if exist "%GAME_DIR%\mods" (
    if not exist "!GAME_DIR!\mods\CDDZTrainerMod" mkdir "!GAME_DIR!\mods\CDDZTrainerMod"
    copy /y "!OUT!\CDDZTrainerMod.dll" "!GAME_DIR!\mods\CDDZTrainerMod\" >nul
    copy /y "!OUT!\ModInfo.json" "!GAME_DIR!\mods\CDDZTrainerMod\" >nul
    echo.
    echo [OK] Built and installed to: !GAME_DIR!\mods\CDDZTrainerMod
) else (
    echo.
    echo [OK] Built to: !OUT!
    echo      No "mods" folder found in the game directory, so copy CDDZTrainerMod.dll,
    echo      ModInfo.json and icon.png into a new folder inside your Mod-Bot mods folder yourself.
)
endlocal
