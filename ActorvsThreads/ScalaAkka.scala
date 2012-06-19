import akka.actor.Actor
import akka.actor.Props
import akka.event.Logging
    
case class Start()
case class Stop()
case class Go()
class Counter extends Actor {
    val log = Logging(context.system, this)
	val counter = 0
    def receive = {
		case Start() ⇒ 
				counter = 0
				self ! Go()
				self ! Stop()
		case Go() ⇒ 
				counter = counter + 1 
				self ! Go()
		case Stop() ⇒ 
				System.out.println("Counter got to " + counter)
		case _ ⇒ log.info("received unknown message")
    }
}

val system = ActorSystem("MySystem")
val counter = system.actorOf(Props[Counter], name = "counter")
counter ! Start()
