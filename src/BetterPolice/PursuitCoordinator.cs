using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.Police;
using UnityEngine;
using UnityEngine.AI;
using Point = System.Numerics.Vector3;

namespace PersonalMods.BetterPolice;

internal static class PursuitCoordinator
{
    private static readonly Dictionary<long, Squad> Squads = new();
    private static readonly Dictionary<long, RouteState> Routes = new();
    private static readonly Dictionary<long, (Point Position, float Until)> FailedWaypoints = new();

    internal static void Tick(PursuitBehaviour pursuit)
    {
        if (!Eligible(pursuit)) { Forget(pursuit.Pointer.ToInt64()); return; }
        var target = pursuit.TargetPlayer;
        var squadId = target.Pointer.ToInt64();
        var time = Time.time;
        if (!Squads.TryGetValue(squadId, out var squad)) Squads.Add(squadId, squad = new Squad());
        squad.LastTick = time;
        if (time >= squad.NextPlan)
        {
            // Advance before any native calls, which may activate another pursuit.
            squad.NextPlan = time + BetterPoliceMod.CoordinationInterval;
            Rebuild(squadId, squad, pursuit, time);
        }
        if (!TryDestination(pursuit, out var destination)) return;
        var state = Routes[pursuit.Pointer.ToInt64()];
        var movement = pursuit.Npc.Movement;
        if (time < state.NextCommand || Vector3.Distance(movement.FootPosition, destination) < 1.5f) return;
        if (movement.IsMoving && Vector3.Distance(movement.CurrentDestination, destination) < 1) return;
        state.NextCommand = time + 0.4f;
        // Normal movement owns path execution/replication. No direct agent warps,
        // arbitrary SetPath calls or movement callbacks that teleport on failure.
        movement.SetDestination(destination);
    }

    private static void Rebuild(long squadId, Squad squad, PursuitBehaviour source, float time)
    {
        var members = new Dictionary<long, PursuitBehaviour>();
        foreach (var officer in PoliceOfficer.Officers)
        {
            if (!officer || !officer.PursuitBehaviour) continue;
            var pursuit = officer.PursuitBehaviour;
            if (Eligible(pursuit) && pursuit.TargetPlayer.Pointer == source.TargetPlayer.Pointer)
                members.TryAdd(pursuit.Pointer.ToInt64(), pursuit);
        }
        var observed = members.Values.Any(x => x.IsTargetImmediatelyVisible);
        if (observed)
        {
            var target = source.TargetPlayer;
            // Actual location/velocity enter squad intel only after a real sighting.
            squad.Intel.Observe(ToPoint(target.transform.position), ToPoint(target.Velocity), time);
        }
        if (!squad.Intel.TryRead(time, BetterPoliceMod.SquadMemory, out var anchor, out var velocity))
        {
            DropSquadRoutes(squadId);
            return; // Native lost-target search and timeout continue normally.
        }
        var actors = members.Select(x => new Pursuer(x.Key, ToPoint(x.Value.Npc.Movement.FootPosition))).ToArray();
        if (actors.Length < 2) { DropSquadRoutes(squadId); return; }
        if (PursuitTactics.Horizontal(velocity).LengthSquared() >= 1)
            squad.Heading = PursuitTactics.Direction(velocity, squad.Heading);
        else if (squad.Heading.LengthSquared() < 0.1f)
            squad.Heading = PursuitTactics.Direction(anchor - actors.OrderBy(x => Point.DistanceSquared(x.Position, anchor)).First().Position, Point.UnitZ);
        var navigation = new NativeNavigation(members, time);
        var settings = new TacticsSettings(BetterPoliceMod.FlankRadiusValue, BetterPoliceMod.DetourLimit, BetterPoliceMod.FlankerCount);
        var orders = squad.Planner.Plan(actors, anchor, squad.Heading, velocity, navigation, settings);
        var ids = orders.Select(x => x.Id).ToHashSet();
        foreach (var id in Routes.Where(x => x.Value.Squad == squadId && !ids.Contains(x.Key)).Select(x => x.Key).ToArray()) Routes.Remove(id);
        foreach (var order in orders)
        {
            if (order.Waypoints.Length == 0) { Routes.Remove(order.Id); continue; }
            if (Routes.TryGetValue(order.Id, out var old) && old.Squad == squadId && old.Order.Role == order.Role
                && time - old.Created < 6 && Point.Distance(old.Anchor, anchor) < 6) continue;
            Routes[order.Id] = new RouteState(squadId, order, anchor, actors.First(x => x.Id == order.Id).Position, time);
        }
    }

