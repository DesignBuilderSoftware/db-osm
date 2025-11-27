using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OSM
{
    /// <summary>
    /// Client for fetching OSM data from the Overpass API
    /// </summary>
    public class OverpassApiClient
    {
        private static readonly HttpClient httpClient;
        private const string OVERPASS_API_URL = "https://overpass-api.de/api/interpreter";

        static OverpassApiClient()
        {
            // Enable TLS 1.2 (required for HTTPS connections in .NET Framework 4.8)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            // Create HttpClient with handler and timeout
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(90); // Set timeout once during initialization
            httpClient.DefaultRequestHeaders.Add("User-Agent", "DesignBuilder-OSM-Plugin/1.0");
        }

        /// <summary>
        /// Fetches OSM data for the specified bounding box
        /// </summary>
        /// <param name="south">Southern latitude boundary</param>
        /// <param name="west">Western longitude boundary</param>
        /// <param name="north">Northern latitude boundary</param>
        /// <param name="east">Eastern longitude boundary</param>
        /// <returns>OSM XML data as string</returns>
        public async Task<string> FetchBuildingsInBoundingBox(double south, double west, double north, double east)
        {
            // Construct Overpass QL query to fetch all buildings in the bounding box
            var query = BuildOverpassQuery(south, west, north, east);

            try
            {
                // Create form content with the query (use data parameter as Overpass API expects)
                var formData = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "data", query }
                };
                var content = new FormUrlEncodedContent(formData);

                // Post query to Overpass API
                var response = await httpClient.PostAsync(OVERPASS_API_URL, content);

                // Check for specific HTTP status codes
                if ((int)response.StatusCode == 504 || (int)response.StatusCode == 503)
                {
                    throw new Exception("The Overpass API server is busy or timed out processing your request.\n\n" +
                        "This usually means the selected area is too large.\n\n" +
                        "Please try:\n" +
                        "1. Select a smaller area\n" +
                        "2. Wait a few minutes and try again\n" +
                        "3. Try a different location with fewer buildings");
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new Exception("Too many requests to Overpass API.\n\n" +
                        "Please wait a few minutes before trying again.");
                }

                response.EnsureSuccessStatusCode();

                // Return the OSM XML data
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                // Check if it's a timeout-related error
                if (ex.Message.Contains("504") || ex.Message.Contains("Gateway Timeout"))
                {
                    throw new Exception("Gateway Timeout: The selected area is too large or has too many buildings.\n\n" +
                        "Please select a smaller area (try reducing the size by 50-75%) and try again.", ex);
                }

                var innerMessage = ex.InnerException != null ? $" ({ex.InnerException.Message})" : "";
                throw new Exception($"Network error: {ex.Message}{innerMessage}\n\nPlease check your internet connection and try again.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new Exception("Request timed out after 90 seconds.\n\n" +
                    "The selected area is too large or has too many buildings.\n\n" +
                    "Please select a smaller area and try again.", ex);
            }
            catch (Exception ex)
            {
                // If the exception message already contains our custom message, don't wrap it
                if (ex.Message.Contains("Overpass API") || ex.Message.Contains("Gateway Timeout") || ex.Message.Contains("Too many requests"))
                {
                    throw;
                }

                throw new Exception($"Unexpected error: {ex.Message}\n\nPlease try again or select a different area.", ex);
            }
        }

        /// <summary>
        /// Builds an Overpass QL query to fetch all buildings and building parts in a bounding box
        /// </summary>
        private string BuildOverpassQuery(double south, double west, double north, double east)
        {
            // Format coordinates with invariant culture to ensure decimal points
            var bbox = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}", south, west, north, east);

            // Overpass QL query:
            // - [out:xml] specifies XML output format
            // - [timeout:90] sets query timeout to 90 seconds
            // - way["building"]({bbox}) fetches all ways tagged with "building"
            // - way["building:part"]({bbox}) fetches all ways tagged with "building:part"
            // - (._;>;) recursively includes all nodes referenced by the ways
            // - out meta; outputs all data with metadata
            var query = $@"[out:xml][timeout:90];
                        (
                        way[""building""]({bbox});
                        way[""building:part""]({bbox});
                        );
                        (._;>;);
                        out meta;";

            return query;
        }

        /// <summary>
        /// Validates that the bounding box is reasonable
        /// </summary>
        public static bool ValidateBoundingBox(double south, double west, double north, double east, out string errorMessage)
        {
            errorMessage = null;

            // Check that bounds are valid
            if (south >= north)
            {
                errorMessage = "Southern latitude must be less than northern latitude.";
                return false;
            }

            if (west >= east)
            {
                errorMessage = "Western longitude must be less than eastern longitude.";
                return false;
            }

            // Check latitude range (-90 to 90)
            if (south < -90 || south > 90 || north < -90 || north > 90)
            {
                errorMessage = "Latitude must be between -90 and 90 degrees.";
                return false;
            }

            // Check longitude range (-180 to 180)
            if (west < -180 || west > 180 || east < -180 || east > 180)
            {
                errorMessage = "Longitude must be between -180 and 180 degrees.";
                return false;
            }

            return true;
        }
    }
}
