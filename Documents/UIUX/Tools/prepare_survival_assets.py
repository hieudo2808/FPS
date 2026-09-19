"""Prepare the selected licensed source art; run before Unity imports the results."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageOps, ImageChops

ROOT = Path(__file__).resolve().parents[3]
SOURCES = ROOT / 'Documents/UIUX/FreeAssets'
OUTPUT = ROOT / 'Assets/FPS/Features/UI/Content/Sprites/Survival/Curated'
SUN = SOURCES / 'SunGraphica-Creepy-UI-FREE/Creepy Game UI FREE'
LOTUS = SOURCES / 'Lotus-Post-Apocalypse-UI'
manifest = []


def prepare(source, destination, treatment, border=(0, 0, 0, 0), crop=None, pack=None):
    image = Image.open(source).convert('RGBA')
    if crop:
        image = image.crop(crop)
    bounds = image.getchannel('A').getbbox()
    if bounds:
        image = image.crop(bounds)
    alpha = image.getchannel('A')
    luminance = ImageOps.grayscale(image)
    if treatment == 'surface':
        rgb = ImageOps.colorize(luminance, (18, 19, 20), (54, 51, 47))
        alpha = Image.new('L', image.size, 255)
    elif treatment == 'control':
        rgb = ImageOps.colorize(luminance, (175, 170, 164), (242, 239, 230))
        alpha = Image.new('L', image.size, 255)
    elif treatment == 'neutral':
        rgb = ImageOps.colorize(luminance, (115, 111, 104), (242, 238, 224))
    elif treatment == 'white':
        rgb = Image.new('RGB', image.size, 'white')
    elif treatment == 'icon':
        rgb = ImageOps.colorize(luminance, (85, 82, 75), (245, 240, 224))
    else:
        rgb = image.convert('RGB')
    rgb.putalpha(alpha)
    target = OUTPUT / (destination + '.png')
    target.parent.mkdir(parents=True, exist_ok=True)
    rgb.save(target)
    manifest.append({'asset': target.relative_to(ROOT).as_posix(),
                     'source': source.relative_to(ROOT).as_posix(),
                     'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                     'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                     'pack': pack, 'treatment': treatment, 'source_crop': crop,
                     'size': list(rgb.size), 'border': list(border)})


prepare(SUN / 'Items - Inventory 1/Bevel button 1.1.png', 'Surfaces/Panel', 'surface', (12, 12, 12, 12), pack='SunGraphica')
prepare(SUN / 'Items - Inventory 1/Bevel button 1.1.png', 'Controls/Control', 'control', (12, 12, 12, 12), pack='SunGraphica')
prepare(SUN / 'Menu/Inventory border 1.2_ copy 3.png', 'Controls/MenuFocus', 'control', (24, 16, 24, 16), pack='SunGraphica')
prepare(SUN / 'Menu/Inventory border 1.2_ copy.png', 'Surfaces/Distress', 'neutral', pack='SunGraphica')
prepare(LOTUS / 'health bar/health bar 1024x128/_0001_frame-bar.png', 'Bars/HealthFrame', 'neutral', (22, 10, 22, 10), pack='lotus_garden')
prepare(LOTUS / 'health bar/health bar 2 1024x128/_0005_fill-bar.png', 'Bars/HealthFill', 'neutral', pack='lotus_garden')
prepare(LOTUS / 'health bar/health bar 1024x128/_0006_background.png', 'Bars/HealthTrack', 'neutral', (8, 4, 8, 4), pack='lotus_garden')
for source, name in [('Check','Check'), ('Back','ArrowBack'), ('Setting','Settings'), ('Sound','Audio'), ('Gamepad','Controls'), ('Lock','Lock')]:
    prepare(SUN / ('Retro icons/Set 1/128 px/' + source + '.png'), 'Icons/' + name, 'white', pack='SunGraphica')

# Sprite rects were read through Unity AssetDatabase. Convert bottom-left coordinates
# to image coordinates, avoiding import of the entire original atlases.
def local_sprite(texture, rectangle, name, treatment):
    source = ROOT / texture
    height = Image.open(source).height
    x, y, width, size_y = rectangle
    prepare(source, 'Icons/' + name, treatment,
            crop=(x, height-y-size_y, x+width, height-y), pack='ProjectSettingsPack')

local_sprite('Assets/ThirdParty/Imported/ProjectSettingsPack/Texture2D/SpriteAtlasTexture-_C16_HUD (Group 1)-1024x1024-fmt12.png', (70,769,32,27), 'Health', 'white')
local_sprite('Assets/ThirdParty/UIKit/Textures/SpriteAtlasTexture-GameMap (Group 0)-2048x2048-fmt5.png', (1108,1747,166,166), 'Biohazard', 'white')
local_sprite('Assets/ThirdParty/Imported/ProjectSettingsPack/Texture2D/SpriteAtlasTexture-ChatIcons (Group 0)-256x512-fmt5.png', (61,68,57,32), 'Grenade', 'icon')
prepare(ROOT / 'Assets/ThirdParty/UIKit/Textures/SpriteAtlasTexture-BloodFX-1024x1024-fmt12.png', 'Effects/BloodEdge', 'white', crop=(0,653,916,786), pack='ProjectSettingsPack')

OUTPUT.mkdir(parents=True, exist_ok=True)
(SOURCES.parent / 'curated-assets.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(f'Prepared {len(manifest)} sprites, {sum((ROOT / m["asset"]).stat().st_size for m in manifest):,} bytes')
