namespace VICE.Enforcement.Multiplayer;

// One instance per camera/player pair: one player's cooldown or lost sight
// cannot suppress or complete another player's detection.
internal sealed class CameraEvidence
{
    internal float Hits, LastSeen, CooldownUntil, Evidence;
    internal readonly HashSet<string> Labels = new(StringComparer.Ordinal);
    internal bool Step(float now, float delta, bool sighted, bool visibilityRoll, IEnumerable<string> labels, float threshold, float cooldown)
    {
        if (now - LastSeen > 120) { Labels.Clear(); Evidence = 0; }
        if (now < CooldownUntil) return false;
        if (!sighted) { Hits = Math.Max(0, Hits - delta); return false; }
        LastSeen = now;
        if (visibilityRoll) Hits += delta;
        foreach (var label in ReplicationRules.NormalizeLabels(labels)) if (!Labels.Add(label)) Evidence += delta * 0.002f;
        if (Hits < threshold) return false;
        Hits = 0; CooldownUntil = now + cooldown; return true;
    }
}
