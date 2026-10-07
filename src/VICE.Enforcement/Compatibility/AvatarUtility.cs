// Adapted from XOWithSauce's MIT-licensed NACops for the beta's modern avatar system.
using System.Collections;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.VoiceOver;
using UnityEngine;

namespace NACops;

public static class AvatarUtility
{
    public static readonly Dictionary<int, AvatarSettings> instancedSettings = new();
    public static readonly List<VODatabase> maleVOs = new(), femaleVOs = new();

    public static void SetRandomAvatar(PoliceOfficer officer, ref AvatarSettings legacySettings)
    {
        legacySettings = null;
        var appearance = officer.Avatar.Appearance;
        var naked = appearance.AppliedNakedAppearance?.Clone();
        if (naked is null) return;
        naked.SkinColor = new[] { new Color(0.36f, 0.30f, 0.27f), new Color(0.45f, 0.37f, 0.34f), new Color(0.78f, 0.59f, 0.44f) }[UnityEngine.Random.Range(0, 3)];
        naked.SetHairAndEyebrowColor(UnityEngine.Random.ColorHSV(0f, 1f, 0f, 0.4f, 0.05f, 0.5f));
        appearance.ApplyNakedAppearance(naked);
    }

    public static IEnumerator PIAvatar(PoliceOfficer officer)
    {
        // Texture-layer paths were replaced by avatar objects. Copy a loaded
        // civilian's modern appearance and outfit to keep the investigator disguised.
        var civilians = new List<NPC>();
        foreach (var npc in NPCManager.NPCRegistry)
            if (npc && npc.TryCast<PoliceOfficer>() is null && npc.Avatar && npc.Avatar.Appearance
                && npc.Avatar.Appearance.AppliedNakedAppearance is not null && npc.NPCData?.Appearance?.DefaultOutfit)
                civilians.Add(npc);
        if (civilians.Count > 0)
        {
            var donor = civilians[UnityEngine.Random.Range(0, civilians.Count)];
            officer.Avatar.Appearance.ApplyNakedAppearance(donor.Avatar.Appearance.AppliedNakedAppearance.Clone());
            officer.Avatar.Appearance.ApplyOutfit(donor.NPCData.Appearance.DefaultOutfit);
            if (officer.PoliceBelt) officer.PoliceBelt.gameObject.SetActive(false);
            SetVOEmitter(officer);
        }
        else ModSupport.WarnOnce("vice:investigator:outfit", "No civilian appearance is available for the investigator disguise.");
        yield return GenerateImpostor(officer, null);
    }

    public static IEnumerator SetRaiderAvatar(PoliceOfficer officer)
    {
        AvatarSettings unused = null;
        SetRandomAvatar(officer, ref unused);
        yield return GenerateImpostor(officer, null);
    }

    public static IEnumerator GenerateImpostor(PoliceOfficer officer, AvatarSettings unused)
    {
        var movement = officer.Movement;
        var avatar = officer.Avatar;
        var position = officer.transform.position;
        var rotation = officer.transform.rotation;
        var agentEnabled = movement._agent && movement._agent.enabled;
        var movementEnabled = movement.enabled;
        var animationEnabled = avatar.Animation.enabled;
        var bodyActive = avatar.BodyContainer.gameObject.activeSelf;
        try
        {
            if (movement._agent) movement._agent.enabled = false;
            movement.enabled = false;
            officer.transform.SetPositionAndRotation(RuntimeImpostor.targetPosition, Quaternion.Euler(RuntimeImpostor.targetRotationEuler));
            avatar.Animation.SetGrounded(true);
            yield return NACops.Wait1;
            avatar.Animation.enabled = false;
            avatar._impostor?.Disable();
            avatar.BodyContainer.gameObject.SetActive(true);
            yield return NACops.Wait01;
            avatar.SetImpostorTexture(RuntimeImpostor.CreateImpostor(officer));
        }
        finally
        {
            if (officer)
            {
                officer.transform.SetPositionAndRotation(position, rotation);
                avatar.Animation.enabled = animationEnabled;
                if (movement._agent) movement._agent.enabled = agentEnabled;
                movement.enabled = movementEnabled;
                avatar.BodyContainer.gameObject.SetActive(bodyActive);
                avatar._impostor?.Enable();
            }
        }
    }

    public static void SetupVODatabaseRefs()
    {
        foreach (var database in Resources.FindObjectsOfTypeAll<VODatabase>())
        {
            if (maleVOs.Contains(database) || femaleVOs.Contains(database)) continue;
            if (database.name is "Tyler VO" or "Hippie VO" or "Redneck VO") maleVOs.Add(database);
            if (database.name is "Female1 VO" or "Female2 VO" or "Timid VO") femaleVOs.Add(database);
        }
    }

    public static void SetVOEmitter(NPC npc)
    {
        if (maleVOs.Count == 0 || femaleVOs.Count == 0) SetupVODatabaseRefs();
        var pool = npc.Avatar.Appearance._appliedGender < 0.5f ? maleVOs : femaleVOs;
        if (pool.Count == 0 || !npc.VoiceOverEmitter) return;
        npc.VoiceOverEmitter.SetDatabase(pool[UnityEngine.Random.Range(0, pool.Count)]);
        npc.VoiceOverEmitter.SetDefaultPitch(UnityEngine.Random.Range(0.9f, 1.15f));
    }
}
