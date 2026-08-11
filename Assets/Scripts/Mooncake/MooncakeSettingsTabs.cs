using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

/// <summary>
/// 設定視窗的分頁切換。每個分頁一顆按鈕、一個內容面板，
/// 選中的按鈕會換色，其餘內容面板關閉。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeSettingsTabs : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public string name;
        public Button button;
        public GameObject content;
        [Tooltip("按鈕底圖，選中／未選中會換圖")]
        public Image buttonBackground;
        [Tooltip("分頁標題文字，選中時換色")]
        public TMPro.TextMeshProUGUI label;
    }

    public List<Tab> tabs = new List<Tab>();

    [Header("外觀")]
    [Tooltip("選中的底圖")]
    public Sprite selectedSprite;
    [Tooltip("未選中的底圖")]
    public Sprite normalSprite;
    [Tooltip("選中時的文字顏色")]
    public Color selectedTextColor = new Color(0.23f, 0.13f, 0.07f, 1f);
    [Tooltip("未選中時的文字顏色")]
    public Color normalTextColor = new Color(1f, 0.94f, 0.85f, 1f);

    [ShowInInspector, ReadOnly] private int _current = -1;

    public int CurrentIndex => _current;

    private void OnEnable()
    {
        Wire();
        Select(Mathf.Max(0, _current));
    }

    private void Wire()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;                       // 迴圈變數要複製，不然全部都會指到最後一個
            var b = tabs[i].button;
            if (b == null) continue;

            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => Select(index));
        }
    }

    [Button("下一個分頁", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    public void NextTab()
    {
        if (tabs.Count == 0) return;
        Select((_current + 1) % tabs.Count);
    }

    public void Select(int index)
    {
        if (tabs.Count == 0) return;
        _current = Mathf.Clamp(index, 0, tabs.Count - 1);

        for (int i = 0; i < tabs.Count; i++)
        {
            bool on = i == _current;
            var t = tabs[i];

            if (t.content != null) t.content.SetActive(on);

            if (t.buttonBackground != null)
            {
                var spr = on ? selectedSprite : normalSprite;
                if (spr != null) t.buttonBackground.sprite = spr;
            }

            if (t.label != null) t.label.color = on ? selectedTextColor : normalTextColor;
        }
    }
}
