namespace Yaaf.Utils.IO.MessageProcessing
{
    using System.IO;

    using Yaaf.Utils.Helper;

    public class ParsedLineEventArgs : ProcessingEventArgs
    {
        public RawLineId Id { get; private set; }
        public string Command { get; private set; }
        public StringParameter Parameter { get; private set; }

        
        public Stream RawData { get; private set; }

        public ParsedLineEventArgs(RawLineId id, string command, StringParameter parameter, Stream rawData)
        {
            this.Id = id;
            this.Command = command;
            this.Parameter = parameter;
            this.RawData = rawData;
        }

        public override string ToString()
        {
            return string.Format("Id: {0}, Command: {1}, Parameter: {2}", this.Id, this.Command, this.Parameter);
        }
    }
}