using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RingLibrary
{
    public class FrameVariant : Frame
    {

        /// <summary>
        /// Sets all attributes to 0 and the data array to an empty byte array.
        /// </summary>
        public FrameVariant()
            : base()
        {
            this.timer = new Stopwatch();
        }

        /// <summary>
        /// Sets the attributes to their corresponding parameters.  Sets the FC byte to 1, the FS byte to 0, and size to the size of data
        /// </summary>
        /// <param name="AC">Access control byte</param>
        /// <param name="DA">Destination address byte</param>
        /// <param name="SA">Source address byte</param>
        /// <param name="data">The data (meat) to be transmitted</param>
        public FrameVariant(byte AC, ushort DA, ushort SA, byte[] data)
            : base(AC, DA, SA, data)
        {
            this.timer = new Stopwatch();
        }

        /// <summary>
        /// Returns a FrameVariant given a Frame.  Sets the FC byte to 1, the FS byte to 0, and size to the size of data
        /// </summary>
        /// <param name="original">source Frame</param>
        public FrameVariant(Frame original)
            : base(original.AC, original.DA, original.SA, original.data)
        {
            this.timer = new Stopwatch();
        }

        /// <summary>
        /// Constructs a frame from a byte array
        /// </summary>
        /// <param name="rep">The byte array to be converted into a frame</param>
        /// <returns>Returns a frame representation of rep</returns>
        new public static FrameVariant MakeFrame(byte[] rep)
        {
            // Process binary representation into
            // frame object (or token object if it's
            // a token.

            FrameVariant frame = new FrameVariant();
            frame.timer = new Stopwatch();

            //Check that there is an acceptable number of bytes
            if (rep.Length < Frame.MinBytes)
            {
                Console.WriteLine("Size of binary frame representation is not large enough.");
            }
            else
            {
                frame.AC = rep[0];
                frame.SA = BitConverter.ToUInt16(rep, 1);
                frame.DA = BitConverter.ToUInt16(rep, 3);
                //Check if it's a token
                if (rep[1] == 0)
                {
                    frame = new TokenVariant(frame.AC, frame.DA, frame.SA);
                }
                else
                {
                    frame.FC = rep[5];
                    frame.size = rep[6];
                    frame.data = new byte[frame.size];

                    //Reconstruct the data
                    for (int k = 0; k < frame.size; k++)
                    {
                        frame.data[k] = rep[k + Frame.MinBytes - 1];
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
        new public byte[] ToBinary()
        {
            MemoryStream ms = new MemoryStream(260);

            // use ms.WriteByte and ms.Write to put the
            // data into binary format.
            ms.WriteByte(AC);

            byte[] SABytes = BitConverter.GetBytes(SA);

            if (SABytes != null)
            {
                //Write out the data
                foreach (byte k in SABytes)
                {
                    ms.WriteByte(k);
                }
            }

            byte[] DABytes = BitConverter.GetBytes(DA);

            if (DABytes != null)
            {
                //Write out the data
                foreach (byte k in DABytes)
                {
                    ms.WriteByte(k);
                }
            }

            ms.WriteByte(FC);

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
        new public bool Timeout()
        {
            return this.timer.ElapsedMilliseconds > FrameVariant.TimeoutLength;
        }
    }
}
