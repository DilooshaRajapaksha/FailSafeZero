# FailSafeZero - Atmospheric Lighting & Mood Specification (Student 1)

## 1. Subterranean Environment Baseline
- **Directional Sunlight:** Intensity set to `0.00` to simulate a fully subterranean, windowless research bunker.
- **Global Ambient Lighting:** Flat color ambient set to deep industrial charcoal (`RGB: 0.05, 0.06, 0.08`). Eliminates washed-out skybox light.
- **Volumetric Exponential Squared Fog:**
  - Mode: `FogMode.ExponentialSquared`
  - Density: `0.020`
  - Color: Cool subterranean slate (`RGB: 0.08, 0.09, 0.12`)
  - Distance attenuation creates depth perception down long 4m corridors while masking distant occlusion culling bounds.
