namespace PersonalMods.LSDGameplay;

internal enum TutorialObjective
{
    BuyBench, BuyCabinet, BuyPaper, BuyInk, PlaceBench, PlaceCabinet, OrderPrism, CollectPrism,
    LoadBatch, RegisterSheet, PrintSheet, CollectWetSheet, LoadCabinet, CollectDrySheet,
    LoadCutter, CutSheet, CollectTabs, PackageTabs
}

internal static class TutorialProgress
{
    internal static readonly string[] Titles={
        "Buy a Blotter Bench from the warehouse",
        "Buy a drying cabinet from the warehouse",
        "Buy blank blotter stock from the warehouse",
        "Buy a print cartridge from the hardware store",
        "Place the Blotter Bench in your Sewer Office",
        "Place the drying cabinet in your Sewer Office",
        "Order Prism Base through the Studio phone app",
        "Collect Prism Base at the hardware store's purple star",
        "Create a batch at the Blotter Bench",
        "Register the sheet on the lightbox",
        "Roll the artwork onto the sheet",
        "Collect the wet sheet from the bench",
        "Load the wet sheet into the drying cabinet",
        "Collect the ready sheet after drying",
        "Load the ready sheet at the bench's cutter",
        "Cut along the sheet's guides",
        "Collect the finished tabs",
        "Package your tabs at a packaging station"
    };
    internal const int Count=18, All=(1<<Count)-1;
    private static int Bit(TutorialObjective objective)=>1<<(int)objective;
    internal static int Completed(WorkshopState state)=>state.TutorialObjectives??FromLegacy(state.TutorialStep);
    internal static bool Has(WorkshopState state,TutorialObjective objective)=>(Completed(state)&Bit(objective))!=0;
    internal static bool Finished(WorkshopState state)=>Completed(state)==All;
    internal static int FromLegacy(int step)
    {
        // Each legacy number identifies the current grouped task, not a completed task.
        var count=step switch{0 or 1=>0,2=>4,3=>6,4=>8,5=>9,6=>11,7=>13,8=>16,9=>17,10=>18,_=>throw new InvalidDataException("Invalid legacy tutorial progress.")};
        return (1<<count)-1;
    }
    internal static bool Record(WorkshopState state,TutorialObjective objective)
    {
        var before=Completed(state);var mask=before|Evidence(objective);
        var legacy=LegacyStep(mask);
        if(mask==before && state.TutorialStep==legacy)return false;
        state.TutorialObjectives=mask;
        state.TutorialStep=legacy;
        state.Revision++;return true;
    }
    private static int Evidence(TutorialObjective objective)
    {
        var bit=Bit(objective);
        return objective switch{
            TutorialObjective.PlaceBench=>bit|Bit(TutorialObjective.BuyBench),
            TutorialObjective.PlaceCabinet=>bit|Bit(TutorialObjective.BuyCabinet),
            TutorialObjective.CollectPrism=>bit|Bit(TutorialObjective.OrderPrism),
            TutorialObjective.LoadBatch=>bit|Evidence(TutorialObjective.PlaceBench)|Bit(TutorialObjective.BuyPaper)|Bit(TutorialObjective.BuyInk)|Evidence(TutorialObjective.CollectPrism),
            TutorialObjective.RegisterSheet=>bit|Evidence(TutorialObjective.LoadBatch),
            TutorialObjective.PrintSheet=>bit|Evidence(TutorialObjective.RegisterSheet),
            TutorialObjective.CollectWetSheet=>bit|Evidence(TutorialObjective.PrintSheet),
            TutorialObjective.LoadCabinet=>bit|Evidence(TutorialObjective.CollectWetSheet)|Evidence(TutorialObjective.PlaceCabinet),
            TutorialObjective.CollectDrySheet=>bit|Evidence(TutorialObjective.LoadCabinet),
            TutorialObjective.LoadCutter=>bit|Evidence(TutorialObjective.CollectDrySheet),
            TutorialObjective.CutSheet=>bit|Evidence(TutorialObjective.LoadCutter),
            TutorialObjective.CollectTabs=>bit|Evidence(TutorialObjective.CutSheet),
            TutorialObjective.PackageTabs=>bit|Evidence(TutorialObjective.CollectTabs),
            _=>bit};
    }
    internal static bool Active(WorkshopState state,int index)
    {
        if(!state.Introduced || Has(state,(TutorialObjective)index))return false;
        var mask=Completed(state);
        var first=Enumerable.Range(0,Count).FirstOrDefault(i=>(mask&(1<<i))==0,Count);
        // Supplies and placement are native checklist groups; production is one task at a time.
        return first<4 ? index<4:first<6 ? index is 4 or 5:index==first;
    }
    internal static bool RecordAction(WorkshopState state,string action,MachineStage priorStage=MachineStage.Empty)
    {
        TutorialObjective? objective=action switch{
            "order"=>TutorialObjective.OrderPrism,"start"=>TutorialObjective.LoadBatch,
            "prepare"=>TutorialObjective.RegisterSheet,"print"=>TutorialObjective.PrintSheet,
            "dry"=>TutorialObjective.LoadCabinet,"cut"=>TutorialObjective.LoadCutter,
            "finishcut"=>TutorialObjective.CutSheet,
            "collect"=>priorStage switch{MachineStage.WetOutput=>TutorialObjective.CollectWetSheet,MachineStage.DryOutput=>TutorialObjective.CollectDrySheet,MachineStage.TabOutput=>TutorialObjective.CollectTabs,_=>null},
            _=>null};
        return objective is not null && Record(state,objective.Value);
    }
    internal static int LegacyStep(int mask)
    {
        // Older mods infer every task before this milestone as complete. Never
        // advance across a missing purchase or placement when later tasks happen first.
        for(var step=10;step>=2;step--){
            var prefix=FromLegacy(step);
            if((mask&prefix)==prefix)return step;
        }
        return 1;
    }
}
