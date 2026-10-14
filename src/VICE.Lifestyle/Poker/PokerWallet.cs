using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace VICE.Lifestyle.Poker;

// Only the owner changes the native local cash instance. The host never substitutes its own wallet.
internal static class PokerWallet
{
    private sealed record Controls(PlayerCamera Camera, PlayerMovement Movement, PlayerInventory Inventory, bool Move, bool Look, bool Hotbar, bool Equip, bool Typing, bool CursorVisible, CursorLockMode CursorLock);
    private static PresentationLease<Controls>? Lease;
    private static PokerCommand? Request;
    private static double Reserved;
    private static bool Applied, NativeApply;
    private static long AppliedInstance;
    private static double QueuedDelta;
    internal static bool Locked => Lease is not null;
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        if (!ModSupport.Patch(harmony, typeof(CashInstance), "ChangeBalance", typeof(PokerWallet), prefix: nameof(Change))) throw new InvalidOperationException("Cash reservation hook unavailable.");
        if (!ModSupport.Patch(harmony, typeof(CashInstance), "SetBalance", typeof(PokerWallet), prefix: nameof(Set))) throw new InvalidOperationException("Cash reservation hook unavailable.");
    }
    private static bool OurCash(CashInstance instance) => Locked && !NativeApply && PlayerInventory.Instance && PlayerInventory.Instance.cashInstance?.Pointer == instance.Pointer;
    private static bool Change(CashInstance __instance, float amount)
    { if (!OurCash(__instance)) return true; QueuedDelta += amount; return false; }
    private static bool Set(CashInstance __instance, float newBalance)
    { if (!OurCash(__instance)) return true; QueuedDelta = newBalance - __instance.Balance; return false; }
    internal static void SetReplica(CashInstance cash, double balance)
    {
        NativeApply = true;
        try { cash.SetBalance((float)balance, true); }
        finally { NativeApply = false; }
    }
    internal static void Receive(PokerMessage message)
    {
        var command = message.Request ?? throw new InvalidDataException("Wallet command missing.");
        if (message.Wallet == "release") { if (Request?.Token == command.Token) Release(); else PokerFeature.Current?.Panel?.RequestFinished(command.Token); return; }
        if (message.Wallet == "reserve")
        {
            if (Request is not null && Request.Token != command.Token) throw new InvalidOperationException("Your wallet is already reserved.");
            if (Request is null)
            {
                if (!Player.Local || !Player.Local.PlayerLoaded || !Player.Local.Health.IsAlive || Player.Local.IsSleeping || PlayerInventory.Instance?.cashInstance is null)
                    throw new InvalidOperationException("Your wallet is unavailable.");
                var camera = PlayerCamera.Instance;
                var pokerView = camera && camera.ActiveUIElements.Contains("VICEPoker");
                if (!camera || !PlayerMovement.Instance || camera.ActiveUIElementCount > 0 && !(pokerView && camera.ActiveUIElementCount == 1)
                    || GameInput.IsTyping && !pokerView)
                    throw new InvalidOperationException("Close other menus or chat before moving poker cash.");
                Lease = new(() =>
                {
                    var c = PlayerCamera.Instance; var i = PlayerInventory.Instance!; var m = PlayerMovement.Instance;
                    return new(c, m, i, m._canMove, c.CanLook, i.HotbarEnabled, i.EquippingEnabled, GameInput.IsTyping, Cursor.visible, Cursor.lockState);
                }, () =>
                {
                    PlayerCamera.Instance.AddActiveUIElement("VICEPokerWallet"); PlayerCamera.Instance.SetCanLook(false); PlayerCamera.Instance.FreeMouse(true);
                    PlayerMovement.Instance._canMove = false; PlayerInventory.Instance!.SetInventoryEnabled(false); GameInput.IsTyping = true;
                }, s =>
                {
                    if (s.Camera) s.Camera.RemoveActiveUIElement("VICEPokerWallet");
                    if (s.Camera && s.Camera.ActiveUIElementCount == 0)
                    {
                        s.Camera.SetCanLook(s.Look); if (s.Movement) s.Movement._canMove = s.Move;
                        if (s.Inventory) { s.Inventory.SetInventoryEnabled(s.Hotbar); s.Inventory.SetEquippingEnabled(s.Equip); }
                        GameInput.IsTyping = s.Typing; Cursor.visible = s.CursorVisible; Cursor.lockState = s.CursorLock;
                    }
                });
                Request = command; Reserved = PlayerInventory.Instance!.cashInstance.Balance; Applied = false; AppliedInstance = 0;
            }
            var reply = Clone(command); reply.Action = "reserved"; reply.Cash = Reserved; PokerNetwork.Submit(reply); return;
        }
        if (message.Wallet != "apply" || Request?.Token != command.Token || message.Before != Reserved || !double.IsFinite(message.After))
            throw new InvalidDataException("Wallet transfer has no matching reservation.");
        var cash = PlayerInventory.Instance.cashInstance;
        if (!Applied)
        {
            if (cash.Balance != message.Before) throw new InvalidDataException("Reserved wallet changed. Reload for durable settlement recovery.");
            NativeApply = true;
            try
            {
                if (PlayerInventory.Instance.cashSlot.ItemInstance?.Pointer != cash.Pointer) PlayerInventory.Instance.cashSlot.SetStoredItem(cash, true);
                cash.SetBalance((float)message.After, true);
            }
            finally { NativeApply = false; }
            if (cash.Balance != message.After) throw new InvalidDataException("Native wallet clamped the transfer. Reload for recovery.");
            Applied = true; AppliedInstance = cash.Pointer.ToInt64();
        }
        if (cash.Pointer.ToInt64() != AppliedInstance) throw new InvalidDataException("Wallet owner changed during transfer.");
        var ack = Clone(command); ack.Action = "applied"; ack.Cash = cash.Balance; PokerNetwork.Submit(ack);
    }
    private static PokerCommand Clone(PokerCommand command) => System.Text.Json.JsonSerializer.Deserialize<PokerCommand>(System.Text.Json.JsonSerializer.Serialize(command))!;
    internal static void Release()
    {
        try
        {
            if (QueuedDelta != 0 && PlayerInventory.Instance && PlayerInventory.Instance.cashInstance is { } cash)
            { NativeApply = true; cash.ChangeBalance((float)QueuedDelta); }
        }
        finally
        {
            var token = Request?.Token ?? ""; NativeApply = false; QueuedDelta = 0; Request = null; Applied = false; Lease?.Dispose(); Lease = null;
            PokerFeature.Current?.Panel?.RequestFinished(token);
        }
    }
}
