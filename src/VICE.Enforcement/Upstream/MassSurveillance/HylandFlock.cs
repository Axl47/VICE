using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using UnityEngine;
using VICE.Enforcement.Multiplayer;
using static NACops.NACops;
using static NACops.MassSurveillance;

namespace NACops;

public enum ECameraType { Unidirectional, Omnidirectional }
public enum ECameraDisableAccess { None, BusinessComputer }

[RegisterTypeInIl2Cpp]
public class HylandFlockInstance : MonoBehaviour
{
    public HylandFlockInstance(IntPtr ptr) : base(ptr) { }
    public HylandFlockInstance() : base(ClassInjector.DerivedConstructorPointer<HylandFlockInstance>()) => ClassInjector.DerivedConstructorBody(this);
    public const float LINE_WIDTH_MIN = 0.0005f, LINE_WIDTH_MAX = 0.015f;
    public const int RAYCAST_STEPS = 3;
    public const float SEEN_CACHE_LIFETIME = 120;
    private readonly Dictionary<string, CameraEvidence> observations = new();
    private readonly Il2CppStructArray<RaycastHit> hits = new(32);
    private Il2CppSystem.Action<Impact>? impacted;
    private float nextCheck, noticeSpeed = 2;
    private Vector3 target;
    public string CameraId = "";
    public bool IsActive, IsBroken, IsPlayerNearby, IsOnCooldown, activeToday, isPlayerSighted, isOffline;
    public float consecutiveHits, cacheEvidenceRatio;
    public List<string> cameraSeenStateCache = new();
    public ECameraType type;
    public ECameraDisableAccess disableType;
    public Light cameraLight;
    public LineRenderer lineRenderer;
    public GameObject fxParticles;
    public PhysicsDamageable damageable;

