@ECHO off
SC QUERY VHT_LogDog > NUL
IF NOT ERRORLEVEL 1060 GOTO UNINSTALL
GOTO INSTALL

:UNINSTALL
NET Stop VHT_LogDog
SC Delete VHT_LogDog

:INSTALL
SC Create "VHT_LogDog" DisplayName= "VHT LogDog" BinPath= "%~dp0VHT_Scanlog_Service.exe" Start= demand
SC Description VHT_LogDog "Virtual Hold Technology log monitoring utility"