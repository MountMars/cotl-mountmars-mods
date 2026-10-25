# Payment System Implementation Plan: POS Checkout

This document outlines the architecture, flow, and implementation steps
for replacing the hold-to-confirm key interaction on purchase objects
(such as shop items, Helob's followers, and Clauneck's tarot altar) with
an interactive Payment Modal supporting real-world card swipe verification
via the Tarot Card Reader.

---

## 1. Resolved Specifications & Key Decisions

1. **Triggering**: A single tap of the interact key (e.g. `E` on keyboard
   or `X` on controller) immediately opens the Payment Method screen,
   bypassing the need to hold the button down.
2. **In-Game Currency**: When payment is made by swiping a card, in-game
   currency (e.g. Gold Coins, Devotion) is still deducted as normal from
   player inventory upon successful card verification.
3. **Cancellation & Back Navigation**:
   * On the **"How do you want to pay?"** screen: Pressing **Cancel** or
     `ESC` cancels the purchase entirely, unfreezes the player, and
     returns to normal gameplay without buying the object.
   * On the **"Swipe Card"** screen: Pressing `ESC` cancels the card swipe
     attempt and **returns back to the "How do you want to pay?" screen**.
4. **Cash Placeholder**: Pressing **"Cash"** currently performs no action
   (reserved for future physical cash drawer / bill acceptor integration).

---

## 2. System Workflow

```mermaid
stateDiagram-v2
    [*] --> Idle: Approach purchasable object
    Idle --> PaymentMethodScreen: Tap Interact ('E' / 'X')

    state PaymentMethodScreen {
        [*] --> SelectingMethod
        SelectingMethod --> Idle: Cancel or ESC
        SelectingMethod --> SelectingMethod: Cash (No-op)
        SelectingMethod --> CardSwipeScreen: Card
    }

    state CardSwipeScreen {
        [*] --> WaitingForSwipe: Prompt to swipe
        WaitingForSwipe --> PaymentMethodScreen: ESC (Back)
        WaitingForSwipe --> ExecutePayment: Signal received
    }

    state ExecutePayment {
        [*] --> CheckAffordability: Check inventory
        CheckAffordability --> Insufficient: Not enough gold
        Insufficient --> Idle: Reject SFX & unfreeze
        CheckAffordability --> DeductCost: Deduct cost
        DeductCost --> CompletePurchase: Deliver item
        CompletePurchase --> Idle: Unfreeze & close
    }
```

---

## 3. Technical Architecture & Components

### A. Interaction Interception
* **Target Classes**:
  * `Interaction_BuyItem`: Shop item booths across dungeons and hubs
    (decorations, blueprints, seeds, shop tarot cards, weapons).
  * `Interaction_FollowerInSpiderWeb` &
    `Interaction_FollowerInSpiderWebDailyShop`: Helob selling followers.
  * `Interaction_Tarot`: Clauneck's altar ritual.
* **Harmony Hook**:
  * Hook `Interaction.OnHoldProgressDown` or prefix `Interactor.Update`:
    * When the player presses the interact button on a valid purchase
      interaction, immediately cancel the hold progression.
    * Freeze player movement and combat input
      (`playerFarming.state.CURRENT_STATE = StateMachine.State.InActive`).
    * Cache the current `Interaction` context and open the
      `PaymentManager` UI.

---

### B. Payment UI System (`PaymentManager.cs`)
Implemented via Unity GUI / custom overlay canvas with COTL-themed dark
crimson and gold styling:

#### Screen 1: Payment Method Selection ("How would you like to pay?")
* **Header**: *"How would you like to pay?"*
* **Item Details**: Displays item name and cost (e.g. *"Follower - 50 Gold"*).
* **Buttons**:
  1. **`[ Card ]`**: Transitions to Screen 2.
  2. **`[ Cash ]`**: Placeholder button (does nothing yet or flashes a brief
     *"Cash payment coming soon"* notice).
  3. **`[ Cancel ]`**: Dismisses modal, restores player state, and keeps
     item unbought.
* **Controls**:
  * Keyboard: `1` (Card), `2` (Cash), `3` / `ESC` (Cancel), or Arrow keys
    plus `Enter`.
  * Gamepad: D-pad / Left Stick navigation, `A` / Cross (Select),
    `B` / Circle (Cancel).
  * Mouse: Clickable buttons.

