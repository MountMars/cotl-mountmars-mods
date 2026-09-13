using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace COTLTimeLogger
{
    // Plugin GUID, Name und Version festlegen
    [BepInPlugin("com.mountmars.cotl.timelogger", "COTL Time Logger", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static float Timer = 0f;
        public const float LogInterval = 3.0f; // Alle 10 Sekunden

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("COTL Time Logger geladen!");

            // Harmony Patches aktivieren
            try
            {
                var harmony = new Harmony("com.jens.cotl.timelogger");
                harmony.PatchAll();
                Log.LogInfo("COTL Time Logger Harmony Patches erfolgreich angewendet!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Fehler beim Anwenden der Harmony Patches: {ex.Message}");
            }
        }
        public static void LogGameTime()
        {
            try
            {
                int day = TimeManager.CurrentDay;
                float time = TimeManager.CurrentGameTime;

                string logLine = $"[TIME] Day: {day} | Time: {time:F2}\n";

                // In das BepInEx-Konsolenlog ausgeben
                Log.LogInfo($"[TIME LOG] Day: {day} | Time: {time:F2}");

                // Per UDP an localhost:9999 senden
                COTL.Common.UdpMessenger.Send(logLine, Log);
            }
            catch (Exception ex)
            {
                Log.LogError($"Fehler beim Verarbeiten der Spielzeit: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SimulationManager), "Update")]
    public static class SimulationManager_Update_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (DataManager.Instance == null)
            {
                return;
            }

            Plugin.Timer += Time.unscaledDeltaTime;

            if (Plugin.Timer >= Plugin.LogInterval)
            {
                Plugin.Timer = 0f;
                Plugin.LogGameTime();
            }
        }
    }
}
