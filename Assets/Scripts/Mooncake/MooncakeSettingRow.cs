using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 一列設定。連續型（音量之類）用拉桿，其餘用「◀ 目前值 ▶」。
/// 兩種控制項都掛著，由 <see cref="MooncakeSettings"/> 決定顯示哪一種。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeSettingRow : MonoBehaviour
{
    public MooncakeSettings.Id settingId;

    [Header("共用")]
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI valueText;

    [Header("步進式（◀ ▶）")]
    public GameObject stepperGroup;
    public Button prevButton;
    public Button nextButton;

    [Header("拉桿式（連續型設定用）")]
    public GameObject sliderGroup;
    public Slider slider;

    private MooncakeSettings _settings;
    private bool _applying;      // 避免 SetValue 觸發 onValueChanged 造成迴圈

    private void OnEnable()
    {
        Bind();
        Refresh();
    }

    private void Start()
    {
        // 同上：確保顯示的是設定元件載入後的值
        Bind();
        Refresh();
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.onChanged.RemoveListener(Refresh);
        if (slider != null) slider.onValueChanged.RemoveListener(OnSlider);
    }

    private void Bind()
    {
        if (_settings != null) return;

        _settings = MooncakeSettings.Instance ?? FindObjectOfType<MooncakeSettings>(true);
        if (_settings == null) return;

        _settings.onChanged.AddListener(Refresh);

        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(Prev);
            prevButton.onClick.AddListener(Prev);
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(Next);
            nextButton.onClick.AddListener(Next);
        }
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.RemoveListener(OnSlider);
            slider.onValueChanged.AddListener(OnSlider);
        }

        // 依項目型別決定顯示哪一組控制項
        bool useSlider = _settings.IsSlider(settingId) && slider != null;
        if (sliderGroup != null) sliderGroup.SetActive(useSlider);
        if (stepperGroup != null) stepperGroup.SetActive(!useSlider);
    }

    public void Prev() => Step(-1);
    public void Next() => Step(1);

    private void Step(int dir)
    {
        if (_settings == null) Bind();
        _settings?.Step(settingId, dir);
        Refresh();
    }

    private void OnSlider(float v)
    {
        if (_applying || _settings == null) return;
        _settings.SetNormalized(settingId, v);
    }

    public void Refresh()
    {
        if (_settings == null) return;

        if (labelText != null) labelText.text = _settings.GetLabel(settingId);
        if (valueText != null) valueText.text = _settings.GetDisplay(settingId);

        if (slider != null && _settings.IsSlider(settingId))
        {
            _applying = true;
            slider.SetValueWithoutNotify(_settings.GetNormalized(settingId));
            _applying = false;
        }
    }
}
