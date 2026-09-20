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

// Classic card detection animation:
// 4 blood red LEDs (every 6th LED) travel once around the ring.
void led_animation_quad_spin(uint32_t color = 0);

// "The Sacrificial Surge (Dual Comet & Flash)":
// Two 4-LED comets accelerate in opposite directions, collide at the bottom (0-450ms),
// ring flashes full crimson #B80000 (450-600ms), followed by logarithmic fade to darkness
// with a smoldering afterglow on 3 random ritual points (600-800ms+).
void led_animation_sacrificial_surge();

// "The Ritual Heartbeat (Double Thump)":
// A heavy, fleshy pulse mimicking Clauneck's tarot card draw.
// 0–200ms: Thump 1 spikes to 100% blood red in 40ms, decays to 20%.
// 200–550ms: Thump 2 spikes to 100% sulfurous red (#FF2A00) in 30ms, decays to 30%.
// 550–800ms: Lingering decay down to zero with subtle micro-flickers.
void led_animation_ritual_heartbeat();

// Active card play animation entry point (defaults to Sacrificial Surge)
void led_play_card_animation(uint32_t color = 0);

#endif // LED_MANAGER_H