    internal static bool TryDestination(PursuitBehaviour pursuit, out Vector3 destination)
    {
        destination = default;
        if (!BetterPoliceMod.CoordinationEnabled || !Eligible(pursuit)
            || !Routes.TryGetValue(pursuit.Pointer.ToInt64(), out var state)
            || state.Squad != pursuit.TargetPlayer.Pointer.ToInt64()) return false;
        if (!Squads.TryGetValue(state.Squad, out var squad)
            || !squad.Intel.TryRead(Time.time, BetterPoliceMod.SquadMemory, out _, out _)) return false;
        var movement = pursuit.Npc.Movement;
        if (!movement.CanMove || movement.IsOnLadder || !movement._agent || !movement._agent.enabled
            || !movement._agent.isOnNavMesh || movement._agent.isOnOffMeshLink) return false;
        var position = ToPoint(movement.FootPosition);
        if (pursuit.IsTargetImmediatelyVisible && Point.Distance(position, ToPoint(pursuit.TargetPlayer.transform.position)) <= 5) return false;
        while (state.Index < state.Order.Waypoints.Length - 1 && Point.Distance(position, state.Order.Waypoints[state.Index]) < 2)
            state.Index++;
        var goal = state.Order.Waypoints[state.Index];
        if (state.Order.Role != PursuitRole.Support && state.Index == state.Order.Waypoints.Length - 1
            && Point.Distance(position, goal) < 2.5f)
        {
            // The detour has delivered the officer to the chosen entrance.
            // Native combat/arrest must now be able to close the final distance.
            Routes.Remove(pursuit.Pointer.ToInt64());
            return false;
        }
        if (Point.Distance(position, state.ProgressPosition) >= 0.7f || Point.Distance(position, goal) < 2.5f)
        {
            state.ProgressPosition = position;
            state.ProgressTime = Time.time;
        }
        if (Time.time - state.ProgressTime > 5)
        {
            // A door/car/obstacle may have changed since the complete-path check.
            FailedWaypoints[pursuit.Pointer.ToInt64()] = (goal, Time.time + 8);
            Routes.Remove(pursuit.Pointer.ToInt64());
            return false;
        }
        destination = ToUnity(goal);
        return true;
    }

    private static bool Eligible(PursuitBehaviour? pursuit)
    {
        if (pursuit is null || !pursuit || !pursuit.Active || !pursuit.IsServerInitialized || !pursuit.Npc
            || pursuit.Npc.TryCast<PoliceOfficer>() is not { } officer || !officer.IsActive || officer.IgnorePlayers
            || !officer.Movement || officer.IsInVehicle || !officer.Health || !officer.IsConscious || !pursuit.TargetPlayer) return false;
        var target = pursuit.TargetPlayer;
        return !target.IsArrested && !target.IsUnconscious && target.CrimeData && (int)target.CrimeData.CurrentPursuitLevel >= 2;
    }

    internal static void Forget(long pursuit)
    {
        if (Routes.Remove(pursuit, out var route) && Squads.TryGetValue(route.Squad, out var squad)) squad.NextPlan = 0;
        FailedWaypoints.Remove(pursuit);
    }

    private static void DropSquadRoutes(long squad)
    {
        foreach (var id in Routes.Where(x => x.Value.Squad == squad).Select(x => x.Key).ToArray()) Routes.Remove(id);
    }

    internal static void Clear() { Squads.Clear(); Routes.Clear(); FailedWaypoints.Clear(); }
    internal static void Prune()
    {
        foreach (var id in Squads.Where(x => Time.time - x.Value.LastTick > 20).Select(x => x.Key).ToArray())
        {
            DropSquadRoutes(id); Squads.Remove(id);
        }
        foreach (var id in FailedWaypoints.Where(x => x.Value.Until < Time.time).Select(x => x.Key).ToArray()) FailedWaypoints.Remove(id);
    }

    private static Point ToPoint(Vector3 value) => new(value.x, value.y, value.z);
    private static Vector3 ToUnity(Point value) => new(value.X, value.Y, value.Z);
    private sealed class Squad
    {
        internal readonly PursuitIntel Intel = new();
        internal readonly PursuitTactics Planner = new();
        internal Point Heading;
        internal float NextPlan, LastTick;
    }
    private sealed class RouteState
    {
        internal readonly long Squad;
        internal readonly TacticalOrder Order;
        internal readonly Point Anchor;
        internal readonly float Created;
        internal Point ProgressPosition;
        internal float ProgressTime, NextCommand;
        internal int Index;
        internal RouteState(long squad, TacticalOrder order, Point anchor, Point position, float time)
        { Squad = squad; Order = order; Anchor = anchor; Created = ProgressTime = time; ProgressPosition = position; }
    }

    private sealed class NativeNavigation : ITacticalNavigation
    {
        private readonly Dictionary<long, NavMeshQueryFilter> Filters = new();
        private readonly NavMeshPath Path = new();
        private readonly float PlannedAt;
        internal NativeNavigation(Dictionary<long, PursuitBehaviour> members, float time)
        {
            PlannedAt = time;
            foreach (var member in members)
            {
                var movement = member.Value.Npc.Movement;
                if (!movement._agent || !movement._agent.enabled || !movement._agent.isOnNavMesh) continue;
                Filters[member.Key] = new NavMeshQueryFilter { agentTypeID = movement._agent.agentTypeID, areaMask = movement.NavMeshAreaMask };
            }
        }

        public bool TryPath(long officer, Point from, Point destination, out TacticalPath path)
        {
            path = null!;
            if (!Filters.TryGetValue(officer, out var filter)) return false;
            if (FailedWaypoints.TryGetValue(officer, out var failed) && PlannedAt < failed.Until && Point.Distance(destination, failed.Position) < 4) return false;
            if (!NavMesh.SamplePosition(ToUnity(from), out var start, 1.5f, filter)
                || !NavMesh.SamplePosition(ToUnity(destination), out var end, 1.5f, filter)
                || Math.Abs(end.position.y - destination.Y) > 2
                || !NavMesh.CalculatePath(start.position, end.position, filter, Path)
                || Path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = Path.corners;
            if (corners is null || corners.Length == 0) return false;
            path = new TacticalPath(corners.Select(ToPoint).ToArray());
            return true;
        }
    }
}
