﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSCore.Codecs.AIFF;
using CSCore.Codecs.FLAC;
using CSCore.Codecs.WAV;
using CSCore.Codecs.OGG;

namespace CSCore.Codecs
{
    /// <summary>
    ///     Helps to choose the right decoder for different codecs.
    /// </summary>
    public class CodecFactory
    {
        // ReSharper disable once InconsistentNaming
        private static readonly CodecFactory _instance = new CodecFactory();

        private readonly Dictionary<object, CodecFactoryEntry> _codecs;

        private CodecFactory()
        {
            _codecs = new Dictionary<object, CodecFactoryEntry>();
            /*Register("mp3", new CodecFactoryEntry(s =>
            {
                try
                {
                    return new DmoMp3Decoder(s);
                }
                catch (Exception)
                {
                    if (Mp3MediafoundationDecoder.IsSupported)
                        return new Mp3MediafoundationDecoder(s);
                    throw;
                }
            },
                "mp3", "mpeg3"));*/
            Register("wave", new CodecFactoryEntry(s =>
            {
                IWaveSource res = new WaveFileReader(s);
                if (res.WaveFormat.WaveFormatTag != AudioEncoding.Pcm &&
                    res.WaveFormat.WaveFormatTag != AudioEncoding.IeeeFloat &&
                    res.WaveFormat.WaveFormatTag != AudioEncoding.Extensible)
                {
                    res.Dispose();
                    res = null;
                }
                return res;
            },
                "wav", "wave"));
            Register("flac", new CodecFactoryEntry(s => new FlacFile(s),
                "flac", "fla"));
            Register("aiff", new CodecFactoryEntry(s => new AiffReader(s),
                "aiff", "aif", "aifc"));
            Register("ogg-vorbis", new CodecFactoryEntry(s => new NVorbisSource(s).ToWaveSource(),
               "ogg"));
            /*Register("mp3", new CodecFactoryEntry(s => new MP3Source(s).ToWaveSource(),
               "mp3", "mpeg3"));*/

            /*if (AacDecoder.IsSupported)
            {
                Register("aac", new CodecFactoryEntry(s => new AacDecoder(s),
                    "aac", "adt", "adts", "m2ts", "mp2", "3g2", "3gp2", "3gp", "3gpp", "m4a", "m4v", "mp4v", "mp4",
                    "mov"));
            }

            if (WmaDecoder.IsSupported)
            {
                Register("wma", new CodecFactoryEntry(s => new WmaDecoder(s),
                    "asf", "wm", "wmv", "wma"));
            }

            if (Mp1Decoder.IsSupported)
            {
                Register("mp1", new CodecFactoryEntry(s => new Mp1Decoder(s),
                    "mp1", "m2ts"));
            }

            if (Mp2Decoder.IsSupported)
            {
                Register("mp2", new CodecFactoryEntry(s => new Mp2Decoder(s),
                    "mp2", "m2ts"));
            }

            if (DDPDecoder.IsSupported)
            {
                Register("ddp", new CodecFactoryEntry(s => new DDPDecoder(s),
                    "mp2", "m2ts", "m4a", "m4v", "mp4v", "mp4", "mov", "asf", "wm", "wmv", "wma", "avi", "ac3", "ec3"));
            }*/
        }

        /// <summary>
        ///     Gets the default singleton instance of the <see cref="CodecFactory" /> class.
        /// </summary>
        public static CodecFactory Instance
        {
            get { return _instance; }
        }

        /// <summary>
        ///     Gets the file filter in English. This filter can be used e.g. in combination with an OpenFileDialog.
        /// </summary>
        public static string SupportedFilesFilterEn
        {
            get { return Instance.GenerateFilter(); }
        }

        /// <summary>
        ///     Registers a new codec.
        /// </summary>
        /// <param name="key">
        ///     The key which gets used internally to save the <paramref name="codec" /> in a
        ///     <see cref="Dictionary{TKey,TValue}" />. This is typically the associated file extension. For example: the mp3 codec
        ///     uses the string "mp3" as its key.
        /// </param>
        /// <param name="codec"><see cref="CodecFactoryEntry" /> which provides information about the codec.</param>
        public void Register(object key, CodecFactoryEntry codec)
        {
            var keyString = key as string;
            if (keyString != null)
                key = keyString.ToLower();

            if (_codecs.ContainsKey(key) != true)
                _codecs.Add(key, codec);
        }

