namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class RemoteServerException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public RemoteServerException()
        {
        }

        public RemoteServerException(string message)
            : base(message)
        {
        }

        public RemoteServerException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected RemoteServerException(
            SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}