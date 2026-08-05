using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// demo 的教學介面：主畫面選種類 → 步驟清單跟著流程走 → 完成 → 回主畫面。
/// 用 world-space canvas 立在工作檯前面，之後換成正式 UI 時只換這一層。
/// </summary>
public class MooncakeDemoUI : MonoBehaviour
{
    [Header("流程")]
    public MooncakeFlow flow;

    [Header("面板")]
    public GameObject menuPanel;
    public GameObject playPanel;
    public GameObject finishPanel;

    [Header("文字")]
    public Text menuTitleText;
    public Text recipeTitleText;
    public Text currentStepText;
    public Text stepListText;
    public Text progressText;
    public Text finishText;

    [Header("按鈕")]
    public Button chineseButton;
    public Button indonesianButton;
    public Button actionButton;
    public Button skipButton;
    public Button backButton;
    public Button finishBackButton;

    readonly StringBuilder _sb = new StringBuilder();

    void Awake()
    {
        if (flow == null) flow = FindObjectOfType<MooncakeFlow>();

        if (chineseButton != null)    chineseButton.onClick.AddListener(() => flow.StartRecipe(MooncakeType.Chinese));
        if (indonesianButton != null) indonesianButton.onClick.AddListener(() => flow.StartRecipe(MooncakeType.Indonesian));
        if (actionButton != null)     actionButton.onClick.AddListener(flow.PerformActionOnCurrentStation);
        if (skipButton != null)       skipButton.onClick.AddListener(flow.ForceCompleteCurrentStep);
        if (backButton != null)       backButton.onClick.AddListener(flow.ReturnToMenu);
        if (finishBackButton != null) finishBackButton.onClick.AddListener(flow.ReturnToMenu);

        if (flow != null) flow.onFlowChanged.AddListener(Refresh);
    }

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        UpdateProgressLine();
    }

    public void Refresh()
    {
        if (flow == null) return;

        if (menuPanel != null)   menuPanel.SetActive(!flow.IsRunning && !flow.IsFinished);
        if (playPanel != null)   playPanel.SetActive(flow.IsRunning);
        if (finishPanel != null) finishPanel.SetActive(flow.IsFinished);

        if (menuTitleText != null) menuTitleText.text = "月餅製作 DEMO\n<size=22>選擇想烘烤的月餅種類</size>";

        if (!flow.IsRunning)
        {
            if (finishText != null && flow.IsFinished)
                finishText.text = MooncakeRecipes.TypeLabel(flow.CurrentType) + " 完成！";
            return;
        }

        var entry = flow.CurrentEntry;

        if (recipeTitleText != null)
            recipeTitleText.text = MooncakeRecipes.TypeLabel(flow.CurrentType) + " 烘烤步驟";

        if (currentStepText != null)
        {
            var mark = MooncakeRecipes.IsNewMechanic(entry.Step) ? " <color=#FF6B4A>●新機制</color>" : "";
            currentStepText.text = string.Format("{0}. {1}{2}\n<size=22>{3}</size>",
                flow.StepIndex + 1, MooncakeRecipes.StepLabel(entry.Step), mark, entry.Hint);
        }

        if (stepListText != null) stepListText.text = BuildStepList();
    }

    string BuildStepList()
    {
        _sb.Length = 0;

        for (int i = 0; i < flow.Steps.Count; i++)
        {
            var s = flow.Steps[i];
            var label = MooncakeRecipes.StepLabel(s.Step);

            if (i < flow.StepIndex)
                _sb.AppendFormat("<color=#5A8F5A>✓ {0}</color>\n", label);
            else if (i == flow.StepIndex)
                _sb.AppendFormat("<color=#FFC63A>▶ {0}　{1}</color>\n", label, s.Hint);
            else
                _sb.AppendFormat("<color=#9A9A9A>　 {0}</color>\n", label);
        }

        return _sb.ToString();
    }

    void UpdateProgressLine()
    {
        if (progressText == null || flow == null) return;

        if (!flow.IsRunning)
        {
            progressText.text = "";
            return;
        }

        var station = flow.FindActiveStation();
        if (station == null)
        {
            progressText.text = "<color=#C05A5A>找不到對應站點</color>";
            return;
        }

        var oven = station as MooncakeOvenStation;
        if (oven != null && oven.IsBaking)
        {
            progressText.text = string.Format("烘烤中 {0}%", Mathf.RoundToInt(oven.BakeProgress01 * 100f));
            return;
        }

        var action = station as MooncakeActionStation;
        if (action != null && action.requiredCount > 1)
        {
            progressText.text = string.Format("{0} / {1}", action.CurrentCount, action.requiredCount);
            return;
        }

        progressText.text = "前往「" + station.stationName + "」";
    }
}
