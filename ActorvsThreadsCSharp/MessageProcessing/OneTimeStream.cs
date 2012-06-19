namespace Yaaf.Utils.IO
{
    using System;
    using System.IO;

    /// <summary>
    /// A stream which can be written one time and than read one time
    /// </summary>
    public class OneTimeStream : Stream
    {
        private OneTimeBuffer<byte> buffer;

        private bool isDisposed;

        /// <summary>
        /// creates a new OneTimeStream with the given fragmentation (the internal buffer size)
        /// </summary>
        /// <param name="fragmentation">
        /// the size of the internal buffering. 
        /// Should have no impact on the api, but can change performance
        /// </param>
        public OneTimeStream(int fragmentation)
        {
            this.buffer = new OneTimeBuffer<byte>(fragmentation);
        }

        public override void Flush()
        {
        }

        /// <summary>
        /// Legt beim Überschreiben in einer abgeleiteten Klasse die Position im aktuellen Stream fest.
        /// </summary>
        /// <returns>
        /// Die neue Position innerhalb des aktuellen Streams.
        /// </returns>
        /// <param name="offset">Ein Byteoffset relativ zum <paramref name="origin"/>-Parameter. </param><param name="origin">Ein Wert vom Typ <see cref="T:System.IO.SeekOrigin"/>, der den Bezugspunkt angibt, von dem aus die neue Position ermittelt wird. </param><exception cref="T:System.IO.IOException">Ein E/A-Fehler tritt auf. </exception><exception cref="T:System.NotSupportedException">Der Stream unterstützt keine Suchvorgänge. Dies ist beispielsweise der Fall, wenn der Stream aus einer Pipe- oder Konsolenausgabe erstellt wird. </exception><exception cref="T:System.ObjectDisposedException">Es wurden Methoden aufgerufen, nachdem der Stream geschlossen wurde. </exception><filterpriority>1</filterpriority>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Not supported");
        }

        /// <summary>
        /// Legt beim Überschreiben in einer abgeleiteten Klasse die Länge des aktuellen Streams fest.
        /// </summary>
        /// <param name="value">Die gewünschte Länge des aktuellen Streams in Bytes. </param><exception cref="T:System.IO.IOException">Ein E/A-Fehler tritt auf. </exception><exception cref="T:System.NotSupportedException">Der Stream unterstützt nicht sowohl Lese- als auch Schreibvorgänge. Dies ist beispielsweise der Fall, wenn der Stream aus einer Pipe- oder Konsolenausgabe erstellt wird. </exception><exception cref="T:System.ObjectDisposedException">Es wurden Methoden aufgerufen, nachdem der Stream geschlossen wurde. </exception><filterpriority>2</filterpriority>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("Not supported");
        }

        /// <summary>
        /// Liest beim Überschreiben in einer abgeleiteten Klasse eine Folge von Bytes aus dem aktuellen Stream und erhöht die Position im Stream um die Anzahl der gelesenen Bytes.
        /// </summary>
        /// <returns>
        /// Die Gesamtanzahl der in den Puffer gelesenen Bytes.Dies kann weniger als die Anzahl der angeforderten Bytes sein, wenn diese Anzahl an Bytes derzeit nicht verfügbar ist, oder 0, wenn das Ende des Streams erreicht ist.
        /// </returns>
        /// <param name="buffer">Ein Bytearray.Nach dem Beenden dieser Methode enthält der Puffer das angegebene Bytearray mit den Werten zwischen <paramref name="offset"/> und (<paramref name="offset"/> + <paramref name="count"/> - 1), die durch aus der aktuellen Quelle gelesene Bytes ersetzt wurden.</param><param name="offset">Der nullbasierte Byteoffset im <paramref name="buffer"/>, ab dem die aus dem aktuellen Stream gelesenen Daten gespeichert werden. </param><param name="count">Die maximale Anzahl an Bytes, die aus dem aktuellen Stream gelesen werden sollen. </param><exception cref="T:System.ArgumentException">Die Summe aus <paramref name="offset"/> und <paramref name="count"/> ist größer als die Pufferlänge. </exception><exception cref="T:System.ArgumentNullException"><paramref name="buffer"/> hat den Wert null. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="offset"/> oder <paramref name="count"/> ist negativ. </exception><exception cref="T:System.IO.IOException">Ein E/A-Fehler tritt auf. </exception><exception cref="T:System.NotSupportedException">Der Stream unterstützt keine Lesevorgänge. </exception><exception cref="T:System.ObjectDisposedException">Es wurden Methoden aufgerufen, nachdem der Stream geschlossen wurde. </exception><filterpriority>1</filterpriority>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.isDisposed) return 0;
            return this.buffer.Read(buffer, offset, count);
        }

        /// <summary>
        /// Schreibt beim Überschreiben in einer abgeleiteten Klasse eine Folge von Bytes in den aktuellen Stream und erhöht die aktuelle Position im Stream um die Anzahl der geschriebenen Bytes.
        /// </summary>
        /// <param name="buffer">Ein Bytearray.Diese Methode kopiert <paramref name="count"/> Bytes aus dem <paramref name="buffer"/> in den aktuellen Stream.</param><param name="offset">Der nullbasierte Byteoffset im <paramref name="buffer"/>, ab dem Bytes in den aktuellen Stream kopiert werden. </param><param name="count">Die Anzahl an Bytes, die in den aktuellen Stream geschrieben werden sollen. </param><exception cref="T:System.ArgumentException">Die Summe aus <paramref name="offset"/> und <paramref name="count"/> ist größer als die Pufferlänge. </exception><exception cref="T:System.ArgumentNullException"><paramref name="buffer"/> hat den Wert null. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="offset"/> oder <paramref name="count"/> ist negativ. </exception><exception cref="T:System.IO.IOException">Ein E/A-Fehler tritt auf. </exception><exception cref="T:System.NotSupportedException">Der Stream unterstützt keine Schreibvorgänge. </exception><exception cref="T:System.ObjectDisposedException">Es wurden Methoden aufgerufen, nachdem der Stream geschlossen wurde. </exception><filterpriority>1</filterpriority>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.isDisposed) return;
            this.buffer.Write(buffer, offset, count);
        }

        public override int ReadByte()
        {
            if (this.isDisposed) return -1;
            bool success;
            var data = this.buffer.ReadOne(out success);
            return success ? data : -1;
        }

        public override void WriteByte(byte value)
        {
            if (this.isDisposed) return;
            this.buffer.WriteOne(value);
        }

        public override bool CanRead
        {
            get
            {
                this.CheckDisposed();
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                this.CheckDisposed();
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                this.CheckDisposed();
                return true;
            }
        }

        /// <summary>
        /// Ruft beim Überschreiben in einer abgeleiteten Klasse die Länge des Streams in Bytes ab.
        /// </summary>
        /// <returns>
        /// Ein Long-Wert, der die Länge des Streams in Bytes darstellt.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">Eine aus Stream abgeleitete Klasse unterstützt keine Suchvorgänge. </exception><exception cref="T:System.ObjectDisposedException">Es wurden Methoden aufgerufen, nachdem der Stream geschlossen wurde. </exception><filterpriority>1</filterpriority>
        public override long Length
        {
            get
            {
                throw new NotSupportedException("Not supported");
            }
        }

        /// <summary>
        /// Ruft beim Überschreiben in einer abgeleiteten Klasse die Position im aktuellen Stream ab oder legt diese fest.
        /// </summary>
        /// <returns>
        /// Die aktuelle Position innerhalb des Streams.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">Ein E/A-Fehler tritt auf. </exception><exception cref="T:System.NotSupportedException">Der Stream unterstützt keine Suchvorgänge. </exception><exception cref="T:System.ObjectDisposedException">Es wurden Methoden aufgerufen, nachdem der Stream geschlossen wurde. </exception><filterpriority>1</filterpriority>
        public override long Position
        {
            get
            {
                throw new NotSupportedException("Not supported");
            }
            set
            {
                throw new NotSupportedException("Not supported");
            }
        }

        private void CheckDisposed()
        {
            if (this.isDisposed) throw new ObjectDisposedException("OneTimeStream is disposed");
        }

        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed) return;
            if (disposing)
            {
                this.buffer = null;
            }

            this.isDisposed = true;
            base.Dispose(disposing);
        }
    }
}