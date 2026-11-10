#!/usr/bin/env python3
"""Compose the README banner: the official dotnet-bot (from the .NET MAUI template, MIT) conducting a choir of
generic AI-assistant characters singing in unison. Pure SVG, no text, no third-party logos.

Usage: scripts/make-banner.py docs/images/src/dotnet_bot.svg docs/images/unisono-choir.svg
Render to PNG on macOS: qlmanage -t -s 1600 -o <dir> <svg>  (then crop the square thumbnail to 1600x720).
"""
import re, sys

bot_src, out = sys.argv[1], sys.argv[2]
bot = open(bot_src, encoding="utf-8").read()
bot_inner = re.sub(r"^.*?<svg[^>]*>", "", bot, count=1, flags=re.S).replace("</svg>", "").strip()

W, H = 1600, 720
FLOOR = 640

def face(cx, cy, scale=1.0, eye_dx=22, mouth_dy=30):
    s = scale
    return f'''
    <g>
      <path d="M{cx-eye_dx*s-9*s},{cy} q{9*s},{-11*s} {18*s},0" stroke="#1f1a3d" stroke-width="{5*s}" fill="none" stroke-linecap="round"/>
      <path d="M{cx+eye_dx*s-9*s},{cy} q{9*s},{-11*s} {18*s},0" stroke="#1f1a3d" stroke-width="{5*s}" fill="none" stroke-linecap="round"/>
      <ellipse cx="{cx}" cy="{cy+mouth_dy*s}" rx="{13*s}" ry="{16*s}" fill="#1f1a3d"/>
      <ellipse cx="{cx}" cy="{cy+mouth_dy*s+8*s}" rx="{7*s}" ry="{6*s}" fill="#ff7a8a"/>
      <circle cx="{cx-eye_dx*s-16*s}" cy="{cy+14*s}" r="{6*s}" fill="#ff9aa2" opacity="0.6"/>
      <circle cx="{cx+eye_dx*s+16*s}" cy="{cy+14*s}" r="{6*s}" fill="#ff9aa2" opacity="0.6"/>
    </g>'''

def note(x, y, color, flip=False, scale=1.0):
    scale = scale * 1.5
    return f'<use href="#u-note{"2" if flip else ""}" transform="translate({x},{y}) scale({scale})" fill="{color}"/>'

def sparkle(x, y, r, color, op=0.9):
    return f'<path d="M{x},{y-r} Q{x},{y} {x+r},{y} Q{x},{y} {x},{y+r} Q{x},{y} {x-r},{y} Q{x},{y} {x},{y-r} Z" fill="{color}" opacity="{op}"/>'

parts = []
# 1. Orb (left outer)
cx = 200; cy = FLOOR - 90
parts.append(f'''
  <g>
    <circle cx="{cx}" cy="{cy}" r="96" fill="#ffffff" opacity="0.12"/>
    <circle cx="{cx}" cy="{cy}" r="76" fill="url(#u-orb)"/>
    {face(cx, cy-4, 1.1)}
    <path d="M{cx-100},{cy+10} q-14,10 -8,26" stroke="#ffffff" stroke-width="4" fill="none" opacity="0.5" stroke-linecap="round"/>
    <path d="M{cx+100},{cy+10} q14,10 8,26" stroke="#ffffff" stroke-width="4" fill="none" opacity="0.5" stroke-linecap="round"/>
  </g>''')
# 2. Speech bubble (left middle)
cx = 380; top = FLOOR - 190
parts.append(f'''
  <g>
    <rect x="{cx-80}" y="{top}" width="160" height="130" rx="34" fill="#ff7a59"/>
    <path d="M{cx-50},{top+128} l-18,34 l40,-32 z" fill="#ff7a59"/>
    <rect x="{cx-80}" y="{top}" width="160" height="22" rx="11" fill="#ffffff" opacity="0.18"/>
    {face(cx, top+58, 1.0)}
  </g>''')
