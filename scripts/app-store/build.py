#!/usr/bin/env python3
"""Build a portable, editable App Store campaign and render with Playwright."""
import base64
import json
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/app-store/en-US'
OUT.mkdir(parents=True, exist_ok=True)
(OUT / 'draft').mkdir(exist_ok=True)
for license_name in ('Instrument-OFL.txt', 'ShadowsIntoLight-OFL.txt'):
    (OUT / license_name).write_text('\n'.join(line.rstrip() for line in (ROOT / 'Assets/Resources/Tagtag/Fonts' / license_name).read_text().splitlines()) + '\n')

def data(path):
    p = ROOT / path
    mime = 'font/ttf' if p.suffix == '.ttf' else 'image/png'
    return f'data:{mime};base64,' + base64.b64encode(p.read_bytes()).decode()

slides = [
    dict(id='01-find-your-places', title='Find your<br>places.', sub='Little discoveries. Right around you.', screen='docs/verification/map-overlap/iphone.png', art='Assets/Resources/Tagtag/Navigation/explore.png', style='hero', note='Native map test capture. Replace test labels and capture current Explore with real pins.'),
    dict(id='02-follow-your-curiosity', title='A little curiosity<br>goes places.', sub='Follow a clue someone left for you.', screen='docs/verification/die-cut-ui/explore-teaser.png', art='Assets/Resources/Tagtag/Presets/taggi-1.png', style='clue', note='Cropped Unity teaser fixture; map-unavailable region excluded. Replace with current teaser capture.'),
    dict(id='03-uncover-a-story', title='Find a sticker.<br>Uncover a story.', sub='A small discovery. A personal connection.', screen='docs/verification/celebration/found.png', art='Assets/Resources/Tagtag/Navigation/stick.png', style='found', note='Unity celebration fixture temporarily represents discovery. Replace with real AR discovery capture.'),
    dict(id='04-collect-your-moments', title='Collect your<br>moments.', sub='Your places, kept in a sticker book.', screen='docs/app-store/captures/collected-current.png', art='Assets/Resources/Tagtag/Navigation/stick-book.png', style='book', note='Fresh 1170 × 2532 Unity capture of current Collected UI with 20 sample stickers.'),
    dict(id='05-make-it-yours', title='Make it<br>yours.', sub='Turn a photo into your next sticker.', screen='docs/app-store/captures/add-sticker-current.png', art='Assets/Resources/Tagtag/Home/make-sticker.png', style='make', note='Fresh 1170 × 2532 Unity capture of current Add Sticker UI; supported-device capabilities are fixture data.'),
    dict(id='06-leave-a-discovery', title='Leave a little<br>discovery.', sub='A place you love. A note worth finding.', screen='docs/verification/celebration/placed.png', art='Assets/Resources/Tagtag/Presets/taggi-2.png', style='leave', note='Unity publication celebration fixture. Replace with current on-device publication success.'),
]
font = 'Assets/Resources/Tagtag/Fonts/'
css = '''
*{box-sizing:border-box}body{margin:0;background:#e8e5df;color:#292823;font-family:Instrument,sans-serif}
@font-face{font-family:Hand;src:url(HAND)}@font-face{font-family:Instrument;src:url(BODY)}
:root{--paper:#fffef9;--ink:#292823;--yellow:#ffe45e}
header{padding:32px 40px;max-width:1400px;margin:auto}header h1{font-size:30px;margin:0 0 8px}header p{line-height:1.5;max-width:850px;margin:0;color:#55524a}
.gallery{display:grid;grid-template-columns:repeat(3,396px);gap:26px;justify-content:center;padding:20px 30px 50px}.tile{width:396px}.viewport{width:396px;height:860.4px;overflow:hidden;box-shadow:0 5px 20px #29282314}.tile .poster{transform:scale(.3);transform-origin:top left}.caption{font-size:14px;line-height:1.4;margin-top:12px;color:#55524a}
.poster{width:1320px;height:2868px;position:relative;overflow:hidden;background:var(--paper)}
.copy{position:absolute;top:150px;left:105px;right:90px;z-index:3}h2{font-family:Hand;font-size:168px;line-height:1.04;font-weight:400;letter-spacing:-3px;margin:0 0 42px}.sub{font-size:40px;line-height:1.4;letter-spacing:-.5px;margin:0;max-width:1060px}
.phone{position:absolute;left:220px;top:815px;width:880px;padding:13px;background:#2c2b27;border-radius:83px;box-shadow:0 30px 65px #36302026;z-index:2;overflow:hidden}.screen{border-radius:70px;overflow:hidden;background:#fffefa}.screen img{width:100%;height:auto;display:block}.art{position:absolute;width:280px;z-index:4;filter:drop-shadow(0 10px 6px #35302620);object-fit:contain}.brand{position:absolute;bottom:68px;left:105px;right:105px;display:flex;align-items:center;justify-content:space-between;z-index:5}.wordmark{font-family:Hand;font-size:65px}.tagline{font-size:25px;color:#5e5a50}.stroke{width:265px;height:19px;background:var(--yellow);position:absolute;left:100px;top:520px;transform:rotate(-2deg);border-radius:65% 30% 55% 30%}
.hero .phone{left:290px;top:780px;width:825px;transform:rotate(3deg)}.hero .art{left:55px;top:2100px;width:365px;transform:rotate(-10deg)}.hero .stroke{top:504px;width:360px}
.clue{background:#f6f2e8}.clue h2{font-size:145px}.clue .phone{top:1525px;left:108px;width:1104px;border-radius:35px;padding:0;background:transparent;box-shadow:0 24px 58px #36302020}.clue .screen{height:780px;border-radius:35px;position:relative}.clue .screen img{position:absolute;top:-1330px}.clue .art{width:580px;top:760px;left:355px;transform:rotate(-8deg)}.clue .stroke{top:458px;width:455px}.clue .excerpt{position:absolute;top:2380px;left:110px;font-size:30px;color:#686258}
.found .phone{top:840px;left:230px;width:860px}.found h2{font-size:141px}.found .art{width:225px;top:2250px;right:25px;transform:rotate(12deg)}.found .stroke{top:449px;width:550px}
.book{background:#f4f0e5}.book .phone{top:785px;left:225px;width:850px;transform:rotate(-3deg)}.book .art{width:290px;top:2240px;right:18px;transform:rotate(10deg)}.book .stroke{top:508px;width:540px}
.make .phone{top:795px;left:295px;width:835px;transform:rotate(2deg)}.make .art{width:355px;left:28px;top:2140px;transform:rotate(-8deg)}.make .stroke{top:510px;width:290px}
.leave{background:#f6f2e8}.leave h2{font-size:150px}.leave .phone{top:805px;left:220px;width:880px}.leave .art{width:235px;right:22px;top:2260px;transform:rotate(9deg)}.leave .stroke{top:465px;width:530px}
.single header,.single .caption{display:none}.single .gallery{display:block;padding:0}.single .tile{display:none}.single .tile.active{display:block;width:1320px}.single .viewport{width:1320px;height:2868px;box-shadow:none}.single .poster{transform:none}
@media(max-width:1250px){.gallery{grid-template-columns:repeat(2,396px)}}@media(max-width:850px){.gallery{grid-template-columns:396px;padding:10px}header{padding:25px}}
'''.replace('HAND',data(font+'ShadowsIntoLight.ttf')).replace('BODY',data(font+'InstrumentRegular.ttf'))
parts=[]
for s in slides:
    extra='<div class="excerpt">A clue from the app · enlarged detail</div>' if s['style']=='clue' else ''
    parts.append(f'''<section class="tile" id="{s['id']}"><div class="viewport"><article class="poster {s['style']}"><div class="stroke"></div><div class="copy"><h2>{s['title']}</h2><p class="sub">{s['sub']}</p></div><div class="phone"><div class="screen"><img src="{data(s['screen'])}" alt="Existing tagtag {s['style']} capture"></div></div><img class="art" src="{data(s['art'])}" alt="Taggi">{extra}<div class="brand"><span class="wordmark">tagtag</span><span class="tagline">Little discoveries worth keeping.</span></div></article></div><p class="caption"><b>DRAFT · {s['id']}</b><br>{s['note']}</p></section>''')
