using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.Shop;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PersonalMods.GasPurchases;

internal sealed class SavedPurchasePanel : IDisposable
{
    private readonly ShopInterface Shop;
    private readonly GameObject Controls, Overlay;
    private readonly RectTransform OrderList;
    private readonly TMP_InputField NameInput;
    private readonly TextMeshProUGUI Preview, Message, CompactMessage;
    private readonly Button SaveButton, LoadButton, DeleteButton, RepeatButton;
    private readonly List<GameObject> Rows = new();
    private string? SelectedName;
    private bool TypingOwned, PreviousTyping;
    private static readonly Color Background = new(0.07f, 0.09f, 0.11f, 1);
    private static readonly Color ButtonColor = new(0.16f, 0.28f, 0.22f, 1);

    internal SavedPurchasePanel(ShopInterface shop)
    {
        Shop = shop;
        var controls = Rectangle(shop.Container, "Saved purchase controls", Vector2.one, Vector2.one, new(-10, -14), new(282, 54));
        controls.pivot = Vector2.one;
        Controls = controls.gameObject;
        MakeButton(Controls.transform, "Saved orders", 0, 0, 138, 30, Show);
        RepeatButton = MakeButton(Controls.transform, "Repeat last", 144, 0, 138, 30, RepeatLast);
        CompactMessage = Label(Controls.transform, "", 0, 34, 282, 18, 12);
        CompactMessage.overflowMode = TextOverflowModes.Ellipsis;

        var overlay = Rectangle(shop.Container, "Saved purchases overlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlay.pivot = new(0.5f, 0.5f);
        overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
        Overlay = overlay.gameObject;
        Overlay.SetActive(false);
        var shade = Overlay.AddComponent<Image>(); shade.color = new(0, 0, 0, 0.8f); shade.raycastTarget = true;
        var dialog = Rectangle(overlay, "Saved purchases", new(0.5f, 0.5f), new(0.5f, 0.5f), Vector2.zero, new(760, 452));
        dialog.pivot = new(0.5f, 0.5f);
        var image = dialog.gameObject.AddComponent<Image>(); image.color = Background;
        Label(dialog, "Saved purchases", 18, 16, 610, 34, 26);
        MakeButton(dialog, "Close", 656, 16, 86, 32, Hide);
        Label(dialog, "Order name", 18, 62, 106, 34, 16);
        NameInput = MakeInput(dialog, 130, 62, 612, 36);
        Label(dialog, "Shopping lists", 18, 115, 258, 26, 18);
        Label(dialog, "Items in selected order", 306, 115, 436, 26, 18);

        var list = Rectangle(dialog, "Order list", new(0, 1), new(0, 1), new(18, -148), new(266, 208));
        var listImage = list.gameObject.AddComponent<Image>(); listImage.color = new(0.1f, 0.13f, 0.15f, 1);
        var viewport = Rectangle(list, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();
        OrderList = Rectangle(viewport, "Content", new(0, 1), new(1, 1), Vector2.zero, new(0, 208));
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = OrderList;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
        Preview = Label(dialog, "", 306, 148, 436, 144, 17);
        LoadButton = MakeButton(dialog, "Load order", 306, 310, 208, 36, LoadSelected);
        DeleteButton = MakeButton(dialog, "Delete order", 530, 310, 212, 36, DeleteSelected);
        Message = Label(dialog, "Loading replaces your cart at today's prices. Check out normally.", 18, 364, 724, 40, 15);
        SaveButton = MakeButton(dialog, "Save current cart", 18, 408, 266, 32, SaveCurrent);
        MakeButton(dialog, "Repeat last purchase", 306, 408, 436, 32, RepeatLast);
        RefreshOrders();
    }

    private TextMeshProUGUI Label(Transform parent, string text, float x, float y, float width, float height, float size)
    {
        var rect = Rectangle(parent, "Label", new(0, 1), new(0, 1), new(x, -y), new(width, height));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Shop.StoreNameLabel) { label.font = Shop.StoreNameLabel.font; label.fontSharedMaterial = Shop.StoreNameLabel.fontSharedMaterial; }
        label.text = text; label.fontSize = size; label.fontStyle = FontStyles.Normal;
        label.color = Color.white; label.richText = false;
        label.alignment = TextAlignmentOptions.TopLeft; label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private static RectTransform Rectangle(Transform parent, string name, Vector2 minimum, Vector2 maximum, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, new[] { Il2CppType.Of<RectTransform>() });
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = minimum; rect.anchorMax = maximum; rect.pivot = new(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private Button MakeButton(Transform parent, string text, float x, float y, float width, float height, Action callback)
    {
        var rect = Rectangle(parent, text, new(0, 1), new(0, 1), new(x, -y), new(width, height));
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = ButtonColor; colors.highlightedColor = new(0.23f, 0.43f, 0.32f, 1);
        colors.pressedColor = new(0.12f, 0.21f, 0.16f, 1); colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new(0.13f, 0.15f, 0.16f, 1); button.colors = colors;
        var label = Label(rect, text, 6, 0, width - 12, height, 16); label.alignment = TextAlignmentOptions.Midline;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => Run(callback)))
            ?? throw new InvalidOperationException("Could not bind a saved-order button."));
        return button;
    }

