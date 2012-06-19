package actorfoundry.examples.counter;
import osl.manager.*;
import osl.util.*;
import osl.manager.annotations.message;

public class Counter extends Actor {

	private int count = 0;
  @message public void boot() throws RemoteCodeException {
    ActorName counter = create("actorfoundry.examples.counter.Counter");
    send(counter, "Start", counter);
  }

  @message public void Start(ActorName counter) {
	count = 0;
	send(counter, "Go", counter);
	send(counter, "Stop");
  }
  
  @message public void Go(ActorName counter) {
	count++;
	send(counter, "Go", counter);
  }

  @message public void Stop() {
    send(stdout, "println",  "Counter got to " + count);
  }
}