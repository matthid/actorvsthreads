namespace ActorvsThreadsCSharp
{
    using System.Threading;

    public interface IActor
    {
    }

    public interface ICounter : IActor
    {
        void SetSelf(ICounter self);
        void Start();
        void Stop();
        void Go();
    }

    public class CounterActor : ICounter
    {
        private ICounter self;

        private int counter;

        public void SetSelf(ICounter self)
        {
            this.self = self;
        }

        public void Start()
        {
            counter = 0;
            self.Go();
            self.Stop();
        }

        public void Stop()
        {
            System.Console.WriteLine("Counter got to {0}", counter);
        }

        public void Go()
        {
            counter++;
            self.Go();
        }

        public static void _Main()
        {
            ICounter pinger = ActorWrapper.WrapActor<ICounter>(() => new CounterActor());
            pinger.SetSelf(pinger);
            pinger.Start();
            Thread.Sleep(1000);
        }
    }
}