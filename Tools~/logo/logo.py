import math, cairosvg
from PIL import Image

def rays(cx, cy, r_in, n, long_r, short_r, w_deg):
    out = []
    for i in range(n):
        a = math.radians(i * 360 / n - 90)
        R = long_r if i % 2 == 0 else short_r
        hw = math.radians(w_deg if i % 2 == 0 else w_deg * 0.75)
        p1 = (cx + r_in * math.cos(a - hw), cy + r_in * math.sin(a - hw))
        p2 = (cx + R * math.cos(a), cy + R * math.sin(a))
        p3 = (cx + r_in * math.cos(a + hw), cy + r_in * math.sin(a + hw))
        out.append(f'<path d="M{p1[0]:.1f},{p1[1]:.1f} L{p2[0]:.1f},{p2[1]:.1f} L{p3[0]:.1f},{p3[1]:.1f} Z"/>')
    return "\n".join(out)

def notches(cx, cy, r, n, size):
    out = []
    for i in range(n):
        a = math.radians(i * 360 / n - 90)
        x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        d = math.degrees(a) + 90
        L, W = size * 0.75, size * 0.42
        out.append(f'<path d="M{x:.1f},{y-L:.1f} L{x+W:.1f},{y:.1f} L{x:.1f},{y+L:.1f} L{x-W:.1f},{y:.1f} Z" transform="rotate({d:.1f} {x:.1f} {y:.1f})"/>')
    return "\n".join(out)

def weapons(cx, cy, s):
    # Turkic sabre (tip to upper right) and recurve bow (other diagonal), drawn pointing up then rotated
    sword = f'''
    <g transform="translate({cx} {cy}) rotate(45) scale({s}) translate(0 48)">
      <path d="M-13,118 C-12,40 -4,-110 22,-262 C32,-160 28,-40 13,118 Z" fill="url(#blade)" stroke="#3B4458" stroke-width="2"/>
      <path d="M5,110 C8,30 13,-110 20,-230" fill="none" stroke="#FFFFFF" stroke-width="2" opacity="0.55"/>
      <rect x="-40" y="116" width="80" height="13" rx="6" fill="url(#gold)" stroke="#5E3A0B" stroke-width="2"/>
      <rect x="-8" y="129" width="16" height="64" rx="4" fill="#4A2A17" stroke="#2A160B" stroke-width="2"/>
      <path d="M-8,142 L8,148 M-8,158 L8,164 M-8,174 L8,180" stroke="#E9B44C" stroke-width="3"/>
      <circle cx="0" cy="203" r="12" fill="url(#gold)" stroke="#5E3A0B" stroke-width="2"/>
    </g>'''
    bow = f'''
    <g transform="translate({cx} {cy}) rotate(-45) scale({s})">
      <path d="M0,-236 C-40,-200 -70,-120 -62,0 C-70,120 -40,200 0,236" fill="none" stroke="#5A3418" stroke-width="13" stroke-linecap="round"/>
      <path d="M0,-236 C-40,-200 -70,-120 -62,0 C-70,120 -40,200 0,236" fill="none" stroke="#9A6233" stroke-width="5" stroke-linecap="round"/>
      <path d="M0,-236 C14,-246 26,-246 30,-236 M0,236 C14,246 26,246 30,236" fill="none" stroke="url(#gold)" stroke-width="8" stroke-linecap="round"/>
      <path d="M12,-238 L12,238" stroke="#E8E2D0" stroke-width="2.5"/>
    </g>'''
    return bow + sword