# 3. Smart speaker (left inner)
cx = 560; top = FLOOR - 200
parts.append(f'''
  <g>
    <rect x="{cx-56}" y="{top}" width="112" height="200" rx="40" fill="#4cd4c0"/>
    <ellipse cx="{cx}" cy="{top+4}" rx="56" ry="14" fill="#9af0e3"/>
    <ellipse cx="{cx}" cy="{top+4}" rx="40" ry="8" fill="#1f1a3d" opacity="0.35"/>
    {"".join(f'<circle cx="{cx-30+i*15}" cy="{top+150+j*14}" r="3" fill="#1f1a3d" opacity="0.25"/>' for i in range(5) for j in range(3))}
    {face(cx, top+72, 0.95)}
  </g>''')
# 4. Sparkle (right inner)
cx = 1040; cy = FLOOR - 120
parts.append(f'''
  <g>
    <path d="M{cx},{cy-110} Q{cx+14},{cy-14} {cx+110},{cy} Q{cx+14},{cy+14} {cx},{cy+110} Q{cx-14},{cy+14} {cx-110},{cy} Q{cx-14},{cy-14} {cx},{cy-110} Z" fill="#ffc857"/>
    <path d="M{cx},{cy-70} Q{cx+8},{cy-8} {cx+70},{cy} Q{cx+8},{cy+8} {cx},{cy+70} Q{cx-8},{cy+8} {cx-70},{cy} Q{cx-8},{cy-8} {cx},{cy-70} Z" fill="#ffd97a" opacity="0.7"/>
    {face(cx, cy-6, 0.9, eye_dx=18, mouth_dy=26)}
  </g>''')
# 5. Chat window (right middle)
cx = 1230; top = FLOOR - 180
parts.append(f'''
  <g>
    <rect x="{cx-90}" y="{top}" width="180" height="140" rx="22" fill="#b39cff"/>
    <rect x="{cx-90}" y="{top}" width="180" height="30" rx="22" fill="#8a6fe8"/>
    <rect x="{cx-90}" y="{top+16}" width="180" height="14" fill="#8a6fe8"/>
    <circle cx="{cx-68}" cy="{top+15}" r="5" fill="#ff7a8a"/><circle cx="{cx-52}" cy="{top+15}" r="5" fill="#ffc857"/><circle cx="{cx-36}" cy="{top+15}" r="5" fill="#4cd4c0"/>
    <rect x="{cx-66}" y="{top+112}" width="80" height="10" rx="5" fill="#ffffff" opacity="0.45"/>
    {face(cx, top+68, 1.0)}
  </g>''')
# 6. Microphone (right outer)
cx = 1410; top = FLOOR - 230
parts.append(f'''
  <g>
    <rect x="{cx-6}" y="{top+150}" width="12" height="60" fill="#cfd3e6"/>
    <rect x="{cx-50}" y="{FLOOR-28}" width="100" height="18" rx="9" fill="#cfd3e6"/>
    <path d="M{cx-72},{top+100} a72,72 0 0 0 144,0" stroke="#cfd3e6" stroke-width="10" fill="none" stroke-linecap="round"/>
    <rect x="{cx-50}" y="{top}" width="100" height="150" rx="50" fill="#f1f2fa"/>
    {"".join(f'<line x1="{cx-40}" y1="{top+20+i*14}" x2="{cx+40}" y2="{top+20+i*14}" stroke="#aeb3cf" stroke-width="3"/>' for i in range(3))}
    {face(cx, top+88, 0.95, eye_dx=18, mouth_dy=26)}
  </g>''')

notes = [note(150, 340, "#ff7a59"), note(330, 300, "#ffc857", True, 0.8), note(440, 250, "#4cd4c0", False, 0.7),
         note(640, 330, "#ffffff", True, 0.8), note(1010, 300, "#ff7a59", False, 0.8), note(1130, 240, "#b39cff", True, 0.7),
         note(1330, 330, "#ffc857"), note(1470, 270, "#4cd4c0", True, 0.8), note(760, 230, "#ffd97a", False, 0.6), note(880, 200, "#ffffff", True, 0.6)]
