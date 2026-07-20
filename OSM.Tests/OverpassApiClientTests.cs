using OSM;
using Xunit;

namespace OSM.Tests
{
    public class OverpassApiClientTests
    {
        [Fact]
        public void ValidateBoundingBox_ValidBox_ReturnsTrueWithNoError()
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(51.0, -0.2, 51.1, -0.1, out string error);

            Assert.True(ok);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateBoundingBox_ExtremeButLegalBounds_ReturnsTrue()
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(-90, -180, 90, 180, out string error);

            Assert.True(ok);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateBoundingBox_SouthGreaterThanNorth_ReturnsFalse()
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(52.0, -0.2, 51.0, -0.1, out string error);

            Assert.False(ok);
            Assert.Contains("Southern latitude", error);
        }

        [Fact]
        public void ValidateBoundingBox_SouthEqualToNorth_ReturnsFalse()
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(51.0, -0.2, 51.0, -0.1, out string error);

            Assert.False(ok);
            Assert.Contains("Southern latitude", error);
        }

        [Fact]
        public void ValidateBoundingBox_WestGreaterThanEast_ReturnsFalse()
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(51.0, 0.5, 51.1, 0.1, out string error);

            Assert.False(ok);
            Assert.Contains("Western longitude", error);
        }

        [Fact]
        public void ValidateBoundingBox_WestEqualToEast_ReturnsFalse()
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(51.0, 0.1, 51.1, 0.1, out string error);

            Assert.False(ok);
            Assert.Contains("Western longitude", error);
        }

        [Theory]
        [InlineData(-91.0, 51.0)]   // south below -90
        [InlineData(50.0, 90.5)]    // north above 90
        public void ValidateBoundingBox_LatitudeOutOfRange_ReturnsFalse(double south, double north)
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(south, -0.2, north, -0.1, out string error);

            Assert.False(ok);
            Assert.Contains("Latitude must be between -90 and 90", error);
        }

        [Theory]
        [InlineData(-180.5, 0.0)]   // west below -180
        [InlineData(0.0, 180.5)]    // east above 180
        public void ValidateBoundingBox_LongitudeOutOfRange_ReturnsFalse(double west, double east)
        {
            bool ok = OverpassApiClient.ValidateBoundingBox(51.0, west, 51.1, east, out string error);

            Assert.False(ok);
            Assert.Contains("Longitude must be between -180 and 180", error);
        }
    }
}
