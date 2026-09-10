using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 把選關畫面上兩顆「進入關卡」的按鈕換成中秋喜慶的朱紅底。
///
/// 原本的 UI_Button.png 是棕底 + 金邊，在頭盔裡整顆偏黃。
/// 換成同一張圖只改底色的 UI_Button_Red.png（朱紅 #C7452F + 原本的金邊），
/// 九宮格邊界與圓角完全一樣，版面不會跑掉。
///
/// 設定按鈕刻意留原本的棕色：兩顆主要動作是紅的、次要動作是棕的，
/// 一眼就分得出來要先按哪個。
/// </summary>
public static class MooncakeMenuTheme
{
    const string k_ScenePath = "Assets/Scenes/MooncakeMainMenu.unity";
    const string k_RedSprite = "Assets/Textures/MenuIcons/UI_Button_Red.png";

    static readonly string[] k_LevelButtons = { "Button_Chinese", "Button_Indonesian" };

    // 紅底配白字；原本的深棕字 (0.13, 0.07, 0.03) 壓在紅底上會看不清楚
    static readonly Color k_LabelColor = new Color(1f, 0.96f, 0.90f, 1f);

    // 底色換成紅色之後，原本米黃色的 hover tint 會把紅色洗掉。
    // 改成「平常稍暗、指到才全亮」，這樣不管底圖什麼顏色都讀得出來。
    static readonly Color k_Normal = new Color(0.93f, 0.93f, 0.93f, 1f);
    static readonly Color k_Highlighted = Color.white;
    static readonly Color k_Pressed = new Color(0.75f, 0.72f, 0.70f, 1f);
    static readonly Color k_Selected = Color.white;

    [MenuItem("Tools/月餅 Demo/套用 選關按鈕配色（朱紅）")]
    public static void ApplyMenu()
    {
        Apply(true);
    }

    public static bool Apply(bool saveScene)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(k_RedSprite);
        if (sprite == null)
        {
            Debug.LogError($"[選關配色] 載不到 {k_RedSprite}，" +
                           "先跑 python Tools/GenerateButtonSprite.py 產生底圖");
            return false;
        }

        var scene = SceneManager.GetActiveScene().path == k_ScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        int done = 0;
        foreach (var name in k_LevelButtons)
        {
            var go = Find(scene, name);
            if (go == null)
            {
                Debug.LogError($"[選關配色] 場景裡找不到「{name}」");
                continue;
            }

            var image = go.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                EditorUtility.SetDirty(image);
            }

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                var colors = button.colors;
                colors.normalColor = k_Normal;
                colors.highlightedColor = k_Highlighted;
                colors.pressedColor = k_Pressed;
                colors.selectedColor = k_Selected;
                button.colors = colors;
                EditorUtility.SetDirty(button);
            }

            // 按鈕上的字：紅底要配淺色
            foreach (var label in go.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = k_LabelColor;
                EditorUtility.SetDirty(label);
            }

            done++;
        }

        if (done == 0) return false;

        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene) EditorSceneManager.SaveScene(scene);

        Debug.Log($"[選關配色] {done} 顆選關按鈕已換成朱紅底 + 金邊，字改成米白。" +
                  "設定按鈕維持原本的棕色。");
        return true;
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == name) return tr.gameObject;
            }
        }
        return null;
    }
}
