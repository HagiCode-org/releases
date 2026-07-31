@echo off
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
