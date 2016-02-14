using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATTransport
{
    class Trip
    {
        public string RouteId { get; set; }
        public string TripId { get; set; }
        public List<TripStopTime> StopTimes {get;set;}
    }
}
