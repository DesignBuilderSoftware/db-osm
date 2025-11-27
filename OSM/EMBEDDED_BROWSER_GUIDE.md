# Embedded Browser Integration Guide

## Overview

The ATM plugin now features an **embedded browser** with **direct OpenStreetMap API integration**, eliminating the need for users to manually download OSM files. Users can now select areas directly from an interactive map and import building data with a single click.

## What's New

### Before
1. Click "OpenStreetMap website" → Opens external browser
2. Navigate to desired location on openstreetmap.org
3. Manually export OSM file
4. Download file to computer
5. Click "Load OpenStreetMap file" → Browse to downloaded file
6. Import into DesignBuilder

### After
1. Click **"Select Area from Map"** → Embedded map opens
2. Draw a rectangle on the map to select area
3. Click **"Load to DesignBuilder"** → Done!

## New Files Added

### 1. MapBrowserForm.cs
- **Purpose**: Windows Form that hosts the embedded WebView2 browser
- **Key Features**:
  - Displays interactive OpenStreetMap using Leaflet.js
  - Handles JavaScript-to-C# communication
  - Fires events when user selects a bounding box
  - Error handling for WebView2 initialization

### 2. OverpassApiClient.cs
- **Purpose**: Client for fetching OSM data from Overpass API
- **Key Features**:
  - Constructs Overpass QL queries for building data
  - Handles HTTP requests with proper timeouts (60 seconds)
  - Validates bounding boxes (lat/lon ranges, area size)
  - Returns OSM XML data directly

### 3. Resources/MapInterface.html
- **Purpose**: Embedded HTML page with interactive map
- **Key Features**:
  - Uses Leaflet.js for map rendering
  - Leaflet Draw plugin for rectangle selection
  - Real-time area calculation
  - Warnings for large areas (>5 km²)
  - Clean, modern UI with toolbar

## Modified Files

### DesignBuilderPlugin.cs
- Added new menu item: **"Select Area from Map"**
- Implemented `SelectAreaFromMap()` method with:
  - MapBrowserForm integration
  - Async/await pattern for API calls
  - Progress dialog during download and conversion
  - Error handling and user feedback
- Updated `ModelLoaded()` and `ModelUnloaded()` to enable/disable new button

### OSM.csproj
- Added **Microsoft.Web.WebView2** NuGet package (v1.0.2420.47)
- Added **Newtonsoft.Json** NuGet package (v13.0.3)
- Added **System.Net.Http** reference for API calls
- Embedded **MapInterface.html** as resource

### Namespace Changes
- Changed all namespaces from `OSM2GBXML` to `OSM` for consistency

## Technical Architecture

### Data Flow

```
User draws rectangle on map
         ↓
JavaScript sends bounding box to C# via WebView2 message
         ↓
C# validates bounding box
         ↓
OverpassApiClient fetches OSM data from API
         ↓
OsmToGbXmlConverter converts XML to gbXML
         ↓
DesignBuilder API imports model
```

### JavaScript-to-C# Communication

The embedded HTML uses WebView2's `postMessage` API:

```javascript
// JavaScript (in MapInterface.html)
window.chrome.webview.postMessage(JSON.stringify(bbox));
```

```csharp
// C# (in MapBrowserForm.cs)
webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
```

### Overpass API Query

The plugin uses Overpass QL (Query Language) to fetch building data:

```
[out:xml][timeout:60];
(
  way["building"]({south},{west},{north},{east});
  way["building:part"]({south},{west},{north},{east});
);
(._;>;);
out meta;
```

This query:
1. Sets output format to XML with 60-second timeout
2. Finds all ways tagged with "building" or "building:part" in bounding box
3. Recursively includes all referenced nodes
4. Outputs complete metadata

## User Interface

### Plugin Menu Structure

```
OSM
├── Select Area from Map    [NEW - Primary method]
├── Load OpenStreetMap file [Legacy - File-based import]
└── OpenStreetMap website   [Opens external browser]
```

### Map Interface Features

- **Zoom Controls**: Top-left corner
- **Drawing Tool**: Top-right corner (rectangle icon)
- **Toolbar**:
  - Title and instructions
  - "Clear Selection" button
  - "Load to DesignBuilder" button (enabled when area is selected)
- **Status Panel**: Bottom-left
  - Shows selected area size
  - Displays bounding box coordinates
  - Warnings for large areas

## Area Limits and Validation

### Validation Rules
- Latitude: Must be between -90° and 90°
- Longitude: Must be between -180° and 180°
- South < North
- West < East

### Size Warnings
- **Warning threshold**: 5 km² (shown in map interface)
- **Large area threshold**: 25 km² (confirmation dialog)
- Areas larger than 25 km² may:
  - Take longer to download
  - Timeout (60-second API limit)
  - Contain hundreds of buildings

