namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Linq;

    using Yaaf.Utils.Logging;

    public class LineData
    {
        public RawLineId Id { get; private set; }

        public string Line { get; private set; }

        public RawDataStream RawData { get; private set; }

        public LineData(RawLineId id, string tempLine, RawDataStream rawData)
        {
            this.Id = id;
            this.Line = tempLine;
            this.RawData = rawData;
        }
    }

    public class SafeCommandReader
    {
        private readonly Encoding encoding;

        private readonly Stream receiveStream;

        private MemoryStream lineBuilder;

        private long readBytes;

        private LineData tempLine;

        public SafeCommandReader(int bufferSize)
        {
            this.receiveStream = new CircularStream(bufferSize);
            this.encoding = Encoding.UTF8;
        }
        private readonly byte[] privateBuffer = new byte[8 * 1024];
        private bool readId = true;

        private readonly List<byte> idByteList = new List<byte>();
        public LineData ReadLine()
        {
            while (true)
            {
                int next = this.receiveStream.ReadByte();
                if (next == -1)
                {
                    // Try again later
                    break;
                }
                this.dataAvailable--;

                if (this.readBytes > 0)
                {
                    this.tempLine.RawData.WriteByte((byte)next);
                    this.readBytes--;

                    // Note this should be some improvement for file receiving

                    int toWrite = (int)Math.Min(this.dataAvailable, this.readBytes);

                    while (toWrite > 0)
                    {
                        int currentWrite = Math.Min(toWrite, this.privateBuffer.Length);

                        int read = this.receiveStream.Read(this.privateBuffer, 0, currentWrite);
                        this.dataAvailable -= currentWrite;
                        Debug.Assert(read == currentWrite);
                        this.tempLine.RawData.Write(this.privateBuffer, 0, currentWrite);
                        this.readBytes -= currentWrite;

                        toWrite = (int)Math.Min(this.dataAvailable, this.readBytes);
                    }

                    Debug.Assert(this.dataAvailable >= 0);
                    Debug.Assert(this.readBytes >= 0);
                }
                else
                {
                    if (next == 0)
                    {
                        return this.FinishLineData();
                    }

                    if (next == 0x20 && this.readId) // Space in UTF-8
                    {
                        this.readId = false;
                    }
                    else
                    {
                        if (this.readId)
                        {
                            if (next <= 0x20)
                            {
                                throw new FormatException(
                                    "Invalid byte layout format (id can't contain byte values <= 20)");
                            }
                            this.idByteList.Add((byte)next);
                        }
                        else
                        {
                            if (this.lineBuilder == null)
                            {
                                this.lineBuilder = new MemoryStream(1024);
                            }

                            this.lineBuilder.WriteByte((byte)next);
                        }
                    }
                }
            }

            return null;

        }

        private LineData FinishLineData()
        {
            var id = new RawLineId(this.idByteList.ToArray());
            this.idByteList.Clear();
            this.readId = true;
            if (this.lineBuilder == null)
            {
                return new LineData(null, "", null);
            }

            this.lineBuilder.Position = 0;
            using (var reader = new StreamReader(this.lineBuilder, this.encoding))
            {
                string line = reader.ReadToEnd();
                string[] splits = line.Split(' ');

                if (splits.Length > 3 && splits[splits.Length - 2] == "RAWDATA")
                {
                    if (!long.TryParse(splits[splits.Length - 1], out this.readBytes))
                    {
                        this.readBytes = 0;
                        throw new FormatException(string.Format("Invalid bytecount {0}", splits[splits.Length - 1]));
                    }
                    Logger.WriteLine("Received Message with {0} bytes", TraceEventType.Information, this.readBytes);
                    this.tempLine = new LineData(
                        id, string.Join(" ", splits.Take(splits.Length - 2)
#if NET2
                        .ToArray()
#endif
                        ), new RawDataStream(1024 * 4, this.readBytes));
                    this.lineBuilder.Dispose();
                    this.lineBuilder = null;
                    if (this.readBytes < 0)
                    {
                        throw new FormatException("Invalid byte layout format (invalid readbytes value)");
                    }
                    return this.tempLine;
                }

                this.lineBuilder = null;
                return new LineData(id, line, null);
            }
        }

        private long dataAvailable = 0;
        public void WriteData(byte[] buffer, int offset, int count)
        {
            this.receiveStream.Write(buffer, offset, count);
            this.dataAvailable += count;
        }
    }
}