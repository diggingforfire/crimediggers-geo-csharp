using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Crimediggers.Geo.Console
{
    public class Waypoint
    {
        [JsonProperty("Lat")]
        public double Latitude { get; set; }

        [JsonProperty("Lon")]
        public double Longitude { get; set; }
        public Location Location => new Location { Latitude = Latitude, Longitude = Longitude };

        public string LonLatInvariant => $"{Longitude.ToString(CultureInfo.InvariantCulture)} {Latitude.ToString(CultureInfo.InvariantCulture)}";

        public DateTime Time { get; set; }

        public override string ToString()
        {
            return $"Lat: {Latitude}. Lon: {Longitude}. Time: {Time}";
        }
    }
}
