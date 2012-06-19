namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;

    internal class RawLineFormatException : Exception
    {
        public RawLineFormatException(string message) : base(message)
        {
        }

        public RawLineFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}