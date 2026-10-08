@echo off
setlocal
echo ========================================================
echo Building Duplicates by S1mplee67 (Offline Duplicate App)
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set WPF="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
set NET="C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

if not exist %CSC% (
    echo Error: C# compiler csc.exe not found at %CSC%
    exit /b 1
)

%CSC% /target:winexe /optimize+ /out:Duplicates-by-S1mplee67.exe ^
  /r:"%WPF%\PresentationFramework.dll" ^
  /r:"%WPF%\PresentationCore.dll" ^
  /r:"%WPF%\WindowsBase.dll" ^
  /r:"%NET%\System.Xaml.dll" ^
  /r:"%NET%\System.dll" ^
  /r:"%NET%\System.Core.dll" ^
  /r:"%NET%\System.Drawing.dll" ^
  /r:"%NET%\System.Windows.Forms.dll" ^
  /r:"%NET%\Microsoft.VisualBasic.dll" ^
  Program.cs Models.cs ImageHasher.cs Scanner.cs FileOperations.cs CompareWindow.cs MainWindow.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo Build SUCCESS! Output: Duplicates-by-S1mplee67.exe
    echo ========================================================
) else (
    echo.
    echo Build FAILED! Please inspect errors above.
    exit /b %ERRORLEVEL%
)
