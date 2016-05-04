using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace RingLibrary
{
    /// <summary>
    /// Binary frame infrastructure for use by the node transmissions in the token ring.
    /// </summary>
    public class Frame : IEquatable<Frame>
    {
        public static int MaxSize = 254;
        public static int MinBytes = 8;
        public static uint TimeoutLength = 15000;
        public ushort DA, SA;
        public byte AC, FC, size, FS;
        public byte[] data;
        public Stopwatch timer;


        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            Frame frame = obj as Frame;
            if (frame == null) return false;
            else return Equals(frame);
        }

        public bool Equals(Frame frame)
        {
            bool retval = (this.DA == frame.DA 
                        && this.SA == frame.SA 
                        && this.size == frame.size);

            if (retval)
            {
                for (int k = 0; this.data != null && k < this.data.Length; k++)
                {
                    if (this.data[k] == frame.data[k])
                    {
                        retval = false;
                        break;
                    }
                }
            }

            return retval;
        }



        /// <summary>
        /// Sets all attributes to 0 and the data array to an empty byte array.
        /// </summary>
        public Frame()
        {
            this.AC = this.FC = this.size = this.FS = 0;
            this.DA = this.SA = 0;
            this.data = new byte[0];
            this.timer = new Stopwatch();
        }

        /// <summary>
        /// Sets the attributes to their corresponding parameters.  Sets the FC byte to 1, the FS byte to 0, and size to the size of data
        /// </summary>
        /// <param name="AC">Access control byte</param>
        /// <param name="DA">Destination address byte</param>
        /// <param name="SA">Source address byte</param>
        /// <param name="data">The data (meat) to be transmitted</param>
        public Frame(byte AC, ushort DA, ushort SA, byte[] data)
        {
            this.AC = AC;
            this.DA = DA;
            this.SA = SA;
            this.data = data;
            this.timer = new Stopwatch();

            //Ensure that the data isn't too large in size
            if (data != null && data.Length > MaxSize)
            {
                Console.WriteLine("The size of the data exceeds the maximum allowable value of 254 bytes.");
                this.size = byte.MaxValue;
            }
            else if (data != null)
            {
                this.size = (byte)data.Length;
            }
            else
            {
                this.size = 0;
            }

            this.FC = 1; //set Frame Control bit to be a Frame by default
            this.FS = 0; //initially set as 0
        }

        /// <summary>
        /// Returns a Frame given a FrameVariant.  Sets the FC byte to 1, the FS byte to 0, and size to the size of data
        /// </summary>
        /// <param name="original">source FrameVariant</param>
        public Frame(FrameVariant original)
        {
            this.AC = original.AC;
            this.DA = original.DA;
            this.SA = original.SA;
            this.data = original.data;
            this.timer = new Stopwatch();

            //Ensure that the data isn't too large in size
            if (data != null && data.Length > MaxSize)
            {
                Console.WriteLine("The size of the data exceeds the maximum allowable value of 254 bytes.");
                this.size = byte.MaxValue;
            }
            else if (data != null)
            {
                this.size = (byte)data.Length;
            }
            else
            {
                this.size = 0;
            }

            this.FC = 1; //set Frame Control bit to be a Frame by default
            this.FS = 0; //initially set as 0
        }

        /// <summary>
        /// Constructs a frame from a byte array
        /// </summary>
        /// <param name="rep">The byte array to be converted into a frame</param>
        /// <returns>Returns a frame representation of rep</returns>
        public static Frame MakeFrame(byte[] rep)
        {
            // Process binary representation into
            // frame object (or token object if it's
            // a token.

            Frame frame = new Frame();
            frame.timer = new Stopwatch();

            //Check that there is an acceptable number of bytes
            if (rep.Length < Frame.MinBytes)
            {
                Console.WriteLine("Size of binary frame representation is not large enough.");
            }
            else
            {
                frame.AC = rep[0];
                frame.DA = BitConverter.ToUInt16(rep, 2);
                frame.SA = BitConverter.ToUInt16(rep, 4);
                //Check if it's a token
                if (rep[1] == 0)
                {
                    frame = new Token(frame.AC, frame.DA, frame.SA);
                }
                else
                {
                    frame.FC = rep[1];
                    frame.size = rep[6];
                    frame.data = new byte[frame.size];

                    //Reconstruct the data
                    for (int k = 0; k < frame.size; k++)
                    {
                        frame.data[k] = rep[k + 7];
                    }

                    frame.FS = rep[rep.Length - 1];
                }
            }

            return frame;
        }

        /// <summary>
        /// Converts the frame (this) to a byte array representation
        /// </summary>
        /// <returns>Returns a byte array representation of the frame</returns>
        public byte[] ToBinary()
        {
            MemoryStream ms = new MemoryStream(260);

            // use ms.WriteByte and ms.Write to put the
            // data into binary format.
            ms.WriteByte(AC);
            ms.WriteByte(FC);

            byte[] DABytes = BitConverter.GetBytes(DA);

            if (DABytes != null)
            {
                //Write out the data
                foreach (byte k in DABytes)
                {
                    ms.WriteByte(k);
                }
            }

            byte[] SABytes = BitConverter.GetBytes(SA);

            if (SABytes != null)
            {
                //Write out the data
                foreach (byte k in SABytes)
                {
                    ms.WriteByte(k);
                }
            }

            ms.WriteByte(size);

            if (data != null)
            {
                //Write out the data
                foreach (byte k in data)
                {
                    ms.WriteByte(k);
                }
            }

            ms.WriteByte(FS);

            byte[] output = ms.ToArray();
            return output;
        }

        /// <summary>
        /// Returns true if the time elapsed in the timer means a timeout
        /// </summary>
        /// <returns>true if we've timed out, false otherwise</returns>
        public bool Timeout()
        {
            return this.timer.ElapsedMilliseconds > Frame.TimeoutLength;
        }
    }
}
