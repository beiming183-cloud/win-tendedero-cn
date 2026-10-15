"""Generate the checked-in app icons; requires Pillow only for asset development."""
from pathlib import Path
from PIL import Image, ImageDraw

assets = Path(__file__).resolve().parents[1] / 'src' / 'WinTendedero' / 'Assets'
scale = 4
image = Image.new('RGBA', (256 * scale, 256 * scale))
draw = ImageDraw.Draw(image)
blue, gold = '#1e6fff', '#f5b942'
points = []
for i in range(101):
    t = i / 100
    x = (1-t)**2 * 24 + 2*(1-t)*t*128 + t*t*232
    y = (1-t)**2 * 58 + 2*(1-t)*t*96 + t*t*58
    points.append((round(x*scale), round(y*scale)))
draw.line(points, fill=blue, width=12*scale, joint='curve')
for x in (24, 232):
    draw.ellipse(((x-6)*scale, 52*scale, (x+6)*scale, 64*scale), fill=blue)
draw.rounded_rectangle((61*scale, 94*scale, 197*scale, 212*scale), radius=20*scale, fill='white', outline=blue, width=12*scale)
draw.polygon([(x*scale, y*scale) for x, y in [(82,185),(119,140),(143,167),(161,146),(183,185)]], fill=blue)
draw.ellipse((154*scale, 112*scale, 178*scale, 136*scale), fill=gold)
draw.rounded_rectangle((115*scale, 58*scale, 131*scale, 117*scale), radius=8*scale, fill=gold)
image.save(assets / 'Mark.png')
image.save(assets / 'win-tendedero.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print('Generated win-tendedero icon and mark from the clothesline design.')
