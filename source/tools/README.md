# Optional verified bridge geometry cache

The geometry overlay currently supports the verified `empire_town_g` bridge on the locally validated game build. It is a top-down overlay on the existing map, with full XYZ source instances retained. It does not replace physics or navigation.

A normal build needs no cache. To package a separately validated geometry artifact, run:

```powershell
python -B source/tools/Export-MapGeometryCache.py --input <validated-artifact-directory> --output source/BattleMiniMap/obj/map-geometry-cache --manifest source/BattleMiniMap/src/View/MapGeometry/MapGeometryManifest.cs
```

The input contains `geometry.json` and its matching validation `summary.json`. The exporter requires the approved 218-triangle artifact, preserves the 3242-triangle original fallback, bakes the five instance transforms and reverses mirrored winding. Cache XML stays in `obj`, the build package and the installed module, outside Git. The generated C# file stores its fingerprint only. Cache export after a geometry change must be followed by rebuilding the DLL.

The runtime checks the scene name, installed scene fingerprint and cache fingerprint. Missing or mismatched input leaves the existing map active. In Map options, use **Bridge geometry overlay**, **Use simplified map geometry**, and optional **Limit displayed geometry height** / **Geometry height ceiling (m)**. Turning off simplification selects the complete original mesh. Height clipping affects display only and does not identify walkable floors. A native proxy upload failure attempts the original mesh once; further managed failure disables the overlay for that mission.

Pure managed checks, without game initialization:

```powershell
dotnet run --project source/tests/MapGeometryTests/MapGeometryTests.csproj -c Release -- <cache-xml>
```

Game-native appearance, switching, depth-buffer behavior and mission teardown need visual validation on the matching game build. No sampling or scene enumeration is used by this feature.

The current visibility recovery uses a deterministic CPU depth-rasterized texture from these same cached triangles. It rebuilds only when map size, variant or height ceiling changes. The private native SceneView implementation is retained for investigation, but is not selected until its pixel output is verified. This is a temporary display fallback, not evidence of GPU speedup. Status logs report nontransparent pixel coverage.


GPU isolation is optional and defaults to Off. In Map options choose **GPU offscreen diagnostic**: Off, Clear color only, Synthetic triangle, Cached bridge geometry. A separate 256 px window appears at the top right; the CPU map stays active. **GPU diagnostic depth test/write** affects only this private scene. Start with depth off; compare Triangle with depth off/on, then Bridge. The bridge view is uniformly normalized from the existing cache; it does not enumerate or sample mission geometry. A blue clear window without geometry distinguishes render execution from geometry visibility; no window means the UI/target/scheduling path still needs investigation. Camera-viewport and ready-state logs do not prove pixel output. No GPU readback is performed.

The diagnostic and map share a single full-screen root (Gauntlet loads only the first Window child). A purple strip above the diagnostic is a CPU UI-presence control and appears even if the GPU target is invalid. It is not GPU output. The target clear color is blue; the UI logs target existence/validity before rejecting an invalid target. Camera viewport Y points up; texture UV Y points down. Loading-state logs concern only the private scene.
