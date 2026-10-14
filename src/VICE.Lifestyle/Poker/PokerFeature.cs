using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppInterop.Runtime;
using MelonLoader;
using S1API.Building;
using S1API.Lifecycle;
using UnityEngine;
using UnityEngine.Events;

namespace VICE.Lifestyle.Poker;

internal sealed class PokerFeature
{
    private sealed class Pending
    {
        internal Player Player = null!;
        internal PokerCommand Command = null!;
        internal double Before, After, Created, Retry;
        internal bool Committed;
    }
    internal static PokerFeature? Current;
    internal bool Ready;
    internal PokerPanel? Panel;
    internal readonly Dictionary<string, PokerView> Views = new();
    internal readonly Dictionary<string, PokerSnapshot> Snapshots = new();
    private PokerJournal? Journal;
    internal Dictionary<string, PokerTable> Tables => Journal?.Document.Tables ?? Empty;
    private readonly Dictionary<string, PokerTable> Empty = new();
    private readonly Dictionary<string, Pending> Transfers = new();
    private readonly Dictionary<string, double> RefundRetry = new();
    private readonly VICE.Core.SubscriptionScope Subscriptions = new();
    private MelonPreferences_Entry<bool> Enabled = null!;
    private MelonPreferences_Entry<int> Timeout = null!;
    private double NextScan, NextPublish;
    private bool Saving, Failed, RecoveryBlocked;
    private double SaveStartedAt;
    private string DeferredSave = "";
    private string SaveTarget = "";
    internal int DefaultTimeout => Math.Clamp(Timeout.Value, 10, 300);
    internal void Initialize(HarmonyLib.Harmony harmony)
    {
        Current = this; var category = MelonPreferences.CreateCategory("VICE.Lifestyle.Poker");
        Enabled = ModSupport.Entry(category, "Enabled", true, "Sell the $900 placeholder cash poker table at Herbert's boutique. Keep definitions for saved furniture.");
        Timeout = ModSupport.Entry(category, "TurnTimeoutSeconds", 60, "Casual timeout 10–300 real seconds; check when possible, otherwise fold.");
        PokerWallet.Install(harmony);
        var hooks = new[]
        {
            ModSupport.Patch(harmony, typeof(InteractableObject), "StartInteract", typeof(PokerFeature), prefix: nameof(Interact)),
            ModSupport.Patch(harmony, typeof(BuildableItem), "CanBeDestroyed", typeof(PokerFeature), prefix: nameof(CanRemove)),
            ModSupport.Patch(harmony, typeof(BuildableItem), "Destroy", typeof(PokerFeature), prefix: nameof(Remove)),
            ModSupport.Patch(harmony, typeof(BuildableItem), "RpcLogic___Destroy_Server_2166136261", typeof(PokerFeature), prefix: nameof(Remove)),
            ModSupport.Patch(harmony, typeof(SaveManager), "Save", typeof(PokerFeature), prefix: nameof(SaveStarted), parameters: new[] { typeof(string) })
        };
        if (hooks.Any(ok => !ok)) throw new InvalidOperationException("Required poker interaction, removal or save hook is unavailable.");
        GameLifecycle.OnPreLoad += Preload; GameLifecycle.OnLoadComplete += Loaded; GameLifecycle.OnPreSceneChange += Clear; GameLifecycle.OnSaveComplete += Saved;
        BuildEvents.OnBuildableItemInitialized += Restore;
        Subscriptions.Add(() => GameLifecycle.OnPreLoad -= Preload); Subscriptions.Add(() => GameLifecycle.OnLoadComplete -= Loaded);
        Subscriptions.Add(() => GameLifecycle.OnPreSceneChange -= Clear); Subscriptions.Add(() => GameLifecycle.OnSaveComplete -= Saved);
        Subscriptions.Add(() => BuildEvents.OnBuildableItemInitialized -= Restore);
    }
    private static bool Interact(InteractableObject __instance)
    {
        var item = __instance.GetComponentInParent<BuildableItem>();
        if (!item || item.ItemInstance?.ID != PokerContent.ItemId) return true;
        Current?.Panel?.Open(item.GUID.ToString()); return false;
    }
    private static void Restore(BuildEventArgs args)
    {
        if (args.ItemId != PokerContent.ItemId || !args.GameObject) return;
        try { PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(args.GameObject.GetComponent<BuildableItem>()); }
        catch (Exception e) { ModSupport.WarnOnce("poker:restore", e.Message); }
    }
    private static bool Owed(BuildableItem item)
    {
        if (item.ItemInstance?.ID != PokerContent.ItemId) return false;
        var id = item.GUID.ToString(); var f = Current;
        if (f is null || !f.Ready || f.Failed) return true;
        return f.Transfers.Values.Any(p => p.Command.Table == id) || (PokerNetwork.Host ? f.Tables.GetValueOrDefault(id)?.Held > 0 : f.Snapshots.GetValueOrDefault(id) is not {} state || state.Pot > 0 || state.Seats.Any(s => s.Stack > 0 || s.Wager > 0));
    }
    private static bool CanRemove(BuildableItem __instance, ref string reason, ref bool __result)
    { if (!Owed(__instance)) return true; reason = "Poker table holds unsettled cash. Cash out every stack first."; __result = false; return false; }
    private static bool Remove(BuildableItem __instance) => !Owed(__instance);
    private static bool SaveStarted(string saveFolderPath)
    {
        var f = Current; if (f is not null && f.Failed && (f.Journal is not null || f.RecoveryBlocked) && PokerNetwork.Host) return false;
        if (f?.Ready != true || !PokerNetwork.Host) return true;
        if (f.Transfers.Count > 0) { f.DeferredSave = saveFolderPath; return false; }
        if (f.Failed) return false;
        try
        {
            f.Journal?.Save(); f.SaveTarget = string.IsNullOrWhiteSpace(saveFolderPath) ? f.Journal!.Root : System.IO.Path.GetFullPath(saveFolderPath);
            f.Journal!.CheckpointTo(f.SaveTarget); f.Saving = true; f.SaveStartedAt = PokerNetwork.Now; return true;
        }
        catch (Exception e) { f.Stop(e); return false; }
    }
    private void Saved()
    {
        if (Saving && Ready && PokerNetwork.Host)
        {
            foreach (var table in Tables.Values.Where(t => t.Active)) table.Deadline += Math.Max(0, PokerNetwork.Now - SaveStartedAt);
            Persist(() => { Journal?.Save(); if (SaveTarget.Length > 0) Journal?.CheckpointTo(SaveTarget); });
        }
        Saving = false; SaveTarget = "";
    }
    private void Preload() { Clear(); Failed = false; try { PokerContent.Register(); } catch (Exception e) { Failed = true; MelonLogger.Error("Poker registration: " + e); } }
    private void Loaded()
    {
        if (Failed) return;
        try
        {
            PokerNetwork.Bind(); PokerContent.Register(); if (Enabled.Value) PokerContent.Shops();
            if (PokerNetwork.Host)
            {
                var root = LoadManager.Instance.LoadedGameFolderPath;
                if (string.IsNullOrWhiteSpace(root)) throw new InvalidDataException("Poker requires a persistent loaded save.");
                RecoveryBlocked = true; Journal = new(root); RecoveryBlocked = false;
            }
            Ready = true; Scan(); Panel = new(this); PokerNetwork.PublishAll();
            MelonLogger.Msg("POKER_READY: $900 Cash Poker Table at Herbert's; host-authoritative 2–4 player Hold'em with per-save escrow journal.");
        }
        catch (Exception e) { Failed = true; Ready = false; MelonLogger.Error("Poker stopped; saved settlements preserved: " + e); }
    }
    internal bool CanUse(Player player, string id) => Views.TryGetValue(id, out var view) && view.Item && view.Item.Initialized && !view.Item.isGhost && !view.Item.IsDestroyed
        && view.Item.ParentProperty && view.Item.ParentProperty.IsOwned && player && player.PlayerLoaded && !player.IsSleeping && player.Health && player.Health.IsAlive
        && Vector3.Distance(player.transform.position, view.Visual.position) <= 5;
    private string Key(Player player, PokerCommand command) => player.PlayerCode + "/" + command.Token;
    internal void Execute(Player player, PokerCommand command)
    {
        if (!PokerNetwork.Host || Journal is null || Failed) throw new InvalidOperationException("Host settlement ledger is unavailable.");
        if (string.IsNullOrWhiteSpace(player.PlayerCode) || Player.PlayerList.ToArray().Count(p => p && p.PlayerLoaded && p.PlayerCode == player.PlayerCode) != 1)
            throw new InvalidDataException("Your authenticated persistent player identity is unavailable.");
        var key = Key(player, command);
        if (command.Action is "reserved" or "applied")
        {
            try { WalletReply(player, command, key); }
            catch (Exception) when (!Failed && Transfers.TryGetValue(key, out var transfer) && !transfer.Committed)
            { Transfers.Remove(key); ResumeClock(transfer); RefundRetry[player.PlayerCode] = PokerNetwork.Now + 30; PokerNetwork.Wallet(player, "release", transfer.Command); throw; }
            return;
        }
        if (!Journal.First(key)) return;
        if (Saving || SaveManager.Instance.IsSaving) throw new InvalidOperationException("Native save in progress. Try again shortly.");
        if (!Tables.TryGetValue(command.Table, out var table)) throw new InvalidOperationException("This table belongs to another session.");
        PokerRequests.Check(table, command, PokerNetwork.Epoch);
        if (command.Action is not ("leave" or "cashout") && !CanUse(player, command.Table)) throw new InvalidOperationException("Use a nearby table in an owned property while awake and alive.");
        if (Transfers.Values.Any(p => p.Command.Table == command.Table || p.Player.PlayerCode == player.PlayerCode)) throw new InvalidOperationException("Wait for the current wallet transfer.");
        if (command.Action == "buyin")
        {
            if (Tables.Values.Any(t => t.Id != table.Id && t.SeatOf(player.PlayerCode) >= 0)) throw new InvalidOperationException("Cash out your other table first.");
            var preview = table.Copy(); preview.Add(player.PlayerCode, player.PlayerName, command.Seat, command.Amount);
            Begin(player, command, key); return;
        }
        if (command.Action == "cashout")
        {
            table.Leave(player.PlayerCode, PokerNetwork.Now); Persist(() => Journal.Mark(key));
            // A new transaction token cashes out safely after the hand; this token only queues departure.
            return;
        }
        var after = table.Copy();
        switch (command.Action)
        {
            case "configure": after.Configure(player.PlayerCode, command.Small, command.Big, command.Minimum, command.Maximum, DefaultTimeout); break;
            case "start": after.Start(player.PlayerCode, PokerNetwork.Now); break;
            case "leave": after.Leave(player.PlayerCode, PokerNetwork.Now); break;
            default: after.Act(player.PlayerCode, command.Action, command.Amount, PokerNetwork.Now); break;
        }
        Tables[after.Id] = after; Persist(() => Journal.Mark(key));
    }
    private void Stop(Exception error)
    {
        Failed = true; Ready = false; MelonLogger.Error("Poker settlement stopped; reload to recover the preserved journal: " + error);
        foreach (var pending in Transfers.Values) if (pending.Player && pending.Player.Owner.Authenticated) PokerNetwork.Wallet(pending.Player, "release", pending.Command);
        PokerWallet.Release(); Panel?.Abort();
    }
    private void Persist(Action write)
    { try { write(); } catch (Exception e) { Stop(e); throw; } }
    private void Begin(Player player, PokerCommand command, string key)
    {
        var pending = new Pending { Player = player, Command = command, Created = PokerNetwork.Now, Retry = PokerNetwork.Now + 1 };
        Transfers[key] = pending; PokerNetwork.Wallet(player, "reserve", command);
    }
    private void WalletReply(Player player, PokerCommand command, string key)
    {
        if (!Transfers.TryGetValue(key, out var pending) || pending.Player.Pointer != player.Pointer) return;
        if (command.Action == "applied")
        {
            if (!pending.Committed || command.Cash != pending.After) throw new InvalidDataException("Wallet acknowledgement differs from the host settlement.");
            UpdateReplica(player, pending.After);
            ResumeClock(pending);
            Transfers.Remove(key); PokerNetwork.Wallet(player, "release", pending.Command); return;
        }
        if (pending.Committed) { PokerNetwork.Wallet(player, "apply", pending.Command, pending.Before, pending.After); return; }
        var table = Tables[pending.Command.Table];
        var live = player.GetInventoryString();
        // The native host replica is fed by the authenticated owner's inventory RPC, not MoneyManager.
        var mirror = PokerInventory.Cash(live);
        if (player.Owner.IsLocalClient) { mirror = PlayerInventory.Instance.cashInstance.Balance; live = PokerInventory.WithCash(live, mirror); }
        if (mirror != command.Cash) throw new InvalidOperationException("Your wallet replica is catching up. Retry the buy-in.");
        var after = table.Copy(); long delta;
        after.Deadline += PokerNetwork.Now - pending.Created;
        if (pending.Command.Action == "buyin") { after.Add(player.PlayerCode, player.PlayerName, pending.Command.Seat, pending.Command.Amount); delta = -pending.Command.Amount; }
        else delta = after.CashOut(player.PlayerCode, PokerInventory.CashOutAmount(mirror, after.Seats[after.SeatOf(player.PlayerCode)].Stack));
        pending.Before = mirror; pending.After = PokerInventory.Target(mirror, delta);
        try
        {
            Journal!.Transfer(key, player.PlayerCode, System.IO.Path.Combine("Players", player.SaveFolderName, "Inventory.json"), live, after, delta);
            pending.Committed = true;
            pending.Created = PokerNetwork.Now;
            // Ensure a subsequent native save cannot serialize the pre-transfer host replica.
            if (!player.Owner.IsLocalClient) UpdateReplica(player, pending.After);
        }
        catch (Exception e) { Stop(e); throw; }
        PokerNetwork.Wallet(player, "apply", pending.Command, pending.Before, pending.After);
    }
    private static void UpdateReplica(Player player, double balance)
    {
        if (player._inventory.Length != PokerInventory.PocketSlot + 1) throw new InvalidDataException("Native player inventory layout changed.");
        var slot = player._inventory[PokerInventory.PocketSlot];
        var cash = slot.ItemInstance?.TryCast<Il2CppScheduleOne.ItemFramework.CashInstance>();
        if (cash is null)
        {
            cash = Il2CppScheduleOne.Registry.GetItem("cash")!.GetDefaultInstance(1).TryCast<Il2CppScheduleOne.ItemFramework.CashInstance>()
                ?? throw new InvalidDataException("Native cash definition did not create a cash instance.");
            PokerWallet.SetReplica(cash, balance); slot.SetStoredItem(cash, false);
        }
        else PokerWallet.SetReplica(cash, balance);
    }
    private void ResumeClock(Pending pending)
    {
        if (!Tables.TryGetValue(pending.Command.Table, out var table) || !table.Active) return;
        table.Deadline += Math.Max(0, PokerNetwork.Now - pending.Created); table.Revision++; Persist(() => Journal!.Save());
    }
    internal void SetSnapshots(List<PokerSnapshot> tables)
    {
        Snapshots.Clear(); foreach (var table in tables) { if (table.Seats.Length != 4 || table.Board.Count > 5) throw new InvalidDataException("Invalid poker table state."); Snapshots[table.Id] = table; }
    }
    internal void OnUpdate()
    {
        if (Failed && Journal is not null) { PokerNetwork.Tick(); return; }
        if (!Ready || !LoadManager.Instance || LoadManager.Instance.IsLoading || !LoadManager.Instance.IsGameLoaded) return;
        try
        {
            PokerNetwork.Tick(); Panel?.Tick(); var now = PokerNetwork.Now;
            if (now >= NextScan) { NextScan = now + .5; Scan(); if (Enabled.Value) PokerContent.Shops(); }
            if (PokerNetwork.Host && !Saving && !SaveManager.Instance.IsSaving)
            {
                foreach (var pair in Transfers.ToArray())
                {
                    var pending = pair.Value;
                    if (!pending.Player || !pending.Player.PlayerLoaded)
                    { Transfers.Remove(pair.Key); ResumeClock(pending); if (pending.Committed) Tables[pending.Command.Table].Leave(pending.Player ? pending.Player.PlayerCode : pair.Key.Split('/')[0], now); Persist(() => Journal!.Save()); continue; }
                    if (!pending.Committed && now - pending.Created > 5)
                    { Transfers.Remove(pair.Key); ResumeClock(pending); RefundRetry[pending.Player.PlayerCode] = now + 30; PokerNetwork.Wallet(pending.Player, "release", pending.Command); continue; }
                    if (now >= pending.Retry)
                    { pending.Retry = now + 1; PokerNetwork.Wallet(pending.Player, pending.Committed ? "apply" : "reserve", pending.Command, pending.Before, pending.After); }
                }
                var changed = false;
                foreach (var table in Tables.Values.ToArray())
                {
                    if (Transfers.Values.Any(p => p.Command.Table == table.Id)) continue;
                    var revision = table.Revision;
                    foreach (var seat in table.Seats.Where(s => s.Player.Length > 0 && !s.Leaving).ToArray())
                    {
                        var player = Player.PlayerList.ToArray().FirstOrDefault(p => p && p.PlayerLoaded && p.PlayerCode == seat.Player);
                        if (!player || !CanUse(player!, table.Id)) table.Leave(seat.Player, now);
                    }
                    table.TimeoutTurn(now); changed |= revision != table.Revision;
                    foreach (var seat in table.Seats.Where(s => s.Leaving && (!table.Active || !s.InHand)).ToArray())
                    {
                        var player = Player.PlayerList.ToArray().FirstOrDefault(p => p && p.PlayerLoaded && p.PlayerCode == seat.Player);
                        if (!player || player!.IsSleeping || !player.Health || !player.Health.IsAlive || Transfers.Count > 0 || RefundRetry.GetValueOrDefault(seat.Player) > now) continue;
                        if (seat.Stack == 0) { table.CashOut(seat.Player); changed = true; continue; }
                        var command = new PokerCommand { Epoch = PokerNetwork.Epoch, Table = table.Id, Instance = table.Instance, Revision = table.Revision, Action = "cashout", Seat = table.SeatOf(seat.Player) };
                        Begin(player!, command, Key(player!, command)); break;
                    }
                }
                if (changed) Persist(() => Journal!.Save());
                if (DeferredSave.Length > 0 && Transfers.Count == 0) { var path = DeferredSave; DeferredSave = ""; SaveManager.Instance.Save(path); }
            }
            if (now >= NextPublish && PokerNetwork.Host) { NextPublish = now + .5; PokerNetwork.PublishAll(); }
            foreach (var view in Views.Values) if (Snapshots.TryGetValue(view.Id, out var state)) view.Tick(state, Panel?.Table == view.Id && Panel.Peek);
        }
        catch (Exception e) { ModSupport.WarnOnce("poker:update:" + e.GetType().Name, "Poker: " + e); Panel?.Notice(PokerNetwork.Safe(e)); }
    }
    private void Scan()
    {
        var found = new HashSet<string>();
        foreach (var property in Property.Properties.ToArray())
        {
            if (!property) continue;
            foreach (var item in property.BuildableItems.ToArray())
            {
                if (!item || !item.Initialized || item.isGhost || item.IsDestroyed || item.ItemInstance?.ID != PokerContent.ItemId) continue;
                var id = item.GUID.ToString(); found.Add(id); if (Views.ContainsKey(id) || Views.Count >= 32) continue;
                PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(item); Views[id] = new(item);
                if (PokerNetwork.Host && !Tables.ContainsKey(id))
                {
                    if (Tables.Count >= 32) throw new InvalidOperationException("Cash out old poker tables before placing more (32 tracked tables).");
                    Tables[id] = new() { Id = id, Timeout = Math.Clamp(Timeout.Value, 10, 300) }; Persist(() => Journal!.Save());
                }
                var interactions = item.GetComponentsInChildren<InteractableObject>(true);
                if (!interactions.Any(i => i.gameObject.activeInHierarchy))
                {
                    var interaction = item.gameObject.AddComponent<InteractableObject>(); interaction.onHovered = new UnityEvent(); interaction.onInteractStart = new UnityEvent(); interaction.onInteractEnd = new UnityEvent();
                    interaction.MaxInteractionRange = 3; interaction.RequiresUniqueClick = true; interaction.displayLocationCollider = item.BoundingCollider;
                }
                foreach (var interaction in item.GetComponentsInChildren<InteractableObject>(true))
                {
                    interaction.SetMessage("Play cash poker"); interaction.onInteractStart = new UnityEvent();
                    interaction.onInteractStart.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => Current?.Panel?.Open(id)))!);
                }
            }
        }
        foreach (var id in Views.Keys.Except(found).ToArray())
        { Views[id].Dispose(); Views.Remove(id); if (PokerNetwork.Host && Tables.TryGetValue(id, out var table)) { table.AbortForRecovery(); if (table.Held == 0) Tables.Remove(id); Persist(() => Journal!.Save()); } }
    }
    private void Clear()
    {
        Panel?.Dispose(); Panel = null;
        if (PokerNetwork.Host && Journal is not null && !Failed)
        { try { foreach (var table in Tables.Values) table.AbortForRecovery(); Journal.Save(); } catch (Exception e) { MelonLogger.Error("Poker shutdown journal: " + e); } }
        PokerWallet.Release(); foreach (var view in Views.Values) view.Dispose(); Views.Clear(); Snapshots.Clear();
        Ready = false; Journal = null; Transfers.Clear(); RefundRetry.Clear(); PokerNetwork.Reset(); Saving = RecoveryBlocked = false; SaveTarget = DeferredSave = ""; NextScan = NextPublish = 0;
    }
    internal void OnDeinitializeMelon() { Clear(); Subscriptions.Dispose(); Current = null; }
}
