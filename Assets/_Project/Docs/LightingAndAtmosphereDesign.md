# FailSafeZero - Atmospheric Lighting & Mood Specification (Student 1)

## 1. Subterranean Environment Baseline
- **Directional Sunlight:** Intensity set to `0.00` (Mode: `Baked`) to simulate a deep subterranean, windowless containment facility.
- **Global Ambient Lighting:** Flat color ambient set to deep industrial charcoal (`RGB: 0.05, 0.06, 0.08`). Eliminates washed-out default skybox glare.
- **Volumetric Exponential Squared Fog:**
  - Mode: `FogMode.ExponentialSquared`
  - Density: `0.020`
  - Color: Cool subterranean slate (`RGB: 0.08, 0.09, 0.12`)
  - Distance attenuation creates depth perception down long 4m corridors while naturally masking distant occlusion boundaries.

## 2. Room Lighting Mood Architecture
- **Corridors (A, B, C):** Sinusoidal pulsing red emergency alert beacons (`RGB: 1.0, 0.1, 0.1`, intensity `0.5 - 3.0`, range `10m`) driven by `EmergencyLightPulse.cs`. Kept as **Realtime** lights with **No Shadows** to maintain 60+ FPS and eliminate dynamic shadow atlas contention.
- **Arena 1 (Zombie Cryo Lab):** Cold Cyan illumination (`RGB: 0.0, 0.85, 0.95`, intensity `3.5`, range `18m`, Baked Soft Shadows) reinforcing clinical freeze/thaw horror.
- **Arena 2 (Gravity Test Chamber):** High-Voltage Industrial Amber (`RGB: 1.0, 0.55, 0.05`, intensity `4.0`, range `22m`, Baked Soft Shadows) highlighting vertical high-bay hazards.
- **Arena 3 (Illusion Containment):** Deep Perceptual Violet (`RGB: 0.75, 0.15, 1.0`, intensity `3.5`, range `20m`, Baked Soft Shadows) complementing holographic decoy bot distortion.
- **07c Maze Arena:** Bio-luminescent teal fill lights (`RGB: 0.2, 0.9, 0.6`, intensity `3.0`, range `16m`, Baked Soft Shadows) providing visibility without spoiling maze navigation.
- **08 Exit Room:** Clean extraction emerald green (`RGB: 0.1, 0.9, 0.3`, intensity `4.0`, range `12m`, Baked Soft Shadows) signaling mission completion.

## 3. Baked Lighting Architecture & Progressive CPU Lightmapper
- **Lighting Settings Asset:** `Assets/_Project/Settings/Environment/LabLightingSettings.lighting`
  - **Lightmapper:** Progressive CPU
  - **Direct Samples:** 32 | **Indirect Samples:** 256 | **Max Bounces:** 2
  - **Lightmap Resolution:** 10.0 texels / unit (sharp architectural edges without excessive VRAM)
  - **Max Lightmap Size:** 1024 x 1024
  - **Ambient Occlusion (AO):** Enabled (Distance: 1.0m) for realistic corner shading and contact shadows.
  - **Texture Compression:** Enabled
- **Bake Results & Asset Serialization:**
  - **Output Assets:** Stored externally at `Assets/_Project/Scenes/MainLabScene/LightingData.asset`
  - **Lightmap Count:** 5 lightmaps (3x 1024x1024px, 2x 256x256px)
  - **Total Lightmap Disk/VRAM Footprint:** 8.3 MB (well within target mobile/standalone VRAM budget)
  - **Bake Time:** 00:03:28.48 (clean reproducible bake via Apple Rosetta lightmapping workers)

## 4. Light Probes & Reflection Probes (Dynamic Character & Specular Lighting)
- **Light Probe Network:** `LightProbeGroup` generated across all 11 lab zones (Start Room, Corridors A-D, Connector, Arenas 1-3, Maze Arena, Exit Room).
  - **Probe Count:** 1,194 light probes providing continuous spherical harmonic irradiance sampling for dynamic agents (`FirstPersonPlayer`, `ZombieBot_Agent`, `Maze_Ghost`, `IllusionBot`).
- **Reflection Probes:** 7 custom box-projected `ReflectionProbe` volumes placed in Start Room, Arenas 1-3, Maze Arena, and Exit Room.
  - Generates `ReflectionProbe-0.exr` through `ReflectionProbe-6.exr` for accurate PBR metallic and roughness reflections on floor trims and metallic crates.

## 5. Universal Render Pipeline (URP) Post-Processing Stack
- **Volume Type:** Global Volume (`LabPostFX.asset`) on `PostFX_Volume` GameObject.
- **Tonemapping:** ACES cinematic curve preserving highlight detail from emergency beacons without crushing shadow contrast.
- **Bloom:** Intensity `0.6`, Threshold `0.9`, Scatter `0.7` for subtle sci-fi emissive glow on terminal screens and hazard strips.
- **Vignette:** Intensity `0.28`, Smoothness `0.35`, Rounded `false` simulating claustrophobic helmet visor optics.
- **Color Adjustments:** Post-exposure `0.0`, Contrast `+15`, Saturation `+10` for punchy industrial contrast.

## 6. Third-party assets and licences
- **PBR Texture Assets:** AmbientCG modular materials (`Lab_Floor`, `Lab_Wall`, `Lab_Pillar`, `Lab_Ceiling`, `Lab_Trim`).
- **Source:** [ambientCG.com](https://ambientCG.com) (created by Lennart Demes).
- **Licence:** Creative Commons CC0 1.0 Universal (Public Domain Dedication).
- **Terms:** Free for personal, educational, and commercial use with no attribution strictly required; cited here for academic integrity and project transparency.
