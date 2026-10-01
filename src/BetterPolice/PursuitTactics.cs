using System.Numerics;

namespace PersonalMods.BetterPolice;

internal enum PursuitRole { Pressure, Intercept, LeftFlank, RightFlank, RearCutoff, Support }
internal sealed record Pursuer(long Id, Vector3 Position);
internal sealed record TacticalOrder(long Id, PursuitRole Role, Vector3[] Waypoints, Vector3 Approach, bool DifferentEntrance);
internal sealed record TacticsSettings(float FlankRadius = 18, float MaximumDetour = 3, int Flankers = 3, int PathBudget = 96);
internal sealed record TacticalPath(Vector3[] Corners)
{
    internal float Length => Enumerable.Range(1, Corners.Length - 1).Sum(i => Vector3.Distance(Corners[i - 1], Corners[i]));
}

internal interface ITacticalNavigation
{
    bool TryPath(long officer, Vector3 from, Vector3 destination, out TacticalPath path);
}

internal sealed class PursuitIntel
{
    private Vector3 Position, Velocity;
    private float LastSeen = float.NegativeInfinity;
    internal void Observe(Vector3 position, Vector3 velocity, float time)
    {
        if (!PursuitTactics.Finite(position) || !float.IsFinite(time)) return;
        Position = position;
        Velocity = PursuitTactics.Finite(velocity) ? PursuitTactics.Horizontal(velocity) : Vector3.Zero;
        LastSeen = time;
    }

    internal bool TryRead(float time, float memory, out Vector3 position, out Vector3 velocity)
    {
        position = Position; velocity = Velocity;
        return float.IsFinite(time) && float.IsFinite(memory) && time >= LastSeen && time - LastSeen <= Math.Max(0, memory);
    }
}

// Pure planning logic. Native navigation supplies complete, sampled paths;
// planning never writes player/NPC positions or changes combat targeting.
internal sealed class PursuitTactics
{
    private readonly Dictionary<long, PursuitRole> Previous = new();
    internal int LastPathQueries { get; private set; }

