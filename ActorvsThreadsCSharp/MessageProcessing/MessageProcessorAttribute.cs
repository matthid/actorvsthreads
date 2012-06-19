namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;

    public delegate void MessageProcessor(MessageServerClient client, ParsedLineEventArgs eventArgs);

    [System.AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class MessageProcessorAttribute : System.Attribute
    {
        public string Command { get; private set; }

        public MessageProcessorAttribute(string command)
        {
            this.Command = command;
        }
    }
}