/*----------------------------------------------------------------
// Copyright (C) 2015 广州，Lucky Game
//
// 模块名：
// 创建者：D.S.Qiu
// 修改者列表：
// 创建日期：June 05 2016
// 模块描述：
//----------------------------------------------------------------*/
// ZipArchive, by Jaime Olivares
// Website: zipstorer.codeplex.com
// Version: 2.35 (March 14, 2010)

// Copy from:https://github.com/neremin/ZipStorerTest
// It also can copy from the mono implement, but it's too complex!
// DotNetZip:GZipֻ��Deflate���ĸ�ʽ��ͬ��ѹ���㷨ͨ��Deflate��Inflate���ɵ�
// ��Unity5.2.4�汾���ǲ�֧��DeflateStream google����:DeflatStream����ԭ��ʵ�ֵ�
// github.com:https://github.com/mono/mono/blob/master/mcs/class/System/System.IO.Compression/DeflateStream.cs
// http://answers.unity3d.com/questions/10620/dllnotfoundexception-monoposixhelpersystemiocompre.html
// System.IO.Compression.GZipStream not with a managed implementation, but it relies on a system installed zlib instead.
// ZlibConstant.WorkingBufferSizeDefault:���ô�һЩѹ���ʻ����󣬱Ƚϲ�����huffman����������ȫ�����ؽ���

using System.Collections.Generic;
using System.Text;
using Ionic.Zlib;
using DeflateStream = Ionic.Zlib.DeflateStream;
using SevenZip;
using UnityEngine;

namespace System.IO.Compression
{
    /// <summary>
    /// Unique class for compression/decompression file. Represents a Zip file.
    /// </summary>
    public class ZipArchive : IDisposable
    {
        /// <summary>
        /// Compression method enumeration
        /// </summary>
        public enum Compression : ushort
        {
            /// <summary>Uncompressed storage</summary> 
            Store = 0,
            /// <summary>Deflate (stadard ZIP) compression method</summary>
            Deflate = 8,
            /// <summary>LZMA (Ultra) compression method</summary>
            LZMA = 14
        }

        /// <summary>
        /// Represents an entry in Zip file directory
        /// </summary>
        public class ZipEntry
        {
            /// <summary>Compression method</summary>
            public Compression Method;
            /// <summary>Full path and filename as stored in Zip</summary>
            public string FilenameInZip;
            /// <summary>Original file size</summary>
            public uint FileSize;
            /// <summary>Compressed file size</summary>
            public uint CompressedSize;
            /// <summary>Offset of header information inside Zip storage</summary>
            public uint HeaderOffset;
            /// <summary>Offset of file inside Zip storage</summary>
            public uint FileOffset;
            /// <summary>Size of header information</summary>
            public uint HeaderSize;
            /// <summary>32-bit checksum of entire file</summary>
            public uint Crc32;
            /// <summary>Last modification time of file</summary>
            public DateTime ModifyTime;
            /// <summary>User comment for file</summary>
            public string Comment;
            /// <summary>True if UTF8 encoding for filename and comments, false if default (CP 437)</summary>
            public bool EncodeUTF8;

            /// <summary>Overriden method</summary>
            /// <returns>Filename in Zip</returns>
            public override string ToString()
            {
                return this.FilenameInZip;
            }
        }

        #region Public fields
        /// <summary>True if UTF8 encoding for filename and comments, false if default (CP 437)</summary>
        public bool EncodeUTF8 = true;
        /// <summary>Force deflate algotithm even if it inflates the stored file. Off by default.</summary>
        public bool ForceDeflating = false;
        #endregion

        #region Private fields
        // List of files to store
        public List<ZipEntry> Entries = new List<ZipEntry>();

        // Filename of storage file
        private string fileName;
        // Stream object of storage file
        private Stream zipFileStream;
        // General comment
        private string Comment = string.Empty;
        // Central dir image
        private byte[] CentralDirImage = null;
        // Existing files in zip
        private ushort ExistingFiles = 0;
        // File access for Open method
        private FileAccess Access;
        // Default filename encoder
        private static Encoding DefaultEncoding = Encoding.GetEncoding(437);
        #endregion

