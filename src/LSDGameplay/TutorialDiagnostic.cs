using S1API.Quests.Constants;
using UnityEngine;

namespace PersonalMods.LSDGameplay;

// Opt-in UI regression on an isolated save copy. Changes only unsaved diagnostic quest state.
internal sealed class TutorialDiagnostic
{
    private readonly string Output;
    private readonly WorkshopWorld World;
    private readonly WorkshopState LoadedState;
    private WorkshopState State=new(){Introduced=true,TutorialObjectives=0};
    private StudioTutorial? Quest;
    private int Phase=-2;
    private float Next;
    internal TutorialDiagnostic(string output,WorkshopWorld world,WorkshopState loadedState){Output=output;World=world;LoadedState=loadedState;}
    internal bool Tick()
    {
        if(Time.realtimeSinceStartup<Next)return false;
        switch(Phase){
            case -2:
                Quest=StudioTutorial.Current;
                if(LoadedState.Introduced && !TutorialProgress.Finished(LoadedState)){
                    if(Quest is null || Quest.QuestEntries.Count!=TutorialProgress.Count)throw new InvalidOperationException("Saved native tutorial did not recreate eighteen entries.");
                    Quest.Tick(LoadedState,World);
                    ExpectActive(Enumerable.Range(0,TutorialProgress.Count).Where(i=>TutorialProgress.Active(LoadedState,i)).ToArray());
                    if(Quest.QuestEntries.Where((entry,index)=>entry.Title!=TutorialProgress.Titles[index]).Any())throw new InvalidOperationException("Native tutorial retained old grouped labels.");
                    File.WriteAllText(Path.Combine(Output,"tutorial-migration-loaded.txt"),"Saved native tutorial loaded with eighteen correct labels and matching migrated active entries.");
                    Next=Time.realtimeSinceStartup+2;Phase=-1;return false;
                }
                Phase=0;return false;
            case -1:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"tutorial-migrated.png"));
                Next=Time.realtimeSinceStartup+0.6f;Phase=0;return false;
            case 0:
                StudioTutorial.Current?.Cancel();
                Quest=(StudioTutorial)S1API.Quests.QuestManager.CreateQuest<StudioTutorial>();
                if(Quest.QuestEntries.Count!=TutorialProgress.Count)throw new InvalidOperationException("Native quest did not create eighteen objectives.");
                Quest.Tick(State,World);ExpectActive(0,1,2,3);
                Next=Time.realtimeSinceStartup+2;Phase=1;return false;
            case 1:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"tutorial-supplies.png"));
                Next=Time.realtimeSinceStartup+0.6f;Phase=2;return false;
            case 2:
                TutorialProgress.Record(State,TutorialObjective.BuyInk);TutorialProgress.Record(State,TutorialObjective.BuyPaper);
                Quest!.Tick(State,World);ExpectActive(0,1);
                if(Quest.QuestEntries[2].State!=QuestState.Completed || Quest.QuestEntries[3].State!=QuestState.Completed)
                    throw new InvalidOperationException("Native checklist did not retain independent purchases.");
                foreach(var objective in new[]{TutorialObjective.BuyBench,TutorialObjective.BuyCabinet,TutorialObjective.PlaceBench,TutorialObjective.PlaceCabinet,TutorialObjective.CollectPrism})
                    TutorialProgress.Record(State,objective);
                TutorialProgress.RecordAction(State,"start");Quest.Tick(State,World);ExpectActive(9);
                Next=Time.realtimeSinceStartup+2;Phase=3;return false;
            case 3:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"tutorial-registration.png"));
                Next=Time.realtimeSinceStartup+0.6f;Phase=4;return false;
            default:
                State=WorkshopState.Deserialize("{\"Version\":1,\"Introduced\":true,\"TutorialStep\":7}");
                Quest!.Tick(State,World);ExpectActive(13);
                TutorialProgress.RecordAction(State,"collect",MachineStage.DryOutput);Quest.Tick(State,World);ExpectActive(14);
                TutorialProgress.RecordAction(State,"cut");Quest.Tick(State,World);ExpectActive(15);
                TutorialProgress.RecordAction(State,"finishcut");Quest.Tick(State,World);ExpectActive(16);
                TutorialProgress.RecordAction(State,"collect",MachineStage.TabOutput);Quest.Tick(State,World);ExpectActive(17);
                TutorialProgress.RecordAction(State,"pack");Quest.Tick(State,World);ExpectActive();
                if(Quest.QuestEntries.Any(e=>e.State!=QuestState.Completed))throw new InvalidOperationException("Native tutorial finished with incomplete entries.");
                // Simulate legacy labels restored after completion, without changing
                // the objective mask or cached Seen value. The completed quest must stay complete.
                for(var i=0;i<Quest.QuestEntries.Count;i++)Quest.QuestEntries[i].Title="Legacy grouped task "+i;
                Quest.Tick(State,World);
                if(Quest.QuestEntries.Where((entry,index)=>entry.Title!=TutorialProgress.Titles[index] || entry.State!=QuestState.Completed).Any())
                    throw new InvalidOperationException("Completed native tutorial retained legacy labels or reopened its entries.");
                File.WriteAllText(Path.Combine(Output,"tutorial-review-regression-passed.txt"),"Completed native quest labels refresh with unchanged progress, and all completed entry states are retained.");
                File.WriteAllText(Path.Combine(Output,"tutorial-check-passed.txt"),"Native eighteen-entry quest: independent supply checklist, single registration task, legacy drying migration, separated cutter/collection/packaging and final completion passed. Isolated save was not saved.");
                return true;
        }
    }
    private void ExpectActive(params int[] indices)
    {
        var actual=Quest!.QuestEntries.Select((entry,index)=>(entry,index)).Where(e=>e.entry.State==QuestState.Active).Select(e=>e.index);
        if(!actual.SequenceEqual(indices))throw new InvalidOperationException("Unexpected native tutorial active entries: "+string.Join(",",actual));
    }
}
