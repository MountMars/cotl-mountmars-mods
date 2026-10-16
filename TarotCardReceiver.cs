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
    public enum PlayerForm
    {
        Default,
        Lamb,
        Goat,
        Owl,
        Snake
    }

    [BepInPlugin("com.mountmars.cotl.tarotcardreceiver", "Tarot Card Receiver", "1.0.0")]
    public class TarotCardReceiverPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static TarotCards.Card CurrentTestCard = TarotCards.Card.TheDeal;
        public static PlayerForm CurrentPlayerForm = PlayerForm.Default;

        // Thread-safe queue for outside-Clauneck banner preview
        public static readonly ConcurrentQueue<TarotCards.Card> PendingCardsToShow = new ConcurrentQueue<TarotCards.Card>();
        public static readonly ConcurrentQueue<TarotCards.Card> ClauneckQueue = new ConcurrentQueue<TarotCards.Card>();

        public static bool OverrideClauneck = true;
        public static bool OverrideShrine = true;

        // Clauneck & Shrine scan-and-present ritual state
        public static bool IsClauneckWaitingForCard = false;
        public static bool IsShrineWaitingForCard = false;
        public static bool IsWaitingForCard => IsClauneckWaitingForCard || IsShrineWaitingForCard;
        public static bool IsCardPresented = false;
        public static bool IsReplacingOverlay = false;
        public static Interaction_Tarot CurrentActiveClauneck = null;
        public static Shrines CurrentActiveShrine = null;
        public static PlayerFarming CurrentShrinePlayer = null;
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
                if (File.Exists(commandFilePath)) File.Delete(commandFilePath);
                if (File.Exists(queueFilePath)) File.Delete(queueFilePath);
            }
            catch
            {
                commandFilePath = "tarot_command.txt";
                queueFilePath = "tarot_queue.txt";
                try
                {
                    if (File.Exists(commandFilePath)) File.Delete(commandFilePath);
                    if (File.Exists(queueFilePath)) File.Delete(queueFilePath);
                }
                catch { }
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

        public static bool TryParseForm(string raw, out PlayerForm form)
        {
            form = PlayerForm.Default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string cleaned = raw.Trim().Trim('\0', '\r', '\n', ' ', '\t', '"', '\'');

            // 1. JSON payload support: {"form": "owl"}
            if (cleaned.StartsWith("{") && cleaned.EndsWith("}"))
            {
                var match = Regex.Match(cleaned, "\"form\"\\s*:\\s*\"?([^\",}]+)\"?", RegexOptions.IgnoreCase);
                if (!match.Success)
                    match = Regex.Match(cleaned, "\"character\"\\s*:\\s*\"?([^\",}]+)\"?", RegexOptions.IgnoreCase);
                if (!match.Success)
                    match = Regex.Match(cleaned, "\"skin\"\\s*:\\s*\"?([^\",}]+)\"?", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    cleaned = match.Groups[1].Value.Trim().Trim('\0', '\r', '\n', ' ', '\t', '"', '\'');
                }
            }

            // 2. Strip prefix: "form:", "form=", "form ", "form-", "skin:", "character:"
            if (cleaned.StartsWith("form:", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("form=", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("form ", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("form-", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("form/", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("skin:", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("character:", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(10).Trim();

            cleaned = cleaned.Trim().ToLowerInvariant();

            switch (cleaned)
            {
                case "owl":
                    form = PlayerForm.Owl;
                    return true;
                case "snake":
                    form = PlayerForm.Snake;
                    return true;
                case "goat":
                    form = PlayerForm.Goat;
                    return true;
                case "lamb":
                    form = PlayerForm.Lamb;
                    return true;
                case "default":
                case "reset":
                    form = PlayerForm.Default;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryProcessFormCommand(string candidate, string source)
        {
            if (TryParseForm(candidate, out var form))
            {
                Log.LogInfo($"[TarotCardReceiver] Form command received via {source}: {form}");
                SetPlayerForm(form);
                return true;
            }
            return false;
        }

        public static void SetPlayerForm(PlayerForm form)
        {
            CurrentPlayerForm = form;
            Log.LogInfo($"[TarotCardReceiver] Setting player form to: {form}");

            var players = PlayerFarming.players;
            if (players != null && players.Count > 0)
            {
                foreach (var player in players)
                {
                    if (player != null)
                    {
                        ApplyFormToPlayer(player, form);
                    }
                }
            }
            else if (PlayerFarming.Instance != null)
            {
                ApplyFormToPlayer(PlayerFarming.Instance, form);
            }

            ShowNotification($"Player Form: {form}");
        }

        public static void ApplyFormToPlayer(PlayerFarming player, PlayerForm form)
        {
            if (player == null) return;

            if (form == PlayerForm.Goat)
            {
                player.IsGoat = true;
                player.isLamb = false;
            }
            else if (form == PlayerForm.Lamb)
            {
                player.IsGoat = false;
                player.isLamb = true;
            }
            else if (form == PlayerForm.Owl || form == PlayerForm.Snake)
            {
                player.IsGoat = false;
                player.isLamb = true;
            }
            else // Default
            {
                player.IsGoat = (player.playerID != 0) || (DataManager.Instance != null && DataManager.Instance.PlayerVisualFleece == 1003);
                player.isLamb = (player.playerID == 0);
            }

            player.SetSkin();
        }

        public static void CyclePlayerForm()
        {
            PlayerForm nextForm;
            switch (CurrentPlayerForm)
            {
                case PlayerForm.Default:
                case PlayerForm.Lamb:
                    nextForm = PlayerForm.Goat;
                    break;
                case PlayerForm.Goat:
                    nextForm = PlayerForm.Owl;
                    break;
                case PlayerForm.Owl:
                    nextForm = PlayerForm.Snake;
                    break;
                case PlayerForm.Snake:
                    nextForm = PlayerForm.Lamb;
                    break;
                default:
                    nextForm = PlayerForm.Lamb;
                    break;
            }
            SetPlayerForm(nextForm);
        }

        public static void ProcessIncomingCard(string candidate, string source)
        {
            if (TryProcessFormCommand(candidate, source))
            {
                return;
            }

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

                if (IsWaitingForCard)
                {
                    string ritualType = IsShrineWaitingForCard ? "Offering Shrine" : "Clauneck";
                    Log.LogInfo($"[TarotCardReceiver] Card received during {ritualType} ritual via {source}: {card}");
                    PresentRitualCard(card);
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

                // Discard any stale command files from before save load
                try { if (!string.IsNullOrEmpty(commandFilePath) && File.Exists(commandFilePath)) File.Delete(commandFilePath); } catch { }
                try { if (!string.IsNullOrEmpty(queueFilePath) && File.Exists(queueFilePath)) File.Delete(queueFilePath); } catch { }
                while (PendingCardsToShow.TryDequeue(out _)) { }
                while (ClauneckQueue.TryDequeue(out _)) { }
                return;
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

            // 1. Process pending preview cards only when NOT in an active ritual
            if (!IsWaitingForCard)
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

            if (Input.GetKeyDown(KeyCode.F9))
            {
                Log?.LogInfo("[TarotCardReceiver] F9 pressed: testing Neptune's Curse fish storm!");
                NeptunesCurseFishStorm.Trigger();
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                Log?.LogInfo("[TarotCardReceiver] F10 pressed: cycling player form!");
                CyclePlayerForm();
            }
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

        // --- Offering Shrine Scan-and-Present Ritual ---

        public static IEnumerator ShrineCustomTarotRoutine(Shrines shrine, PlayerFarming playerFarming)
        {
            IsShrineWaitingForCard = true;
            IsCardPresented = false;
            IsReplacingOverlay = false;
            CurrentActiveShrine = shrine;
            CurrentShrinePlayer = playerFarming;
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

            PlayerFarming player = playerFarming != null ? playerFarming : (shrine.playerFarming != null ? shrine.playerFarming : PlayerFarming.Instance);
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
                cam.DisablePlayerLook = true;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOneShot("event:/tarot/tarot_card_pull", shrine.gameObject);
            }

            yield return new WaitForSeconds(0.1f);
            GameManager.GetInstance().CameraSetTargetZoom(6f);

            ShowNotification("Offering accepted. Scan a Tarot card (Esc to cancel)");
            Log.LogInfo("[TarotCardReceiver] Shrine ritual started. Waiting for card scan via IPC...");

            while (IsShrineWaitingForCard)
            {
                if (!IsCardPresented && InputManager.UI != null && InputManager.UI.GetCancelButtonDown(null))
                {
                    Log.LogInfo("[TarotCardReceiver] Player cancelled waiting for Shrine Tarot card scan.");
                    AbortShrineRitual(shrine, player);
                    yield break;
                }

                if (shrine == null || !shrine.gameObject.activeInHierarchy)
                {
                    ResetRitualState();
                    yield break;
                }

                yield return null;
            }
        }

        public static void AbortShrineRitual(Shrines shrine, PlayerFarming player)
        {
            ResetRitualState();

            try
            {
                Time.timeScale = 1f;
                LetterBox.Hide(true);
                if (HUD_Manager.Instance != null)
                {
                    HUD_Manager.Instance.Show(0, false);
                }
                if (AudioManager.Instance != null && shrine != null)
                {
                    AudioManager.Instance.PlayOneShot("event:/tarot/tarot_card_close", shrine.gameObject);
                }
                GameManager.GetInstance()?.CameraResetTargetZoom();
                if (CameraFollowTarget.Instance != null)
                {
                    CameraFollowTarget.Instance.DisablePlayerLook = false;
                }
                GameManager.GetInstance()?.UnFreezeAllies();

                if (player != null)
                {
                    if (player.simpleSpineAnimator != null)
                    {
                        player.simpleSpineAnimator.Animate("cards/cards-stop-seperate", 0, false);
                        player.simpleSpineAnimator.AddAnimate("idle", 0, true, 0f);
                    }
                    if (player.state != null)
                    {
                        player.state.CURRENT_STATE = StateMachine.State.Idle;
                    }
                    player.SpineUseDeltaTime(true);
                }
                SimpleSetCamera.EnableAll();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[TarotCardReceiver] Error during AbortShrineRitual: {ex.Message}");
            }
        }

        public static void PresentClauneckCard(TarotCards.Card cardType)
        {
            PresentRitualCard(cardType);
        }

        public static void PresentRitualCard(TarotCards.Card cardType)
        {
            try
            {
                CloseClauneckBark();
                Log.LogInfo($"[TarotCardReceiver] Presenting Tarot card: {cardType}");

                if (cardType == TarotCards.Card.NeptunesCurse)
                {
                    NeptunesCurseFishStorm.Trigger();
                }

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
                                OnRitualCardDismissed();
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
                    Log.LogWarning("[TarotCardReceiver] UIManager.Instance is null when presenting ritual card!");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[TarotCardReceiver] Exception in PresentRitualCard: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void OnClauneckCardDismissed()
        {
            OnRitualCardDismissed();
        }

        public static void OnRitualCardDismissed()
        {
            try
            {
                CloseClauneckBark();
                Log.LogInfo($"[TarotCardReceiver] Player dismissed card overlay. Chosen card: {LastPresentedCard?.CardType}");

                var chosenCard = LastPresentedCard;
                var clauneck = CurrentActiveClauneck;
                var shrine = CurrentActiveShrine;
                var shrinePlayer = CurrentShrinePlayer;

                if (chosenCard != null && chosenCard.CardType == TarotCards.Card.NeptunesCurse)
                {
                    NeptunesCurseFishStorm.Trigger();
                }

                ResetRitualState();

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
                else if (shrine != null)
                {
                    Log.LogInfo($"[TarotCardReceiver] Completing Shrine tarot ritual with card: {chosenCard?.CardType}");

                    if (shrine.playerFarming == null)
                    {
                        shrine.playerFarming = shrinePlayer ?? PlayerFarming.Instance;
                    }

                    var backMethod = AccessTools.Method(typeof(Shrines), "BackToIdleRoutine", new Type[] { typeof(TarotCards.TarotCard), typeof(float) });
                    if (backMethod != null)
                    {
                        var routine = (IEnumerator)backMethod.Invoke(shrine, new object[] { chosenCard, 0f });
                        shrine.StartCoroutine(routine);
                    }
                    else
                    {
                        Log.LogError("[TarotCardReceiver] Could not find BackToIdleRoutine on Shrines!");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[TarotCardReceiver] Exception in OnRitualCardDismissed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void ResetClauneckState()
        {
            ResetRitualState();
        }

        public static void ResetRitualState()
        {
            CloseClauneckBark();
            IsClauneckWaitingForCard = false;
            IsShrineWaitingForCard = false;
            IsCardPresented = false;
            IsReplacingOverlay = false;
            CurrentTarotOverlay = null;
            CurrentActiveClauneck = null;
            CurrentActiveShrine = null;
            CurrentShrinePlayer = null;
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

    // Intercept Offering Shrine's tarot card draw
    [HarmonyPatch(typeof(Shrines), "DrawCardRoutine")]
    public static class Shrines_DrawCardRoutine_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Shrines __instance, PlayerFarming playerFarming, ref IEnumerator __result)
        {
            if (!TarotCardReceiverPlugin.OverrideShrine || __instance.TypeOfShrine != Shrines.ShrineType.TAROT_CARD)
            {
                return true;
            }

            __result = TarotCardReceiverPlugin.ShrineCustomTarotRoutine(__instance, playerFarming);
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

    // Override PlayerFarming skin to support Owl, Snake, Goat, and Lamb dynamically
    [HarmonyPatch(typeof(PlayerFarming), "SetSkin", new Type[] { typeof(bool) })]
    public static class PlayerFarming_SetSkin_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerFarming __instance, bool BlackAndWhite, ref Spine.Skin __result)
        {
            if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Default)
            {
                return true;
            }

            try
            {
                if (__instance.playerID != 0)
                {
                    __instance.IsGoat = true;
                }
                else
                {
                    if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Goat)
                        __instance.IsGoat = true;
                    else if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Lamb)
                        __instance.IsGoat = (DataManager.Instance != null && DataManager.Instance.PlayerVisualFleece == 1003);
                    else
                        __instance.IsGoat = false;
                }

                Spine.Skin playerSkin = new Spine.Skin("Player Skin");
                Traverse.Create(__instance).Field("PlayerSkin").SetValue(playerSkin);

                Spine.Skin characterSkin = null;
                if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Owl)
                {
                    characterSkin = __instance.Spine.Skeleton.Data.FindSkin("Owl");
                    if (characterSkin != null)
                    {
                        playerSkin.AddSkin(characterSkin);

                        // Fix missing directional frames in Owl skin left pointing to Lamb by game developers:
                        int headBackSlot = __instance.Spine.Skeleton.Data.FindSlotIndex("images/HeadBack");
                        if (headBackSlot >= 0)
                        {
                            var owlHeadBack = characterSkin.GetAttachment(headBackSlot, "HeadBack");
                            if (owlHeadBack != null)
                            {
                                playerSkin.SetAttachment(headBackSlot, "HeadBackDown", owlHeadBack);
                                playerSkin.SetAttachment(headBackSlot, "HeadBackDown_RITUAL", owlHeadBack);
                                playerSkin.SetAttachment(headBackSlot, "HeadBackDown_SERMON", owlHeadBack);
                                playerSkin.SetAttachment(headBackSlot, "HeadBack_Static1", owlHeadBack);
                                playerSkin.SetAttachment(headBackSlot, "HeadBack_Static2", owlHeadBack);
                            }
                        }

                        int eyeLeftSlot = __instance.Spine.Skeleton.Data.FindSlotIndex("EYE_LEFT");
                        if (eyeLeftSlot >= 0)
                        {
                            var owlEye = characterSkin.GetAttachment(eyeLeftSlot, "EYE_ANGRY_LEFT")
                                         ?? characterSkin.GetAttachment(eyeLeftSlot, "EYE");
                            if (owlEye != null)
                            {
                                playerSkin.SetAttachment(eyeLeftSlot, "EYE_DETERMINED_DOWN_LEFT", owlEye);
                                playerSkin.SetAttachment(eyeLeftSlot, "EYE_DOWN", owlEye);
                                playerSkin.SetAttachment(eyeLeftSlot, "EYE_UP", owlEye);
                            }
                        }

                        int eyeRightSlot = __instance.Spine.Skeleton.Data.FindSlotIndex("EYE_RIGHT");
                        if (eyeRightSlot >= 0)
                        {
                            var owlEye = characterSkin.GetAttachment(eyeRightSlot, "EYE_ANGRY_RIGHT")
                                         ?? characterSkin.GetAttachment(eyeRightSlot, "EYE");
                            if (owlEye != null)
                            {
                                playerSkin.SetAttachment(eyeRightSlot, "EYE_DETERMINED_DOWN_RIGHT", owlEye);
                                playerSkin.SetAttachment(eyeRightSlot, "EYE_DOWN", owlEye);
                                playerSkin.SetAttachment(eyeRightSlot, "EYE_UP", owlEye);
                            }
                        }
                    }
                }
                else if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Snake)
                {
                    characterSkin = __instance.Spine.Skeleton.Data.FindSkin("Snake");
                    if (characterSkin != null)
                    {
                        playerSkin.AddSkin(characterSkin);

                        // Fix downward eyes in Snake
                        int eyeLeftSlot = __instance.Spine.Skeleton.Data.FindSlotIndex("EYE_LEFT");
                        if (eyeLeftSlot >= 0)
                        {
                            var snakeEye = characterSkin.GetAttachment(eyeLeftSlot, "EYE_ANGRY_LEFT")
                                           ?? characterSkin.GetAttachment(eyeLeftSlot, "EYE");
                            if (snakeEye != null)
                            {
                                playerSkin.SetAttachment(eyeLeftSlot, "EYE_DETERMINED_DOWN_LEFT", snakeEye);
                                playerSkin.SetAttachment(eyeLeftSlot, "EYE_DOWN", snakeEye);
                                playerSkin.SetAttachment(eyeLeftSlot, "EYE_UP", snakeEye);
                            }
                        }

                        int eyeRightSlot = __instance.Spine.Skeleton.Data.FindSlotIndex("EYE_RIGHT");
                        if (eyeRightSlot >= 0)
                        {
                            var snakeEye = characterSkin.GetAttachment(eyeRightSlot, "EYE_ANGRY_RIGHT")
                                           ?? characterSkin.GetAttachment(eyeRightSlot, "EYE");
                            if (snakeEye != null)
                            {
                                playerSkin.SetAttachment(eyeRightSlot, "EYE_DETERMINED_DOWN_RIGHT", snakeEye);
                                playerSkin.SetAttachment(eyeRightSlot, "EYE_DOWN", snakeEye);
                                playerSkin.SetAttachment(eyeRightSlot, "EYE_UP", snakeEye);
                            }
                        }
                    }
                }
                else if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Goat || __instance.IsGoat)
                {
                    characterSkin = __instance.Spine.Skeleton.Data.FindSkin("Goat");
                    if (characterSkin != null) playerSkin.AddSkin(characterSkin);
                }
                else // Lamb
                {
                    int visualFleece = (DataManager.Instance != null) ? DataManager.Instance.PlayerVisualFleece : 0;
                    string suffix = visualFleece.ToString() + (BlackAndWhite ? "_BW" : "");
                    characterSkin = __instance.Spine.Skeleton.Data.FindSkin("Lamb_" + suffix)
                                    ?? __instance.Spine.Skeleton.Data.FindSkin("Lamb");
                    if (characterSkin != null) playerSkin.AddSkin(characterSkin);
                }

                // Weapon Skin
                string weaponName = "Normal";
                if (__instance.currentWeapon != EquipmentType.None)
                {
                    var weaponData = EquipmentManager.GetWeaponData(__instance.currentWeapon);
                    if (weaponData != null)
                    {
                        weaponName = weaponData.ToString();
                    }
                }
                var weaponSkin = __instance.Spine.Skeleton.Data.FindSkin("Weapons/" + weaponName);
                if (weaponSkin != null)
                {
                    playerSkin.AddSkin(weaponSkin);
                }

                // Hurt Skin in Dungeon
                if (LocationManager.IsDungeonActive() && __instance.health != null)
                {
                    var healthPlayer = __instance.health as HealthPlayer;
                    float totalHealth = healthPlayer != null ? healthPlayer.PLAYER_TOTAL_HEALTH : 6f;
                    float currentHealth = __instance.health.CurrentHP;
                    if (currentHealth <= totalHealth * 0.25f)
                    {
                        string hurtSkinName = __instance.IsGoat ? "Goat_Hurt2" : "Hurt2";
                        var hurtSkin = __instance.Spine.Skeleton.Data.FindSkin(hurtSkinName);
                        if (hurtSkin != null) playerSkin.AddSkin(hurtSkin);
                    }
                    else if (currentHealth <= totalHealth * 0.5f)
                    {
                        string hurtSkinName = __instance.IsGoat ? "Goat_Hurt1" : "Hurt1";
                        var hurtSkin = __instance.Spine.Skeleton.Data.FindSkin(hurtSkinName);
                        if (hurtSkin != null) playerSkin.AddSkin(hurtSkin);
                    }
                }

                // Mop Skin
                int choreLevel = (DataManager.Instance != null)
                    ? (__instance.isLamb ? DataManager.Instance.ChoreXPLevel : DataManager.Instance.ChoreXPLevel_Coop) + 1
                    : 1;
                int clampedLevel = Mathf.Clamp(choreLevel, 1, 10);
                var mopSkin = __instance.Spine.Skeleton.Data.FindSkin("Mops/" + clampedLevel);
                if (mopSkin != null)
                {
                    playerSkin.AddSkin(mopSkin);
                }

                // Apply to skeleton
                __instance.Spine.Skeleton.SetSkin(playerSkin);
                __instance.Spine.Skeleton.SetSlotsToSetupPose();

                // Clear Lamb ear attachments when using Owl or Snake form
                if (TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Owl ||
                    TarotCardReceiverPlugin.CurrentPlayerForm == PlayerForm.Snake)
                {
                    var earLeft = __instance.Spine.Skeleton.FindSlot("EAR_LEFT");
                    if (earLeft != null) earLeft.Attachment = null;
                    var earRight = __instance.Spine.Skeleton.FindSlot("EAR_RIGHT");
                    if (earRight != null) earRight.Attachment = null;
                }

                __result = playerSkin;
                return false;
            }
            catch (Exception ex)
            {
                TarotCardReceiverPlugin.Log?.LogError($"[TarotCardReceiver] Error in PlayerFarming_SetSkin_Patch: {ex.Message}\n{ex.StackTrace}");
                return true;
            }
        }
    }
}
