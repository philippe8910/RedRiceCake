"""重新生成選關畫面上的兩顆月餅圖示。

用法：python Tools/GenerateMenuIcons.py（需要 Pillow）

沿用 Icon_Settings.png 的畫法（深色粗描邊 + 平塗，配色直接從既有圖示取樣），
但改成先畫 4 倍再縮小，邊緣才不會是鋸齒。

  Icon_ChineseMooncake    金黃色、蓮花壓模的傳統中式月餅（俯視）
  Icon_IndonesianMooncake 米白酥皮 + 紅色印章的印尼月餅（對應「蓋上印章」那一步）
"""
import math
import os

from PIL import Image, ImageDraw

S = 4                      # 超取樣倍率
N = 256                    # 輸出尺寸
W = N * S

OUTLINE = (92, 58, 28, 255)     # #5C3A1C
GOLD = (232, 178, 74, 255)      # #E8B24A
GOLD_DEEP = (206, 148, 56, 255)
CREAM = (247, 226, 178, 255)    # #F7E2B2
CREAM_DEEP = (228, 201, 148, 255)
RED = (199, 69, 47, 255)        # 印章
RED_DEEP = (168, 52, 36, 255)

DEST = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    'Assets', 'Textures', 'MenuIcons')

C = W / 2.0


def poly(draw, pts, fill):
    draw.polygon(pts, fill=fill)


def scallop(cx, cy, radius, lobes, depth, steps=1440, phase=0.0):
    """半徑會起伏的圓——月餅模具的荷葉邊。"""
    pts = []
    for i in range(steps):
        a = 2 * math.pi * i / steps
        r = radius * (1.0 + depth * math.cos(lobes * a + phase))
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def circle(draw, cx, cy, r, fill):
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)


def petal(cx, cy, r_in, r_out, angle, width, steps=48):
    """從中心往外指的圓頭花瓣。"""
    half = width / 2.0
    pts = []
    # 外側圓頭
    for i in range(steps + 1):
        t = math.pi * (i / steps) - math.pi / 2
        pts.append((r_out - half + half * math.cos(t), half * math.sin(t)))
    # 內側圓頭
    for i in range(steps + 1):
        t = math.pi * (i / steps) + math.pi / 2
        pts.append((r_in + half + half * math.cos(t), half * math.sin(t)))
    ca, sa = math.cos(angle), math.sin(angle)
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]


# --------------------------------------------------------------------------
# 中式月餅：蓮花壓模、荷葉邊
# --------------------------------------------------------------------------

def chinese():
    im = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    R = W * 0.455
    t = W * 0.030          # 描邊粗細

    # 壓模的荷葉邊：淺而密的凹槽，才不會看起來像餅乾花
    poly(d, scallop(C, C, R, 22, 0.016), OUTLINE)
    poly(d, scallop(C, C, R - t, 22, 0.016), GOLD)

    # 內圈：壓模的中央區塊。整顆維持金色，靠描邊與深一階的金做出「壓出來」的感覺，
    # 用米白填花瓣會變成一朵雛菊，反而不像月餅
    r_ring = R * 0.74
    circle(d, C, C, r_ring, OUTLINE)
    circle(d, C, C, r_ring - t, GOLD)

    # 六片蓮花瓣
    r_in = R * 0.17
    r_out = R * 0.585
    wide = R * 0.265
    for k in range(6):
        a = 2 * math.pi * k / 6 - math.pi / 2
        poly(d, petal(C, C, r_in - t * 0.5, r_out + t * 0.5, a, wide + t), OUTLINE)
    for k in range(6):
        a = 2 * math.pi * k / 6 - math.pi / 2
        poly(d, petal(C, C, r_in, r_out, a, wide), GOLD_DEEP)

    # 花心
    circle(d, C, C, R * 0.185, OUTLINE)
    circle(d, C, C, R * 0.185 - t, GOLD_DEEP)
    circle(d, C, C, R * 0.075, OUTLINE)

    return im


# --------------------------------------------------------------------------
# 印尼月餅 Tiong Ciu Pia：米白酥皮 + 紅色印章
# --------------------------------------------------------------------------

def indonesian():
    im = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    R = W * 0.455
    t = W * 0.030

    # 圓餅本體
    circle(d, C, C, R, OUTLINE)
    circle(d, C, C, R - t, CREAM)

    # 酥皮層次：外圈一道淺影 + 一圈斷開的短弧
    circle(d, C, C, R - t, CREAM_DEEP)
    circle(d, C, C, R - t * 2.2, CREAM)

    r_arc = R * 0.76
    box = [C - r_arc, C - r_arc, C + r_arc, C + r_arc]
    for k in range(8):
        a0 = 45 * k + 9
        d.arc(box, a0, a0 + 27, fill=CREAM_DEEP, width=int(t * 1.3))

    # 紅色印章（對應「蓋上印章」那一步）
    rs = R * 0.50
    circle(d, C, C, rs + t, OUTLINE)
    circle(d, C, C, rs, RED)
    circle(d, C, C, rs - t * 1.5, RED_DEEP)
    circle(d, C, C, rs - t * 2.4, RED)

    # 印章裡的八瓣花
    rosette = []
    for i in range(48):
        a = 2 * math.pi * i / 48 - math.pi / 2
        r = rs * (0.50 + 0.20 * math.cos(8 * a))
        rosette.append((C + r * math.cos(a), C + r * math.sin(a)))
    poly(d, rosette, CREAM)
    circle(d, C, C, rs * 0.17, RED_DEEP)

    return im


def save(im, name):
    im = im.resize((N, N), Image.LANCZOS)
    path = os.path.join(DEST, name + '.png')
    im.save(path)
    print('wrote', path)


if __name__ == '__main__':
    save(chinese(), 'Icon_ChineseMooncake')
    save(indonesian(), 'Icon_IndonesianMooncake')
