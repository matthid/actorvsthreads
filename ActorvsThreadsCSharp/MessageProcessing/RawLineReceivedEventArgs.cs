namespace Yaaf.Utils.IO.MessageProcessing
{
    public class RawLineReceivedEventArgs : ProcessingEventArgs
    {
        public LineData RawLine { get; private set; }


        public RawLineReceivedEventArgs(LineData rawLine)
        {
            this.RawLine = rawLine;
        }
    }
}