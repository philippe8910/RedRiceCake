using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 驗證這批修改真的進去了。純讀取，不改任何東西。
/// 每一項都印出實際量到的值，不只印「OK」——數字對不對要看得見。
/// </summary>
public static class MooncakeVerifyChanges
{
    const string k_MenuScene = "Assets/Scenes/MooncakeMainMenu.unity";
    const string k_ChineseScene = "Assets/Scenes/ChineseMooncakeDemo.unity";
    const string k_IndonesianScene = "Assets/Scenes/IndonesianMooncakeDemo.unity";
    const string k_TutorialRoot = "月餅-教學";

    [MenuItem("Tools/月餅 Demo/驗證 這批修改")]
    public static void VerifyMenu()
    {
        Debug.Log(Run());
    }

    public static void VerifyFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);

        string outPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "mooncake-verify.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            if (a.StartsWith("verifyOut=")) outPath = a.Substring("verifyOut=".Length);
        }
        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));

        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[驗證] ==================================================");

        VerifyProjectSettings(sb);
        VerifyMenuButtons(sb);
        VerifyTutorial(sb, k_ChineseScene, MooncakeTutorial.Variant.中式模具);
        VerifyTutorial(sb, k_IndonesianScene, MooncakeTutorial.Variant.印尼印章);
        VerifyStampSize(sb);

        sb.AppendLine("[驗證] ==================================================");
        return sb.ToString();
    }

    // ------------------------------------------------------------------

    static void VerifyProjectSettings(StringBuilder sb)
    {
        sb.AppendLine("── 包名與圖示 ──");
        sb.AppendLine($"  applicationIdentifier = {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}");
        sb.AppendLine($"  productName           = {PlayerSettings.productName}");
        sb.AppendLine($"  companyName           = {PlayerSettings.companyName}");

        var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android);
        foreach (var kind in kinds)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            int set = 0;
            string sample = "";

            foreach (var icon in icons)
            {
                var textures = icon.GetTextures();
                bool any = false;
                foreach (var t in textures)
                {
                    if (t == null) continue;
                    any = true;
                    if (sample.Length == 0) sample = t.name;
                }
                if (any) set++;
            }

            sb.AppendLine($"  圖示 {kind,-10} {set}/{icons.Length} 個尺寸有貼圖" +
                          (sample.Length > 0 ? $"（例如 {sample}）" : ""));
        }
    }

    // ------------------------------------------------------------------

    static void VerifyMenuButtons(StringBuilder sb)
    {
        sb.AppendLine("── 選關按鈕配色 ──");
        var scene = EditorSceneManager.OpenScene(k_MenuScene, OpenSceneMode.Single);

        foreach (var name in new[] { "Button_Chinese", "Button_Indonesian", "Button_Settings" })
        {
            var go = Find(scene, name);
            if (go == null)
            {
                sb.AppendLine($"  {name}：找不到");
                continue;
            }

            var image = go.GetComponent<Image>();
            var button = go.GetComponent<Button>();
            string sprite = image != null && image.sprite != null ? image.sprite.name : "(無)";

            sb.AppendLine($"  {name,-20} sprite={sprite}");

            if (button != null)
            {
                var c = button.colors;
                sb.AppendLine($"      normal={Hex(c.normalColor)} highlighted={Hex(c.highlightedColor)} " +
                              $"pressed={Hex(c.pressedColor)}");
            }

            foreach (var label in go.GetComponentsInChildren<TMP_Text>(true))
                sb.AppendLine($"      字「{label.text}」color={Hex(label.color)}");
        }
    }

    // ------------------------------------------------------------------

    static void VerifyTutorial(StringBuilder sb, string scenePath, MooncakeTutorial.Variant expected)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        sb.AppendLine($"── 教學：{scene.name} ──");

        var go = Find(scene, k_TutorialRoot);
        if (go == null)
        {
            sb.AppendLine($"  找不到根物件「{k_TutorialRoot}」");
            return;
        }

        var tutorial = go.GetComponent<MooncakeTutorial>();
        if (tutorial == null)
        {
            sb.AppendLine("  根物件上沒有 MooncakeTutorial");
            return;
        }

        sb.AppendLine($"  variant = {tutorial.variant}" +
                      (tutorial.variant == expected ? "" : $"  ← 預期是 {expected}"));
        sb.AppendLine($"  面板    = {(tutorial.panel != null ? "有" : "缺")}" +
                      $"  箭頭範本 = {(tutorial.arrowTemplate != null ? "有" : "缺")}" +
                      $"  flow = {(tutorial.flow != null ? "有" : "缺")}");
        sb.AppendLine($"  文案    = {(tutorial.texts != null ? tutorial.texts.Count : 0)} 段");

        // 成形那一步的文字，兩個場景應該不一樣
        if (tutorial.texts != null)
        {
            foreach (var t in tutorial.texts)
            {
                if (t.step != MooncakeTutorial.Step.放進模具) continue;
                sb.AppendLine($"  成形步驟：「{t.title}」/「{t.body}」");
                sb.AppendLine($"  對應 term：{MooncakeTutorial.TitleTerm(t.step, MooncakeTutorial.DefaultTermPrefix, tutorial.variant)}");
                break;
            }
        }

        var flow = Object.FindObjectOfType<MooncakeChineseFlow>();
        if (flow != null) sb.AppendLine($"  bakesBeforeDone = {flow.bakesBeforeDone}");
    }

    // ------------------------------------------------------------------

    static void VerifyStampSize(StringBuilder sb)
    {
        sb.AppendLine("── 印章尺寸 ──");

        var importer = AssetImporter.GetAtPath("Assets/Model/MooncakeModel/Stamp.fbx") as ModelImporter;
        sb.AppendLine($"  Stamp.fbx globalScale = {(importer != null ? importer.globalScale.ToString() : "?")}");

        var scene = EditorSceneManager.OpenScene(k_IndonesianScene, OpenSceneMode.Single);

        // 場景裡「月餅-完成體」不只一顆（烤盤格子上也有），一定要限定在
        // 印章握把底下找，否則量到的是別顆，數字會看起來像縮水了。
        var handle = Find(scene, "月餅-印章握把");
        if (handle == null)
        {
            sb.AppendLine("  找不到「月餅-印章握把」");
            return;
        }

        foreach (var name in new[] { "月餅-印章握把", "月餅-完成體" })
        {
            GameObject go = null;
            foreach (var tr in handle.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != name) continue;
                go = tr.gameObject;
                break;
            }

            if (go == null)
            {
                sb.AppendLine($"  {name}：找不到");
                continue;
            }

            var r = go.GetComponent<Renderer>();
            if (r == null)
            {
                sb.AppendLine($"  {name}：沒有 Renderer");
                continue;
            }

            var s = r.bounds.size;
            sb.AppendLine($"  {name,-14} 世界尺寸 = {s.x:F4} x {s.y:F4} x {s.z:F4} 公尺" +
                          $"（最長邊 {Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 100f:F1} 公分）");
        }
    }

    // ------------------------------------------------------------------

    static string Hex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        }
        return null;
    }
}
