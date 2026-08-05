using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// 用方塊／圓球把月餅製作 demo 的站點自動搭進 MooncakeDemo 場景。
/// 重複執行會整組重建（只砍自己建的 MooncakeDemo 根物件，不動場景其他東西）。
/// </summary>
public static class MooncakeDemoSceneBuilder
{
    const string k_ScenePath = "Assets/Scenes/MooncakeDemo.unity";
    const string k_RootName = "MooncakeDemo";
    const string k_MatFolder = "Assets/Materials/MooncakeDemo";
    const string k_PrefabFolder = "Assets/Prefab/Mooncake";
    const string k_DoughPrefabPath = k_PrefabFolder + "/MooncakeDough.prefab";

    // 檯面高度（相對 demo 根物件，也就是玩家腳下）
    const float k_TableTop = 0.9f;

    [MenuItem("Tools/月餅 Demo/建立(重建) Demo 場景")]
    public static void BuildMenu()
    {
        Build(true);
    }

    [MenuItem("Tools/月餅 Demo/把玩家移到 Demo 區")]
    public static void MovePlayerToDemo()
    {
        var root = GameObject.Find(k_RootName);
        if (root == null)
        {
            Debug.LogError("[月餅] 場景裡找不到 " + k_RootName + "，請先建立 Demo 場景");
            return;
        }

        var origin = Object.FindObjectOfType<XROrigin>();
        if (origin == null)
        {
            Debug.LogError("[月餅] 場景裡找不到 XR Origin");
            return;
        }

        Undo.RecordObject(origin.transform, "Move Player To Mooncake Demo");
        origin.transform.position = root.transform.TransformPoint(new Vector3(0f, 0f, -0.95f));
        origin.transform.rotation = root.transform.rotation;
        EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
        Debug.Log("[月餅] 已把 XR Origin 移到 demo 工作檯前");
    }

    /// <summary>給 -executeMethod 用的入口。</summary>
    public static void BuildFromCommandLine()
    {
        Build(true);
        EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    // 建置主流程
    // ------------------------------------------------------------------

    public static void Build(bool saveScene)
    {
        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        var mats = new Mats();
        var doughPrefab = BuildDoughPrefab(mats);

        // 砍掉上一次建的
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == k_RootName) Object.DestroyImmediate(go);

        var root = new GameObject(k_RootName);
        PlaceRoot(root.transform);

        var flow = new GameObject("Flow").AddComponent<MooncakeFlow>();
        flow.transform.SetParent(root.transform, false);

        BuildTable(root.transform, mats);

        // ---- 站點（x 由左到右排在檯面上）----
        var grab = BuildGrabStation(root.transform, mats, doughPrefab, new Vector3(-1.30f, 0f, 0f));

        var flatten1 = BuildActionStation(root.transform, mats, "拍打台", MooncakeStep.Flatten,
            new Vector3(-0.90f, 0f, 0f), 5, MooncakeActionStation.ResultShape.Flat);

        BuildFillingStation(root.transform, mats, "餡料碗-豆沙", "豆沙",
            new Color(0.42f, 0.22f, 0.12f), MooncakeType.Chinese, new Vector3(-0.50f, 0f, 0.22f));
        BuildFillingStation(root.transform, mats, "餡料碗-蓮蓉", "蓮蓉",
            new Color(0.85f, 0.72f, 0.38f), MooncakeType.Chinese, new Vector3(-0.50f, 0f, -0.18f));
        BuildFillingStation(root.transform, mats, "餡料碗-巧克力", "巧克力",
            new Color(0.28f, 0.16f, 0.10f), MooncakeType.Indonesian, new Vector3(-0.50f, 0f, 0.22f));
        BuildFillingStation(root.transform, mats, "餡料碗-波羅蜜", "波羅蜜",
            new Color(0.95f, 0.68f, 0.20f), MooncakeType.Indonesian, new Vector3(-0.50f, 0f, -0.18f));

        BuildActionStation(root.transform, mats, "揉捏台", MooncakeStep.Knead,
            new Vector3(-0.10f, 0f, 0f), 6, MooncakeActionStation.ResultShape.Ball);

        BuildPressStation(root.transform, mats, "壓模台", MooncakeStep.Mold,
            MooncakePressStation.PressResult.Mold, MooncakeType.Chinese, new Vector3(0.30f, 0f, 0f));
        BuildPressStation(root.transform, mats, "蓋章台", MooncakeStep.Stamp,
            MooncakePressStation.PressResult.Stamp, MooncakeType.Indonesian, new Vector3(0.30f, 0f, 0f));

        BuildEggWashStation(root.transform, mats, new Vector3(0.70f, 0f, 0f));

        BuildOven(root.transform, mats, new Vector3(1.25f, 0f, 0f));

        BuildUI(root.transform, mats);

        // 玩家站位參考點
        var spot = new GameObject("PlayerSpot");
        spot.transform.SetParent(root.transform, false);
        spot.transform.localPosition = new Vector3(0f, 0f, -0.95f);

        // 把站點餵給 flow
        flow.stations.Clear();
        flow.stations.AddRange(root.GetComponentsInChildren<MooncakeStation>(true));

        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene) EditorSceneManager.SaveScene(scene);

