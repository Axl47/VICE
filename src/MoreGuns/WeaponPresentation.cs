using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Dragging;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Trash;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.MoreGuns;

internal static class WeaponPresentation
{
    internal static void PrepareAvatar(AvatarEquippable equippable)
    {
        var weapon = equippable.TryCast<AvatarWeapon>();
        if (weapon is not null && weapon.EquipClips is null)
            weapon.EquipClips = new Il2CppReferenceArray<AudioClip>(0);
    }

    internal static TrashItem? PrepareMagazineTrash(GameObject? prefab, GunOptions options)
    {
        if (prefab is null || !prefab) return null;
        try
        {
            var trash = prefab.GetComponent<TrashItem>() ?? throw new InvalidOperationException("Missing magazine trash component.");
            var body = prefab.GetComponent<Rigidbody>() ?? throw new InvalidOperationException("Missing magazine rigidbody.");
            var interaction = prefab.GetComponent<InteractableObject>() ?? throw new InvalidOperationException("Missing magazine interaction component.");
            // The beta expects TrashItemDraggable rather than the old generic Draggable.
            // Otherwise native reload consumes the spare, throws in Initialize, and never refills the gun.
            var draggable = prefab.GetComponent<TrashItemDraggable>();
            if (!draggable)
            {
                var legacy = prefab.GetComponent<Draggable>();
                if (legacy) Object.DestroyImmediate(legacy, true); // Private, in-memory bundle prefab.
                draggable = prefab.AddComponent<TrashItemDraggable>();
            }
            trash.Id = options.Id + "mag"; // The serialized legacy field was named ID.
            trash.Size = 2;
            trash.SellValue = 3;
            trash._canGoInTrashContainer = true;
            trash._rigidbody = body;
            trash._draggable = draggable;
            draggable._trashItem = trash;
            draggable._rigidbody = body;
            draggable._interactable = interaction;
            return trash;
        }
        catch (Exception error)
        {
            // A cosmetic discard must never interrupt native ammo consumption/refill.
            ModSupport.WarnOnce($"guns:trash:prepare:{options.Id}", $"{options.Name} magazine discard disabled: {error.Message}");
            return null;
        }
    }
}