dots = [sparkle(120, 120, 14, "#ffc857"), sparkle(1480, 110, 18, "#ffc857"), sparkle(690, 110, 10, "#ffffff", 0.7),
        sparkle(960, 140, 12, "#ffffff", 0.6), sparkle(300, 190, 7, "#ffffff", 0.5), sparkle(1300, 170, 8, "#ffffff", 0.5)]

bot_scale = 0.62
bot_x, bot_y = 800 - 419 * bot_scale / 2, 556 - 519 * bot_scale   # feet on the podium top
hand_x, hand_y = bot_x + 405 * bot_scale, bot_y + 132 * bot_scale

svg = f'''<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img" aria-label="The .NET bot conducting a choir of AI assistants singing in unison">
  <defs>
    <linearGradient id="u-bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#17103a"/><stop offset="1" stop-color="#2d1f6b"/></linearGradient>
    <radialGradient id="u-glow" cx="0.5" cy="0.8" r="0.6"><stop offset="0" stop-color="#8a6fe8" stop-opacity="0.45"/><stop offset="1" stop-color="#8a6fe8" stop-opacity="0"/></radialGradient>
    <radialGradient id="u-orb" cx="0.4" cy="0.35" r="0.75"><stop offset="0" stop-color="#ffffff"/><stop offset="1" stop-color="#cfd3f5"/></radialGradient>
    <linearGradient id="u-floor" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#4a3796"/><stop offset="1" stop-color="#2a1c66"/></linearGradient>
    <g id="u-note">
      <ellipse cx="12" cy="50" rx="12" ry="8" transform="rotate(-20 12 50)"/><rect x="20" y="6" width="5" height="44"/><path d="M25,6 q18,10 8,30 q4,-14 -8,-18 z"/>
    </g>
    <g id="u-note2">
      <ellipse cx="10" cy="52" rx="10" ry="7" transform="rotate(-20 10 52)"/><ellipse cx="42" cy="46" rx="10" ry="7" transform="rotate(-20 42 46)"/><rect x="17" y="10" width="4" height="42"/><rect x="49" y="4" width="4" height="42"/><path d="M17,10 L53,4 L53,14 L17,20 z"/>
    </g>
  </defs>
  <rect width="{W}" height="{H}" fill="url(#u-bg)"/>
  <rect width="{W}" height="{H}" fill="url(#u-glow)"/>
  <ellipse cx="800" cy="{FLOOR+14}" rx="820" ry="72" fill="url(#u-floor)"/>
  <ellipse cx="800" cy="{FLOOR+8}" rx="600" ry="40" fill="#ffffff" opacity="0.06"/>
  {"".join(dots)}
  {"".join(notes)}
  {"".join(parts)}
  <!-- podium -->
  <rect x="700" y="556" width="200" height="90" rx="10" fill="#8f5a2a"/>
  <ellipse cx="800" cy="556" rx="100" ry="22" fill="#c98b47"/>
  <ellipse cx="800" cy="556" rx="86" ry="16" fill="#b8793a"/>
  <!-- the official dotnet-bot, from the .NET MAUI project template (MIT) -->
  <g transform="translate({bot_x:.1f},{bot_y:.1f}) scale({bot_scale})">
    {bot_inner}
  </g>
  <!-- baton in the raised hand -->
  <line x1="{hand_x-6:.0f}" y1="{hand_y+6:.0f}" x2="{hand_x+110:.0f}" y2="{hand_y-78:.0f}" stroke="#f4e7d3" stroke-width="7" stroke-linecap="round"/>
  <circle cx="{hand_x+110:.0f}" cy="{hand_y-78:.0f}" r="7" fill="#ffffff"/>
</svg>
'''
open(out, "w", encoding="utf-8").write(svg)
print(f"wrote {out} ({len(svg)//1024} KB)")
