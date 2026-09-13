# cotl-mountmars-mods

A collection of **Cult of the Lamb** BepInEx plugins paired with an asynchronous Python telemetry bridge for real-time game event monitoring and Bluetooth Low Energy (BLE) notifications.

---

## Components

### 1. BepInEx Plugins (C#)
- **COTL Time Logger (`COTLTimeLogger.cs`)**: Periodically captures in-game day and time progression, logging it to the BepInEx console and broadcasting via UDP.
- **Follower Poop Watcher (`FollowerPoopWatcher.cs`)**: Intercepts follower waste and cleanliness events (poop, vomit, camp hygiene) in real-time.
- **Follower Thought Reader (`FollowerThoughtReader.cs`)**: Reads and exposes real-time thoughts and state transitions of followers.
- **UDP Messenger (`Common/UdpMessenger.cs`)**: Shared helper module that serializes and transmits telemetry packets over UDP to `127.0.0.1:9999`.

### 2. Python Bridge (`python_bridge.py`)
- Asynchronous UDP server (`127.0.0.1:9999`) built with `asyncio`.
- Receives live telemetry strings sent by the Unity plugins.
- Forwards received game notifications over Bluetooth Low Energy (BLE) using Nordic UART service characteristics (`Bleak`).

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

2. **Start the Python Bridge:**
   ```bash
   python python_bridge.py
   ```

3. **Launch the Game:**
   - Launch *Cult of the Lamb*.
   - The mods will load with BepInEx, patch the relevant game methods via Harmony, and begin transmitting events to the bridge.

---

## License

This project is licensed under the MIT License.
