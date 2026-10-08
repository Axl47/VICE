using VICE.Syndicate.Compatibility;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.Reflection;

using static VICE.Syndicate.AmbushOverrides;
using static VICE.Syndicate.CartelInventory;
using static VICE.Syndicate.DebugModule;
using static VICE.Syndicate.DriveByEvent;
using static VICE.Syndicate.FrequencyOverrides;
using static VICE.Syndicate.InfluenceOverrides;
using static VICE.Syndicate.InterceptEvent;
using static VICE.Syndicate.MiniQuest;
using static VICE.Syndicate.EndGameQuest;
using static VICE.Syndicate.DealerActivity;
using static VICE.Syndicate.CartelGathering;
using static VICE.Syndicate.SabotageEvent;
using static VICE.Syndicate.StealBackCustomer;
using static VICE.Syndicate.AlliedExtension;
using static VICE.Syndicate.AlliedCartelDialogue;
using static VICE.Syndicate.CartelInfluenceChangePopup_Show_Patch;
using static VICE.Syndicate.SuppliesModule;
using static VICE.Syndicate.NPCInitHelper;

using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.NPCs.Framework;
using Il2CppScheduleOne.Dialogue;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Object;
using Il2Cpp;

[assembly: MelonInfo(typeof(VICE.Syndicate.SyndicateMod), VICE.Syndicate.BuildInfo.Name, VICE.Syndicate.BuildInfo.Version, VICE.Syndicate.BuildInfo.Author, VICE.Syndicate.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: HarmonyDontPatchAll]
[assembly: MelonOptionalDependencies("FishNet.Runtime")]
[assembly: MelonGame("TVGS", "Schedule I")]

[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: MelonLoader.VerifyLoaderVersion("0.7.3", true)]

namespace VICE.Syndicate
{
    public static class BuildInfo
    {
        public const string Name = "VICE Syndicate";
        public const string Description = "Cartel - Modded and configurable";
        public const string Author = "Axl47";
        public const string Company = null;
        public const string Version = "0.1.2";
        public const string DownloadLink = null;
    }

    public class SyndicateMod : MelonMod
    {
        public static ModPrefsHandler Prefs { get; private set; } 
        public static SyndicateMod Instance { get; private set; }
        public static ModConfig currentConfig;
        public static InfluenceConfig influenceConfig;
        public static CurrentEventCooldowns eventCooldowns;
        public static List<object> coros = new();
        public static bool registered = false;
        private LoadManager subscribedLoader;
        private UnityEngine.Events.UnityAction loadCompleteCallback;
        private bool clientSession;
        internal static VICE.Core.SubscriptionScope SaveSubscriptions = new();
        public static bool isSaving = false;

        #region await
        public static WaitForEndOfFrame frameEnd = new WaitForEndOfFrame();
        public static WaitForSeconds Wait01 = new WaitForSeconds(0.1f);
        public static WaitForSeconds Wait025 = new WaitForSeconds(0.25f);
        public static WaitForSeconds Wait05 = new WaitForSeconds(0.5f);
        public static WaitForSeconds Wait1 = new WaitForSeconds(1f);
        public static WaitForSeconds Wait2 = new WaitForSeconds(2f);
        public static WaitForSeconds Wait5 = new WaitForSeconds(5f);
        public static WaitForSeconds Wait10 = new WaitForSeconds(10f);
        public static WaitForSeconds Wait30 = new WaitForSeconds(30f);
        public static WaitForSeconds Wait60 = new WaitForSeconds(60f);
        #endregion

        #region Melon Prefs
        // On init sync .json config based on melon preferences if they differ from default
        public static void SyncConfig()
        {
            bool hasChanged = false;
            FieldInfo[] modConfigFields = currentConfig.GetType().GetFields();
            foreach (FieldInfo field in modConfigFields)
            {
                if (field.Name.Contains("endGameQuestMonologueSpeed")) continue;

                var entry = Prefs.modConfigCategory.GetEntry(field.Name);
                if (entry == null) continue;

                if ((bool)field.GetValue(currentConfig) == (bool)entry.BoxedValue)
                {
                    //MelonLogger.Msg("No changed value for :" + field.Name);
                    continue; // not changed
                }
                else
                {
                    hasChanged = true;
                    //MelonLogger.Msg("Update config value for :" + field.Name);
                    field.SetValue(currentConfig, entry.BoxedValue);
                }
            }

            // Because allied extension depends on the end game quest -> enable on incorrect state
            // regardless if its changed
            if (currentConfig.alliedExtensions && !currentConfig.endGameQuest)
            {
                hasChanged = true;
                currentConfig.endGameQuest = true;
            }
            if (hasChanged)
            {
                ConfigLoader.Save(currentConfig, logResult: false);
            }
        }
        #endregion

        public override void OnInitializeMelon()
        {
            base.OnInitializeMelon();

            var conflicts = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name)
                .Where(name => name != null && name.StartsWith("CartelEnforcer", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (conflicts.Length > 0) { MelonLogger.Error("Remove the original Cartel Enforcer DLL before using VICE Syndicate."); return; }
            VICE.Core.DefaultFiles.Seed(typeof(SyndicateMod).Assembly, "VICE.Syndicate.Defaults/",
                ModDataPaths.Root);
            Instance = this;
            loadCompleteCallback = (UnityEngine.Events.UnityAction)OnLoadCompleteCb;
            currentConfig = ConfigLoader.Load();
            influenceConfig = ConfigLoader.LoadInfluenceConfig();

            Prefs = new ModPrefsHandler();
            Prefs.SetupMelonPreferences();
            SyncConfig();
            foreach (var type in typeof(SyndicateMod).Assembly.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false)))
                HarmonyInstance.CreateClassProcessor(type).Patch();
            Difficulty.Initialize(Prefs.modConfigCategory, HarmonyInstance);
            MelonPreferences.SaveCategory<MelonPreferences_Category>("VICESyndicate", printmsg: false);

            MelonLogger.Msg("VICE Syndicate Mod Loaded");
            return;
        }

        public override void OnLateInitializeMelon() { if (Instance != null) Difficulty.RemoveLegacyHooks(HarmonyInstance); }

        #region Unity Methods

        public override void OnUpdate()
        {
            if (Instance == null) return;
            var loader = LoadManager.Instance;
            if (loader && (!subscribedLoader || subscribedLoader.Pointer != loader.Pointer))
            {
                DetachLoader();
                subscribedLoader = loader;
                loader.onLoadComplete.AddListener(loadCompleteCallback);
            }
            if (loader && loader.IsGameLoaded && Il2CppScheduleOne.PlayerScripts.Player.Local)
            {
                if (!registered && !clientSession) OnLoadCompleteCb();
            }
            else
            {
                if (registered) ExitPreTask();
                clientSession = false;
            }
            if (!registered || currentConfig == null) return;
            if (currentConfig.debugMode)
            {
                if (playerTransform != null && positionText != null)
                {
                    Vector3 playerPos = playerTransform.position;
                    string formattedPosition = $"X: {playerPos.x:F2}\nY: {playerPos.y:F2}\nZ: {playerPos.z:F2}";
                    positionText.text = formattedPosition;
                }
            }
            return;
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (Instance == null) return;
            if (buildIndex != 1)
            {
                if (registered) ExitPreTask();
                clientSession = false;
                DetachLoader();
                return;
            }
            var loader = LoadManager.Instance;
            if (!loader || (subscribedLoader && loader.Pointer == subscribedLoader.Pointer)) return;
            DetachLoader();
            subscribedLoader = loader;
            loader.onLoadComplete.AddListener(loadCompleteCallback);
        }

        private void DetachLoader()
        {
            if (subscribedLoader) subscribedLoader.onLoadComplete.RemoveListener(loadCompleteCallback);
            subscribedLoader = null;
        }

        public override void OnDeinitializeMelon()
        {
            if (registered) ExitPreTask();
            clientSession = false;
            DetachLoader();
        }

        internal static void SubscribeHour(Il2CppSystem.Action callback)
        {
            var time = NetworkSingleton<TimeManager>.Instance;
            time.onHourPass += callback;
            SaveSubscriptions.Add(() => { if (time) time.onHourPass -= callback; });
        }
        internal static void SubscribeDay(Il2CppSystem.Action callback)
        {
            var time = NetworkSingleton<TimeManager>.Instance;
            time.onDayPass += callback;
            SaveSubscriptions.Add(() => { if (time) time.onDayPass -= callback; });
        }
        internal static void SubscribeSleep(Il2CppSystem.Action callback)
        {
            var sleep = NetworkSingleton<SleepController>.Instance;
            sleep.OnSleepEnd += callback;
            SaveSubscriptions.Add(() => { if (sleep) sleep.OnSleepEnd -= callback; });
        }
        #endregion

        #region Mod Initialization and Coroutine load order

        private void OnLoadCompleteCb()
        {
            if (registered || clientSession) return;
            // The native callback can run before the loaded flag/local player is
            // published. OnUpdate starts setup once both are available.
            var loader = LoadManager.Instance;
            if (!loader || !loader.IsGameLoaded || !Il2CppScheduleOne.PlayerScripts.Player.Local) return;
            var network = Il2CppFishNet.InstanceFinder.NetworkManager;
            if (!network) return;
            // Guests receive the host's networked world. They have no local save
            // slot and must not load persistent state or start event simulations.
            if (!network.IsServer)
            {
                influenceConfig ??= ConfigLoader.LoadInfluenceConfig();
                clientSession = true;
                MelonLogger.Msg("Syndicate guest ready; persistent state and events are owned by the host.");
                return;
            }
            if (loader.ActiveSaveInfo == null) return;
            SaveSubscriptions.Dispose();
            SaveSubscriptions = new();
            registered = true;

            coros.Add(MelonCoroutines.Start(Setup()));
            return;
        }

        public static IEnumerator Setup()
        {
            yield return new WaitUntil((Il2CppSystem.Func<bool>)(() => LoadManager.Instance.IsGameLoaded && Il2CppScheduleOne.PlayerScripts.Player.Local));
            var network = Il2CppFishNet.InstanceFinder.NetworkManager;
            if (!network || !network.IsServer || LoadManager.Instance.ActiveSaveInfo == null) yield break;
            currentConfig = ConfigLoader.Load();
            influenceConfig = ConfigLoader.LoadInfluenceConfig();
            cartelStolenItems = ConfigLoader.LoadStolenItems();
            frequencyConfig = ConfigLoader.LoadEventFrequencyConfig();
            eventCooldowns = ConfigLoader.LoadPersistentCooldowns();
            dealerConfig = ConfigLoader.LoadDealerConfig();
            stolenNPCs = ConfigLoader.LoadStolenCustomers();

            SubscribeDay((Il2CppSystem.Action)OnDayPassChangePassive);
            PreparePackagingRefs();
            PopulateBombLocations();
            PrepareBombFXObjects();

            coros.Add(MelonCoroutines.Start(FetchUIElementsInit()));

            coros.Add(MelonCoroutines.Start(InitializeAndEvaluateDriveBy()));

            coros.Add(MelonCoroutines.Start(InitializeAmbush()));

            InitFrequencyOverrides();

            SubscribeHour((Il2CppSystem.Action)OnHourPassReduceCartelRegActHours);

            coros.Add(MelonCoroutines.Start(InitializeAndEvaluateMiniQuest()));

            coros.Add(MelonCoroutines.Start(EvaluateCartelIntercepts()));
            SubscribeHour((Il2CppSystem.Action)HourPassInterceptCooldown);

            coros.Add(MelonCoroutines.Start(InitializeEndGameQuest()));

            coros.Add(MelonCoroutines.Start(EvaluateDealerState()));

            coros.Add(MelonCoroutines.Start(SetupAlliedExtension()));

            SubscribeHour((Il2CppSystem.Action)OnHourPassTryGather);
            coros.Add(MelonCoroutines.Start(InitializeAndEvaluateSabotage()));

            SubscribeSleep((Il2CppSystem.Action)OnDayPassTrySteal);

            if (currentConfig.debugMode)
            {
                coros.Add(MelonCoroutines.Start(GodMode()));
                MelonCoroutines.Start(MakeUI());
            }

            // needed for true brothers quest only
            if (currentConfig.endGameQuest && currentConfig.alliedExtensions)
                coros.Add(MelonCoroutines.Start(ReplicateCopNPC()));

            coros.Add(MelonCoroutines.Start(ExtendGoonPool()));

            yield break;
        }

        public static void ReduceDriveByHours()
        {
            if (isSaving) return;
            if (!currentConfig.driveByEnabled) return;
            bool isHostile = NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile;
            if (!isHostile) return;

            hoursUntilDriveBy = Mathf.Clamp(hoursUntilDriveBy - 1, 0, int.MaxValue);
        }

        public static IEnumerator InitializeAndEvaluateDriveBy()
        {
            yield return MelonCoroutines.Start(InitializeDriveByData());
            SubscribeHour((Il2CppSystem.Action)ReduceDriveByHours);

            coros.Add(MelonCoroutines.Start(EvaluateDriveBy()));
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnDriveByAreaVisual());
            yield break;
        }

        public static IEnumerator InitializeAmbush()
        {
            yield return MelonCoroutines.Start(ApplyGameDefaultAmbush());
            yield return MelonCoroutines.Start(AddUserModdedAmbush());
            yield return MelonCoroutines.Start(SetAmbushGeneralSettings());
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnAmbushAreaVisual());
            yield break;
        }

        public static IEnumerator InitializeAndEvaluateSabotage()
        {
            yield return Wait2;
            if (!registered) yield break;

            Log("Starting Sabotage Event evaluation");
            SubscribeHour((Il2CppSystem.Action)SabotageEvent.ReduceSabotageHours);
            coros.Add(MelonCoroutines.Start(EvaluateBombEvent()));

            yield break;
        }

        public static IEnumerator InitializeAndEvaluateMiniQuest()
        {
            yield return Wait10;
            if (!registered) yield break;

            yield return InitMiniQuest();
            SubscribeDay((Il2CppSystem.Action)OnDayPassNewDiag);
            coros.Add(MelonCoroutines.Start(EvaluateMiniQuestCreation()));

            yield break;
        }

        public static IEnumerator ExtendGoonPool()
        {
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            WaitForEndOfFrame frameEnd = new WaitForEndOfFrame();
            CartelGoon goon = UnityEngine.Object.FindObjectOfType<CartelGoon>(true);

            if (goon == null)
            {
                Log("No goon found");
                yield break;
            }
            GameObject obj = goon.gameObject;
            obj.SetActive(false);
            yield return Wait01;
            yield return frameEnd;

            NPCData templateData = null;
            if (goon.NPCData == null)
            {
                Log("Original goon is missing NPC Template data!");
            }
            else
            {
                templateData = goon.NPCData.GetDeepCopy();
                if (templateData == null || templateData.WeatherBehaviour == null)
                    Log("Failed to get template NPCData");
                else
                    templateData.WeatherBehaviour.UseUmbrellaChance = 0f;
            }
            
            List<CartelGoon> clones = new();
            for (int i = 5; i <= 9; i++)
            {
                yield return Wait01;
                yield return frameEnd;

                GameObject clone = UnityEngine.Object.Instantiate(obj);
                NetworkObject newNob = clone.GetComponent<NetworkObject>();
                NPC npc = clone.GetComponent<NPC>();
                npc.GUID = GUIDManager.GenerateUniqueGUID();
                if (GUIDManager.IsGUIDAlreadyRegistered(npc.GUID))
                {
                    Log("Failed to generate registreable GUID");
                    continue;
                }
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;

                clone.name = $"CartelGoon ({i})";
                yield return MelonCoroutines.Start(InitiateClone(newNob, netManager, templateData));

                Log("Spawn");
                netManager.ServerManager.Spawn(newNob);

                CartelGoon newGoonComp = clone.GetComponent<CartelGoon>();
                if (newGoonComp.DialogueHandler == null)
                {
                    Log("New goon does not have Dialogue Handler initiated!");
                }
                else
                {
                    DialogueController controller = newGoonComp.DialogueHandler.GetComponent<DialogueController>();
                    if (controller == null)
                    {
                        Log("Failed to find dialogue controller!");
                    }
                    else
                        controller.Choices.Clear();
                }
                    

                clones.Add(newGoonComp);

                Log($"  Done: {i} -------------\n");
            }

            obj.SetActive(true);

            Log("Swapping array");
            try
            {
                GoonPool goonPool = NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool;
                CartelGoon[] originalGoons = goonPool.goons;

                int originalCount = originalGoons.Length;

                int extra = clones.Count;
                int newCount = originalCount + extra;

                CartelGoon[] newGoons = new CartelGoon[newCount];

                Log("Copying array");
                System.Array.Copy(originalGoons, newGoons, originalCount);

                if (goonPool.unspawnedGoons == null)
                {
                    Log("GoonPool unspawned list is uninitialized");
                }

                int clonesIdx = 0;
                for (int i = originalCount; i < newCount; i++)
                {
                    if (clonesIdx >= clones.Count) break;
                    CartelGoon newGoon = clones[clonesIdx];
                    newGoons[i] = newGoon;
                    goonPool.unspawnedGoons.Add(newGoon);
                    clonesIdx++;
                }
                Log("Replace array ");
                goonPool.goons = newGoons;

                Log("Array swapped now count: " + NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.goons.Length);
            } catch (Exception ex)
            {
                Log($"Failed to extend goon pool array:{ex}");
            }
            yield break;
        }

