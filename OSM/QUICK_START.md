# Quick Start Guide

## Installation (3 Easy Steps)

### 1. Build the Plugin
```bash
cd OsmToGbXml
dotnet build -c Release
```

### 2. Install to DesignBuilder
```powershell -ExecutionPolicy Bypass -File install-plugin.ps1
```

Or manually copy `bin\Release\net48\OsmToGbXml.dll` to:
`C:\Users\[YourUsername]\AppData\Roaming\DesignBuilder\User Plugins\`

### 3. Restart DesignBuilder
Close and reopen DesignBuilder to load the plugin.

---

## Using the Plugin

1. **Open DesignBuilder** and load or create a model
2. **Find the OSM2GBXML menu** in the menu bar
3. **Click "Import from OpenStreetMap"**
4. **Select your .osm file**
5. **Done!** The buildings are now imported

---

## What Gets Imported?

- Building footprints from OpenStreetMap
- 3D building geometry (floors, walls, ceilings)
- Building heights (from `height` or `building:levels` tags)
- Building names (from `name` tag)
- Multiple buildings → Multiple spaces in one building

---

## Files Overview

| File | Purpose |
|------|---------|
| **OsmToGbXml.dll** | The DesignBuilder plugin (in bin\Release\net48\) |
| **DesignBuilderPlugin.cs** | Plugin UI code |
| **OsmToGbXmlConverter.cs** | OSM to gbXML conversion logic |
| **Program.cs** | Standalone console app (excluded from plugin) |

---

## Troubleshooting

**Plugin menu doesn't show?**
- Check DLL is in `%APPDATA%\DesignBuilder\Plugins\`
- Restart DesignBuilder

**Menu items are grayed out?**
- Load a model first (File → New or Open)

**No buildings found?**
- Verify your OSM file has `building=*` tags
- Check the OSM file in a text editor or OpenStreetMap

---

## Getting OSM Files

1. Go to https://www.openstreetmap.org
2. Navigate to your area of interest
3. Click **Export**
4. Select area (or use Overpass API for larger areas)
5. Download as `.osm` file

---

## Support

- See [README.md](README.md) for detailed documentation
- Check that DesignBuilder is properly licensed
- Ensure OSM data contains valid building geometries
