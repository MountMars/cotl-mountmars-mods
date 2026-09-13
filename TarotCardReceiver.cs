using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Lamb.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace COTLTarotCardReceiver
{
    [BepInPlugin("com.mountmars.cotl.tarotcardreceiver", "Tarot Card Receiver", "1.0.0")]
    public class TarotCardReceiverPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static TarotCards.Card CurrentTestCard = TarotCards.Card.TheDeal;

        // Thread-safe queues
        public static readonly ConcurrentQueue<TarotCards.Card> PendingCardsToShow = new ConcurrentQueue<TarotCards.Card>();
        public static readonly ConcurrentQueue<TarotCards.Card> ClauneckQueue = new ConcurrentQueue<TarotCards.Card>();

        public static bool OverrideClauneck = true;

        // Windows Win32 API for direct hardware key detection
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public const int VK_F7 = 0x76;
        public const int VK_F8 = 0x77;
        public const int VK_F9 = 0x78;
        public const int VK_F10 = 0x79;

        private static float keyCooldownTimer = 0f;
        private static bool hasLoggedReady = false;

        // Deduplication timer to avoid double-triggers from simultaneous UDP + HTTP
        private static float lastCardTime = 0f;
        private static TarotCards.Card lastEnqueuedCard;

        // File-based IPC paths (bypasses Windows firewall / Avast / loopback socket locks)
        private static string commandFilePath;
        private static string queueFilePath;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Tarot Card Receiver Plugin loaded!");

            try
            {
                commandFilePath = Path.Combine(Paths.PluginPath, "tarot_command.txt");
                queueFilePath = Path.Combine(Paths.PluginPath, "tarot_queue.txt");
            }
            catch
            {
                commandFilePath = "tarot_command.txt";
                queueFilePath = "tarot_queue.txt";
            }

            try
            {
                var harmony = new Harmony("com.mountmars.cotl.tarotcardreceiver");
                harmony.PatchAll();
                Log.LogInfo("Tarot Card Receiver Harmony patches applied successfully!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error applying Harmony patches: {ex.Message}");
            }
        }

        public static bool TryParseCard(string raw, out TarotCards.Card card)
        {
            card = TarotCards.Card.TheDeal;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string cleaned = raw.Trim().Trim('\0', '\r', '\n', ' ', '\t');

            // 1. JSON payload support: {"card": "TheDeal"} or {"id": 13}
            if (cleaned.StartsWith("{") && cleaned.EndsWith("}"))
            {
                var match = Regex.Match(cleaned, "\"card\"\\s*:\\s*\"?([^\",}]+)\"?", RegexOptions.IgnoreCase);
                if (!match.Success)
                    match = Regex.Match(cleaned, "\"name\"\\s*:\\s*\"?([^\",}]+)\"?", RegexOptions.IgnoreCase);
                if (!match.Success)
                    match = Regex.Match(cleaned, "\"id\"\\s*:\\s*([0-9]+)", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    cleaned = match.Groups[1].Value.Trim().Trim('\0', '\r', '\n', ' ', '\t');
                }
            }

            // 2. Strip URL path slash, query strings, or common prefixes
            if (cleaned.StartsWith("/")) cleaned = cleaned.Substring(1).Trim();
            if (cleaned.StartsWith("?card=", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(6).Trim();
            else if (cleaned.StartsWith("card=", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("card:", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("Card.", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("TarotCards.Card.", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(16).Trim();

            if (cleaned.EndsWith("/")) cleaned = cleaned.Substring(0, cleaned.Length - 1).Trim();

            // 3. Direct enum parse
            if (Enum.TryParse<TarotCards.Card>(cleaned, true, out card))
            {
                return true;
            }

            // 4. Normalized without spaces, underscores, dashes
            string normalized = cleaned.Replace(" ", "").Replace("_", "").Replace("-", "");
            if (Enum.TryParse<TarotCards.Card>(normalized, true, out card))
            {
                return true;
            }

            // Roman numerals e.g. HeartsI -> Hearts1
            string romanNormalized = normalized
                .Replace("III", "3")
                .Replace("II", "2")
                .Replace("IV", "4")
                .Replace("I", "1");
            if (Enum.TryParse<TarotCards.Card>(romanNormalized, true, out card))
            {
                return true;
            }

            // 5. Numeric ID
            if (int.TryParse(cleaned, out int cardId) && Enum.IsDefined(typeof(TarotCards.Card), cardId))
            {
                card = (TarotCards.Card)cardId;
                return true;
            }

            return false;
        }

        public static void ProcessIncomingCard(string candidate, string source)
        {
            if (TryParseCard(candidate, out var card))
            {
                // Deduplicate if exact same card arrived within 1.0s (e.g. from simultaneous UDP + HTTP)
                if (card == lastEnqueuedCard && (Time.unscaledTime - lastCardTime) < 1.0f)
                {
                    Log.LogInfo($"[TarotCardReceiver] Duplicate '{card}' from {source} ignored (within 1s).");
                    return;
                }

                lastCardTime = Time.unscaledTime;
                lastEnqueuedCard = card;

                PendingCardsToShow.Enqueue(card);
                ClauneckQueue.Enqueue(card);
                CurrentTestCard = card;
                Log.LogInfo($"[TarotCardReceiver] Successfully queued card via {source}: {card}");
            }
            else
            {
                Log.LogWarning($"[TarotCardReceiver] Unknown card payload from {source}: '{candidate}'");
            }
        }

        private static bool CheckHardwareKeyDown(int vKey)
        {
            try
            {
                return (GetAsyncKeyState(vKey) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckKeyPressed(int vKey, KeyCode legacyKey, Key newKey)
        {
            // 1. Hardware Windows key state
            if (CheckHardwareKeyDown(vKey))
            {
                return true;
            }

            // 2. Rewired Input
            try
            {
                if (Rewired.ReInput.isReady &&
                    Rewired.ReInput.controllers != null &&
                    Rewired.ReInput.controllers.Keyboard != null &&
                    Rewired.ReInput.controllers.Keyboard.GetKeyDown(legacyKey))
                {
                    return true;
                }
            }
            catch { }

            // 3. Unity New Input System
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard[newKey].wasPressedThisFrame)
                {
                    return true;
                }
            }
            catch { }

            // 4. Unity Legacy Input
            try
            {
                if (Input.GetKeyDown(legacyKey))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static void TickInGame()
        {
            // Strictly guard: only execute once save data is loaded (in camp or dungeon)
            if (DataManager.Instance == null)
            {
                hasLoggedReady = false;
                return;
            }

            if (!hasLoggedReady)
            {
                hasLoggedReady = true;
                Log.LogInfo("[TarotCardReceiver] Active in-game! Ready to receive cards via File IPC.");
            }

            // 0. Process File-based IPC (100% immune to Windows/Avast firewall or socket issues)
            try
            {
                if (!string.IsNullOrEmpty(commandFilePath) && File.Exists(commandFilePath))
                {
                    string fileContent = File.ReadAllText(commandFilePath).Trim();
                    try { File.Delete(commandFilePath); } catch { }

                    if (!string.IsNullOrEmpty(fileContent))
                    {
                        Log.LogInfo($"[TarotCardReceiver] File IPC trigger detected: '{fileContent}'");
                        ProcessIncomingCard(fileContent, "File IPC");
                    }
                }

                if (!string.IsNullOrEmpty(queueFilePath) && File.Exists(queueFilePath))
                {
                    string[] lines = File.ReadAllLines(queueFilePath);
                    try { File.Delete(queueFilePath); } catch { }

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            Log.LogInfo($"[TarotCardReceiver] File Queue IPC trigger detected: '{trimmed}'");
                            ProcessIncomingCard(trimmed, "File Queue IPC");
                        }
                    }
                }
            }
            catch { }

            // 1. Process pending cards from File IPC
            while (PendingCardsToShow.TryDequeue(out var incomingCard))
            {
                CurrentTestCard = incomingCard;
                Log.LogInfo($"[TarotCardReceiver] Processing incoming card on main thread: {incomingCard}");
                ShowCardBanner(incomingCard);
            }

            // 2. Cooldown check to prevent rapid repeating of hotkeys
            if (Time.unscaledTime < keyCooldownTimer)
            {
                return;
            }

            // 3. Hotkeys
            if (CheckKeyPressed(VK_F8, KeyCode.F8, Key.F8))
            {
                keyCooldownTimer = Time.unscaledTime + 0.4f;
                Log.LogInfo($"[TarotCardReceiver] >>> F8 DETECTED! Showing card: {CurrentTestCard} <<<");
                ShowCardBanner(CurrentTestCard);
                return;
            }

            if (CheckKeyPressed(VK_F7, KeyCode.F7, Key.F7))
            {
                keyCooldownTimer = Time.unscaledTime + 0.3f;
                CycleCard(-1);
                return;
            }

            if (CheckKeyPressed(VK_F9, KeyCode.F9, Key.F9))
            {
                keyCooldownTimer = Time.unscaledTime + 0.3f;
                CycleCard(1);
                return;
            }

            if (CheckKeyPressed(VK_F10, KeyCode.F10, Key.F10))
            {
                keyCooldownTimer = Time.unscaledTime + 0.4f;
                ClauneckQueue.Enqueue(CurrentTestCard);
                Log.LogInfo($"[TarotCardReceiver] F10 pressed: Enqueued {CurrentTestCard} for next Clauneck altar choice.");
                ShowNotification($"Queued for Clauneck: {CurrentTestCard}");
                return;
            }
        }

        private void Update()
        {
            TickInGame();
        }

        public static void ShowCardBanner(TarotCards.Card cardType)
        {
            try
            {
                TarotCards.TarotCard card = new TarotCards.TarotCard(cardType, 0);

                if (UIManager.Instance != null)
                {
                    var overlay = UIManager.Instance.ShowTarotPickUp(card);
                    Log.LogInfo($"[TarotCardReceiver] Successfully triggered Tarot card banner: {cardType} (Overlay: {overlay != null})");
                }
                else
                {
                    Log.LogWarning($"[TarotCardReceiver] UIManager.Instance is null! Cannot show banner for {cardType}.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[TarotCardReceiver] Exception in ShowCardBanner: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void ShowNotification(string message)
        {
            try
            {
                if (NotificationCentre.Instance != null)
                {
                    NotificationCentre.Instance.PlayGenericNotification(message, NotificationBase.Flair.Positive);
                }
            }
            catch { }
        }

        public static void CycleCard(int delta)
        {
            var values = (TarotCards.Card[])Enum.GetValues(typeof(TarotCards.Card));
            int currentIndex = Array.IndexOf(values, CurrentTestCard);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = (currentIndex + delta + values.Length) % values.Length;
            CurrentTestCard = values[nextIndex];

            Log.LogInfo($"[TarotCardReceiver] Selected card: {CurrentTestCard} (Index: {nextIndex + 1}/{values.Length})");
            ShowNotification($"Selected: {CurrentTestCard}");
        }



        public static bool TryGetQueuedClauneckCard(out TarotCards.Card card)
        {
            return ClauneckQueue.TryDequeue(out card);
        }
    }

    // Hook Cult of the Lamb's simulation loop
    [HarmonyPatch(typeof(SimulationManager), "Update")]
    public static class SimulationManager_TarotReceiver_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            TarotCardReceiverPlugin.TickInGame();
        }
    }

    // Intercept Clauneck's altar offering choices
    [HarmonyPatch(typeof(Interaction_Tarot), "GetCard")]
    public static class ClauneckTarotChoicePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(bool canBeCorrupted, ref TarotCards.TarotCard __result)
        {
            if (!TarotCardReceiverPlugin.OverrideClauneck)
            {
                return true;
            }

            if (TarotCardReceiverPlugin.TryGetQueuedClauneckCard(out var queuedCard))
            {
                TarotCardReceiverPlugin.Log?.LogInfo($"[ClauneckPatch] Overriding Clauneck choice with queued card: {queuedCard}");
                __result = new TarotCards.TarotCard(queuedCard, 0);
                return false;
            }

            return true;
        }
    }
}
