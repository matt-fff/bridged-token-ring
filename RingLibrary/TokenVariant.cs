using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RingLibrary
{
    /// <summary>
    /// Stores the token class - a special case of the binary Frame
    /// </summary>
    public class TokenVariant : FrameVariant
    {
        public static int THT = 1500; //The universal token holding time

        /// <summary>
        /// Creates a token, setting its attributes to their corresponding parameters, 
        /// setting data to an empty byte array, and setting FC to 0
        /// </summary>
        /// <param name="AC">Access control byte</param>
        /// <param name="DA">Destination address byte</param>
        /// <param name="SA">Source address byte</param>
        public TokenVariant(byte AC, ushort DA, ushort SA)
            : base(AC, DA, SA, new byte[0])
        {
            this.FC = 0;
        }
    }
}
