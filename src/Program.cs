using System;
using System.Data.SqlTypes;
using System.Globalization;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Types;

namespace Crimediggers.Geo.Console
{
    class Program
    {
        static void Main()
        {
            var suspectOneWaypoints = JsonConvert.DeserializeObject<WaypointContainer>(File.ReadAllText("gps1.json")).Waypoints;
            var suspectTwoWaypoints = JsonConvert.DeserializeObject<WaypointContainer>(File.ReadAllText("gps2.json")).Waypoints;

            // Latitude and longitude precision: https://gis.stackexchange.com/a/8674

            const double highPrecision = 0.0000000001;
            const double mediumPrecision = 0.00001;

            // Approach 1 - high precision match on coordinates
            // This actually yields a single result, but the timestamps don't match
            var sameLocationWaypointsHighPrecision = 
            (
                from waypointOne in suspectOneWaypoints 
                from waypointTwo in suspectTwoWaypoints 
                where Math.Abs(waypointOne.Latitude - waypointTwo.Latitude) < highPrecision && Math.Abs(waypointOne.Longitude - waypointTwo.Longitude) < highPrecision 
                select new 
                {
                    WaypointOne = waypointOne, 
                    WaypointTwo = waypointTwo

                }
            ).ToList();

            // Approach 2a - medium precision match on coordinates
            // This yields more results, some even with timestamps that could be considered close to eachother
            // It doesn't necessarily mean they crossed paths at these locations
            var sameLocationWaypointsMediumPrecision =
            (
                from waypointOne in suspectOneWaypoints 
                from waypointTwo in suspectTwoWaypoints
                where Math.Abs(waypointOne.Latitude - waypointTwo.Latitude) < mediumPrecision && Math.Abs(waypointOne.Longitude - waypointTwo.Longitude) < mediumPrecision
                select new
                {
                    WaypointOne = waypointOne,
                    WaypointTwo = waypointTwo
                }
            ).ToList();

            // Approach 2b - match on distance between waypoints wit haversine: https://en.wikipedia.org/wiki/Haversine_formula
            // Same as 2a, doesn't necessarily mean they crossed paths at these locations
            var waypointsOrderedByDistanceFromEachother =
            (
                from waypointOne in suspectOneWaypoints
                from waypointTwo in suspectTwoWaypoints
                select new
                {
                    WaypointOne = waypointOne,
                    WaypointTwo = waypointTwo,  
                    Distance = Haversine(waypointOne.Location, waypointTwo.Location)
                }
            ).Where(waypoints => waypoints.Distance <= 10).OrderBy(waypoints => waypoints.Distance).ToList();

            // Approach 3 - time to break out the geo spatial goodness using WKT: https://en.wikipedia.org/wiki/Well-known_text_representation_of_geometry
            // Let's 'draw some lines' between coordinates and see where they intersect

            var suspectOneLines = GetLinesFromWaypoints(suspectOneWaypoints);
            var suspectTwoLines = GetLinesFromWaypoints(suspectTwoWaypoints);

            // product of all possible line combination and their intersections (if any)
            var cartesianProduct = suspectOneLines.SelectMany(suspectOneLine => suspectTwoLines, (suspectOneLine, suspectTwoLine) => new
            {
                suspectOneLine,
                suspectTwoLine,
                Intersection = suspectOneLine.Line.STIntersection(suspectTwoLine.Line)
            }).ToArray();

            // filter by having an intersection and start/end times of both lines overlapping
            // this produces 2 near identical intersections that are only different way beyond the 4th decimal, so just take the first
            var intersection = 
                cartesianProduct.First(lines => !lines.Intersection.STIsEmpty().Value &&
                                        DateTimePeriodsOverlap(
                                            lines.suspectOneLine.Current.Time,
                                            lines.suspectTwoLine.Next.Time,
                                            lines.suspectOneLine.Next.Time,
                                            lines.suspectTwoLine.Current.Time));

            double truncatedLat = Math.Truncate(10000 * intersection.Intersection.STY.Value) / 10000;
            double truncatedLon = Math.Truncate(10000 * intersection.Intersection.STX.Value) / 10000;

            var meetingPoint = $"{truncatedLat.ToString(CultureInfo.InvariantCulture)};{truncatedLon.ToString(CultureInfo.InvariantCulture)}";
  
            System.Console.WriteLine(meetingPoint);
            System.Console.ReadKey();
        }

        private static bool DateTimePeriodsOverlap(DateTime firstStart, DateTime secondEnd, DateTime firstEnd, DateTime secondStart)
        {
            return firstStart <= secondEnd && firstEnd >= secondStart;
        }

        private static (SqlGeometry Line, Waypoint Current, Waypoint Next)[] GetLinesFromWaypoints(Waypoint[] waypoints)
        {
            return waypoints.Skip(1).Select((waypoint, index) =>
            {
                var current = waypoints[index] ?? waypoints[0]; // round trip
                var next = waypoint;
                string lineString = $"LINESTRING({current.LonLatInvariant}, {next.LonLatInvariant})";
                var geometry = SqlGeometry.STLineFromText(new SqlChars(lineString.ToCharArray()), 4326); // WGS SRID: https://en.wikipedia.org/wiki/World_Geodetic_System

                return (geometry, current, next);

            }).ToArray();
        }

        private static double Haversine(Location point1, Location point2)
        {
            var d1 = point1.Latitude * (Math.PI / 180.0);
            var num1 = point1.Longitude * (Math.PI / 180.0);
            var d2 = point2.Latitude * (Math.PI / 180.0);
            var num2 = point2.Longitude * (Math.PI / 180.0) - num1;
            var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) +
                     Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);
            return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
        }
    }
}
