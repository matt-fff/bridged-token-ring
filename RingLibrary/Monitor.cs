using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RingLibrary
{
    /// <summary>
    /// Contains the class and functions for the special monitor node
    /// </summary>
    public class Monitor : Node
    {
        public static byte exitCode = 252;
        private bool exit;
        private uint nodeCount; //The count of all nodes not including the monitor
        private bool[] nodesFinished; //Each entry in the array signals whether or not the node has finished transmitting

        /// <summary>
        /// Does nothing.
        /// </summary>
        public Monitor()
        {
        }

        /// <summary>
        /// Sets the attributes to their corresponding parameters.
        /// </summary>
        /// <param name="num">The number of this node (should be 0)</param>
        /// <param name="sock">The socket for receiving data</param>
        /// <param name="port">The port that this node will be listening on</param>
        /// <param name="nodeCount">The number of nodes in the token ring</param>
        public Monitor(ushort num, Socket sock, int port, uint nodeCount)
        {
            this.SetMonitor(num, sock, port, nodeCount);
        }

        /// <summary>
        /// Sets the attributes to their corresponding parameters.
        /// </summary>
        /// <param name="num">The number of this node (should be 0)</param>
        /// <param name="sock">The socket for receiving data</param>
        /// <param name="port">The port that this node will be listening on</param>
        /// <param name="nodeCount">The number of nodes in the token ring</param>
        public void SetMonitor(ushort num, Socket sock, int port, uint nodeCount)
        {
            // Assign object variables
            this.num = num;
            this.sock = sock;
            this.port = port;
            this.exit = false;
            this.exitSent = false;
            this.nodeCount = nodeCount;
            this.nodesFinished = new bool[this.nodeCount];

            //populate the values of nodesFinished
            for (int k = 0; k < this.nodeCount; k++)
            {
                this.nodesFinished[k] = false;
            }

            sendy = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        }
        
        /// <summary>
        /// Runs all the important functions of the monitor.  Passes data along through the ring.
        /// Detects when other nodes finish transmitting.  Creates and handle tokens.  Send out exit signal to the ring.
        /// </summary>
        override public void Run()
        {
            Listen();

            // This is the monitor. Make the first token.
            //TODO: double check the token creation
            Transmit();
            

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
                        //Transmit(frame); //we can transmit
                        Transmit();
                    }
                    //Check if the frame is signalling an exit
                    else if (frame.FS == Monitor.exitCode)
                    {
                        if (frame.DA == this.num && frame.SA != this.num)
                        {
                            SetExitStatus(frame);
                        }
                        else
                        {
                            break;
                        }
                    }
                    //check if the frame was sent to this node
                    else if (frame.DA == this.num)
                    {
                        Random random = new Random();

                        //Use randomness to determine acceptance
                        if (random.Next(0, 2) >= 1)
                        {
                            frame.FS = 2; //accept the frame
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
                CloseRing();
                // Close all open resources (i.e. sockets!)
                sendy.Close();
                sock.Close();
            }
        }

        /// <summary>
        /// If the exit attribute is true, send out the exit frame to the ring.  If not, create and transmit a new token.
        /// </summary>
        protected void Transmit()
        {
            // Check if we have received the exit signal
            if (this.exit)// && ! this.exitSent)
            {
                Frame frame = new Frame(0, this.num, this.num, null);
                frame.FS = Monitor.exitCode;
                Send(frame); //Send out a frame signalling an exit for all nodes
                this.exitSent = true;
            }
            //If we're transmitting and not exitting, just send out another token
            else 
            {
                Token token = new Token(16, this.num, this.num);
                Send(token);
            }
        }

        /// <summary>
        /// Sets the finished status of the source of the frame to be true.
        /// Then calls CheckExit to ensure proper setting of the exit attribute.
        /// </summary>
        /// <param name="frame">Assumed to be an exit signal.  The frame whose source's finished status needs changed.</param>
        private void SetExitStatus(Frame frame)
        {
            if (!this.nodesFinished[frame.SA])
            {
                this.nodesFinished[frame.SA] = true; //Set the source's finished status to true
                Send(frame);
            }

            CheckExit(); //Check if the change has triggered an exit
        }

        /// <summary>
        /// Iterates through the finished statuses of all the nodes and determines if we should signal an overall exit.
        /// </summary>
        private void CheckExit()
        {
            this.exit = true; //we exit if no node has a false exit status

            for (int k = 0; k < this.nodeCount; k++)
            {
                //if any node isn't finished, we aren't ready to exit
                if (!this.nodesFinished[k])
                {
                    this.exit = false;
                    break;
                }
            }
        }

        /// <summary>
        /// Sets the exit signal to be true.  Will close the ring the next time the monitor receives the token.
        /// </summary>
        public void CloseRing()
        {
            this.exit = true;
        }

    }
}
