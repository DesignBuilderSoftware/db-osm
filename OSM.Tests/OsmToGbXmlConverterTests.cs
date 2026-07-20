using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OSM;
using Xunit;

namespace OSM.Tests
{
    public class OsmToGbXmlConverterTests : IDisposable
    {
        private static readonly XNamespace Gb = "http://www.gbxml.org/schema";
        private readonly List<string> tempFiles = new List<string>();

        public void Dispose()
        {
            foreach (var f in tempFiles)
            {
                try { File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        // ---------- helpers ----------

        private string WriteOsmFile(string body)
        {
            var path = Path.Combine(Path.GetTempPath(), "osm_test_" + Guid.NewGuid().ToString("N") + ".osm");
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<osm version=\"0.6\">\n" + body + "\n</osm>");
            tempFiles.Add(path);
            return path;
        }

        private static string NodeXml(int id, double lat, double lon)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "<node id=\"{0}\" lat=\"{1}\" lon=\"{2}\"/>\n", id, lat, lon);
        }

        private static string WayXml(int id, int[] nodeRefs, params string[][] tags)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<way id=\"{0}\">\n", id);
            foreach (var r in nodeRefs)
                sb.AppendFormat("  <nd ref=\"{0}\"/>\n", r);
            foreach (var tag in tags)
                sb.AppendFormat("  <tag k=\"{0}\" v=\"{1}\"/>\n", tag[0], tag[1]);
            sb.Append("</way>\n");
            return sb.ToString();
        }

        private static string[] Tag(string k, string v)
        {
            return new[] { k, v };
        }

        /// <summary>
        /// Four nodes forming a square: (lat,lon), (lat,lon+d), (lat+d,lon+d), (lat+d,lon),
        /// with ids firstId..firstId+3.
        /// </summary>
        private static string SquareNodesXml(int firstId, double lat, double lon, double d)
        {
            return NodeXml(firstId, lat, lon)
                 + NodeXml(firstId + 1, lat, lon + d)
                 + NodeXml(firstId + 2, lat + d, lon + d)
                 + NodeXml(firstId + 3, lat + d, lon);
        }

        private static int[] Refs(params int[] ids)
        {
            return ids;
        }

        private static XDocument ConvertSingle(string osmBody, out int parsed, out Tuple<string, int> result)
        {
            var converter = new OsmToGbXmlConverter(WriteOsmFileStatic(osmBody));
            parsed = converter.ParseOsm();
            result = converter.CreateGbXml();
            return result.Item1 == null ? null : XDocument.Parse(result.Item1);
        }

        // static variant used by ConvertSingle; files land in %TEMP% and are cleaned up by the OS
        private static string WriteOsmFileStatic(string body)
        {
            var path = Path.Combine(Path.GetTempPath(), "osm_test_" + Guid.NewGuid().ToString("N") + ".osm");
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<osm version=\"0.6\">\n" + body + "\n</osm>");
            return path;
        }

        /// <summary>Returns the maximum Z value across all CartesianPoints (i.e. the extruded height).</summary>
        private static double MaxZ(XDocument doc)
        {
            return doc.Descendants(Gb + "CartesianPoint")
                .Select(cp => double.Parse(cp.Elements(Gb + "Coordinate").ElementAt(2).Value, CultureInfo.InvariantCulture))
                .Max();
        }

        // ---------- ParseOsm ----------

        [Fact]
        public void ParseOsm_CountsBuildingWays_AndIgnoresNonBuildingWays()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + SquareNodesXml(5, 51.0, 0.01, 0.001)
                     + SquareNodesXml(9, 51.0, 0.02, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"))
                     + WayXml(101, Refs(5, 6, 7, 8), Tag("building", "residential"))
                     + WayXml(102, Refs(9, 10, 11, 12), Tag("highway", "residential"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));

            Assert.Equal(2, converter.ParseOsm());
        }

        [Fact]
        public void ParseOsm_IncludesBuildingPartWays()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building:part", "yes"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));

            Assert.Equal(1, converter.ParseOsm());
        }

        [Fact]
        public void ParseOsm_IgnoresWaysWithFewerThanThreeNodes()
        {
            var body = NodeXml(1, 51.0, 0.0)
                     + NodeXml(2, 51.0, 0.001)
                     + WayXml(100, Refs(1, 2), Tag("building", "yes"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));

            Assert.Equal(0, converter.ParseOsm());
        }

        [Fact]
        public void ParseOsm_IgnoresNodeRefsThatCannotBeResolved()
        {
            // Way references 4 nodes but only 2 are present in the file,
            // so fewer than 3 vertices resolve and the way must be dropped.
            var body = NodeXml(1, 51.0, 0.0)
                     + NodeXml(2, 51.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));

            Assert.Equal(0, converter.ParseOsm());
        }

        [Fact]
        public void ParseOsm_EmptyFile_ReturnsZero()
        {
            var converter = new OsmToGbXmlConverter(WriteOsmFile(""));

            Assert.Equal(0, converter.ParseOsm());
        }

        // ---------- polygon filter ----------

        [Fact]
        public void PolygonFilter_KeepsOnlyBuildingsWhoseCentreIsInsidePolygon()
        {
            // Building A centred near (51.0005, 0.0005); building B centred near (51.0005, 0.0105).
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + SquareNodesXml(5, 51.0, 0.01, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("name", "Inside"))
                     + WayXml(101, Refs(5, 6, 7, 8), Tag("building", "yes"), Tag("name", "Outside"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));
            // Polygon tightly surrounding building A only.
            converter.SetPolygonFilter(new List<PolygonFilterVertex>
            {
                new PolygonFilterVertex { Lat = 50.999, Lon = -0.001 },
                new PolygonFilterVertex { Lat = 50.999, Lon = 0.002 },
                new PolygonFilterVertex { Lat = 51.002, Lon = 0.002 },
                new PolygonFilterVertex { Lat = 51.002, Lon = -0.001 }
            });

            Assert.Equal(1, converter.ParseOsm());

            var result = converter.CreateGbXml();
            var doc = XDocument.Parse(result.Item1);
            var names = doc.Descendants(Gb + "Space").Select(s => s.Element(Gb + "Name").Value).ToList();
            Assert.Equal(new[] { "Inside" }, names);
        }

        [Fact]
        public void PolygonFilter_ExcludingAllBuildings_YieldsZero()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));
            // Polygon far away from the building.
            converter.SetPolygonFilter(new List<PolygonFilterVertex>
            {
                new PolygonFilterVertex { Lat = 10.0, Lon = 10.0 },
                new PolygonFilterVertex { Lat = 10.0, Lon = 11.0 },
                new PolygonFilterVertex { Lat = 11.0, Lon = 11.0 },
                new PolygonFilterVertex { Lat = 11.0, Lon = 10.0 }
            });

            Assert.Equal(0, converter.ParseOsm());
        }

        // ---------- CreateGbXml ----------

        [Fact]
        public void CreateGbXml_NoBuildings_ReturnsNullContentAndZeroBlocks()
        {
            var converter = new OsmToGbXmlConverter(WriteOsmFile(""));
            converter.ParseOsm();

            var result = converter.CreateGbXml();

            Assert.Null(result.Item1);
            Assert.Equal(0, result.Item2);
        }

        [Fact]
        public void CreateGbXml_ProducesWellFormedGbXmlWithExpectedRootAndUnits()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            Assert.Equal(1, result.Item2);
            Assert.Equal(Gb + "gbXML", doc.Root.Name);
            Assert.Equal("Meters", doc.Root.Attribute("lengthUnit").Value);
            Assert.Equal("true", doc.Root.Attribute("useSIUnitsForResults").Value);
            Assert.Single(doc.Descendants(Gb + "Campus"));
            Assert.Single(doc.Descendants(Gb + "Building"));
            Assert.Single(doc.Descendants(Gb + "BuildingStorey"));
            Assert.Single(doc.Descendants(Gb + "Space"));
        }

        [Fact]
        public void CreateGbXml_CampusLocationIsAverageOfBuildingCentres()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            var location = doc.Descendants(Gb + "Location").Single();
            Assert.Equal("51.000500", location.Element(Gb + "Latitude").Value);
            Assert.Equal("0.000500", location.Element(Gb + "Longitude").Value);
        }

        [Fact]
        public void CreateGbXml_HeightTakenFromHeightTag_IgnoringUnitsSuffix()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("height", "12.5 m"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            Assert.Equal(12.5, MaxZ(doc), 6);
        }

        [Fact]
        public void CreateGbXml_HeightFromBuildingLevels_ThreeMetresPerLevel()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("building:levels", "3"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            Assert.Equal(9.0, MaxZ(doc), 6);
        }

        [Fact]
        public void CreateGbXml_InvalidHeightTag_FallsBackToLevels()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4),
                         Tag("building", "yes"), Tag("height", "tall"), Tag("building:levels", "2"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            Assert.Equal(6.0, MaxZ(doc), 6);
        }

        [Fact]
        public void CreateGbXml_NoHeightInformation_UsesMinimalDefaultHeight()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            Assert.Equal(0.1, MaxZ(doc), 6);
        }

        [Fact]
        public void CreateGbXml_ZeroHeightTag_FallsBackToDefault()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("height", "0"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            Assert.Equal(0.1, MaxZ(doc), 6);
        }

        [Fact]
        public void CreateGbXml_ClosedRing_DuplicateClosingVertexIsRemoved()
        {
            // OSM closed ways repeat the first node as the last node.
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4, 1), Tag("building", "yes"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            var shell = doc.Descendants(Gb + "ClosedShell").Single();
            var loops = shell.Elements(Gb + "PolyLoop").ToList();

            // floor + ceiling + 4 walls
            Assert.Equal(6, loops.Count);
            // floor loop must contain 4 unique vertices, not 5
            Assert.Equal(4, loops[0].Elements(Gb + "CartesianPoint").Count());
            // each wall is a quad
            foreach (var wall in loops.Skip(2))
                Assert.Equal(4, wall.Elements(Gb + "CartesianPoint").Count());
        }

        [Fact]
        public void CreateGbXml_SpaceNameFromNameTag_WithFallbackToGeneratedName()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + SquareNodesXml(5, 51.0, 0.01, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("name", "Town Hall"))
                     + WayXml(101, Refs(5, 6, 7, 8), Tag("building", "yes"));

            var converter = new OsmToGbXmlConverter(WriteOsmFile(body));
            converter.ParseOsm();
            var result = converter.CreateGbXml();
            var doc = XDocument.Parse(result.Item1);

            var names = doc.Descendants(Gb + "Space").Select(s => s.Element(Gb + "Name").Value).ToList();
            Assert.Equal(new[] { "Town Hall", "Space 2" }, names);
            Assert.Equal(2, result.Item2);
        }

        [Fact]
        public void CreateGbXml_SpaceIdsIncludeOsmWayIds()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(4242, Refs(1, 2, 3, 4), Tag("building", "yes"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            var space = doc.Descendants(Gb + "Space").Single();
            Assert.Equal("space-4242", space.Attribute("id").Value);
            var shellGeometry = doc.Descendants(Gb + "ShellGeometry").Single();
            Assert.Equal("shell-4242", shellGeometry.Attribute("id").Value);
        }

        [Fact]
        public void CreateGbXml_ProjectsLatLonSpansToExpectedMetres()
        {
            // A square 0.001 degrees on each side at ~51 degrees north.
            // Expected northing span: 6371000 * 0.001 * pi/180  = 111.195 m
            // Expected easting span:  111.195 * cos(51.0005 deg) = 69.976 m
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            var floor = doc.Descendants(Gb + "ClosedShell").Single().Elements(Gb + "PolyLoop").First();
            var points = floor.Elements(Gb + "CartesianPoint")
                .Select(cp => cp.Elements(Gb + "Coordinate")
                    .Select(c => double.Parse(c.Value, CultureInfo.InvariantCulture)).ToArray())
                .ToList();

            double xSpan = points.Max(p => p[0]) - points.Min(p => p[0]);
            double ySpan = points.Max(p => p[1]) - points.Min(p => p[1]);

            Assert.InRange(ySpan, 111.14, 111.25);
            Assert.InRange(xSpan, 69.93, 70.03);

            // Geometry is centred on the origin (building centre = campus origin)
            Assert.InRange(points.Average(p => p[0]), -0.001, 0.001);
            Assert.InRange(points.Average(p => p[1]), -0.001, 0.001);
        }

        [Fact]
        public void CreateGbXml_UsesInvariantDecimalSeparator_UnderNonEnglishCulture()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                // de-DE uses ',' as the decimal separator; output must still use '.'
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                         + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("height", "7.5"));

                var converter = new OsmToGbXmlConverter(WriteOsmFileStatic(body));
                converter.ParseOsm();
                var result = converter.CreateGbXml();
                var doc = XDocument.Parse(result.Item1);

                var allValues = doc.Descendants(Gb + "Coordinate").Select(c => c.Value)
                    .Concat(new[]
                    {
                        doc.Descendants(Gb + "Latitude").Single().Value,
                        doc.Descendants(Gb + "Longitude").Single().Value
                    });

                foreach (var v in allValues)
                {
                    Assert.DoesNotContain(",", v);
                    double parsed = double.Parse(v, CultureInfo.InvariantCulture);
                    Assert.False(double.IsNaN(parsed));
                }

                Assert.Equal(7.5, MaxZ(doc), 6);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Fact]
        public void CreateGbXml_FloorIsAtZeroAndCeilingAtHeight()
        {
            var body = SquareNodesXml(1, 51.0, 0.0, 0.001)
                     + WayXml(100, Refs(1, 2, 3, 4), Tag("building", "yes"), Tag("height", "10"));

            int parsed;
            Tuple<string, int> result;
            var doc = ConvertSingle(body, out parsed, out result);

            var loops = doc.Descendants(Gb + "ClosedShell").Single().Elements(Gb + "PolyLoop").ToList();

            Func<XElement, IEnumerable<double>> zs = loop => loop
                .Elements(Gb + "CartesianPoint")
                .Select(cp => double.Parse(cp.Elements(Gb + "Coordinate").ElementAt(2).Value, CultureInfo.InvariantCulture));

            Assert.All(zs(loops[0]), z => Assert.Equal(0.0, z, 6));   // floor
            Assert.All(zs(loops[1]), z => Assert.Equal(10.0, z, 6));  // ceiling
        }
    }
}
