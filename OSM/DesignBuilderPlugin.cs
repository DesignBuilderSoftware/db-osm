using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using DB.Extensibility.Contracts;
using DB.Api;

namespace OSM
{
    [Export(typeof(IPlugin2))]
    public class OSM2GBXMLPlugin : PluginBase2, IPlugin2
    {
        class MenuKeys
        {
            public const string Root = "OSM";
            public const string LoadOSMFile = "LoadOSMFile";
            public const string BrowseOpenStreetMap = "BrowseOpenStreetMap";
            public const string OpenStreetMapWebsite = "OpenStreetMapWebsite";
            public const string Help = "Help";
        }

        class MenuItem
        {
            public Action Action { get; set; }
            public bool IsEnabled { get; set; }
            public bool IsVisible { get; set; }

            public MenuItem(
                Action action = null,
                bool enabled = true,
                bool visible = true)
            {
                Action = action ?? delegate { };
                IsEnabled = enabled;
                IsVisible = visible;
            }
        }

        private readonly Dictionary<string, MenuItem> mMenuItems = new Dictionary<string, MenuItem>();

        public override bool HasMenu
        {
            get { return true; }
        }

        public override string MenuLayout
        {
            get
            {
                StringBuilder menu = new StringBuilder();
                menu.AppendFormat("*OSM,{0}", MenuKeys.Root);
                menu.AppendFormat("*>Browse OpenStreetMap,{0}", MenuKeys.BrowseOpenStreetMap);
                menu.AppendFormat("*>Load OpenStreetMap file,{0}", MenuKeys.LoadOSMFile);
                menu.AppendFormat("*>OpenStreetMap website,{0}", MenuKeys.OpenStreetMapWebsite);
                menu.AppendFormat("*>Help,{0}", MenuKeys.Help);
                return menu.ToString();
            }
        }

        public override bool IsMenuItemVisible(string key)
        {
            if (!mMenuItems.ContainsKey(key))
                return true;
            return mMenuItems[key].IsVisible;
        }

        public override bool IsMenuItemEnabled(string key)
        {
            if (!mMenuItems.ContainsKey(key))
                return true;
            return mMenuItems[key].IsEnabled;
        }

        public override void OnMenuItemPressed(string key)
        {
            if (mMenuItems.ContainsKey(key))
            {
                mMenuItems[key].Action();
            }
        }

