using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Lamb.UI;
using MMTools;
using UnityEngine;

namespace COTLTarotCardReceiver
{
    [BepInPlugin("com.mountmars.cotl.tarotcardreceiver", "Tarot Card Receiver", "1.0.0")]
    public class TarotCardReceiverPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static TarotCards.Card CurrentTestCard = TarotCards.Card.TheDeal;

        // Thread-safe queue for outside-Clauneck banner preview
        public static readonly ConcurrentQueue<TarotCards.Card> PendingCardsToShow = new ConcurrentQueue<TarotCards.Card>();
        public static readonly ConcurrentQueue<TarotCards.Card> ClauneckQueue = new ConcurrentQueue<TarotCards.Card>();

        public static bool OverrideClauneck = true;

        // Clauneck scan-and-present ritual state
        public static bool IsClauneckWaitingForCard = false;
        public static bool IsCardPresented = false;
        public static bool IsReplacingOverlay = false;
        public static Interaction_Tarot CurrentActiveClauneck = null;
        public static TarotCards.TarotCard LastPresentedCard = null;
        public static UITarotPickUpOverlayController CurrentTarotOverlay = null;

        private static bool hasLoggedReady = false;

        // Deduplication timer to avoid double-triggers from simultaneous UDP + HTTP / fast repeat scans
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
                // Deduplicate if exact same card arrived within 1.0s (e.g. from simultaneous UDP + HTTP / fast repeat)
                if (card == lastEnqueuedCard && (Time.unscaledTime - lastCardTime) < 1.0f)
                {
                    Log.LogInfo($"[TarotCardReceiver] Duplicate '{card}' from {source} ignored (within 1s).");
                    return;
                }

                lastCardTime = Time.unscaledTime;
                lastEnqueuedCard = card;
                CurrentTestCard = card;

                if (IsClauneckWaitingForCard)
                {
                    Log.LogInfo($"[TarotCardReceiver] Card received during Clauneck ritual via {source}: {card}");
                    PresentClauneckCard(card);
                    return;
                }

