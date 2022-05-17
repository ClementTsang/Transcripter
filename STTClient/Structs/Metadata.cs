using System;
using System.Runtime.InteropServices;

namespace STTClient.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Metadata
    {
        /// <summary>
        ///     Native list of candidate transcripts.
        /// </summary>
        internal IntPtr transcripts;

        /// <summary>
        ///     Count of transcripts from the native side.
        /// </summary>
        internal int num_transcripts;
    }
}