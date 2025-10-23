@echo off
set JAVA_HOME=C:\Program Files\Android\Android Studio\jbr
echo Using JAVA_HOME: %JAVA_HOME%
echo.
echo Running Rider plugin in development mode...
echo This will download dependencies on first run (may take 5-10 minutes)
echo.
call gradlew.bat runIde
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Error occurred! Exit code: %ERRORLEVEL%
    pause
)
