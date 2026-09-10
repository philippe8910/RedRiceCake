"""產生 Android app 圖示。

用法：python Tools/GenerateAppIcon.py（需要 Pillow）

月餅本體直接借 GenerateMenuIcons.chinese()，不重畫一份 ——
選單圖示改配色時這裡會跟著變，不會兩邊走鐘。

輸出三張（Assets/Textures/AppIcon/）：
  AppIcon             朱紅底 + 金月餅，給 Legacy／Round 用
  AppIcon_Background  純朱紅，adaptive 的背景層
  AppIcon_Foreground  透明底 + 金月餅，adaptive 的前景層

adaptive 前景要縮在畫布中央 ~62%：Android 會依機型把圖示裁成圓形／
方圓形／水滴形，外圈那一整圈隨時可能被切掉，只有中間 66% 保證看得到。
Legacy 不會被裁那麼兇，所以放大到 72% 讓圖示不會顯得空。
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import GenerateMenuIcons as art  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEST = os.path.join(ROOT, 'Assets', 'Textures', 'AppIcon')

N = 512                     # 輸出邊長
S = 4                       # 超取樣倍率
W = N * S

RED = (199, 69, 47, 255)    # 朱紅 #C7452F，跟選關按鈕同一個底色

# chinese() 畫出來的月餅直徑大約佔畫布的 91%（R = W * 0.455）
ART_COVERAGE = 0.91


def mooncake(canvas, coverage):
    """把月餅畫在 canvas 見方的透明圖上，直徑佔畫布的 coverage。"""
    art.W = canvas
    art.C = canvas / 2.0
    im = art.chinese()

    target = int(round(canvas * coverage / ART_COVERAGE))
    im = im.resize((target, target), Image.LANCZOS)

    out = Image.new('RGBA', (canvas, canvas), (0, 0, 0, 0))
    off = (canvas - target) // 2
    out.paste(im, (off, off), im)
    return out


def save(im, name):
    im = im.resize((N, N), Image.LANCZOS)
    path = os.path.join(DEST, name + '.png')
    im.save(path)
    print('wrote', path, im.size)


def main():
    os.makedirs(DEST, exist_ok=True)

    # adaptive 背景：整片朱紅，不能有透明區
    save(Image.new('RGBA', (W, W), RED), 'AppIcon_Background')

    # adaptive 前景：透明底，月餅縮在安全範圍內
    save(mooncake(W, 0.62), 'AppIcon_Foreground')

    # Legacy / Round：紅底合成
    legacy = Image.new('RGBA', (W, W), RED)
    cake = mooncake(W, 0.72)
    legacy.paste(cake, (0, 0), cake)
    save(legacy, 'AppIcon')


if __name__ == '__main__':
    main()