def emblem(cx=256, cy=256, R=170):
    # medallion: dawn sky, sun with rays, three mountains with snow caps, gold ring with eight diamonds
    s = R / 170.0
    def X(v): return cx + (v - 256) * s
    def Y(v): return cy + (v - 256) * s
    sun_cx, sun_cy = X(256), Y(236)
    m = []
    # mountains (back, then front)
    back = f'M{X(60)},{Y(330)} L{X(150)},{Y(250)} L{X(205)},{Y(292)} L{X(256)},{Y(205)} L{X(310)},{Y(292)} L{X(365)},{Y(245)} L{X(452)},{Y(330)} L{X(452)},{Y(452)} L{X(60)},{Y(452)} Z'
    front = f'M{X(60)},{Y(372)} L{X(140)},{Y(300)} L{X(200)},{Y(350)} L{X(256)},{Y(296)} L{X(318)},{Y(352)} L{X(378)},{Y(298)} L{X(452)},{Y(368)} L{X(452)},{Y(452)} L{X(60)},{Y(452)} Z'
    snow = (f'M{X(256)},{Y(205)} L{X(282)},{Y(250)} L{X(268)},{Y(244)} L{X(256)},{Y(258)} L{X(243)},{Y(242)} L{X(231)},{Y(248)} Z '
            f'M{X(150)},{Y(250)} L{X(170)},{Y(267)} L{X(158)},{Y(265)} L{X(148)},{Y(274)} L{X(140)},{Y(262)} L{X(132)},{Y(266)} Z '
            f'M{X(365)},{Y(245)} L{X(384)},{Y(262)} L{X(373)},{Y(260)} L{X(364)},{Y(270)} L{X(356)},{Y(258)} L{X(347)},{Y(262)} Z')
    return f'''
  <clipPath id="med"><circle cx="{cx}" cy="{cy}" r="{R}"/></clipPath>
  <g clip-path="url(#med)">
    <rect x="{cx-R}" y="{cy-R}" width="{2*R}" height="{2*R}" fill="url(#sky)"/>
    <circle cx="{sun_cx}" cy="{sun_cy}" r="{120*s}" fill="url(#glow)"/>
    <g fill="#F7C553">{rays(sun_cx, sun_cy, 58*s, 16, 104*s, 84*s, 7)}</g>
    <circle cx="{sun_cx}" cy="{sun_cy}" r="{54*s}" fill="url(#sun)"/>
    <circle cx="{sun_cx}" cy="{sun_cy}" r="{40*s}" fill="none" stroke="#FFE7A3" stroke-width="{3*s}" opacity="0.7"/>
    <path d="{back}" fill="#2A2350"/>
    <path d="{snow}" fill="#E9E4F2"/>
    <path d="{front}" fill="#141B36"/>
  </g>
  <circle cx="{cx}" cy="{cy}" r="{R}" fill="none" stroke="url(#gold)" stroke-width="{14*s}"/>
  <circle cx="{cx}" cy="{cy}" r="{R-11*s}" fill="none" stroke="#8C5A14" stroke-width="{2*s}" opacity="0.8"/>
  <g fill="url(#gold)" stroke="#5E3A0B" stroke-width="{1.5*s}">{notches(cx, cy, R, 8, 22*s)}</g>
'''

DEFS = '''
<defs>
  <radialGradient id="bg" cx="50%" cy="45%" r="70%">
    <stop offset="0" stop-color="#2B2F6B"/><stop offset="0.6" stop-color="#151A3D"/><stop offset="1" stop-color="#0A0D22"/>
  </radialGradient>
  <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#1B1F4F"/><stop offset="0.45" stop-color="#7A3A63"/><stop offset="0.7" stop-color="#E0784A"/><stop offset="1" stop-color="#F3B05C"/>
  </linearGradient>
  <linearGradient id="blade" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#8E99AE"/><stop offset="0.5" stop-color="#F2F5FA"/><stop offset="1" stop-color="#9AA5B9"/></linearGradient>
  <radialGradient id="glow"><stop offset="0" stop-color="#FFD37A" stop-opacity="0.9"/><stop offset="1" stop-color="#FFB45A" stop-opacity="0"/></radialGradient>
  <radialGradient id="sun" cx="45%" cy="40%" r="65%"><stop offset="0" stop-color="#FFF1B8"/><stop offset="0.55" stop-color="#F8C04C"/><stop offset="1" stop-color="#E2861E"/></radialGradient>
  <linearGradient id="gold" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#FFE29A"/><stop offset="0.35" stop-color="#E9B44C"/><stop offset="0.7" stop-color="#B9791F"/><stop offset="1" stop-color="#F2CC74"/>
  </linearGradient>
</defs>'''

def svg(w, h, body, bg=True):
    back = f'<rect width="{w}" height="{h}" fill="url(#bg)"/>' if bg else ''
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{DEFS}{back}{body}</svg>'

def render(s, out, w, h):
    cairosvg.svg2png(bytestring=s.encode(), write_to=out, output_width=w, output_height=h)

# Play icon 512 and in-game/launcher legacy icon: same art
icon = svg(512, 512, weapons(256, 256, 1.0) + emblem(256, 256, 160))
open('simge.svg', 'w').write(icon)
render(icon, 'play_simge_512.png', 512, 512)
render(icon, 'simge_1024.png', 1024, 1024)
# adaptive: background layer (just bg) and foreground (emblem within the 66% safe zone of 432)
render(svg(432, 432, ''), 'uyarlanabilir_arka.png', 432, 432)
render(svg(432, 432, weapons(216, 216, 0.5625) + emblem(216, 216, 90), bg=False), 'uyarlanabilir_on.png', 432, 432)
render(svg(432, 432, weapons(216, 216, 0.5625) + emblem(216, 216, 90)), 'uyarlanabilir_birlesik.png', 432, 432)
im = Image.open('play_simge_512.png'); print(im.mode, im.size)

# round launcher icon (Android 7.1 round): same art masked to a circle; 32-bit PNG everywhere (Play wants alpha channel)
from PIL import ImageDraw
for ad in ['play_simge_512.png', 'simge_1024.png', 'uyarlanabilir_arka.png']:
    Image.open(ad).convert('RGBA').save(ad)
kare = Image.open('simge_1024.png').convert('RGBA')
maske = Image.new('L', kare.size, 0)
ImageDraw.Draw(maske).ellipse((0, 0, kare.size[0] - 1, kare.size[1] - 1), fill=255)
yuvarlak = Image.new('RGBA', kare.size, (0, 0, 0, 0)); yuvarlak.paste(kare, (0, 0), maske)
yuvarlak.save('simge_yuvarlak.png')
