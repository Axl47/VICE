using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using S1API.Quests.Constants;
using UnityEngine;

namespace PersonalMods.LSDGameplay;

public sealed class StudioTutorial : S1API.Quests.Quest
{
    internal static StudioTutorial? Current;
    protected override string Title=>"First edition";
    protected override string Description=>"Set up Off Register's studio, print your artwork, and package your first tabs.";
    protected override bool AutoBegin=>false;
    protected override Sprite QuestIcon=>StudioAssets.DeliveryStar;
    private int Seen=-1;
    public StudioTutorial()
    {
        AddEntry("Buy bench, cabinet and paper at the warehouse; cartridge at hardware store");
        AddEntry("Place the bench and cabinet inside your Sewer Office");
        AddEntry("Order Prism Base in Studio; collect at Dan's Hardware's purple star");
        AddEntry("Choose or draw an edition, then load a batch at the bench");
        AddEntry("Register the sheet and roll the print onto it");
        AddEntry("Carry the wet sheet to the drying cabinet");
        AddEntry("Collect the dry sheet and cut it at the bench");
        AddEntry("Collect your finished tabs");
        AddEntry("Package tabs: baggie 1, jar 5, or edition sleeve 20");
    }
    protected override void OnCreated(){base.OnCreated();Current=this;}
    protected override void OnDestroyed(){if(Current==this)Current=null;base.OnDestroyed();}
    internal static bool Observe(WorkshopState state,WorkshopWorld world)
    {
        if(!WorkshopNetwork.Host || !state.Introduced || !Player.Local)return false;
        var step=Math.Max(1,state.TutorialStep);
        // The host owns tutorial state, but any loaded peer can perform the production run.
        var items=Player.PlayerList.ToArray().Where(p=>p && p.PlayerLoaded)
            .SelectMany(p=>p.IsLocalPlayer ? InventoryTransaction.RegularSlots():p._inventory.ToArray())
            .Select(s=>s.ItemInstance).Where(i=>i is not null && i.Quantity>0).ToArray();
        var built=world.Office?.BuildableItems.ToArray().Where(i=>i && i.ItemInstance is not null).Select(i=>i.ItemInstance.ID).ToArray()??Array.Empty<string>();
        bool Has(string id)=>items.Any(i=>i.ID==id)||built.Contains(id);
        if(new[]{WorkshopRules.Bench,WorkshopRules.Cabinet,WorkshopRules.Paper,WorkshopRules.Ink}.All(Has))step=Math.Max(step,2);
        if(step>=2 && built.Contains(WorkshopRules.Bench)&&built.Contains(WorkshopRules.Cabinet))step=Math.Max(step,3);
        if(step>=3 && Has(WorkshopRules.Prism))step=Math.Max(step,4);
        foreach(var machine in state.Machines)step=Math.Max(step,machine.Stage switch{
            MachineStage.Preparing or MachineStage.Printing=>5,MachineStage.WetOutput=>6,
            MachineStage.Drying or MachineStage.DryOutput or MachineStage.Cutting=>7,MachineStage.TabOutput=>8,_=>0});
        foreach(var item in items){
            if(WorkshopRules.TrySheet(item.ID,out var dry,out _,out _))step=Math.Max(step,dry ? 7:6);
            if(item.ID.StartsWith(WorkshopRules.Owner+":tab-",StringComparison.Ordinal))
                step=Math.Max(step,item.TryCast<ProductItemInstance>()?.AppliedPackaging ? 10:9);
        }
        if(step==state.TutorialStep)return false;state.TutorialStep=step;state.Revision++;return true;
    }
    internal void Tick(WorkshopState state,WorkshopWorld world)
    {
        if(!state.Introduced || Seen==state.TutorialStep)return;
        Seen=state.TutorialStep;
        if(QuestState==QuestState.Inactive)Begin();
        if(QuestState==QuestState.Completed)return;
        for(var i=0;i<QuestEntries.Count;i++){
            var desired=i<state.TutorialStep-1 ? QuestState.Completed:i==state.TutorialStep-1 ? QuestState.Active:QuestState.Inactive;
            if(QuestEntries[i].State!=desired)QuestEntries[i].SetState(desired);
            if(desired==QuestState.Active && world.Office && state.TutorialStep is not (1 or 3))QuestEntries[i].POIPosition=world.Office!.InteriorSpawnPoint.position;
            else QuestEntries[i].ClearPOI();
        }
        if(state.TutorialStep>=10 && QuestState!=QuestState.Completed)Complete();
    }
}
