using UnityEngine;
using TMPro;

/// <summary>
/// 靜態文字的多語化：掛在 TMP 文字上，指定 term 後會依目前語言更新。
/// 換語言時由 <see cref="MooncakeSettings.onLanguageChanged"/> 觸發重整。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public class MooncakeLocalizedText : MonoBehaviour
{
    [Tooltip("I2 的 term，例如 Menu/Title")]
    public string term;

    [Tooltip("查不到翻譯時顯示這個；留空則沿用元件目前的文字")]
    public string fallback;

    private TextMeshProUGUI _text;
    private MooncakeSettings _settings;

    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
        if (string.IsNullOrEmpty(fallback) && _text != null) fallback = _text.text;
    }

    private void OnEnable()
    {
        _settings = MooncakeSettings.Instance ?? FindObjectOfType<MooncakeSettings>(true);
        if (_settings != null) _settings.onLanguageChanged.AddListener(OnLanguageChanged);
        Refresh();
    }

    private void Start()
    {
        // 保險：OnEnable 可能早於設定元件的 Awake，Start 一定在所有 Awake 之後
        Refresh();
    }

    private void OnDisable()
    {
        if (_settings != null) _settings.onLanguageChanged.RemoveListener(OnLanguageChanged);
    }

    private void OnLanguageChanged(string _) => Refresh();

    public void Refresh()
    {
        if (_text == null) _text = GetComponent<TextMeshProUGUI>();
        if (_text == null) return;

        _text.text = MooncakeLoc.T(term, fallback);
    }
}
