

public class Start implements Serializable {
    public Start() { }
}
public class Stop implements Serializable {
    public Stop() { }
}  
public class Go implements Serializable {
    public Go() { }
}

public class GreetingActor extends UntypedActor {
    LoggingAdapter log = Logging.getLogger(getContext().system(), this);
	private int counter ;
    public void onReceive(Object message) throws Exception {
		if (message instanceof Start){
			counter = 0;
			getSelf().tell(new Go());
			getSelf().tell(new Stop());
		} else if (message instanceof Stop){
			system.out.println("Counter got to " + counter);
		} else if (message instanceof Go) {
			counter++;
			getSelf().tell(new Go());
		}
    }
}

ActorSystem system = ActorSystem.create("MySystem");
ActorRef counter = system.actorOf(new Props(GreetingActor.class), "counter");
counter.tell(new Start());

