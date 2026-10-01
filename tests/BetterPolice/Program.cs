using System.Numerics;
using PersonalMods.BetterPolice;

var assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new InvalidOperationException(message);
}
Vector3 P(float x, float z) => new(x, 0, z);
TacticalOrder[] Plan(PursuitTactics planner, ITacticalNavigation navigation, params Pursuer[] officers)
    => planner.Plan(officers, Vector3.Zero, Vector3.UnitZ, Vector3.Zero, navigation, new());

var open = new OpenNavigation();
var tactics = new PursuitTactics();
var pair = new[] { new Pursuer(1, P(0, -20)), new Pursuer(2, P(3, -22)) };
var orders = Plan(tactics, open, pair);
Check(orders.Single(x => x.Id == 1).Role == PursuitRole.Pressure, "Rear officer should maintain direct pressure.");
var interceptor = orders.Single(x => x.Id == 2);
Check(interceptor.Role == PursuitRole.Intercept && interceptor.DifferentEntrance, "Second officer should intercept from the front.");
Check(interceptor.Waypoints.Length > 0 && interceptor.Approach.Z > 0, "Interceptor needs reachable front waypoints.");
Check(Plan(tactics, open, pair.Reverse().ToArray()).All(x => orders.Single(y => y.Id == x.Id).Role == x.Role), "Enumeration order must not change assignments.");
var promoted = Plan(tactics, open, new Pursuer(2, P(3, -22)), new Pursuer(3, P(-6, -25)));
Check(promoted.Count(x => x.Role == PursuitRole.Pressure) == 1, "Contact must be replaced when the original officer leaves.");
var lone = Plan(tactics, open, new Pursuer(2, P(3, -22)));
Check(lone.Single().Waypoints.Length == 0, "Single officer must retain native pursuit.");

// Concrete obstacle topology: a room has south and north doors. Both officers
// begin south; the interceptor must actually route around the outer wall.
bool Room((int X, int Z) cell, bool northDoor)
{
    if (Math.Abs(cell.X) == 5 && Math.Abs(cell.Z) <= 10) return false;
    if (Math.Abs(cell.Z) == 10 && Math.Abs(cell.X) <= 5)
        return cell.X == 0 && (cell.Z < 0 || northDoor);
    return true;
}
var twoDoors = new GridNavigation(cell => Room(cell, true));
var roomOrders = Plan(new(), twoDoors, pair);
var around = roomOrders.Single(x => x.Id == 2);
Check(around.DifferentEntrance && around.Approach.Z > 5, "Two-door room needs a north entrance flanker.");
Check(twoDoors.TryPath(2, pair[1].Position, around.Waypoints[0], out var aroundPath), "Outer flank waypoint must be reachable.");
Check(aroundPath.Corners.Any(x => Math.Abs(x.X) > 5 && x.Z >= 10), "Flanking route must travel around the building.");
Check(aroundPath.Corners.All(x => Vector3.Distance(x, Vector3.Zero) >= 4.5f), "Flanker cannot walk past the player en route to an outer gate.");
Check(around.Waypoints.All(x => twoDoors.TryPath(2, x, Vector3.Zero, out _)), "Flanking destinations must connect back to the encounter.");
var oneDoor = new GridNavigation(cell => Room(cell, false));
var oneDoorOrders = Plan(new(), oneDoor, pair.Concat(new[] { new Pursuer(3, P(-3, -25)) }).ToArray());
Check(oneDoorOrders.All(x => !x.DifferentEntrance), "Closed rear wall must not be treated as a new entrance.");
Check(oneDoorOrders.Count(x => x.Role == PursuitRole.Support && x.Waypoints.Length == 1) == 2, "One-door room needs spaced support.");
var holds = oneDoorOrders.Where(x => x.Role == PursuitRole.Support).Select(x => x.Waypoints.Single()).ToArray();
Check(Vector3.Distance(holds[0], holds[1]) >= 2.5f, "Support must not stack in one place.");

// An isolated corridor has no way around the camper. Preserve the native lead
// officer and reject incomplete/unreachable alternate paths.
var corridor = new GridNavigation(cell => Math.Abs(cell.X) <= 1 && cell.Z <= 3);
var corridorOrders = Plan(new(), corridor, pair.Select(x => x with { Position = P(0, x.Position.Z) }).ToArray());
Check(corridorOrders.All(x => !x.DifferentEntrance), "Dead-end corridor must not invent a flank through walls.");
Check(corridorOrders.Single(x => x.Role == PursuitRole.Support).Waypoints.Length == 1, "Corridor support should keep distance.");
foreach (var invalid in new[] { InvalidNavigation.Kind.Partial, InvalidNavigation.Kind.NaN, InvalidNavigation.Kind.Empty })
{
    var rejected = Plan(new(), new InvalidNavigation(invalid), pair);
    Check(rejected.All(x => x.Waypoints.Length == 0), $"{invalid} paths must fall back to native movement.");
}
var noFlanks = new PursuitTactics().Plan(pair, Vector3.Zero, Vector3.UnitZ, Vector3.Zero, open, new(Flankers: 0));
Check(noFlanks.All(x => !x.DifferentEntrance), "Flanker setting zero disables flank routes.");
var bounded = new PursuitTactics();
bounded.Plan(Enumerable.Range(1, 30).Select(id => new Pursuer(id, P(id, -30))), Vector3.Zero, Vector3.UnitZ, Vector3.Zero, open, new(PathBudget: 8));
Check(bounded.LastPathQueries <= 8, "Many officers must respect the strict path query budget.");
Check(Plan(new(), open, new Pursuer(1, P(0, -20)), new Pursuer(1, P(0, -21))).Length == 1, "Duplicate officer IDs must be deduplicated.");
Check(Plan(new(), open, new Pursuer(9, new(float.NaN, 0, 0))).Length == 0, "Invalid officer position must be rejected.");

