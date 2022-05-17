﻿using System;
using System.IO;
using STTClient.Models;

namespace STTClient.Interfaces
{
    /// <summary>
    ///     Client interface for Coqui STT
    /// </summary>
    public interface ISTT : IDisposable
    {
        /// <summary>
        ///     Return version of this library. The returned version is a semantic version
        ///     (SemVer 2.0.0).
        /// </summary>
        string Version();

        /// <summary>
        ///     Return the sample rate expected by the model.
        /// </summary>
        /// <returns>Sample rate.</returns>
        int GetModelSampleRate();

        /// <summary>
        ///     Get beam width value used by the model. If SetModelBeamWidth was not
        ///     called before, will return the default value loaded from the model
        ///     file.
        /// </summary>
        /// <returns>Beam width value used by the model.</returns>
        uint GetModelBeamWidth();

        /// <summary>
        ///     Set beam width value used by the model.
        /// </summary>
        /// <param name="aBeamWidth">
        ///     The beam width used by the decoder. A larger beam width value generates better results at the
        ///     cost of decoding time.
        /// </param>
        /// <exception cref="ArgumentException">Thrown on failure.</exception>
        void SetModelBeamWidth(uint aBeamWidth);

        /// <summary>
        ///     Enable decoding using an external scorer.
        /// </summary>
        /// <param name="aScorerPath">The path to the external scorer file.</param>
        /// <exception cref="ArgumentException">Thrown when the native binary failed to enable decoding with an external scorer.</exception>
        /// <exception cref="FileNotFoundException">Thrown when cannot find the scorer file.</exception>
        void EnableExternalScorer(string aScorerPath);

        /// <summary>
        ///     Add a hot-word.
        /// </summary>
        /// <param name="aWord">Some word</param>
        /// <param name="aBoost">Some boost</param>
        /// <exception cref="ArgumentException">Thrown on failure.</exception>
        void AddHotWord(string aWord, float aBoost);

        /// <summary>
        ///     Erase entry for a hot-word.
        /// </summary>
        /// <param name="aWord">Some word</param>
        /// <exception cref="ArgumentException">Thrown on failure.</exception>
        void EraseHotWord(string aWord);

        /// <summary>
        ///     Clear all hot-words.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown on failure.</exception>
        void ClearHotWords();

        /// <summary>
        ///     Disable decoding using an external scorer.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when an external scorer is not enabled.</exception>
        void DisableExternalScorer();

        /// <summary>
        ///     Set hyperparameters alpha and beta of the external scorer.
        /// </summary>
        /// <param name="aAlpha">The alpha hyperparameter of the decoder. Language model weight.</param>
        /// <param name="aBeta">The beta hyperparameter of the decoder. Word insertion weight.</param>
        /// <exception cref="ArgumentException">Thrown when an external scorer is not enabled.</exception>
        void SetScorerAlphaBeta(float aAlpha, float aBeta);

        /// <summary>
        ///     Use the STT model to perform Speech-To-Text.
        /// </summary>
        /// <param name="aBuffer">
        ///     A 16-bit, mono raw audio signal at the appropriate sample rate (matching what the model was
        ///     trained on).
        /// </param>
        /// <param name="aBufferSize">The number of samples in the audio signal.</param>
        /// <returns>The STT result. Returns NULL on error.</returns>
        string SpeechToText(short[] aBuffer,
            uint aBufferSize);

        /// <summary>
        ///     Use the STT model to perform Speech-To-Text, return results including metadata.
        /// </summary>
        /// <param name="aBuffer">
        ///     A 16-bit, mono raw audio signal at the appropriate sample rate (matching what the model was
        ///     trained on).
        /// </param>
        /// <param name="aBufferSize">The number of samples in the audio signal.</param>
        /// <param name="aNumResults">Maximum number of candidate transcripts to return. Returned list might be smaller than this.</param>
        /// <returns>The extended metadata. Returns NULL on error.</returns>
        Metadata SpeechToTextWithMetadata(short[] aBuffer,
            uint aBufferSize,
            uint aNumResults);

        /// <summary>
        ///     Destroy a streaming state without decoding the computed logits.
        ///     This can be used if you no longer need the result of an ongoing streaming
        ///     inference and don't want to perform a costly decode operation.
        /// </summary>
        void FreeStream(STTStream stream);

        /// <summary>
        ///     Creates a new streaming inference state.
        /// </summary>
        STTStream CreateStream();

        /// <summary>
        ///     Feeds audio samples to an ongoing streaming inference.
        /// </summary>
        /// <param name="stream">Instance of the stream to feed the data.</param>
        /// <param name="aBuffer">
        ///     An array of 16-bit, mono raw audio samples at the appropriate sample rate (matching what the
        ///     model was trained on).
        /// </param>
        void FeedAudioContent(STTStream stream, short[] aBuffer, uint aBufferSize);

        /// <summary>
        ///     Computes the intermediate decoding of an ongoing streaming inference.
        /// </summary>
        /// <param name="stream">Instance of the stream to decode.</param>
        /// <returns>The STT intermediate result.</returns>
        string IntermediateDecode(STTStream stream);

        /// <summary>
        ///     Computes the intermediate decoding of an ongoing streaming inference, including metadata.
        /// </summary>
        /// <param name="stream">Instance of the stream to decode.</param>
        /// <param name="aNumResults">Maximum number of candidate transcripts to return. Returned list might be smaller than this.</param>
        /// <returns>The extended metadata result.</returns>
        Metadata IntermediateDecodeWithMetadata(STTStream stream, uint aNumResults);

        /// <summary>
        ///     Closes the ongoing streaming inference, returns the STT result over the whole audio signal.
        /// </summary>
        /// <param name="stream">Instance of the stream to finish.</param>
        /// <returns>The STT result.</returns>
        string FinishStream(STTStream stream);

        /// <summary>
        ///     Closes the ongoing streaming inference, returns the STT result over the whole audio signal, including metadata.
        /// </summary>
        /// <param name="stream">Instance of the stream to finish.</param>
        /// <param name="aNumResults">Maximum number of candidate transcripts to return. Returned list might be smaller than this.</param>
        /// <returns>The extended metadata result.</returns>
        Metadata FinishStreamWithMetadata(STTStream stream, uint aNumResults);
    }
}