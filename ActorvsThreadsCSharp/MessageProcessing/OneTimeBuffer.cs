namespace Yaaf.Utils.IO
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// A self expanding buffer for unlimited amount of data, but only to be used one time 
    /// (as soon as the data gets read it will be deleted)
    /// </summary>
    /// <typeparam name="T">the data Type</typeparam>
    public class OneTimeBuffer<T>
    {
        private readonly int fragmentation;

        private class OneTimeBufferHelper
        {
            private readonly int capacity;

            private readonly T[] internalBuffer;

            private int currentWrite = 0;
            private int currentRead = 0;
            private readonly object locker = new object();
            public OneTimeBufferHelper(int capacity)
            {
                this.capacity = capacity;
                this.internalBuffer = new T[capacity];
            }

            public bool IsDataAvailable
            {
                get
                {
                    return this.currentWrite - this.currentRead > 0;
                }
            }

            public bool IsFull
            {
                get
                {
                    return this.internalBuffer.Length == this.currentWrite;
                }
            }

            public int Write(T[] buffer, int offset, int count)
            {
                lock (this.locker)
                {
                    var writeCount = Math.Min(count, this.internalBuffer.Length - this.currentWrite);
                    if (writeCount == 0) return 0; // Full
                    Array.Copy(buffer, offset, this.internalBuffer, this.currentWrite, writeCount);
                    this.currentWrite += writeCount;
                    return writeCount;
                }
            }

            public int Read(T[] buffer, int offset, int count)
            {
                lock (this.locker)
                {
                    var readCount = Math.Min(count, this.currentWrite - this.currentRead);
                    if (readCount == 0) return 0; // No More Data
                    Array.Copy(this.internalBuffer, this.currentRead, buffer, offset, readCount);
                    this.currentRead += readCount;
                    return readCount; 
                }
            }

            public T ReadOne(out bool internalSuccess)
            {
                lock (this.locker)
                {
                    if (this.currentWrite <= this.currentRead)
                    {
                        internalSuccess = false;
                        return default(T);
                    }
                    internalSuccess = true;
                    return this.internalBuffer[this.currentRead++];
                }
            }

            public bool WriteOne(T value)
            {
                lock (this.locker)
                {
                    if (this.currentWrite >= this.internalBuffer.Length) return false;
                    this.internalBuffer[this.currentWrite++] = value;
                    return true;
                }
            }
        }


        private readonly LinkedList<OneTimeBufferHelper> helperList = new LinkedList<OneTimeBufferHelper>();

        public OneTimeBuffer(int fragmentation)
        {
            if (fragmentation <= 0) throw new ArgumentOutOfRangeException("fragmentation");
            this.fragmentation = fragmentation;
        }

        private readonly object readLock = new object();
        private readonly object writeLock = new object();
        public void Write(T[] buffer, int offset, int count)
        {
            lock (this.writeLock)
            {
                OneTimeBufferHelper helper;
                lock (((ICollection)this.helperList).SyncRoot)
                {
                    if (this.helperList.Count == 0)
                    {
                        this.helperList.AddFirst(new OneTimeBufferHelper(this.fragmentation));
                    }
                    helper = this.helperList.Last.Value;
                }
                var writtenData = 0;
                while (writtenData < count)
                {
                    int tryWrite = count - writtenData;
                    var realWritten = helper.Write(buffer, offset, tryWrite);
                    writtenData += realWritten;
                    offset += realWritten;
                    if (helper.IsFull) // helper buffer full
                    {
                        helper = new OneTimeBufferHelper(this.fragmentation);
                        lock (((ICollection)this.helperList).SyncRoot)
                        {
                            this.helperList.AddLast(helper);
                        }
                    }
                }
            }
        }

        public int Read(T[] buffer, int offset, int count)
        {
            lock (this.readLock)
            {
                OneTimeBufferHelper helper;
                
                var readBytes = 0;
                while (count > 0)
                {
                    lock (((ICollection)this.helperList).SyncRoot)
                    {
                        if (this.helperList.Count == 0)
                        {
                            break;
                        }
                        helper = this.helperList.First.Value;
                    }
                    var read = helper.Read(buffer, offset, count);
                    offset += read;
                    count -= read;
                    readBytes += read;
                    if (!helper.IsDataAvailable)
                    {
                        if (helper.IsFull)
                        {
                            lock (((ICollection)this.helperList).SyncRoot)
                            {
                                this.helperList.RemoveFirst();
                            }
                        }
                        else
                        {// currently no more data available
                            break;
                        }
                    }
                }

                return readBytes;
            }
        }

        public T ReadOne(out bool success)
        {
            lock (this.readLock)
            {
                OneTimeBufferHelper helper; 
               

                while (true)
                {
                    lock (((ICollection)this.helperList).SyncRoot)
                    {
                        if (this.helperList.Count == 0)
                        {
                            success = false;
                            return default(T);
                        }

                        helper = this.helperList.First.Value;
                    }

                    bool internalSuccess;
                    var res = helper.ReadOne(out internalSuccess);
                    if (internalSuccess)
                    {
                        success = true;
                        return res;
                    }
                    if (helper.IsFull)
                    {
                        lock (this.helperList)
                        {
                            this.helperList.RemoveFirst();
                        }
                    }
                    else
                    { // No data currenty available
                        success = false;
                        return res;
                    }
                }
            }
        }

        public void WriteOne(T value)
        {
            lock (this.writeLock)
            {
                OneTimeBufferHelper helper;
                lock (((ICollection)this.helperList).SyncRoot)
                {
                    if (this.helperList.Count == 0)
                    {
                        this.helperList.AddFirst(new OneTimeBufferHelper(this.fragmentation));
                    }

                    helper = this.helperList.Last.Value;
                }

                bool written = false;
                while (!written)
                {
                    written = helper.WriteOne(value);
                    if (!written)
                    {
                        lock (((ICollection)this.helperList).SyncRoot)
                        {
                            helper = new OneTimeBufferHelper(this.fragmentation);
                            this.helperList.AddLast(helper);
                        }
                    }
                }
            }
        }
    }

}