using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace RingLibrary
{
    /// <summary>
    /// Contains the class and functions for the standard nodes in the token ring.
    /// </summary>
    public class Node
    {
        protected int THT;                    // Token Holding Time
        protected byte previousReserved;      // Stores the previous reserved bits for priority
        protected bool swappedReserved;        // true if we set a token's AC reserved bits and haven't set it back yet
        protected ushort num;         // The number (id, really) of the node
        protected Socket sock;      // the socket for listening/receiving
        protected Socket sendy;     // the socket for sending data
        public int port;           // the port used by this node for listening
        protected int targetPort;             // the port used by this node's target node for listening
        protected StreamReader reader;        // the reader for this node's input file
        protected StreamWriter writer;        // the writer for this node's output file
        protected Frame frame;                // the current frame from the input file we need to send
        protected bool lastFrameSent;         // indicates whether or not frame has been sent and we can move on to the next
        protected bool exitSent;              // we only ever want to send an exit code once
        protected List<Frame> awaitingConfirmation; // contains all sent frames that we have not yet received a positive confirmation for.

        /// <summary>
        /// Does nothing.  Exists primarily to eliminate build errors for subclass Monitor.
        /// </summary>
        public Node()
        { 
        }

        /// <summary>
        /// Sets attributes to equal their corresponding parameters. Finds and opens the proper streamreader/writer files.
        /// </summary>
        /// <param name="num">The number identifier of the node</param>
        /// <param name="sock">The receiving socket for this node</param>
        /// <param name="port">The port this node will listen on</param>
        public Node(ushort num, Socket sock, int port)
        {
            // Assign object variables
            this.num = num;
            this.previousReserved = 0;
            this.swappedReserved = false;
            this.sock = sock;
            this.port = port;
            this.THT = 0; // By default, a node is given no THT
            sendy = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            this.lastFrameSent = true;
            this.awaitingConfirmation = new List<Frame>();
            this.exitSent = false;
            
            try
            {
                this.reader = new StreamReader("../../../io/input-file-" + this.num);
                this.writer = new StreamWriter("../../../io/output-file-" + this.num);
            }
            catch (Exception)
            {
                Console.WriteLine("Error opening the input/output files for node " + this.num + ".");
                
                //construct a frame that tells the monitor that we're done
                this.lastFrameSent = false;
                this.frame = new Frame(0, 0, this.num, null);
                this.frame.FS = Monitor.exitCode;
            }
            
        }

        /// <summary>
        /// Sets the targetPort attribute equal to the parameter and attempts a connection.
        /// </summary>
        /// <param name="target">The target node for sending data</param>
        public void Connect(Node target)
        {
            this.targetPort = target.port;

            Connect();
        }

        /// <summary>
        /// Sets the targetPort attribute equal to the parameter and attempts a connection.
        /// </summary>
        /// <param name="target">The target port for sending data</param>
        public void Connect(int target)
        {
            this.targetPort = target;

            Connect();
        }

        /// <summary>
        /// Attempts to connect to the port specified by the targetPort attribute.
        /// </summary>
        protected void Connect()
        {
            sendy.Connect(new IPEndPoint(Address.IP, this.targetPort));
        }

        /// <summary>
        /// Sets this node listening for a connection so it can receive data
        /// </summary>
        protected void Listen()
        {
            Socket listener = sock;
            listener.Listen(10);

            try
            {
                // Program is suspended while waiting for an incoming connection.
                sock = listener.Accept();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());

                //construct a frame that tells the monitor that we're done
                this.lastFrameSent = false;
                this.frame = new Frame(0, 0, this.num, null);
                this.frame.FS = Monitor.exitCode;
            }
        }

        /// <summary>
        /// Attempts to send the specified frame to the target node.
        /// </summary>
        /// <param name="frame">The frame to be sent</param>
        protected void Send(Frame frame)
        {
            while (true)
            {
                try
                {
                    if (sendy.Connected)
                    {
                        sendy.Send(frame.ToBinary());

                        break;
                    }
                    else 
                    {
                        Console.WriteLine("Cannot send data from node " + this.num + ".  Waiting for connection...");
                        System.Threading.Thread.Sleep(1000);
                        Connect();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    
                    //construct a frame that tells the monitor that we're done
                    this.lastFrameSent = false;
                    this.frame = new Frame(0, 0, this.num, null);
                    this.frame.FS = Monitor.exitCode;
                }
            }
        }

        /// <summary>
        /// Runs all the important functions of the node.  Passes data along through the ring when not in a transmission state.
        /// Detects tokens and switches to a transmission state when they're received.  
        /// Sends out an exit signal to the monitor when transmission is complete.
        /// </summary>
        virtual public void Run()
        {
            Listen();


            // Accept incoming connection and wait for
            // right neighbor to be ready.
            try
            {
                while (true)
                {
                    Frame frame = Frame.MakeFrame(Receive());

                    //check if the frame is a token
                    if (frame is Token)
                    {
                        Send(Transmit(frame));//we can transmit and pass the token along
                    }
                    //Check if the frame is signalling an exit (we only care if it's from the monitor)
                    else if (frame.SA == 0 && frame.FS == Monitor.exitCode)
                    {
                        Send(frame);
                        break;
                    }
                    //check if the frame was sent to this node
                    else if (frame.DA == this.num)
                    {
                        Random random = new Random();

                        //Use randomness to determine acceptance
                        if (random.Next(0, 2) >= 1)
                        {
                            frame.FS = 2; //accept the frame
                            writeOutputFrame(frame);
                        }
                        else 
                        {
                            frame.FS = 3; //reject the frame
                        }
                        
                        Send(frame);
                    }
                    //check if the frame was sent from this node (i.e. made a complete circuit)
                    else if (frame.SA == this.num)
                    {
                        //Check if the frame was rejected by the sender
                        if (frame.FS == 3)
                        {
                            frame.FS = 0;
                            Send(frame); //Resend the frame
                        }
                        //Check if we're ready to send the monitor node an exit signal
                        else if (!this.exitSent && !this.lastFrameSent && this.frame.FS == Monitor.exitCode && this.awaitingConfirmation.Count == 0)
                        {
                            this.frame.timer.Reset();
                            this.frame.timer.Start();
                            this.awaitingConfirmation.Add(this.frame);
                            Send(this.frame); //Send the exit signal
                            this.exitSent = true;
                        }
                        else
                        {
                            this.awaitingConfirmation.Remove(frame); //remove one of the frames from "awaiting confirmation" status
                        }
                        //By doing nothing, we effectively remove the frame from the ring
                    }
                    //this frame is not relevant to this node.  Pass it along.
                    else
                    {
                        Send(frame);
                    }
                }
            }
            finally
            {
                // Close all open resources (i.e. sockets!)
                if(sock != null)
                    sock.Close();
                if(reader != null)
                    reader.Close();
                if(reader != null)
                    writer.Close();
                if(reader != null)
                    sendy.Close();
            }
        }

        /// <summary>
        /// Attempts to receive a complete frame from the receiving socket sock
        /// </summary>
        /// <returns>Returns a byte array of the received frame</returns>
        protected byte[] Receive()
        {
            byte[] completeData = new byte[0]; //initially be large enough for AC, FC, DA, SA, and Size
            int frameSize = Frame.MinBytes; //assume the minimum frame size initially

            try
            {
                while (true)
                {
                    if (sock.Connected)
                    {

                        while (completeData.Length < frameSize)  //continue until the frame is complete
                        {
                            byte[] data = new byte[1];
                            int numBytes = sock.Receive(data);

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
                        Console.WriteLine("Cannot receive data at node " + this.num + ". Socket not connected.  Attempting reconnect...");
                        Listen();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }

            return completeData;
        }

        /// <summary>
        /// Writes the parameter to the output file.  Uses non-priority format if the priority bit in FC is not set,
        /// otherwise it uses priority format.
        /// </summary>
        /// <param name="output">The frame to be outputted</param>
        void writeOutputFrame(Frame output)
        {
            //write out the data
            //Check if the priority bit is set
            if ((byte)((output.FC | 127) ^ 127) == 128)
            {
                //use priority output
                writer.Write(output.SA
                                + ","
                                + output.DA
                                + ","
                                + output.AC
                                + ","
                                + output.size
                                + ","
                                + Encoding.GetEncoding("iso-8859-1").GetString(output.data)
                                + "\n");
            }
            else
            {
                //use standard output
                writer.Write(output.SA
                                + ","
                                + output.DA
                                + ","
                                + output.size
                                + ","
                                + Encoding.GetEncoding("iso-8859-1").GetString(output.data)
                                + "\n");
            }
            

            writer.Flush();
        }

        /// <summary>
        /// Reads in a frame from the input file, storing the result in this.frame and setting this.lastFrameSent accordingly.
        /// If there is no data left to read, this.frame is set to be an exit frame so that an exit signal will be sent to the monitor.
        /// </summary>
        /// <returns>Returns false if the processing of the file throws an exception or we're out of data</returns>
        bool readInputFrame()
        {
            // Send until THT reached
            string line;

            try
            {

                //read the line in.  Set frame if there is 
                if ((line = reader.ReadLine()) != null)
                {
                    string[] splits = line.Split(',');
                    try
                    {
                        byte dest = (byte)Convert.ToInt16(splits[0]);
                        byte access = 0; //Access byte defaults to 0 if no priority
                        byte size;
                        byte[] data;

                        //Less than four and we're dealing with non-priority files
                        if (splits.Length < 4)
                        {
                            size = (byte)Convert.ToInt16(splits[1]);
                            data = Encoding.GetEncoding("iso-8859-1").GetBytes(splits[2]);

                            this.frame = new Frame(access, dest, this.num, data);
                        }
                        //We're dealing with priority files
                        else
                        {
                            access = (byte)Convert.ToInt16(splits[1]);
                            size = (byte)Convert.ToInt16(splits[2]);
                            data = Encoding.GetEncoding("iso-8859-1").GetBytes(splits[3]);

                            this.frame = new Frame(access, dest, this.num, data);
                            this.frame.FC = (byte)(this.frame.FC | 128); //Setting the highest bit to 1 to signify priority frame
                        }

                        if (data.Length != size)
                        {
                            Console.WriteLine("Input file data incorrect size for node " + this.num + ".");
                            //Bad data file
                        }
                        this.lastFrameSent = false;
                    }
                    catch (Exception)
                    {
                        this.lastFrameSent = true;
                        Console.WriteLine("Malformed input file for node " + this.num + ".");

                        //construct a frame that tells the monitor that we're done
                        this.lastFrameSent = false;
                        this.frame = new Frame(0, 0, this.num, null);
                        this.frame.FS = Monitor.exitCode;
                        return false; //Bad data file
                    }

                }
                else
                {
                    //construct a frame that tells the monitor that we're done
                    this.lastFrameSent = false;
                    this.frame = new Frame(0, 0, this.num, null);
                    this.frame.FS = Monitor.exitCode;
                    return false; //No data left to transmit
                }
            }
            catch (Exception)
            {
                this.lastFrameSent = true;
                Console.WriteLine("Malformed input file for node " + this.num + ".");

                //construct a frame that tells the monitor that we're done
                this.lastFrameSent = false;
                this.frame = new Frame(0, 0, this.num, null);
                this.frame.FS = Monitor.exitCode;
                return false; //Bad data file
            }

            return true;
        }

        /// <summary>
        /// Retrieves a frame for sending and sends it, modifying the token as needed for proper priority reservations
        /// </summary>
        /// <param name="token">The token that prompted the transmission state</param>
        /// <returns>The parameter token - potentially modified with a new priority reservation</returns>
        virtual protected Frame Transmit(Frame token)
        {
            this.THT = Token.THT; //Add the appropriate THT since we now have a token

            //Check if we're receiving a token of the same priority as we reserved for
            if (this.swappedReserved && (byte)(token.AC >> 5) == this.frame.AC)
            {
                this.previousReserved = (byte)((token.AC | 248) ^ 248); //zero out all but the last three bits of the token's AC and store it for future use
                this.swappedReserved = false;
            }

            //This if statement may seem redundant, but readInputFrame() may change the value of lastFrameSent.
            //If we have a frame to send
            while (this.lastFrameSent == false || (readInputFrame() && this.lastFrameSent == false))
            {
                //Check if we're sending an exit code
                if (this.frame.FS == Monitor.exitCode)
                {
                    this.frame.timer.Reset();
                    this.frame.timer.Start();
                    this.awaitingConfirmation.Add(this.frame);
                    Send(this.frame); //Send the "finished" signal to the monitor
                    break;
                }
                //Now check if we're going to go over the THT and if we have sufficient priority
                else if ((byte)(token.AC >> 5) <= this.frame.AC && this.frame.size < this.THT)
                {
                    this.THT -= this.frame.size;
                    this.frame.timer.Reset();
                    this.frame.timer.Start();
                    this.awaitingConfirmation.Add(this.frame);
                    Send(this.frame);
                    this.lastFrameSent = true;
                }
                //Check if we need to reserve the token for later priority
                else if ((byte)(token.AC << 5) < (byte)(this.frame.AC << 5))
                {
                    this.previousReserved = (byte)((token.AC | 248) ^ 248); //zero out all but the last three bits of the token's AC and store it for future use
                    this.swappedReserved = true;
                    token.AC = (byte) (((token.AC | 7) ^ 7) | this.frame.AC); //zero out the last three bits of the token's AC and reserve the next priority 
                    this.lastFrameSent = false;
                    break;
                }
                else
                {
                    this.lastFrameSent = false;
                    break;
                }
            }

            return token;
        }
    }
}
