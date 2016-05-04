using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RingLibrary;

namespace NetworkBridge
{
    class NetworkBridge
    {
        static void Main(string[] argv)
        {
            if (argv.Length != 1)
            {
                Console.WriteLine("Usage: <executable name>.exe <log file>");
            }
            else
            {
                Bridge bridge = new Bridge(argv[0]);

                bridge.Run();
            }
        }
    }
}
