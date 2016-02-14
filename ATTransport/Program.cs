using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ATTransport
{
    class Program
    {
        static List<Route> routes = new List<Route>();
        static Dictionary<string, Stop> stopLookup = new Dictionary<string, Stop>();

        //http://stackoverflow.com/questions/1253499/simple-calculations-for-working-with-lat-lon-km-distance
        //Auckland Center = Lat : -36.84846 Lng :174.76333 
        static double centerLat = -36.8500;
        static double centerLng = 174.8667;
        static int maxLatKM = 5;
        static int maxLngKM = 7;

        static void Main(string[] args)
        {
          
            LoadData();
            Console.WriteLine("Done Loading Data!");
            OutputData();

        }

        private static void LoadData()
        {
            //Load Routes. 
            var routeLines = File.ReadAllLines("google_transit/routes.txt").ToList();

            //Remove header line. 
            routeLines.RemoveAt(0);

            foreach (var line in routeLines)
            {
                var lineParts = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                routes.Add(new Route
                {
                    RouteId = lineParts[4],
                    RouteShortName = lineParts[6],
                    Trips = new List<Trip>()
                });
            }

            //A route has many trips (e.g. The same number runs multiple times). 
            var tripLines = File.ReadAllLines("google_transit/trips.txt").ToList();
            tripLines.RemoveAt(0);

            //Use a dictionary since it will be WAY faster later to find all these trips when we have to add stops to them. 
            Dictionary<string, Trip> tripLookup = new Dictionary<string, Trip>();

            foreach (var line in tripLines)
            {
                var lineParts = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                Trip newTrip = new Trip { RouteId = lineParts[1], TripId = lineParts[6], StopTimes = new List<TripStopTime>() };
                routes.Single(x => x.RouteId == newTrip.RouteId).Trips.Add(newTrip);
                tripLookup.Add(newTrip.TripId, newTrip);
            }

            //For each trip, load in the time they hit each stop. 
            var stopTimeLines = File.ReadAllLines("google_transit/stop_times.txt").ToList();
            stopTimeLines.RemoveAt(0);

            foreach (var line in stopTimeLines)
            {
                var lineParts = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

                //Sometimes the trip ends after midnight. (Or sometimes longer), so just skip these ones for now. 
                if (int.Parse(lineParts[2].Split(':')[0]) > 23)
                {
                    continue;
                }

                TripStopTime stopTime = new TripStopTime { TripId = lineParts[0], StopId = lineParts[3], Time = DateTime.Parse(lineParts[2]) }; //Z appended so it comes through as UTC. 
                tripLookup[stopTime.TripId].StopTimes.Add(stopTime);
            }

            //Load stop data. 
            var stopLines = File.ReadAllLines("google_transit/stops.txt").ToList();
            stopLines.RemoveAt(0);

            foreach (var line in stopLines)
            {
                var lineParts = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

                Stop newStop = new Stop { StopId = lineParts[3], StopLat = double.Parse(lineParts[0]), StopLng = double.Parse(lineParts[2]) };
                stopLookup.Add(newStop.StopId, newStop);
            }
        }

        private static void OutputData()
        {
            //Minimums that we will use later. 
            double maxLat = centerLat + (maxLatKM / 110.574);
            double minLat = centerLat - (maxLatKM / 110.574);
            double maxLng = centerLng + (maxLngKM / 111.320 * Math.Cos(centerLat));
            double minLng = centerLng - (maxLngKM / 111.320 * Math.Cos(centerLat));

            //Overrideen to focus on Dominion road
            //maxLat = -36.860440;
            //minLat = -36.911379;
            //minLng = 174.722142;
            //maxLng = 174.766603;

            var selectedRoutes = routes;

            var outputMapStopItems = new List<OutputMapStopItem>();
            foreach (var route in selectedRoutes)
            {
                //Get all trips that actually fall into our time range that we want. 
                //Just hardcoded to 6 - 10AM approx. 
                var selectedTrips = route.Trips.Where(x => x.StopTimes.Any(y => DateTime.Compare(y.Time, DateTime.Parse("6:00:00 AM")) > 0 && DateTime.Compare(y.Time, Convert.ToDateTime("10:00:00 AM")) < 0));

                foreach (var trip in selectedTrips)
                {
                    var minStopTime = DateTime.MaxValue;
                    var maxStopTime = DateTime.MinValue;

                    var stopLatLngs = new List<OutputMapStopLatLng>();
                    foreach (var stopTime in trip.StopTimes.OrderBy(x => x.Time))
                    {
                        var stop = stopLookup[stopTime.StopId];

                        if (stop.StopLat < maxLat && stop.StopLat > minLat && stop.StopLng < maxLng && stop.StopLng > minLng)
                        {
                            stopLatLngs.Add(new OutputMapStopLatLng { Lat = stop.StopLat, Lng = stop.StopLng });

                            //Used later on. 
                            if (stopTime.Time < minStopTime)
                                minStopTime = stopTime.Time;
                            if (stopTime.Time > maxStopTime)
                                maxStopTime = stopTime.Time;
                        }
                    }

                    //Bail if we didn't actually find any stops that fit what we needed. 
                    if (stopLatLngs.Count < 2)
                        continue;

                    //Work out the distance travelled total. 
                    var distanceTravelled = 0.0;
                    for(int i=1; i < stopLatLngs.Count; i++)
                    {
                        distanceTravelled += DistanceBetweenPoints(stopLatLngs[i - 1].Lat, stopLatLngs[i - 1].Lng, stopLatLngs[i].Lat, stopLatLngs[i].Lng);
                    }

                    var stopTimeStart = trip.StopTimes.OrderBy(x => x.Time).First();

                    outputMapStopItems.Add(new OutputMapStopItem
                    {
                        StartTime = trip.StopTimes.OrderBy(x => x.Time).First().Time.TimeOfDay.TotalSeconds,
                        TripId = trip.TripId,
                        LatLngs = stopLatLngs,
                        TotalDistanceTraveled = (int)(distanceTravelled * 1000), 
                        TotalTimeTaken = maxStopTime.Subtract(stopTimeStart.Time).TotalMilliseconds
                    });
                }


            }

            var outputText = JsonConvert.SerializeObject(outputMapStopItems);

            File.WriteAllText("stops.json", outputText);
        }

        private static double ToRadian(double val) { return val * (Math.PI / 180); }
        private static double DiffRadian(double val1, double val2) { return ToRadian(val2) - ToRadian(val1); }
        private static double DistanceBetweenPoints(double lat1, double lng1, double lat2, double lng2)
        {
            double radius = 6367.0;
            return radius * 2 * Math.Asin(Math.Min(1, Math.Sqrt((Math.Pow(Math.Sin((DiffRadian(lat1, lat2)) / 2.0), 2.0) + Math.Cos(ToRadian(lat1)) * Math.Cos(ToRadian(lat2)) * Math.Pow(Math.Sin((DiffRadian(lng1, lng2)) / 2.0), 2.0)))));
        }
    }


}
