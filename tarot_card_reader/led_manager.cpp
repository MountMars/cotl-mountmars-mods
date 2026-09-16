#include "led_manager.h"

// Adafruit_NeoPixel instance for the 24-LED ring
static Adafruit_NeoPixel pixels(LED_NUMPIXELS, LED_PIN, NEO_GRB + NEO_KHZ800);

void led_init() {
  pixels.begin();
  pixels.setBrightness(LED_BRIGHTNESS);
  pixels.clear();
  pixels.show();
}

void led_clear() {
  pixels.clear();
  pixels.show();
}

void led_fill(uint32_t color) {
  for (int i = 0; i < LED_NUMPIXELS; i++) {
    pixels.setPixelColor(i, color);
  }
  pixels.show();
}

void led_play_card_animation(uint32_t color) {
  // Default color: occult purple (Cult of the Lamb theme)
  if (color == 0) {
    color = pixels.Color(150, 0, 150);
  }

  const int totalDurationMs = 600;
  const int steps = LED_NUMPIXELS / 2; // 12 steps for half-circle travel
  const int stepDelay = totalDurationMs / (steps + 1); // ~61 ms per frame

  // Start at two neighboring LEDs (index 0 and 1)
  const int startLeft = 0;
  const int startRight = 1;

  for (int step = 0; step <= steps; step++) {
    pixels.clear();

    int left = (startLeft - step + LED_NUMPIXELS) % LED_NUMPIXELS;
    int right = (startRight + step) % LED_NUMPIXELS;

    pixels.setPixelColor(left, color);
    pixels.setPixelColor(right, color);
    pixels.show();

    delay(stepDelay);
  }

  // Clear and turn off all LEDs when animation completes
  pixels.clear();
  pixels.show();
}
