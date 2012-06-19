%% Author: Matthias
%% Created: 12.05.2012
-module(improvedFact).

%%
%% Exported Functions
%%
-export([fac/2,printFinishedFac/1,timeFac/1]).

%%
%% API Functions
%%

% This will calculate asynchronously and parallel the factorial of N and send the reply to Reply
fac(N, Reply) ->
	Adder = spawn(fun() -> adder(N, fun(A,B) -> A * B end, Reply, {none, 0}) end),
	sendAll(N, Adder).

% This will execute fac(N, Reply) and post the execution time (asynchronously)
timeFac(N) ->
	spawn(fun() ->
				  {Time, _} = timer:tc(improvedFact, printFinishedFac, [N]),
				  io:format("Time for fac ~w: ~w~n", [N, Time])
				end).

% This will execute fac(N, Reply) synchronously and post a finished Message
printFinishedFac(N) -> 
	Self = self(),
	fac(N, Self),
	receive 
		_ ->
			io:format("Finished fac for ~w~n", [N])
	end.


%%
%% Local Functions
%%

sendAll(0, _) ->
	ok;
sendAll(N, Adder) ->
	Adder ! N,
	sendAll(N-1, Adder).

% Adds all given values with the given values via tree to one value
%    15
%    /\
%  4    11
% / \   / \
% 2  2  4  7
% If AddFun is the simple addition 
% The Operator (AddFun) has to be commutative and associative as the 
% operations will executed as parallel as possible
% N Has to be the number of values we get
adder(N, AddFun, Reply, State) ->
	receive 
		Msg ->
			case {N, State} of
				{1, {none, _}} ->
					Reply ! Msg;
				{N, {none, _}} ->
					adder(N, AddFun, Reply, {some, Msg});
				{N, {some, T}} -> 
					Self = self(),
					spawn(fun() -> Self ! AddFun(T, Msg) end),
					%% Self ! AddFun(T, Msg),
					adder(N - 1, AddFun, Reply, {none, 0})
			end
	end.

