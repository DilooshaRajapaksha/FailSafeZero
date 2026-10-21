# Student 1: World Builder & Zombie Bot - Technical Design & Viva Defense Notes

## 1. Level Scale & Environmental Architecture
- **Metrics Standard:** 1 Unity Unit = 1 Metre across all modular assets.
- **Sequential Flow:** Start Room (10x10m) -> Corridor A -> Arena 1 (Zombie Cryo Lab) -> Corridor B -> Arena 2 (Gravity Chamber) -> Corridor C -> Arena 3 (Illusion Lab) -> Corridor D Extension -> Connector Stub -> Student 4 Maze Arena -> Exit Room (Extraction).
- **Tactical Geometry:** Symmetrical pillars and containment crates in Arena 1 provide cover for timing stops against the motion-tracking Zombie Bot.

## 2. Subterranean Lighting & Atmosphere (URP)
- **Directional Light Intensity = 0:** Emphasizes that the facility is deep underground.
- **Flat Ambient:** Replaced default skybox ambient with dark charcoal (`0.05, 0.06, 0.08`).
- **Volumetric Fog:** Exponential squared fog (`density = 0.02`) creates depth cueing down corridors.
- **Global Post-Processing:** ACES tonemapping, bloom on emissive trims, and vignette for helmet optics.
