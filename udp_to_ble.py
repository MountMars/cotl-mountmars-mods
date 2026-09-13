import asyncio
from bleak import BleakClient
from bleak.exc import BleakError

from bridge_common import (
    UDP_IP,
    UDP_PORT,
    DEFAULT_UART_RX_CHAR_UUID,
    DEFAULT_UART_SERVICE_UUID,
    log_bridge,
    choose_ble_device,
    find_ble_device,
    resolve_rx_characteristic,
)


class UdpServerProtocol(asyncio.DatagramProtocol):
    """Listens for telemetry messages sent from C# BepInEx plugins to forward to BLE."""
    def __init__(self, message_queue: asyncio.Queue):
        self.message_queue = message_queue

    def datagram_received(self, data: bytes, addr):
        message = data.decode("utf-8", errors="replace").strip()
        log_bridge(f"[UDP 9999 <- Mod] Received: {message}")
        if self.message_queue.full():
            try:
                self.message_queue.get_nowait()
            except asyncio.QueueEmpty:
                pass
        self.message_queue.put_nowait(message)


async def ble_sender_worker(message_queue: asyncio.Queue, config: dict):
    """Manages BLE connection to the selected telemetry receiver device:
    Forwards outgoing telemetry messages from UDP port 9999 to BLE RX.
    """
    mode = config["mode"]
    device_name = config["name"]
    target_address = config.get("address")
    rx_uuid = config.get("rx_uuid", DEFAULT_UART_RX_CHAR_UUID)
    service_uuid = config.get("service_uuid", DEFAULT_UART_SERVICE_UUID)

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
                log_bridge(f"[BLE Sender] Device '{device_name}' not found. Retrying in 3s...")
                await asyncio.sleep(3.0)
                continue

            log_bridge(f"[BLE Sender] Found '{device_name}' ({device.address}). Connecting...")

            disconnected_event = asyncio.Event()

            def on_disconnect(client):
                log_bridge(f"[BLE Sender] Disconnected from {device_name}.")
                disconnected_event.set()

            async with BleakClient(device, disconnected_callback=on_disconnect) as client:
                log_bridge(f"[BLE Sender] Successfully connected to {device_name}!")

                # Resolve RX Characteristic (PC -> Device)
                target_char = resolve_rx_characteristic(client, rx_uuid)
                supports_write_nr = "write-without-response" in target_char.properties
                use_response = False if supports_write_nr else True

                log_bridge(
                    f"[BLE Sender -> Device] Ready to send data via RX characteristic: {target_char.uuid} "
                    f"(write-without-response: {supports_write_nr})"
                )

                while client.is_connected and not disconnected_event.is_set():
                    try:
                        # Wait for an outgoing telemetry message from UDP
                        message = await asyncio.wait_for(message_queue.get(), timeout=1.0)
                    except asyncio.TimeoutError:
                        continue

                    payload = message.encode("utf-8")
                    try:
                        await client.write_gatt_char(target_char, payload, response=use_response)
                        log_bridge(f"[PC -> BLE] Forwarded message to {device_name}: {message}")
                    except Exception as e:
                        log_bridge(f"[BLE Sender] Error writing to characteristic: {e}")
                        break

        except (BleakError, asyncio.TimeoutError, Exception) as ex:
            log_bridge(f"[BLE Sender] Connection error: {ex}. Retrying in 3s...")
            await asyncio.sleep(3.0)


async def main():
    config = await choose_ble_device(
        role_title="Telemetry Sender (UDP 9999 -> BLE)",
        prompt_rx=True,
        prompt_tx=False
    )

    message_queue = asyncio.Queue(maxsize=50)

    loop = asyncio.get_running_loop()
    transport, protocol = await loop.create_datagram_endpoint(
        lambda: UdpServerProtocol(message_queue),
        local_addr=(UDP_IP, UDP_PORT)
    )
    log_bridge(f"\n[UDP 9999] Server listening on {UDP_IP}:{UDP_PORT} (Mod Telemetry -> BLE).")

    ble_task = asyncio.create_task(ble_sender_worker(message_queue, config))

    try:
        await ble_task
    except asyncio.CancelledError:
        pass
    finally:
        transport.close()
        log_bridge("\n[UDP 9999] Server stopped.")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        log_bridge("\nShutdown requested by user.")
