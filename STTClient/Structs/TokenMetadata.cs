using System;
using System.Runtime.InteropServices;

namespace STTClient.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TokenMetadata
    {
        /// <summary>
        ///     Native text.
        /// </summary>
        internal IntPtr text;

        /// <summary>
        ///     Position of the character in units of 20ms.
        /// </summary>
        internal int timestep;

        /// <summary>
        ///     Position of the character in seconds.
        /// </summary>
        internal float start_time;
    }
}