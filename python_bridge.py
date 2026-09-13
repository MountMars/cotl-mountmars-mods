"""Legacy launcher / dispatcher for the Cult of the Lamb Bluetooth LE bridges.

The bridge logic has been split into independent programs so you can connect
to different BLE devices simultaneously in separate terminals:

  - udp_to_ble.py:
      Listens on UDP port 9999 for game events (poop, thoughts, time)
      and transmits them to a BLE display/device.

  - ble_to_file.py:
      Subscribes to BLE notifications from an external card reader/controller
      and writes commands to tarot_command.txt for TarotCardReceiver.

  - bridge_common.py:
      Shared BLE scanning, GATT characteristic resolution, and configuration constants.
"""

import sys
import asyncio
import udp_to_ble
import ble_to_file
from bridge_common import log_bridge


def main():
    log_bridge("=" * 65)
    log_bridge("          Cult of the Lamb - BLE Bridge Launcher")
    log_bridge("=" * 65)
    log_bridge("Select which bridge service you want to start:")
    log_bridge("  [1] Telemetry Sender   (UDP Port 9999 -> BLE)")
    log_bridge("  [2] Card Receiver      (BLE -> File IPC tarot_command.txt)")
    log_bridge("  [3] Run Both           (Separate BLE tasks)")
    log_bridge("-" * 65)
    log_bridge("Tip: You can also run 'py udp_to_ble.py' and 'py ble_to_file.py'")
    log_bridge("     in separate terminal windows to connect to different devices.")
    log_bridge("-" * 65)

    try:
        choice = input("Enter choice [1/2/3] (Default 1): ").strip()
    except (EOFError, KeyboardInterrupt):
        return

    if choice == "2":
        asyncio.run(ble_to_file.main())
    elif choice == "3":
        async def run_both():
            await asyncio.gather(
                udp_to_ble.main(),
                ble_to_file.main()
            )
        try:
            asyncio.run(run_both())
        except KeyboardInterrupt:
            log_bridge("\nShutdown requested by user.")
    else:
        asyncio.run(udp_to_ble.main())


if __name__ == "__main__":
    main()