#### Screen 2: Card Swipe Prompt ("Card Payment")
* **Header**: *"Card Payment"*
* **Prompt**: *"Please swipe your card on the tarot reader..."*
* **Visuals**: Animated pulsing waiting prompt with a card reader icon.
* **Controls**:
  * `ESC` / Gamepad `B`: Aborts card payment and **returns back to
    Screen 1**.
  * Background listener: Awaiting `"card payment"` IPC event from the
    Tarot Reader.

---

### C. Hardware & IPC Signal Pipeline

1. **Hardware Reader Firmware (`tarot_card_reader/tarot_card_reader.ino`)**:
   * Add a payment card entry to `cardMap`:
     ```cpp
     {"card payment", "card payment"},
     ```
2. **BLE Bridge (`ble_to_file.py` & `bridge_common.py`)**:
   * Ensure `sanitize_card_message` allows `"card payment"` and writes
     it to `tarot_command.txt`.
3. **C# Plugin (`TarotCardReceiver.cs`)**:
   * In `TickInGame()`, check if `PaymentManager.IsWaitingForCard` is
     active.
   * If incoming command is `"card payment"`, route it to
     `PaymentManager.OnCardPaymentReceived()`.

---

### D. Purchase Execution & Cost Deduction

When the `"card payment"` signal arrives:
1. **Affordability Verification**:
   * Check if player has the required inventory
     (`Inventory.GetItemQuantity(costType) >= cost`).
   * If insufficient, play reject SFX (`cantAfford`) and show
     *"Not enough gold!"* notification.
2. **Deduct Inventory**:
   * Deduct required items/currency
     (`Inventory.ChangeItemQuantity(costType, -cost)`).
   * Play purchase SFX (`event:/shop/buy`).
3. **Execute Item-Specific Fulfillment**:
   * **For `Interaction_BuyItem`**: Call `boughtItem(itemForSale)` and
     trigger item pickup animation.
   * **For `Interaction_FollowerInSpiderWeb`**: Trigger
     `StartCoroutine(Purchase())`.
   * **For `Interaction_Tarot`**: Trigger `DoRoutine()` to present
     Clauneck's cards.
4. **Cleanup**:
   * Close the Payment UI.
   * Unfreeze player back to `StateMachine.State.Idle`.

---

## 4. Files to Create and Modify

* **`PaymentManager.cs`** (Create):
  * Implements POS UI dialogs, input navigation, state machine, and
    purchase callbacks.
* **`PaymentPatches.cs`** (Create):
  * Harmony patches on `Interaction`, `Interaction_BuyItem`,
    `Interaction_FollowerInSpiderWeb`, and `Interaction_Tarot` to
    intercept tap interaction.
* **`TarotCardReceiver.cs`** (Modify):
  * Forward `"card payment"` IPC messages to `PaymentManager` and render
    GUI.
* **`bridge_common.py`** (Modify):
  * Ensure `"card payment"` payload is preserved without being discarded
    as an unknown card.
* **`tarot_card_reader/tarot_card_reader.ino`** (Modify):
  * Register payment card payload in `cardMap` sending `"card payment"`
    over BLE.

---

## 5. Implementation Stages

1. **Stage 1: Core Payment Manager & UI**:
   * Create `PaymentManager.cs` handling Screen 1 (Card/Cash/Cancel),
     Screen 2 (Waiting for swipe), and all input bindings.
2. **Stage 2: Harmony Interception**:
   * Create `PaymentPatches.cs` to catch taps on Helob, Shop booths,
     and Clauneck, opening the modal instantly without holding.
3. **Stage 3: IPC & Bridge Integration**:
   * Hook `"card payment"` in `TarotCardReceiver.cs` and adjust
     `bridge_common.py`.
4. **Stage 4: Fulfillment & Inventory Deduction**:
   * Wire execution routines for each interaction type upon receipt of
     `"card payment"`.
5. **Stage 5: Verification & Edge Cases**:
   * Test ESC cancel from Screen 1 (returns to game).
   * Test ESC from Screen 2 (returns to Screen 1).
   * Test Cash click (no-op).
   * Test card payment with sufficient and insufficient gold.
