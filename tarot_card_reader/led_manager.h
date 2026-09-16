#ifndef LED_MANAGER_H
#define LED_MANAGER_H

#include <Arduino.h>
#include <Adafruit_NeoPixel.h>

// Digital pin connected to DI (Data In) of the LED ring (GPIO 4 / D4)
// Note: Do not use GPIO 5 as it is the default VSPI hardware SS pin and boot strapping pin
#define LED_PIN        4

// Number of LEDs on the NeoPixel ring
#define LED_NUMPIXELS  24

// Default brightness (0-255)
#define LED_BRIGHTNESS 200

// Initialize the NeoPixel ring
void led_init();

// Clear/turn off all LEDs
void led_clear();

// Set all LEDs to a specific color
void led_fill(uint32_t color);

// Play the 800ms card detection animation:
// Two neighboring LEDs turn on and travel in both directions
// around the ring until they meet again at the opposite side.
void led_play_card_animation(uint32_t color = 0);

#endif // LED_MANAGER_H
