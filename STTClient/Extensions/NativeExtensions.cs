using System;
using System.Runtime.InteropServices;
using System.Text;
using STTClient.Models;

namespace STTClient.Extensions
{
    internal static class NativeExtensions
    {
        /// <summary>
        ///     Converts native pointer to UTF-8 encoded string.
        /// </summary>
        /// <param name="intPtr">Native pointer.</param>
        /// <param name="releasePtr">Optional parameter to release the native pointer.</param>
        /// <returns>Result string.</returns>
        internal static string PtrToString(this IntPtr intPtr, bool releasePtr = true)
        {
            var len = 0;
            while (Marshal.ReadByte(intPtr, len) != 0) ++len;
            var buffer = new byte[len];
            Marshal.Copy(intPtr, buffer, 0, buffer.Length);
            if (releasePtr)
                NativeImp.STT_FreeString(intPtr);
            var result = Encoding.UTF8.GetString(buffer);
            return result;
        }

        /// <summary>
        ///     Converts a pointer into managed TokenMetadata object.
        /// </summary>
        /// <param name="intPtr">Native pointer.</param>
        /// <returns>TokenMetadata managed object.</returns>
        private static TokenMetadata PtrToTokenMetadata(this IntPtr intPtr)
        {
            var token = Marshal.PtrToStructure<Structs.TokenMetadata>(intPtr);
            var managedToken = new TokenMetadata
            {
                Timestep = token.timestep,
                StartTime = token.start_time,
                Text = token.text.PtrToString(false)
            };
            return managedToken;
        }

        /// <summary>
        ///     Converts a pointer into managed CandidateTranscript object.
        /// </summary>
        /// <param name="intPtr">Native pointer.</param>
        /// <returns>CandidateTranscript managed object.</returns>
        private static CandidateTranscript PtrToCandidateTranscript(this IntPtr intPtr)
        {
            var managedTranscript = new CandidateTranscript();
            var transcript = Marshal.PtrToStructure<Structs.CandidateTranscript>(intPtr);

            managedTranscript.Tokens = new TokenMetadata[transcript.num_tokens];
            managedTranscript.Confidence = transcript.confidence;

            //we need to manually read each item from the native ptr using its size
            var sizeOfTokenMetadata = Marshal.SizeOf<Structs.TokenMetadata>();
            for (var i = 0; i < transcript.num_tokens; i++)
            {
                managedTranscript.Tokens[i] = transcript.tokens.PtrToTokenMetadata();
                transcript.tokens += sizeOfTokenMetadata;
            }

            return managedTranscript;
        }

        /// <summary>
        ///     Converts a pointer into managed Metadata object.
        /// </summary>
        /// <param name="intPtr">Native pointer.</param>
        /// <returns>Metadata managed object.</returns>
        internal static Metadata PtrToMetadata(this IntPtr intPtr)
        {
            var managedMetadata = new Metadata();
            var metadata = Marshal.PtrToStructure<Structs.Metadata>(intPtr);

            managedMetadata.Transcripts = new CandidateTranscript[metadata.num_transcripts];

            //we need to manually read each item from the native ptr using its size
            var sizeOfCandidateTranscript = Marshal.SizeOf<Structs.CandidateTranscript>();
            for (var i = 0; i < metadata.num_transcripts; i++)
            {
                managedMetadata.Transcripts[i] = metadata.transcripts.PtrToCandidateTranscript();
                metadata.transcripts += sizeOfCandidateTranscript;
            }

            NativeImp.STT_FreeMetadata(intPtr);
            return managedMetadata;
        }
    }
}