        /// <summary>
        /// Encapsulates a <see cref="System.IO.Stream" /> to calculate the CRC32 checksum on-the-fly as data passes through.
        /// </summary>
        /// <original>http://bit.ly/1PoBsXk</original>
        sealed class CrcStream : Stream
        {
            /// <summary>
            /// Encapsulate a <see cref="System.IO.Stream" />.
            /// </summary>
            /// <param name="stream">The stream to calculate the checksum for.</param>
            public CrcStream(Stream stream)
            {
                this.stream = stream;
                this.readCrc = new CRC();
                this.writeCrc = new CRC();
            }

            readonly Stream stream;

            readonly CRC readCrc;
            readonly CRC writeCrc;

            /// <summary>
            /// Gets the underlying stream.
            /// </summary>
            public Stream Stream
            {
                get { return this.stream; }
            }

            public override bool CanRead
            {
                get { return this.stream.CanRead; }
            }
            public override bool CanSeek
            {
                get { return this.stream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return this.stream.CanWrite; }
            }

            public override void Flush()
            {
                this.stream.Flush();
            }

            public override long Length
            {
                get { return this.stream.Length; }
            }

            public override long Position
            {
                get
                {
                    return stream.Position;
                }
                set
                {
                    stream.Position = value;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return this.stream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                this.stream.SetLength(value);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = stream.Read(buffer, offset, count);
                this.readCrc.Update(buffer, (uint)offset, (uint)count);
                return count;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                stream.Write(buffer, offset, count);
                this.writeCrc.Update(buffer, (uint)offset, (uint)count);
            }

            /// <summary>
            /// Gets the CRC checksum of the data that was read by the stream thus far.
            /// </summary>
            public uint ReadCrc
            {
                get { return readCrc.GetDigest(); }
            }

            /// <summary>
            /// Gets the CRC checksum of the data that was written to the stream thus far.
            /// </summary>
            public uint WriteCrc
            {
                get { return writeCrc.GetDigest(); }
            }

            /// <summary>
            /// Resets the read and write checksums.
            /// </summary>
            public void ResetChecksum()
            {
                readCrc.Init();
                writeCrc.Init();
            }
        }

        sealed class SegmentStream : Stream
        {
            readonly Stream stream;
            readonly long start;
            readonly long length;

            public SegmentStream(Stream stream, long length)
            {
                if (stream == null)
                    throw new ArgumentNullException();
                if (stream.Length < stream.Position + length)
                    throw new ArgumentOutOfRangeException();
                //Contract.Requires<ArgumentOutOfRangeException>(stream.Length >= stream.Position + length);
                this.stream = stream;
                this.start = stream.Position;
                this.length = length;
            }

            public override bool CanRead
            {
                get {return this.stream.CanRead;}
            } 

            public override bool CanSeek
            {
                get { return this.stream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return this.stream.CanWrite; }
            }

            public override long Length
            {
                get { return this.length; }
            }

            public override long Position
            {
                get
                {
                    return this.stream.Position - this.start;
                }
                set
                {
                    if (0 > value || value >= Length)
                        throw new ArgumentOutOfRangeException();
                    this.stream.Position = this.start + value;
                }
            }

            public override void Flush()
            {
                this.stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return this.stream.Read(buffer, offset, (int)Math.Min(count, this.length - this.Position));
            }
            
            long CalcSeekPosition(long offset, SeekOrigin origin)
            {
                var startPosition = start;
                switch (origin)
                {
                    case SeekOrigin.Current:
                        startPosition = this.stream.Position;
                        break;
                    case SeekOrigin.End:
                        startPosition = this.start + this.Length;
                        break;
                }
                return Math.Min(Math.Max(startPosition + offset, start), start + this.length);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return this.stream.Position = CalcSeekPosition(offset, origin);
            }

            public override void SetLength(long value)
            {
                if (!CanWrite)
                    throw new InvalidOperationException();
                if (0 > value || value > Length)
                    throw new ArgumentOutOfRangeException();
                this.stream.SetLength(value + this.start + 1);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.stream.Write(buffer, offset, (int)Math.Min(count, this.Length - this.Position));
            }
            
        }

