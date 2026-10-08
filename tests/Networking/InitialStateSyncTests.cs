using PersonalMods.Networking;

internal static class InitialStateSyncTests
{
    internal static void Run(Action<bool,string> check)
    {
        var sync = new InitialStateSync();
        check(sync.Awaiting && sync.Take(0), "A newly ready guest did not request its initial state.");
        check(!sync.Take(0.1) && !sync.Take(1.9), "Initial state requests were not bounded.");
        // Model a host dropping the initial request while its player-data load
        // is still in progress, then becoming ready at a later retry.
        check(sync.Take(2) && sync.Take(4), "A dropped request permanently blocked guest state loading.");
        sync.Received();
        check(!sync.Awaiting && !sync.Take(100), "A received snapshot did not stop initial-state retries.");
        sync.Reset();
        check(sync.Take(1), "Joining another world retained the previous synchronization state.");
        check(!sync.Take(double.NaN) && !sync.Take(double.PositiveInfinity), "Invalid clock escaped the state-sync budget.");
        check(sync.Take(100) && !sync.Take(100), "Returning to the game caused a burst of initial-state requests.");
    }
}
