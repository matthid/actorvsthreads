namespace ActorvsThreadsCSharp
{
    using System;

    public class SheetExamples
    {
        private object lockObject = new object();

        public void LockingMethod(object p)
        {
            lock (lockObject)
            {
                Console.WriteLine("Locked statement {0}", p);
            }
        }
    }
}