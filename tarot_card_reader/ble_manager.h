#ifndef BLE_MANAGER_H
#define BLE_MANAGER_H

#include <Arduino.h>
#include <string>

// Initialize BLE UART (Nordic UART Service) with a custom device name
void ble_init(const char* deviceName = "TAROT_READER");

// Check if a client device (e.g. phone or another ESP32) is connected
bool ble_is_connected();

// Send text message to connected device
void ble_send_message(const String& message);
void ble_send_message(const std::string& message);

#endif // BLE_MANAGER_H
