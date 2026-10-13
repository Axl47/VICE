using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.UI.Shop;
using Il2CppTMPro;
using S1API.Items;
using S1API.Items.Buildable;
using UnityEngine;
using VICE.Lifestyle.Amenities;
using Object = UnityEngine.Object;

namespace VICE.Lifestyle.Poker;

// Code-owned geometry in metres: +Y up, seat 0 at -Z, clockwise seats viewed from above.
internal static class PokerGeometry
{
    internal const float Surface = .76f;
    internal static float Yaw(int seat) => 90 * seat;
    internal static Vector3 Rotate(Vector3 position, int seat) => Quaternion.Euler(0, Yaw(seat), 0) * position;
    internal static Vector3 Hole(int seat, int card) => Rotate(new Vector3(card == 0 ? -.05f : .05f, .779f, -.57f), seat);
    internal static Vector3 Stack(int seat) => Rotate(new Vector3(.23f, .79f, -.62f), seat);
    internal static Vector3 Bet(int seat) => Rotate(new Vector3(-.23f, .79f, -.41f), seat);
    internal static Vector3 Seat(int seat) => Rotate(new Vector3(0, .43f, -1.08f), seat);
    internal static Vector3 Board(int card) => new((card - 2) * .10f, .78f, 0);
    internal static Vector3 Button(int seat) => Rotate(new Vector3(.35f, .784f, -.45f), seat);
    internal static Vector3 Deck => new(.48f, .795f, .18f);
    internal static Vector3 Pot => new(-.38f, .795f, .18f);
    internal static Vector3 Controls(int seat) => Rotate(new Vector3(0, .78f, -.85f), seat);
}
internal static class PokerContent
{
    internal const string ItemId = "vice.lifestyle:poker";
    private static S1API.Items.Buildable.BuildableItemDefinition? Definition;
    internal static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        => AmenityContent.Primitive(parent, name, type, position, scale, AmenityContent.Material("poker:" + color, color));
    internal static void Register()
    {
        if (Definition is not null) { ItemManager.EnsureItemRegistered(Definition); return; }
        var root = new GameObject("PokerTable_Root"); root.SetActive(false); Object.DontDestroyOnLoad(root);
        Part(root.transform, "Poker_TableBody", PrimitiveType.Cylinder, new(0, .70f, 0), new(1.8f, .06f, 1.8f), new(.18f, .095f, .055f));
        Part(root.transform, "Poker_Felt", PrimitiveType.Cylinder, new(0, .756f, 0), new(1.62f, .004f, 1.62f), new(.035f, .30f, .18f));
        Part(root.transform, "Poker_Pedestal", PrimitiveType.Cylinder, new(0, .32f, 0), new(.30f, .32f, .30f), new(.18f, .095f, .055f));
        Part(root.transform, "Poker_Foot", PrimitiveType.Cylinder, new(0, .045f, 0), new(.72f, .045f, .72f), new(.18f, .095f, .055f));
        for (var i = 0; i < 4; i++)
        {
            var chair = Part(root.transform, $"Poker_Chair_{i}", PrimitiveType.Cube, PokerGeometry.Seat(i), new(.4f, .08f, .4f), new(.21f, .12f, .065f));
            chair.transform.localRotation = Quaternion.Euler(0, PokerGeometry.Yaw(i), 0);
            Part(root.transform, $"Poker_ChairStem_{i}", PrimitiveType.Cylinder, PokerGeometry.Rotate(new(0, .21f, -1.08f), i), new(.10f, .21f, .10f), new(.10f, .10f, .10f));
            var anchor = new GameObject($"Seat_{i}"); anchor.transform.SetParent(root.transform, false); anchor.transform.localPosition = PokerGeometry.Seat(i);
            anchor.transform.localRotation = Quaternion.Euler(0, PokerGeometry.Yaw(i), 0);
        }
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++) texture.SetPixel(x, y, (x - 32) * (x - 32) + (y - 32) * (y - 32) < 27 * 27 ? new(.05f, .4f, .2f) : Color.clear);
        texture.Apply(); var icon = Sprite.Create(texture, new Rect(0, 0, 64, 64), new(.5f, .5f));
        Definition = FurnitureCreator.CreateBuilder().WithBasicInfo(ItemId, "Cash Poker Table", "Two to four players. No-limit Hold'em, native pocket cash buy-ins, no house fee. Placeholder model.")
            .WithModel(root).WithPricing(900).WithStackLimit(1).WithIcon(icon).WithBuildSound(S1API.Items.Buildable.BuildSoundType.Wood).WithFootprint(6, 6).Build();
        // S1API centres the visual on its grid. Keep collision code-owned when artwork is replaced.
        var built = Il2CppScheduleOne.Registry.GetItem(ItemId)!.TryCast<Il2CppScheduleOne.ItemFramework.BuildableItemDefinition>()!.BuiltItem;
        var visual = built.transform.Find("FurnitureVisual");
        built.BoundingCollider.center = visual.localPosition + new Vector3(0, .38f, 0);
        built.BoundingCollider.size = new Vector3(2.56f, .76f, 2.56f);
        var collision = visual.GetComponent<BoxCollider>();
        if (collision) { collision.center = new(0, .38f, 0); collision.size = new(2.56f, .76f, 2.56f); }
    }
    internal static void Shops()
    {
        if (Definition is null) return;
        foreach (var shop in ShopInterface.AllShops.ToArray().Where(s => s && s.ShopCode == "boutique"))
            if (shop.GetListing(ItemId) is null) S1API.Shops.ShopManager.AddToShops(Definition, shop.ShopName);
    }
}
internal sealed class PokerView : IDisposable
{
    internal readonly BuildableItem Item;
    internal readonly Transform Visual;
    internal string Id => Item.GUID.ToString();
    private readonly GameObject Root, Button, PotCash;
    private readonly GameObject[] Community = new GameObject[5];
    private readonly GameObject[,] Hole = new GameObject[4, 2];
    private readonly TextMeshPro[] Stack = new TextMeshPro[4], Bet = new TextMeshPro[4];
    private readonly GameObject[] Cash = new GameObject[4];
    private readonly GameObject[] BetCash = new GameObject[4];
    private readonly TextMeshPro Pot;
    private readonly Dictionary<GameObject, TextMeshPro> Faces = new();
    internal PokerView(BuildableItem item)
    {
        Item = item; Visual = item.transform.Find("FurnitureVisual");
        Root = new("Poker_Runtime"); Root.transform.SetParent(Visual, false);
        var deck = PokerContent.Part(Root.transform, "Poker_Deck", PrimitiveType.Cube, PokerGeometry.Deck, new(.063f, .03f, .088f), new(.12f, .17f, .52f));
        Label(deck.transform, "DECK", new(0, .52f, 0), 0, .6f, .8f, .15f);
        for (var i = 0; i < 5; i++) Community[i] = Card($"Community_{i}", PokerGeometry.Board(i), 0);
        for (var s = 0; s < 4; s++)
        {
            for (var c = 0; c < 2; c++) Hole[s, c] = Card($"Hole_{s}_{c}", PokerGeometry.Hole(s, c), PokerGeometry.Yaw(s));
            Cash[s] = PokerContent.Part(Root.transform, $"CashStack_{s}", PrimitiveType.Cube, PokerGeometry.Stack(s), new(.09f, .035f, .05f), new(.30f, .45f, .22f));
            Cash[s].transform.localRotation = Quaternion.Euler(0, PokerGeometry.Yaw(s), 0);
            Stack[s] = Label(Root.transform, "", PokerGeometry.Stack(s) + Vector3.up * .024f, PokerGeometry.Yaw(s), .37f, .10f, .035f);
            Bet[s] = Label(Root.transform, "", PokerGeometry.Bet(s), PokerGeometry.Yaw(s), .26f, .10f, .035f);
            BetCash[s] = PokerContent.Part(Root.transform, $"CashBet_{s}", PrimitiveType.Cube, PokerGeometry.Bet(s) - Vector3.up * .014f, new(.09f, .014f, .05f), new(.30f, .45f, .22f));
            BetCash[s].transform.localRotation = Quaternion.Euler(0, PokerGeometry.Yaw(s), 0);
        }
        Button = PokerContent.Part(Root.transform, "DealerButton", PrimitiveType.Cylinder, Vector3.zero, new(.04f, .003f, .04f), Color.white);
        Pot = Label(Root.transform, "POT $0", PokerGeometry.Pot, 0, .38f, .10f, .045f);
        PotCash = PokerContent.Part(Root.transform, "CashPot", PrimitiveType.Cube, PokerGeometry.Pot - Vector3.up * .015f, new(.09f, .015f, .05f), new(.30f, .45f, .22f));
    }
    private GameObject Card(string name, Vector3 position, float yaw)
    {
        var card = PokerContent.Part(Root.transform, name, PrimitiveType.Cube, position, new(.063f, .002f, .088f), new(.12f, .17f, .52f));
        card.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        // Face text lives in metre space, avoiding inherited card-body scale.
        var face = Label(Root.transform, "", position + Vector3.up * .0015f, yaw, .063f, .088f, .024f);
        Faces[card] = face; card.SetActive(false); face.gameObject.SetActive(false); return card;
    }
    internal static TextMeshPro Label(Transform parent, string value, Vector3 position, float yaw, float width, float height, float size)
    {
        var root = new GameObject("PokerLabel"); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        root.transform.localRotation = Quaternion.Euler(90, yaw, 0); var label = root.AddComponent<TextMeshPro>();
        var font = ShopInterface.AllShops.ToArray().FirstOrDefault(s => s && s.StoreNameLabel)?.StoreNameLabel;
        if (font) label.font = font!.font;
        label.rectTransform.sizeDelta = new(width, height); label.fontSize = size * 10; label.alignment = TextAlignmentOptions.Center;
        label.text = value; label.color = Color.white; label.richText = false; label.enableWordWrapping = false; return label;
    }
    private void DrawCard(GameObject card, int value, bool visible)
    {
        card.SetActive(visible); var face = Faces[card]; face.gameObject.SetActive(visible && value >= 0);
        card.GetComponent<Renderer>().sharedMaterial = AmenityContent.Material(value >= 0 ? "poker:cardface" : "poker:cardback", value >= 0 ? new(.92f, .9f, .82f) : new(.12f, .17f, .52f));
        if (value >= 0) { face.text = PokerCards.Name(value); face.color = value / 13 is 1 or 2 ? new(.8f, .09f, .09f) : Color.black; }
    }
    internal void Tick(PokerSnapshot state, bool peek)
    {
        for (var i = 0; i < 5; i++) DrawCard(Community[i], i < state.Board.Count ? state.Board[i] : -1, i < state.Board.Count);
        for (var s = 0; s < 4; s++)
        {
            var seat = state.Seats[s]; var owner = seat.Player == PokerNetwork.LocalPlayer;
            for (var c = 0; c < 2; c++) DrawCard(Hole[s, c], c < seat.Hole.Length && (!owner || peek || state.Street == "between") ? seat.Hole[c] : -1, c < seat.Hole.Length);
            Cash[s].SetActive(seat.Stack > 0); Stack[s].text = seat.Player.Length == 0 ? $"SEAT {s + 1}" : $"{seat.Name}: ${seat.Stack}";
            BetCash[s].SetActive(seat.Wager > 0);
            Stack[s].color = state.Turn == s ? new(1, .8f, .15f) : Color.white;
            Bet[s].text = seat.Wager > 0 ? $"BET ${seat.Wager}" : seat.Folded ? "FOLDED" : seat.InHand && seat.Stack == 0 ? "ALL IN" : "";
        }
        Button.SetActive(state.Dealer >= 0); if (state.Dealer >= 0) Button.transform.localPosition = PokerGeometry.Button(state.Dealer);
        Pot.text = $"POT ${state.Pot}" + (state.Pots.Count > 1 ? $" + {state.Pots.Count - 1} side pots" : "");
        PotCash.SetActive(state.Pot > 0);
    }
    public void Dispose() { if (Root) Object.Destroy(Root); }
}
