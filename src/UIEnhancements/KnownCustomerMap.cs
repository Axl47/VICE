using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.UI.Phone.Map;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.UIEnhancements;

internal sealed class KnownCustomerMap : IDisposable
{
    private readonly MapApp Map;
    private readonly Dictionary<IntPtr, CustomerMarker> Markers = new();
    private GameObject? Root;
    private float NextRefresh;
    private bool Disposed;
    private static bool Enabled => UIEnhancementsMod.Enabled.Value && UIEnhancementsMod.CustomerMarkers.Value;

    internal KnownCustomerMap(MapApp map) => Map = map;
    internal bool BelongsTo(MapApp map) => !Disposed && Map && Map.Pointer == map.Pointer;

    internal void Refresh()
    {
        if (Disposed) return;
        if (!Enabled || !Map || !Map.PoIContainer) { Clear(); return; }
        if ((!Map.isOpen && !PlayerMinimap.Ready) || !NPCManager.InstanceExists) return;
        var prefab = NPCManager.Instance.NPCPoIPrefab;
        if (!prefab || !prefab.UIPrefab) throw new InvalidOperationException("The native portrait marker prefab is missing.");
        if (Customer.UnlockedCustomers is null) return;
        var seen = new HashSet<IntPtr>();
        // This native list excludes potential customers whose locations are still zones.
        foreach (var customer in Customer.UnlockedCustomers)
        {
            try
            {
                if (!customer) continue;
                var npc = customer.NPC;
                if (!IsEligible(npc) || !seen.Add(npc.Pointer)) continue;
                if (Markers.TryGetValue(npc.Pointer, out var old) && !old.IsAlive)
                {
                    old.Dispose();
                    Markers.Remove(npc.Pointer);
                }
                if (!Markers.ContainsKey(npc.Pointer))
                {
                    if (!Root) Root = new GameObject("Personal known-customer markers");
                    Markers.Add(npc.Pointer, CustomerMarker.Create(prefab, npc, Root!.transform));
                }
            }
            catch (Exception error) { ModSupport.WarnOnce("ui:map-customer", $"A customer marker could not be created: {error.Message}"); }
        }
        foreach (var key in Markers.Keys.Where(key => !seen.Contains(key)).ToArray()) Remove(key);
        NextRefresh = Time.realtimeSinceStartup + 1f;
        UpdatePositions();
    }

    internal void Tick()
    {
        if (Disposed) return;
        if (!Enabled || !Map || !Map.PoIContainer) { Clear(); return; }
        if (!Map.isOpen && !PlayerMinimap.Ready) return;
        if (Time.realtimeSinceStartup >= NextRefresh) Refresh();
        else UpdatePositions();
    }

    private void UpdatePositions()
    {
        foreach (var pair in Markers.ToArray())
        {
            try
            {
                if (!pair.Value.IsAlive || !IsEligible(pair.Value.NPC)) { Remove(pair.Key); continue; }
                pair.Value.UpdatePosition();
            }
            catch (Exception error)
            {
                Remove(pair.Key);
                ModSupport.WarnOnce("ui:map-position", $"A customer map position was unavailable: {error.Message}");
            }
        }
    }

    private static bool IsEligible(NPC? npc) => npc is not null && npc && npc.IsActive && npc.RelationData is not null
        && npc.RelationData.Unlocked && (!npc.Health || !npc.Health.IsDead);

    private void Remove(IntPtr key)
    {
        if (Markers.Remove(key, out var marker)) marker.Dispose();
    }

    private void Clear()
    {
        foreach (var marker in Markers.Values) marker.Dispose();
        Markers.Clear();
        if (Root) Object.Destroy(Root);
        Root = null;
        NextRefresh = 0;
    }

    public void Dispose() { if (Disposed) return; Clear(); Disposed = true; }
}

internal sealed class CustomerMarker : IDisposable
{
    internal readonly NPC NPC;
    private readonly GameObject Holder;
    private readonly NPCPoI Poi;
    private Vector3 IconScale;
    private bool ScaleSaved;
    internal bool IsAlive => NPC && Holder && Poi;

    private CustomerMarker(NPC npc, GameObject holder, NPCPoI poi) { NPC = npc; Holder = holder; Poi = poi; }

    internal static CustomerMarker Create(NPCPoI prefab, NPC npc, Transform parent)
    {
        var holder = new GameObject("Known customer portrait");
        holder.SetActive(false);
        holder.transform.SetParent(parent, false);
        try
        {
            // Configure while inactive so native OnEnable receives valid NPC/name data.
            var poi = Object.Instantiate(prefab, holder.transform);
            poi.AutoUpdatePosition = false;
            poi.Rotate = false;
            poi.MainTextVisibility = POI.TextShowMode.OnHover;
            poi.SetNPC(npc);
            poi.SetMainText(npc.FullName);
            var marker = new CustomerMarker(npc, holder, poi);
            marker.UpdatePosition();
            poi.gameObject.SetActive(true);
            holder.SetActive(true); // Native creation registers mouse/controller navigation.
            marker.UpdatePosition();
            return marker;
        }
        catch
        {
            holder.SetActive(false);
            Object.Destroy(holder);
            throw;
        }
    }

    internal void UpdatePosition()
    {
        var position = NPC.transform.position;
        // Native indoor NPCs are hidden at their entrance; use that entrance explicitly.
        if (NPC.isInBuilding && NPC.LastEnteredDoor) position = NPC.LastEnteredDoor.transform.position;
        if (!float.IsFinite(position.x) || !float.IsFinite(position.z)) return;
        Poi.transform.position = position;
        if (!Poi.UI) return; // Native UI may finish initializing on the next frame.
        Poi.UpdatePosition(); // Uses the same projection/zoom container as other map pins.
        if (!Poi.IconContainer) return;
        if (!ScaleSaved) { IconScale = Poi.IconContainer.localScale; ScaleSaved = true; }
        var configured = UIEnhancementsMod.MarkerScale.Value;
        var scale = float.IsFinite(configured) ? Math.Clamp(configured, 0.35f, 1.5f) : 0.75f;
        Poi.IconContainer.localScale = IconScale * scale;
    }

    public void Dispose()
    {
        if (!Holder) return;
        try
        {
            Holder.SetActive(false); // Native OnDisable deregisters and destroys only this pin.
            Object.Destroy(Holder);
        }
        catch (Exception error) { ModSupport.WarnOnce("ui:map-cleanup", $"Customer marker cleanup failed: {error.Message}"); }
    }
}
