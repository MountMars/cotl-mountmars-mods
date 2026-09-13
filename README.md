# cotl-mountmars-mods

A collection of **Cult of the Lamb** BepInEx plugins paired with an asynchronous Python telemetry bridge for real-time game event monitoring and Bluetooth Low Energy (BLE) notifications.

---

## Components

### 1. BepInEx Plugins (C#)
- **COTL Time Logger (`COTLTimeLogger.cs`)**: Periodically captures in-game day and time progression, logging it to the BepInEx console and broadcasting via UDP.
- **Follower Poop Watcher (`FollowerPoopWatcher.cs`)**: Intercepts follower waste and cleanliness events (poop, vomit, camp hygiene) in real-time.
- **Follower Thought Reader (`FollowerThoughtReader.cs`)**: Reads and exposes real-time thoughts and state transitions of followers.
- **Tarot Card Receiver (`TarotCardReceiver.cs`)**: Replaces Clauneck's 2-card altar choice with a physical scan-and-present ritual. When interacting with Clauneck, the player is prompted to draw and scan a card. The scanned card is presented in the full-screen Tarot pickup overlay. Subsequent scans update the presented card, and clicking away the card locks in that (last) card as the chosen reward. Also supports live in-game preview banners outside of Clauneck interactions via File IPC (`tarot_command.txt` and `tarot_queue.txt`).
  - See **[TarotCardList.md](file:///c:/Program%20Files%20%28x86%29/Steam/steamapps/common/Cult%20of%20the%20Lamb/BepInEx/plugins/TarotCardList.md)** for the complete list of all 104 `TarotCards.Card` enum values.
  - See **[TarotCardsGuide.md](file:///c:/Program%20Files%20%28x86%29/Steam/steamapps/common/Cult%20of%20the%20Lamb/BepInEx/plugins/TarotCardsGuide.md)** for the developer integration guide.
- **UDP Messenger (`Common/UdpMessenger.cs`)**: Shared helper module that serializes and transmits telemetry packets over UDP to `127.0.0.1:9999`.

### 2. Python Bridges
The bridge logic is split into modular scripts allowing simultaneous connections to different BLE devices:
- **Telemetry Sender (`udp_to_ble.py`)**: Asynchronous UDP server (`127.0.0.1:9999`) that receives game telemetry packets (follower thoughts, waste events, time) and sends them to a dedicated BLE device/display via Nordic UART RX.
- **Tarot Card Receiver (`ble_to_file.py`)**: Subscribes to notifications from an external BLE card reader/controller and writes commands to `tarot_command.txt` via File IPC for `TarotCardReceiver.cs`.
- **Bridge Common (`bridge_common.py`)**: Shared constants, device scanning/selection, and GATT characteristic resolution helpers.
- **Bridge Launcher (`python_bridge.py`)**: Interactive dispatcher menu to launch either service or both concurrently.

---

## Requirements

- **Cult of the Lamb** (Steam / PC)
- **[BepInEx 5](https://github.com/BepInEx/BepInEx)** (x64) installed into the game directory
- **.NET SDK / MSBuild** to compile the C# project
- **Python 3.8+** with dependencies:
  ```bash
  pip install bleak
  ```

---

## Installation & Setup

1. **Compile & Install Mod:**
   - Build the project using `COTLTimeLogger.csproj`.
   - Place the compiled `.dll` inside `Cult of the Lamb/BepInEx/plugins/`.

2. **Start the Python Bridges:**
   - Run the telemetry bridge in one terminal window:
     ```bash
     python udp_to_ble.py
     ```
   - Run the tarot card receiver in a second terminal window (connecting to a different BLE device):
     ```bash
     python ble_to_file.py
     ```
   - Or use the interactive launcher:
     ```bash
     python python_bridge.py
     ```

3. **Launch the Game:**
   - Launch *Cult of the Lamb*.
   - The mods will load with BepInEx, patch the relevant game methods via Harmony, and begin transmitting events to the bridge.

---

## Disclaimer & Legal Notice

This is an independent, unofficial, private hobby project intended solely for personal, educational, and research purposes.

This project is **not** affiliated with, authorized by, endorsed by, sponsored by, or in any way officially associated with **Massive Monster Pty Ltd** (the developer) or **Devolver Digital, Inc.** (the publisher), or any of their subsidiaries or affiliates.

*Cult of the Lamb*, all related titles, logos, characters, artwork, and associated intellectual property are trademarks or registered trademarks of **Massive Monster Pty Ltd** and **Devolver Digital, Inc.** All other trademarks and copyrights are the property of their respective owners.

This software is provided "as is" without warranty of any kind, express or implied. Use of this software is at your own risk.

---

## License

This project is licensed under the MIT License.

