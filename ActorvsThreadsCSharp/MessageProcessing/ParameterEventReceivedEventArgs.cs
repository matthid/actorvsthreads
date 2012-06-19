namespace Yaaf.Utils.IO.MessageProcessing
{
    using Yaaf.Utils.Helper;

    public class ParameterEventReceivedEventArgs : ProcessingEventArgs
    {
        public StringParameter Parameter { get; private set; }
        public RawLineId Id { get; private set; }

        public ParameterEventReceivedEventArgs(StringParameter parameter, RawLineId id)
        {
            this.Parameter = parameter;
            this.Id = id;
        }
    }
}