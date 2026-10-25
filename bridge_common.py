import asyncio
import json
import os
from bleak import BleakClient, BleakScanner
from bleak.exc import BleakError

# Network / IPC Configuration
UDP_IP = "127.0.0.1"
UDP_PORT = 9999
TAROT_IPC_FILE = "tarot_command.txt"

# Nordic UART Service (NUS) Default UUIDs
DEFAULT_DEVICE_NAME = "COTL_NOTIFY"
DEFAULT_UART_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
DEFAULT_UART_RX_CHAR_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"  # PC -> Device (write)
DEFAULT_UART_TX_CHAR_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"  # Device -> PC (notify/indicate)

# Aliases for backward compatibility
DEVICE_NAME = DEFAULT_DEVICE_NAME
UART_SERVICE_UUID = DEFAULT_UART_SERVICE_UUID
UART_RX_CHAR_UUID = DEFAULT_UART_RX_CHAR_UUID
UART_TX_CHAR_UUID = DEFAULT_UART_TX_CHAR_UUID


def log_bridge(msg: str):
    """Prints message directly to stdout without file logging."""
    print(msg, flush=True)


def sanitize_card_message(raw_msg: str) -> str:
    """Normalizes incoming BLE card payloads to match TarotCards.Card enum names.
    
    Accepts:
    - Pure enum name: "TheDeal"
    - Numeric ID: "13"
    - Prefixed strings: "TarotCards.Card.TheDeal", "Card.TheDeal", "card: TheDeal"
    - JSON payload: {"card": "TheDeal"} or {"id": 13}
    """
    msg = raw_msg.strip()
    if not msg:
        return ""

    # Check for JSON wrapper
    if msg.startswith("{") and msg.endswith("}"):
        try:
            parsed = json.loads(msg)
            if isinstance(parsed, dict):
                for key in ("card", "name", "id", "cardType", "command", "action", "payment"):
                    if key in parsed:
                        msg = str(parsed[key]).strip()
                        break
        except Exception:
            pass

    # Strip prefixes
    lowered = msg.lower()
    if lowered.startswith("tarotcards.card."):
        msg = msg[len("tarotcards.card."):].strip()
    elif lowered.startswith("card."):
        msg = msg[len("card."):].strip()
    elif lowered.startswith("card:"):
        msg = msg[len("card:"):].strip()

    return msg


def resolve_rx_characteristic(client: BleakClient, preferred_uuid: str):
    """Finds the appropriate BleakGATTCharacteristic to write data to (PC -> BLE)."""
    all_chars = list(client.services.characteristics.values())

    # 1. Match preferred UUID
    if preferred_uuid:
        for char in all_chars:
            if char.uuid.lower() == preferred_uuid.lower():
                return char

    # 2. Match default Nordic UART RX characteristic
    for char in all_chars:
        if char.uuid.lower() == DEFAULT_UART_RX_CHAR_UUID.lower():
            log_bridge(f"[BLE] Using default Nordic UART RX characteristic: {char.uuid}")
            return char

    # 3. Match any characteristic supporting write
    writable = [
        c for c in all_chars
        if any(p in ("write", "write-without-response") for p in c.properties)
    ]
    if writable:
        chosen = writable[0]
        log_bridge(
            f"[BLE] Auto-detected writable characteristic: {chosen.uuid} "
            f"({chosen.description or 'No description'}, properties: {chosen.properties})"
        )
        return chosen

    raise BleakError(
        f"No writable characteristic found on device (preferred '{preferred_uuid}')."
    )


def resolve_tx_characteristic(client: BleakClient, preferred_uuid: str):
    """Finds the appropriate BleakGATTCharacteristic to subscribe to for incoming data (BLE -> PC)."""
    all_chars = list(client.services.characteristics.values())

    # 1. Match preferred UUID
    if preferred_uuid:
        for char in all_chars:
            if char.uuid.lower() == preferred_uuid.lower():
                return char

    # 2. Match default Nordic UART TX characteristic
    for char in all_chars:
        if char.uuid.lower() == DEFAULT_UART_TX_CHAR_UUID.lower():
            log_bridge(f"[BLE] Using default Nordic UART TX characteristic: {char.uuid}")
            return char

    # 3. Match any characteristic supporting notify or indicate
    notifiable = [
        c for c in all_chars
        if any(p in ("notify", "indicate") for p in c.properties)
    ]
    if notifiable:
        chosen = notifiable[0]
        log_bridge(
            f"[BLE] Auto-detected notifiable characteristic: {chosen.uuid} "
            f"({chosen.description or 'No description'}, properties: {chosen.properties})"
        )
        return chosen

    return None


async def find_ble_device(mode: str, device_name: str, target_address: str = None, service_uuid: str = DEFAULT_UART_SERVICE_UUID, timeout: float = 5.0):
    """Locates a target BLE device by address or name/service filter."""
    if mode == "direct" and target_address:
        log_bridge(f"[BLE] Locating device '{device_name}' ({target_address})...")
        return await BleakScanner.find_device_by_address(target_address, timeout=timeout)
    else:
        log_bridge(f"[BLE] Scanning for '{device_name}' (Fallback mode)...")
        return await BleakScanner.find_device_by_filter(
            lambda d, adv: (
                (d.name and device_name.lower() in d.name.lower())
                or (adv.local_name and device_name.lower() in adv.local_name.lower())
                or (service_uuid.lower() in [str(s).lower() for s in adv.service_uuids])
            ),
            timeout=timeout
        )