        Debug.Log(string.Format("[月餅] Demo 場景建置完成：{0} 個站點，根物件在 {1}",
            flow.stations.Count, root.transform.position));
    }

    static void PlaceRoot(Transform root)
    {
        var origin = Object.FindObjectOfType<XROrigin>();
        var basePos = origin != null ? origin.transform.position : Vector3.zero;
        var baseRot = origin != null
            ? Quaternion.Euler(0f, origin.transform.eulerAngles.y, 0f)
            : Quaternion.identity;

        // 放在玩家右手邊 5 公尺，避免壓到紅龜粿原本的關卡佈置
        root.position = basePos + baseRot * new Vector3(5f, 0f, 0f);
        root.rotation = baseRot;
    }

    // ------------------------------------------------------------------
    // 場景零件
    // ------------------------------------------------------------------

    static void BuildTable(Transform parent, Mats mats)
    {
        var table = Prim(PrimitiveType.Cube, "工作檯", parent,
            new Vector3(0f, k_TableTop - 0.05f, 0f), new Vector3(3.2f, 0.1f, 1.0f), mats.Table);

        var leg = Prim(PrimitiveType.Cube, "檯座", parent,
            new Vector3(0f, (k_TableTop - 0.1f) * 0.5f, 0f),
            new Vector3(3.0f, k_TableTop - 0.1f, 0.8f), mats.TableLeg);
        leg.GetComponent<Collider>().enabled = false;
    }

    static MooncakeGrabStation BuildGrabStation(Transform parent, Mats mats,
        MooncakeWorkpiece doughPrefab, Vector3 localPos)
    {
        var go = StationRoot("抓取-麵團桶", parent, localPos, new Vector3(0.34f, 0.34f, 0.34f));
        var pad = StationPad(go.transform, mats, 0.30f);

        var bucket = Prim(PrimitiveType.Cube, "桶身", go.transform,
            new Vector3(0f, -0.06f, 0f), new Vector3(0.26f, 0.16f, 0.26f), mats.Bucket);
        bucket.GetComponent<Collider>().enabled = false;

        var spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(go.transform, false);
        spawn.transform.localPosition = new Vector3(0f, 0.12f, 0f);

        var st = go.AddComponent<MooncakeGrabStation>();
        st.step = MooncakeStep.Grab;
        st.stationName = "麵團桶";
        st.zoneRenderer = pad;
        st.doughPrefab = doughPrefab;
        st.spawnPoint = spawn.transform;

        Label(go.transform, mats, "抓取", new Vector3(0f, 0.30f, 0f));
        return st;
    }

    static MooncakeActionStation BuildActionStation(Transform parent, Mats mats, string name,
        MooncakeStep step, Vector3 localPos, int required, MooncakeActionStation.ResultShape result)
    {
        var go = StationRoot(name, parent, localPos, new Vector3(0.34f, 0.30f, 0.34f));
        var pad = StationPad(go.transform, mats, 0.30f);

        var st = go.AddComponent<MooncakeActionStation>();
        st.step = step;
        st.stationName = name;
        st.zoneRenderer = pad;
        st.requiredCount = required;
        st.result = result;

        Label(go.transform, mats, MooncakeRecipes.StepLabel(step), new Vector3(0f, 0.28f, 0f));
        return st;
    }

    static void BuildEggWashStation(Transform parent, Mats mats, Vector3 localPos)
    {
        var go = StationRoot("刷蛋液台", parent, localPos, new Vector3(0.34f, 0.30f, 0.34f));
        var pad = StationPad(go.transform, mats, 0.30f);

        // 蛋液碗
        var bowl = Prim(PrimitiveType.Cylinder, "蛋液碗", go.transform,
            new Vector3(0.22f, -0.09f, 0f), new Vector3(0.14f, 0.03f, 0.14f), mats.EggWash);
        bowl.GetComponent<Collider>().enabled = false;

        // 可抓的刷子
        var brush = Prim(PrimitiveType.Cube, "蛋液刷", go.transform,
            new Vector3(0.22f, 0.02f, 0f), new Vector3(0.03f, 0.14f, 0.03f), mats.Brush);
        var rb = brush.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        brush.AddComponent<XRGrabInteractable>();

        var st = go.AddComponent<MooncakeActionStation>();
        st.step = MooncakeStep.EggWash;
        st.stationName = "刷蛋液";
        st.zoneRenderer = pad;
        st.requiredCount = 5;
        st.result = MooncakeActionStation.ResultShape.None;
        st.applyEggWash = true;
        st.actionTool = brush.transform;
        st.restrictByType = true;
        st.onlyForType = MooncakeType.Chinese;

        Label(go.transform, mats, "刷蛋液", new Vector3(0f, 0.28f, 0f));
    }

    static void BuildFillingStation(Transform parent, Mats mats, string name, string fillingName,
        Color color, MooncakeType type, Vector3 localPos)
    {
        var go = StationRoot(name, parent, localPos, new Vector3(0.26f, 0.26f, 0.26f));

        var bowl = Prim(PrimitiveType.Cylinder, "碗", go.transform,
            new Vector3(0f, -0.09f, 0f), new Vector3(0.22f, 0.035f, 0.22f), mats.Bowl);
        bowl.GetComponent<Collider>().enabled = false;

        var fillingMat = mats.Make("Filling_" + fillingName, color);
        var filling = Prim(PrimitiveType.Cylinder, "餡料", go.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(0.18f, 0.02f, 0.18f), fillingMat);
        filling.GetComponent<Collider>().enabled = false;
        var pad = filling.GetComponent<Renderer>();

        var st = go.AddComponent<MooncakeFillingStation>();
        st.step = MooncakeStep.AddFilling;
        st.fillingName = fillingName;
        st.fillingColor = color;
        st.stationName = fillingName;
        st.zoneRenderer = pad;
        st.idleColor = color;
        st.activeColor = Color.Lerp(color, Color.white, 0.35f);
        st.restrictByType = true;
        st.onlyForType = type;

        // 碗前面的名牌
        Label(go.transform, mats, fillingName, new Vector3(0f, -0.02f, -0.16f));
    }

    static void BuildPressStation(Transform parent, Mats mats, string name, MooncakeStep step,
        MooncakePressStation.PressResult result, MooncakeType type, Vector3 localPos)
    {
        var go = StationRoot(name, parent, localPos, new Vector3(0.34f, 0.34f, 0.34f));
        var pad = StationPad(go.transform, mats, 0.30f);

        var isStamp = result == MooncakePressStation.PressResult.Stamp;

        var presser = Prim(PrimitiveType.Cube, isStamp ? "印章" : "模具", go.transform,
            new Vector3(0f, 0.20f, 0f),
            isStamp ? new Vector3(0.14f, 0.10f, 0.14f) : new Vector3(0.22f, 0.14f, 0.22f),
            isStamp ? mats.Stamp : mats.Mold);
        presser.GetComponent<Collider>().enabled = false;

        var st = go.AddComponent<MooncakePressStation>();
        st.step = step;
        st.stationName = name;
        st.zoneRenderer = pad;
        st.presser = presser.transform;
        st.result = result;
        st.pressDistance = 0.16f;
        st.restrictByType = true;
        st.onlyForType = type;

        Label(go.transform, mats, MooncakeRecipes.StepLabel(step), new Vector3(0f, 0.40f, 0f));
    }

    static void BuildOven(Transform parent, Mats mats, Vector3 localPos)
    {
        var oven = new GameObject("烤箱");
        oven.transform.SetParent(parent, false);
        oven.transform.localPosition = localPos + new Vector3(0f, k_TableTop, 0f);

        // 箱體（三面牆，正面留開口）
        var body = Prim(PrimitiveType.Cube, "箱體", oven.transform,
            new Vector3(0f, 0.22f, 0.16f), new Vector3(0.64f, 0.44f, 0.10f), mats.Oven);
        Prim(PrimitiveType.Cube, "左側", oven.transform,
            new Vector3(-0.30f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.42f), mats.Oven);
        Prim(PrimitiveType.Cube, "右側", oven.transform,
            new Vector3(0.30f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.42f), mats.Oven);
        Prim(PrimitiveType.Cube, "頂板", oven.transform,
            new Vector3(0f, 0.44f, 0f), new Vector3(0.64f, 0.05f, 0.42f), mats.Oven);
        var tray = Prim(PrimitiveType.Cube, "烤盤", oven.transform,
            new Vector3(0f, 0.02f, 0f), new Vector3(0.56f, 0.03f, 0.38f), mats.Tray);

        // 門（沿下緣旋轉開合）
        var hinge = new GameObject("門軸");
        hinge.transform.SetParent(oven.transform, false);
        hinge.transform.localPosition = new Vector3(0f, 0.01f, -0.21f);
        var door = Prim(PrimitiveType.Cube, "門", hinge.transform,
            new Vector3(0f, 0.21f, 0f), new Vector3(0.64f, 0.42f, 0.04f), mats.OvenDoor);
        door.GetComponent<Collider>().enabled = false;

        var trayPoint = new GameObject("TrayPoint");
        trayPoint.transform.SetParent(oven.transform, false);
        trayPoint.transform.localPosition = new Vector3(0f, 0.10f, 0f);

        // 烘烤站點（爐內空間）
        var bakeZone = StationRoot("烘烤區", oven.transform, new Vector3(0f, 0.18f, 0f),
            new Vector3(0.5f, 0.3f, 0.34f), false);
        var bake = bakeZone.AddComponent<MooncakeOvenStation>();
        bake.step = MooncakeStep.Bake;
        bake.stationName = "烤箱";
        bake.zoneRenderer = null;
        bake.door = hinge.transform;
        bake.trayPoint = trayPoint.transform;
        bake.bakeSeconds = 4f;

        // 取出站點（同一塊空間，另一步）
        var outZone = StationRoot("取出區", oven.transform, new Vector3(0f, 0.18f, 0f),
            new Vector3(0.5f, 0.3f, 0.34f), false);
        var cooling = new GameObject("出爐架");
        cooling.transform.SetParent(oven.transform, false);
        cooling.transform.localPosition = new Vector3(-0.55f, 0.08f, -0.25f);
        var rack = Prim(PrimitiveType.Cube, "架面", cooling.transform,
            Vector3.zero, new Vector3(0.3f, 0.02f, 0.3f), mats.Tray);
        rack.GetComponent<Collider>().enabled = false;

        var takeOut = outZone.AddComponent<MooncakeTakeOutStation>();
        takeOut.step = MooncakeStep.TakeOut;
        takeOut.stationName = "取出";
        takeOut.zoneRenderer = null;
        takeOut.oven = bake;
        takeOut.coolingPoint = cooling.transform;

        Label(oven.transform, mats, "烤箱", new Vector3(0f, 0.60f, 0f));

        // 箱體本身不要當觸發器擋住麵團
        body.GetComponent<Collider>().enabled = true;
        tray.GetComponent<Collider>().enabled = true;
    }

    // ------------------------------------------------------------------
    // 麵團 prefab
    // ------------------------------------------------------------------

    static MooncakeWorkpiece BuildDoughPrefab(Mats mats)
    {
        EnsureFolder(k_PrefabFolder);

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MooncakeDough";
        go.tag = "DoughObject";
        go.transform.localScale = Vector3.one * 0.13f;
        go.GetComponent<Renderer>().sharedMaterial = mats.Dough;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        go.AddComponent<XRGrabInteractable>();

        var filling = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        filling.name = "Filling";
        filling.transform.SetParent(go.transform, false);
        filling.transform.localScale = Vector3.one * 0.55f;
        Object.DestroyImmediate(filling.GetComponent<Collider>());
        filling.GetComponent<Renderer>().sharedMaterial = mats.FillingDefault;
        filling.SetActive(false);

        var stamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stamp.name = "StampMark";
        stamp.transform.SetParent(go.transform, false);
        stamp.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        stamp.transform.localScale = new Vector3(0.5f, 0.12f, 0.5f);
        Object.DestroyImmediate(stamp.GetComponent<Collider>());
        stamp.GetComponent<Renderer>().sharedMaterial = mats.Stamp;
        stamp.SetActive(false);

        var piece = go.AddComponent<MooncakeWorkpiece>();
        piece.bodyRenderer = go.GetComponent<Renderer>();
        piece.fillingVisual = filling.transform;
        piece.stampVisual = stamp.transform;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, k_DoughPrefabPath);
        Object.DestroyImmediate(go);

        return prefab.GetComponent<MooncakeWorkpiece>();
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    static void BuildUI(Transform parent, Mats mats)
    {
        var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);
        canvasGo.transform.localPosition = new Vector3(0f, 1.55f, 0.55f);
        canvasGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(900f, 620f);
        rt.localScale = Vector3.one * 0.0011f;

        var ui = canvasGo.AddComponent<MooncakeDemoUI>();
        ui.flow = parent.GetComponentInChildren<MooncakeFlow>();

        // ---- 主畫面 ----
        var menu = Panel("MenuPanel", canvasGo.transform, new Color(0.10f, 0.12f, 0.16f, 0.92f));
        ui.menuPanel = menu.gameObject;
        ui.menuTitleText = Text(menu, "Title", "月餅製作 DEMO", 44, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.62f), new Vector2(1f, 0.95f));
        ui.chineseButton = Button(menu, "BtnChinese", "中式月餅",
            new Vector2(0.08f, 0.22f), new Vector2(0.48f, 0.55f), new Color(0.82f, 0.55f, 0.20f));
        ui.indonesianButton = Button(menu, "BtnIndonesian", "印尼月餅",
            new Vector2(0.52f, 0.22f), new Vector2(0.92f, 0.55f), new Color(0.75f, 0.70f, 0.55f));
        Text(menu, "Note", "demo 階段：方塊圓球替身", 22, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.06f), new Vector2(1f, 0.18f));

        // ---- 製作中 ----
        var play = Panel("PlayPanel", canvasGo.transform, new Color(0.10f, 0.12f, 0.16f, 0.90f));
        ui.playPanel = play.gameObject;
        ui.recipeTitleText = Text(play, "RecipeTitle", "", 30, TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f));
        ui.currentStepText = Text(play, "CurrentStep", "", 36, TextAnchor.UpperLeft,
            new Vector2(0.40f, 0.55f), new Vector2(0.97f, 0.85f));
        ui.progressText = Text(play, "Progress", "", 30, TextAnchor.UpperLeft,
            new Vector2(0.40f, 0.40f), new Vector2(0.97f, 0.55f));
        ui.stepListText = Text(play, "StepList", "", 24, TextAnchor.UpperLeft,
            new Vector2(0.04f, 0.16f), new Vector2(0.38f, 0.85f));

        ui.actionButton = Button(play, "BtnAction", "執行動作",
            new Vector2(0.40f, 0.17f), new Vector2(0.62f, 0.34f), new Color(0.30f, 0.60f, 0.85f));
        ui.skipButton = Button(play, "BtnSkip", "跳過此步",
            new Vector2(0.64f, 0.17f), new Vector2(0.80f, 0.34f), new Color(0.45f, 0.45f, 0.50f));
        ui.backButton = Button(play, "BtnBack", "回主畫面",
            new Vector2(0.82f, 0.17f), new Vector2(0.97f, 0.34f), new Color(0.55f, 0.35f, 0.35f));
        Text(play, "Tip", "在 VR 裡直接對站點互動即可；上面兩顆是桌機測試用", 20,
            TextAnchor.MiddleLeft, new Vector2(0.04f, 0.03f), new Vector2(0.97f, 0.14f));

        // ---- 完成 ----
        var finish = Panel("FinishPanel", canvasGo.transform, new Color(0.10f, 0.16f, 0.12f, 0.92f));
        ui.finishPanel = finish.gameObject;
        ui.finishText = Text(finish, "FinishText", "完成！", 44, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.45f), new Vector2(1f, 0.85f));
        ui.finishBackButton = Button(finish, "BtnBackToMenu", "回主畫面",
            new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.40f), new Color(0.35f, 0.65f, 0.45f));

        play.gameObject.SetActive(false);
        finish.gameObject.SetActive(false);
    }

    static RectTransform Panel(string name, Transform parent, Color bg)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        Stretch(rt, Vector2.zero, Vector2.one);
        go.GetComponent<Image>().color = bg;
        return rt;
    }

    static Text Text(RectTransform parent, string name, string content, int size,
        TextAnchor anchor, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<Text>();
        t.font = UiFont();
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        Stretch(go.GetComponent<RectTransform>(), min, max);
        return t;
    }

    static Button Button(RectTransform parent, string name, string label,
        Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        Stretch(go.GetComponent<RectTransform>(), min, max);

        var text = Text(go.GetComponent<RectTransform>(), "Label", label, 28,
            TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        text.color = Color.white;

        return go.GetComponent<Button>();
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static Font s_font;

    static Font UiFont()
    {
        if (s_font != null) return s_font;

        // 2022 內建字型；中文靠系統字型 fallback（TMP 沒有 CJK 字型資產）
        s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_font == null) s_font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return s_font;
    }

    /// <summary>站點旁邊的立牌文字（world space canvas）。</summary>
    static void Label(Transform parent, Mats mats, string text, Vector3 localPos)
    {
        var go = new GameObject("Label_" + text, typeof(Canvas));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260f, 70f);
        rt.localScale = Vector3.one * 0.0012f;

        var t = Text(rt, "Text", text, 46, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        t.color = Color.white;
    }

    // ------------------------------------------------------------------
    // 小工具
    // ------------------------------------------------------------------

    static GameObject StationRoot(string name, Transform parent, Vector3 localPos,
        Vector3 size, bool onTable = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = onTable
            ? localPos + new Vector3(0f, k_TableTop + size.y * 0.5f, 0f)
            : localPos;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        return go;
    }

    static Renderer StationPad(Transform parent, Mats mats, float size)
    {
        var pad = Prim(PrimitiveType.Cube, "Pad", parent,
            new Vector3(0f, -0.14f, 0f), new Vector3(size, 0.015f, size), mats.Zone);
        pad.GetComponent<Collider>().enabled = false;
        return pad.GetComponent<Renderer>();
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent,
        Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>demo 用的一組灰盒材質，重建時沿用同一批 asset。</summary>
    class Mats
    {
        public readonly Material Table, TableLeg, Zone, Bucket, Bowl, Mold, Stamp,
            Oven, OvenDoor, Tray, Dough, FillingDefault, EggWash, Brush;

        public Mats()
        {
            EnsureFolder(k_MatFolder);

            Table = Make("Table", new Color(0.72f, 0.62f, 0.50f));
            TableLeg = Make("TableLeg", new Color(0.45f, 0.38f, 0.30f));
            Zone = Make("Zone", new Color(0.55f, 0.55f, 0.60f));
            Bucket = Make("Bucket", new Color(0.80f, 0.80f, 0.84f));
            Bowl = Make("Bowl", new Color(0.90f, 0.90f, 0.92f));
            Mold = Make("Mold", new Color(0.65f, 0.50f, 0.30f));
            Stamp = Make("Stamp", new Color(0.75f, 0.20f, 0.18f));
            Oven = Make("Oven", new Color(0.35f, 0.35f, 0.38f));
            OvenDoor = Make("OvenDoor", new Color(0.28f, 0.28f, 0.32f));
            Tray = Make("Tray", new Color(0.55f, 0.55f, 0.58f));
            Dough = Make("Dough", new Color(0.93f, 0.89f, 0.78f));
            FillingDefault = Make("FillingDefault", new Color(0.42f, 0.22f, 0.12f));
            EggWash = Make("EggWash", new Color(0.95f, 0.75f, 0.30f));
            Brush = Make("Brush", new Color(0.60f, 0.45f, 0.25f));
        }

        public Material Make(string name, Color color)
        {
            var path = k_MatFolder + "/M_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                mat = new Material(DefaultShader());
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Shader DefaultShader()
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            return urp != null ? urp : Shader.Find("Standard");
        }
    }
}
