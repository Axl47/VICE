using Il2CppTMPro;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalMods.LSDGameplay;

public sealed class CollectiveCatalogue : PhoneApp
{
    private TextMeshProUGUI? Status;
    private static CollectiveCatalogue? Instance;
    private float NextRefresh;
    private bool PendingOpen;
    protected override string AppName=>"Off Register";
    protected override string AppTitle=>"Off Register";
    protected override string IconLabel=>"Studio";
    protected override EOrientation Orientation=>EOrientation.Vertical;
    protected override string IconFileName=>"OffRegister.png";
    protected override Sprite IconSprite=>StudioAssets.Symbol;
    protected override void OnDestroyed(){if(Instance==this)Instance=null;base.OnDestroyed();}
    protected override void OnCreatedUI(GameObject container)
    {
        Instance=this;
        var background=container.GetComponent<Image>() ?? container.AddComponent<Image>();
        background.color=new(0.035f,0.045f,0.07f,1);background.raycastTarget=false;
        StudioWidgets.Label(container.transform,"OFF REGISTER",32,28,590,46,32);
        StudioWidgets.Label(container.transform,"PRISM BASE",32,118,590,36,24);
        StudioWidgets.Label(container.transform,"One parcel prints one sheet.",32,158,590,40,20);
        StudioWidgets.Button(container.transform,"1 parcel · $30",32,228,278,58,()=>Order(1));
        StudioWidgets.Button(container.transform,"4 parcels · $120",330,228,278,58,()=>Order(4));
        StudioWidgets.Label(container.transform,"DELIVERY",32,354,590,36,24);
        Status=StudioWidgets.Label(container.transform,"",32,404,570,440,23);
        Refresh();
    }
    private static void Order(int quantity)=>WorkshopNetwork.Submit(new StudioCommand { Action="order",Quantity=quantity });
    internal static void UpdateStatus()=>Instance?.Refresh();
    internal static void Tick()
    {
        if(Instance?.PendingOpen==true && Il2CppScheduleOne.UI.Phone.Phone.Instance.State.IsActive){
            Instance.PendingOpen=false;Instance.OpenApp();
        }
        if (Instance is null || Time.realtimeSinceStartup<Instance.NextRefresh) return;
        Instance.NextRefresh=Time.realtimeSinceStartup+1;Instance.Refresh();
    }
    internal static void OpenCatalogue()
    {
        var phone=Il2CppScheduleOne.UI.Phone.Phone.Instance;
        if (!phone || Instance is null) return;
        Instance.PendingOpen=true;
        var menu=Il2CppScheduleOne.UI.GameplayMenu.Instance;menu.Open();menu.SetScreen(Il2CppScheduleOne.UI.GameplayMenu.EGameplayScreen.Phone);
    }
    internal static bool CheckIsOpen=>Instance?.IsOpen()==true && Il2CppScheduleOne.UI.Phone.Phone.Instance.State.IsActive;
    internal static string CheckPhoneState=>$"app={Instance?.IsOpen()}, active={Il2CppScheduleOne.UI.Phone.Phone.Instance.State.IsActive}, open={Il2CppScheduleOne.UI.Phone.Phone.Instance.IsOpen}";
    internal static void CheckClose(){Instance?.CloseApp();Il2CppScheduleOne.UI.GameplayMenu.Instance.Close();}
    private void Refresh()
    {
        if (Status is null || !Status) return;
        var current=LSDGameplayMod.Current;
        if (current?.Ready!=true || !current.State.Introduced) { Status.text="Find our sticker inside the owned Sewer Office before ordering.";return; }
        var lines=current.State.Orders.TakeLast(4).Select(o=>$"{o.Quantity} parcel{(o.Quantity==1 ? "" : "s")} · "+(o.Stage==OrderStage.InTransit
            ? $"arriving in {Math.Max(0,o.DueMinute-LSDGameplayMod.Minute)} min" : o.Stage==OrderStage.WaitingForSpace ? "pallet full" : "delivered"));
        Status.text=(current.State.Orders.Count==0 ? "No orders yet." : string.Join("\n\n",lines))+"\n\nPick up at Dan's Hardware.\nFollow the purple star.";
    }
}
