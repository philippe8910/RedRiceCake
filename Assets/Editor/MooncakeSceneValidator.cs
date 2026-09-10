using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 兩個實際遊玩場景（中式 / 印尼）的無頭接線檢查。
///
/// 檢查的是「一條龍走得完」需要的每個環節：麵團球 → 交接提示 → 壓扁站 →
/// 可抓麵團 → 模具／印章 → 烤盤三格 → 烤箱 → 出爐，
/// 以及串起這條鏈的 Tag（MooncakePiece → Mold）與烘烤輪數是否對得上流程表。
///
/// 用法：選單 Tools/月餅/驗證兩個場景，或
/// -executeMethod MooncakeSceneValidator.ValidateFromCommandLine
///
/// 注意：舊的 MooncakeDemoValidator 驗的是已經不存在的 MooncakeDemo.unity
/// （MooncakeFlow / MooncakeStation 那套原型），與這兩個場景無關。
/// </summary>
public static class MooncakeSceneValidator
{
    struct SceneSpec
    {
        public string path;
        public MooncakeType type;
        public string label;
    }

    static readonly SceneSpec[] k_Scenes =
    {
        new SceneSpec { path = "Assets/Scenes/ChineseMooncakeDemo.unity",    type = MooncakeType.Chinese,    label = "中式" },
        new SceneSpec { path = "Assets/Scenes/IndonesianMooncakeDemo.unity", type = MooncakeType.Indonesian, label = "印尼" },
    };

    static readonly List<string> s_errors = new List<string>();
    static readonly StringBuilder s_log = new StringBuilder();
    static string s_prefix = "";

    [MenuItem("Tools/月餅/驗證兩個場景")]
    public static void ValidateMenu()
    {
        Run();
    }

    public static void ValidateFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Run()
    {
        s_errors.Clear();
        s_log.Length = 0;

        CheckBuildSettings();

        foreach (var spec in k_Scenes)
            ValidateScene(spec);

        s_log.AppendLine(s_errors.Count == 0
            ? "[月餅] 場景驗證通過 ALL CHECKS PASSED"
            : "[月餅] 場景驗證失敗，共 " + s_errors.Count + " 項");

        foreach (var e in s_errors) s_log.AppendLine("  X " + e);

        Debug.Log(s_log.ToString());
        return s_errors.Count == 0;
    }

    static void Err(string msg)
    {
        s_errors.Add(s_prefix + msg);
    }

    static void Note(string msg)
    {
        s_log.AppendLine(msg);
    }

    // ---------------- Build Settings ----------------

