"""產生選關按鈕用的朱紅底圖 UI_Button_Red.png。

用法：python Tools/GenerateButtonSprite.py（需要 Pillow）

不是重畫一張，而是把既有的 UI_Button.png「換底色」：
原圖只有兩個色（棕色填色 + 金色描邊），中間的像素是這兩色的混合。
逐像素解出混合比例 t，再用同一個 t 把填色換成朱紅重組回去 ——
這樣圓角半徑、描邊粗細、去鋸齒邊緣、九宮格切圖全部原封不動，
只有底色變了。直接重畫的話這些細節對不齊，九宮格會歪。
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Assets', 'Textures', 'MenuIcons', 'UI_Button.png')
DST = os.path.join(ROOT, 'Assets', 'Textures', 'MenuIcons', 'UI_Button_Red.png')

FILL_OLD = (96, 58, 30)      # 原本的棕色填色
BORDER = (232, 178, 74)      # 金邊 #E8B24A，維持不變
FILL_NEW = (199, 69, 47)     # 朱紅 #C7452F


def blend_factor(px, a, b):
    """解 px = a*(1-t) + b*t 的最小平方解，回傳夾在 [0,1] 的 t。"""
    num = 0.0
    den = 0.0
    for i in range(3):
        d = b[i] - a[i]
        num += (px[i] - a[i]) * d
        den += d * d
    if den == 0:
        return 0.0
    t = num / den
    return max(0.0, min(1.0, t))


def main():
    im = Image.open(SRC).convert('RGBA')
    w, h = im.size
    out = Image.new('RGBA', (w, h))

    src = im.load()
    dst = out.load()

    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a == 0:
                dst[x, y] = (0, 0, 0, 0)
                continue

            t = blend_factor((r, g, b), FILL_OLD, BORDER)
            new = tuple(
                int(round(FILL_NEW[i] * (1.0 - t) + BORDER[i] * t))
                for i in range(3)
            )
            dst[x, y] = (new[0], new[1], new[2], a)

    out.save(DST)
    print('wrote', DST, out.size)


if __name__ == '__main__':
    main()
