# OSM to gbXML Converter & DesignBuilder Plugin

This project provides two ways to convert OpenStreetMap (OSM) data to gbXML format:

1. **Console Application** - Standalone command-line tool for OSM to gbXML conversion
2. **DesignBuilder Plugin** - Integrated plugin that allows direct import of OSM files into DesignBuilder

## Project Structure

```
OSM/
├── Core Components
│   ├── OsmToGbXmlConverter.cs           - Main conversion logic (OSM → gbXML)
│   ├── OverpassApiClient.cs             - Overpass API client for fetching OSM data
│   └── DesignBuilderPlugin.cs           - DesignBuilder plugin implementation
│
├── UI Components
│   └── MapBrowserForm.cs                - Interactive map browser form
│
├── Resources
│   └── MapInterface.html                - Embedded browser map interface
│
├── Configuration
│   ├── OSM.csproj                       - Project configuration
│   ├── OSM.sln                          - Solution file
│   └── install-plugin.ps1               - PowerShell installation script
│
├── Documentation
│   ├── README.md                        - Main documentation (this file)
│   ├── QUICK_START.md                   - Quick start guide
│   └── EMBEDDED_BROWSER_GUIDE.md        - Guide for embedded browser features
│
└── External Dependencies (local copies)
    ├── DB.Api.dll                       - DesignBuilder API
    └── DB.Extensibility.Contracts.dll   - DesignBuilder extensibility contracts
```

## Usage

### DesignBuilder Plugin

The plugin adds a new menu to DesignBuilder with two options:

1. **Import from OpenStreetMap** - Directly import and convert OSM files
2. **Load gbXML** - Import pre-converted gbXML files

#### Installing the Plugin

1. **Update DesignBuilder API References:**

   Edit `OsmToGbXml.csproj` and uncomment the reference lines, updating the paths to match your DesignBuilder installation:

   ```xml
   <Reference Include="DB.Api">
     <HintPath>C:\Program Files\DesignBuilder\DB.Api.dll</HintPath>
     <Private>False</Private>
   </Reference>
   <Reference Include="DB.Extensibility.Contracts">
     <HintPath>C:\Program Files\DesignBuilder\DB.Extensibility.Contracts.dll</HintPath>
     <Private>False</Private>
   </Reference>
   ```

2. **Build the Project:**

   ```bash
   dotnet build
   ```

3. **Copy Plugin to DesignBuilder:**

   Copy the following file from `bin\Release\net48\` to your DesignBuilder plugins folder:
   - `OsmToGbXml.dll`

   The DesignBuilder plugins folder is typically located at:
   - `C:\Users\[YourUsername]\AppData\Roaming\DesignBuilder\Plugins\`

4. **Restart DesignBuilder**

5. **Use the Plugin:**
   - Open or create a model in DesignBuilder
   - Look for the "OSM2GBXML" menu
   - Select "Import from OpenStreetMap" to load OSM files directly
   - The plugin will convert the OSM file to gbXML and import it automatically

## Features

### OSM Conversion Features

- Extracts building geometries from OpenStreetMap data
- Converts geographic coordinates (lat/lon) to metric coordinates
- Generates 3D building models as outline blocks.
- Supports building height from OSM tags:
  - `height` tag (in meters)
  - `building:levels` tag (assumes 3m per level)
  - Default height of 3m if no height data available

### Plugin Features

- **Direct OSM Import:** Skip the intermediate conversion step
- **Automatic Conversion:** Converts OSM to gbXML in memory

## Supported OSM Tags

The converter recognizes buildings with the following tags:
- `building=*` (any building type)
- `building:part=*` (building parts)
- `height=*` (building height in meters)
- `building:levels=*` (number of floors)
- `name=*` (building name)


## Development

To modify or extend the converter:

1. **Core Conversion Logic:** Edit `OsmToGbXmlConverter.cs`
2. **Console UI:** Edit `Program.cs`
3. **Plugin UI:** Edit `DesignBuilderPlugin.cs`

The converter logic is separated from both interfaces, making it easy to maintain and extend.

## Troubleshooting

### Console Application Issues

- **"No buildings found":** Check that your OSM file contains ways with `building` tags
- **Missing file errors:** Verify the input file path is correct

### Plugin Issues

- **Plugin doesn't appear in DesignBuilder:**
  - Verify DLL is in correct plugins folder
  - Check that DesignBuilder API references are correctly configured
  - Ensure project builds without errors

- **Import errors:**
  - Check that the OSM file contains valid building data
  - Verify DesignBuilder model is loaded before attempting import

- **Missing menu items:**
  - Ensure a model is loaded in DesignBuilder (menu items are disabled when no model is loaded)

## License

This project integrates OpenStreetMap data, which is © OpenStreetMap contributors and available under the Open Database License (ODbL).

