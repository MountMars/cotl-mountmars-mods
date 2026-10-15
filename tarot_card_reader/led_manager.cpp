#include "led_manager.h"

// Adafruit_NeoPixel instance for the 24-LED ring
static Adafruit_NeoPixel pixels(LED_NUMPIXELS, LED_PIN, NEO_GRB + NEO_KHZ800);

void led_init() {
  pixels.begin();
  pixels.setBrightness(LED_BRIGHTNESS);
  pixels.clear();
  pixels.show();
  // Quick startup test animation to visually verify power, wiring, and GPIO pin connection on boot
  led_animation_quad_spin();
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

void led_animation_quad_spin(uint32_t color) {
  // Default color: blood red
  if (color == 0) {
    color = pixels.Color(255, 0, 0);
  }

  const int totalDurationMs = 600;
  const int steps = LED_NUMPIXELS; // 24 steps to travel once around the ring
  const int stepDelay = totalDurationMs / steps; // 25 ms per frame

  for (int step = 0; step < steps; step++) {
    pixels.clear();

    // 4 LEDs on, every 6th LED (0, 6, 12, 18 offset by step)
    for (int k = 0; k < 4; k++) {
      int idx = (step + k * 6) % LED_NUMPIXELS;
      pixels.setPixelColor(idx, color);
    }
    pixels.show();

    delay(stepDelay);
  }

  // Clear and turn off all LEDs when animation completes
  pixels.clear();
  pixels.show();
}

void led_animation_sacrificial_surge() {
  // ---------------------------------------------------------------------------
  // Phase 1 (0–450ms): Dual 4-LED comets accelerate in opposite directions,
  // completing 1.5 rotations (36 steps) to collide at the bottom (LED 12).
  // ---------------------------------------------------------------------------
  const unsigned long DURATION_COMET = 450;
  static const float weights[4] = { 1.0f, 0.65f, 0.35f, 0.15f };
  const float maxCrimson = 184.0f; // #B80000

  unsigned long startMs = millis();
  while (true) {
    unsigned long elapsed = millis() - startMs;
    if (elapsed >= DURATION_COMET) break;

    float progress = (float)elapsed / (float)DURATION_COMET; // 0.0 to 1.0
    // Quadratic acceleration: slow start, accelerating into collision
    float eased = progress * progress;
    float pos = eased * 36.0f; // 0.0 to 36.0 steps

    int headCW = (int)pos;
    uint16_t redAccum[LED_NUMPIXELS] = { 0 };

    for (int k = 0; k < 4; k++) {
      // Clockwise comet: moves forward (0 -> 1 -> 2 ...), tail trails behind (head - k)
      int idxCW = (headCW - k) % LED_NUMPIXELS;
      if (idxCW < 0) idxCW += LED_NUMPIXELS;
      redAccum[idxCW] += (uint16_t)(maxCrimson * weights[k]);

      // Counter-clockwise comet: moves backward (0 -> 23 -> 22 ...), tail trails behind (head + k)
      int idxCCW = (-headCW + k) % LED_NUMPIXELS;
      if (idxCCW < 0) idxCCW += LED_NUMPIXELS;
      redAccum[idxCCW] += (uint16_t)(maxCrimson * weights[k]);
    }

    for (int i = 0; i < LED_NUMPIXELS; i++) {
      uint8_t r = (redAccum[i] > 255) ? 255 : (uint8_t)redAccum[i];
      pixels.setPixelColor(i, pixels.Color(r, 0, 0));
    }
    pixels.show();
    delay(4);
  }

  // ---------------------------------------------------------------------------
  // Phase 2 (450–600ms, 150ms): Detonation flash.
  // All 24 LEDs instantly flash to full crimson (#B80000: R=184, G=0, B=0).
  // ---------------------------------------------------------------------------
  uint32_t crimson = pixels.Color(184, 0, 0);
  for (int i = 0; i < LED_NUMPIXELS; i++) {
    pixels.setPixelColor(i, crimson);
  }
  pixels.show();
  delay(150);

  // ---------------------------------------------------------------------------
  // Phase 3 (600–800ms): Logarithmic fade down to total darkness,
  // leaving a smoldering afterglow on just 3 random ritual points.
  // ---------------------------------------------------------------------------
  int ritualPoints[3];
  ritualPoints[0] = random(0, LED_NUMPIXELS);
  do {
    ritualPoints[1] = random(0, LED_NUMPIXELS);
  } while (ritualPoints[1] == ritualPoints[0]);
  do {
    ritualPoints[2] = random(0, LED_NUMPIXELS);
  } while (ritualPoints[2] == ritualPoints[0] || ritualPoints[2] == ritualPoints[1]);

  const unsigned long DURATION_FADE = 200; // 600ms to 800ms
  unsigned long fadeStart = millis();

  while (true) {
    unsigned long elapsed = millis() - fadeStart;
    if (elapsed >= DURATION_FADE) break;

    float progress = (float)elapsed / (float)DURATION_FADE; // 0.0 to 1.0
    // Logarithmic / perceptual exponential decay curve
    float fadeFactor = powf(1.0f - progress, 2.5f);

    uint8_t baseRed = (uint8_t)(184.0f * fadeFactor);
    uint8_t emberRed = (uint8_t)(35.0f + (184.0f - 35.0f) * fadeFactor);

    for (int i = 0; i < LED_NUMPIXELS; i++) {
      bool isRitual = (i == ritualPoints[0] || i == ritualPoints[1] || i == ritualPoints[2]);
      uint8_t r = isRitual ? emberRed : baseRed;
      pixels.setPixelColor(i, pixels.Color(r, 0, 0));
    }
    pixels.show();
    delay(5);
  }

  // Smoldering afterglow gently extinguishing to darkness
  const unsigned long DURATION_EMBER = 350;
  unsigned long emberStart = millis();
  while (true) {
    unsigned long elapsed = millis() - emberStart;
    if (elapsed >= DURATION_EMBER) break;

    float progress = (float)elapsed / (float)DURATION_EMBER;
    float emberFactor = powf(1.0f - progress, 1.8f);
    uint8_t emberRed = (uint8_t)(35.0f * emberFactor);

    pixels.clear();
    for (int k = 0; k < 3; k++) {
      pixels.setPixelColor(ritualPoints[k], pixels.Color(emberRed, 0, 0));
    }
    pixels.show();
    delay(10);
  }

  // Clear and turn off all LEDs when animation completes
  pixels.clear();
  pixels.show();
}

void led_animation_ritual_heartbeat() {
  // ---------------------------------------------------------------------------
  // "The Ritual Heartbeat (Double Thump)"
  // 0–200ms (Thump 1): Spikes to 100% blood red in 40ms, decays to 20% by 200ms.
  // 200–550ms (Thump 2): Spikes to 100% sulfurous red (#FF2A00) in 30ms, decays to 30%.
  // 550–800ms: Lingering decay down to zero with subtle micro-flickers.
  // ---------------------------------------------------------------------------
  const unsigned long TOTAL_DURATION = 800;
  unsigned long startMs = millis();

  while (true) {
    unsigned long elapsed = millis() - startMs;
    if (elapsed >= TOTAL_DURATION) break;

    uint8_t baseR = 0;
    uint8_t baseG = 0;
    bool applyFlicker = false;

    if (elapsed < 40) {
      // Thump 1 Attack: 0–40ms, spikes from black to 100% blood red (R=255, G=0, B=0)
      float p = (float)elapsed / 40.0f;
      baseR = (uint8_t)(255.0f * p);
      baseG = 0;
    } else if (elapsed < 200) {
      // Thump 1 Decay: 40–200ms, decays to 20% (R=51)
      float p = (float)(elapsed - 40) / 160.0f;
      float decay = powf(1.0f - p, 1.5f);
      baseR = (uint8_t)(51.0f + (255.0f - 51.0f) * decay);
      baseG = 0;
    } else if (elapsed < 230) {
      // Thump 2 Attack: 200–230ms, spikes from 20% to #FF2A00 (R=255, G=42, B=0)
      float p = (float)(elapsed - 200) / 30.0f;
      baseR = (uint8_t)(51.0f + (255.0f - 51.0f) * p);
      baseG = (uint8_t)(42.0f * p);
    } else if (elapsed < 550) {
      // Thump 2 Decay: 230–550ms, fades smoothly to 30% (R=76)
      float p = (float)(elapsed - 230) / 320.0f;
      float decay = powf(1.0f - p, 1.4f);
      baseR = (uint8_t)(76.0f + (255.0f - 76.0f) * decay);
      // Sulfurous green quickly cools back to deep red
      float greenDecay = powf(1.0f - p, 2.5f);
      baseG = (uint8_t)(42.0f * greenDecay);
    } else {
      // Lingering Decay: 550–800ms, down to zero with subtle pseudo-random micro-flickers
      float p = (float)(elapsed - 550) / 250.0f;
      float decay = powf(1.0f - p, 1.8f);
      baseR = (uint8_t)(76.0f * decay);
      baseG = 0;
      applyFlicker = true;
    }

    if (applyFlicker && baseR > 0) {
      for (int i = 0; i < LED_NUMPIXELS; i++) {
        int flicker = random(-15, 16);
        int r = (int)baseR + (int)((baseR * flicker) / 100);
        if (r < 0) r = 0;
        if (r > 255) r = 255;
        pixels.setPixelColor(i, pixels.Color((uint8_t)r, 0, 0));
      }
    } else {
      uint32_t col = pixels.Color(baseR, baseG, 0);
      for (int i = 0; i < LED_NUMPIXELS; i++) {
        pixels.setPixelColor(i, col);
      }
    }

    pixels.show();
    delay(4);
  }

  // Clear and turn off all LEDs when animation completes
  pixels.clear();
  pixels.show();
}

void led_play_card_animation(uint32_t color) {
  if (color != 0) {
    led_animation_quad_spin(color);
  } else {
    if (random(0, 2) == 0) {
      led_animation_sacrificial_surge();
    } else {
      led_animation_ritual_heartbeat();
    }
  }
}


