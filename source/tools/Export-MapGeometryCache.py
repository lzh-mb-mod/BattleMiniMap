"""Export a verified bridge map artifact into an optional local game cache (stdlib)."""
import argparse,hashlib,json,math
from pathlib import Path
import xml.etree.ElementTree as ET
SCENE_HASH='965ede77b48e0ef353578447160563af442234dfbf2f13063468bb99979a63ba'
def world(p,pl):
 x,y,z=[a*b for a,b in zip(p,pl['scale'])];a,b,c=pl['euler_rad']
 x,z=math.cos(b)*x+math.sin(b)*z,-math.sin(b)*x+math.cos(b)*z
 y,z=math.cos(a)*y-math.sin(a)*z,math.sin(a)*y+math.cos(a)*z
 x,y=math.cos(c)*x-math.sin(c)*y,math.sin(c)*x+math.cos(c)*y
 return tuple(a+b for a,b in zip((x,y,z),pl['origin']))
def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--input',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);ap.add_argument('--manifest',type=Path,required=True);args=ap.parse_args()
 summary=json.loads((args.input/'summary.json').read_text(encoding='utf-8-sig'));blob=(args.input/'geometry.json').read_bytes();digest=hashlib.sha256(blob).hexdigest()
 if not summary['selected']['passed'] or summary['selected']['scene_faces_after']!=218:raise ValueError('Expected approved 218-face artifact')
 if not any(Path(r['path']).name=='geometry.json' and r['sha256']==digest for r in summary['artifacts']):raise ValueError('Geometry artifact hash mismatch')
 data=json.loads(blob);root=ET.Element('mapGeometry',schema='1',scene='empire_town_g',sceneSha256=SCENE_HASH,artifactSha256=digest,xyBudget='0.05',surfaceBudget='0.1')
 totals={'original':0,'proxy':0};layers=0
 for name,r in data['resources'].items():
  if name not in ('stone_bridge_wide','stone_bridge_wide_extension'):raise ValueError('Unexpected resource')
  for index,pl in enumerate(r['placements']):
   layer=ET.SubElement(root,'layer',id=f'{name}/{index}',resource=name,origin=','.join(map(str,pl['origin'])),euler=','.join(map(str,pl['euler_rad'])),scale=','.join(map(str,pl['scale'])))
   for source,target in [('original','original'),('simplified','proxy')]:
    mesh=r[source];v=[world(p,pl) for p in mesh['vertices']];faces=mesh['faces']
    if math.prod(pl['scale'])<0:faces=[(f[2],f[1],f[0]) for f in faces]
    m=ET.SubElement(layer,target);ET.SubElement(m,'vertices').text=';'.join(','.join(format(x,'.17g') for x in p) for p in v)
    ET.SubElement(m,'faces').text=';'.join(','.join(map(str,f)) for f in faces);totals[target]+=len(faces)
   layers+=1
 if layers!=5 or totals!={'original':3242,'proxy':218}:raise ValueError('Inventory mismatch')
 cache=ET.tostring(root,encoding='utf-8',xml_declaration=True);sha=hashlib.sha256(cache).hexdigest()
 args.output.mkdir(parents=True,exist_ok=True);p=args.output/'empire_town_g.xml';p.write_bytes(cache)
 args.manifest.parent.mkdir(parents=True,exist_ok=True);args.manifest.write_text('// Generated fingerprint only; raw game geometry is not stored in source control.\nnamespace BattleMiniMap.View.MapGeometry\n{\n    internal static class MapGeometryManifest\n    {\n        internal const string CacheSha256 = "'+sha+'";\n    }\n}\n',encoding='utf-8')
 report=dict(input_geometry_sha256=digest,cache_path=str(p.resolve()),bytes=len(cache),sha256=sha,layers=layers,triangles=totals,scene_sha256=SCENE_HASH)
 (args.output/'export-summary.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(report))
if __name__=='__main__':main()
