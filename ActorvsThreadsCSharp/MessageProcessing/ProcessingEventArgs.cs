namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;

    public class ProcessingEventArgs : EventArgs
    {
        public ProcessingEventArgs()
        {
            this.PreventProcessing = false;
        }

        public bool PreventProcessing { get; set; }
    }
}