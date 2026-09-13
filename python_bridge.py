import asyncio
import logging
import sys
from bleak import BleakClient, BleakScanner
from bleak.exc import BleakError

UDP_IP = "127.0.0.1"
UDP_PORT = 9999

# Hardcoded fallback values
DEFAULT_DEVICE_NAME = "COTL_NOTIFY"
DEFAULT_UART_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
DEFAULT_UART_RX_CHAR_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"

# Aliases for backward compatibility
DEVICE_NAME = DEFAULT_DEVICE_NAME
UART_SERVICE_UUID = DEFAULT_UART_SERVICE_UUID
UART_RX_CHAR_UUID = DEFAULT_UART_RX_CHAR_UUID


class UdpServerProtocol(asyncio.DatagramProtocol):
    def __init__(self, message_queue: asyncio.Queue):
        self.message_queue = message_queue

    def datagram_received(self, data: bytes, addr):
        message = data.decode("utf-8", errors="replace").strip()
        print(f"[UDP] Received: {message}", flush=True)
        if self.message_queue.full():
            try:
                self.message_queue.get_nowait()
            except asyncio.QueueEmpty:
                pass
        self.message_queue.put_nowait(message)


def resolve_rx_characteristic(client: BleakClient, preferred_uuid: str):
    """Finds the appropriate BleakGATTCharacteristic to write to.
    
    1. Match preferred UUID if present.
    2. Fall back to default Nordic UART RX UUID.
    3. Auto-detect any characteristic with write capability.
    """
    all_chars = list(client.services.characteristics.values())

    # 1. Match preferred UUID
    if preferred_uuid:
        for char in all_chars:
            if char.uuid.lower() == preferred_uuid.lower():
                return char

    # 2. Match default Nordic UART RX characteristic
    for char in all_chars:
        if char.uuid.lower() == DEFAULT_UART_RX_CHAR_UUID.lower():
            print(f"[BLE] Using default Nordic UART RX characteristic: {char.uuid}", flush=True)
            return char

    # 3. Match any characteristic supporting write
    writable = [
        c for c in all_chars
        if any(p in ("write", "write-without-response") for p in c.properties)
    ]
    if writable:
        chosen = writable[0]
        print(
            f"[BLE] Auto-detected writable characteristic: {chosen.uuid} "
            f"({chosen.description or 'No description'}, properties: {chosen.properties})",
            flush=True
        )
        return chosen

    raise BleakError(
        f"No writable characteristic found on device (preferred '{preferred_uuid}')."
    )


async def choose_ble_device() -> dict:
    """Scans for nearby BLE devices and prompts the user to select one,
    with fallback to hardcoded default values.
    """
    fallback_config = {
        "mode": "fallback",
        "name": DEFAULT_DEVICE_NAME,
        "address": None,
        "service_uuid": DEFAULT_UART_SERVICE_UUID,
        "rx_uuid": DEFAULT_UART_RX_CHAR_UUID,
    }

    while True:
        print("\n" + "=" * 65)
        print("          Scanning for nearby Bluetooth LE devices (4s)...")
        print("=" * 65, flush=True)

        try:
            discovered = await BleakScanner.discover(timeout=4.0, return_adv=True)
        except Exception as e:
            print(f"[BLE] Scan failed ({e}). Defaulting to fallback configuration.", flush=True)
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

        print("\nAvailable Devices:")
        print("  [0] Use Hardcoded Fallback:")
        print(f"      Name: {DEFAULT_DEVICE_NAME}")
        print(f"      Service UUID: {DEFAULT_UART_SERVICE_UUID}")
        print(f"      RX Char UUID: {DEFAULT_UART_RX_CHAR_UUID}")
        print("-" * 65)

        if not devices_list:
            print("  (No BLE devices discovered nearby)")
        else:
            for idx, d in enumerate(devices_list, 1):
                srv_hint = f" | Services: {len(d['services'])}" if d["services"] else ""
                print(f"  [{idx:2d}] {d['name']:<24} | Address: {d['address']} | RSSI: {d['rssi']} dBm{srv_hint}")

        print("-" * 65)
        print("Options: Enter a number [0-N], 'r' to re-scan, or press Enter for fallback [0].")

        try:
            user_input = await asyncio.to_thread(input, "\nSelection: ")
            user_input = user_input.strip()
        except (EOFError, KeyboardInterrupt):
            print("\n[BLE] Defaulting to hardcoded fallback.", flush=True)
            return fallback_config

        if not user_input or user_input == "0":
            print(f"[BLE] Selected: Hardcoded fallback ({DEFAULT_DEVICE_NAME})", flush=True)
            return fallback_config

        if user_input.lower() == "r":
            continue

        try:
            choice_idx = int(user_input)
            if 1 <= choice_idx <= len(devices_list):
                selected = devices_list[choice_idx - 1]
                print(f"\n[BLE] Selected device: {selected['name']} ({selected['address']})", flush=True)

                # Prompt for custom RX UUID or fallback
                try:
                    uuid_input = await asyncio.to_thread(
                        input,
                        f"Custom RX UUID (Press Enter for default [{DEFAULT_UART_RX_CHAR_UUID}]): "
                    )
                    uuid_input = uuid_input.strip()
                except (EOFError, KeyboardInterrupt):
                    uuid_input = ""

                chosen_rx_uuid = uuid_input if uuid_input else DEFAULT_UART_RX_CHAR_UUID

                return {
                    "mode": "direct",
                    "name": selected["name"],
                    "address": selected["address"],
                    "service_uuid": DEFAULT_UART_SERVICE_UUID,
                    "rx_uuid": chosen_rx_uuid,
                }
            else:
                print(f"Invalid selection: {user_input}. Please choose between 0 and {len(devices_list)}.")
        except ValueError:
            print(f"Invalid input: '{user_input}'. Please enter a number or 'r'.")


