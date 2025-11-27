# DesignBuilder Plugin Installation Script
# This script copies the OsmToGbXml plugin to the DesignBuilder plugins folder

$pluginFolderName = "OSM"
$userPluginsBase = "$env:LOCALAPPDATA\DesignBuilder\User Plugins"
$pluginsFolder = Join-Path $userPluginsBase $pluginFolderName
$sourceFile = "bin\Release\net48\OSM.dll"

# Check if source file exists
if (-not (Test-Path $sourceFile)) {
    Write-Host "Error: Plugin DLL not found at $sourceFile" -ForegroundColor Red
    Write-Host "Please build the project first using: dotnet build -c Release" -ForegroundColor Yellow
    exit 1
}

# Create plugin folder if it doesn't exist
if (-not (Test-Path $pluginsFolder)) {
    Write-Host "Creating plugin folder: $pluginsFolder" -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $pluginsFolder | Out-Null
}

# Copy plugin
Write-Host "Installing plugin..." -ForegroundColor Cyan
Copy-Item $sourceFile -Destination $pluginsFolder -Force

# Copy dependencies
Write-Host "Copying dependencies..." -ForegroundColor Cyan
$sourceBinFolder = "bin\Release\net48"

# Copy WebView2 DLLs
Copy-Item "$sourceBinFolder\Microsoft.Web.WebView2.*.dll" -Destination $pluginsFolder -Force

# Copy Newtonsoft.Json DLL
Copy-Item "$sourceBinFolder\Newtonsoft.Json.dll" -Destination $pluginsFolder -Force

# Create Resources folder if needed
$resourcesFolder = Join-Path $pluginsFolder "Resources"
if (-not (Test-Path $resourcesFolder)) {
    New-Item -ItemType Directory -Force -Path $resourcesFolder | Out-Null
}

# Copy HTML interface
if (Test-Path "$sourceBinFolder\Resources\MapInterface.html") {
    Copy-Item "$sourceBinFolder\Resources\MapInterface.html" -Destination $resourcesFolder -Force
}

# Copy help file
if (Test-Path "$sourceBinFolder\Resources\help_readme.md") {
    Copy-Item "$sourceBinFolder\Resources\help_readme.md" -Destination $resourcesFolder -Force
}

# Copy favicon
if (Test-Path "$sourceBinFolder\Resources\favicon.svg") {
    Copy-Item "$sourceBinFolder\Resources\favicon.svg" -Destination $resourcesFolder -Force
}

# Copy icon
if (Test-Path "$sourceBinFolder\Resources\Openstreetmap_logo.png") {
    Copy-Item "$sourceBinFolder\Resources\Openstreetmap_logo.png" -Destination $resourcesFolder -Force
}

# Copy WebView2Loader.dll (x86 version)
if (Test-Path "$sourceBinFolder\runtimes\win-x86\native\WebView2Loader.dll") {
    Copy-Item "$sourceBinFolder\runtimes\win-x86\native\WebView2Loader.dll" -Destination $pluginsFolder -Force
}

if ($?) {
    Write-Host "`nPlugin installed successfully!" -ForegroundColor Green
    Write-Host "Location: $pluginsFolder\OSM.dll" -ForegroundColor Green
    Write-Host "`nDependencies installed:" -ForegroundColor Green
    Write-Host "  - Microsoft.Web.WebView2 (Core, WinForms, Wpf)" -ForegroundColor Gray
    Write-Host "  - Newtonsoft.Json" -ForegroundColor Gray
    Write-Host "  - WebView2Loader.dll" -ForegroundColor Gray
    Write-Host "  - Resources\MapInterface.html" -ForegroundColor Gray
    Write-Host "  - Resources\help_readme.md" -ForegroundColor Gray
    Write-Host "  - Resources\Openstreetmap_logo.png" -ForegroundColor Gray
    
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Close DesignBuilder if it's running" -ForegroundColor White
    Write-Host "2. Restart DesignBuilder" -ForegroundColor White
    Write-Host "3. Look for 'OSM' menu in DesignBuilder" -ForegroundColor White
}
else {
    Write-Host "`nError: Failed to copy plugin or dependencies" -ForegroundColor Red
    exit 1
}
