%% Author: Matthias
%% Created: 19.06.2012
-module(erlangProcesses).
-export([counter/2]).

counter(Count, Reply) ->
	
	receive
		start ->
			Self = self(),
			Self ! go,
			Self ! stop,
			counter(0, Reply);
		go ->
			Self = self(),
			Self ! go,
			counter(Count + 1, Reply);
		stop -> 
			erlang:display(string:concat("Counter got to ", integer_to_list(Count))),
			Reply ! Count

	end.

Console = self().
Counter = spawn(fun() -> erlangProcesses:counter(0, Console) end).