var intel = new PursuitIntel();
Check(!intel.TryRead(0, 6, out _, out _), "No squad intel before a real sighting.");
intel.Observe(P(4, 2), P(3, 0), 10);
Check(intel.TryRead(15, 6, out var observed, out var velocity) && observed == P(4, 2) && velocity == P(3, 0), "Sight memory retains only the last observation.");
Check(!intel.TryRead(16.01f, 6, out _, out _), "Lost-target memory must expire.");
Check(!intel.TryRead(9, 6, out _, out _), "Scene clock reset cannot reuse future observations.");
intel.Observe(new(float.NaN, 0, 0), Vector3.Zero, 16);
Check(!intel.TryRead(17, 6, out _, out _), "Invalid observations cannot renew sight memory.");

var random = new Random(473);
for (var scenario = 0; scenario < 500; scenario++)
{
    var count = random.Next(2, 18);
    var actors = Enumerable.Range(1, count).Select(id => new Pursuer(id, P(random.Next(-30, 31), random.Next(-30, 31)))).ToArray();
    var planner = new PursuitTactics();
    var planned = planner.Plan(actors, P(random.Next(-5, 6), random.Next(-5, 6)), P(random.Next(-3, 4), random.Next(-3, 4)),
        P(random.Next(-5, 6), random.Next(-5, 6)), open, new());
    Check(planned.Length == count && planned.Select(x => x.Id).Distinct().Count() == count, "Each officer gets exactly one order.");
    Check(planned.Count(x => x.Role == PursuitRole.Pressure) == 1, "Each squad needs one native pressure officer.");
    Check(planned.Count(x => x.DifferentEntrance) <= 3, "Default limit must cap flankers.");
    Check(planner.LastPathQueries <= 96, "Default planning budget must be bounded.");
    Check(planned.SelectMany(x => x.Waypoints).All(PursuitTactics.Finite), "All destinations must be finite.");
    foreach (var order in planned.Where(x => x.DifferentEntrance))
        Check(order.Waypoints.Length > 0 && order.Role != PursuitRole.Support, "Flankers need assigned movement routes.");
}
// Cooldown boundaries: higher frequency can never produce zero/negative new
// waits, overflow long native values, or lengthen the native result.
foreach (var hours in new[] { 1, 2, 3, 7, 12, 24, 100, int.MaxValue })
    foreach (var multiplier in new[] { -10f, 0.25f, 0.5f, 1f, 10f, float.NaN, float.PositiveInfinity })
    {
        var adjusted = GangTuning.Cooldown(hours, multiplier);
        Check(adjusted >= 1 && adjusted <= hours, "Gang cooldown must stay within one hour and the native wait.");
    }
Check(GangTuning.Cooldown(0, 0.5f) == 0 && GangTuning.Cooldown(-1, 0.5f) == -1, "Native sentinel cooldowns must be preserved.");
Check(GangTuning.Cooldown(int.MaxValue, 1) == int.MaxValue, "Large cooldown cannot overflow.");
Check(GangTuning.Cooldown(12, 0.5f) == 6 && GangTuning.Radius(20, 1.5f) == 30, "Default gang tuning must match documented settings.");
Check(GangTuning.Radius(float.MaxValue, 2) == float.MaxValue && GangTuning.Radius(0, 2) == 0, "Radius boundaries must remain safe.");
Console.WriteLine($"Better Police checks passed: {assertions:N0} assertions; 500 randomized squad scenarios, obstacle routes, sight memory and gang tuning.");

sealed class OpenNavigation : ITacticalNavigation
{
    public bool TryPath(long officer, Vector3 from, Vector3 destination, out TacticalPath path)
    { path = new(new[] { from, destination }); return true; }
}
sealed class InvalidNavigation(InvalidNavigation.Kind kind) : ITacticalNavigation
{
    internal enum Kind { Partial, NaN, Empty }
    public bool TryPath(long officer, Vector3 from, Vector3 destination, out TacticalPath path)
    {
        path = new(kind switch
        {
            Kind.Partial => new[] { from, from + Vector3.UnitX },
            Kind.NaN => new[] { from, new Vector3(float.NaN, 0, 0), destination },
            _ => Array.Empty<Vector3>()
        });
        return true;
    }
}
sealed class GridNavigation(Func<(int X, int Z), bool> walkable) : ITacticalNavigation
{
    public bool TryPath(long officer, Vector3 from, Vector3 destination, out TacticalPath path)
    {
        path = null!;
        var start = ((int)MathF.Round(from.X), (int)MathF.Round(from.Z));
        var goal = ((int)MathF.Round(destination.X), (int)MathF.Round(destination.Z));
        bool Valid((int X, int Z) point) => Math.Abs(point.X) <= 45 && Math.Abs(point.Z) <= 45 && walkable(point);
        if (!Valid(start) || !Valid(goal)) return false;
        var queue = new Queue<(int X, int Z)>();
        var parents = new Dictionary<(int X, int Z), (int X, int Z)> { [start] = start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (point == goal)
            {
                var corners = new List<Vector3> { new(goal.Item1, 0, goal.Item2) };
                while (point != start) { point = parents[point]; corners.Add(new(point.X, 0, point.Z)); }
                corners.Reverse(); path = new(corners.ToArray()); return true;
            }
            foreach (var next in new[] { (point.X, point.Z + 1), (point.X + 1, point.Z), (point.X - 1, point.Z), (point.X, point.Z - 1) })
                if (Valid(next) && parents.TryAdd(next, point)) queue.Enqueue(next);
        }
        return false;
    }
}
