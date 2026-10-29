using HarmonyLib;
using Lamb.UI;
using UnityEngine;

namespace COTLTarotCardReceiver
{
    // 1. Intercept hold-down start on Interaction (for hold-to-interact accessibility mode)
    [HarmonyPatch(typeof(Interaction), "OnHoldProgressDown")]
    public static class Interaction_OnHoldProgressDown_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction __instance, Indicator indicator, PlayerFarming currentPlayerFarming)
        {
            if (PaymentManager.Instance.IsActive)
            {
                return false;
            }

            if (!PaymentManager.Instance.IsFulfillingPayment &&
                PaymentManager.IsEligiblePurchaseInteraction(__instance))
            {
                TarotCardReceiverPlugin.Log?.LogInfo($"[PaymentPatches] Intercepted OnHoldProgressDown on {__instance.GetType().Name}");
                __instance.HoldProgress = 0f;
                __instance.HoldBegun = false;

                PaymentManager.Instance.StartPayment(__instance, currentPlayerFarming);
                return false; // Suppress normal hold progress & hold sound loop
            }

            return true;
        }
    }

    // 2. Intercept Interactor.Interact (universal entry point when interact button is pressed)
    [HarmonyPatch(typeof(Interactor), "Interact")]
    public static class Interactor_Interact_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interactor __instance, Interaction interaction)
        {
            if (PaymentManager.Instance.IsActive && !PaymentManager.Instance.IsFulfillingPayment)
            {
                return false;
            }

            if (!PaymentManager.Instance.IsFulfillingPayment &&
                PaymentManager.IsEligiblePurchaseInteraction(interaction))
            {
                TarotCardReceiverPlugin.Log?.LogInfo($"[PaymentPatches] Intercepted Interactor.Interact on {interaction.GetType().Name}");
                var player = __instance != null ? (__instance.GetComponent<PlayerFarming>() ?? PlayerFarming.Instance) : PlayerFarming.Instance;
                PaymentManager.Instance.StartPayment(interaction, player);
                return false;
            }

            return true;
        }
    }

    // 3. Intercept Interaction_FollowerInSpiderWeb.OnInteract directly
    [HarmonyPatch(typeof(Interaction_FollowerInSpiderWeb), "OnInteract")]
    public static class Interaction_FollowerInSpiderWeb_OnInteract_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction_FollowerInSpiderWeb __instance, StateMachine state)
        {
            if (!PaymentManager.Instance.IsFulfillingPayment &&
                PaymentManager.IsEligiblePurchaseInteraction(__instance))
            {
                TarotCardReceiverPlugin.Log?.LogInfo("[PaymentPatches] Intercepted Interaction_FollowerInSpiderWeb.OnInteract directly");
                PaymentManager.Instance.StartPayment(__instance, PlayerFarming.Instance);
                return false;
            }
            return true;
        }
    }

    // 4. Intercept Interaction_FollowerInSpiderWebDailyShop.OnInteract directly
    [HarmonyPatch(typeof(Interaction_FollowerInSpiderWebDailyShop), "OnInteract")]
    public static class Interaction_FollowerInSpiderWebDailyShop_OnInteract_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction_FollowerInSpiderWebDailyShop __instance, StateMachine state)
        {
            if (!PaymentManager.Instance.IsFulfillingPayment &&
                PaymentManager.IsEligiblePurchaseInteraction(__instance))
            {
                TarotCardReceiverPlugin.Log?.LogInfo("[PaymentPatches] Intercepted Interaction_FollowerInSpiderWebDailyShop.OnInteract directly");
                PaymentManager.Instance.StartPayment(__instance, PlayerFarming.Instance);
                return false;
            }
            return true;
        }
    }

    // 5. Intercept Interaction_BuyItem.OnInteract directly (Shop item booths)
    [HarmonyPatch(typeof(Interaction_BuyItem), "OnInteract")]
    public static class Interaction_BuyItem_OnInteract_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction_BuyItem __instance, StateMachine state)
        {
            if (!PaymentManager.Instance.IsFulfillingPayment &&
                PaymentManager.IsEligiblePurchaseInteraction(__instance))
            {
                TarotCardReceiverPlugin.Log?.LogInfo("[PaymentPatches] Intercepted Interaction_BuyItem.OnInteract directly");
                PaymentManager.Instance.StartPayment(__instance, PlayerFarming.Instance);
                return false;
            }
            return true;
        }
    }

    // 6. Intercept Interaction_Tarot.OnInteract directly (Clauneck's altar)
    [HarmonyPatch(typeof(Interaction_Tarot), "OnInteract")]
    public static class Interaction_Tarot_OnInteract_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction_Tarot __instance, StateMachine state)
        {
            if (!PaymentManager.Instance.IsFulfillingPayment &&
                PaymentManager.IsEligiblePurchaseInteraction(__instance))
            {
                TarotCardReceiverPlugin.Log?.LogInfo("[PaymentPatches] Intercepted Interaction_Tarot.OnInteract directly");
                PaymentManager.Instance.StartPayment(__instance, PlayerFarming.Instance);
                return false;
            }
            return true;
        }
    }
}
