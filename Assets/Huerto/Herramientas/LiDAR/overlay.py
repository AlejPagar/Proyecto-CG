import re,sys
from PIL import Image, ImageDraw
gml, orto, out = sys.argv[1:4]
X0,Y0,X1,Y1 = map(float, sys.argv[4:8])  # recuadro UTM de la ortofoto
t=open(gml,encoding="latin-1").read()
im=Image.open(orto).convert("RGB"); d=ImageDraw.Draw(im); W,H=im.size
def px(x,y): return ((x-X0)/(X1-X0)*W, (Y1-y)/(Y1-Y0)*H)
for m in re.finditer(r'<cp:CadastralParcel gml:id="ES\.SDGC\.CP\.([^"]+)">(.*?)</cp:CadastralParcel>',t,re.S):
    rc,body=m.group(1),m.group(2)
    area=re.search(r'areaValue uom="m2">(\d+)',body).group(1)
    for pl in re.findall(r'<gml:posList[^>]*>([^<]+)',body):
        v=list(map(float,pl.split())); pts=[px(v[i],v[i+1]) for i in range(0,len(v),2)]
        col=(255,40,40) if rc.startswith("4961106") else (255,255,0)
        d.line(pts+[pts[0]],fill=col,width=3 if col[1]==40 else 1)
    rp=re.search(r'<gml:pos>([\d.]+) ([\d.]+)',body)
    if rp:
        x,y=px(float(rp.group(1)),float(rp.group(2)))
        if 0<x<W and 0<y<H: d.text((x-14,y-5),rc[:7][-3:]+"\n"+area,fill=(0,255,255))
im.save(out)
