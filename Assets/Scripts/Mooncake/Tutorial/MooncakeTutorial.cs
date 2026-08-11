using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 中式月餅 demo 的新手教學。
///
/// 掛著就好，不用在場景上連任何事件：Start 時自己去訂閱
/// <see cref="MooncakeChineseFlow"/> 與底下各站點的事件，跟著流程一步一步走。
///
/// 每個步驟「第一次」出現時才會跳文字板（打字效果）＋箭頭指向目標，
/// 所以實際效果就是第一顆月餅有完整教學，第二、三顆安靜做完。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeTutorial : MonoBehaviour
{
    public enum Step
    {
        抓麵團,
        放上檯面,
        拍打麵團,
        挑餡料,
        放入餡料,
        包起來,
        放進模具,
        放上烤盤,
        烤盤進烤箱,
        刷蛋液,
        再送烤箱,
        完成
    }

    [System.Serializable]
    public class StepText
    {
        public Step step;
        public string title;
        [TextArea(2, 4)] public string body;
    }

    [Header("流程與介面")]
    [Tooltip("留空會自動在場景裡找")]
    public MooncakeChineseFlow flow;
    public MooncakeTutorialPanel panel;
    [Tooltip("箭頭範本；執行時會複製出需要的數量（自己保持關閉）")]
    public MooncakeTutorialArrow arrowTemplate;
    [Tooltip("同時最多幾支箭頭（挑餡料那步有四個碗）")]
    public int maxArrows = 4;

    [Header("額外站點（留空會自動找）")]
    [Tooltip("餡料碗；留空會自動找場景裡生成物 Tag 是 Filling 的抓取來源")]
    public Transform[] fillingBowls;
    [Tooltip("蛋液刷；留空會自動找場景裡的 MooncakeLiquidDripper")]
    public Transform eggBrush;

    [Header("行為")]
    [Tooltip("每個步驟只教一次（第二、三顆就不再跳）")]
    public bool onlyFirstTime = true;
    [Tooltip("進場後隔多久開始第一段教學，留時間給畫面淡入")]
    public float startDelay = 1.5f;
    [Tooltip("最後一段「完成」顯示幾秒後自動收起，0 = 不收")]
    public float finishHideSeconds = 6f;
    [Tooltip("多久重算一次箭頭目標（烤盤格子、生出來的麵團會換位置）")]
    public float targetRefreshInterval = 0.25f;
    [Tooltip("記在 PlayerPrefs 裡，下次進遊戲就不再教（demo 通常關著）")]
    public bool rememberAcrossSessions = false;

    [Header("多國語言（I2）")]
    [Tooltip("先查 I2 的 term，查不到才用下面的內建文案")]
    public bool useLocalization = true;
    [Tooltip("term 前綴，實際 term 是「前綴 + 步驟英文名 + _Title / _Body」")]
    public string termPrefix = DefaultTermPrefix;
    [Tooltip("玩家中途換語言時，把正在顯示的那段重新打一次")]
    public bool retypeOnLanguageChange = true;

    [Header("事件")]
    [Tooltip("每次真的跳出教學時送出步驟名稱")]
    public MooncakeStringEvent onStepShown;

    [Header("教學文字（I2 查不到時的備援）")]
    [Tooltip("內文可用代號：{pats}、{wraps}、{count}、{placed}、{left}\n" +
             "（也接受中文寫法 {拍打次數}、{包餡次數}、{顆數}、{已放}、{剩幾顆}）")]
    public List<StepText> texts = new List<StepText>();

    // ---- 內部狀態 ----
    private readonly HashSet<Step> _shown = new HashSet<Step>();
    private readonly List<MooncakeTutorialArrow> _arrows = new List<MooncakeTutorialArrow>();
    private readonly List<Transform> _targets = new List<Transform>();
    private readonly List<MooncakeSpawnSource> _fillingSources = new List<MooncakeSpawnSource>();

    private Step _current;
    private bool _hasCurrent;
    private bool _panelShowing;
    private float _nextRefresh;
    private float _nextLanguageCheck;
    private string _lastLanguage;
    private GameObject _doughPiece;
    private Coroutine _hideRoutine;

    private const string PrefsKey = "mc_tutorial_shown";

    /// <summary>寫進 I2 的 term 前綴，Editor 的寫入工具也用同一個。</summary>
    public const string DefaultTermPrefix = "Mooncake/Tutorial/";

    /// <summary>目前正在教（或剛跑過）的步驟。</summary>
    public Step CurrentStep => _current;

    // ==================================================================
    // 生命週期
    // ==================================================================

    private void Awake()
    {
        if (flow == null) flow = FindObjectOfType<MooncakeChineseFlow>();
        if (panel == null) panel = GetComponentInChildren<MooncakeTutorialPanel>(true);
        if (arrowTemplate == null) arrowTemplate = GetComponentInChildren<MooncakeTutorialArrow>(true);

        if (texts == null || texts.Count == 0) texts = BuildDefaultTexts();

        if (arrowTemplate != null) arrowTemplate.gameObject.SetActive(false);
        if (rememberAcrossSessions) LoadShown();
    }

    private void Start()
    {
        if (flow == null)
        {
            Debug.LogWarning("[月餅教學] 場景裡找不到 MooncakeChineseFlow，教學不會啟動", this);
            enabled = false;
            return;
        }

        ResolveExtraTargets();
        BuildArrows();
        Subscribe();

        if (panel != null) panel.Hide();

        StartCoroutine(BeginAfterDelay());
    }

    private IEnumerator BeginAfterDelay()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        Begin(Step.抓麵團);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // ==================================================================
    // 訂閱流程事件
    // ==================================================================

    private void Subscribe()
    {
        flow.onDoughPieceSpawned.AddListener(HandleDoughPieceSpawned);
        flow.onAllPlaced.AddListener(HandleAllPlaced);
        flow.onEggWashStart.AddListener(HandleEggWashStart);
        flow.onEggWashComplete.AddListener(HandleEggWashComplete);
        flow.onAllBaked.AddListener(HandleAllBaked);

        if (flow.doughBall != null) flow.doughBall.onSpawned.AddListener(HandleDoughGrabbed);
        if (flow.doughHint != null) flow.doughHint.Received += HandleDoughDelivered;

        if (flow.flattenStation != null)
        {
            flow.flattenStation.onFlattenComplete.AddListener(HandleFlattenComplete);
            flow.flattenStation.onFillingPlaced.AddListener(HandleFillingPlaced);
            flow.flattenStation.onWrapComplete.AddListener(HandleWrapComplete);
        }

        if (flow.moldStation != null) flow.moldStation.onDoughReceived.AddListener(HandleMoldReceived);

        if (flow.traySlots != null)
        {
            foreach (var slot in flow.traySlots)
            {
                if (slot != null) slot.Placed += HandleSlotPlaced;
            }
        }

        if (flow.ovenStation != null) flow.ovenStation.onPanInserted.AddListener(HandlePanInserted);

        foreach (var bowl in _fillingSources)
        {
            if (bowl != null) bowl.onSpawned.AddListener(HandleFillingGrabbed);
        }
    }

    private void Unsubscribe()
    {
        if (flow == null) return;

        flow.onDoughPieceSpawned.RemoveListener(HandleDoughPieceSpawned);
        flow.onAllPlaced.RemoveListener(HandleAllPlaced);
        flow.onEggWashStart.RemoveListener(HandleEggWashStart);
        flow.onEggWashComplete.RemoveListener(HandleEggWashComplete);
        flow.onAllBaked.RemoveListener(HandleAllBaked);

        if (flow.doughBall != null) flow.doughBall.onSpawned.RemoveListener(HandleDoughGrabbed);
        if (flow.doughHint != null) flow.doughHint.Received -= HandleDoughDelivered;

        if (flow.flattenStation != null)
        {
            flow.flattenStation.onFlattenComplete.RemoveListener(HandleFlattenComplete);
            flow.flattenStation.onFillingPlaced.RemoveListener(HandleFillingPlaced);
            flow.flattenStation.onWrapComplete.RemoveListener(HandleWrapComplete);
        }

        if (flow.moldStation != null) flow.moldStation.onDoughReceived.RemoveListener(HandleMoldReceived);

        if (flow.traySlots != null)
        {
            foreach (var slot in flow.traySlots)
            {
                if (slot != null) slot.Placed -= HandleSlotPlaced;
            }
        }

        if (flow.ovenStation != null) flow.ovenStation.onPanInserted.RemoveListener(HandlePanInserted);

        foreach (var bowl in _fillingSources)
        {
            if (bowl != null) bowl.onSpawned.RemoveListener(HandleFillingGrabbed);
        }
    }

    // ---- 各站點回呼 ----

    private void HandleDoughGrabbed(GameObject go) => Begin(Step.放上檯面);
    private void HandleDoughDelivered(GameObject go) => Begin(Step.拍打麵團);
    private void HandleFlattenComplete() => Begin(Step.挑餡料);
    private void HandleFillingPlaced(GameObject go) => Begin(Step.包起來);
    private void HandleWrapComplete() => Begin(Step.放進模具);
    private void HandleMoldReceived(GameObject go) => Begin(Step.放上烤盤);
    private void HandleAllPlaced() => Begin(Step.烤盤進烤箱);
    private void HandleEggWashStart() => Begin(Step.刷蛋液);
    private void HandleEggWashComplete() => Begin(Step.再送烤箱);
    private void HandleAllBaked() => Begin(Step.完成);

    private void HandleDoughPieceSpawned(GameObject go)
    {
        _doughPiece = go;
    }

    private void HandleFillingGrabbed(GameObject go)
    {
        // 只有在等餡料的時候才往下推，別的階段抓餡料不算數
        if (flow.flattenStation == null ||
            flow.flattenStation.CurrentPhase == MooncakeFlattenStation.Phase.WaitFilling)
            Begin(Step.放入餡料);
    }

    private void HandleSlotPlaced(MooncakeTraySlot slot)
    {
        // 放滿三顆時 flow 會先送 onAllPlaced，那邊已經接手了
        int total = flow.traySlots != null ? flow.traySlots.Length : 0;
        if (total > 0 && flow.PlacedCount >= total) return;

        // 還沒滿 → 回到第一步做下一顆（第二顆之後已經教過，會自動安靜）
        Begin(Step.抓麵團);
    }

    private void HandlePanInserted()
    {
        // 烤箱門關上、倒數中，先把教學收起來，等出爐再說
        HideAll();
    }

    // ==================================================================
    // 步驟切換
    // ==================================================================

    /// <summary>跳到某個步驟；已經教過的話只會安靜地把畫面收乾淨。</summary>
    public void Begin(Step step)
    {
        _current = step;
        _hasCurrent = true;

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        if (onlyFirstTime && _shown.Contains(step))
        {
            HideAll();
            return;
        }

        _shown.Add(step);
        if (rememberAcrossSessions) SaveShown();

        ShowCurrentText();
        _panelShowing = true;

        RefreshTargets();
        _nextRefresh = Time.time + targetRefreshInterval;

        onStepShown?.Invoke(step.ToString());

        if (step == Step.完成 && finishHideSeconds > 0f)
            _hideRoutine = StartCoroutine(HideAfter(finishHideSeconds));
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        HideAll();
        _hideRoutine = null;
    }

    /// <summary>把文字板與所有箭頭收起來。</summary>
    public void HideAll()
    {
        _panelShowing = false;

        if (panel != null) panel.Hide();
        foreach (var arrow in _arrows)
        {
            if (arrow != null) arrow.Hide();
        }
    }

    private void Update()
    {
        CheckLanguageChanged();

        if (!_panelShowing || Time.time < _nextRefresh) return;

        _nextRefresh = Time.time + Mathf.Max(0.05f, targetRefreshInterval);
        RefreshTargets();
    }

    /// <summary>玩家在設定裡換語言時，正在顯示的那段要跟著換。</summary>
    private void CheckLanguageChanged()
    {
        if (!useLocalization || Time.unscaledTime < _nextLanguageCheck) return;

        _nextLanguageCheck = Time.unscaledTime + 0.5f;

        string lang = MooncakeLoc.CurrentLanguage;
        if (lang == _lastLanguage) return;

        bool first = _lastLanguage == null;
        _lastLanguage = lang;

        if (!first && retypeOnLanguageChange && _panelShowing) ShowCurrentText();
    }

    // ==================================================================
    // 箭頭
    // ==================================================================

    private void BuildArrows()
    {
        if (arrowTemplate == null)
        {
            Debug.LogWarning("[月餅教學] 沒有指定箭頭範本，只會有文字沒有箭頭", this);
            return;
        }

        arrowTemplate.gameObject.SetActive(false);

        for (int i = 0; i < Mathf.Max(1, maxArrows); i++)
        {
            var clone = Instantiate(arrowTemplate, arrowTemplate.transform.parent);
            clone.name = $"教學箭頭 {i + 1}";
            clone.gameObject.SetActive(true);
            clone.SetTarget(null);
            clone.Hide();
            _arrows.Add(clone);
        }
    }

    private void RefreshTargets()
    {
        if (!_hasCurrent) return;

        ResolveTargets(_current, _targets);

        for (int i = 0; i < _arrows.Count; i++)
        {
            var arrow = _arrows[i];
            if (arrow == null) continue;

            if (i < _targets.Count)
            {
                arrow.SetTarget(_targets[i]);
                arrow.Show();
            }
            else
            {
                arrow.Hide();
            }
        }
    }

    /// <summary>算出這一步該指哪些東西（最多 maxArrows 個，多的會被忽略）。</summary>
    private void ResolveTargets(Step step, List<Transform> into)
    {
        into.Clear();

        switch (step)
        {
            case Step.抓麵團:
                Add(into, flow.doughBall);
                break;

            case Step.放上檯面:
                Add(into, flow.doughHint);
                break;

            case Step.拍打麵團:
            case Step.包起來:
                Add(into, flow.flattenStation);
                break;

            case Step.挑餡料:
                if (fillingBowls != null)
                {
                    foreach (var bowl in fillingBowls) Add(into, bowl);
                }
                break;

            case Step.放入餡料:
                if (flow.flattenStation != null) Add(into, flow.flattenStation.hintObject);
                break;

            case Step.放進模具:
                if (flow.moldStation != null) Add(into, flow.moldStation.hintObject);
                if (_doughPiece != null) Add(into, _doughPiece.transform);
                break;

            case Step.放上烤盤:
                Add(into, NextEmptySlot());
                Add(into, flow.moldStation);
                break;

            case Step.烤盤進烤箱:
            case Step.再送烤箱:
                Add(into, flow.bakingPan);
                if (flow.ovenStation != null) Add(into, flow.ovenStation.ovenZone);
                break;

            case Step.刷蛋液:
                Add(into, eggBrush);
                if (flow.traySlots != null)
                {
                    foreach (var slot in flow.traySlots)
                    {
                        if (slot != null && slot.IsFilled && !slot.IsEggWashed)
                            Add(into, slot.placedObject);
                    }
                }
                break;

            case Step.完成:
                break;
        }
    }

    private static void Add(List<Transform> into, Component c)
    {
        if (c != null) into.Add(c.transform);
    }

    private static void Add(List<Transform> into, GameObject go)
    {
        if (go != null) into.Add(go.transform);
    }

    private static void Add(List<Transform> into, Transform t)
    {
        if (t != null) into.Add(t);
    }

    private MooncakeTraySlot NextEmptySlot()
    {
        if (flow.traySlots == null) return null;

        foreach (var slot in flow.traySlots)
        {
            if (slot != null && !slot.IsFilled) return slot;
        }
        return null;
    }

    // ==================================================================
    // 自動找場景物件
    // ==================================================================

    private void ResolveExtraTargets()
    {
        CollectFillingSources();

        if (fillingBowls == null || fillingBowls.Length == 0)
        {
            var list = new List<Transform>();
            foreach (var src in _fillingSources) list.Add(src.transform);
            fillingBowls = list.ToArray();

            if (fillingBowls.Length == 0)
                Debug.LogWarning("[月餅教學] 找不到餡料碗，「挑餡料」那步不會有箭頭", this);
        }

        if (eggBrush == null)
        {
            var dripper = FindObjectOfType<MooncakeLiquidDripper>(true);
            if (dripper != null) eggBrush = dripper.transform;
        }
    }

    /// <summary>場景裡生成物是餡料（Tag = Filling）的抓取來源，也就是四個餡料碗。</summary>
    private void CollectFillingSources()
    {
        _fillingSources.Clear();

        foreach (var src in FindObjectsOfType<MooncakeSpawnSource>(true))
        {
            if (src == null || src == flow.doughBall) continue;
            if (src.spawnPrefab == null) continue;
            if (!src.spawnPrefab.CompareTag("Filling")) continue;

            _fillingSources.Add(src);
        }
    }

    // ==================================================================
    // 文字
    // ==================================================================

    /// <summary>把目前這一步的文字丟給面板（先查 I2，查不到用內建文案）。</summary>
    private void ShowCurrentText()
    {
        if (panel == null || !_hasCurrent) return;

        var text = FindText(_current);

        string title = Localized(TitleTerm(_current, termPrefix), text.title);
        string body = Localized(BodyTerm(_current, termPrefix), text.body);

        panel.Show(title, Substitute(body));
    }

    private string Localized(string term, string fallback)
    {
        return useLocalization ? MooncakeLoc.Get(term, fallback) : fallback;
    }

    /// <summary>步驟對應的 I2 term 英文代號（term 用 ASCII，翻譯人員比較好認）。</summary>
    public static string TermKey(Step step)
    {
        switch (step)
        {
            case Step.抓麵團:     return "GrabDough";
            case Step.放上檯面:   return "PlaceOnTable";
            case Step.拍打麵團:   return "FlattenDough";
            case Step.挑餡料:     return "PickFilling";
            case Step.放入餡料:   return "PlaceFilling";
            case Step.包起來:     return "WrapDough";
            case Step.放進模具:   return "PutInMold";
            case Step.放上烤盤:   return "PlaceOnTray";
            case Step.烤盤進烤箱: return "TrayIntoOven";
            case Step.刷蛋液:     return "EggWash";
            case Step.再送烤箱:   return "BakeAgain";
            case Step.完成:       return "Finish";
            default:              return step.ToString();
        }
    }

    public static string TitleTerm(Step step, string prefix = DefaultTermPrefix)
    {
        return prefix + TermKey(step) + "_Title";
    }

    public static string BodyTerm(Step step, string prefix = DefaultTermPrefix)
    {
        return prefix + TermKey(step) + "_Body";
    }

    private StepText FindText(Step step)
    {
        if (texts != null)
        {
            foreach (var t in texts)
            {
                if (t != null && t.step == step) return t;
            }
        }

        return new StepText { step = step, title = step.ToString(), body = "" };
    }

    /// <summary>把內文裡的代號換成實際數字，這樣改 Inspector 設定不用回頭改文案。</summary>
    private string Substitute(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        int total = flow.traySlots != null ? flow.traySlots.Length : 3;
        int placed = flow.PlacedCount;

        int flatten = flow.flattenStation != null ? flow.flattenStation.pokesToFlatten : 5;
        int wrap = flow.flattenStation != null ? flow.flattenStation.pokesToWrap : 5;

        string left = Mathf.Max(0, total - placed).ToString();

        return body
            // 翻譯用的 ASCII 代號
            .Replace("{pats}", flatten.ToString())
            .Replace("{wraps}", wrap.ToString())
            .Replace("{count}", total.ToString())
            .Replace("{placed}", placed.ToString())
            .Replace("{left}", left)
            // 中文寫法也留著，舊文案不用改
            .Replace("{拍打次數}", flatten.ToString())
            .Replace("{包餡次數}", wrap.ToString())
            .Replace("{顆數}", total.ToString())
            .Replace("{已放}", placed.ToString())
            .Replace("{剩幾顆}", left);
    }

    /// <summary>預設文案；Inspector 上清空再按「載入預設文字」就會回到這一份。</summary>
    public static List<StepText> BuildDefaultTexts()
    {
        return new List<StepText>
        {
            new StepText { step = Step.抓麵團,     title = "① 拿一團麵團",
                body = "把手伸到麵團球上，按下扳機鍵抓一團麵團出來。" },
            new StepText { step = Step.放上檯面,   title = "② 放到檯面上",
                body = "把手裡的麵團，放到檯面上發亮的位置。" },
            new StepText { step = Step.拍打麵團,   title = "③ 把麵團拍扁",
                body = "用手輕拍麵團 {拍打次數} 下，把它拍成一張餅皮。" },
            new StepText { step = Step.挑餡料,     title = "④ 挑一種餡料",
                body = "旁邊的碗裡有蓮子、紅豆、奶油、巧克力，任選一種抓起來。" },
            new StepText { step = Step.放入餡料,   title = "⑤ 把餡放進餅皮",
                body = "把手上的餡料，放到餅皮中央的凹槽裡。" },
            new StepText { step = Step.包起來,     title = "⑥ 把餡包起來",
                body = "再輕拍 {包餡次數} 下，讓餅皮慢慢收口包住餡料。" },
            new StepText { step = Step.放進模具,   title = "⑦ 放進模具",
                body = "拿起做好的麵團，塞進月餅模具裡壓出花紋。" },
            new StepText { step = Step.放上烤盤,   title = "⑧ 壓到烤盤上",
                body = "握著模具，對準烤盤上發亮的格子壓下去。這一盤要做 {顆數} 顆。" },
            new StepText { step = Step.烤盤進烤箱, title = "⑨ 送進烤箱",
                body = "{顆數} 顆都排好了，把整個烤盤端起來，放進烤箱裡面。" },
            new StepText { step = Step.刷蛋液,     title = "⑩ 刷上蛋液",
                body = "第一輪烤好了！拿起刷子，在每一顆月餅上都刷過一次。" },
            new StepText { step = Step.再送烤箱,   title = "⑪ 再烤一次",
                body = "蛋液都刷好了，把烤盤再送回烤箱，烤出金黃色。" },
            new StepText { step = Step.完成,       title = "完成！",
                body = "月餅出爐囉，小心燙。" },
        };
    }

    // ==================================================================
    // 記錄
    // ==================================================================

    private void LoadShown()
    {
        _shown.Clear();

        string saved = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var part in saved.Split(','))
        {
            if (System.Enum.TryParse(part, out Step step)) _shown.Add(step);
        }
    }

    private void SaveShown()
    {
        var parts = new List<string>();
        foreach (var s in _shown) parts.Add(s.ToString());

        PlayerPrefs.SetString(PrefsKey, string.Join(",", parts));
        PlayerPrefs.Save();
    }

    // ==================================================================
    // Debug
    // ==================================================================

    [FoldoutGroup("Debug"), Button("載入預設文字（會覆蓋現有文案）", ButtonSizes.Medium), GUIColor(1f, 0.8f, 0.4f)]
    public void LoadDefaultTexts()
    {
        texts = BuildDefaultTexts();
    }

    [FoldoutGroup("Debug"), Tooltip("要測試的步驟")]
    public Step debugStep = Step.抓麵團;

    [FoldoutGroup("Debug"), Button("Debug：跳到這一步", ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugShowStep()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[月餅教學] 要在 Play Mode 下按（面板與箭頭是執行時才生出來的）", this);
            return;
        }

        _shown.Remove(debugStep);
        Begin(debugStep);
    }

    [FoldoutGroup("Debug"), Button("Debug：全部收起來", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void DebugHide()
    {
        HideAll();
    }

    [FoldoutGroup("Debug"), Button("重置教學（下一顆會重新教一次）", ButtonSizes.Large), GUIColor(1f, 0.5f, 0.3f)]
    public void ResetTutorial()
    {
        _shown.Clear();
        _doughPiece = null;

        if (rememberAcrossSessions)
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        HideAll();
    }
}
