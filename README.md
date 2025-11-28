# OpenStreetMap Plugin for DesignBuilder

Import real-world buildings from OpenStreetMap directly into DesignBuilder. 
Browse a map, click on a location, and automatically download building geometry into your model.

## What Does This Plugin Do?

This plugin lets you:
- **Browse a map** and click to download buildings from that area
- **Import building files** (.osm format) from OpenStreetMap
- **Automatically create 3D blocks** in DesignBuilder with the correct heights and shapes

## Installation

1. Open a command prompt in the project folder
2. Run: `dotnet build -c Release`
3. Run: `.\install-plugin.ps1`
4. Restart the DesignBuilder

After installation, you'll see a new "OSM" menu in DesignBuilder.

## How to Use

### Option 1: Browse Map

1. Click **OSM → Browse OpenStreetMap** to open a map
2. Navigate to your location
3. Click the square button and draw a rectangle around the area you want
4. Click **Load to DesignBuilder**
5. Done! The blocks will appear in your model

### Option 2: Import a File

1. Click **OSM → OpenStreetMap website** to open [OpenStreetMap.org](https://www.openstreetmap.org)
2. Navigate to your location
3. Click **Export** at the top
4. Select an area
5. Click the blue **Export** button to download the .osm file
6. In DesignBuilder, click **OSM → Load OpenStreetMap file**
7. Select your downloaded .osm file
8. Done! The blocks will appear in your model

## Common Problems

**The OSM menu doesn't appear in DesignBuilder**
- Make sure you restarted DesignBuilder after installation
- Check that the plugin file was copied correctly

**"No buildings found" message**
- The area you selected might not have building data in OpenStreetMap
- Try a different location with buildings visible on the map

**The map doesn't load**
- Check your internet connection
- Try importing a file instead (see Option 2 above)

**Buttons are greyed out in the map**
- You need to draw a rectangle on the map first, using the square button

## About OpenStreetMap Data

This plugin uses building data from OpenStreetMap, which is created by volunteers around the world. The data is © OpenStreetMap contributors. 
OpenStreetMap is open data, licensed under the Open Data Commons Open Database License (ODbL) by the OpenStreetMap Foundation (OSMF).

