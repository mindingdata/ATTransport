using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATTransport
{
    class OutputMapStopItem
    {
        public string TripId { get; set; }
        public double StartTime { get; set; }
        public List<OutputMapStopLatLng> LatLngs { get; set; }
        public int TotalDistanceTraveled { get; set; }

        public double TotalTimeTaken { get; set; }
    }

    class OutputMapStopLatLng
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
