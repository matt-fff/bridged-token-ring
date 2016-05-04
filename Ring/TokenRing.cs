using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RingLibrary;

namespace Ring
{
    /// <summary>
    /// Contains the main method for creating and running the token ring
    /// </summary>
    class TokenRing
    {
        /// <summary>
        /// Creates and starts the token ring, ending when the token ring has completed transmission
        /// </summary>
        static void Main(string[] argv)
        {
            if (argv.Length != 2)
            {
                Console.WriteLine("Usage: <executable name>.exe <config file> <# nodes>");
            }
            else
            {
                List<Node> nodes = new List<Node>();
                nodes.Add(new RingLibrary.Monitor());

                try
                {
                    using (StreamReader streamy = new StreamReader(argv[0]))
                    {

                        int port = 7000;

                        //Create every node based on the config file
                        while (streamy.Peek() >= 0)
                        {
                            ushort i = Convert.ToUInt16(streamy.ReadLine(), 10);

                            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                            bool portFound = false;

                            //Find an unbound port for the next node
                            while (!portFound)
                            {
                                //Make sure to get a good port for each node
                                try
                                {
                                    sock.Bind(new IPEndPoint(Address.IP, port));
                                    portFound = true;
                                }
                                catch (Exception)
                                {
                                    // Socket already bound to port
                                    Console.WriteLine("Socket already bound to port " + port);
                                    //Environment.Exit(1);
                                    portFound = false; //unneccessary, but I like thoroughness
                                    port++; //increment the port
                                }
                            }

                            //create the new node with the right port
                            nodes.Add(new Node(i, sock, port));
                            port++;
                        }

                        if (nodes.Count > 254)
                        {
                            throw new Exception();
                        }

                        //Now we have to Handle the Monitor.
                        {
                            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                            bool portFound = false;

                            //Find an unbound port for the next node
                            while (!portFound)
                            {
                                //Make sure to get a good port for each node
                                try
                                {
                                    sock.Bind(new IPEndPoint(Address.IP, port));
                                    portFound = true;
                                }
                                catch (Exception)
                                {
                                    //Socket already bound to port
                                    Console.WriteLine("Socket already bound to port");
                                    portFound = false; //unneccessary, but I like thoroughness
                                    port++; //increment the port
                                }
                            }


                            //Create the monitor node
                            ((RingLibrary.Monitor) nodes[0]).SetMonitor(0, sock, port, Convert.ToUInt16(Convert.ToUInt16(argv[1]) + 1));
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Error processing the configuration file.");
                    Environment.Exit(1);
                }

                //Send port output for the bridge
                using (StreamWriter writey = new StreamWriter(Bridge.RingOut1))
                {
                    writey.WriteLine(Bridge.ListenPort1); //Tell the bridge where to listen
                    writey.WriteLine(nodes[(1 % nodes.Count)].port); //Tell the bridge where to send
                    writey.WriteLine(nodes.Count - 1); //Tell the bridge the subnet's node count
                }

                Thread[] threads = new Thread[nodes.Count];
                for (byte i = 0; i < nodes.Count; i++)
                {
                    threads[i] = new Thread(new ThreadStart(nodes[i].Run));
                    try
                    {
                        threads[i].Start();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Cannot start thread for node " + i + ".");
                        Environment.Exit(1);
                    }
                }

                //Connect the Monitor to the bridge
                while (true)
                {
                    try
                    {
                        nodes[0].Connect(Bridge.ListenPort1);

                        Console.WriteLine("Ring 1 Connected to Bridge");
                        break;
                    }
                    catch (Exception)
                    {
                    }
                }

                for (int k = 2; k < nodes.Count + 1; k++)
                {
                    try
                    {
                        //Set the node we're about to connect to a'listenin'
                        //nodes[(k % numNodes)].Listen();

                        //Connect the node to the next node in the ring.
                        nodes[(k - 1)].Connect(nodes[(k % nodes.Count)]);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Cannot connect node " + (k - 1) + " to node " + (k % nodes.Count) + ".");
                        Environment.Exit(1);
                    }
                }

                //Main cannot exit until all threads are done.
                //Wait for all nodes to finish
                threads[0].Join(); //Wait for the monitor thread to finish

                Console.WriteLine("Finished.");
            }
        }
    }
}