html='<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>tagtag · App Store screenshot studio</title><style>'+css+'</style><header><h1>tagtag — Little discoveries worth keeping.</h1><p>English iPhone campaign · Six editable layouts · 1320 × 2868 px<br>Review drafts: existing test captures illustrate the layouts. Read the capture notes below each image before preparing an App Store upload.</p></header><main class="gallery">'+''.join(parts)+'</main><script>const selected=new URLSearchParams(location.search).get("slide");if(selected){document.body.classList.add("single");document.getElementById(selected)?.classList.add("active")}</script></html>'
(OUT/'index.html').write_text(html)
(OUT/'manifest.json').write_text(json.dumps({'status':'review-draft','width':1320,'height':2868,'slides':slides},indent=2)+'\n')
with sync_playwright() as p:
    browser=p.chromium.launch()
    page=browser.new_page(viewport={'width':1320,'height':2868},device_scale_factor=1)
    for s in slides:
        page.goto((OUT/'index.html').as_uri()+'?slide='+s['id'])
        page.evaluate('document.fonts.ready')
        page.locator('img').evaluate_all('(imgs)=>Promise.all(imgs.map(i=>i.decode()))')
        page.screenshot(path=str(OUT/'draft'/f"{s['id']}.png"))
        print('Rendered',s['id'])
    page.set_viewport_size({'width':1320,'height':2100})
    page.goto((OUT/'index.html').as_uri())
    page.evaluate('document.fonts.ready')
    page.locator('img').evaluate_all('(imgs)=>Promise.all(imgs.map(i=>i.decode()))')
    page.screenshot(path=str(OUT/'contact-sheet.png'),full_page=True)
    browser.close()
print('Wrote editable HTML, manifest, six PNGs, and contact sheet.')