## Error Handling

### WebView2 Runtime Check
If Microsoft Edge WebView2 Runtime is not installed:
- User-friendly error message
- Download link provided
- Form closes gracefully

### API Errors
- **Network errors**: "Failed to fetch data from Overpass API"
- **Timeouts**: "Request timed out. Please select a smaller area."
- **Parse errors**: Caught and displayed to user

### Conversion Errors
- **No buildings found**: Warning dialog, no import
- **Invalid OSM data**: Error message with details

## Installation

### Prerequisites
- **Microsoft Edge WebView2 Runtime**
  - Usually pre-installed on Windows 10/11
  - Download: https://developer.microsoft.com/en-us/microsoft-edge/webview2/

### Build and Install

```powershell
# Build the plugin
cd c:\GitHub\db-osm\OSM
dotnet build --configuration Release

# Install to DesignBuilder
.\install-plugin.ps1
```

## Dependencies

### NuGet Packages
| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Web.WebView2 | 1.0.2420.47 | Embedded Chromium browser |
| Newtonsoft.Json | 13.0.3 | JSON parsing for JS-to-C# messages |

### .NET References
- System.ComponentModel.Composition (MEF plugin framework)
- System.Windows.Forms (UI)
- System.Net.Http (API calls)
- System.Xml.Linq (XML parsing)

### External APIs
- **Overpass API**: https://overpass-api.de/api/interpreter
  - Public API for OpenStreetMap data
  - No API key required
  - Fair use policy applies

### JavaScript Libraries (CDN)
- **Leaflet.js** v1.9.4 (map rendering)
- **Leaflet Draw** v1.0.4 (drawing tools)

## Usage Tips

### For Best Results
1. **Start with smaller areas** (~1-2 km²) to test
2. **Use appropriate zoom level** (zoom 14-16 for neighborhoods)
3. **Avoid very large areas** - split into multiple imports if needed
4. **Check area size** in status panel before loading

### Keyboard Shortcuts
- **Escape**: Cancel drawing
- **Delete**: Remove selected shape (while editing)

### Default Map Location
- Centered on London, UK (51.505°N, 0.09°W)
- Zoom level 13
- Users can pan/zoom to any location worldwide

## Troubleshooting

### "Failed to initialize embedded browser"
- **Cause**: WebView2 Runtime not installed
- **Solution**: Install from https://go.microsoft.com/fwlink/p/?LinkId=2124703

### "Request timed out"
- **Cause**: Area too large or slow API response
- **Solution**: Select a smaller area and try again

### "No buildings found"
- **Cause**: Selected area has no buildings in OSM database
- **Solution**: Try a different area or check openstreetmap.org for data coverage

### Map doesn't load
- **Cause**: No internet connection
- **Solution**: Ensure internet connectivity (required for Leaflet.js CDN and map tiles)

## Future Enhancements (Potential)

1. **Location Search**: Add geocoding to search for addresses/cities
2. **Building Filtering**: Filter by building type (residential, commercial, etc.)
3. **Preview Mode**: Show building count before importing
4. **Offline Mode**: Cache map tiles and Leaflet.js locally
5. **Custom Styling**: Apply different colors to building types
6. **Area History**: Save frequently used areas

## API Rate Limits

The Overpass API has fair use policies:
- Maximum 60-second query timeout (configured in code)
- Avoid running too many queries in quick succession
- If blocked, wait a few minutes before retrying

## Security Considerations

- WebView2 runs in sandboxed environment
- Only communication is via WebMessageReceived event
- No eval() or dynamic code execution in JavaScript
- HTTPS used for all CDN resources (Leaflet, map tiles)
- Temporary files deleted after import

## Code Architecture Highlights

### Async/Await Pattern
```csharp
mapForm.BoundingBoxSelected += async (sender, args) =>
{
    string osmXml = await apiClient.FetchBuildingsInBoundingBox(...);
    // Process data
};
```

### Resource Embedding
```csharp
var assembly = Assembly.GetExecutingAssembly();
var resourceName = "OSM.Resources.MapInterface.html";
using (Stream stream = assembly.GetManifestResourceStream(resourceName))
{
    // Load HTML
}
```

### Event-Driven Communication
```csharp
public event EventHandler<BoundingBoxSelectedEventArgs> BoundingBoxSelected;
```

## Summary

The embedded browser integration transforms the ATM plugin from a two-step manual process into a seamless one-click workflow. Users can now:

✅ Select areas visually from an interactive map
✅ See real-time area calculations and warnings
✅ Import buildings directly without downloading files
✅ Get immediate feedback on progress and errors

All functionality is implemented **entirely in C#** with embedded HTML/JavaScript for the map interface, ensuring a native integration with DesignBuilder.