async def ble_worker(message_queue: asyncio.Queue, config: dict):
    """Manages BLE connection to selected device and transmits queued messages."""
    mode = config["mode"]
    device_name = config["name"]
    target_address = config.get("address")
    rx_uuid = config.get("rx_uuid", DEFAULT_UART_RX_CHAR_UUID)
    service_uuid = config.get("service_uuid", DEFAULT_UART_SERVICE_UUID)

    while True:
        try:
            device = None
            if mode == "direct" and target_address:
                print(f"[BLE] Locating device '{device_name}' ({target_address})...", flush=True)
                device = await BleakScanner.find_device_by_address(target_address, timeout=5.0)
                if not device:
                    print(f"[BLE] Device '{device_name}' ({target_address}) not found in scan. Retrying in 3s...", flush=True)
                    await asyncio.sleep(3.0)
                    continue
            else:
                print(f"[BLE] Scanning for '{device_name}' (Fallback mode)...", flush=True)
                device = await BleakScanner.find_device_by_filter(
                    lambda d, adv: (
                        (d.name and device_name.lower() in d.name.lower())
                        or (adv.local_name and device_name.lower() in adv.local_name.lower())
                        or (service_uuid.lower() in [str(s).lower() for s in adv.service_uuids])
                    ),
                    timeout=5.0
                )
                if not device:
                    print(f"[BLE] Device '{device_name}' not found. Retrying in 3s...", flush=True)
                    await asyncio.sleep(3.0)
                    continue

            print(f"[BLE] Found '{device_name}' ({device.address}). Connecting...", flush=True)

            disconnected_event = asyncio.Event()

            def on_disconnect(client):
                print(f"[BLE] Disconnected from {device_name}.", flush=True)
                disconnected_event.set()

            async with BleakClient(device, disconnected_callback=on_disconnect) as client:
                print(f"[BLE] Successfully connected to {device_name}!", flush=True)

                # Resolve characteristic
                target_char = resolve_rx_characteristic(client, rx_uuid)
                supports_write_nr = "write-without-response" in target_char.properties
                use_response = False if supports_write_nr else True

                print(
                    f"[BLE] Ready to send data via characteristic: {target_char.uuid} "
                    f"(write-without-response: {supports_write_nr})",
                    flush=True
                )

                while client.is_connected and not disconnected_event.is_set():
                    try:
                        # Wait for a message with a timeout so we can regularly check connection status
                        message = await asyncio.wait_for(message_queue.get(), timeout=1.0)
                    except asyncio.TimeoutError:
                        continue

                    payload = message.encode("utf-8")
                    try:
                        await client.write_gatt_char(target_char, payload, response=use_response)
                        print(f"[BLE] Forwarded message to {device_name}: {message}", flush=True)
                    except Exception as e:
                        print(f"[BLE] Error writing to characteristic: {e}", flush=True)
                        break

        except (BleakError, asyncio.TimeoutError, Exception) as ex:
            print(f"[BLE] Connection error: {ex}. Retrying in 3s...", flush=True)
            await asyncio.sleep(3.0)


async def main():
    config = await choose_ble_device()

    message_queue = asyncio.Queue(maxsize=50)

    loop = asyncio.get_running_loop()
    transport, protocol = await loop.create_datagram_endpoint(
        lambda: UdpServerProtocol(message_queue),
        local_addr=(UDP_IP, UDP_PORT)
    )
    print(f"\n[UDP] Server listening on {UDP_IP}:{UDP_PORT}... Waiting for COTL messages.", flush=True)

    ble_task = asyncio.create_task(ble_worker(message_queue, config))

    try:
        await ble_task
    except asyncio.CancelledError:
        pass
    finally:
        transport.close()
        print("\nServer stopped.", flush=True)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nShutdown requested by user.", flush=True)