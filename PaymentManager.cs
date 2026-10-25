using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using Lamb.UI;
using UnityEngine;

namespace COTLTarotCardReceiver
{
    public enum PaymentState
    {
        Idle,
        MethodSelection, // Screen 1: "How do you want to pay?" (Card, Cash, Cancel)
        CardSwipePrompt  // Screen 2: "Please swipe card..."
    }

    public class PaymentManager : MonoBehaviour
    {
        private static PaymentManager instance;
        public static PaymentManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<PaymentManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("PaymentManager");
                        DontDestroyOnLoad(go);
                        instance = go.AddComponent<PaymentManager>();
                    }
                }
                return instance;
            }
        }

        public PaymentState CurrentState = PaymentState.Idle;
        public bool IsActive => CurrentState != PaymentState.Idle;
        public bool IsWaitingForCard => CurrentState == PaymentState.CardSwipePrompt;
        public bool IsFulfillingPayment = false;

        private Interaction currentInteraction;
        private PlayerFarming currentPlayer;
        private string itemName = "Item";
        private int itemCost = 0;
        private InventoryItem.ITEM_TYPE costType = InventoryItem.ITEM_TYPE.BLACK_GOLD;

        private int selectedIndex = 0;
        private float lastNavTime = 0f;
        private string noticeMessage = "";
        private float noticeExpiry = 0f;

        // ==========================================
        // OPACITY & VISUAL SETTINGS
        // Adjust DialogOpacity: 0.0f = completely transparent, 1.0f = 100% solid/opaque
        // Adjust ButtonOpacity: 0.0f = completely transparent, 1.0f = 100% solid/opaque
        // ==========================================
        public static float DialogOpacity = 0.40f;
        public static Color DialogBackdropColor = new Color(0.06f, 0.04f, 0.06f);
        public static Color DialogBoxTint = new Color(0.18f, 0.12f, 0.18f, 0.40f);
        public static float ButtonOpacity = 0.40f;

        // ==========================================
        // COTL SIGNATURE COLOR CODING (#D22B2B & #E5A93C)
        // Highlights key action verbs: TAP, COMMUNE, CONFIRM
        // ==========================================
        public const string BloodRedHex = "#D22B2B";
        public const string GoldHex = "#E5A93C";

        public static string BloodRed(string text) => $"<color={BloodRedHex}><b>{text}</b></color>";
        public static string Gold(string text) => $"<color={GoldHex}><b>{text}</b></color>";

        // Style caching and frame guard
        private int lastRenderedFrame = -1;
        private EventType lastRenderedEventType = EventType.Ignore;
        private bool hasLoggedGuiActive = false;

        private static GUIStyle containerBoxStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle priceStyle;
        private static GUIStyle normalBtnStyle;
        private static GUIStyle selectedBtnStyle;
        private static GUIStyle noticeStyle;
        private static GUIStyle pulsingStatusStyle;
        private static bool stylesInitialized = false;

        public static void Initialize(MonoBehaviour host)
        {
            if (host == null) return;
            var existing = host.gameObject.GetComponent<PaymentManager>();
            if (existing == null)
            {
                instance = host.gameObject.AddComponent<PaymentManager>();
            }
            else
            {
                instance = existing;
            }
            TarotCardReceiverPlugin.Log?.LogInfo("[PaymentManager] Initialized as MonoBehaviour on host GameObject.");
        }

        public static bool IsEligiblePurchaseInteraction(Interaction interaction)
        {
            if (interaction == null || !interaction.Interactable) return false;

            if (interaction is Interaction_BuyItem buyItem)
            {
                return !buyItem.Activated;
            }
            if (interaction is Interaction_FollowerInSpiderWeb spider)
            {
                return !spider.Activated;
            }
            if (interaction is Interaction_FollowerInSpiderWebDailyShop spiderDaily)
            {
                try
                {
                    return !Traverse.Create(spiderDaily).Field<bool>("Activated").Value;
                }
                catch
                {
                    return true;
                }
            }
            if (interaction is Interaction_Tarot tarot)
            {
                return !tarot.Activated;
            }

            return false;
        }

        public void StartPayment(Interaction interaction, PlayerFarming player)
        {
            if (IsActive || interaction == null) return;

            currentInteraction = interaction;
            currentPlayer = player ?? PlayerFarming.Instance ?? (PlayerFarming.players != null && PlayerFarming.players.Count > 0 ? PlayerFarming.players[0] : null);

            ExtractItemDetails(interaction);

            CurrentState = PaymentState.CardSwipePrompt;
            selectedIndex = 0;
            noticeMessage = "";

            // Freeze player during payment dialog
            if (currentPlayer != null && currentPlayer.state != null)
            {
                currentPlayer.state.CURRENT_STATE = StateMachine.State.InActive;
            }

            TarotCardReceiverPlugin.Log?.LogInfo($"[PaymentManager] Payment opened: {itemName} | Cost: {itemCost} {costType}");
        }

        private void ExtractItemDetails(Interaction interaction)
        {
            itemCost = 0;
            costType = InventoryItem.ITEM_TYPE.BLACK_GOLD;
            itemName = "Item";

            if (interaction is Interaction_BuyItem buyItem)
            {
                itemCost = buyItem.GetCost();
                if (buyItem.itemForSale != null)
                {
                    costType = buyItem.itemForSale.costType;
                }
                itemName = CleanText(buyItem.Label);
                if (string.IsNullOrWhiteSpace(itemName) || itemName == "Item")
                {
                    itemName = buyItem.itemForSale != null ? buyItem.itemForSale.itemToBuy.ToString() : "Shop Item";
                }
            }
            else if (interaction is Interaction_FollowerInSpiderWeb spider)
            {
                itemCost = spider.Cost;
                costType = InventoryItem.ITEM_TYPE.BLACK_GOLD;
                itemName = CleanText(spider.Label);
                if (string.IsNullOrWhiteSpace(itemName)) itemName = "Follower in Spider Web";
            }
            else if (interaction is Interaction_FollowerInSpiderWebDailyShop spiderDaily)
            {
                itemCost = spiderDaily.Cost;
                costType = InventoryItem.ITEM_TYPE.BLACK_GOLD;
                itemName = CleanText(spiderDaily.Label);
                if (string.IsNullOrWhiteSpace(itemName)) itemName = "Follower in Spider Web";
            }
            else if (interaction is Interaction_Tarot tarot)
            {
                try
                {
                    itemCost = Traverse.Create(tarot).Field<int>("devotionCost").Value;
                }
                catch
                {
                    itemCost = 0;
                }
                costType = InventoryItem.ITEM_TYPE.SOUL;
                itemName = CleanText(tarot.Label);
                if (string.IsNullOrWhiteSpace(itemName)) itemName = "Clauneck's Tarot Card";
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = "Item";
            }
        }

        private string CleanText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string cleaned = Regex.Replace(raw, "<.*?>", string.Empty);
            return cleaned.Replace("\n", " ").Replace("\r", "").Trim();
        }

        public void Update()
        {
            if (!IsActive) return;

            // Keep player frozen while modal is active
            if (currentPlayer != null && currentPlayer.state != null)
            {
                if (currentPlayer.state.CURRENT_STATE != StateMachine.State.InActive)
                {
                    currentPlayer.state.CURRENT_STATE = StateMachine.State.InActive;
                }
            }

            // Universal controller and keyboard inputs
            bool acceptPressed = false;
            bool cancelPressed = false;
            float verticalAxis = 0f;

            try
            {
                if (InputManager.UI != null)
                {
                    acceptPressed = InputManager.UI.GetAcceptButtonDown();
                    cancelPressed = InputManager.UI.GetCancelButtonDown();
                    verticalAxis = InputManager.UI.GetVerticalAxis();
                }
            }
            catch { }

            // Fallbacks for keyboard & generic joysticks
            if (!acceptPressed)
            {
                acceptPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            }
            if (!cancelPressed)
            {
                cancelPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
            }
            if (Mathf.Abs(verticalAxis) < 0.2f)
            {
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) verticalAxis = 1f;
                else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) verticalAxis = -1f;
            }

            if (CurrentState == PaymentState.MethodSelection)
            {
                // Navigation between 3 options (0=Card, 1=Cash, 2=Cancel)
                if (Mathf.Abs(verticalAxis) > 0.45f && (Time.unscaledTime - lastNavTime > 0.22f))
                {
                    lastNavTime = Time.unscaledTime;
                    if (verticalAxis > 0)
                    {
                        selectedIndex = (selectedIndex - 1 + 3) % 3;
                    }
                    else
                    {
                        selectedIndex = (selectedIndex + 1) % 3;
                    }
                }

                // Hotkey numbers
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                {
                    selectedIndex = 0;
                    ExecuteOption(0);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                {
                    selectedIndex = 1;
                    ExecuteOption(1);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                {
                    selectedIndex = 2;
                    ExecuteOption(2);
                    return;
                }

                // Accept button (A / Enter)
                if (acceptPressed)
                {
                    ExecuteOption(selectedIndex);
                    return;
                }

                // Cancel button (B / ESC)
                if (cancelPressed)
                {
                    CancelPayment();
                    return;
                }
            }
            else if (CurrentState == PaymentState.CardSwipePrompt)
            {
                // ESC or B cancels the payment
                if (cancelPressed)
                {
                    CancelPayment();
                    return;
                }
            }
        }

        private void ExecuteOption(int option)
        {
            switch (option)
            {
                case 0:
                    SelectCardPayment();
                    break;
                case 1:
                    SelectCashPayment();
                    break;
                case 2:
                    CancelPayment();
                    break;
            }
        }

        public void SelectCardPayment()
        {
            CurrentState = PaymentState.CardSwipePrompt;
            noticeMessage = "";
            TarotCardReceiverPlugin.Log?.LogInfo("[PaymentManager] Switched to Card Payment screen. Awaiting card swipe...");
        }

        public void SelectCashPayment()
        {
            noticeMessage = "Cash payment is not yet configured.";
            noticeExpiry = Time.unscaledTime + 2.5f;
            TarotCardReceiverPlugin.Log?.LogInfo("[PaymentManager] Cash selected (placeholder).");
        }

        public void ReturnToMethodSelection()
        {
            CancelPayment();
        }

        public void CancelPayment()
        {
            TarotCardReceiverPlugin.Log?.LogInfo("[PaymentManager] Payment cancelled by player.");
            CloseAndUnfreeze();
        }

        public void OnCardPaymentReceived()
        {
            if (!IsWaitingForCard) return;

            TarotCardReceiverPlugin.Log?.LogInfo($"[PaymentManager] Card payment verified for {itemName}!");

            // Check affordability
            if (itemCost > 0 && Inventory.GetItemQuantity((int)costType) < itemCost)
            {
                TarotCardReceiverPlugin.Log?.LogWarning($"[PaymentManager] Player lacks funds: requires {itemCost} {costType}");
                TarotCardReceiverPlugin.SetOnScreenNotification("Insufficient Funds!");
                CancelPayment();
                return;
            }

            // Execute purchase fulfillment
            IsFulfillingPayment = true;
            try
            {
                if (currentInteraction != null)
                {
                    var playerState = currentPlayer != null ? currentPlayer.state : null;

                    if (currentInteraction is Interaction_BuyItem buyItem)
                    {
                        buyItem.OnInteract(playerState);
                    }
                    else if (currentInteraction is Interaction_FollowerInSpiderWeb spider)
                    {
                        spider.OnInteract(playerState);
                    }
                    else if (currentInteraction is Interaction_FollowerInSpiderWebDailyShop spiderDaily)
                    {
                        spiderDaily.OnInteract(playerState);
                    }
                    else if (currentInteraction is Interaction_Tarot tarot)
                    {
                        tarot.OnInteract(playerState);
                    }
                    else
                    {
                        currentInteraction.OnInteract(playerState);
                    }
                }
            }
            catch (Exception ex)
            {
                TarotCardReceiverPlugin.Log?.LogError($"[PaymentManager] Error during purchase fulfillment: {ex}");
            }
            finally
            {
                IsFulfillingPayment = false;
            }

            TarotCardReceiverPlugin.SetOnScreenNotification("Payment Complete!");
            CloseAndUnfreeze();
        }

        private void CloseAndUnfreeze()
        {
            CurrentState = PaymentState.Idle;
            hasLoggedGuiActive = false;

            if (currentPlayer != null && currentPlayer.state != null)
            {
                if (currentPlayer.state.CURRENT_STATE == StateMachine.State.InActive)
                {
                    currentPlayer.state.CURRENT_STATE = StateMachine.State.Idle;
                }
            }

            currentInteraction = null;
            currentPlayer = null;
        }

        private void OnGUI()
        {
            DrawOnGUI();
        }

        public void DrawOnGUI()
        {
            if (!IsActive) return;

            // Guard against multiple calls in the same event pass of the frame
            if (Time.frameCount == lastRenderedFrame && Event.current.type == lastRenderedEventType)
            {
                return;
            }
            lastRenderedFrame = Time.frameCount;
            lastRenderedEventType = Event.current.type;

            if (!hasLoggedGuiActive)
            {
                hasLoggedGuiActive = true;
                TarotCardReceiverPlugin.Log?.LogInfo($"[PaymentManager] DrawOnGUI active on screen! Resolution: {Screen.width}x{Screen.height}, State: {CurrentState}");
            }

            EnsureStyles();

            GUI.depth = -1000;

            // Centered compact dialog box dimensions
            float width = 600f;
            float height = 80f;
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;
            Rect dialogRect = new Rect(x, y, width, height);

            var prevBg = GUI.backgroundColor;
            var prevColor = GUI.color;

            // 1. Semi-transparent dark backdrop
            GUI.color = new Color(DialogBackdropColor.r, DialogBackdropColor.g, DialogBackdropColor.b, DialogOpacity);
            GUI.DrawTexture(dialogRect, Texture2D.whiteTexture);
            GUI.color = prevColor;

            // 2. Semi-transparent beveled occult box frame on top
            GUI.backgroundColor = new Color(DialogBoxTint.r, DialogBoxTint.g, DialogBoxTint.b, DialogOpacity);
            GUI.Box(dialogRect, GUIContent.none, containerBoxStyle);
            GUI.backgroundColor = prevBg;

            if (CurrentState == PaymentState.CardSwipePrompt)
            {
                DrawCardSwipePrompt(dialogRect);
            }
            else if (CurrentState == PaymentState.MethodSelection)
            {
                DrawMethodSelection(dialogRect);
            }

            GUI.backgroundColor = prevBg;
            GUI.color = prevColor;
        }

        private void DrawMethodSelection(Rect container)
        {
            float contentX = container.x + 35f;
            float contentWidth = container.width - 70f;

            var prevBg = GUI.backgroundColor;
            var prevColor = GUI.color;

            // Title
            Rect titleRect = new Rect(contentX, container.y + 20f, contentWidth, 42f);
            GUI.color = new Color(0.12f, 0.06f, 0.09f, ButtonOpacity);
            GUI.DrawTexture(titleRect, Texture2D.whiteTexture);
            GUI.color = prevColor;

            GUI.backgroundColor = new Color(0.20f, 0.12f, 0.15f, 1f);
            GUI.Box(titleRect, $"TRANSACTION OF THE FAITHFUL", titleStyle);
            GUI.backgroundColor = prevBg;

            // Item Details
            Rect itemRect = new Rect(contentX, container.y + 72f, contentWidth, 26f);
            GUI.Label(itemRect, $"Item: {itemName}", subtitleStyle);

            // Price Details
            string costStr = itemCost > 0 ? $"{itemCost} {costType}" : "Free";
            Rect priceRect = new Rect(contentX, container.y + 100f, contentWidth, 26f);
            GUI.Label(priceRect, $"Amount Due: {costStr}", priceStyle);

            // Notice message
            if (Time.unscaledTime < noticeExpiry && !string.IsNullOrEmpty(noticeMessage))
            {
                Rect noticeRect = new Rect(contentX, container.y + 130f, contentWidth, 24f);
                GUI.Label(noticeRect, noticeMessage, noticeStyle);
            }

            // Buttons
            string[] buttonLabels = new string[]
            {
                $"[ 1 / A ] {Gold("COMMUNE")} WITH SACRED PLASTIC",
                $"[ 2 ] {Gold("TENDER")} RAW GOLD",
                $"[ 3 / B ] {BloodRed("RENEGE")} ON PACT"
            };

            float startY = container.y + 165f;
            float btnHeight = 58f;
            float btnSpacing = 16f;

            for (int i = 0; i < 3; i++)
            {
                Rect btnRect = new Rect(contentX, startY + i * (btnHeight + btnSpacing), contentWidth, btnHeight);
                bool isSelected = (selectedIndex == i);

                // Mouse hover detection
                if (btnRect.Contains(Event.current.mousePosition))
                {
                    selectedIndex = i;
                    isSelected = true;
                }

                // Solid button backdrop to prevent transparency
                var btnBackdrop = isSelected ? new Color(0.35f, 0.08f, 0.14f, ButtonOpacity) : new Color(0.12f, 0.08f, 0.10f, ButtonOpacity);
                GUI.color = btnBackdrop;
                GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
                GUI.color = prevColor;

                string prefix = isSelected ? "▶  " : "    ";
                GUI.backgroundColor = isSelected ? new Color(0.85f, 0.18f, 0.28f, 1f) : new Color(0.28f, 0.20f, 0.26f, 1f);
                if (GUI.Button(btnRect, prefix + buttonLabels[i], isSelected ? selectedBtnStyle : normalBtnStyle))
                {
                    selectedIndex = i;
                    ExecuteOption(i);
                }
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawCardSwipePrompt(Rect container)
        {
            float contentX = container.x + 15f;
            float contentY = container.y + 14f;
            float contentWidth = container.width - 30f;
            float contentHeight = container.height - 28f;

            var prevBg = GUI.backgroundColor;
            var prevColor = GUI.color;

            Rect statusRect = new Rect(contentX, contentY, contentWidth, contentHeight);

            // Semi-transparent backing for status prompt
            GUI.color = new Color(0.20f, 0.14f, 0.04f, DialogOpacity);
            GUI.DrawTexture(statusRect, Texture2D.whiteTexture);
            GUI.color = prevColor;

            GUI.backgroundColor = new Color(0.55f, 0.38f, 0.12f, DialogOpacity);
            GUI.Box(statusRect, "TAP THE SACRAMENTAL PLASTIC", pulsingStatusStyle);
            GUI.backgroundColor = prevBg;
        }

        private static void EnsureStyles()
        {
            if (stylesInitialized && containerBoxStyle != null) return;

            containerBoxStyle = new GUIStyle(GUI.skin.box);

            titleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            titleStyle.normal.textColor = Color.white;

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            subtitleStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);

            priceStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            priceStyle.normal.textColor = new Color(0.898f, 0.663f, 0.235f);

            noticeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            noticeStyle.normal.textColor = new Color(0.824f, 0.169f, 0.169f);

            pulsingStatusStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            pulsingStatusStyle.normal.textColor = Color.white;

            normalBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            normalBtnStyle.normal.textColor = Color.white;

            selectedBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            selectedBtnStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }
    }
}
