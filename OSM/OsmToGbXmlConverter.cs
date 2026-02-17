using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OSM
{
    public class OsmToGbXmlConverter
    {
        private string osmFile;
        private List<Building> buildings = new List<Building>();
        private Dictionary<string, Node> nodes = new Dictionary<string, Node>();
        private List<PolygonFilterVertex> polygonFilter;

        public OsmToGbXmlConverter(string osmFile)
        {
            this.osmFile = osmFile;
        }

        /// <summary>
        /// Sets a polygon filter. Only buildings whose center falls inside this polygon will be included.
        /// Each vertex has Lat and Lon properties.
        /// </summary>
        public void SetPolygonFilter(List<PolygonFilterVertex> polygon)
        {
            this.polygonFilter = polygon;
        }

        public int ParseOsm()
        {
            var doc = XDocument.Load(osmFile);

            foreach (var node in doc.Descendants("node"))
            {
                var id = node.Attribute("id")?.Value;
                var lat = double.Parse(node.Attribute("lat")?.Value ?? "0");
                var lon = double.Parse(node.Attribute("lon")?.Value ?? "0");
                nodes[id] = new Node { Lat = lat, Lon = lon };
            }

            foreach (var way in doc.Descendants("way"))
            {
                var tags = way.Descendants("tag")
                    .ToDictionary(t => t.Attribute("k")?.Value, t => t.Attribute("v")?.Value);

                if (tags.ContainsKey("building") || tags.ContainsKey("building:part"))
                {
                    var nodeRefs = way.Descendants("nd")
                        .Select(nd => nd.Attribute("ref")?.Value)
                        .Where(r => nodes.ContainsKey(r))
                        .Select(r => nodes[r])
                        .ToList();

                    if (nodeRefs.Count >= 3)
                    {
                        double centerLat = nodeRefs.Average(n => n.Lat);
                        double centerLon = nodeRefs.Average(n => n.Lon);

                        // If polygon filter is set, only include buildings whose center is inside
                        if (polygonFilter != null && !IsPointInPolygon(centerLat, centerLon, polygonFilter))
                            continue;

                        buildings.Add(new Building
                        {
                            Id = way.Attribute("id")?.Value,
                            Tags = tags,
                            Geometry = nodeRefs,
                            Center = new BuildingCenter { Lat = centerLat, Lon = centerLon }
                        });
                    }
                }
            }
            return buildings.Count;
        }

        /// <summary>
        /// Ray-casting point-in-polygon test.
        /// Returns true if the point (lat, lon) is inside the polygon.
        /// </summary>
        private static bool IsPointInPolygon(double lat, double lon, List<PolygonFilterVertex> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double yi = polygon[i].Lat, xi = polygon[i].Lon;
                double yj = polygon[j].Lat, xj = polygon[j].Lon;

                if (((yi > lat) != (yj > lat)) &&
                    (lon < (xj - xi) * (lat - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private double GetHeight(Dictionary<string, string> tags)
        {
            if (tags.ContainsKey("height") && double.TryParse(tags["height"].Replace("m", "").Trim(), out double h))
                return h;
            if (tags.ContainsKey("building:levels") && int.TryParse(tags["building:levels"], out int l))
                return l * 3.0;
            return 0.1;
        }

        private Coordinate LatLonToMeters(double lat, double lon, double oLat, double oLon)
        {
            int earthRadius = 6371000; // in meters
            double x = earthRadius * (lon - oLon) * Math.PI / 180 * Math.Cos(oLat * Math.PI / 180);
            double y = earthRadius * (lat - oLat) * Math.PI / 180;
            return new Coordinate { X = x, Y = y };
        }

        public Tuple<string, int> CreateGbXml()
        {
            if (buildings.Count == 0) return Tuple.Create<string, int>(null, 0);

            var oLat = buildings.Average(b => b.Center.Lat);
            var oLon = buildings.Average(b => b.Center.Lon);

            XNamespace ns = "http://www.gbxml.org/schema";
            var gbxml = new XElement(ns + "gbXML",
                new XAttribute("version", "6.01"),
                new XAttribute("lengthUnit", "Meters"));

            var campus = new XElement("Campus", new XAttribute("id", "campus"),
                new XElement("Location",
                    new XElement("Longitude", oLon.ToString("F6")),
                    new XElement("Latitude", oLat.ToString("F6"))));

            var building = new XElement("Building",
                new XAttribute("id", "building"),
                new XAttribute("buildingType", "Mixed"),
                new XElement("Name", "Combined Building"));

            int blockCount = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                var coords = b.Geometry.Select(p => LatLonToMeters(p.Lat, p.Lon, oLat, oLon)).ToList();
                var h = GetHeight(b.Tags);
                // Remove duplicate last point if it equals the first point
                var norm = (coords.Count > 0 && coords[0].X == coords[coords.Count - 1].X && coords[0].Y == coords[coords.Count - 1].Y)
                    ? coords.GetRange(0, coords.Count - 1)
                    : coords;

                string blockName = b.Tags.ContainsKey("name") ? b.Tags["name"] : $"Space {i + 1}";
                var block = new XElement("Space",
                    new XAttribute("id", $"space-{b.Id}"),
                    new XAttribute("buildingStoreyIdRef", "storey-1"),
                    new XElement("Name", blockName));

                var shell = new XElement("ClosedShell");

                var floor = new XElement("PolyLoop");
                foreach (var coord in norm) floor.Add(CP(coord.X, coord.Y, 0));
                shell.Add(floor);

                var ceiling = new XElement("PolyLoop");
                for (int j = norm.Count - 1; j >= 0; j--)
                {
                    ceiling.Add(CP(norm[j].X, norm[j].Y, h));
                }
                shell.Add(ceiling);

                for (int j = 0; j < norm.Count; j++)
                {
                    var coord1 = norm[j];
                    var coord2 = norm[(j + 1) % norm.Count];
                    shell.Add(new XElement("PolyLoop",
                        CP(coord1.X, coord1.Y, 0),
                        CP(coord2.X, coord2.Y, 0),
                        CP(coord2.X, coord2.Y, h),
                        CP(coord1.X, coord1.Y, h)));
                }

                block.Add(new XElement("ShellGeometry", new XAttribute("id", $"shell-{b.Id}"), shell));
                building.Add(block);
                blockCount++;
            }

            building.Add(new XElement("BuildingStorey",
                new XAttribute("id", "storey-1"),
                new XElement("Name", "Ground Floor"),
                new XElement("Level", "0.0")));

            campus.Add(building);
            gbxml.Add(campus);
            return Tuple.Create(gbxml.ToString(), blockCount);
        }

        private XElement CP(double x, double y, double z)
        {
            return new XElement("CartesianPoint",
                new XElement("Coordinate", Math.Round(x, 3).ToString("F3")),
                new XElement("Coordinate", Math.Round(y, 3).ToString("F3")),
                new XElement("Coordinate", Math.Round(z, 3).ToString("F3")));
        }
    }

    public class Node
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class Building
    {
        public string Id { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public List<Node> Geometry { get; set; }
        public BuildingCenter Center { get; set; }
    }

    public class BuildingCenter
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class Coordinate
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class PolygonFilterVertex
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
