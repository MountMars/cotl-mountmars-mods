/*
wiring:
RC522    PinESP32     PinNote
3.3V     3V3          Do not connect to 5V (can damage the module)
RST      GPIO 22      Reset pin
GND      GND          Ground
IRQ      Unconnected  Not needed for basic reading
MISO     GPIO 19      SPI Master In / Slave Out
MOSI     GPIO 23      SPI Master Out / Slave In
SCK      GPIO 18      SPI Clock
SDA (SS) GPIO 21      SPI Chip Select

WS2812B NeoPixel Ring (24 LEDs):
VCC      5V / VIN     Power supply (5V - do NOT use 3.3V)
GND      GND          Ground
DI       GPIO 4       Data input pin (D4)
*/

#include <SPI.h>
#include <MFRC522.h>
#include <map>
#include "ble_manager.h"
#include "led_manager.h"

// Mapping of card text -> BLE message value
static const std::map<String, String> cardMap = {
  {"Temptation's Fate", "Sin"},
  {"The Hearts III", "Hearts3"},
  {"The Bomb", "BombOnRoll"},
  {"Devine Strength", "AttackRate"},
  {"Telescope", "Telescope"},
  {"Fervour's Harvest", "IncreaseBlackSoulsDrop"}
};

#define SS_PIN  21  // SDA pin on RC522
#define RST_PIN 22  // RST pin on RC522

MFRC522 rfid(SS_PIN, RST_PIN);

String readUltralightText() {
  byte data[48];
  byte buffer[18];
  byte size;
  int totalBytes = 0;

  // Read pages 4 through 15 (3 reads of 16 bytes each)
  for (byte page = 4; page <= 12; page += 4) {
    size = sizeof(buffer);
    MFRC522::StatusCode status = rfid.MIFARE_Read(page, buffer, &size);
    if (status != MFRC522::STATUS_OK) {
      return "";
    }
    for (byte i = 0; i < 16; i++) {
      data[totalBytes++] = buffer[i];
    }
  }

  // Find start of text:
  // Standard NDEF Text records have 'T' (0x54) followed by status byte (with language code length)
  int textStart = -1;
  for (int i = 0; i < totalBytes - 3; i++) {
    if (data[i] == 0x54) { // 'T'
      byte langLen = data[i + 1] & 0x3F;
      textStart = i + 2 + langLen;
      break;
    }
  }

  // Fallback: skip the first 14 header bytes if 'T' wasn't matched
  if (textStart == -1 || textStart >= totalBytes) {
    textStart = 14;
  }

  // Find end of text: stops at 0xFE (NDEF terminator) or null byte 0x00
  int textEnd = totalBytes;
  for (int i = textStart; i < totalBytes; i++) {
    if (data[i] == 0xFE || data[i] == 0x00) {
      textEnd = i;
      break;
    }
  }

  // Build the clean text string
  String text = "";
  for (int i = textStart; i < textEnd; i++) {
    if (data[i] >= 32 && data[i] <= 126) {
      text += (char)data[i];
    }
  }

  return text;
}

void setup() {
  Serial.begin(115200);
  led_init();                    // Initialize NeoPixel LED ring and clear boot noise immediately
  
  SPI.begin(18, 19, 23, SS_PIN); // Initialize SPI bus with explicit pins (SCK, MISO, MOSI, SS)
  rfid.PCD_Init();               // Initialize MFRC522 reader
  
  ble_init("TAROT_READER");      // Initialize BLE UART

  Serial.println("\nScan an RFID card or key fob...");
}

void loop() {
  // Look for new cards
  if (!rfid.PICC_IsNewCardPresent()) {
    return;
  }

  // Verify if the card UID is readable
  if (!rfid.PICC_ReadCardSerial()) {
    return;
  }

  // Format UID as uppercase hex string
  String uidStr = "";
  for (byte i = 0; i < rfid.uid.size; i++) {
    if (i > 0) uidStr += " ";
    if (rfid.uid.uidByte[i] < 0x10) uidStr += "0";
    uidStr += String(rfid.uid.uidByte[i], HEX);
  }
  uidStr.toUpperCase();

  // Print UID to Serial Monitor
  Serial.print("Card UID: ");
  Serial.println(uidStr);

  // Print PICC (Tag) Type
  MFRC522::PICC_Type piccType = rfid.PICC_GetType(rfid.uid.sak);
  Serial.print("Tag Type: ");
  Serial.println(rfid.PICC_GetTypeName(piccType));

  // Read and print clean text payload from card memory
  String cardText = readUltralightText();
  if (cardText.length() > 0) {
    Serial.print("Card Text: ");
    Serial.println(cardText);

    auto it = cardMap.find(cardText);
    if (it != cardMap.end()) {
      Serial.print("Mapped BLE Value: ");
      Serial.println(it->second);
      ble_send_message(it->second);
      led_play_card_animation();
    } else {
      Serial.println("Card text not found in map, not sending via BLE.");
    }
  }

  // Halt PICC to stop reading continuously
  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();
}