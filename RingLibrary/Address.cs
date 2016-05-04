using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace RingLibrary
{
    /// <summary>
    /// Address class for use with the token ring sockets.
    /// </summary>
    static public class Address
    {
        private static IPAddress ip = new IPAddress(new byte[] { 127, 0, 0, 1 });
        public static IPAddress IP { get { return ip; } }
    }
}
