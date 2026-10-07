using S1LSD.Core;

namespace PersonalMods.S1LSDComparison;

// Same exponential transition as the borrowed listener processor. Reset to
// dry before routing; never turn a freshly loaded mixer into a loud default.
internal sealed class ComparisonAudioState
{
    private readonly Ramp[] Values = Enumerable.Range(0, 21).Select(_ => new Ramp()).ToArray();
    internal ComparisonAudioState()
    {
        var initial = new float[] { -10000, 0, 1.2f, -10000, 0, 180, 0, 0, 1, 0, 1, 0.04f, 0.35f, 22000, 0, 0, 0, 0, 0, 0, 1 };
        for (var i = 0; i < Values.Length; i++) Values[i].Reset(i < initial.Length ? initial[i] : 0);
    }
    internal AudioTargets Step(in AudioTargets t, float delta, float transition)
    {
        var dt = float.IsFinite(delta) ? Math.Clamp(delta, 0, 0.1f) : 0;
        var seconds = Math.Clamp(transition, 2, 6);
        float S(int i, float v, float multiplier = 1) => Values[i].Step(v, dt, seconds * multiplier);
        return new AudioTargets {
            ReverbRoomMb = S(0, t.ReverbOn ? t.ReverbRoomMb : -10000), ReverbRoomHfMb = S(1, t.ReverbRoomHfMb),
            ReverbDecaySeconds = S(2, t.ReverbDecaySeconds), ReverbLevelMb = S(3, t.ReverbOn ? t.ReverbLevelMb : -10000),
            ReverbDryMb = S(4, t.ReverbOn ? t.ReverbDryMb : 0), EchoDelayMs = S(5, t.EchoDelayMs, 2),
            EchoDecay = S(6, t.EchoDecay), EchoWet = S(7, t.EchoOn ? t.EchoWet : 0), EchoDry = S(8, t.EchoOn ? t.EchoDry : 1),
            ChorusWet = S(9, t.ChorusOn ? t.ChorusWet : 0), ChorusDry = S(10, t.ChorusOn ? t.ChorusDry : 1),
            ChorusDepth = S(11, t.ChorusDepth), ChorusRateHz = S(12, t.ChorusRateHz),
            LowPassHz = S(13, t.LowPassOn ? t.LowPassHz : 22000),
            Drone = S(14, t.Drone), Shimmer = S(15, t.Shimmer), ComeUpPulse = S(16, t.ComeUpPulse),
            BadDrone = S(17, t.BadDrone), Heart = S(18, t.Heart), Whisper = S(19, t.Whisper), HeartRate = S(20, t.HeartRate)
        };
    }
}
