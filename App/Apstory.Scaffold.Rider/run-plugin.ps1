$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
Write-Host "Using JAVA_HOME: $env:JAVA_HOME" -ForegroundColor Green
Write-Host ""
Write-Host "Running Rider plugin in development mode..." -ForegroundColor Cyan
Write-Host "This will download dependencies on first run (may take 5-10 minutes)" -ForegroundColor Yellow
Write-Host ""

.\gradlew.bat runIde
