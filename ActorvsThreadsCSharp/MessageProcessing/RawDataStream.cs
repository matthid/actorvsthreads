namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;
    using System.Threading;

    /// <summary>
    /// A data Stream designed to be used one time to read data from the network.
    /// </summary>
    public class RawDataStream : OneTimeStream
    {
        private readonly long length;

        private long wroteBytes = 0;

        private long readPos = 0;
        public RawDataStream(int fragmentation, long length)
            : base(fragmentation)
        {
            this.length = length;
        }

        private readonly object lockSync = new object();
        private readonly object readlockSync = new object();
        private readonly object writelockSync = new object();

        /// <summary>
        /// Writes data on the end of the current Stream
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (this.writelockSync)
            {
                base.Write(buffer, offset, count);
                lock (this.lockSync)
                {
                    this.wroteBytes += count;
                }
            }
        }

        /// <summary>
        /// Reads some bytes from the current Stream.
        /// Blocks the caller until at least 1 byte is available.
        /// Returns 0 when no more data is available, the buffer is full or count is 0.
        /// </summary>
        /// <returns>the read bytes</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (this.readlockSync)
            {
                while (this.GetDif() == 0)
                {
                    if (this.IsCompleteRead())
                    {
                        return 0;
                    }

                    Thread.Sleep(200); // wait for data
                }

                var read = base.Read(buffer, offset, Math.Min(count, (int)Math.Min(this.GetDif(), int.MaxValue)));
                lock (this.lockSync)
                {
                    this.readPos += read;
                }
                return read;
            }
        }

        /// <summary>
        /// Reads a byte from the current Stream.
        /// Blocks the caller until further data is available.
        /// Or returns -1 when no more data is available.
        /// </summary>
        /// <returns>the read byte or -1</returns>
        public override int ReadByte()
        {
            lock (this.readlockSync)
            {
                while (this.GetDif() == 0)
                {
                    if (this.IsCompleteRead())
                    {
                        return -1;
                    }

                    Thread.Sleep(200);
                }

                var read = base.ReadByte();
                Interlocked.Increment(ref this.readPos);
                return read;
            }
        }


        private bool IsCompleteRead()
        {
            lock (this.lockSync)
            {
                return this.readPos == this.length;
            }
        }

        /// <summary>
        /// Writes a byte on the end of the stream
        /// </summary>
        /// <param name="value"></param>
        public override void WriteByte(byte value)
        {
            lock (this.writelockSync)
            {
                base.WriteByte(value);
                Interlocked.Increment(ref this.wroteBytes);
            }
        }

        /// <summary>
        /// The complete count of bytes available
        /// </summary>
        public override long Length
        {
            get
            {
                return this.length;
            }
        }

        private long GetDif()
        {
            lock (this.lockSync)
            {
                return this.wroteBytes - this.readPos;
            }
        }
    }
}