                PendingCardsToShow.Enqueue(card);
                Log.LogInfo($"[TarotCardReceiver] Successfully queued preview card via {source}: {card}");
            }
            else
            {
                Log.LogWarning($"[TarotCardReceiver] Unknown card payload from {source}: '{candidate}'");
            }
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

            // 1. Process pending preview cards only when Clauneck is NOT waiting
            if (!IsClauneckWaitingForCard)
            {
                while (PendingCardsToShow.TryDequeue(out var incomingCard))
                {
                    CurrentTestCard = incomingCard;
                    Log.LogInfo($"[TarotCardReceiver] Processing incoming card on main thread: {incomingCard}");
                    ShowCardBanner(incomingCard);
                }
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
                    NotificationCentre.Instance.Show(true);
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

        // --- Clauneck Scan-and-Present Ritual ---

        public static IEnumerator ClauneckCustomTarotRoutine(Interaction_Tarot interactionTarot)
        {
            IsClauneckWaitingForCard = true;
            IsCardPresented = false;
            IsReplacingOverlay = false;
            CurrentActiveClauneck = interactionTarot;
            LastPresentedCard = null;
            CurrentTarotOverlay = null;

            while (ClauneckQueue.TryDequeue(out _)) { }
            while (PendingCardsToShow.TryDequeue(out _)) { }

            if (HUD_Manager.Instance != null)
            {
                HUD_Manager.Instance.Hide(false, 0, false);
            }
            GameManager.GetInstance().CameraSetTargetZoom(4f);
            LetterBox.Show(false);

            PlayerFarming player = interactionTarot.playerFarming != null ? interactionTarot.playerFarming : PlayerFarming.Instance;
            if (player != null)
            {
                player.state.CURRENT_STATE = StateMachine.State.CustomAnimation;
                player.state.facingAngle = -90f;
                if (player.simpleSpineAnimator != null)
                {
                    player.simpleSpineAnimator.Animate("cards/cards-start", 0, false);
                    player.simpleSpineAnimator.AddAnimate("cards/cards-loop", 0, true, 0f);
                }
            }

            CameraFollowTarget cam = CameraFollowTarget.Instance;
            if (cam != null)
            {
                Traverse.Create(interactionTarot).Field("c").SetValue(cam);
                cam.DisablePlayerLook = true;
            }

            var centreMethod = AccessTools.Method(typeof(Interaction_Tarot), "CentrePlayer");
            if (centreMethod != null)
            {
                interactionTarot.StartCoroutine((IEnumerator)centreMethod.Invoke(interactionTarot, null));
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOneShot("event:/tarot/tarot_card_pull", interactionTarot.gameObject);
            }

            yield return new WaitForSeconds(0.1f);
            GameManager.GetInstance().CameraSetTargetZoom(6f);

            var tarotcardsGo = interactionTarot.tarotcards;
            if (tarotcardsGo != null)
            {
                tarotcardsGo.SetActive(false);
            }

            ShowClauneckBark(interactionTarot, "Draw thy card, Lamb, and reveal thy destiny...");
            ShowNotification("Scan a Tarot card (Esc to cancel)");
            Log.LogInfo("[TarotCardReceiver] Clauneck ritual started. Displayed bark and scan notification.");

            while (IsClauneckWaitingForCard)
            {
                // If player cancelled before any card was presented, gracefully abort
                if (!IsCardPresented && InputManager.UI != null && InputManager.UI.GetCancelButtonDown(null))
                {
                    Log.LogInfo("[TarotCardReceiver] Player cancelled waiting for Tarot card scan.");
                    AbortClauneckRitual(interactionTarot);
                    yield break;
                }

                if (interactionTarot == null || !interactionTarot.gameObject.activeInHierarchy)
                {
                    ResetClauneckState();
                    yield break;
                }

                yield return null;
            }
        }

        public static void AbortClauneckRitual(Interaction_Tarot interactionTarot)
        {
            CloseClauneckBark();
            ResetClauneckState();

            if (interactionTarot != null)
            {
                interactionTarot.Activated = false;
                if (interactionTarot.simpleBarkBeforeCard != null)
                {
                    interactionTarot.simpleBarkBeforeCard.enabled = true;
                }

                var backMethod = AccessTools.Method(typeof(Interaction_Tarot), "BackToIdleRoutine", new Type[] { typeof(TarotCards.TarotCard), typeof(float) });
                if (backMethod != null)
                {
                    var routine = (IEnumerator)backMethod.Invoke(interactionTarot, new object[] { null, 0f });
                    interactionTarot.StartCoroutine(routine);
                }
            }
        }

        public static void PresentClauneckCard(TarotCards.Card cardType)
        {
            try
            {
                CloseClauneckBark();
                Log.LogInfo($"[TarotCardReceiver] Presenting Clauneck Tarot card: {cardType}");
                TarotCards.TarotCard card = new TarotCards.TarotCard(cardType, 0);
                LastPresentedCard = card;
                IsCardPresented = true;

                if (CurrentTarotOverlay != null)
                {
                    IsReplacingOverlay = true;
                    try
                    {
                        CurrentTarotOverlay.Hide(true);
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[TarotCardReceiver] Error hiding previous overlay: {ex.Message}");
                    }
                    IsReplacingOverlay = false;
                    CurrentTarotOverlay = null;
                }

                if (UIManager.Instance != null)
                {
                    CurrentTarotOverlay = UIManager.Instance.ShowTarotPickUp(card);
                    if (CurrentTarotOverlay != null)
                    {
                        CurrentTarotOverlay.OnHidden += () =>
                        {
                            if (!IsReplacingOverlay)
                            {
                                OnClauneckCardDismissed();
                            }
                        };
                    }
                    else
                    {
                        Log.LogWarning("[TarotCardReceiver] UIManager.ShowTarotPickUp returned null!");
                    }
                }
                else
                {
                    Log.LogWarning("[TarotCardReceiver] UIManager.Instance is null when presenting Clauneck card!");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[TarotCardReceiver] Exception in PresentClauneckCard: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void OnClauneckCardDismissed()
        {
            try
            {
                CloseClauneckBark();
                Log.LogInfo($"[TarotCardReceiver] Player dismissed card overlay. Chosen card: {LastPresentedCard?.CardType}");

                var chosenCard = LastPresentedCard;
                var clauneck = CurrentActiveClauneck;

                ResetClauneckState();

                if (clauneck != null)
                {
                    var giveRoutine = Traverse.Create(clauneck).Field("giveTarotRoutine").GetValue<Coroutine>();
                    if (giveRoutine != null)
                    {
                        try
                        {
                            clauneck.StopCoroutine(giveRoutine);
                        }
                        catch { }
                        Traverse.Create(clauneck).Field("giveTarotRoutine").SetValue(null);
                    }

                    var backMethod = AccessTools.Method(typeof(Interaction_Tarot), "BackToIdleRoutine", new Type[] { typeof(TarotCards.TarotCard), typeof(float) });
                    if (backMethod != null)
                    {
                        var routine = (IEnumerator)backMethod.Invoke(clauneck, new object[] { chosenCard, 0f });
                        clauneck.StartCoroutine(routine);
                    }
                    else
                    {
                        Log.LogError("[TarotCardReceiver] Could not find BackToIdleRoutine on Interaction_Tarot!");
                    }

                    if (DataManager.Instance != null && !DataManager.Instance.HasEncounteredTarot)
                    {
                        if (clauneck.introGiveDeck != null)
                        {
                            clauneck.introGiveDeck.enabled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[TarotCardReceiver] Exception in OnClauneckCardDismissed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void ResetClauneckState()
        {
            CloseClauneckBark();
            IsClauneckWaitingForCard = false;
            IsCardPresented = false;
            IsReplacingOverlay = false;
            CurrentTarotOverlay = null;
            CurrentActiveClauneck = null;
            while (ClauneckQueue.TryDequeue(out _)) { }
            while (PendingCardsToShow.TryDequeue(out _)) { }
        }

        public static void ShowClauneckBark(Interaction_Tarot interactionTarot, string text)
        {
            try
            {
                GameObject speaker = interactionTarot.gameObject;
                string sound = "event:/dialogue/shop_tarot_clauneck/standard_clauneck";
                Vector3 offset = new Vector3(0f, 2.5f, 0f);

                if (interactionTarot.simpleBarkBeforeCard != null && 
                    interactionTarot.simpleBarkBeforeCard.Entries != null && 
                    interactionTarot.simpleBarkBeforeCard.Entries.Count > 0)
                {
                    var template = interactionTarot.simpleBarkBeforeCard.Entries[0];
                    if (template != null)
                    {
                        if (template.Speaker != null) speaker = template.Speaker;
                        if (!string.IsNullOrEmpty(template.soundPath)) sound = template.soundPath;
                        if (template.Offset != Vector3.zero) offset = template.Offset;
                    }
                }

                var entry = new ConversationEntry(speaker, null, null, sound, null, "Clauneck");
                entry.DisplayText = text;
                entry.Offset = offset;

                var entries = new List<ConversationEntry> { entry };
                var convObj = new ConversationObject(entries, null, null, null);
                MMConversation.PlayBark(convObj, false);
                Log.LogInfo($"[TarotCardReceiver] Successfully triggered Clauneck bark: '{text}' on {speaker.name} (offset: {offset})");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[TarotCardReceiver] Failed to show Clauneck bark: {ex.Message}");
            }
        }

        public static void CloseClauneckBark()
        {
            try
            {
                if (MMConversation.mmConversation != null && MMConversation.isBark)
                {
                    MMConversation.mmConversation.Close(true, null, false);
                }
            }
            catch { }
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

    // Intercept Clauneck's altar ritual
    [HarmonyPatch(typeof(Interaction_Tarot), "DoRoutine")]
    public static class Interaction_Tarot_DoRoutine_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction_Tarot __instance)
        {
            if (!TarotCardReceiverPlugin.OverrideClauneck)
            {
                return true;
            }

            var customRoutine = __instance.StartCoroutine(TarotCardReceiverPlugin.ClauneckCustomTarotRoutine(__instance));
            Traverse.Create(__instance).Field("giveTarotRoutine").SetValue(customRoutine);
            return false;
        }
    }

    // Allow custom DisplayText in MMConversation barks without requiring localization keys
    [HarmonyPatch(typeof(MMConversation), "UpdateText")]
    public static class MMConversation_UpdateText_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(MMConversation __instance)
        {
            try
            {
                if (MMConversation.CURRENT_CONVERSATION != null &&
                    MMConversation.CURRENT_CONVERSATION.Entries != null &&
                    MMConversation.Position >= 0 &&
                    MMConversation.Position < MMConversation.CURRENT_CONVERSATION.Entries.Count)
                {
                    var entry = MMConversation.CURRENT_CONVERSATION.Entries[MMConversation.Position];
                    if (!string.IsNullOrEmpty(entry.DisplayText))
                    {
                        if (__instance.TitleText != null)
                        {
                            __instance.TitleText.text = !string.IsNullOrEmpty(entry.CharacterName) ? entry.CharacterName : "Clauneck";
                        }
                        if (__instance.TextPlayer != null)
                        {
                            __instance.TextPlayer.ShowText(entry.DisplayText);
                        }
                        TarotCardReceiverPlugin.Log?.LogInfo($"[TarotCardReceiver] MMConversation.UpdateText rendered DisplayText: '{entry.DisplayText}'");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                TarotCardReceiverPlugin.Log?.LogWarning($"[TarotCardReceiver] Error in MMConversation.UpdateText patch: {ex.Message}");
            }
            return true;
        }
    }
}
