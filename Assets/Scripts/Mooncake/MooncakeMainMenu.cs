using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

/// <summary>
/// 選關畫面。三顆按鈕都對應一個 public 方法，UI Button 直接綁；
/// 同時每個都有 Odin 按鈕，在 Editor 不戴頭盔也能按。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeMainMenu : MonoBehaviour
{
    [Header("場景名稱（必須加進 Build Settings）")]
    public string chineseScene = "ChineseMooncakeDemo";
    public string indonesianScene = "IndonesianMooncakeDemo";

    [Header("設定選單")]
    [Tooltip("設定面板的根物件，開場關閉")]
    public GameObject settingsPanel;
    [Tooltip("開設定時要收起來的主選單根物件（可留空）")]
    public GameObject mainPanel;

    [Header("事件")]
    public UnityEvent onSettingsOpened;
    public UnityEvent onSettingsClosed;

    [ShowInInspector, ReadOnly] private bool _settingsOpen;

    public bool IsSettingsOpen => _settingsOpen;

    private void Start()
    {
        CloseSettings();
    }

    // ---------------- 三顆按鈕 ----------------

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("① 進入 中式月餅", ButtonSizes.Large), GUIColor(0.95f, 0.75f, 0.35f)]
    public void LoadChineseScene()
    {
        LoadScene(chineseScene);
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("② 進入 印尼月餅", ButtonSizes.Large), GUIColor(0.95f, 0.88f, 0.7f)]
    public void LoadIndonesianScene()
    {
        LoadScene(indonesianScene);
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("③ 開啟設定", ButtonSizes.Large), GUIColor(0.6f, 0.8f, 1f)]
    public void OpenSettings()
    {
        _settingsOpen = true;
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (mainPanel != null) mainPanel.SetActive(false);
        onSettingsOpened?.Invoke();
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("關閉設定", ButtonSizes.Medium), GUIColor(0.8f, 0.8f, 0.8f)]
    public void CloseSettings()
    {
        _settingsOpen = false;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        onSettingsClosed?.Invoke();
    }

    /// <summary>設定按鈕綁這個就能開關切換。</summary>
    public void ToggleSettings()
    {
        if (_settingsOpen) CloseSettings();
        else OpenSettings();
    }

    // ---------------- 換場景 ----------------

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[選關] 沒有指定場景名稱", this);
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[選關] 現在不是 Play Mode，不會真的載入「{sceneName}」", this);
            return;
        }

        if (!SceneExistsInBuild(sceneName))
        {
            Debug.LogError($"[選關] 場景「{sceneName}」不在 Build Settings 裡，載入會失敗。" +
                           "請到 File → Build Settings 把它加進去。", this);
            return;
        }

        var fader = MooncakeScreenFader.Instance;
        if (fader != null) fader.LoadScene(sceneName);      // 淡出後再載入
        else SceneManager.LoadScene(sceneName);
    }

    private static bool SceneExistsInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return true;
        }
        return false;
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("檢查 Build Settings 有沒有這兩個場景", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void DebugCheckBuildSettings()
    {
        foreach (var n in new[] { chineseScene, indonesianScene })
        {
            bool ok = SceneExistsInBuild(n);
            if (ok) Debug.Log($"[選關] ✓ 「{n}」在 Build Settings 裡", this);
            else Debug.LogError($"[選關] ✗ 「{n}」不在 Build Settings 裡", this);
        }
    }
}
