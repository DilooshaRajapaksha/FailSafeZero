# FailSafeZero - World Performance & Rendering Optimization (Student 1)
1. **Static Batching:** Enabled across all modular floor, wall, pillar, and prop meshes to minimize URP draw calls.
2. **Texture Compression:** Max resolution capped at 1024px with Crunch Compression (Quality 75) to reduce VRAM footprint.
3. **Shadow Atlas Management:** Punctual light ranges bounded per arena so shadow maps do not overlap across corridors.
4. **Zero-Allocation AI Sensing:** Player velocity sensed via frame displacement without physics raycast garbage collection.
