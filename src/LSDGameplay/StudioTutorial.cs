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
    public StudioTutorial(){foreach(var title in TutorialProgress.Titles)AddEntry(title);}
    protected override void OnCreated(){base.OnCreated();Current=this;}
    protected override void OnDestroyed(){if(Current==this)Current=null;base.OnDestroyed();}
    internal static bool Observe(WorkshopState state,WorkshopWorld world)
    {
        if(!WorkshopNetwork.Host || !state.Introduced || !Player.Local)return false;
        var changed=false;
        // The host owns tutorial state, but any loaded peer can perform the production run.
        var items=Player.PlayerList.ToArray().Where(p=>p && p.PlayerLoaded)
            .SelectMany(p=>p.IsLocalPlayer ? InventoryTransaction.RegularSlots():p._inventory.ToArray())
            .Select(s=>s.ItemInstance).Where(i=>i is not null && i.Quantity>0).ToArray();
        var built=world.Office?.BuildableItems.ToArray().Where(i=>i && i.ItemInstance is not null).Select(i=>i.ItemInstance.ID).ToArray()??Array.Empty<string>();
        bool Has(string id)=>items.Any(i=>i.ID==id)||built.Contains(id);
        foreach(var (id,objective) in new[]{(WorkshopRules.Bench,TutorialObjective.BuyBench),(WorkshopRules.Cabinet,TutorialObjective.BuyCabinet),(WorkshopRules.Paper,TutorialObjective.BuyPaper),(WorkshopRules.Ink,TutorialObjective.BuyInk)})
            if(Has(id))changed=TutorialProgress.Record(state,objective)||changed;
        if(built.Contains(WorkshopRules.Bench))changed=TutorialProgress.Record(state,TutorialObjective.PlaceBench)||changed;
        if(built.Contains(WorkshopRules.Cabinet))changed=TutorialProgress.Record(state,TutorialObjective.PlaceCabinet)||changed;
        if(state.Orders.Count>0)changed=TutorialProgress.Record(state,TutorialObjective.OrderPrism)||changed;
        if(Has(WorkshopRules.Prism))changed=TutorialProgress.Record(state,TutorialObjective.CollectPrism)||changed;
        foreach(var machine in state.Machines){
            TutorialObjective? objective=machine.Stage switch{
                MachineStage.Preparing=>TutorialObjective.LoadBatch,MachineStage.Printing=>TutorialObjective.RegisterSheet,
                MachineStage.WetOutput=>TutorialObjective.PrintSheet,MachineStage.Drying or MachineStage.DryOutput=>TutorialObjective.LoadCabinet,
                MachineStage.Cutting=>TutorialObjective.LoadCutter,MachineStage.TabOutput=>TutorialObjective.CutSheet,_=>null};
            if(objective is not null)changed=TutorialProgress.Record(state,objective.Value)||changed;
        }
        foreach(var item in items){
            if(WorkshopRules.TrySheet(item.ID,out var dry,out _,out _))
                changed=TutorialProgress.Record(state,dry ? TutorialObjective.CollectDrySheet:TutorialObjective.CollectWetSheet)||changed;
            if(item.ID.StartsWith(WorkshopRules.Owner+":tab-",StringComparison.Ordinal))
                changed=TutorialProgress.Record(state,item.TryCast<ProductItemInstance>()?.AppliedPackaging ? TutorialObjective.PackageTabs:TutorialObjective.CollectTabs)||changed;
        }
        return changed;
    }
    internal void Tick(WorkshopState state,WorkshopWorld world)
    {
        // Native saved quest entries can still carry grouped labels, even after completion.
        for(var i=0;i<QuestEntries.Count;i++)
            if(QuestEntries[i].Title!=TutorialProgress.Titles[i])QuestEntries[i].Title=TutorialProgress.Titles[i];
        var completed=TutorialProgress.Completed(state);
        if(!state.Introduced || Seen==completed)return;
        Seen=completed;
        if(QuestState==QuestState.Inactive)Begin();
        if(QuestState==QuestState.Completed)return;
        for(var i=0;i<QuestEntries.Count;i++){
            var desired=TutorialProgress.Has(state,(TutorialObjective)i) ? QuestState.Completed:TutorialProgress.Active(state,i) ? QuestState.Active:QuestState.Inactive;
            if(QuestEntries[i].State!=desired)QuestEntries[i].SetState(desired);
            if(desired==QuestState.Active && world.Office && (i is 4 or 5 || i>=8))QuestEntries[i].POIPosition=world.Office!.InteriorSpawnPoint.position;
            else QuestEntries[i].ClearPOI();
        }
        if(TutorialProgress.Finished(state) && QuestState!=QuestState.Completed)Complete();
    }
}