        /// <summary>
        ///     Returns a fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified file. If the
        ///     specified file can not be decoded, this method throws an <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="filename">Filename of the specified file.</param>
        /// <param name="extension">Extension hint specifying the format of the file, in case it cannot be extracted from the filename.</param>
        /// <returns>Fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified file.</returns>
        /// <exception cref="NotSupportedException">The codec of the specified file is not supported.</exception>
        public IWaveSource GetCodec(string filename, string extension = null)
        {
            if (String.IsNullOrEmpty(filename))
                throw new ArgumentNullException("filename");

            if (!File.Exists(filename))
                throw new FileNotFoundException("File not found.", filename);

            if (extension == null)
                extension = Path.GetExtension(filename).Replace(".", ""); //get the extension without the "dot".

            var stream = File.OpenRead(filename);

            try
            {
                return GetCodec(stream, extension);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        ///     Returns a fully initialized <see cref="IWaveSource" /> instance which is able to decode the stream based on the extension hint. If the
        ///     specified stream can not be decoded, this method throws an <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="stream">Stream to decode.</param>
        /// <param name="extension">Extension hint specifying the format of the stream.</param>
        /// <returns>Fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified stream.</returns>
        /// <exception cref="NotSupportedException">The codec of the specified extension is not supported.</exception>
        public IWaveSource GetCodec(Stream stream, string extension)
        {
            StringBuilder str = null;

            //remove the dot in front of the file extension.
            foreach (var codecEntry in _codecs)
            {
                try
                {
                    if (codecEntry.Value.FileExtensions.Any(x => x.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                        return codecEntry.Value.GetCodecAction(stream);
                }
                catch (Exception ex)
                {
                    if (str == null)
                        str = new StringBuilder();

                    str.AppendLine(ex.ToString());

                    Debug.WriteLine(ex.ToString());
                }
            }

            foreach (var codecEntry in _codecs)
            {
                str.AppendLine("Codec: " + codecEntry.Key);
                foreach (var ext in codecEntry.Value.FileExtensions)
                    str.AppendLine("Extension: " + ext);
            }

            throw new NotSupportedException($"Didn't find suitable codec for {extension}. Errors:\n" + str.ToString());
        }

        /// <summary>
        ///     Returns a fully initialized <see cref="IWaveSource" /> instance which is able to decode the audio source behind the
        ///     specified <paramref name="uri" />.
        ///     If the specified audio source can not be decoded, this method throws an <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="uri">Uri which points to an audio source.</param>
        /// <returns>Fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified audio source.</returns>
        /// <exception cref="NotSupportedException">The codec of the specified audio source is not supported.</exception>
        public IWaveSource GetCodec(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            try
            {
                var filename = TryFindFilename(uri);
                if (!String.IsNullOrEmpty(filename))
                    return GetCodec(filename);

                return null;
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new NotSupportedException("Codec not supported.", e);
            }
        }

        internal IWaveSource GetCodec(Stream stream, object key)
        {
            return _codecs[key].GetCodecAction(stream);
        }

        /// <summary>
        ///     Returns all the common file extensions of all supported codecs. Note that some of these file extensions belong to
        ///     more than one codec.
        ///     That means that it can be possible that some files with the file extension abc can be decoded but other a few files
        ///     with the file extension abc can't be decoded.
        /// </summary>
        /// <returns>Supported file extensions.</returns>
        public string[] GetSupportedFileExtensions()
        {
            var extensions = new List<string>();
            foreach (CodecFactoryEntry item in _codecs.Select(x => x.Value))
            {
                foreach (string e in item.FileExtensions)
                {
                    if (!extensions.Contains(e))
                        extensions.Add(e);
                }
            }
            return extensions.ToArray();
        }

        private string GenerateFilter()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Supported Files|");
            stringBuilder.Append(String.Concat(GetSupportedFileExtensions().Select(x => "*." + x + ";").ToArray()));
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            return stringBuilder.ToString();
        }

        private string TryFindFilename(Uri uri)
        {
            if (File.Exists(uri.LocalPath))
                return uri.LocalPath;

            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(uri.LocalPath);
                if (fileInfo.Exists)
                    return fileInfo.FullName;
            }
            catch (Exception)
            {
                Debug.WriteLine(String.Format("{0} not found.", uri.LocalPath));
            }

            try
            {
                fileInfo = new FileInfo(uri.OriginalString);
                if (fileInfo.Exists)
                    return fileInfo.FullName;
            }
            catch (Exception)
            {
                Debug.WriteLine(String.Format("{0} not found.", uri.OriginalString));
            }

            var path = Win32.NativeMethods.PathCreateFromUrl(uri.OriginalString);
            if (path == null || !File.Exists(path))
            {
                path = Win32.NativeMethods.PathCreateFromUrl(uri.AbsoluteUri);
            }
            if (path != null && File.Exists(path))
            {
                return path;
            }

            return null;
        }

        private class DisposeFileStreamSource : WaveAggregatorBase
        {
            private Stream _stream;

            public DisposeFileStreamSource(IWaveSource source, Stream stream)
                : base(source)
            {
                _stream = stream;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (_stream != null)
                {
                    try
                    {
                        _stream.Dispose();
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("Stream was already disposed.");
                    }
                    finally
                    {
                        _stream = null;
                    }
                }
            }
        }
    }
}