    private TMP_InputField MakeInput(Transform parent, float x, float y, float width, float height)
    {
        var rect = Rectangle(parent, "Order name", new(0, 1), new(0, 1), new(x, -y), new(width, height));
        var background = rect.gameObject.AddComponent<Image>(); background.color = new(0.17f, 0.2f, 0.22f, 1);
        var viewport = Rectangle(rect, "Text viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.offsetMin = new(10, 4); viewport.offsetMax = new(-10, -4);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label(viewport, "", 0, 0, width - 20, height - 8, 18);
        text.enableWordWrapping = false; text.alignment = TextAlignmentOptions.MidlineLeft;
        var placeholder = Label(viewport, "e.g. Mixing supplies", 0, 0, width - 20, height - 8, 18);
        placeholder.color = new(0.65f, 0.69f, 0.68f, 1); placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background; input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
        input.fontAsset = text.font; input.characterLimit = 48; input.richText = false;
        input.contentType = TMP_InputField.ContentType.Standard; input.lineType = TMP_InputField.LineType.SingleLine;
        input.onSelect.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>((Action<string>)(_ => TakeTyping()))
            ?? throw new InvalidOperationException("Could not bind the order-name input."));
        input.onDeselect.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>((Action<string>)(_ => ReleaseTyping()))
            ?? throw new InvalidOperationException("Could not bind the order-name input."));
        return input;
    }

    private void TakeTyping()
    {
        if (TypingOwned) return;
        PreviousTyping = GameInput.IsTyping; TypingOwned = true; GameInput.IsTyping = true;
    }

    private void ReleaseTyping()
    {
        if (!TypingOwned) return;
        GameInput.IsTyping = PreviousTyping; TypingOwned = false;
    }

    private void Run(Action action)
    {
        if (!GasPurchasesMod.Enabled.Value || !Shop || !Shop.IsOpen) return;
        try { action(); }
        catch (Exception error) { SetMessage(error.Message); }
    }

    private SavedOrder? Selected => GasPurchasesMod.Orders.Book.Orders.FirstOrDefault(x => x.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase));
    internal void RefreshOrders()
    {
        foreach (var row in Rows) { if (row) { row.SetActive(false); Object.Destroy(row); } }
        Rows.Clear();
        var orders = GasPurchasesMod.Orders.Book.Orders;
        OrderList.sizeDelta = new(0, Math.Max(208, orders.Length * 40 + 12));
        for (var index = 0; index < orders.Length; index++)
        {
            var name = orders[index].Name;
            var button = MakeButton(OrderList, name, 6, 6 + index * 40, 254, 34, () => Select(name));
            Rows.Add(button.gameObject);
        }
        if (orders.Length == 0) Rows.Add(Label(OrderList, "No saved orders yet.", 12, 14, 240, 28, 16).gameObject);
        if (Selected is null) SelectedName = null;
        RefreshPreview();
        if (GasPurchasesMod.Orders.ReadError is not null) SetMessage("Saved orders could not be read. See MelonLoader's log before saving new orders.");
    }

    private void Select(string name) { SelectedName = name; NameInput.SetTextWithoutNotify(name); RefreshPreview(); }
    private void RefreshPreview()
    {
        var order = Selected;
        Preview.text = order is null ? "Choose a saved order on the left.\n\nTo save a new order, fill your cart and enter a name above."
            : string.Join("\n", order.Items.Take(6).Select(x => $"{x.Name} × {x.Quantity}"))
                + (order.Items.Length > 6 ? $"\n+ {order.Items.Length - 6} more items" : "");
    }

    private void Show() { RefreshOrders(); Overlay.SetActive(true); Overlay.transform.SetAsLastSibling(); }
    internal void Hide()
    {
        if (NameInput && NameInput.isFocused) NameInput.DeactivateInputField(true);
        ReleaseTyping();
        if (Overlay) Overlay.SetActive(false);
    }

    private void SaveCurrent()
    {
        var order = GasPurchasesMod.Capture(Shop.Cart, NameInput.text);
        if (order is null) { SetMessage("Add items to the cart before saving an order."); return; }
        GasPurchasesMod.Orders.Save(order); SelectedName = order.Name;
        RefreshOrders(); SetMessage($"Saved {order.Name}. Loading an order replaces the current cart.");
    }

    private void LoadSelected() { if (Selected is { } order) Load(order); }
    private void RepeatLast()
    {
        if (GasPurchasesMod.Orders.Book.LastPurchase is { } order) Load(order);
        else SetMessage("Complete a gas-mart purchase first, or save your current cart.");
    }

    private void Load(SavedOrder order)
    {
        var result = GasPurchasesMod.Restore(Shop, order);
        SetMessage(result.Message);
        if (result.Loaded) Hide();
    }

    private void DeleteSelected()
    {
        if (Selected is not { } order) return;
        GasPurchasesMod.Orders.Delete(order.Name); SelectedName = null;
        NameInput.SetTextWithoutNotify(""); RefreshOrders(); SetMessage($"Deleted {order.Name}.");
    }

    private void SetMessage(string message) { Message.text = message; CompactMessage.text = message; }
    internal void Tick()
    {
        if (!Shop || !Controls || !Overlay) return;
        var open = GasPurchasesMod.Enabled.Value && Shop.IsOpen;
        Controls.SetActive(open);
        if (!open) { Hide(); return; }
        RepeatButton.interactable = GasPurchasesMod.Orders.Book.LastPurchase is not null;
        SaveButton.interactable = GasPurchasesMod.Orders.ReadError is null && Shop.Cart && Shop.Cart.cartDictionary?.Count > 0;
        LoadButton.interactable = Selected is not null;
        DeleteButton.interactable = GasPurchasesMod.Orders.ReadError is null && Selected is not null;
    }

    public void Dispose() { Hide(); if (Controls) Object.Destroy(Controls); if (Overlay) Object.Destroy(Overlay); }
}
