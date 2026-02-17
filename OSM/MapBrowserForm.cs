using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;

namespace OSM
{
    /// <summary>
    /// Form that hosts an embedded WebView2 browser for selecting areas from OpenStreetMap
    /// </summary>
    public class MapBrowserForm : Form
    {
        private WebView2 webView;

        /// <summary>
        /// Event fired when user selects a bounding box from the map
        /// </summary>
        public event EventHandler<BoundingBoxSelectedEventArgs> BoundingBoxSelected;

        public MapBrowserForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "OpenStreetMap Browser";
            this.Width = 1200;
            this.Height = 800;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new System.Drawing.Size(800, 600);

            // Load icon from plugin directory
            try
            {
                var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var iconPath = Path.Combine(pluginDir, "Resources", "Openstreetmap_logo.png");
                if (File.Exists(iconPath))
                {
                    using (var bitmap = new System.Drawing.Bitmap(iconPath))
                    {
                        var iconHandle = bitmap.GetHicon();
                        this.Icon = System.Drawing.Icon.FromHandle(iconHandle);
                    }
                }
            }
            catch
            {
                // Ignore icon loading errors
            }

            // Create WebView2 control
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;

            // Add to form
            this.Controls.Add(webView);

            // Handle form load
            this.Load += MapBrowserForm_Load;
        }

        private async void MapBrowserForm_Load(object sender, EventArgs e)
        {
            await InitializeWebView();
        }

        private async Task InitializeWebView()
        {
            try
            {
                // Initialize WebView2 environment
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath(), null);
                await webView.EnsureCoreWebView2Async(environment);

                // Enable web security features that allow API calls
                var settings = webView.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = true;
                settings.IsWebMessageEnabled = true;
                settings.IsScriptEnabled = true;

                // Set user agent to identify as a modern browser
                webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

                // Enable DevTools for debugging (can be opened with F12)
                settings.AreDevToolsEnabled = true;

                // Set up message handler for JavaScript communication
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Add console message handler for debugging
                webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;

                // Load the HTML map interface
                LoadMapInterface();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize embedded browser:\n{ex.Message}\n\n" +
                    "Please ensure Microsoft Edge WebView2 Runtime is installed.\n" +
                    "You can download it from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                    "Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                this.Close();
            }
        }

        private void LoadMapInterface()
        {
            try
            {
                // Load HTML from plugin directory
                var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var htmlSource = Path.Combine(pluginDir, "Resources", "MapInterface.html");

                if (!File.Exists(htmlSource))
                {
                    throw new Exception($"Could not find HTML file: {htmlSource}");
                }

                // Copy to temp directory
                var tempPath = Path.Combine(Path.GetTempPath(), "OSM_MapInterface");
                Directory.CreateDirectory(tempPath);

                // Copy HTML
                var htmlDest = Path.Combine(tempPath, "map.html");
                File.Copy(htmlSource, htmlDest, true);

                // Set up a virtual host mapping to avoid CORS issues
                // This gives the page a proper origin (https://osm.local) instead of "null"
                const string virtualHost = "osm.local";
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    virtualHost,
                    tempPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                // Navigate using virtual host
                webView.CoreWebView2.Navigate($"https://{virtualHost}/map.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load map interface:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                this.Close();
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Parse the message from JavaScript
                string message = e.TryGetWebMessageAsString();

                // Deserialize the bounding box data
                var bbox = JsonConvert.DeserializeObject<BoundingBoxData>(message);

                if (bbox != null)
                {
                    // Fire the event with the selected bounding box
                    BoundingBoxSelected?.Invoke(this, new BoundingBoxSelectedEventArgs(bbox));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error processing selected area:\n{ex.Message}",
                    "Processing Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CoreWebView2_WebResourceResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            // Log web resource responses for debugging
            System.Diagnostics.Debug.WriteLine($"Resource: {e.Request.Uri} - Status: {e.Response.StatusCode}");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                webView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Data class for bounding box information from the map
    /// </summary>
    public class BoundingBoxData
    {
        [JsonProperty("south")]
        public double South { get; set; }

        [JsonProperty("west")]
        public double West { get; set; }

        [JsonProperty("north")]
        public double North { get; set; }

        [JsonProperty("east")]
        public double East { get; set; }

        [JsonProperty("area")]
        public double Area { get; set; }

        /// <summary>
        /// Polygon vertices for polygon selections. Null for rectangle selections.
        /// </summary>
        [JsonProperty("polygon")]
        public PolygonVertex[] Polygon { get; set; }
    }

    /// <summary>
    /// A single vertex in a polygon selection
    /// </summary>
    public class PolygonVertex
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lng")]
        public double Lng { get; set; }
    }

    /// <summary>
    /// Event args for bounding box selection
    /// </summary>
    public class BoundingBoxSelectedEventArgs : EventArgs
    {
        public BoundingBoxData BoundingBox { get; }

        public BoundingBoxSelectedEventArgs(BoundingBoxData boundingBox)
        {
            BoundingBox = boundingBox;
        }
    }
}
