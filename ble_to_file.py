import asyncio
import os
import random
import winsound
from bleak import BleakClient
from bleak.exc import BleakError

from bridge_common import (
    TAROT_IPC_FILE,
    DEFAULT_UART_TX_CHAR_UUID,
    DEFAULT_UART_SERVICE_UUID,
    log_bridge,
    choose_ble_device,
    find_ble_device,
    resolve_tx_characteristic,
    sanitize_card_message,
    write_tarot_ipc_file,
)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
FIRE_WHOOSH_SOUNDS = [
    os.path.join(SCRIPT_DIR, "1 fire whoosh.wav"),
    os.path.join(SCRIPT_DIR, "2 fire whoosh.wav"),
    os.path.join(SCRIPT_DIR, "3 fire whoosh.wav"),
]
AVAILABLE_FIRE_WHOOSH_SOUNDS = [path for path in FIRE_WHOOSH_SOUNDS if os.path.isfile(path)]
SPIDER_WARNING_SOUND = os.path.join(SCRIPT_DIR, "Spider Warning.wav")
APPLEPAY_SOUND = os.path.join(SCRIPT_DIR, "applepay.wav")


def play_applepay_sound():
    if not os.path.isfile(APPLEPAY_SOUND):
        log_bridge(f"[Audio] ApplePay sound not found at '{APPLEPAY_SOUND}'")
        return

    try:
        winsound.PlaySound(APPLEPAY_SOUND, winsound.SND_FILENAME | winsound.SND_ASYNC)
    except Exception as ex:
        log_bridge(f"[Audio] Error playing sound: {ex}")


def play_fire_whoosh_sound():
    if not AVAILABLE_FIRE_WHOOSH_SOUNDS:
        return

    sound_to_play = random.choice(AVAILABLE_FIRE_WHOOSH_SOUNDS)
    try:
        winsound.PlaySound(sound_to_play, winsound.SND_FILENAME | winsound.SND_ASYNC)
    except Exception as ex:
        log_bridge(f"[Audio] Error playing sound: {ex}")


def play_spider_warning_sound():
    if not os.path.isfile(SPIDER_WARNING_SOUND):
        log_bridge(f"[Audio] Spider warning sound not found at '{SPIDER_WARNING_SOUND}'")
        return

    try:
        winsound.PlaySound(SPIDER_WARNING_SOUND, winsound.SND_FILENAME | winsound.SND_ASYNC)
    except Exception as ex:
        log_bridge(f"[Audio] Error playing sound: {ex}")


async def ble_receiver_worker(config: dict):
    """Manages BLE connection to the selected card reader / controller device:
    Receives incoming BLE TX notifications and forwards them to TarotCardReceiver via File IPC.
    """
    mode = config["mode"]
    device_name = config["name"]
    target_address = config.get("address")
    tx_uuid = config.get("tx_uuid", DEFAULT_UART_TX_CHAR_UUID)
    service_uuid = config.get("service_uuid", DEFAULT_UART_SERVICE_UUID)
    script_dir = os.path.dirname(os.path.abspath(__file__))

    while True:
        try:
            device = await find_ble_device(
                mode=mode,
                device_name=device_name,
                target_address=target_address,
                service_uuid=service_uuid,
                timeout=5.0
            )

            if not device:
                log_bridge(f"[BLE Receiver] Device '{device_name}' not found. Retrying in 3s...")
                await asyncio.sleep(3.0)
                continue

            log_bridge(f"[BLE Receiver] Found '{device_name}' ({device.address}). Connecting...")

            disconnected_event = asyncio.Event()

            def on_disconnect(client):
                log_bridge(f"[BLE Receiver] Disconnected from {device_name}.")
                disconnected_event.set()

            async with BleakClient(device, disconnected_callback=on_disconnect) as client:
                log_bridge(f"[BLE Receiver] Successfully connected to {device_name}!")

                # Resolve and subscribe to TX characteristic (Device -> PC)
                tx_char = resolve_tx_characteristic(client, tx_uuid)

                def on_ble_notification(sender, data: bytearray):
                    raw_text = data.decode("utf-8", errors="replace").strip()
                    card_message = sanitize_card_message(raw_text)
                    log_bridge(f"[BLE -> PC] Incoming notification: '{raw_text}' -> Parsed: '{card_message}'")

                    if card_message:
                        write_tarot_ipc_file(card_message, target_file=TAROT_IPC_FILE, base_dir=script_dir)
                        lowered = card_message.strip().lower()
                        raw_lowered = raw_text.strip().lower()

                        if lowered in ("card payment", "card_payment", "payment") or raw_lowered in ("card payment", "card_payment", "payment"):
                            play_applepay_sound()
                        elif lowered in ("increaseddamage", "33", "spider", "8") or raw_lowered in ("increaseddamage", "spider"):
                            play_spider_warning_sound()
                        else:
                            play_fire_whoosh_sound()

                if tx_char:
                    log_bridge(f"[BLE Receiver] Subscribing to notifications on TX characteristic: {tx_char.uuid}")
                    await client.start_notify(tx_char, on_ble_notification)
                    log_bridge(f"[BLE -> TarotCardReceiver] Active! Forwarding to {TAROT_IPC_FILE}.")
                else:
                    log_bridge("[BLE Receiver] Warning: No notifiable TX characteristic found on device.")

                try:
                    await disconnected_event.wait()
                finally:
                    if tx_char and client.is_connected:
                        try:
                            await client.stop_notify(tx_char)
                        except Exception:
                            pass

        except (BleakError, asyncio.TimeoutError, Exception) as ex:
            log_bridge(f"[BLE Receiver] Connection error: {ex}. Retrying in 3s...")
            await asyncio.sleep(3.0)


async def main():
    config = await choose_ble_device(
        role_title="Tarot Card Receiver (BLE -> File IPC)",
        prompt_rx=False,
        prompt_tx=True
    )
    log_bridge(f"\n[File IPC] Configured target: {TAROT_IPC_FILE} (BLE -> TarotCardReceiver).")

    ble_task = asyncio.create_task(ble_receiver_worker(config))

    try:
        await ble_task
    except asyncio.CancelledError:
        pass
    finally:
        log_bridge("\n[BLE Receiver] Stopped.")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        log_bridge("\nShutdown requested by user.")
