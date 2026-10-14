using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using VICE.Lifestyle.Television;
using Object = UnityEngine.Object;

namespace VICE.Lifestyle.Poker;

// A world-space rail console. No substitute two-dimensional game surface.
internal sealed class PokerPanel : IDisposable
{
    private readonly PokerFeature Feature;
    private readonly GameObject Root;
    private readonly RectTransform Frame;
    private readonly Canvas Canvas;
    private readonly TextMeshProUGUI Status, Feedback, Message;
    private readonly TMP_InputField Amount, Small, Big, Minimum, Maximum;
    private readonly Button Fold, CheckCall, Raise, AllIn, Start, Configure, Buy;
    private readonly GameInput.ExitDelegate ExitHandler;
    private PokerCamera? Camera;
    private int Selected;
    private bool CloseRequested;
    private string PendingBuy = "";
    internal string Table { get; private set; } = "";
    internal bool Peek { get; private set; }
    internal PokerPanel(PokerFeature feature)
    {
        Feature = feature;
        Root = new GameObject("Poker_SeatEdge_Controls", new[] { Il2CppType.Of<RectTransform>() });
        Canvas = Root.AddComponent<Canvas>(); Canvas.renderMode = RenderMode.WorldSpace; Canvas.sortingOrder = 20;
        Root.AddComponent<GraphicRaycaster>(); Frame = Root.GetComponent<RectTransform>(); Frame.sizeDelta = new(1050, 340); Frame.pivot = new(.5f, .5f);
        Frame.gameObject.AddComponent<Image>().color = new(.045f, .065f, .05f, .97f);
        Status = TVWidgets.Label(Frame, "", 18, 8, 1000, 32, 22);
        Feedback = TVWidgets.Label(Frame, "", 18, 40, 1000, 60, 19);
        Small = Field("SB", "5", 20, 104, 80); Big = Field("BB", "10", 110, 104, 80);
        Minimum = Field("Min buy", "200", 200, 104, 120); Maximum = Field("Max buy", "1000", 330, 104, 120);
        Configure = Button("Set session", 460, 120, 130, () => Send("configure"));
        Start = Button("Next hand", 600, 120, 130, () => Send("start"));
        Button("Peek", 740, 120, 110, () => Peek = !Peek); Button("Leave / cash out", 860, 120, 170, Close);
        for (var s = 0; s < 4; s++) { var seat = s; Button("Seat " + (s + 1), 20 + s * 108, 170, 100, () => { Selected = seat; Pose(); }); }
        Amount = Field("Buy-in / bet-to $", "200", 460, 156, 150);
        Buy = Button("Buy in / add", 620, 170, 150, () => Send("buyin"));
        Button("½ pot", 780, 170, 75, () => Quick(.5)); Button("Pot", 865, 170, 70, () => Quick(1)); Button("Min", 945, 170, 85, () => SetMinimum());
        Fold = Button("Fold", 20, 224, 170, () => Send("fold"));
        CheckCall = Button("Check / call", 200, 224, 260, () => Send(State().Wager <= MySeat()?.Wager ? "check" : "call"));
        Raise = Button("Bet / raise to $", 470, 224, 250, () => Send(State().Wager == 0 ? "bet" : "raise"));
        AllIn = Button("All in", 730, 224, 150, () => Send("allin")); Button("Close view", 890, 224, 140, Close);
        Message = TVWidgets.Label(Frame, "Choose limits before buying in. Seat controls are private to this view.", 20, 280, 1010, 50, 18);
        ExitHandler = DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>((Action<ExitAction>)(a => { if (Table.Length > 0) { a.used = true; Close(); } }))!;
        GameInput.RegisterExitListener(ExitHandler, 10); Root.SetActive(false);
    }
    private TMP_InputField Field(string label, string value, float x, float y, float width)
    { TVWidgets.Label(Frame, label, x, y, width, 18, 16); var input = TVWidgets.Input(Frame, label, x, y + 20, width, 32, 10); input.contentType = TMP_InputField.ContentType.IntegerNumber; input.text = value; return input; }
    private Button Button(string text, float x, float y, float width, Action action) => TVWidgets.Button(Frame, text, x, y, width, 38, () =>
    { try { action(); } catch (Exception e) { Notice(PokerNetwork.Safe(e)); } });
    internal void Notice(string text) { if (Message) Message.text = text[..Math.Min(500, text.Length)]; }
    internal void RequestFinished(string token) { if (token == PendingBuy) PendingBuy = ""; }
    internal void Abort() { PendingBuy = ""; Close(); }
    internal void Open(string id)
    {
        if (Table.Length > 0 || PokerWallet.Locked || !Player.Local || !Feature.CanUse(Player.Local, id) || !PokerCamera.Available) return;
        TelevisionFeature.Current?.Panel?.Close();
        Camera = new(); Table = id; Peek = false; CloseRequested = false;
        Selected = Feature.Snapshots.TryGetValue(id, out var state) ? Math.Max(0, Array.FindIndex(state.Seats, s => s.Player == PokerNetwork.LocalPlayer)) : 0;
        if (state is not null) { Small.text = state.Small.ToString(); Big.text = state.Big.ToString(); Minimum.text = state.Minimum.ToString(); Maximum.text = state.Maximum.ToString(); }
        Frame.SetParent(Feature.Views[id].Visual, false); Frame.localScale = Vector3.one * .0008f; Pose(); Canvas.worldCamera = Camera.View;
        Root.SetActive(true); if (!PokerNetwork.Host) PokerNetwork.Submit(new());
    }
    private PokerSnapshot State() => Feature.Snapshots.GetValueOrDefault(Table) ?? throw new InvalidOperationException("Waiting for host state.");
    private PokerSeatSnapshot? MySeat() => Feature.Snapshots.GetValueOrDefault(Table)?.Seats.FirstOrDefault(s => s.Player == PokerNetwork.LocalPlayer);
    private long Number(TMP_InputField field) => long.TryParse(field.text, out var amount) && amount >= 0 ? amount : throw new InvalidOperationException("Enter a whole-dollar amount.");
    private void Send(string action)
    {
        if (PendingBuy.Length > 0) throw new InvalidOperationException("Wait for the host's buy-in reply.");
        var state = State(); var seat = Array.FindIndex(state.Seats, s => s.Player == PokerNetwork.LocalPlayer);
        var command = new PokerCommand { Epoch = PokerNetwork.Epoch, Table = Table, Instance = state.Instance, Revision = state.Revision,
            Action = action, Seat = seat >= 0 ? seat : Selected, Amount = action is "buyin" or "bet" or "raise" ? Number(Amount) : 0 };
        if (action == "configure") { command.Small = Number(Small); command.Big = Number(Big); command.Minimum = Number(Minimum); command.Maximum = Number(Maximum); command.Timeout = Feature.DefaultTimeout; }
        if (action == "buyin") PendingBuy = command.Token;
        try { PokerNetwork.Submit(command); } catch { RequestFinished(command.Token); throw; }
    }
    private void SetMinimum() { var state = State(); Amount.text = (state.Street == "between" || MySeat() is null ? state.Minimum : state.MinimumTo).ToString(); }
    private void Quick(double fraction)
    {
        var state = State(); var own = MySeat(); if (own is null) return;
        var call = Math.Max(0, state.Wager - own.Wager);
        Amount.text = Math.Min(own.Wager + own.Stack, Math.Max(state.MinimumTo, state.Wager + (long)Math.Floor((state.Pot + call) * fraction))).ToString();
    }
    private void Pose()
    {
        if (Table.Length == 0 || Camera is null || !Feature.Views.TryGetValue(Table, out var view)) return;
        var seat = Feature.Snapshots.TryGetValue(Table, out var state) ? Array.FindIndex(state.Seats, s => s.Player == PokerNetwork.LocalPlayer) : -1;
        if (seat >= 0) Selected = seat;
        Camera.Pose(view.Visual, Selected); Frame.localPosition = PokerGeometry.Controls(Selected);
        Frame.localRotation = Quaternion.Euler(70, PokerGeometry.Yaw(Selected), 0);
    }
    internal void Tick()
    {
        if (Table.Length == 0) return;
        if (CloseRequested && !PokerWallet.Locked && PendingBuy.Length == 0) { Close(); return; }
        if (!Player.Local || !Feature.CanUse(Player.Local, Table) || Camera is null || !Camera.Valid) { Close(); return; }
        Pose();
        if (!Feature.Snapshots.TryGetValue(Table, out var state)) { Status.text = "Waiting for the host…"; return; }
        var seat = Array.FindIndex(state.Seats, s => s.Player == PokerNetwork.LocalPlayer); var own = seat >= 0 ? state.Seats[seat] : null;
        var busy = PokerWallet.Locked || PendingBuy.Length > 0;
        var turn = state.Street != "between" && seat >= 0 && state.Turn == seat && !busy;
        var call = own is null ? 0 : Math.Max(0, state.Wager - own.Wager);
        Status.text = $"HOLD'EM · ${state.Small}/${state.Big} · {state.Street} · POT ${state.Pot} · " + (own is null ? "Spectating" : $"Stack ${own.Stack} · Pocket ${PlayerInventory.Instance.cashInstance.Balance:0.##}");
        Feedback.text = state.Feedback + (state.Turn >= 0 ? $"\nSeat {state.Turn + 1} acts · {state.Remaining:0}s · current wager ${state.Wager}" : "");
        Fold.interactable = CheckCall.interactable = turn;
        var opponentCanMatch = state.Seats.Count(s => s.InHand && !s.Folded && s.Stack > 0) > 1;
        Raise.interactable = turn && state.RaiseAllowed && own!.Stack > call && opponentCanMatch;
        AllIn.interactable = turn && (own!.Stack <= call || state.RaiseAllowed && opponentCanMatch);
        CheckCall.GetComponentInChildren<TextMeshProUGUI>().text = call == 0 ? "Check" : $"Call ${Math.Min(call, own?.Stack ?? 0)}";
        Configure.interactable = state.Street == "between" && state.Seats.All(s => s.Stack == 0) && !busy;
        Start.interactable = state.Street == "between" && state.Organizer == PokerNetwork.LocalPlayer && !busy;
        Buy.interactable = !busy && (own is null ? state.Seats[Selected].Player.Length == 0 : state.Street == "between" && !own.Leaving);
        if (busy) Notice("Securing native pocket cash. Please wait for host acknowledgement.");
    }
    internal void Close()
    {
        if (Table.Length == 0) return;
        if (PokerWallet.Locked || PendingBuy.Length > 0) { CloseRequested = true; return; }
        try { if (MySeat() is not null) Send("leave"); }
        catch (Exception e) { ModSupport.WarnOnce("poker:leave", e.Message); }
        finally { Peek = false; Table = ""; Root.SetActive(false); Frame.SetParent(null, false); Camera?.Dispose(); Camera = null; CloseRequested = false; }
    }
    public void Dispose()
    {
        PokerWallet.Release(); PendingBuy = ""; Close(); GameInput.DeregisterExitListener(ExitHandler); if (Root) Object.Destroy(Root);
    }
}