#endregion

        #region Harmony Patches for Saving and Coroutine safety
        static void ExitPreTask()
        {
            if (!registered) return;
            if (positionText) UnityEngine.Object.Destroy(positionText.gameObject);
            //MelonLogger.Msg("Pre-Exit Task");
            registered = false;
            SaveSubscriptions.Dispose();
            foreach (object coro in coros)
            {
                if (coro != null)
                    MelonCoroutines.Stop(coro);
            }

            coros.Clear();
            // Now mostly just the different mod related lists that got populated in init, reset and clear to repopulate everything on new load
            driveByLocations.Clear();
            regActivityHours.Clear();
            targetNPCs.Clear();
            cartelStolenItems.Clear();
            emptyDrops.Clear();
            targetNPCsList.Clear();
            targetNPCs.Clear();
            manorGoons.Clear();
            manorGoonGuids.Clear();
            spawnedGatherGoons.Clear();
            burningPlayers.Clear();
            locations.Clear();
            playerDealerStolen.Clear();
            consumedGUIDs.Clear();
            stolenNPCs.Clear();
            // This list is populated later by the allied supplies coroutine.
            // An interrupted load may exit before that coroutine has run.
            supplyLocations?.Clear();
            barrelLoot.Clear();

            // clear inner lists for car loot (kinda redundant since they get repopulated on save load)
            foreach (string key in carLoot.Keys.ToList())
                carLoot[key].Clear();

            // allied extension states and objects reset also
            foreach (string key in alliedDialogueKeys)
                persuasionChances[key] = 0f;

            allCartelDealers = null;

            influenceConfig = null;
            eventCooldowns = null;

            isSaving = false;

            // Now the created states and any boolean flags for events
            // QUests
            activeDefeatEnforcerQuest = null;
            defeatEnforcerCompleted = false;
            activeManorQuest = null;
            manorCompleted = false;
            activeCarMeetupQuest = null;
            carMeetupCompleted = false;

            hasGeneratedDefeatEnforcerQuest = false;
            hasGeneratedManorQuest = false;
            hasGeneratedCarQuest = false;

            RandomManorGenerator.ResetManorItemRef();

            // allied quests
            activeTruceIntro = null;
            activeAlliedSupplies = null;
            alliedSuppliesActive = false;
            alliedGuard = null;
            alliedVanObject = null;
            guardChoiceIndex = -1;

            activeTrueBrothersQuest = null;
            trueBrothersCompleted = false;
            encounterActive = false;

            // quest npcs
            fixer = null;
            ray = null;
            jeremy = null;
            crankyFrank = null;
            bossGoon = null;
            fixerDiagIndex = 0;
            rayChoiceIndex = 0;
            jeremyDiagIndex = -1;
            frankDiagIndex = -1;
            jeremyDialogueActive = false;

            // Mini quests and events
            lootGoblinIndex = -1;
            StageDeadDropsObserved = 0;
            StageGatheringsDefeated = 0;
            driveByActive = false;
            interceptingDeal = false;
            interceptor = null;
            startedCombat = false;
            areGoonsGathering = false;
            currentGatheringLocation = null;
            previousGatheringLocation = null;

            // sabotage related
            bombDefused = false;
            sabotageEventActive = false;
            sabotager = null;
            interactionsUntilDefuse = 6;
            intBomb = null;
            reactiveFire = null;
            bombInteractable = null;
            bombLight = null;
            bombCubeMat = null;
            bombSound = null;
            fireHandler = null;
            fireLight = null;

            // cartel inv
            jarPackaging = null;
            brickPackaging = null;
            cartelCashAmount = 0f;

            // temp variable for showing the influence reduction while truced
            showEnqueued = false;

            hoursUntilNextGathering = 3;
            currentDealerActivity = 0f;
            previousDealerActivity = 0f;
        }
        
        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { typeof(string) })]
        public static class SaveManager_Save_String_Patch
        {
            public static bool Prefix(SaveManager __instance, string saveFolderPath)
            {
                if (!registered || currentConfig == null) return true;
                if (!isSaving)
                {
                    isSaving = true;
                    ConfigLoader.Save(cartelStolenItems);
                    if (currentConfig.alliedExtensions)
                        ConfigLoader.Save(alliedQuests);

                    if (currentConfig.stealBackCustomers)
                    {
                        SerializedStolenNPCs data = new();
                        data.stolenCustomers = new();
                        foreach(StolenNPC stolenNPC in stolenNPCs)
                        {
                            StolenNPCSerialized serialized = new();
                            serialized.npcID = stolenNPC.npc.ID;
                            serialized.sampleChancesProcessed = stolenNPC.sampleChancesProcessed;
                            data.stolenCustomers.Add(serialized);
                        }

                        ConfigLoader.Save(data);
                    }

                    CurrentEventCooldowns currentCooldowns = new();

                    currentCooldowns.StealDeadDropCooldown = regActivityHours.First(x => x.cartelActivityClass == 0).hoursUntilEnable;
                    currentCooldowns.CartelCustomerDealCooldown = regActivityHours.First(x => x.cartelActivityClass == 1).hoursUntilEnable;
                    currentCooldowns.RobDealerCooldown = regActivityHours.First(x => x.cartelActivityClass == 2).hoursUntilEnable;
                    currentCooldowns.SprayGraffitiCooldown = regActivityHours.First(x => x.cartelActivityClass == 3).hoursUntilEnable;

                    currentCooldowns.DriveByCooldown = DriveByEvent.hoursUntilDriveBy;
                    currentCooldowns.GatheringCooldown = CartelGathering.hoursUntilNextGathering;
                    currentCooldowns.InterceptDealsCooldown = InterceptEvent.hoursUntilInterceptEvent;
                    
                    currentCooldowns.SabotageCooldowns = new();
                    SabotageEvent.locations.ForEach(x => currentCooldowns.SabotageCooldowns.Add(x.business.PropertyName, x.hoursUntilEnabled));

                    ConfigLoader.Save(currentCooldowns);
                    Log("Saved");
                }
                isSaving = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { })]
        public static class SaveManager_Save_Patch
        {
            public static bool Prefix(SaveManager __instance)
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(LoadManager), "ExitToMenu")]
        public static class LoadManager_ExitToMenu_Patch
        {
            public static bool Prefix(LoadManager __instance, SaveInfo autoLoadSave = null, MainMenuPopup.Data mainMenuPopup = null, bool preventLeaveLobby = false)
            {
                ExitPreTask();
                return true;
            }
        }

        [HarmonyPatch(typeof(DeathScreen), "LoadSaveClicked")]
        public static class DeathScreen_LoadSaveClicked_Patch
        {
            public static bool Prefix(DeathScreen __instance)
            {
                ExitPreTask();
                return true;
            }
        }
        #endregion

        #region Fix the Invisible Cartel Goon Bug
        /*
        Sometimes during cartel goon despawn (in mod added events non-daypass despawn)
        The cartel goon is "despawned" but stays active, invisible but aware.

        Despawn postfix checks each possible bugged state and then reverts it.
        Spawn postfix handles the logic for ensuring that later spawns after potential bug
        will not keep the cartel goon invisible while spawned.

        Then because the StayInside behaviour forces the cartel goons to navigate to 
        the nearest valid building but the the cartel goon starts attempting to go inside
        constantly invoking despawn.
         */
         
        [HarmonyPatch(typeof(CartelGoon), "Spawn")]
        public static class CartelGoon_Spawn_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CartelGoon __instance)
            {
                coros.Add(MelonCoroutines.Start(AfterSpawnEvaluate(__instance)));
                return;
            }

            public static IEnumerator AfterSpawnEvaluate(CartelGoon __instance)
            {
                yield return Wait01;
                if (!registered) yield break;

                if (!__instance.IsGoonSpawned)
                    __instance.IsGoonSpawned = true;

                if (NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.unspawnedGoons.Contains(__instance))
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.unspawnedGoons.Remove(__instance);

                if (!NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons.Contains(__instance))
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons.Add(__instance);

                if (__instance.Movement.SpeedController.DoesSpeedControlExist(BetaNpc.PauseId))
                    __instance.Movement.ResumeMovement();

                if (!__instance.Movement._agent.enabled)
                    __instance.Movement._agent.enabled = true;

                if (!__instance.isVisible)
                    __instance.SetVisible(true, false);

                if (!__instance.Awareness.enabled)
                    __instance.Awareness.SetAwarenessActive(true);

                if (__instance.goonMates.Count > 0)
                    __instance.goonMates.Clear();

                if (!__instance.gameObject.activeSelf)
                    __instance.gameObject.SetActive(true);

                yield break;
            }
        }

        [HarmonyPatch(typeof(CartelGoon), "Despawn")]
        public static class CartelGoon_Despawn_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CartelGoon __instance)
            {
                coros.Add(MelonCoroutines.Start(AfterDespawnEvaluate(__instance)));
                return;
            }

            public static IEnumerator AfterDespawnEvaluate(CartelGoon __instance)
            {
                yield return Wait01;
                if (!registered) yield break;

                if (__instance.IsGoonSpawned)
                    __instance.IsGoonSpawned = false;

                if (NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons.Contains(__instance))
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons.Remove(__instance);

                if (!NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.unspawnedGoons.Contains(__instance))
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.unspawnedGoons.Add(__instance);

                if (!__instance.Movement.SpeedController.DoesSpeedControlExist(BetaNpc.PauseId))
                    __instance.Movement.PauseMovement();

                if (__instance.Movement._agent.enabled)
                    __instance.Movement._agent.enabled = false;

                if (__instance.isVisible)
                    __instance.SetVisible(false, false);

                if (__instance.Awareness.enabled)
                    __instance.Awareness.SetAwarenessActive(false);

                if (__instance.goonMates.Count > 0)
                    __instance.goonMates.Clear();

                yield break;
            }
        }
        #endregion



    }
}
