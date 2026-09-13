using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using COTL.Common;

namespace COTLFollowerThoughtReader
{
    [BepInPlugin("com.mountmars.cotl.followerthoughtreader", "Follower Thought Reader", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Follower Thought Reader geladen!");

            try
            {
                var harmony = new Harmony("com.mountmars.cotl.followerthoughtreader");
                harmony.PatchAll();
                Log.LogInfo("Follower Thought Reader Harmony Patches erfolgreich angewendet!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Fehler beim Anwenden der Harmony Patches: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(FollowerBrain))]
    public static class FollowerBrain_AddThought_Patch
    {
        [HarmonyPatch(nameof(FollowerBrain.AddThought), new[] { typeof(Thought), typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        public static void Postfix_Thought(FollowerBrain __instance, Thought thought, bool InsetAtZero, bool forced)
        {
            ProcessThought(__instance, thought, forced);
        }

        [HarmonyPatch(nameof(FollowerBrain.AddThought), new[] { typeof(ThoughtData), typeof(bool) })]
        [HarmonyPostfix]
        public static void Postfix_ThoughtData(FollowerBrain __instance, ThoughtData thought, bool forced)
        {
            if (thought != null)
            {
                ProcessThought(__instance, thought.ThoughtType, forced);
            }
        }

        private static void ProcessThought(FollowerBrain brain, Thought thought, bool forced)
        {
            if (brain == null || brain.Info == null || thought == Thought.None)
            {
                return;
            }

            // Check if thoughts were blocked by the follower's current task
            if (brain.CurrentTask != null && brain.CurrentTask.BlockThoughts && !forced)
            {
                return;
            }

            try
            {
                string followerName = string.IsNullOrEmpty(brain.Info.Name) ? $"Follower_{brain.Info.ID}" : brain.Info.Name;
                int followerId = brain.Info.ID;

                string localizedName = string.Empty;
                string localizedDesc = string.Empty;

                try
                {
                    localizedName = FollowerThoughts.GetLocalisedName(thought, followerId);
                    localizedDesc = FollowerThoughts.GetLocalisedDescription(thought, followerId);
                }
                catch
                {
                    // Fallback to enum if localization is unavailable
                }

                string thoughtText;
                if (!string.IsNullOrEmpty(localizedDesc))
                {
                    thoughtText = localizedDesc;
                }
                else if (!string.IsNullOrEmpty(localizedName))
                {
                    thoughtText = localizedName;
                }
                else
                {
                    thoughtText = thought.ToString();
                }

                string logMessage = $"[THOUGHT] {followerName}: {thoughtText}";
                Plugin.Log?.LogInfo(logMessage);

                string payload = $"{followerName}: {thoughtText}\n";
                UdpMessenger.Send(payload, Plugin.Log);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Fehler beim Verarbeiten des Gedankens: {ex.Message}");
            }
        }
    }
}
