using Il2CppTMPro;
using S1API.PhoneApp;
using UnityEngine;

namespace PersonalMods.LSDGameplay;

public sealed class CollectiveCatalogue : PhoneApp
{
    private TextMeshProUGUI? Status;
    private static CollectiveCatalogue? Instance;
    protected override string AppName=>"Off Register";
    protected override string AppTitle=>"Off Register";
    protected override string IconLabel=>"Studio";
    protected override EOrientation Orientation=>EOrientation.Vertical;
    protected override string IconFileName=>"OffRegister.png";
    protected override Sprite IconSprite=>StudioAssets.Symbol;
    protected override void OnCreatedUI(GameObject container)
    {
        Instance=this;
        StudioWidgets.Label(container.transform,"OFF REGISTER",32,28,590,46,32);
        StudioWidgets.Label(container.transform,"Small editions. Underground printing.",32,80,590,38,21);
        Status=StudioWidgets.Label(container.transform,"Find our sticker inside the owned Sewer Office.",32,140,590,180,21);
        StudioWidgets.Button(container.transform,"Order 1 Prism Base · $30",32,336,360,48,()=>Order(1));
        StudioWidgets.Button(container.transform,"Order 4 Prism Base · $120",32,396,360,48,()=>Order(4));
        StudioWidgets.Label(container.transform,"Paper, bench, cabinet and empty bulk sleeves: warehouse.\nPrint cartridges: hardware store.\nEach starter batch uses one parcel, one blank sheet and one cartridge.",32,530,590,140,20);
        StudioWidgets.Button(container.transform,"Refresh delivery status",32,456,360,48,Refresh);
        Refresh();
    }
    private static void Order(int quantity)=>WorkshopNetwork.Submit(new StudioCommand { Action="order",Quantity=quantity });
    internal static void UpdateStatus()=>Instance?.Refresh();
    private void Refresh()
    {
        if (Status is null || !Status) return;
        var current=LSDGameplayMod.Current;
        if (current?.Ready!=true || !current.State.Introduced) { Status.text="Find our sticker inside the owned Sewer Office before ordering.";return; }
        var lines=current.State.Orders.TakeLast(4).Select(o=>$"{o.Id[..8]} · {o.Quantity} parcel(s) · "+(o.Stage==OrderStage.InTransit
            ? $"{Math.Max(0,o.DueMinute-LSDGameplayMod.Minute)} game minutes" : o.Stage==OrderStage.WaitingForSpace ? "Held: pallet full" : "Delivered to pallet"));
        Status.text="Collection: "+current.World?.CollectionName+"\n\n"+string.Join("\n",lines)+"\n\nOnline balance: $"+S1API.Money.Money.GetOnlineBalance().ToString("0");
    }
}
