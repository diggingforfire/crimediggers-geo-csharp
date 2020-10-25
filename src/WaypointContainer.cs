using Newtonsoft.Json;

namespace Crimediggers.Geo.Console
{
    public class WaypointContainer
    {
        [JsonProperty("wpt")]
        public Waypoint[] Waypoints { get; set; }
    }
}

