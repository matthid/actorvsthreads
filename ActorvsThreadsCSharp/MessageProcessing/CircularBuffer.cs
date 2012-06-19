namespace Yaaf.Utils.IO
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    public class CircularBuffer<T> : ICollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private int capacity;
        private int size;
        private int head;
        private int tail;
        private T[] buffer;

        [NonSerialized]
        private object syncRoot;

        public CircularBuffer(int capacity)
            : this(capacity, false)
        {
        }

        public CircularBuffer(int capacity, bool allowOverflow)
        {
            if (capacity < 0)
                throw new ArgumentException("capacity must be greater than or equal to zero.",
                    "capacity");

            this.capacity = capacity;
            this.size = 0;
            this.head = 0;
            this.tail = 0;
            this.buffer = new T[capacity];
            this.AllowOverflow = allowOverflow;
        }

        public bool AllowOverflow
        {
            get;
            set;
        }

        public int Capacity
        {
            get { return this.capacity; }
            set
            {
                if (value == this.capacity)
                    return;

                if (value < this.size)
                    throw new ArgumentOutOfRangeException("value",
                        "value must be greater than or equal to the buffer size.");

                var dst = new T[value];
                if (this.size > 0)
                    this.CopyTo(dst);
                this.buffer = dst;

                this.capacity = value;
            }
        }

        public int Size
        {
            get { return this.size; }
        }

        public bool Contains(T item)
        {
            int bufferIndex = this.head;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < this.size; i++, bufferIndex++)
            {
                if (bufferIndex == this.capacity)
                    bufferIndex = 0;

                if (item == null && this.buffer[bufferIndex] == null)
                    return true;
                else if ((this.buffer[bufferIndex] != null) &&
                    comparer.Equals(this.buffer[bufferIndex], item))
                    return true;
            }

            return false;
        }

        public void Clear()
        {
            this.size = 0;
            this.head = 0;
            this.tail = 0;
        }

        public int Put(T[] src)
        {
            return this.Put(src, 0, src.Length);
        }

        public int Put(T[] src, int offset, int count)
        {
            int realCount = this.AllowOverflow ? count : Math.Min(count, this.capacity - this.size);
            int srcIndex = offset;
            for (int i = 0; i < realCount; i++, this.tail++, srcIndex++)
            {
                if (this.tail == this.capacity)
                    this.tail = 0;
                this.buffer[this.tail] = src[srcIndex];
            }
            this.size = Math.Min(this.size + realCount, this.capacity);
            return realCount;
        }

        public void Put(T item)
        {
            if (!this.AllowOverflow && this.size == this.capacity)
                throw new InternalBufferOverflowException("Buffer is full.");

            this.buffer[this.tail] = item;
            if (++this.tail == this.capacity)
                this.tail = 0;
            this.size++;
        }

        public void Skip(int count)
        {
            this.head += count;
            if (this.head >= this.capacity)
                this.head -= this.capacity;
        }

        public T[] Get(int count)
        {
            var dst = new T[count];
            this.Get(dst);
            return dst;
        }

        public int Get(T[] dst)
        {
            return this.Get(dst, 0, dst.Length);
        }

        public int Get(T[] dst, int offset, int count)
        {
            int realCount = Math.Min(count, this.size);
            int dstIndex = offset;
            for (int i = 0; i < realCount; i++, this.head++, dstIndex++)
            {
                if (this.head == this.capacity)
                    this.head = 0;
                dst[dstIndex] = this.buffer[this.head];
            }
            this.size -= realCount;
            return realCount;
        }

        public T Get()
        {
            if (this.size == 0)
                throw new InvalidOperationException("Buffer is empty.");

            var item = this.buffer[this.head];
            if (++this.head == this.capacity)
                this.head = 0;
            this.size--;
            return item;
        }

        public void CopyTo(T[] array)
        {
            this.CopyTo(array, 0);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.CopyTo(0, array, arrayIndex, this.size);
        }

        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            if (count > this.size)
                throw new ArgumentOutOfRangeException("count",
                    "count cannot be greater than the buffer size.");

            int bufferIndex = this.head;
            for (int i = 0; i < count; i++, bufferIndex++, arrayIndex++)
            {
                if (bufferIndex == this.capacity)
                    bufferIndex = 0;
                array[arrayIndex] = this.buffer[bufferIndex];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            int bufferIndex = this.head;
            for (int i = 0; i < this.size; i++, bufferIndex++)
            {
                if (bufferIndex == this.capacity)
                    bufferIndex = 0;

                yield return this.buffer[bufferIndex];
            }
        }

        public T[] GetBuffer()
        {
            return this.buffer;
        }

        public T[] ToArray()
        {
            var dst = new T[this.size];
            this.CopyTo(dst);
            return dst;
        }

        #region ICollection<T> Members

        int ICollection<T>.Count
        {
            get { return this.Size; }
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return false; }
        }

        void ICollection<T>.Add(T item)
        {
            this.Put(item);
        }

        bool ICollection<T>.Remove(T item)
        {
            if (this.size == 0)
                return false;

            this.Get();
            return true;
        }

        #endregion

        #region IEnumerable<T> Members

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion

        #region ICollection Members

        int ICollection.Count
        {
            get { return this.Size; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (this.syncRoot == null)
                    Interlocked.CompareExchange(ref this.syncRoot, new object(), null);
                return this.syncRoot;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            this.CopyTo((T[])array, arrayIndex);
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)this.GetEnumerator();
        }

        #endregion
    }
}
