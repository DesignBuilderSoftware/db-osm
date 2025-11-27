using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
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
            public const string LoadOSM = "LoadOSM";
            public const string SelectFromMap = "SelectFromMap";
            public const string OpenStreetMapWebsite = "OpenStreetMapWebsite";
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
                menu.AppendFormat("*>Select area from OpenStreetMap,{0}", MenuKeys.SelectFromMap);
                menu.AppendFormat("*>Load OpenStreetMap file,{0}", MenuKeys.LoadOSM);
                menu.AppendFormat("*>OpenStreetMap website,{0}", MenuKeys.OpenStreetMapWebsite);
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

        public override void Create()
        {
            mMenuItems.Add(MenuKeys.Root, new MenuItem());
            mMenuItems.Add(MenuKeys.SelectFromMap, new MenuItem(SelectAreaFromMap, false));
            mMenuItems.Add(MenuKeys.LoadOSM, new MenuItem(LoadOSM, false));
            mMenuItems.Add(MenuKeys.OpenStreetMapWebsite, new MenuItem(OpenOpenStreetMapWebsite, true));
        }

        public void LoadOSM()
        {
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
                    string osmFile = openFileDialog.FileName;
                    string tempGbXmlFile = Path.Combine(Path.GetTempPath(), "osm_converted.xml");

                    var converter = new OsmToGbXmlConverter(osmFile);
                    int numBuildings = converter.ParseOsm();

                    if (numBuildings == 0)
                    {
                        MessageBox.Show("No buildings found in the OSM file.", "OSM Import",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var result = converter.CreateGbXml();
                    string xmlContent = result.Item1;
                    int spaces = result.Item2;

                    File.WriteAllText(tempGbXmlFile, xmlContent);

                    Site site = ApiEnvironment.Site;
                    site.SetAttribute("ImportGBXMLBlockMode", "0");

                    GbXmlFile gbXmlFile = ApiEnvironment.GbXmlFileOperations.Current;
                    gbXmlFile.LoadFile(tempGbXmlFile);
                    gbXmlFile.ImportModel();

                    MessageBox.Show(
                        $"Successfully imported {numBuildings} blocks from OpenStreetMap.",
                        "OSM Import Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    try
                    {
                        File.Delete(tempGbXmlFile);
                    }
                    catch { }
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
            try
            {
                // Create and show the embedded map browser
                var mapForm = new MapBrowserForm();

                // Handle bounding box selection
                mapForm.BoundingBoxSelected += async (sender, args) =>
                {
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
                        var progressForm = new Form
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

                        // Convert using existing converter
                        var converter = new OsmToGbXmlConverter(tempOsmFile);
                        int numBuildings = converter.ParseOsm();

                        if (numBuildings == 0)
                        {
                            progressForm.Close();
                            MessageBox.Show("No buildings found in the selected area.", "OSM Import",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);

                            try { File.Delete(tempOsmFile); } catch { }
                            return;
                        }

                        var conversionResult = converter.CreateGbXml();
                        string xmlContent = conversionResult.Item1;
                        int spaces = conversionResult.Item2;

                        progressLabel.Text = "Importing into DesignBuilder...";
                        Application.DoEvents();

                        // Import into DesignBuilder
                        string tempGbXmlFile = Path.Combine(Path.GetTempPath(), "osm_converted.xml");
                        File.WriteAllText(tempGbXmlFile, xmlContent);

                        Site site = ApiEnvironment.Site;
                        site.SetAttribute("ImportGBXMLBlockMode", "0");

                        GbXmlFile gbXmlFile = ApiEnvironment.GbXmlFileOperations.Current;
                        gbXmlFile.LoadFile(tempGbXmlFile);
                        gbXmlFile.ImportModel();

                        // Close progress and show success
                        progressForm.Close();

                        MessageBox.Show(
                            $"Successfully imported {numBuildings} buildings from OpenStreetMap.\n\n" +
                            $"Area: {args.BoundingBox.Area:F2} km²",
                            "OSM Import Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        // Cleanup
                        try
                        {
                            File.Delete(tempOsmFile);
                            File.Delete(tempGbXmlFile);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
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

        public override void ModelLoaded()
        {
            mMenuItems[MenuKeys.SelectFromMap].IsEnabled = true;
            mMenuItems[MenuKeys.LoadOSM].IsEnabled = true;
        }

        public override void ModelUnloaded()
        {
            mMenuItems[MenuKeys.SelectFromMap].IsEnabled = false;
            mMenuItems[MenuKeys.LoadOSM].IsEnabled = false;
        }
    }
}