    internal TacticalOrder[] Plan(IEnumerable<Pursuer> source, Vector3 target, Vector3 heading, Vector3 velocity,
        ITacticalNavigation navigation, TacticsSettings settings)
    {
        LastPathQueries = 0;
        var officers = source.Where(x => Finite(x.Position)).GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.Id).ToList();
        if (officers.Count == 0 || !Finite(target)) { Previous.Clear(); return Array.Empty<TacticalOrder>(); }
        heading = Direction(heading, target - officers[0].Position);
        var probes = new PathProbes(navigation, Math.Clamp(settings.PathBudget, 0, 256));
        var remaining = new List<Pursuer>(officers);
        var result = new List<TacticalOrder>();
        var contact = ChooseContact(officers, target, heading);
        remaining.Remove(contact);
        result.Add(new(contact.Id, PursuitRole.Pressure, Array.Empty<Vector3>(), contact.Position, false));
        probes.Try(contact.Id, contact.Position, target, out var contactPath);
        var approaches = new List<Vector3>();
        var occupiedRoutes = new List<TacticalPath>();
        if (contactPath is not null)
        {
            approaches.Add(PointBeforeEnd(contactPath, 8));
            occupiedRoutes.Add(contactPath);
        }
        var radius = Valid(settings.FlankRadius, 10, 40, 18);
        var detour = Valid(settings.MaximumDetour, 1.5f, 6, 3);
        var lead = Clamp(Horizontal(Finite(velocity) ? velocity : Vector3.Zero) * 1.25f, 10);
        var right = new Vector3(heading.Z, 0, -heading.X);
        var roles = new[] { PursuitRole.Intercept, PursuitRole.LeftFlank, PursuitRole.RightFlank, PursuitRole.RearCutoff };
        foreach (var role in roles.Take(Math.Clamp(settings.Flankers, 0, roles.Length)))
        {
            if (remaining.Count == 0) break;
            var direction = role switch
            {
                PursuitRole.Intercept => heading,
                PursuitRole.LeftFlank => -right,
                PursuitRole.RightFlank => right,
                _ => -heading
            };
            var goal = target + lead + direction * (role == PursuitRole.Intercept ? 6 : 8);
            var chosen = remaining.OrderBy(x => Vector3.Distance(x.Position, goal)
                - (Previous.GetValueOrDefault(x.Id, PursuitRole.Support) == role ? 8 : 0)).ThenBy(x => x.Id).First();
            if (contactPath is null || !TryFlank(chosen, role, target, goal, direction, radius, detour,
                approaches, occupiedRoutes, probes, out var order, out var route)) continue;
            remaining.Remove(chosen);
            result.Add(order!);
            approaches.Add(order!.Approach);
            occupiedRoutes.Add(route!);
        }
        var supportIndex = 0;
        foreach (var officer in remaining.OrderBy(x => Vector3.DistanceSquared(x.Position, target)))
        {
            // One officer applies direct pressure. Others hold spaced points along
            // reachable approach routes if no genuinely different entrance exists.
            var waypoints = Array.Empty<Vector3>();
            if (contactPath is not null && probes.Try(officer.Id, officer.Position, target, out var direct))
            {
                var hold = PointBeforeEnd(direct!, 11 + Math.Min(supportIndex++, 6) * 3);
                if (Vector3.Distance(hold, target) >= 6 && probes.Try(officer.Id, officer.Position, hold, out var holdPath))
                    waypoints = new[] { holdPath!.Corners[^1] };
            }
            result.Add(new(officer.Id, PursuitRole.Support, waypoints, officer.Position, false));
        }
        Previous.Clear();
        foreach (var order in result) Previous[order.Id] = order.Role;
        LastPathQueries = probes.Queries;
        return result.ToArray();
    }

    private Pursuer ChooseContact(List<Pursuer> officers, Vector3 target, Vector3 heading)
    {
        float Score(Pursuer officer)
        {
            var offset = Horizontal(officer.Position - target);
            return offset.Length() + (Vector3.Dot(offset, heading) > 2 ? 8 : 0);
        }
        var nearest = officers.OrderBy(x => Vector3.DistanceSquared(x.Position, target)).ThenBy(x => x.Id).First();
        if (Vector3.Distance(nearest.Position, target) <= 5) return nearest;
        var best = officers.OrderBy(Score).ThenBy(x => x.Id).First();
        var previous = officers.FirstOrDefault(x => Previous.GetValueOrDefault(x.Id, PursuitRole.Support) == PursuitRole.Pressure);
        return previous is not null && Score(previous) <= Score(best) + 6 ? previous : best;
    }

    private static bool TryFlank(Pursuer officer, PursuitRole role, Vector3 target, Vector3 goal, Vector3 direction,
        float radius, float detour, List<Vector3> approaches, List<TacticalPath> occupiedRoutes, PathProbes probes,
        out TacticalOrder? order, out TacticalPath? route)
    {
        order = null; route = null;
        if (!probes.Try(officer.Id, officer.Position, target, out var direct)
            || !probes.Try(officer.Id, goal, target, out var finish)) return false;
        // If an officer already reached the appropriate side, continue inward;
        // refreshing the plan must not send it back to the outer waypoint.
        var gates = new List<Vector3> { goal };
        foreach (var scale in new[] { 1f, 1.6f })
            foreach (var degrees in new[] { 0, -30, 30, -60, 60 })
                gates.Add(target + Rotate(direction, degrees) * radius * scale);
        var bestScore = float.PositiveInfinity;
        foreach (var gate in gates)
        {
            if (!probes.Try(officer.Id, officer.Position, gate, out var first)
                || !probes.Try(officer.Id, first!.Corners[^1], goal, out var second)) continue;
            var approachRoute = Join(first!, second!);
            if (approachRoute.Length > direct!.Length * detour + radius || PassesTarget(approachRoute, target, 4.5f)) continue;
            var full = Join(approachRoute, finish!);
            var entrance = PointBeforeEnd(full, 8);
            if (approaches.Any(x => Horizontal(x - entrance).Length() < 6)) continue;
            var alignment = Vector3.Dot(Direction(entrance - target, direction), direction);
            if (alignment < 0.15f) continue;
            var shared = occupiedRoutes.Count == 0 ? 0 : occupiedRoutes.Max(x => SharedApproach(full, x, target));
            var score = approachRoute.Length + shared * 2 + (1 - alignment) * radius;
            if (score >= bestScore) continue;
            bestScore = score;
            var waypoint = first!.Corners[^1];
            var end = second!.Corners[^1];
            var points = Vector3.Distance(waypoint, end) < 2 ? new[] { end } : new[] { waypoint, end };
            order = new(officer.Id, role, points, entrance, true);
            route = full;
        }
        return order is not null;
    }

    internal static Vector3 PointBeforeEnd(TacticalPath path, float distance)
    {
        distance = Math.Max(0, distance);
        for (var i = path.Corners.Length - 1; i > 0; i--)
        {
            var end = path.Corners[i]; var start = path.Corners[i - 1];
            var length = Vector3.Distance(start, end);
            if (length >= distance && length > 0.001f) return Vector3.Lerp(end, start, distance / length);
            distance -= length;
        }
        return path.Corners[0];
    }

    private static TacticalPath Join(TacticalPath a, TacticalPath b) => new(a.Corners.Concat(b.Corners.Skip(1)).ToArray());
    private static bool PassesTarget(TacticalPath path, Vector3 target, float radius)
        => Enumerable.Range(1, path.Corners.Length - 1).Any(i => SegmentDistance(target, path.Corners[i - 1], path.Corners[i]) < radius);

    private static float SharedApproach(TacticalPath a, TacticalPath b, Vector3 target)
    {
        var shared = 0f;
        for (var distance = 6f; distance <= Math.Min(24, a.Length); distance += 2)
        {
            var point = PointBeforeEnd(a, distance);
            if (Horizontal(point - target).Length() > 26) continue;
            if (Enumerable.Range(1, b.Corners.Length - 1).Any(i => SegmentDistance(point, b.Corners[i - 1], b.Corners[i]) < 2)) shared += 2;
        }
        return shared;
    }

    private static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
    {
        var delta = Horizontal(b - a);
        var t = delta.LengthSquared() < 0.0001f ? 0 : Math.Clamp(Vector3.Dot(Horizontal(point - a), delta) / delta.LengthSquared(), 0, 1);
        var closest = Vector3.Lerp(a, b, t);
        return Math.Abs(closest.Y - point.Y) > 3 ? float.PositiveInfinity : Horizontal(closest - point).Length();
    }

    private static Vector3 Rotate(Vector3 value, float degrees)
    {
        var angle = degrees * MathF.PI / 180;
        return new(value.X * MathF.Cos(angle) - value.Z * MathF.Sin(angle), 0, value.X * MathF.Sin(angle) + value.Z * MathF.Cos(angle));
    }

    internal static Vector3 Horizontal(Vector3 value) => new(value.X, 0, value.Z);
    internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    internal static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value = Horizontal(value); fallback = Horizontal(fallback);
        return Finite(value) && value.LengthSquared() > 0.001f ? Vector3.Normalize(value)
            : Finite(fallback) && fallback.LengthSquared() > 0.001f ? Vector3.Normalize(fallback) : Vector3.UnitZ;
    }
    private static Vector3 Clamp(Vector3 value, float max) => value.LengthSquared() > max * max ? Vector3.Normalize(value) * max : value;
    private static float Valid(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private sealed class PathProbes
    {
        private readonly ITacticalNavigation Navigation;
        private readonly int Budget;
        private readonly Dictionary<(long, Vector3, Vector3), TacticalPath?> Cache = new();
        internal int Queries { get; private set; }
        internal PathProbes(ITacticalNavigation navigation, int budget) { Navigation = navigation; Budget = budget; }
        internal bool Try(long id, Vector3 from, Vector3 to, out TacticalPath? path)
        {
            var key = (id, from, to);
            if (Cache.TryGetValue(key, out path)) return path is not null;
            path = null;
            if (Queries < Budget && Finite(from) && Finite(to))
            {
                Queries++;
                if (Navigation.TryPath(id, from, to, out var found) && found is not null && found.Corners.Length > 0
                    && found.Corners.All(Finite) && Vector3.Distance(found.Corners[0], from) <= 3
                    && Vector3.Distance(found.Corners[^1], to) <= 3) path = found;
            }
            Cache[key] = path;
            return path is not null;
        }
    }
}
