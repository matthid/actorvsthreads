namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class RawLineId : IComparable<RawLineId>
    {
        private readonly byte[] bytes;

        private const int Offset = 0x21;
        private const int MaxValue = byte.MaxValue - Offset;

        internal RawLineId() : this (new byte[]{Offset})
        {
            
        }

        public static RawLineId FromBytes(byte[] privateBuffer)
        {
            return new RawLineId(privateBuffer);
        }

        internal RawLineId(byte[] bytes)
        {
            this.bytes = bytes;
            if (bytes.Any(b => b < Offset))
            {
                throw new FormatException("Invalid byte layout format (id can't contain byte values <= 20)");
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] -= Offset;
            }
        }

        internal RawLineId Inc()
        {
            return new RawLineId(MyIncrement(this.bytes, 0, 1).Select(b => (byte)(b + Offset)).ToArray());
        }
        
        private static Tuple<byte, bool> AddBytes(byte b1, byte b2)
        {
            var add = b1 + b2;
            bool overFlow = false;
            if (add > MaxValue)
            {
                overFlow = true;
                add -= MaxValue;
            }
            return Tuple.Create((byte)add, overFlow);
        }

        private static IEnumerable<byte> MyIncrement(IEnumerable<byte> bytes, int offset, byte toInc)
        {
            if (toInc > MaxValue)
            {
                toInc -= MaxValue;
                bytes = MyIncrement(bytes, offset + 1, 1);
            }
            foreach (var b in bytes)
            {
                if (offset > 0)
                {
                    yield return b;
                    offset --;
                }
                if (toInc > 0)
                {
                    var addedTuple = AddBytes(toInc, b);
                    yield return addedTuple.Item1;
                    toInc = (byte)((addedTuple.Item2) ? 1 : 0);
                }
                else
                {
                    yield return b;
                }
            }

            if (toInc > 0)
            {
                for (int i = 0; i < offset; i++)
                {
                    yield return 0;
                }
            
                yield return toInc;
            }
        }

        /// <summary>
        /// Vergleicht das aktuelle Objekt mit einem anderen Objekt desselben Typs.
        /// </summary>
        /// <returns>
        /// Ein Wert, der die relative Reihenfolge der verglichenen Objekte angibt.Der Rückgabewert hat folgende Bedeutung:Wert Bedeutung Kleiner als 0 (null) Dieses Objekt ist kleiner als der <paramref name="other"/>-Parameter.Zero Dieses Objekt ist gleich <paramref name="other"/>. Größer als 0 (null) Dieses Objekt ist größer als <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">Ein Objekt, das mit diesem Objekt verglichen werden soll.</param>
        public int CompareTo(RawLineId other)
        {
            var otherBytes = other.bytes;
            var length = (this.bytes.Length.CompareTo(otherBytes.Length));
            if (length != 0)
            {
                return length;
            }

            for (int i = this.bytes.Length - 1; i >= 0; i--)
            {
                var comp = this.bytes[i].CompareTo(otherBytes[i]);
                if (comp != 0) return comp;
            }

            return 0;
        }

        public bool Equals(RawLineId other)
        {
            return this.CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != typeof(RawLineId))
            {
                return false;
            }
            return this.Equals((RawLineId)obj);
        }
        public static bool operator ==(RawLineId current, RawLineId other)
        {
            if (ReferenceEquals(current,null))
            {
                return false;
            }
            return current.Equals(other);
        }

        public static bool operator !=(RawLineId current, RawLineId other)
        {
            return !(current == other);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        private string toStringSave = null;
        public override string ToString()
        {
            if (this.toStringSave == null)
            {
                UInt64 id = 0;
                var builder = new StringBuilder(this.bytes.Length);
                for (int i = this.bytes.Length - 1; i >= 0; i--)
                {
                    builder.Append((char)(this.bytes[i] + Offset));
                    id |= ((ulong)this.bytes[i] + Offset) << ((this.bytes.Length - 1) - i) * 8;
                }

                this.toStringSave = string.Format("ID({0}, {1})", builder, id);
            }

            return this.toStringSave;
        }

        public byte[] GetIdBytes()
        {
            return this.bytes.Select(b => (byte)(b + Offset)).ToArray();
        }
    }
}