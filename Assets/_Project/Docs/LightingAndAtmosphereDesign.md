# FailSafeZero - Atmospheric Lighting & Mood Specification (Student 1)

## 1. Subterranean Environment Baseline
- **Directional Sunlight:** Intensity set to `0.00` to simulate a fully subterranean, windowless research bunker.
- **Global Ambient Lighting:** Flat color ambient set to deep industrial charcoal (`RGB: 0.05, 0.06, 0.08`). Eliminates washed-out skybox light.
- **Volumetric Exponential Squared Fog:**
  - Mode: `FogMode.ExponentialSquared`
  - Density: `0.020`
  - Color: Cool subterranean slate (`RGB: 0.08, 0.09, 0.12`)
  - Distance attenuation creates depth perception down long 4m corridors while masking distant occlusion culling bounds.

## 2. Room Lighting Mood Architecture
- **Corridors (A, B, C, D):** Low ambient point lights (intensity 1.5) with sinusoidal pulsing red warning beacons (`RGB: 1.0, 0.1, 0.1`) driven by `EmergencyLightPulse.cs`.
- **Arena 1 (Zombie Cryo Lab):** Cold Cyan illumination (`RGB: 0.0, 0.85, 0.95`, intensity 3.5, range 18m) reinforcing clinical freeze/thaw horror.
- **Arena 2 (Gravity Test Chamber):** Industrial High-Voltage Amber (`RGB: 1.0, 0.55, 0.05`, intensity 4.0, range 22m) highlighting vertical high-bay hazards.
- **Arena 3 (Illusion Containment):** Deep Perceptual Violet (`RGB: 0.75, 0.15, 1.0`, intensity 3.5, range 20m) complementing holographic decoy bot distortion.
- **07c Maze Arena:** Bio-luminescent teal fill lights (`RGB: 0.2, 0.9, 0.6`, intensity 3.0, range 16m) providing visibility without spoiling maze navigation.
- **08 Exit Room:** Clean extraction emerald green (`RGB: 0.1, 0.9, 0.3`, intensity 4.0, range 12m) indicating objective completion.
