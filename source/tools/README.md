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
