#include "ble_manager.h"
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

// Nordic UART Service (NUS) UUIDs
#define SERVICE_UUID           "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define CHARACTERISTIC_UUID_RX "6E400002-B5A3-F393-E0A9-E50E24DCCA9E" // Write (Remote -> ESP32)
#define CHARACTERISTIC_UUID_TX "6E400003-B5A3-F393-E0A9-E50E24DCCA9E" // Notify (ESP32 -> Remote)

static BLEServer* pServer = nullptr;
static BLECharacteristic* pTxCharacteristic = nullptr;
static bool deviceConnected = false;

// Callbacks for client connection and disconnection
class ServerCallbacks : public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) override {
        deviceConnected = true;
        // Serial.println("[BLE] Client connected.");
    }

    void onDisconnect(BLEServer* pServer) override {
        deviceConnected = false;
        Serial.println("[BLE] Client disconnected. Restarting advertising...");
        pServer->startAdvertising();
    }
};

// Optional: Callbacks for data received from the remote device
class RxCallbacks : public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic* pCharacteristic) override {
        String rxValue = pCharacteristic->getValue().c_str();
        if (rxValue.length() > 0) {
            Serial.print("[BLE] Received: ");
            Serial.println(rxValue);
        }
    }
};

void ble_init(const char* deviceName) {
    // 1. Initialize BLE device
    BLEDevice::init(deviceName);

    // 2. Create BLE Server
    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new ServerCallbacks());

    // 3. Create Nordic UART Service
    BLEService* pService = pServer->createService(SERVICE_UUID);

    // 4. Create TX Characteristic (Notify to remote device)
    pTxCharacteristic = pService->createCharacteristic(
        CHARACTERISTIC_UUID_TX,
        BLECharacteristic::PROPERTY_NOTIFY
    );
    pTxCharacteristic->addDescriptor(new BLE2902());

    // 5. Create RX Characteristic (Receive from remote device)
    BLECharacteristic* pRxCharacteristic = pService->createCharacteristic(
        CHARACTERISTIC_UUID_RX,
        BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR
    );
    pRxCharacteristic->setCallbacks(new RxCallbacks());

    // 6. Start the service
    pService->start();

    // 7. Setup advertising
    BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(true);
    pAdvertising->setMinPreferred(0x06); // functions that help with iPhone connections issue
    pAdvertising->setMaxPreferred(0x12);
    
    BLEDevice::startAdvertising();
    Serial.printf("[BLE] Device '%s' initialized and advertising.\n", deviceName);
}

bool ble_is_connected() {
    return deviceConnected;
}

void ble_send_message(const std::string& message) {
    if (!deviceConnected || pTxCharacteristic == nullptr) {
        Serial.println("[BLE] Cannot send message: No client connected.");
        return;
    }

    pTxCharacteristic->setValue((uint8_t*)message.c_str(), message.length());
    pTxCharacteristic->notify();
    Serial.printf("[BLE] Sent: %s\n", message.c_str());
}

void ble_send_message(const String& message) {
    ble_send_message(std::string(message.c_str()));
}
