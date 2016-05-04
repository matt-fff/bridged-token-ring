using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RingLibrary
{
    /// <summary>
    /// Contains the class and functions for the network bridge.
    /// </summary>
    public class Bridge
    {
        enum SubNet { Unknown, Ring1, Ring2 };
        public static int ListenPort1 = 6998;
        public static int ListenPort2 = 6999;
        public static string RingOut1 = "../../../io/Ring1";
        public static string RingOut2 = "../../../io/Ring2";
        public static int ExitTimeout = 15000;
        static int BufferSize = 100;
        protected Socket receive1;
        protected Socket send1;
        protected Socket receive2;
        protected Socket send2;
        StreamWriter logger;
        List<Frame> buffer1;
        List<Frame> unconfirmed1;
        List<FrameVariant> buffer2;
        List<FrameVariant> unconfirmed2;
        SubNet[] routingTable;
        bool ring1Exit;
        bool ring2Exit;
        private bool exit;
        private bool[] nodesFinished; //Each entry in the array signals whether or not the node has finished transmitting
        private Stopwatch exitClock;


        /// <summary>
        /// Sets up all sockets based on the Ring output files.  Determines the number of nodes.
        /// Opens a file for the logger.
        /// </summary>
        /// <param name="logfile">the path for the logfile</param>
        public Bridge(string logfile)
        {
            this.exitClock = new Stopwatch();
            this.exitClock.Reset();
            this.buffer1 = new List<Frame>();
            this.unconfirmed1 = new List<Frame>();
            this.buffer2 = new List<FrameVariant>();
            this.unconfirmed2 = new List<FrameVariant>();

            this.receive1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            this.receive2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            this.send1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            this.send2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            try
            {
                this.logger = new StreamWriter(logfile);
            }
            catch (Exception)
            {
                Console.WriteLine("Error opening the logfile for the bridge");
                Environment.Exit(1);
            }

            int totalNodes = 0;

            try
            {
                //Get input from ring 1
                using (StreamReader streamy = new StreamReader(Bridge.RingOut1))
                {
                    if (streamy.Peek() >= 0)
                    {
                        receive1.Bind(new IPEndPoint(Address.IP, Convert.ToInt32(streamy.ReadLine(), 10)));

                        if (streamy.Peek() >= 0)
                        {
                            send1.Connect(new IPEndPoint(Address.IP, Convert.ToInt32(streamy.ReadLine(), 10)));
                            log("Bridge Connected to Ring 1");

                            if (streamy.Peek() >= 0)
                            {
                                totalNodes += Convert.ToInt32(streamy.ReadLine(), 10);
                            }
                            else
                            {
                                log("Token Ring subnet bridge config output incomplete");
                                throw new Exception();
                            }
                        }
                        else
                        {
                            log("Token Ring subnet bridge config output incomplete");
                            throw new Exception();
                        }
                    }
                    else
                    {
                        log("Token Ring subnet bridge config output incomplete");
                        throw new Exception();
                    }
                }

                //Get input from ring 2
                using (StreamReader streamy = new StreamReader(Bridge.RingOut2))
                {
                    if (streamy.Peek() >= 0)
                    {
                        receive2.Bind(new IPEndPoint(Address.IP, Convert.ToInt32(streamy.ReadLine(), 10)));

                        if (streamy.Peek() >= 0)
                        {
                            send2.Connect(new IPEndPoint(Address.IP, Convert.ToInt32(streamy.ReadLine(), 10)));
                            log("Bridge Connected to Ring 2");

                            if (streamy.Peek() >= 0)
                            {
                                totalNodes += Convert.ToInt32(streamy.ReadLine(), 10);
                            }
                            else
                            {
                                log("Token Ring subnet bridge config output incomplete");
                                throw new Exception();
                            }
                        }
                        else
                        {
                            log("Token Ring subnet bridge config output incomplete");
                            throw new Exception();
                        }
                    }
                    else
                    {
                        log("Token Ring subnet bridge config output incomplete");
                        throw new Exception();
                    }
                }
            }
            catch (Exception)
            {
                log("Error setting up bridge ports from ring output");
                Environment.Exit(1);
            }

            routingTable = new SubNet[totalNodes+1]; //Plus one because we ignore index 0 for the monitor

            for (int k = 0; k < routingTable.Length; k++)
            {
                routingTable[k] = SubNet.Unknown;
            }

            this.nodesFinished = new bool[totalNodes + 1];

            this.nodesFinished[0] = true; //Node 0 is imaginary (or a monitor, depending on your point of view)
            //populate the values of nodesFinished
            for (int k = 1; k < totalNodes + 1; k++)
            {
                this.nodesFinished[k] = false;
            }
        }

        /// <summary>
        /// Sets the bridge listening for a connection so it can receive data
        /// </summary>
        protected void Listen()
        {
            Listen1();
            Listen2();
        }

        /// <summary>
        /// Sets the bridge listening for a connection so it can receive data
        /// </summary>
        protected void Listen1()
        {
            Socket listener1 = receive1;
            listener1.Listen(10);

            try
            {
                //Program is suspended while waiting for an incoming connection.
                receive1 = listener1.Accept();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Sets the bridge listening for a connection so it can receive data
        /// </summary>
        protected void Listen2()
        {
            Socket listener2 = receive2;
            listener2.Listen(10);

            try
            {
                //Program is suspended while waiting for an incoming connection.
                receive2 = listener2.Accept();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Runs all the important functions of the bridge.  Transmits messages between and within subnets.
        /// </summary>
        public void Run()
        {
            Listen();

            try
            {
                while (true)
                {
                    //Deal with Ring 1
                    Frame frame1 = Frame.MakeFrame(Receive1());

                    this.unconfirmed1.Remove(frame1);
                    this.unconfirmed2.Remove(new FrameVariant(frame1));


                    if (!(frame1 is Token) && this.exit)
                    {
                        this.exitClock.Reset();
                        this.exitClock.Start();
                    }

                    //check if the frame1 is a token
                    if (frame1 is Token)
                    {
                        Transmit1();
                        Send1(frame1);//we can transmit and pass the token along
                    }
                    //Check if the frame is signalling an exit (we only care if it's from the monitor)
                    else if (frame1.SA == 0 && frame1.FS == Monitor.exitCode)
                    {
                        this.ring1Exit = true;
                        //log("Ring 1 is ready for shutdown.");

                        //Just try to empty the buffer
                        Transmit1();
                    }
                    //Pass exit information to both monitors
                    else if (frame1.FS == Monitor.exitCode)
                    {
                        log("Node " + frame1.SA + " is ready for shutdown.");
                        Send1(frame1);
                        SetExitStatus(frame1.SA);

                        //Construct and buffer an exit frame for ring 2's monitor
                        FrameVariant tmpFrame = new FrameVariant(0, 0, frame1.SA, null);
                        tmpFrame.FS = MonitorVariant.exitCode;
                        AddToBuffer2(tmpFrame, true);
                    }
                    //pass monitor communication along as usual
                    else if (frame1.SA == 0 || frame1.DA == 0)
                    {
                        Send1(frame1);
                    }
                    //check if we sent this frame to a foreign ring
                    else if (routingTable[frame1.SA] == SubNet.Ring2)
                    {
                        //means that the target node is in Ring1
                        if (frame1.FS == 2 || frame1.FS == 3)
                        {
                            //populate the routing table
                            if (routingTable[frame1.DA] == SubNet.Unknown)
                            {
                                routingTable[frame1.DA] = SubNet.Ring1;
                                log("Node " + frame1.DA + " discovered in Ring 1");
                            }

                            //Check if the frame was rejected by the sender
                            if (frame1.FS == 3)
                            {
                                frame1.FS = 0;

                                if (! this.unconfirmed1.Contains(frame1))
                                {
                                    this.unconfirmed1.Add(frame1);
                                }

                                Send1(frame1); //Resend the frame
                                log("Resending frame to Node " + frame1.DA + " in Ring 1");
                            }
                            //If the frame was accepted
                            else
                            {
                                //AddToBuffer2(new FrameVariant(frame1));
                                //this.unconfirmed1.Remove(frame1);
                            }
                        }
                        //means that the target node is not in Ring1
                        else
                        {
                            //populate the routing table
                            if (routingTable[frame1.DA] == SubNet.Unknown)
                            {
                                routingTable[frame1.DA] = SubNet.Ring2;
                                log("Node " + frame1.DA + " discovered in Ring 2");
                            }
                        }
                    }
                    else
                    {
                        switch (routingTable[frame1.DA])
                        {
                            case SubNet.Ring1:
                                Send1(frame1); //Move along, sir
                                break;

                            case SubNet.Ring2:
                                if (AddToBuffer2(new FrameVariant(frame1)))
                                {
                                    log("Cross-subnet frame from Node " + frame1.SA + " to Node " + frame1.DA);

                                    //Fake ack
                                    frame1.FS = 2;
                                    Send1(frame1);
                                }
                                break;

                            case SubNet.Unknown:
                            default:
                                //We know this frame came from ring 1.
                                if (routingTable[frame1.SA] == SubNet.Unknown)
                                {
                                    routingTable[frame1.SA] = SubNet.Ring1;
                                    log("Node " + frame1.SA + " discovered in Ring 1");
                                }

                                log("Flooding in search of Node " + frame1.DA);

                                Send1(frame1);
                                if (AddToBuffer2(new FrameVariant(frame1)))
                                {
                                    //this.unconfirmed2.Remove(new FrameVariant(frame1));

                                    //Fake ack
                                    frame1.FS = 2;
                                    Send1(frame1);
                                }
                                break;
                        }
                    }


                    //Deal with Ring 2
                    FrameVariant frame2 = FrameVariant.MakeFrame(Receive2());
                    this.unconfirmed1.Remove(new Frame(frame2));
                    this.unconfirmed2.Remove(frame2);

                    if (!(frame2 is TokenVariant) && this.exit)
                    {
                        this.exitClock.Reset();
                        this.exitClock.Start();
                    }

                    //check if the frame2 is a token
                    if (frame2 is TokenVariant)
                    {
                        Transmit2();
                        Send2(frame2);//we can transmit and pass the token along
                    }
                    //Check if the frame is signalling an exit (we only care if it's from the monitor)
                    else if (frame2.SA == 0 && frame2.FS == MonitorVariant.exitCode)
                    {
                        this.ring2Exit = true;
                        //log("Ring 2 is ready for shutdown.");

                        //Just try to empty the buffer
                        Transmit2();
                    }
                    //Pass exit information to both monitors
                    else if (frame2.FS == MonitorVariant.exitCode)
                    {
                        log("Node " + frame2.SA + " is ready for shutdown.");
                        Send2(frame2);
                        SetExitStatus(frame2.SA);

                        //Construct and buffer an exit frame for ring 1's monitor
                        Frame tmpFrame = new Frame(0, 0, frame2.SA, null);
                        tmpFrame.FS = Monitor.exitCode;
                        AddToBuffer1(tmpFrame, true);
                    }
                    //pass monitor communication along as usual
                    else if (frame2.SA == 0 || frame2.DA == 0)
                    {
                        Send2(frame2);
                    }
                    //check if we sent this frame to a foreign ring
                    else if (routingTable[frame2.SA] == SubNet.Ring1)
                    {
                        //means that the target node is in Ring2
                        if (frame2.FS == 2 || frame2.FS == 3)
                        {
                            //populate the routing table
                            if (routingTable[frame2.DA] == SubNet.Unknown)
                            {
                                routingTable[frame2.DA] = SubNet.Ring2;
                                log("Node " + frame2.DA + " discovered in Ring 2");
                            }

                            //Check if the frame was rejected by the sender
                            if (frame2.FS == 3)
                            {
                                frame2.FS = 0;

                                if (!this.unconfirmed2.Contains(frame2))
                                {
                                    this.unconfirmed2.Add(frame2);
                                }

                                Send2(frame2); //Resend the frame
                                log("Resending frame to Node " + frame2.DA + " in Ring 2");
                            }
                            //If the frame was accepted
                            else
                            {
                                //AddToBuffer1(new Frame(frame2));
                                //this.unconfirmed2.Remove(frame2);
                            }
                        }
                        //means that the target node is not in Ring2
                        else
                        {
                            //populate the routing table
                            if (routingTable[frame2.DA] == SubNet.Unknown)
                            {
                                routingTable[frame2.DA] = SubNet.Ring1;
                                log("Node " + frame2.DA + " discovered in Ring 1");
                            }
                        }
                    }
                    else
                    {
                        switch (routingTable[frame2.DA])
                        {
                            case SubNet.Ring2:
                                Send2(frame2); //Move along, sir
                                break;

                            case SubNet.Ring1:
                                if(AddToBuffer1(new Frame(frame2)))
                                {
                                    log("Cross-subnet frame from Node " + frame2.SA + " to Node " + frame2.DA);

                                    //Fake ack
                                    frame2.FS = 2;
                                    Send2(frame2);
                                }
                                break;

                            case SubNet.Unknown:
                            default:
                                //We know this frame came from ring 2.
                                if (routingTable[frame2.SA] == SubNet.Unknown)
                                {
                                    routingTable[frame2.SA] = SubNet.Ring2;
                                    log("Node " + frame2.SA + " discovered in Ring 2");
                                }

                                log("Flooding in search of Node " + frame2.DA);

                                Send2(frame2);
                                if (AddToBuffer1(new Frame(frame2)))
                                {
                                    //this.unconfirmed1.Remove(new Frame(frame2));

                                    //Fake ack
                                    frame2.FS = 2;
                                    Send2(frame2);
                                }
                                break;
                        }
                    }

                    //Check for exit conditions
                    if (this.ring1Exit && this.ring2Exit)
                    {
                        //Send exit code around ring 1
                        frame1 = new Frame(0, 0, 0, null);
                        frame1.FS = Monitor.exitCode;
                        Send1(frame1); //Send out a frame signalling an exit for all nodes

                        //Send exit code around ring 2
                        frame2 = new FrameVariant(0, 0, 0, null);
                        frame2.FS = MonitorVariant.exitCode;
                        Send2(frame2); //Send out a frame signalling an exit for all nodes

                        //Stop the bridge
                        break;
                    }
                }
            }
            finally
            {
                // Close all open resources
                if (receive1 != null)
                    receive1.Close();
                if (receive2 != null)
                    receive2.Close();
                if (send1 != null)
                    send1.Close();
                if (send2 != null)
                    send2.Close();
                if (logger != null)
                    logger.Close();
            }
        }


        /// <summary>
        /// Attempts to receive a complete frame from ring 1
        /// </summary>
        /// <returns>Returns a byte array of the received frame</returns>
        protected byte[] Receive1()
        {
            byte[] completeData = new byte[0]; //initially be large enough for AC, FC, DA, SA, and Size
            int frameSize = Frame.MinBytes; //assume the minimum frame size initially

            try
            {
                while (true)
                {
                    if (receive1.Connected)
                    {
                        while (completeData.Length < frameSize)  //continue until the frame is complete
                        {
                            byte[] data = new byte[1];
                            int numBytes = receive1.Receive(data);

                            //If we received anything, concatenate it with the frame thus far.
                            if (numBytes > 0)
                            {
                                completeData = completeData.Concat(data).ToArray();
                            }

                            //Check if we have enough bytes to figure out the data size
                            if (frameSize == Frame.MinBytes && completeData.Length >= Frame.MinBytes - 1)
                            {
                                //Once we know the number of data bytes, we know the frame size
                                frameSize = Frame.MinBytes + completeData[6];
                            }
                        }
                        break;
                    }
                    else
                    {
                        log("Cannot receive data at the bridge ring 1 not connected.  Attempting reconnect...");
                        Listen1();
                    }
                }
            }
            catch (Exception e)
            {
                log(e.ToString());
                Environment.Exit(1);
            }

            return completeData;
        }


        /// <summary>
        /// Attempts to receive a complete frame from ring 2
        /// </summary>
        /// <returns>Returns a byte array of the received frame</returns>
        protected byte[] Receive2()
        {
            byte[] completeData = new byte[0]; //initially be large enough for AC, FC, DA, SA, and Size
            int frameSize = Frame.MinBytes; //assume the minimum frame size initially

            try
            {
                while (true)
                {
                    if (receive2.Connected)
                    {
                        while (completeData.Length < frameSize)  //continue until the frame is complete
                        {
                            byte[] data = new byte[1];
                            int numBytes = receive2.Receive(data);

                            //If we received anything, concatenate it with the frame thus far.
                            if (numBytes > 0)
                            {
                                completeData = completeData.Concat(data).ToArray();
                            }

                            //Check if we have enough bytes to figure out the data size
                            if (frameSize == Frame.MinBytes && completeData.Length >= Frame.MinBytes - 1)
                            {
                                //Once we know the number of data bytes, we know the frame size
                                frameSize = Frame.MinBytes + completeData[6];
                            }
                        }
                        break;
                    }
                    else
                    {
                        log("Cannot receive data at the bridge ring 2 not connected.  Attempting reconnect...");
                        Listen2();
                    }
                }
            }
            catch (Exception e)
            {
                log(e.ToString());
                Environment.Exit(1);
            }

            return completeData;
        }

        /// <summary>
        /// Sends from ring 1's buffer until the THT or the buffer is exhausted
        /// </summary>
        /// <param name="token">The token that prompted the transmission state</param>
        /// <returns>The parameter token</returns>
        virtual protected void Transmit1()
        {
            //Check if the bridge has completed its work
            if (this.exit
                //&& this.exitClock.ElapsedMilliseconds > Bridge.ExitTimeout
                && this.unconfirmed2.Count == 0
                && this.buffer2.Count == 0
                && this.unconfirmed1.Count == 0
                && this.buffer1.Count == 0)
            {
                Frame frame1 = new Frame(0, 0, 0, null);
                frame1.FS = Monitor.exitCode;
                Send1(frame1); //Send out a frame signalling an exit for all nodes in ring 1

                FrameVariant frame2 = new FrameVariant(0, 0, 0, null);
                frame2.FS = MonitorVariant.exitCode;
                Send2(frame2); //Send out a frame signalling an exit for all nodes in ring 2
                Environment.Exit(1);
            }
            else
            {
                int THT = 0;

                double ratBefore = (double)buffer1.Count / (double)Bridge.BufferSize;

                //Transmit from the buffer until we can't anymore
                while (this.buffer1.Count > 0 && (THT + this.buffer1[0].size) < Token.THT)
                {
                    Send1(this.buffer1[0]);
                    THT += this.buffer1[0].size;

                    //Set the frame to unconfirmed
                    if (!this.unconfirmed1.Contains(this.buffer1[0]))
                    {
                        this.unconfirmed1.Add(this.buffer1[0]);
                    }

                    this.buffer1.RemoveAt(0);
                }


                double ratAfter = (double)buffer1.Count / (double)Bridge.BufferSize;

                if ((ratBefore >= .25 && ratAfter < .25)
                    || (ratBefore >= .5 && ratAfter < .5)
                    || (ratBefore >= .75 && ratAfter < .75))
                {
                    log("Buffer 1 is " + ratAfter * 100 + "% full");
                }
            }

        }

        /// <summary>
        /// Sends from ring 2's buffer until the THT or the buffer is exhausted
        /// </summary>
        /// <param name="token">The token that prompted the transmission state</param>
        /// <returns>The parameter token</returns>
        virtual protected void Transmit2()
        {
            //Check if the bridge has completed its work
            if (this.exit
                //&& this.exitClock.ElapsedMilliseconds > Bridge.ExitTimeout
                && this.unconfirmed2.Count == 0
                && this.buffer2.Count == 0
                && this.unconfirmed1.Count == 0
                && this.buffer1.Count == 0)
            {
                Frame frame1 = new Frame(0, 0, 0, null);
                frame1.FS = Monitor.exitCode;
                Send1(frame1); //Send out a frame signalling an exit for all nodes in ring 1

                FrameVariant frame2 = new FrameVariant(0, 0, 0, null);
                frame2.FS = MonitorVariant.exitCode;
                Send2(frame2); //Send out a frame signalling an exit for all nodes in ring 2
                Environment.Exit(1);
            }
            else
            {
                int THT = 0;

                double ratBefore = (double)buffer2.Count / (double)Bridge.BufferSize;

                //Transmit from the buffer until we can't anymore
                while (this.buffer2.Count > 0 && (THT + this.buffer2[0].size) < Token.THT)
                {
                    Send2(this.buffer2[0]);
                    THT += this.buffer2[0].size;

                    //Set the frame to unconfirmed
                    if (!this.unconfirmed2.Contains(this.buffer2[0]))
                    {
                        this.unconfirmed2.Add(this.buffer2[0]);
                    }

                    this.buffer2.RemoveAt(0);
                }

                double ratAfter = (double)buffer2.Count / (double)Bridge.BufferSize;

                if ((ratBefore >= .25 && ratAfter < .25)
                    || (ratBefore >= .5 && ratAfter < .5)
                    || (ratBefore >= .75 && ratAfter < .75))
                {
                    log("Buffer 2 is " + ratAfter * 100 + "% full");
                }
            }
        }

        /// <summary>
        /// Attempts to send the specified frame to ring 1
        /// </summary>
        /// <param name="frame">The frame to be sent</param>
        protected void Send1(Frame frame)
        {
            while (true)
            {
                try
                {
                    if (send1.Connected)
                    {
                        send1.Send(frame.ToBinary());

                        break;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception e)
                {
                    log("Cannot send data from the bridge to ring 1.");
                    log(e.ToString());
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// Attempts to send the specified frame to ring 2
        /// </summary>
        /// <param name="frame">The frame to be sent</param>
        protected void Send2(FrameVariant frame)
        {
            while (true)
            {
                try
                {
                    if (send2.Connected)
                    {
                        send2.Send(frame.ToBinary());

                        break;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception e)
                {
                    log("Cannot send data from the bridge to ring 2.");
                    log(e.ToString());
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// Outputs the given string to the logfile (with timestamp) and to console (for my sanity)
        /// </summary>
        /// <param name="output">The event description for logging</param>
        protected void log(string output)
        {
            Console.WriteLine(output); //Printing to console as well to help my sanity.
            logger.Write("Time: " + DateTime.Now.ToString("HH:mm:ss tt") + "\t Event: " + output + "\n");
            logger.Flush();
        }

        /// <summary>
        /// If the buffer is not full, add to the buffer.  Logs notable transitions in buffer size.
        /// The limit can be bypassed by setting the ignoreMax parameter to true.
        /// </summary>
        /// <param name="frame1">The frame to be buffered</param>
        /// <param name="ignoreMax">Bool for bypassing buffer limit</param>
        /// <returns>returns true if the frame was inserted, false otherwise</returns>
        protected bool AddToBuffer1(Frame frame1, bool ignoreMax = false)
        {
            /*if (buffer1.Count < Bridge.BufferSize || ignoreMax)
            {*/
                double ratBefore = (double)buffer1.Count / (double)Bridge.BufferSize;

                buffer1.Add(frame1);

                double ratAfter = (double)buffer1.Count / (double)Bridge.BufferSize;

                //Not printing at 100%, because at that point you see a print every half second
                if ((ratBefore < .25 && ratAfter >= .25)
                    || (ratBefore < .5 && ratAfter >= .5)
                    || (ratBefore < .75 && ratAfter >= .75))
                {
                    log("Buffer 1 is " + ratAfter * 100 + "% full");
                }

                return true;
            /*}
            else 
            {
                return false;
            }*/
        }

        /// <summary>
        /// If the buffer is not full, add to the buffer.  Logs notable transitions in buffer size.
        /// The limit can be bypassed by setting the ignoreMax parameter to true.
        /// </summary>
        /// <param name="frame2">The frame to be buffered</param>
        /// <param name="ignoreMax">Bool for bypassing buffer limit</param>
        /// <returns>returns true if the frame was inserted, false otherwise</returns>
        protected bool AddToBuffer2(FrameVariant frame2, bool ignoreMax = false)
        {
            /*if (buffer2.Count < Bridge.BufferSize || ignoreMax)
            {*/
                double ratBefore = (double)buffer2.Count / (double)Bridge.BufferSize;

                buffer2.Add(frame2);

                double ratAfter = (double)buffer2.Count / (double)Bridge.BufferSize;


                //Not printing at 100%, because at that point you see a print every half second
                if ((ratBefore < .25 && ratAfter >= .25)
                    || (ratBefore < .5 && ratAfter >= .5)
                    || (ratBefore < .75 && ratAfter >= .75))
                {
                    log("Buffer 2 is " + ratAfter*100 + "% full");
                }

                return true;
            /*}
            else 
            {
                return false;
            }*/
        }

        /// <summary>
        /// Sets the finished status of the source of the frame to be true.
        /// Then calls CheckExit to ensure proper setting of the exit attribute.
        /// </summary>
        /// <param name="frame">Assumed to be an exit signal.  The frame whose source's finished status needs changed.</param>
        private void SetExitStatus(ushort SA)
        {
            if (!this.nodesFinished[SA])
            {
                this.nodesFinished[SA] = true; //Set the source's finished status to true
            }

            CheckExit(); //Check if the change has triggered an exit
        }

        /// <summary>
        /// Iterates through the finished statuses of all the nodes and determines if we should signal an overall exit.
        /// </summary>
        private void CheckExit()
        {
            this.exit = true; //we exit if no node has a false exit status

            for (int k = 0; k < this.nodesFinished.Length; k++)
            {
                //if any node isn't finished, we aren't ready to exit
                if (!this.nodesFinished[k])
                {
                    this.exit = false;
                    break;
                }
            }

            if (this.exit)
            {
                this.exitClock.Start();
            }
        }
    }
}