async def choose_ble_device(role_title: str = "BLE Device", prompt_rx: bool = True, prompt_tx: bool = True) -> dict:
    """Scans for nearby BLE devices and prompts the user to select one,
    with fallback to hardcoded default values.
    """
    fallback_config = {
        "mode": "fallback",
        "name": DEFAULT_DEVICE_NAME,
        "address": None,
        "service_uuid": DEFAULT_UART_SERVICE_UUID,
        "rx_uuid": DEFAULT_UART_RX_CHAR_UUID,
        "tx_uuid": DEFAULT_UART_TX_CHAR_UUID,
    }

    while True:
        log_bridge("\n" + "=" * 65)
        log_bridge(f"   [{role_title}] Scanning for BLE devices (4s)...")
        log_bridge("=" * 65)

        try:
            discovered = await BleakScanner.discover(timeout=4.0, return_adv=True)
        except Exception as e:
            log_bridge(f"[BLE] Scan failed ({e}). Defaulting to fallback configuration.")
            return fallback_config

        devices_list = []
        for address, (dev, adv) in discovered.items():
            name = dev.name or adv.local_name
            devices_list.append({
                "address": address,
                "name": name if name else "(Unnamed Device)",
                "has_name": bool(name),
                "rssi": adv.rssi if adv.rssi is not None else -999,
                "services": [str(s) for s in adv.service_uuids]
            })

        # Sort: named devices first, then by signal strength (RSSI) descending
        devices_list.sort(key=lambda d: (not d["has_name"], -d["rssi"]))

        log_bridge("\nAvailable Devices:")
        log_bridge("  [0] Use Hardcoded Fallback:")
        log_bridge(f"      Name:         {DEFAULT_DEVICE_NAME}")
        log_bridge(f"      Service UUID: {DEFAULT_UART_SERVICE_UUID}")
        if prompt_rx:
            log_bridge(f"      RX Char UUID: {DEFAULT_UART_RX_CHAR_UUID} (PC -> Device)")
        if prompt_tx:
            log_bridge(f"      TX Char UUID: {DEFAULT_UART_TX_CHAR_UUID} (Device -> PC)")
        log_bridge("-" * 65)

        if not devices_list:
            log_bridge("  (No BLE devices discovered nearby)")
        else:
            for idx, d in enumerate(devices_list, 1):
                srv_hint = f" | Services: {len(d['services'])}" if d["services"] else ""
                log_bridge(f"  [{idx:2d}] {d['name']:<24} | Address: {d['address']} | RSSI: {d['rssi']} dBm{srv_hint}")

        log_bridge("-" * 65)
        log_bridge("Options: Enter a number [0-N], 'r' to re-scan, or press Enter for fallback [0].")

        try:
            user_input = await asyncio.to_thread(input, f"\n[{role_title}] Selection: ")
            user_input = user_input.strip()
        except (EOFError, KeyboardInterrupt):
            log_bridge("\n[BLE] Defaulting to hardcoded fallback.")
            return fallback_config

        if not user_input or user_input == "0":
            log_bridge(f"[BLE] Selected: Hardcoded fallback ({DEFAULT_DEVICE_NAME})")
            return fallback_config

        if user_input.lower() == "r":
            continue

        try:
            choice_idx = int(user_input)
            if 1 <= choice_idx <= len(devices_list):
                selected = devices_list[choice_idx - 1]
                log_bridge(f"\n[BLE] Selected device: {selected['name']} ({selected['address']})")

                chosen_rx_uuid = DEFAULT_UART_RX_CHAR_UUID
                if prompt_rx:
                    try:
                        rx_input = await asyncio.to_thread(
                            input,
                            f"Custom RX UUID (Press Enter for default [{DEFAULT_UART_RX_CHAR_UUID}]): "
                        )
                        rx_input = rx_input.strip()
                    except (EOFError, KeyboardInterrupt):
                        rx_input = ""
                    if rx_input:
                        chosen_rx_uuid = rx_input

                chosen_tx_uuid = DEFAULT_UART_TX_CHAR_UUID
                if prompt_tx:
                    try:
                        tx_input = await asyncio.to_thread(
                            input,
                            f"Custom TX UUID (Press Enter for default [{DEFAULT_UART_TX_CHAR_UUID}]): "
                        )
                        tx_input = tx_input.strip()
                    except (EOFError, KeyboardInterrupt):
                        tx_input = ""
                    if tx_input:
                        chosen_tx_uuid = tx_input

                return {
                    "mode": "direct",
                    "name": selected["name"],
                    "address": selected["address"],
                    "service_uuid": DEFAULT_UART_SERVICE_UUID,
                    "rx_uuid": chosen_rx_uuid,
                    "tx_uuid": chosen_tx_uuid,
                }
            else:
                log_bridge(f"Invalid selection: {user_input}. Please choose between 0 and {len(devices_list)}.")
        except ValueError:
            log_bridge(f"Invalid input: '{user_input}'. Please enter a number or 'r'.")


def write_tarot_ipc_file(card_message: str, target_file: str = TAROT_IPC_FILE, base_dir: str = None) -> bool:
    """Writes a card message to the shared IPC file for TarotCardReceiver."""
    if not card_message:
        return False
    if base_dir is None:
        base_dir = os.path.dirname(os.path.abspath(__file__))
    cmd_file = os.path.join(base_dir, target_file)
    try:
        with open(cmd_file, "w", encoding="utf-8") as f:
            f.write(card_message.strip())
        log_bridge(f"[PC -> Game File IPC] Wrote card '{card_message}' to {cmd_file}")
        return True
    except Exception as ipc_err:
        log_bridge(f"[PC -> Game File IPC Error]: {ipc_err}")
        return False