    static void CheckBuildSettings()
    {
        var wanted = new[]
        {
            "Assets/Scenes/MooncakeSplash.unity",
            "Assets/Scenes/MooncakeMainMenu.unity",
            "Assets/Scenes/ChineseMooncakeDemo.unity",
            "Assets/Scenes/IndonesianMooncakeDemo.unity",
        };

        s_prefix = "[Build Settings] ";
        foreach (var path in wanted)
        {
            bool found = false;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == path && s.enabled) found = true;

            if (!found) Err("場景「" + path + "」沒有加進 Build Settings（或被停用）");
        }
        s_prefix = "";
    }

    // ---------------- 單一場景 ----------------

    static void ValidateScene(SceneSpec spec)
    {
        s_prefix = "[" + spec.label + "] ";

        var scene = EditorSceneManager.OpenScene(spec.path, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Err("打不開場景 " + spec.path);
            s_prefix = "";
            return;
        }

        var flow = FindInScene<MooncakeChineseFlow>(scene);
        if (flow == null)
        {
            Err("場景裡沒有 MooncakeChineseFlow");
            s_prefix = "";
            return;
        }

        Note("── " + spec.label + "月餅 " + spec.path + " ──");

        var mold = CheckFlowWiring(flow);
        CheckDoughChain(flow, mold);
        CheckMold(mold);
        CheckTraySlots(flow, mold);
        CheckOven(flow);
        CheckBakeRounds(flow, spec.type);

        s_prefix = "";
    }

    /// <summary>流程控制上每一個必填欄位。這裡漏一個就會有一段流程走不下去。</summary>
    static MooncakeMoldStation CheckFlowWiring(MooncakeChineseFlow flow)
    {
        if (flow.doughBall == null) Err("流程控制沒有接上麵團球（doughBall）");
        if (flow.doughHint == null) Err("流程控制沒有接上麵團交接提示（doughHint）");
        if (flow.flattenStation == null) Err("流程控制沒有接上壓扁站（flattenStation）");
        if (flow.bakingPan == null) Err("流程控制沒有接上烤盤（bakingPan）");
        if (flow.ovenStation == null) Err("流程控制沒有接上烤箱（ovenStation）");
        if (flow.celebration == null) Err("流程控制沒有接上過關彩帶（celebration）");
        if (flow.doughPiecePrefab == null) Err("流程控制沒有接上可抓麵團 Prefab（doughPiecePrefab）");

        // 這一格是「模具／印章」，兩個場景都必填：
        // 少了它，包餡完成後生出來的麵團無處可放，烤盤永遠湊不滿三顆，流程就停在這裡。
        if (flow.moldStation == null)
        {
            Err("流程控制沒有接上模具／印章站（moldStation）——包餡之後的流程會整段斷掉");
            return null;
        }

        if (!flow.moldStation.gameObject.activeInHierarchy)
            Err("模具／印章站「" + flow.moldStation.name + "」開場是關著的");

        return flow.moldStation;
    }

    /// <summary>麵團球 → 交接提示 → 壓扁站 → 可抓麵團 → 模具接收器，Tag 要一路對得起來。</summary>
    static void CheckDoughChain(MooncakeChineseFlow flow, MooncakeMoldStation mold)
    {
        if (flow.doughBall != null && flow.doughBall.spawnPrefab == null)
            Err("麵團球沒有接上要生出來的 Prefab");

        if (flow.doughHint != null)
        {
            if (flow.doughHint.socket == null && flow.doughHint.GetComponent<MooncakeDropSocket>() == null)
                Err("麵團交接提示沒有接收器（socket）");
            if (flow.doughHint.targetObject == null)
                Err("麵團交接提示沒有指定收到麵團後要打開的物件（targetObject）");
        }

        var flatten = flow.flattenStation;
        if (flatten != null)
        {
            if (flatten.hintObject == null) Err("壓扁站沒有接上內陷提示（hintObject）");
            if (flatten.fillingObject == null) Err("壓扁站沒有接上內陷（fillingObject）");

            var socket = flatten.socket != null
                ? flatten.socket
                : (flatten.hintObject != null ? flatten.hintObject.GetComponentInChildren<MooncakeDropSocket>(true) : null);
            if (socket == null) Err("壓扁站找不到餡料接收器（socket）");

            var skinned = flatten.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null)
            {
                Err("壓扁站底下沒有 SkinnedMeshRenderer，BlendShape 動不了");
            }
            else
            {
                if (MooncakeBlendShapeUtil.ResolveIndex(skinned, flatten.ballShapeName, flatten.ballShapeIndexOverride) < 0)
                    Err("壓扁站找不到 BlendShape「" + flatten.ballShapeName + "」");
                if (MooncakeBlendShapeUtil.ResolveIndex(skinned, flatten.flatShapeName, flatten.flatShapeIndexOverride) < 0)
                    Err("壓扁站找不到 BlendShape「" + flatten.flatShapeName + "」");
            }
        }

        // 包餡完成後生出來的麵團，Tag 必須是模具接收器收得到的那一種
        if (mold != null && flow.doughPiecePrefab != null)
        {
            var socket = ResolveMoldSocket(mold);
            if (socket != null && flow.doughPiecePrefab.tag != socket.acceptTag)
                Err("可抓麵團的 Tag 是「" + flow.doughPiecePrefab.tag +
                    "」，模具接收器只收「" + socket.acceptTag + "」");
        }
    }

    static MooncakeDropSocket ResolveMoldSocket(MooncakeMoldStation mold)
    {
        if (mold == null) return null;
        if (mold.socket != null) return mold.socket;
        return mold.hintObject != null ? mold.hintObject.GetComponentInChildren<MooncakeDropSocket>(true) : null;
    }

    /// <summary>模具／印章自己的零件：提示、完成體、接收器、可抓取需要的物理元件。</summary>
    static void CheckMold(MooncakeMoldStation mold)
    {
        if (mold == null) return;

        if (mold.hintObject == null) Err("模具／印章沒有接上提示（hintObject）");
        else if (!mold.hintObject.transform.IsChildOf(mold.transform))
            Err("模具／印章的提示「" + mold.hintObject.name + "」不在它底下，拿起來不會跟著走");

        if (mold.moldedObject == null) Err("模具／印章沒有接上完成體（moldedObject）");
        else if (!mold.moldedObject.transform.IsChildOf(mold.transform))
            Err("模具／印章的完成體「" + mold.moldedObject.name + "」不在它底下，拿起來不會跟著走");

        if (ResolveMoldSocket(mold) == null) Err("模具／印章找不到麵團接收器（socket）");

        if (mold.GetComponent<Collider>() == null) Err("模具／印章身上沒有 Collider，抓不到也碰不到烤盤");
        if (mold.GetComponent<Rigidbody>() == null) Err("模具／印章身上沒有 Rigidbody");
        if (mold.GetComponent<XRGrabInteractable>() == null) Err("模具／印章身上沒有 XRGrabInteractable，VR 裡拿不起來");
    }

    /// <summary>烤盤三格：接收 Tag 要對得上模具的 Tag，生／熟兩個造型都要在。</summary>
    static void CheckTraySlots(MooncakeChineseFlow flow, MooncakeMoldStation mold)
    {
        var slots = flow.traySlots;
        if (slots == null || slots.Length == 0)
        {
            Err("流程控制沒有接上烤盤欄位（traySlots）");
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null)
            {
                Err("烤盤第 " + (i + 1) + " 格是空的");
                continue;
            }

            if (slot.placedObject == null) Err(slot.name + " 沒有接上生的月餅（placedObject）");
            if (slot.bakedObject == null) Err(slot.name + " 沒有接上烤過的月餅（bakedObject）");
            if (slot.GetComponent<SphereCollider>() == null) Err(slot.name + " 身上沒有 SphereCollider，碰不到");

            // 兩種流程要檢查的東西不一樣：
            //   中式：握著模具去壓烤盤 ⇒ 格子要收模具的 Tag
            //   印尼：月餅蓋完會脫離印章、玩家用手拿 ⇒ 格子要收脫離後那顆的 Tag
            var station = mold != null ? mold.GetComponent<MooncakeMoldStation>() : null;

            if (station != null && station.detachAfterStamp)
            {
                if (slot.acceptTag != station.detachedTag)
                    Err(slot.name + " 只收 Tag「" + slot.acceptTag +
                        "」，但脫離後的月餅 Tag 是「" + station.detachedTag + "」");

                if (slot.requireMooncakeOnMold)
                    Err(slot.name + " 還開著 requireMooncakeOnMold，但月餅已經脫離印章了，會收不到");
            }
            else if (mold != null && slot.acceptTag != mold.tag)
            {
                Err(slot.name + " 只收 Tag「" + slot.acceptTag +
                    "」，但模具／印章的 Tag 是「" + mold.tag + "」");
            }
        }

        if (flow.bakingPan != null && flow.bakingPan.grabbableAtStart)
            Err("烤盤開場就可以抓，應該等三顆放滿才開放");
    }

    static void CheckOven(MooncakeChineseFlow flow)
    {
        var oven = flow.ovenStation;
        if (oven == null) return;

        if (oven.ovenZone == null) Err("烤箱沒有接上內部偵測區（ovenZone）");
        if (oven.panAnchor == null) Err("烤箱沒有接上烤盤定位（panAnchor）");
        if (oven.countdown == null) Err("烤箱沒有接上倒數計時器（countdown）");

        if (oven.door == null && oven.doorSearchRoot == null)
            Err("烤箱既沒有指定門，也沒有指定往下找門的根物件");
        else if (oven.door == null)
        {
            bool found = false;
            foreach (var t in oven.doorSearchRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == oven.doorName) found = true;
            if (!found) Err("在「" + oven.doorSearchRoot.name + "」底下找不到烤箱門「" + oven.doorName + "」");
        }

        if (flow.celebration != null &&
            (flow.celebration.confettiPrefabs == null || flow.celebration.confettiPrefabs.Length == 0))
            Err("過關彩帶沒有接上任何 Prefab");
    }

    /// <summary>烘烤輪數要跟流程表對得上，刷蛋液的道具也要跟著在／不在。</summary>
    static void CheckBakeRounds(MooncakeChineseFlow flow, MooncakeType type)
    {
        int recipeBakes = 0;
        foreach (var entry in MooncakeRecipes.For(type))
            if (entry.Step == MooncakeStep.Bake) recipeBakes++;

        if (flow.bakesBeforeDone != recipeBakes)
            Err("烘烤輪數 bakesBeforeDone = " + flow.bakesBeforeDone +
                "，但流程表裡有 " + recipeBakes + " 段烘烤");

        bool recipeHasEggWash = false;
        foreach (var entry in MooncakeRecipes.For(type))
            if (entry.Step == MooncakeStep.EggWash) recipeHasEggWash = true;

        // 刷蛋液只在「還有下一輪要烤」時才會被開放（見 MooncakeChineseFlow.HandleBakeComplete）
        bool sceneHasEggWash = flow.bakesBeforeDone > 1;
        if (recipeHasEggWash != sceneHasEggWash)
            Err(recipeHasEggWash
                ? "流程表有刷蛋液，但場景只烤一輪，刷蛋液整段不會被觸發"
                : "流程表沒有刷蛋液，但場景設定成要烤兩輪");

        if (sceneHasEggWash && flow.traySlots != null)
        {
            foreach (var slot in flow.traySlots)
                if (slot != null && slot.eggWashSocket == null)
                    Err(slot.name + " 要刷蛋液卻沒有接上蛋液接收器（eggWashSocket）");
        }

        Note("  烘烤 " + flow.bakesBeforeDone + " 輪，刷蛋液=" + sceneHasEggWash +
             "，烤盤 " + (flow.traySlots != null ? flow.traySlots.Length : 0) + " 格");
    }

    // ---------------- 小工具 ----------------

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
