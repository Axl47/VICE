using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppInterop.Runtime;
using S1API.Building;
using S1API.Lifecycle;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

namespace VICE.Lifestyle.Amenities;

internal sealed class AmenitiesFeature
{
    internal static AmenitiesFeature? Current;
    internal bool Ready;
    internal AmenityPanel? Panel;
    internal readonly Dictionary<string,AmenitySession> States=new();
    internal readonly Dictionary<string,AmenityView> Views=new();
    private readonly VICE.Core.SubscriptionScope Subscriptions=new();
    private MelonPreferences_Entry<bool> Enabled=null!;
    private double NextScan,NextPublish,NextFullPublish;
    private double Accumulator;

    internal void Initialize(HarmonyLib.Harmony harmony)
    {
        Current=this;Enabled=ModSupport.Entry(MelonPreferences.CreateCategory("VICE.Lifestyle.Amenities"),"EnablePrototypes",false,"Opt in to playable pool/darts placeholder furniture at Herbert's boutique. Definitions remain available for saved furniture.");
        ModSupport.Patch(harmony,typeof(InteractableObject),"StartInteract",typeof(AmenitiesFeature),prefix:nameof(Interact));
        GameLifecycle.OnPreLoad+=Preload;GameLifecycle.OnLoadComplete+=Loaded;GameLifecycle.OnPreSceneChange+=Clear;
        BuildEvents.OnBuildableItemInitialized+=Restore;
        Subscriptions.Add(()=>GameLifecycle.OnPreLoad-=Preload);Subscriptions.Add(()=>GameLifecycle.OnLoadComplete-=Loaded);
        Subscriptions.Add(()=>GameLifecycle.OnPreSceneChange-=Clear);Subscriptions.Add(()=>BuildEvents.OnBuildableItemInitialized-=Restore);
        MelonLogger.Msg("Home games prototypes available: opt in with [VICE.Lifestyle.Amenities] EnablePrototypes=true. Presentation is placeholder; no wagers.");
    }
    private static bool Interact(InteractableObject __instance)
    {
        var item=__instance.GetComponentInParent<BuildableItem>();
        if(!item||AmenityContent.Kind(item.ItemInstance?.ID) is null||Current?.Ready!=true)return true;
        Current.Panel?.Open(item.GUID.ToString());return false;
    }
    private static void Restore(BuildEventArgs args)
    {
        if(AmenityContent.Kind(args.ItemId) is null||!args.GameObject)return;
        try{PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(args.GameObject.GetComponent<BuildableItem>());}
        catch(Exception e){ModSupport.WarnOnce("amenities:restore",e.Message);}
    }
    private void Preload(){Clear();try{AmenityContent.Register();}catch(Exception e){MelonLogger.Error("Home-game item registration failed: "+e);}}
    private void Loaded()
    {
        try{AmenityContent.Register();if(Enabled.Value)AmenityContent.Shops();Panel=new(this);Ready=true;Scan();AmenityNetwork.Bind();MelonLogger.Msg("HOME_GAMES_READY placeholder darts/pool, opt-in shop="+Enabled.Value);}
        catch(Exception e){Clear();MelonLogger.Error("Home-game initialization failed: "+e);}
    }
    internal void OnUpdate()
    {
        if(!Ready||!LoadManager.Instance||LoadManager.Instance.IsLoading||!LoadManager.Instance.IsGameLoaded)return;
        try
        {
            AmenityNetwork.Tick();var now=AmenityNetwork.Now;
            if(now>=NextScan){NextScan=now+.5;Scan();if(Enabled.Value)AmenityContent.Shops();}
            Panel?.Tick();
            if(AmenityNetwork.Host)
            {
                var changed=false;
                foreach(var state in States.Values)changed|=state.Prune(actor=>Present(actor,state.Id),now);
                Accumulator=Math.Min(.15,Accumulator+Math.Min(Time.unscaledDeltaTime,.1f));
                while(Accumulator>=PoolSimulation.StepSeconds)
                {
                    Accumulator-=PoolSimulation.StepSeconds;
                    foreach(var state in States.Values.Where(s=>s.Moving))
                        if(state.Pool.Simulation.Step()){state.Pool.Resolve(state.Solo);state.Revision++;changed=true;}
                }
                if(changed||now>=NextFullPublish)
                {NextFullPublish=now+2;AmenityNetwork.Publish();}
                else if(now>=NextPublish)
                {NextPublish=now+.1;AmenityNetwork.Publish(activeOnly:true);}
            }
            foreach(var pair in Views)if(States.TryGetValue(pair.Key,out var state))pair.Value.Tick(state,Time.unscaledDeltaTime);
        }
        catch(Exception e){ModSupport.WarnOnce("amenities:update:"+e.GetType().Name,"Home-game update failed: "+e);}
    }
    private bool Present(int actor,string id)
    {
        var player=Player.PlayerList.ToArray().FirstOrDefault(p=>p&&p.PlayerLoaded&&p.Owner.ClientId==actor);
        return player&&CanUse(player!,id);
    }
    internal bool CanUse(Player player,string id)=>Views.TryGetValue(id,out var view)&&view.Item&&view.Item.Initialized&&!view.Item.IsDestroyed&&!view.Item.isGhost
        &&view.Item.ParentProperty&&view.Item.ParentProperty.IsOwned&&player&&player.PlayerLoaded&&!player.IsSleeping
        &&player.Health&&player.Health.IsAlive
        &&Vector3.Distance(player.transform.position,view.Visual.position)<=5;
    internal void Execute(Player player,AmenityCommand command)
    {
        if(!States.TryGetValue(command.Table,out var state))throw new InvalidOperationException("Table is unavailable. Reopen its controls.");
        var actor=player.Owner.ClientId;
        if(command.Action!="leave"&&!CanUse(player,command.Table))throw new InvalidOperationException("Use a table nearby in an owned property.");
        if((command.Action is "solo" or "duel" or "join")&&States.Values.Any(s=>s.Id!=state.Id&&s.Seats.Contains(actor)))
            throw new InvalidOperationException("Leave your other home game before joining this one.");
        state.Execute(actor,command,AmenityNetwork.Now);
    }
    private void Scan()
    {
        var found=new HashSet<string>();
        foreach(var property in Property.Properties.ToArray())
        {
            if(!property)continue;
            foreach(var item in property.BuildableItems.ToArray())
            {
                if(!item||!item.Initialized||item.isGhost||item.IsDestroyed||AmenityContent.Kind(item.ItemInstance?.ID) is not {} kind)continue;
                var id=item.GUID.ToString();found.Add(id);if(Views.ContainsKey(id))continue;
                if(Views.Count>=32)continue;
                PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(item);
                var view=new AmenityView(item,kind);Views[id]=view;
                if(AmenityNetwork.Host)States[id]=new(){Id=id,Kind=kind};
                var interactions=item.GetComponentsInChildren<InteractableObject>(true);
                if(!interactions.Any(i=>i.gameObject.activeInHierarchy))
                {
                    var interaction=item.gameObject.AddComponent<InteractableObject>();interaction.onHovered=new UnityEvent();interaction.onInteractStart=new UnityEvent();interaction.onInteractEnd=new UnityEvent();
                    interaction.MaxInteractionRange=3;interaction.RequiresUniqueClick=true;interaction.displayLocationCollider=item.BoundingCollider;
                }
                foreach(var interaction in item.GetComponentsInChildren<InteractableObject>(true))
                {
                    interaction.SetMessage(kind=="darts"?"Play prototype darts":"Play prototype pool");interaction.onInteractStart=new UnityEvent();
                    interaction.onInteractStart.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Current?.Panel?.Open(id)))!);
                }
            }
        }
        foreach(var id in Views.Keys.Except(found).ToArray())
        {Views[id].Dispose();Views.Remove(id);if(AmenityNetwork.Host)States.Remove(id);}
    }
    private void Clear()
    {
        Panel?.Dispose();Panel=null;Ready=false;
        foreach(var view in Views.Values)view.Dispose();Views.Clear();States.Clear();AmenityNetwork.Reset();NextScan=NextPublish=NextFullPublish=0;Accumulator=0;
    }
    internal void OnDeinitializeMelon(){Clear();Subscriptions.Dispose();Current=null;}
}
