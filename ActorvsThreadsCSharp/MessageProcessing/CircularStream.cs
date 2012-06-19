namespace Yaaf.Utils.IO
{
    using System;
    using System.IO;

    public class CircularStream : Stream
    {
        private readonly CircularBuffer<byte> buffer;

        public CircularStream(int bufferCapacity)
            : base()
        {
            this.buffer = new CircularBuffer<byte>(bufferCapacity);
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int Capacity
        {
            get { return this.buffer.Capacity; }
            set { this.buffer.Capacity = value; }
        }

        public override long Length
        {
            get { return this.buffer.Size; }
        }

        /// <summary>
        /// Ruft beim Überschreiben in einer abgeleiteten Klasse einen Wert ab, der angibt, ob der aktuelle Stream Suchvorgänge unterstützt.
        /// </summary>
        /// <returns>
        /// true, wenn der Stream Suchvorgänge unterstützt, andernfalls false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Ruft beim Überschreiben in einer abgeleiteten Klasse einen Wert ab, der angibt, ob der aktuelle Stream Lesevorgänge unterstützt.
        /// </summary>
        /// <returns>
        /// true, wenn der Stream Lesevorgänge unterstützt, andernfalls false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public byte[] GetBuffer()
        {
            return this.buffer.GetBuffer();
        }

        public byte[] ToArray()
        {
            return this.buffer.ToArray();
        }

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.buffer.Put(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            this.buffer.Put(value);
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.buffer.Get(buffer, offset, count);
        }

        public override int ReadByte()
        {
            if (this.buffer.Size == 0) return -1;
            return this.buffer.Get();
        }
        
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }
}
