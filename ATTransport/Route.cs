using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATTransport
{
    class Route
    {
        public string RouteId { get; set; }
        public string RouteShortName { get; set; }

        public List<Trip> Trips {get;set;}

    }
}