        #region Public methods

        /// <summary>
        /// Method to create a new storage file
        /// </summary>
        /// <param name="fileName">Full path of Zip file to create</param>
        /// <param name="comment">General comment for Zip file</param>
        /// <returns>A valid ZipArchive object</returns>
        public static ZipArchive Create(string fileName, string comment)
        {
            Stream stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);

            ZipArchive zip = Create(stream, comment);
            zip.Comment = comment;
            zip.fileName = fileName;

            return zip;
        }
        /// <summary>
        /// Method to create a new zip storage in a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="comment"></param>
        /// <returns>A valid ZipArchive object</returns>
        public static ZipArchive Create(Stream stream, string comment)
        {
            ZipArchive zip = new ZipArchive();
            zip.Comment = comment;
            zip.zipFileStream = stream;
            zip.Access = FileAccess.Write;

            return zip;
        }
        /// <summary>
        /// Method to open an existing storage file
        /// </summary>
        /// <param name="fileName">Full path of Zip file to open</param>
        /// <param name="access">File access mode as used in FileStream constructor</param>
        /// <returns>A valid ZipArchive object</returns>
        public static ZipArchive Open(string fileName, FileAccess access)
        {
            Stream stream = new FileStream(fileName, FileMode.Open, access == FileAccess.Read ? FileAccess.Read : FileAccess.ReadWrite);

            ZipArchive zip = Open(stream, access);
            zip.fileName = fileName;
            return zip;
        }
        /// <summary>
        /// Method to open an existing storage from stream
        /// </summary>
        /// <param name="stream">Already opened stream with zip contents</param>
        /// <param name="access">File access mode for stream operations</param>
        /// <returns>A valid ZipArchive object</returns>
        public static ZipArchive Open(Stream stream, FileAccess access)
        {
            if (!stream.CanSeek && access != FileAccess.Read)
                throw new InvalidOperationException("Stream cannot seek");

            ZipArchive zip = new ZipArchive();
            //zip.fileName = fileName;
            zip.zipFileStream = stream;
            zip.Access = access;

            if (zip.ReadArchiveInfo())
                return zip;
            throw new Exception("Open the stream error!");
            //throw new System.IO.InvalidDataException();
        }
        /// <summary>
        /// Add full contents of a file into the Zip storage
        /// </summary>
        /// <param name="_method">Compression method</param>
        /// <param name="_pathname">Full path of file to add to Zip storage</param>
        /// <param name="_filenameInZip">Filename and path as desired in Zip directory</param>
        /// <param name="_comment">Comment for stored file</param>        
        public void AddFile(Compression method, string pathName, string fileNameInZip, string comment)
        {
            if (Access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not alowed");

            FileStream stream = new FileStream(pathName, FileMode.Open, FileAccess.Read);
            AddStream(method, fileNameInZip, stream, File.GetLastWriteTime(pathName), comment);
            stream.Close();
        }
        /// <summary>
        /// Add full contents of a stream into the Zip storage
        /// </summary>
        /// <param name="_method">Compression method</param>
        /// <param name="_filenameInZip">Filename and path as desired in Zip directory</param>
        /// <param name="_source">Stream object containing the data to store in Zip</param>
        /// <param name="_modTime">Modification time of the data to store</param>
        /// <param name="_comment">Comment for stored file</param>
        public void AddStream(Compression method, string fileNameInZip, Stream source, DateTime modTime, string comment)
        {
            if (Access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not alowed");
            
            // Prepare the fileinfo
            ZipEntry zfe = new ZipEntry();
            zfe.Method = method;
            zfe.EncodeUTF8 = this.EncodeUTF8;
            zfe.FilenameInZip = NormalizedFilename(fileNameInZip);
            zfe.Comment = (comment == null ? string.Empty : comment);
            // Even though we write the header now, it will have to be rewritten, since we don't know compressed size or crc.
            zfe.Crc32 = 0;  // to be updated later
            zfe.HeaderOffset = (uint)this.zipFileStream.Position;  // offset within file of the start of this local record
            zfe.ModifyTime = modTime;

            // Write local header
            WriteLocalHeader(ref zfe);
            zfe.FileOffset = (uint)this.zipFileStream.Position;

            // Write file to zip (store)
            Store(ref zfe, source);
            source.Close();

            this.UpdateCrcAndSizes(ref zfe);

            Entries.Add(zfe);
        }
        /// <summary>
        /// Updates central directory (if pertinent) and close the Zip storage
        /// </summary>
        /// <remarks>This is a required step, unless automatic dispose is used</remarks>
        public void Close()
        {
            if (this.Access != FileAccess.Read)
            {
                uint centralOffset = (uint)this.zipFileStream.Position;
                uint centralSize = 0;

                if (this.CentralDirImage != null)
                    this.zipFileStream.Write(CentralDirImage, 0, CentralDirImage.Length);

                for (int i = 0; i < Entries.Count; i++)
                {
                    long pos = this.zipFileStream.Position;
                    this.WriteEntryInfo(Entries[i]);
                    centralSize += (uint)(this.zipFileStream.Position - pos);
                }

                if (this.CentralDirImage != null)
                    this.WriteEndRecord(centralSize + (uint)CentralDirImage.Length, centralOffset);
                else
                    this.WriteEndRecord(centralSize, centralOffset);
            }

            if (this.zipFileStream != null)
            {
                this.zipFileStream.Flush();
                this.zipFileStream.Dispose();
                this.zipFileStream = null;
            }
        }

        public ZipEntry GetEntry(string fileNameInZip)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].FilenameInZip == fileNameInZip)
                    return Entries[i];
            }
            return null;
        }
        /// <summary>
        /// Read all the file records in the central directory 
        /// </summary>
        /// <returns>List of all entries in directory</returns>
        public List<ZipEntry> GetEntries()
        {
            if (this.CentralDirImage == null)
                throw new InvalidOperationException("Central directory currently does not exist");

            List<ZipEntry> result = new List<ZipEntry>();

            for (int pointer = 0; pointer < this.CentralDirImage.Length;)
            {
                uint signature = BitConverter.ToUInt32(CentralDirImage, pointer);
                if (signature != 0x02014b50)
                    break;

                bool encodeUTF8 = (BitConverter.ToUInt16(CentralDirImage, pointer + 8) & 0x0800) != 0;
                ushort method = BitConverter.ToUInt16(CentralDirImage, pointer + 10);
                uint modifyTime = BitConverter.ToUInt32(CentralDirImage, pointer + 12);
                uint crc32 = BitConverter.ToUInt32(CentralDirImage, pointer + 16);
                uint comprSize = BitConverter.ToUInt32(CentralDirImage, pointer + 20);
                uint fileSize = BitConverter.ToUInt32(CentralDirImage, pointer + 24);
                ushort filenameSize = BitConverter.ToUInt16(CentralDirImage, pointer + 28);
                ushort extraSize = BitConverter.ToUInt16(CentralDirImage, pointer + 30);
                ushort commentSize = BitConverter.ToUInt16(CentralDirImage, pointer + 32);
                uint headerOffset = BitConverter.ToUInt32(CentralDirImage, pointer + 42);
                uint headerSize = (uint)(46 + filenameSize + extraSize + commentSize);

                Encoding encoder = encodeUTF8 ? Encoding.UTF8 : DefaultEncoding;

                ZipEntry zfe = new ZipEntry();
                zfe.Method = (Compression)method;
                zfe.FilenameInZip = encoder.GetString(CentralDirImage, pointer + 46, filenameSize);
                zfe.FileOffset = GetFileOffset(headerOffset);
                zfe.FileSize = fileSize;
                zfe.CompressedSize = comprSize;
                zfe.HeaderOffset = headerOffset;
                zfe.HeaderSize = headerSize;
                zfe.Crc32 = crc32;
                zfe.ModifyTime = DosTimeToDateTime(modifyTime);
                if (commentSize > 0)
                    zfe.Comment = encoder.GetString(CentralDirImage, pointer + 46 + filenameSize + extraSize, commentSize);

                result.Add(zfe);
                pointer += (46 + filenameSize + extraSize + commentSize);
            }
            return result;
        }


        public bool ExtractFile(string fileNameInZip, string fileName)
        {
            ZipEntry entry = GetEntry(fileNameInZip);
            if (entry == null)
                return false;
            return ExtractFile(entry,fileName);
        }
        /// <summary>
        /// Copy the contents of a stored file into a physical file
        /// </summary>
        /// <param name="entry">Entry information of file to extract</param>
        /// <param name="fileName">Name of file to store uncompressed data</param>
        /// <returns>True if success, false if not.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public bool ExtractFile(ZipEntry entry, string fileName)
        {
            // Make sure the parent directory exist
            string path = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            // Check it is directory. If so, do nothing
            if (Directory.Exists(fileName))
                return true;

            using (Stream output = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                if (ExtractFile(entry, output))
                {
                    return false;
                }
            }
            File.SetCreationTime(fileName, entry.ModifyTime);
            File.SetLastWriteTime(fileName, entry.ModifyTime);

            return true;
        }
        /// <summary>
        /// Copy the contents of a stored file into an opened stream
        /// </summary>
        /// <param name="entry">Entry information of file to extract</param>
        /// <param name="_stream">Stream to store the uncompressed data</param>
        /// <returns>True if success, false if not.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public bool ExtractFile(ZipEntry entry, Stream stream)
        {
            if (!stream.CanWrite)
                throw new InvalidOperationException("Stream cannot be written");

            // check signature
            byte[] signature = new byte[4];
            this.zipFileStream.Seek(entry.HeaderOffset, SeekOrigin.Begin);
            this.zipFileStream.Read(signature, 0, 4);
            if (BitConverter.ToUInt32(signature, 0) != 0x04034b50)
                return false;

            // Prepare streams
            stream.SetLength(entry.FileSize);
            var outStream = new CrcStream(stream);
            this.zipFileStream.Seek(entry.FileOffset, SeekOrigin.Begin);

            // Select input stream for inflating or just reading
            Stream inStream = new SegmentStream(this.zipFileStream, entry.CompressedSize);
            
            if (entry.Method == Compression.Deflate)
            {
                using (var intPutStream = new Ionic.Zlib.DeflateStream(inStream, Ionic.Zlib.CompressionMode.Decompress))
                {
                    intPutStream.FlushMode = FlushType.Full;
                    int buffSize = 4096;
                    byte[] buff = new byte[buffSize];
                    int size = 0;
                    do
                    {
                        size = intPutStream.Read(buff, 0, buffSize);
                        if (size > 0)
                            outStream.Write(buff, 0, size);
                    } while (size > 0);
                }
                //inStream = new DeflateStream(inStream, CompressionMode.Decompress, true);
            }

            // Decompress
            if (entry.Method == Compression.LZMA)
            {
                var decoder = new SevenZip.Compression.LZMA.Decoder();

                const int PropsLength = 5;
                var buffer = new byte[PropsLength];

                inStream.Read(buffer, 0, sizeof(int));
                if (BitConverter.ToInt32(buffer, 0) != 0x00051409)
                {
                    throw new Exception("Invalid LZMA stream signature");
                }

                if (inStream.Read(buffer, 0, PropsLength) != PropsLength)
                {
                    throw new Exception("Invalid LZMA properties length");
                }
                decoder.SetDecoderProperties(buffer);

                decoder.Code(inStream, outStream, entry.CompressedSize, entry.FileSize, null);
            }
            else
            {
                inStream.CopyTo(outStream);
            }

            stream.Flush();

            //if (entry.Method == Compression.Deflate)
            inStream.Dispose();
            if (entry.Crc32 != outStream.WriteCrc)
            {
                throw new Exception("Uncompressed file CRC mismatch");
            }
            return true;
        }
        #endregion

        #region Private methods
        // Calculate the file offset by reading the corresponding local header
        private uint GetFileOffset(uint headerOffset)
        {
            byte[] buffer = new byte[2];

            this.zipFileStream.Seek(headerOffset + 26, SeekOrigin.Begin);
            this.zipFileStream.Read(buffer, 0, 2);
            ushort filenameSize = BitConverter.ToUInt16(buffer, 0);
            this.zipFileStream.Read(buffer, 0, 2);
            ushort extraSize = BitConverter.ToUInt16(buffer, 0);

            return (uint)(30 + filenameSize + extraSize + headerOffset);
        }
        /* Local file header:
            local file header signature     4 bytes  (0x04034b50)
            version needed to extract       2 bytes
            general purpose bit flag        2 bytes
            compression method              2 bytes
            last mod file time              2 bytes
            last mod file date              2 bytes
            crc-32                          4 bytes
            compressed size                 4 bytes
            uncompressed size               4 bytes
            filename length                 2 bytes
            extra field length              2 bytes

            filename (variable size)
            extra field (variable size)
        */
        private void WriteLocalHeader(ref ZipEntry entry)
        {
            long pos = this.zipFileStream.Position;
            Encoding encoder = entry.EncodeUTF8 ? Encoding.UTF8 : DefaultEncoding;
            byte[] encodedFilename = encoder.GetBytes(entry.FilenameInZip);

            this.zipFileStream.Write(new byte[] { 80, 75, 3, 4, 20, 0 }, 0, 6); // No extra header
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)(entry.EncodeUTF8 ? 0x0800 : 0)), 0, 2); // filename and comment encoding 
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)entry.Method), 0, 2);  // zipping method
            this.zipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(entry.ModifyTime)), 0, 4); // zipping date and time
            this.zipFileStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 12); // unused CRC, un/compressed size, updated later
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)encodedFilename.Length), 0, 2); // filename length
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // extra length

            this.zipFileStream.Write(encodedFilename, 0, encodedFilename.Length);
            entry.HeaderSize = (uint)(this.zipFileStream.Position - pos);
        }
        /* Central directory's File header:
            central file header signature   4 bytes  (0x02014b50)
            version made by                 2 bytes
            version needed to extract       2 bytes
            general purpose bit flag        2 bytes
            compression method              2 bytes
            last mod file time              2 bytes
            last mod file date              2 bytes
            crc-32                          4 bytes
            compressed size                 4 bytes
            uncompressed size               4 bytes
            filename length                 2 bytes
            extra field length              2 bytes
            file comment length             2 bytes
            disk number start               2 bytes
            internal file attributes        2 bytes
            external file attributes        4 bytes
            relative offset of local header 4 bytes

            filename (variable size)
            extra field (variable size)
            file comment (variable size)
        */
        private void WriteEntryInfo(ZipEntry entry)
        {
            Encoding encoder = entry.EncodeUTF8 ? Encoding.UTF8 : DefaultEncoding;
            byte[] encodedFilename = encoder.GetBytes(entry.FilenameInZip);
            byte[] encodedComment = encoder.GetBytes(entry.Comment);

            this.zipFileStream.Write(new byte[] { 80, 75, 1, 2, 23, 0xB, 20, 0 }, 0, 8);
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)(entry.EncodeUTF8 ? 0x0800 : 0)), 0, 2); // filename and comment encoding 
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)entry.Method), 0, 2);  // zipping method
            this.zipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(entry.ModifyTime)), 0, 4);  // zipping date and time
            this.zipFileStream.Write(BitConverter.GetBytes(entry.Crc32), 0, 4); // file CRC
            this.zipFileStream.Write(BitConverter.GetBytes(entry.CompressedSize), 0, 4); // compressed file size
            this.zipFileStream.Write(BitConverter.GetBytes(entry.FileSize), 0, 4); // uncompressed file size
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)encodedFilename.Length), 0, 2); // Filename in zip
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // extra length
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)encodedComment.Length), 0, 2);

            this.zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // disk=0
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // file type: binary
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // Internal file attributes
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)0x8100), 0, 2); // External file attributes (normal/readable)
            this.zipFileStream.Write(BitConverter.GetBytes(entry.HeaderOffset), 0, 4);  // Offset of header

            this.zipFileStream.Write(encodedFilename, 0, encodedFilename.Length);
            this.zipFileStream.Write(encodedComment, 0, encodedComment.Length);
        }
        /* End of central dir record:
            end of central dir signature    4 bytes  (0x06054b50)
            number of this disk             2 bytes
            number of the disk with the
            start of the central directory  2 bytes
            total number of entries in
            the central dir on this disk    2 bytes
            total number of entries in
            the central dir                 2 bytes
            size of the central directory   4 bytes
            offset of start of central
            directory with respect to
            the starting disk number        4 bytes
            zipfile comment length          2 bytes
            zipfile comment (variable size)
        */
        private void WriteEndRecord(uint size, uint offset)
        {
            Encoding encoder = this.EncodeUTF8 ? Encoding.UTF8 : DefaultEncoding;
            byte[] encodedComment = encoder.GetBytes(this.Comment);

            this.zipFileStream.Write(new byte[] { 80, 75, 5, 6, 0, 0, 0, 0 }, 0, 8);
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)Entries.Count + ExistingFiles), 0, 2);
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)Entries.Count + ExistingFiles), 0, 2);
            this.zipFileStream.Write(BitConverter.GetBytes(size), 0, 4);
            this.zipFileStream.Write(BitConverter.GetBytes(offset), 0, 4);
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)encodedComment.Length), 0, 2);
            this.zipFileStream.Write(encodedComment, 0, encodedComment.Length);
        }

        // Copies all source file into storage file
        private void Store(ref ZipEntry entry, Stream source)
        {
            uint totalRead = 0;
            Stream outStream;

            long posStart = this.zipFileStream.Position;
            long sourceStart = source.Position;

            outStream = this.zipFileStream;

            var inStream = new CrcStream(source);
            switch (entry.Method)
            {
                case Compression.LZMA:
                    var encoder = new SevenZip.Compression.LZMA.Encoder();

                    // LZMA "magic bytes" (7-zip requires them)
                    var magicBytes = BitConverter.GetBytes(0x00051409);
                    outStream.Write(magicBytes, 0, magicBytes.Length);

                    encoder.WriteCoderProperties(outStream);
                    
                    encoder.Code(inStream, outStream, -1, -1, null);
                    break;
                case Compression.Deflate:
                    //using (var outPutStream = new ZOutputStream(outStream, zlibConst.Z_DEFAULT_COMPRESSION))
                    var outPutStream = new Ionic.Zlib.DeflateStream(outStream, Ionic.Zlib.CompressionMode.Compress, CompressionLevel.BestCompression,true);
                    {
                        outPutStream.FlushMode = FlushType.Full;
                        int buffSize = ZlibConstants.WorkingBufferSizeDefault;
                        byte[] buff = new byte[buffSize];
                        int size = 0;
                        do
                        {
                            size = inStream.Read(buff, 0, buffSize);
                            if (size > 0)
                            {
                                outPutStream.Write(buff, 0, size);
                            }
                                
                        } while (size > 0);
                    }
                    break;
                default: //case Compression.Store:
                    inStream.CopyTo(outStream);
                    break;
            }
            entry.Crc32 = inStream.ReadCrc;
            outStream.Flush();

            entry.FileSize = (uint)(source.Position - sourceStart);
            entry.CompressedSize = (uint)(this.zipFileStream.Position - posStart);

            // Verify for real compression
            if (entry.Method != Compression.Store && !this.ForceDeflating && source.CanSeek && entry.CompressedSize > entry.FileSize)
            {
                // Start operation again with Store algorithm
                entry.Method = Compression.Store;
                this.zipFileStream.Position = posStart;
                this.zipFileStream.SetLength(posStart);
                source.Position = sourceStart;
                this.Store(ref entry, source);
            }
        }
        /* DOS Date and time:
            MS-DOS date. The date is a packed value with the following format. Bits Description 
                0-4 Day of the month (1?1) 
                5-8 Month (1 = January, 2 = February, and so on) 
                9-15 Year offset from 1980 (add 1980 to get actual year) 
            MS-DOS time. The time is a packed value with the following format. Bits Description 
                0-4 Second divided by 2 
                5-10 Minute (0?9) 
                11-15 Hour (0?3 on a 24-hour clock) 
        */
        private uint DateTimeToDosTime(DateTime _dt)
        {
            return (uint)(
                (_dt.Second / 2) | (_dt.Minute << 5) | (_dt.Hour << 11) |
                (_dt.Day << 16) | (_dt.Month << 21) | ((_dt.Year - 1980) << 25));
        }
        private DateTime DosTimeToDateTime(uint _dt)
        {
            return new DateTime(
                (int)(_dt >> 25) + 1980,
                (int)(_dt >> 21) & 15,
                (int)(_dt >> 16) & 31,
                (int)(_dt >> 11) & 31,
                (int)(_dt >> 5) & 63,
                (int)(_dt & 31) * 2);
        }

        /* CRC32 algorithm
          The 'magic number' for the CRC is 0xdebb20e3.  
          The proper CRC pre and post conditioning
          is used, meaning that the CRC register is
          pre-conditioned with all ones (a starting value
          of 0xffffffff) and the value is post-conditioned by
          taking the one's complement of the CRC residual.
          If bit 3 of the general purpose flag is set, this
          field is set to zero in the local header and the correct
          value is put in the data descriptor and in the central
          directory.
        */
        private void UpdateCrcAndSizes(ref ZipEntry entry)
        {
            long lastPos = this.zipFileStream.Position;  // remember position

            this.zipFileStream.Position = entry.HeaderOffset + 8;
            this.zipFileStream.Write(BitConverter.GetBytes((ushort)entry.Method), 0, 2);  // zipping method

            this.zipFileStream.Position = entry.HeaderOffset + 14;
            this.zipFileStream.Write(BitConverter.GetBytes(entry.Crc32), 0, 4);  // Update CRC
            this.zipFileStream.Write(BitConverter.GetBytes(entry.CompressedSize), 0, 4);  // Compressed size
            this.zipFileStream.Write(BitConverter.GetBytes(entry.FileSize), 0, 4);  // Uncompressed size
            this.zipFileStream.Position = lastPos;  // restore position
        }
        // Replaces backslashes with slashes to store in zip header
        private string NormalizedFilename(string _filename)
        {
            string filename = _filename.Replace('\\', '/');

            int pos = filename.IndexOf(':');
            if (pos >= 0)
                filename = filename.Remove(0, pos + 1);

            return filename.Trim('/');
        }

        // Reads the end-of-central-directory record
        private bool ReadArchiveInfo()
        {
            if (this.zipFileStream.Length < 22)
                return false;

            try
            {
                this.zipFileStream.Seek(-17, SeekOrigin.End);
                BinaryReader br = new BinaryReader(this.zipFileStream);
                do
                {
                    this.zipFileStream.Seek(-5, SeekOrigin.Current);
                    UInt32 sig = br.ReadUInt32();
                    if (sig == 0x06054b50)
                    {
                        this.zipFileStream.Seek(6, SeekOrigin.Current);

                        UInt16 entries = br.ReadUInt16();
                        Int32 centralSize = br.ReadInt32();
                        UInt32 centralDirOffset = br.ReadUInt32();
                        UInt16 commentSize = br.ReadUInt16();

                        // check if comment field is the very last data in file
                        if (this.zipFileStream.Position + commentSize != this.zipFileStream.Length)
                            return false;

                        // Copy entire central directory to a memory buffer
                        this.ExistingFiles = entries;
                        this.CentralDirImage = new byte[centralSize];
                        this.zipFileStream.Seek(centralDirOffset, SeekOrigin.Begin);
                        this.zipFileStream.Read(this.CentralDirImage, 0, centralSize);

                        // Leave the pointer at the begining of central dir, to append new files
                        this.zipFileStream.Seek(centralDirOffset, SeekOrigin.Begin);
                        return true;
                    }
                } while (this.zipFileStream.Position > 0);
            }
            catch { }

            return false;
        }
        #endregion

        #region IDisposable Members
        /// <summary>
        /// Closes the Zip file stream
        /// </summary>
        public void Dispose()
        {
            this.Close();
        }
        #endregion
    }
}