using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using COTL.Common;

namespace COTLFollowerPoopWatcher
{
    [BepInPlugin("com.mountmars.cotl.followerpoopwatcher", "Follower Poop Watcher", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Follower Poop Watcher geladen!");

            try
            {
                var harmony = new Harmony("com.mountmars.cotl.followerpoopwatcher");
                harmony.PatchAll();
                Log.LogInfo("Follower Poop Watcher Harmony Patches erfolgreich angewendet!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Fehler beim Anwenden der Harmony Patches: {ex.Message}");
            }
        }

        public static void SendPoopNotification(FollowerBrain brain, string actionDetail)
        {
            if (brain == null || brain.Info == null)
            {
                return;
            }

            try
            {
                string followerName = string.IsNullOrEmpty(brain.Info.Name) ? $"Follower_{brain.Info.ID}" : brain.Info.Name;
                string logMessage = $"[POOP] {followerName}: {actionDetail}";
                Log?.LogInfo(logMessage);

                string payload = $"{followerName}: {actionDetail}\n";
                UdpMessenger.Send(payload, Log);
            }
            catch (Exception ex)
            {
                Log?.LogError($"Fehler beim Senden der Kack-Benachrichtigung: {ex.Message}");
            }
        }

        public static string FormatSpecialPoop(StructureBrain.TYPES poopType)
        {
            switch (poopType)
            {
                case StructureBrain.TYPES.POOP_GOLD:
                    return "Gold";
                case StructureBrain.TYPES.POOP_RAINBOW:
                    return "Rainbow";
                case StructureBrain.TYPES.POOP_MASSIVE:
                    return "Massive";
                case StructureBrain.TYPES.POOP_DEVOTION:
                    return "Devotion";
                case StructureBrain.TYPES.POOP_GLOW:
                    return "Glow";
                case StructureBrain.TYPES.POOP_ROTSTONE:
                    return "Rotstone";
                case StructureBrain.TYPES.POOP_PET:
                    return "Pet";
                default:
                    return null;
            }
        }
    }

    [HarmonyPatch(typeof(FollowerTask_Bathroom), "ProgressTask")]
    public static class FollowerTask_Bathroom_ProgressTask_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(FollowerTask_Bathroom __instance)
        {
            if (__instance == null || __instance.Brain == null) return;

            try
            {
                bool hasToilet = __instance.UsingStructureID > 0;

                string actionDetail;
                if (hasToilet)
                {
                    actionDetail = "Used the outhouse";
                }
                else
                {
                    StructureBrain.TYPES poopType = Traverse.Create(__instance).Field<StructureBrain.TYPES>("poopType").Value;
                    string special = Plugin.FormatSpecialPoop(poopType);
                    actionDetail = string.IsNullOrEmpty(special)
                        ? "Pooped on the floor"
                        : $"Pooped ({special}) on the floor";
                }

                Plugin.SendPoopNotification(__instance.Brain, actionDetail);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Fehler im FollowerTask_Bathroom Patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(FollowerTask_InstantPoop), "ProgressTask")]
    public static class FollowerTask_InstantPoop_ProgressTask_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(FollowerTask_InstantPoop __instance)
        {
            if (__instance == null || __instance.Brain == null) return;

            try
            {
                StructureBrain.TYPES poopType = Traverse.Create(__instance).Field<StructureBrain.TYPES>("poopType").Value;
                string special = Plugin.FormatSpecialPoop(poopType);
                string actionDetail = string.IsNullOrEmpty(special)
                    ? "Pooped on the floor"
                    : $"Pooped ({special}) on the floor";

                Plugin.SendPoopNotification(__instance.Brain, actionDetail);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Fehler im FollowerTask_InstantPoop Patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(FollowerTask_IllPoopy), "ProgressTask")]
    public static class FollowerTask_IllPoopy_ProgressTask_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(FollowerTask_IllPoopy __instance)
        {
            if (__instance == null || __instance.Brain == null) return;

            try
            {
                FollowerBrain brain = __instance.Brain;
                string special = null;
                if (brain.Info != null)
                {
                    if (brain.Info.HasTrait(FollowerTrait.TraitType.RoyalPooper))
                    {
                        special = "Gold";
                    }
                    else if (brain.Info.HasTrait(FollowerTrait.TraitType.RotstonePooper) || brain.Info.HasTrait(FollowerTrait.TraitType.Mutated))
                    {
                        special = "Rotstone";
                    }
                }

                string actionDetail = string.IsNullOrEmpty(special)
                    ? "Pooped on the floor (Sick)"
                    : $"Pooped ({special}) on the floor (Sick)";

                Plugin.SendPoopNotification(brain, actionDetail);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Fehler im FollowerTask_IllPoopy Patch: {ex.Message}");
            }
        }
    }
}
