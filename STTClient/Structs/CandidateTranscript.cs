using System;
using System.Runtime.InteropServices;

namespace STTClient.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CandidateTranscript
    {
        /// <summary>
        ///     Native list of tokens.
        /// </summary>
        internal IntPtr tokens;

        /// <summary>
        ///     Count of tokens from the native side.
        /// </summary>
        internal int num_tokens;

        /// <summary>
        ///     Approximated confidence value for this transcription.
        /// </summary>
        internal double confidence;
    }
}