        private bool IfAtSiteLevel()
        {
            if (ApiEnvironment.CurrentBuildingIndex == -1)
            {
                MessageBox.Show("Please navigate to a building level first.", "OSM Import",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return true;
            }
            return false;
        }

        public override void Create()
        {
            mMenuItems.Add(MenuKeys.Root, new MenuItem());
            mMenuItems.Add(MenuKeys.BrowseOpenStreetMap, new MenuItem(SelectAreaFromMap, false));
            mMenuItems.Add(MenuKeys.LoadOSMFile, new MenuItem(LoadOSMFile, false));
            mMenuItems.Add(MenuKeys.OpenStreetMapWebsite, new MenuItem(OpenOpenStreetMapWebsite, true));
            mMenuItems.Add(MenuKeys.Help, new MenuItem(ShowHelp, true));
        }

        /// <summary>
        /// Converts an OSM file to gbXML and imports it into DesignBuilder.
        /// Returns the number of blocks imported, or 0 if none found.
        /// </summary>
        private int ImportOsmToDesignBuilder(string osmFile, List<PolygonFilterVertex> polygonFilter = null)
        {
            var converter = new OsmToGbXmlConverter(osmFile);

            if (polygonFilter != null)
                converter.SetPolygonFilter(polygonFilter);

            int numBlocks = converter.ParseOsm();
            if (numBlocks == 0) return 0;

            var result = converter.CreateGbXml();
            string tempGbXmlFile = Path.Combine(Path.GetTempPath(), "osm_converted.xml");

            File.WriteAllText(tempGbXmlFile, result.Item1);

            Site site = ApiEnvironment.Site;
            site.SetAttribute("ImportGBXMLBlockMode", "0");

            GbXmlFile gbXmlFile = ApiEnvironment.GbXmlFileOperations.Current;
            gbXmlFile.LoadFile(tempGbXmlFile);
            gbXmlFile.ImportModel();

            return numBlocks;
        }

        public void LoadOSMFile()
        {
            if (IfAtSiteLevel())
                return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "OpenStreetMap files (*.osm)|*.osm|All files (*.*)|*.*";
            openFileDialog.Title = "Load OpenStreetMap file";
            openFileDialog.DefaultExt = "osm";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    int numBlocks = ImportOsmToDesignBuilder(openFileDialog.FileName);

                    if (numBlocks == 0)
                    {
                        MessageBox.Show("No geometry found in the OSM file.", "OSM Import",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    MessageBox.Show(
                        $"Successfully imported {numBlocks} blocks from OpenStreetMap.",
                        "OSM Import Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing OSM file: {ex.Message}", "OSM Import Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void SelectAreaFromMap()
        {
            if (IfAtSiteLevel())
                return;

            try
            {
                // Create and show the embedded map browser
                var mapForm = new MapBrowserForm();

                // Handle bounding box selection
                mapForm.BoundingBoxSelected += async (sender, args) =>
                {
                    Form progressForm = null;
                    try
                    {
                        // Close the map form
                        mapForm.Close();

                        // Validate bounding box
                        string errorMessage;
                        if (!OverpassApiClient.ValidateBoundingBox(
                            args.BoundingBox.South,
                            args.BoundingBox.West,
                            args.BoundingBox.North,
                            args.BoundingBox.East,
                            out errorMessage))
                        {
                            MessageBox.Show(errorMessage, "Invalid Selection",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Show warning if area is large
                        if (errorMessage != null)
                        {
                            var result = MessageBox.Show(
                                errorMessage + "\n\nDo you want to continue?",
                                "Large Area Warning",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);

                            if (result != DialogResult.Yes)
                            {
                                return;
                            }
                        }

                        // Show progress dialog
                        progressForm = new Form
                        {
                            Text = "Downloading OSM Data",
                            Width = 400,
                            Height = 150,
                            FormBorderStyle = FormBorderStyle.FixedDialog,
                            StartPosition = FormStartPosition.CenterScreen,
                            ControlBox = false
                        };

                        var progressLabel = new Label
                        {
                            Text = "Downloading building data from OpenStreetMap...\nThis may take a few moments.",
                            AutoSize = false,
                            Width = 360,
                            Height = 60,
                            Left = 20,
                            Top = 20,
                            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                        };

                        progressForm.Controls.Add(progressLabel);
                        progressForm.Show();

                        // Fetch OSM data from Overpass API
                        var apiClient = new OverpassApiClient();
                        string osmXml = await apiClient.FetchBuildingsInBoundingBox(
                            args.BoundingBox.South,
                            args.BoundingBox.West,
                            args.BoundingBox.North,
                            args.BoundingBox.East);

                        progressLabel.Text = "Converting to gbXML format...";
                        Application.DoEvents();

                        // Save to temporary file
                        string tempOsmFile = Path.Combine(Path.GetTempPath(), "osm_downloaded.osm");
                        File.WriteAllText(tempOsmFile, osmXml);

                        // Build polygon filter if the user drew a polygon
                        List<PolygonFilterVertex> polygonFilter = null;
                        if (args.BoundingBox.Polygon != null && args.BoundingBox.Polygon.Length >= 3)
                        {
                            polygonFilter = args.BoundingBox.Polygon
                                .Select(v => new PolygonFilterVertex { Lat = v.Lat, Lon = v.Lng }).ToList();
                        }

                        progressLabel.Text = "Converting and importing...";
                        Application.DoEvents();

                        int numBlocks = ImportOsmToDesignBuilder(tempOsmFile, polygonFilter);

                        progressForm.Close();

                        if (numBlocks == 0)
                        {
                            MessageBox.Show("No geometry found in the selected area.", "OSM Import",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Successfully imported {numBlocks} blocks from OpenStreetMap.",
                                "OSM Import Complete",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }

                        try { File.Delete(tempOsmFile); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                    catch (Exception ex)
                    {
                        if (progressForm != null && progressForm.Visible)
                            progressForm.Close();

                        MessageBox.Show($"Error importing OSM data: {ex.Message}", "OSM Import Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                // Show the map form
                mapForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening map browser: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OpenOpenStreetMapWebsite()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.openstreetmap.org",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening OpenStreetMap website: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ShowHelp()
        {
            try
            {
                // Try to read help text from file
                string helpFilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Resources", "help_readme.md");
                string helpMessage = File.ReadAllText(helpFilePath);

                MessageBox.Show(helpMessage, "OSM Plugin - How to Use",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading help: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public override void ModelLoaded()
        {
            mMenuItems[MenuKeys.BrowseOpenStreetMap].IsEnabled = true;
            mMenuItems[MenuKeys.LoadOSMFile].IsEnabled = true;
        }

        public override void ModelUnloaded()
        {
            mMenuItems[MenuKeys.BrowseOpenStreetMap].IsEnabled = false;
            mMenuItems[MenuKeys.LoadOSMFile].IsEnabled = false;
        }
    }
}
