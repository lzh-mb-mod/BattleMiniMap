"""Prefab single-root regression using the same first-element selection as Gauntlet."""
from pathlib import Path
import xml.etree.ElementTree as ET
p=Path(__file__).parents[2]/'BattleMiniMap/Modules/BattleMiniMap/GUI/Prefabs/BattleMiniMapView.xml'
window=ET.parse(p).getroot().find('Window');roots=list(window)
assert len(roots)==1,'Gauntlet only loads Window.FirstChild'
root=roots[0];children=root.find('Children');assert children is not None
assert children.find('BattleMiniMap_MapWidget') is not None
assert children.find('BattleMiniMap_GpuDiagnosticWidget') is not None
assert children.find('BattleMiniMap_MapWidget').find('.//BattleMiniMap_GpuDiagnosticWidget') is None,'Diagnostic must not inherit minimap circular clipping'
bad=ET.fromstring('<Window><Map/><Diagnostic/></Window>');assert list(bad)[0].find('Diagnostic') is None
for passive in (root,children.find('BattleMiniMap_GpuDiagnosticWidget')):
    assert passive.get('DoNotAcceptEvents')=='true','Presentation containers must not capture the mouse'
    assert passive.get('DoNotPassEventsToChildren')=='false','Map interaction descendants must remain reachable'
control=children.find('BattleMiniMap_MapWidget').find('.//MapControlWidget')
assert control is not None and control.get('DoNotAcceptEvents','false')=='false'
print('PASS: single root, map and diagnostic reachable as siblings, no circular map clipping; old two-root fixture drops diagnostic')
print('PASS: fullscreen root and diagnostic ignore input; map interaction child remains reachable')

preview=children.find('BattleMiniMap_GeometryPreviewWidget')
assert preview is not None and preview.get('IsVisible')=='@GeometryPreviewVisible'
assert children.find('BattleMiniMap_MapWidget').find('.//BattleMiniMap_GeometryPreviewWidget') is None
assert preview.find('.//ButtonWidget').get('Command.Click')=='CloseGeometryPreview'
print('PASS: independently hidden preview sibling with close command; no circular clipping')