    public void Initialize()
    {
        if (cameraLight) return;
        var head = transform.parent;
        if (type == ECameraType.Unidirectional)
        {
            // Select the original camera mesh, never a newly added Flock child.
            while (head && head.childCount > 0 && head.GetChild(0) != transform) head = head.GetChild(0);
            if (!head) throw new InvalidOperationException("Missing camera head: " + CameraId);
            transform.SetPositionAndRotation(head.position + Vector3.up * 0.04f + head.forward * 0.32f, head.rotation);
        }
        else { transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); transform.localPosition = new(0, -0.33f, 0.45f); }
        cameraLight = gameObject.AddComponent<Light>(); cameraLight.range = 0.25f; cameraLight.color = Color.blue; cameraLight.intensity = 0;
        var line = new GameObject("Camera detection line"); line.transform.SetParent(transform, false);
        lineRenderer = line.AddComponent<LineRenderer>(); lineRenderer.material = lineRendererMaterial; lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position); lineRenderer.widthMultiplier = 0; lineRenderer.forceRenderingOff = true;
        var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true;
        var box = gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(0.45f, 0.45f, 0.15f);
        damageable = gameObject.AddComponent<PhysicsDamageable>(); damageable.Rb = rb;
        impacted = (Il2CppSystem.Action<Impact>)OnCameraImpacted; damageable.OnImpacted += impacted;
    }
    public void OnDestroy()
    {
        if (damageable && impacted is not null) damageable.OnImpacted -= impacted;
        impacted = null; observations.Clear();
    }
    public void Update()
    {
        if (!EnforcementReplication.SessionReady || !cameraLight) return;
        if (EnforcementReplication.Host && registered && IsActive && !IsBroken && Time.realtimeSinceStartup >= nextCheck)
        {
            var delta = nextCheck == 0 ? 0.1f : Mathf.Clamp(Time.realtimeSinceStartup - nextCheck + 0.1f, 0, 0.25f);
            nextCheck = Time.realtimeSinceStartup + 0.1f;
            DetectPlayers(delta);
        }
        Present();
    }
    private void DetectPlayers(float delta)
    {
        noticeSpeed = surveillanceConfig.CameraNoticeSpeed;
        IsPlayerNearby = isPlayerSighted = false; consecutiveHits = 0;
        var players = Player.PlayerList.ToArray().Where(p => p && p.PlayerLoaded).ToArray();
        var connected = players.Select(p => p.PlayerCode).ToHashSet(StringComparer.Ordinal);
        foreach (var code in observations.Keys.Where(code => !connected.Contains(code)).ToArray()) observations.Remove(code);
        foreach (var player in players)
        {
            var distance = Vector3.Distance(transform.position, player.Avatar.CenterPoint);
            if (distance > surveillanceConfig.CameraActivationRange)
            {
                if (observations.TryGetValue(player.PlayerCode, out var outside)) outside.Step(Time.realtimeSinceStartup, delta, false, false, Array.Empty<string>(), noticeSpeed, surveillanceConfig.CameraNoticeCooldown);
                continue;
            }
            IsPlayerNearby = true;
            if (!observations.TryGetValue(player.PlayerCode, out var observation)) observations[player.PlayerCode] = observation = new();
            if (Time.realtimeSinceStartup < observation.CooldownUntil) continue;
            var vision = EnforcementReplication.Vision(player);
            var visible = vision is not null && RaycastPlayer(player);
            var noticed = observation.Step(Time.realtimeSinceStartup, delta, visible, visible && UnityEngine.Random.Range(0f, 90f) < vision!.Visibility,
                vision?.Labels ?? Array.Empty<string>(), noticeSpeed, surveillanceConfig.CameraNoticeCooldown);
            if (!visible) continue;
            isPlayerSighted = true;
            if (observation.Hits >= consecutiveHits) { consecutiveHits = observation.Hits; target = player.Avatar.CenterPoint; }
            if (noticed)
            {
                OnCameraFullyNoticed(player, observation.Labels.ToList(), observation.Evidence);
            }
        }
    }
    public bool RaycastPlayer(Player player)
    {
        var center = player.Avatar.CenterPoint;
        var distance = Vector3.Distance(transform.position, center);
        var angle = Vector3.Angle(transform.forward, center - transform.position);
        if (!ReplicationRules.InView(distance, angle, surveillanceConfig.CameraActivationRange, type == ECameraType.Omnidirectional)) return false;
        var eyes = player.EyePosition;
        if (Vector3.Distance(eyes, center) > 2.5f) eyes = center + Vector3.up * 0.6f;
        for (var i = 0; i < RAYCAST_STEPS; i++)
        {
            var endpoint = Vector3.Lerp(eyes, center, ReplicationRules.RayFraction(i, RAYCAST_STEPS));
            var direction = endpoint - transform.position;
            var count = Physics.RaycastNonAlloc(transform.position, direction.normalized, hits, direction.magnitude, raycastIgnoreZone, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) continue; // A saturated buffer cannot establish unobstructed sight.
            var blocked = false;
            foreach (var hit in hits.ToArray().Take(count).OrderBy(h => h.distance))
            {
                if (!hit.collider || hit.collider.transform.IsChildOf(transform.parent)) continue;
                var hitPlayer = hit.collider.GetComponentInParent<Player>();
                if (hitPlayer && hitPlayer.Pointer == player.Pointer) break;
                if ((obstacleLayer & (1 << hit.collider.gameObject.layer)) != 0) { blocked = true; break; }
            }
            if (!blocked) return true;
        }
        return false;
    }
    private void Present()
    {
        lineRenderer.forceRenderingOff = !IsActive || IsBroken || !isPlayerSighted;
        lineRenderer.SetPosition(1, target);
        lineRenderer.widthMultiplier = Mathf.Lerp(LINE_WIDTH_MIN, LINE_WIDTH_MAX, Mathf.Clamp01(consecutiveHits / Mathf.Max(0.1f, noticeSpeed)));
        cameraLight.intensity = !IsActive || IsBroken ? 0 : isPlayerSighted ? Mathf.Lerp(0.5f, 5, Mathf.Clamp01(consecutiveHits / Mathf.Max(0.1f, noticeSpeed))) : 0.5f + Mathf.PingPong(Time.realtimeSinceStartup, 2.5f);
    }
    public void ActivateInstance()
    {
        if (!EnforcementReplication.Host) return;
        activeToday = IsActive = true; IsBroken = false; ClearSeenCache();
    }
    public void DeactivateInstance() { activeToday = IsActive = IsBroken = isPlayerSighted = false; ClearSeenCache(); if (fxParticles) fxParticles.SetActive(false); Present(); }
    public void ClearSeenCache() { observations.Clear(); cameraSeenStateCache.Clear(); consecutiveHits = cacheEvidenceRatio = 0; IsPlayerNearby = IsOnCooldown = false; nextCheck = 0; }
    public void OnCameraImpacted(Impact impact)
    {
        if (!IsActive || IsBroken || impact.ImpactDamage <= 0) return;
        if (EnforcementReplication.Host) BreakAuthoritative(); else EnforcementReplication.RequestBreak(CameraId);
    }
    public void BreakAuthoritative()
    {
        if (!EnforcementReplication.Host || !IsActive || IsBroken) return;
        IsBroken = true; isPlayerSighted = false; ShowBroken();
        NetworkSingleton<LevelManager>.Instance.AddXP(50);
    }
    private void ShowBroken()
    {
        if (!fxParticles && fxSparksTemplate) { fxParticles = UnityEngine.Object.Instantiate(fxSparksTemplate, transform); fxParticles.transform.localPosition = Vector3.zero; }
        if (fxParticles) fxParticles.SetActive(IsActive && IsBroken);
    }
    [HideFromIl2Cpp]
    internal CameraState Snapshot() => new() { Id = CameraId, Active = IsActive, Broken = IsBroken, Sighted = isPlayerSighted, Progress = consecutiveHits, Target = new[] { target.x, target.y, target.z } };
    [HideFromIl2Cpp]
    internal void ApplySnapshot(CameraState state, float speed)
    {
        if (EnforcementReplication.Host) return;
        IsActive = activeToday = state.Active; IsBroken = state.Broken; isPlayerSighted = state.Sighted;
        consecutiveHits = state.Progress; noticeSpeed = speed; target = new(state.Target[0], state.Target[1], state.Target[2]);
        ShowBroken(); Present();
    }
    public void SetupCameraDisableAccess() { if (disableType != ECameraDisableAccess.None) isOffline = true; }
}
