// ZipStorer, by Jaime Olivares
// Website: zipstorer.codeplex.com
// Version: 2.35 (March 14, 2010)

// Copy from:https://github.com/neremin/ZipStorerTest
// It also can copy from the mono implement, but it's too complex!
// DotNetZip:GZip只是Deflate流的格式不同，压缩算法通过Deflate和Inflate完成的
// 在Unity5.2.4版本还是不支持DeflateStream google发现:DeflatStream不是原生实现的
// github.com:https://github.com/mono/mono/blob/master/mcs/class/System/System.IO.Compression/DeflateStream.cs
// http://answers.unity3d.com/questions/10620/dllnotfoundexception-monoposixhelpersystemiocompre.html
// System.IO.Compression.GZipStream not with a managed implementation, but it relies on a system installed zlib instead.
// ZlibConstant.WorkingBufferSizeDefault:设置大一些压缩率会增大，比较不是像huffman那样把数据全部加载进来

using System.Collections.Generic;
using System.Text;
using Ionic.Zlib;
using DeflateStream = Ionic.Zlib.DeflateStream;
using SevenZip;
using Utility;

namespace System.IO.Compression
{
    /// <summary>
    /// Unique class for compression/decompression file. Represents a Zip file.
    /// </summary>
    public class ZipStorer : IDisposable
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
        public struct ZipFileEntry
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
        private List<ZipFileEntry> Files = new List<ZipFileEntry>();
        // Filename of storage file
        private string FileName;
        // Stream object of storage file
        private Stream ZipFileStream;
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
                //Contract.Requires<InvalidOperationException>(CanWrite);
                //Contract.Requires<ArgumentOutOfRangeException>(0 <= value && value <= Length);
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
        /// <param name="_filename">Full path of Zip file to create</param>
        /// <param name="_comment">General comment for Zip file</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Create(string _filename, string _comment)
        {
            Stream stream = new FileStream(_filename, FileMode.Create, FileAccess.ReadWrite);

            ZipStorer zip = Create(stream, _comment);
            zip.Comment = _comment;
            zip.FileName = _filename;

            return zip;
        }
        /// <summary>
        /// Method to create a new zip storage in a stream
        /// </summary>
        /// <param name="_stream"></param>
        /// <param name="_comment"></param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Create(Stream _stream, string _comment)
        {
            ZipStorer zip = new ZipStorer();
            zip.Comment = _comment;
            zip.ZipFileStream = _stream;
            zip.Access = FileAccess.Write;

            return zip;
        }
        /// <summary>
        /// Method to open an existing storage file
        /// </summary>
        /// <param name="_filename">Full path of Zip file to open</param>
        /// <param name="_access">File access mode as used in FileStream constructor</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Open(string _filename, FileAccess _access)
        {
            Stream stream = new FileStream(_filename, FileMode.Open, _access == FileAccess.Read ? FileAccess.Read : FileAccess.ReadWrite);

            ZipStorer zip = Open(stream, _access);
            zip.FileName = _filename;

            return zip;
        }
        /// <summary>
        /// Method to open an existing storage from stream
        /// </summary>
        /// <param name="_stream">Already opened stream with zip contents</param>
        /// <param name="_access">File access mode for stream operations</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Open(Stream _stream, FileAccess _access)
        {
            if (!_stream.CanSeek && _access != FileAccess.Read)
                throw new InvalidOperationException("Stream cannot seek");

            ZipStorer zip = new ZipStorer();
            //zip.FileName = _filename;
            zip.ZipFileStream = _stream;
            zip.Access = _access;

            if (zip.ReadFileInfo())
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
        public void AddFile(Compression _method, string _pathname, string _filenameInZip, string _comment)
        {
            if (Access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not alowed");

            FileStream stream = new FileStream(_pathname, FileMode.Open, FileAccess.Read);
            AddStream(_method, _filenameInZip, stream, File.GetLastWriteTime(_pathname), _comment);
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
        public void AddStream(Compression _method, string _filenameInZip, Stream _source, DateTime _modTime, string _comment)
        {
            if (Access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not alowed");

            long offset;
            if (this.Files.Count == 0)
                offset = 0;
            else
            {
                ZipFileEntry last = this.Files[this.Files.Count - 1];
                offset = last.HeaderOffset + last.HeaderSize;
            }

            // Prepare the fileinfo
            ZipFileEntry zfe = new ZipFileEntry();
            zfe.Method = _method;
            zfe.EncodeUTF8 = this.EncodeUTF8;
            zfe.FilenameInZip = NormalizedFilename(_filenameInZip);
            zfe.Comment = (_comment == null ? string.Empty : _comment);

            // Even though we write the header now, it will have to be rewritten, since we don't know compressed size or crc.
            zfe.Crc32 = 0;  // to be updated later
            zfe.HeaderOffset = (uint)this.ZipFileStream.Position;  // offset within file of the start of this local record
            zfe.ModifyTime = _modTime;

            // Write local header
            WriteLocalHeader(ref zfe);
            zfe.FileOffset = (uint)this.ZipFileStream.Position;

            // Write file to zip (store)
            Store(ref zfe, _source);
            _source.Close();

            this.UpdateCrcAndSizes(ref zfe);

            Files.Add(zfe);
        }
        /// <summary>
        /// Updates central directory (if pertinent) and close the Zip storage
        /// </summary>
        /// <remarks>This is a required step, unless automatic dispose is used</remarks>
        public void Close()
        {
            if (this.Access != FileAccess.Read)
            {
                uint centralOffset = (uint)this.ZipFileStream.Position;
                uint centralSize = 0;

                if (this.CentralDirImage != null)
                    this.ZipFileStream.Write(CentralDirImage, 0, CentralDirImage.Length);

                for (int i = 0; i < Files.Count; i++)
                {
                    long pos = this.ZipFileStream.Position;
                    this.WriteCentralDirRecord(Files[i]);
                    centralSize += (uint)(this.ZipFileStream.Position - pos);
                }

                if (this.CentralDirImage != null)
                    this.WriteEndRecord(centralSize + (uint)CentralDirImage.Length, centralOffset);
                else
                    this.WriteEndRecord(centralSize, centralOffset);
            }

            if (this.ZipFileStream != null)
            {
                this.ZipFileStream.Flush();
                this.ZipFileStream.Dispose();
                this.ZipFileStream = null;
            }
        }
        /// <summary>
        /// Read all the file records in the central directory 
        /// </summary>
        /// <returns>List of all entries in directory</returns>
        public List<ZipFileEntry> ReadCentralDir()
        {
            if (this.CentralDirImage == null)
                throw new InvalidOperationException("Central directory currently does not exist");

            List<ZipFileEntry> result = new List<ZipFileEntry>();

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

                ZipFileEntry zfe = new ZipFileEntry();
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
        /// <summary>
        /// Copy the contents of a stored file into a physical file
        /// </summary>
        /// <param name="_zfe">Entry information of file to extract</param>
        /// <param name="_filename">Name of file to store uncompressed data</param>
        /// <returns>True if success, false if not.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public bool ExtractFile(ZipFileEntry _zfe, string _filename)
        {
            // Make sure the parent directory exist
            string path = System.IO.Path.GetDirectoryName(_filename);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            // Check it is directory. If so, do nothing
            if (Directory.Exists(_filename))
                return true;

            using (Stream output = new FileStream(_filename, FileMode.Create, FileAccess.Write))
            {
                if (ExtractFile(_zfe, output))
                {
                    return false;
                }
            }
            File.SetCreationTime(_filename, _zfe.ModifyTime);
            File.SetLastWriteTime(_filename, _zfe.ModifyTime);

            return true;
        }
        /// <summary>
        /// Copy the contents of a stored file into an opened stream
        /// </summary>
        /// <param name="_zfe">Entry information of file to extract</param>
        /// <param name="_stream">Stream to store the uncompressed data</param>
        /// <returns>True if success, false if not.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public bool ExtractFile(ZipFileEntry _zfe, Stream _stream)
        {
            if (!_stream.CanWrite)
                throw new InvalidOperationException("Stream cannot be written");

            // check signature
            byte[] signature = new byte[4];
            this.ZipFileStream.Seek(_zfe.HeaderOffset, SeekOrigin.Begin);
            this.ZipFileStream.Read(signature, 0, 4);
            if (BitConverter.ToUInt32(signature, 0) != 0x04034b50)
                return false;

            
            // Prepare streams
            _stream.SetLength(_zfe.FileSize);
            var outStream = new CrcStream(_stream);
            this.ZipFileStream.Seek(_zfe.FileOffset, SeekOrigin.Begin);

            // Select input stream for inflating or just reading
            Stream inStream = new SegmentStream(this.ZipFileStream, _zfe.CompressedSize);
            
            if (_zfe.Method == Compression.Deflate)
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
            if (_zfe.Method == Compression.LZMA)
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

                decoder.Code(inStream, outStream, _zfe.CompressedSize, _zfe.FileSize, null);
            }
            else
            {
                inStream.CopyTo(outStream);
            }

            _stream.Flush();

            if (_zfe.Method == Compression.Deflate)
                inStream.Dispose();
            if (_zfe.Crc32 != outStream.WriteCrc)
            {
                throw new Exception("Uncompressed file CRC mismatch");
            }
            return true;
        }
        /// <summary>
        /// Removes one of many files in storage. It creates a new Zip file.
        /// </summary>
        /// <param name="_zip">Reference to the current Zip object</param>
        /// <param name="_zfes">List of Entries to remove from storage</param>
        /// <returns>True if success, false if not</returns>
        /// <remarks>This method only works for storage of type FileStream</remarks>
        public static bool RemoveEntries(ref ZipStorer _zip, List<ZipFileEntry> _zfes)
        {
            if (!(_zip.ZipFileStream is FileStream))
                throw new InvalidOperationException("RemoveEntries is allowed just over streams of type FileStream");


            //Get full list of entries
            List<ZipFileEntry> fullList = _zip.ReadCentralDir();

            //In order to delete we need to create a copy of the zip file excluding the selected items
            string tempZipName = Path.GetTempFileName();
            string tempEntryName = Path.GetTempFileName();

            try
            {
                ZipStorer tempZip = ZipStorer.Create(tempZipName, string.Empty);

                foreach (ZipFileEntry zfe in fullList)
                {
                    if (!_zfes.Contains(zfe))
                    {
                        if (_zip.ExtractFile(zfe, tempEntryName))
                        {
                            tempZip.AddFile(zfe.Method, tempEntryName, zfe.FilenameInZip, zfe.Comment);
                        }
                    }
                }
                _zip.Close();
                tempZip.Close();

                File.Delete(_zip.FileName);
                File.Move(tempZipName, _zip.FileName);

                _zip = ZipStorer.Open(_zip.FileName, _zip.Access);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (File.Exists(tempZipName))
                    File.Delete(tempZipName);
                if (File.Exists(tempEntryName))
                    File.Delete(tempEntryName);
            }
            return true;
        }
        #endregion

        #region Private methods
        // Calculate the file offset by reading the corresponding local header
        private uint GetFileOffset(uint _headerOffset)
        {
            byte[] buffer = new byte[2];

            this.ZipFileStream.Seek(_headerOffset + 26, SeekOrigin.Begin);
            this.ZipFileStream.Read(buffer, 0, 2);
            ushort filenameSize = BitConverter.ToUInt16(buffer, 0);
            this.ZipFileStream.Read(buffer, 0, 2);
            ushort extraSize = BitConverter.ToUInt16(buffer, 0);

            return (uint)(30 + filenameSize + extraSize + _headerOffset);
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
        private void WriteLocalHeader(ref ZipFileEntry _zfe)
        {
            long pos = this.ZipFileStream.Position;
            Encoding encoder = _zfe.EncodeUTF8 ? Encoding.UTF8 : DefaultEncoding;
            byte[] encodedFilename = encoder.GetBytes(_zfe.FilenameInZip);

            this.ZipFileStream.Write(new byte[] { 80, 75, 3, 4, 20, 0 }, 0, 6); // No extra header
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)(_zfe.EncodeUTF8 ? 0x0800 : 0)), 0, 2); // filename and comment encoding 
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)_zfe.Method), 0, 2);  // zipping method
            this.ZipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(_zfe.ModifyTime)), 0, 4); // zipping date and time
            this.ZipFileStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 12); // unused CRC, un/compressed size, updated later
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)encodedFilename.Length), 0, 2); // filename length
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // extra length

            this.ZipFileStream.Write(encodedFilename, 0, encodedFilename.Length);
            _zfe.HeaderSize = (uint)(this.ZipFileStream.Position - pos);
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
        private void WriteCentralDirRecord(ZipFileEntry _zfe)
        {
            Encoding encoder = _zfe.EncodeUTF8 ? Encoding.UTF8 : DefaultEncoding;
            byte[] encodedFilename = encoder.GetBytes(_zfe.FilenameInZip);
            byte[] encodedComment = encoder.GetBytes(_zfe.Comment);

            this.ZipFileStream.Write(new byte[] { 80, 75, 1, 2, 23, 0xB, 20, 0 }, 0, 8);
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)(_zfe.EncodeUTF8 ? 0x0800 : 0)), 0, 2); // filename and comment encoding 
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)_zfe.Method), 0, 2);  // zipping method
            this.ZipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(_zfe.ModifyTime)), 0, 4);  // zipping date and time
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.Crc32), 0, 4); // file CRC
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.CompressedSize), 0, 4); // compressed file size
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.FileSize), 0, 4); // uncompressed file size
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)encodedFilename.Length), 0, 2); // Filename in zip
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // extra length
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)encodedComment.Length), 0, 2);

            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // disk=0
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // file type: binary
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // Internal file attributes
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)0x8100), 0, 2); // External file attributes (normal/readable)
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.HeaderOffset), 0, 4);  // Offset of header

            this.ZipFileStream.Write(encodedFilename, 0, encodedFilename.Length);
            this.ZipFileStream.Write(encodedComment, 0, encodedComment.Length);
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
        private void WriteEndRecord(uint _size, uint _offset)
        {
            Encoding encoder = this.EncodeUTF8 ? Encoding.UTF8 : DefaultEncoding;
            byte[] encodedComment = encoder.GetBytes(this.Comment);

            this.ZipFileStream.Write(new byte[] { 80, 75, 5, 6, 0, 0, 0, 0 }, 0, 8);
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)Files.Count + ExistingFiles), 0, 2);
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)Files.Count + ExistingFiles), 0, 2);
            this.ZipFileStream.Write(BitConverter.GetBytes(_size), 0, 4);
            this.ZipFileStream.Write(BitConverter.GetBytes(_offset), 0, 4);
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)encodedComment.Length), 0, 2);
            this.ZipFileStream.Write(encodedComment, 0, encodedComment.Length);
        }
        // Copies all source file into storage file
        private void Store(ref ZipFileEntry _zfe, Stream _source)
        {
            uint totalRead = 0;
            Stream outStream;

            long posStart = this.ZipFileStream.Position;
            long sourceStart = _source.Position;

            outStream = this.ZipFileStream;

            var inStream = new CrcStream(_source);
            switch (_zfe.Method)
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
            _zfe.Crc32 = inStream.ReadCrc;
            outStream.Flush();

            _zfe.FileSize = (uint)(_source.Position - sourceStart);
            _zfe.CompressedSize = (uint)(this.ZipFileStream.Position - posStart);

            // Verify for real compression
            if (_zfe.Method != Compression.Store && !this.ForceDeflating && _source.CanSeek && _zfe.CompressedSize > _zfe.FileSize)
            {
                // Start operation again with Store algorithm
                _zfe.Method = Compression.Store;
                this.ZipFileStream.Position = posStart;
                this.ZipFileStream.SetLength(posStart);
                _source.Position = sourceStart;
                this.Store(ref _zfe, _source);
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
        private void UpdateCrcAndSizes(ref ZipFileEntry _zfe)
        {
            long lastPos = this.ZipFileStream.Position;  // remember position

            this.ZipFileStream.Position = _zfe.HeaderOffset + 8;
            this.ZipFileStream.Write(BitConverter.GetBytes((ushort)_zfe.Method), 0, 2);  // zipping method

            this.ZipFileStream.Position = _zfe.HeaderOffset + 14;
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.Crc32), 0, 4);  // Update CRC
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.CompressedSize), 0, 4);  // Compressed size
            this.ZipFileStream.Write(BitConverter.GetBytes(_zfe.FileSize), 0, 4);  // Uncompressed size
            this.ZipFileStream.Position = lastPos;  // restore position
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
        private bool ReadFileInfo()
        {
            if (this.ZipFileStream.Length < 22)
                return false;

            try
            {
                this.ZipFileStream.Seek(-17, SeekOrigin.End);
                BinaryReader br = new BinaryReader(this.ZipFileStream);
                do
                {
                    this.ZipFileStream.Seek(-5, SeekOrigin.Current);
                    UInt32 sig = br.ReadUInt32();
                    if (sig == 0x06054b50)
                    {
                        this.ZipFileStream.Seek(6, SeekOrigin.Current);

                        UInt16 entries = br.ReadUInt16();
                        Int32 centralSize = br.ReadInt32();
                        UInt32 centralDirOffset = br.ReadUInt32();
                        UInt16 commentSize = br.ReadUInt16();

                        // check if comment field is the very last data in file
                        if (this.ZipFileStream.Position + commentSize != this.ZipFileStream.Length)
                            return false;

                        // Copy entire central directory to a memory buffer
                        this.ExistingFiles = entries;
                        this.CentralDirImage = new byte[centralSize];
                        this.ZipFileStream.Seek(centralDirOffset, SeekOrigin.Begin);
                        this.ZipFileStream.Read(this.CentralDirImage, 0, centralSize);

                        // Leave the pointer at the begining of central dir, to append new files
                        this.ZipFileStream.Seek(centralDirOffset, SeekOrigin.Begin);
                        return true;
                    }
                } while (this.ZipFileStream.Position > 0);
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