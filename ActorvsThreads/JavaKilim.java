package kilim.examples;

import kilim.ExitMsg;
import kilim.Mailbox;
import kilim.Pausable;
import kilim.Task;

/**
* A simple Kilim Counter Task.
*
* [compile] javac -d ./classes CounterTask.java
* [weave] java kilim.tools.Weave -d ./classes CounterTask
* [run] java -cp ./classes:./classes:$CLASSPATH CounterTask
*/
public class CounterTask extends Task {
    static Mailbox<String> mb = new Mailbox<String>();
    static Mailbox<ExitMsg> exitmb = new Mailbox<ExitMsg>();
    
    public static void main(String[] args) throws Exception {
        Task t = new CounterTask().start();
        t.informOnExit(exitmb);
        mb.putnb("start");
        
        exitmb.getb();
        System.exit(0);
    }

    /**
* The entry point. mb.get() is a blocking call that yields
* the thread ("pausable")
*/
	private int counter;
    public void execute() throws Pausable{
        while (true) {
            String s = mb.get();
			if (s.equals("stop")) break;
            else if (s.equals("start")){
				counter = 0;
				mb.putnb("go");
				mb.putnb("stop");
			}
			else if (s.equals("go")) {
				mb.putnb("go");
				counter++;
			}else {
				System.out.print("Received Invalid Message!");
			}
        }
		System.out.print("Counter got to " + counter);
        Task.exit(0); // Strictly optional.
    }
}