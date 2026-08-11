using System.Collections.Generic;
using I2.Loc;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把第一顆教學的 12 段文字寫進 I2 的 language source（Assets/Resources/I2Languages.asset）。
///
/// term 命名：Mooncake/Tutorial/&lt;英文步驟名&gt;_Title / _Body，跟
/// <see cref="MooncakeTutorial.TitleTerm"/> 對得起來。
/// 內文可用 {pats}、{wraps}、{count} 這些代號，執行時會換成實際數字。
/// </summary>
public static class MooncakeTutorialLocalization
{
    const string k_SourcePath = "Assets/Resources/I2Languages.asset";

    // 順序要跟 MooncakeSettings.languages 一致，之後用索引切語言才對得上
    static readonly string[] k_Languages = { "English", "Chinese", "Japanese", "Korean", "Indonesian" };
    static readonly string[] k_Codes = { "en", "zh-TW", "ja", "ko", "id" };

    [MenuItem("Tools/月餅 Demo/寫入教學翻譯 (I2)")]
    public static void WriteMenu()
    {
        Write(false);
    }

    [MenuItem("Tools/月餅 Demo/強制覆寫教學翻譯 (I2)")]
    public static void OverwriteMenu()
    {
        if (!EditorUtility.DisplayDialog("覆寫教學翻譯",
            "會把 Mooncake/Tutorial/ 底下所有 term 的翻譯覆蓋成內建版本，手動改過的內容會不見。要繼續嗎？",
            "覆寫", "取消"))
            return;

        Write(true);
    }

    /// <summary>建置教學時順手叫一次：只補空的，不動已經有的翻譯。</summary>
    public static void EnsureTerms()
    {
        Write(false);
    }

    static void Write(bool overwrite)
    {
        var asset = AssetDatabase.LoadAssetAtPath<LanguageSourceAsset>(k_SourcePath);
        if (asset == null)
        {
            Debug.LogError($"[月餅教學] 找不到 {k_SourcePath}，先建一個 I2 Language Source 再跑一次");
            return;
        }

        var source = asset.SourceData;

        // 語言
        int addedLanguages = 0;
        for (int i = 0; i < k_Languages.Length; i++)
        {
            if (source.GetLanguageIndex(k_Languages[i], false, false) >= 0) continue;

            source.AddLanguage(k_Languages[i], k_Codes[i]);
            addedLanguages++;
        }

        // 每個語言在這份 source 裡實際的欄位索引
        var slot = new int[k_Languages.Length];
        for (int i = 0; i < k_Languages.Length; i++)
            slot[i] = source.GetLanguageIndex(k_Languages[i], false, false);

        int addedTerms = 0, wrote = 0, kept = 0;

        foreach (var row in BuildRows())
        {
            addedTerms += Apply(source, MooncakeTutorial.TitleTerm(row.step), row.titles, slot, overwrite, ref wrote, ref kept);
            addedTerms += Apply(source, MooncakeTutorial.BodyTerm(row.step), row.bodies, slot, overwrite, ref wrote, ref kept);
        }

        source.UpdateDictionary(true);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[月餅教學] I2 翻譯寫入完成：新增語言 {addedLanguages} 個、新增 term {addedTerms} 個、" +
                  $"寫入翻譯 {wrote} 筆、保留既有 {kept} 筆。\n" +
                  (kept > 0 && !overwrite ? "（要一併蓋掉既有內容請用「強制覆寫教學翻譯 (I2)」）\n" : "") +
                  "註：日文假名、韓文諺文不在 NotoSansTC 的涵蓋範圍，記得在「TMP 中文字型」的 fallback 清單補上 Noto Sans JP / KR。");
    }

    /// <summary>寫一個 term 的所有語言；回傳這個 term 是不是新加的（1／0）。</summary>
    static int Apply(LanguageSourceData source, string term, string[] values, int[] slot,
                     bool overwrite, ref int wrote, ref int kept)
    {
        int isNew = 0;

        var data = source.GetTermData(term);
        if (data == null)
        {
            data = source.AddTerm(term, eTermType.Text, false);
            isNew = 1;
        }

        // 語言可能是這次才加的，term 的欄位要跟著長
        if (data.Languages.Length < source.mLanguages.Count)
        {
            System.Array.Resize(ref data.Languages, source.mLanguages.Count);
            System.Array.Resize(ref data.Flags, source.mLanguages.Count);
        }

        for (int i = 0; i < values.Length && i < slot.Length; i++)
        {
            int index = slot[i];
            if (index < 0 || index >= data.Languages.Length) continue;

            bool empty = string.IsNullOrEmpty(data.Languages[index]);
            if (!empty && !overwrite)
            {
                if (data.Languages[index] != values[i]) kept++;
                continue;
            }

            data.Languages[index] = values[i];
            wrote++;
        }

        return isNew;
    }

    // ------------------------------------------------------------------
    // 文案（順序：English / Chinese / Japanese / Korean / Indonesian）
    // ------------------------------------------------------------------

    struct Row
    {
        public MooncakeTutorial.Step step;
        public string[] titles;
        public string[] bodies;
    }

    static IEnumerable<Row> BuildRows()
    {
        yield return new Row
        {
            step = MooncakeTutorial.Step.抓麵團,
            titles = new[] { "① Take some dough", "① 拿一團麵團", "① 生地を取る", "① 반죽 집기", "① Ambil adonan" },
            bodies = new[]
            {
                "Reach for the dough ball and press the trigger to grab a piece.",
                "把手伸到麵團球上，按下扳機鍵抓一團麵團出來。",
                "生地の玉に手を伸ばし、トリガーを押して一つかみ取ります。",
                "반죽 덩어리에 손을 뻗어 트리거를 눌러 한 덩이를 집으세요.",
                "Arahkan tangan ke bola adonan lalu tekan trigger untuk mengambil sepotong."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.放上檯面,
            titles = new[] { "② Put it on the table", "② 放到檯面上", "② 台の上に置く", "② 작업대에 놓기", "② Letakkan di meja" },
            bodies = new[]
            {
                "Place the dough on the glowing spot on the table.",
                "把手裡的麵團，放到檯面上發亮的位置。",
                "手に持った生地を、光っている場所に置きます。",
                "손에 든 반죽을 작업대에서 빛나는 자리에 놓으세요.",
                "Taruh adonan di titik yang menyala pada meja."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.拍打麵團,
            titles = new[] { "③ Flatten the dough", "③ 把麵團拍扁", "③ 生地を平らにする", "③ 반죽 납작하게", "③ Pipihkan adonan" },
            bodies = new[]
            {
                "Pat the dough {pats} times until it becomes a flat wrapper.",
                "用手輕拍麵團 {pats} 下，把它拍成一張餅皮。",
                "生地を {pats} 回たたいて、平らな皮にします。",
                "반죽을 {pats}번 두드려 납작한 피로 만드세요.",
                "Tepuk adonan {pats} kali sampai menjadi lembaran kulit."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.挑餡料,
            titles = new[] { "④ Pick a filling", "④ 挑一種餡料", "④ 餡を選ぶ", "④ 소 고르기", "④ Pilih isian" },
            bodies = new[]
            {
                "The bowls hold lotus seed, red bean, butter and chocolate. Grab whichever you like.",
                "旁邊的碗裡有蓮子、紅豆、奶油、巧克力，任選一種抓起來。",
                "隣の器に蓮の実、あずき、バター、チョコレートがあります。好きなものを掴んでください。",
                "옆 그릇에 연밥, 팥, 버터, 초콜릿이 있습니다. 원하는 것을 집으세요.",
                "Di mangkuk ada biji teratai, kacang merah, mentega, dan cokelat. Ambil salah satu."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.放入餡料,
            titles = new[] { "⑤ Add the filling", "⑤ 把餡放進餅皮", "⑤ 餡を入れる", "⑤ 소 넣기", "⑤ Masukkan isian" },
            bodies = new[]
            {
                "Drop the filling into the dent in the middle of the wrapper.",
                "把手上的餡料，放到餅皮中央的凹槽裡。",
                "手に持った餡を、皮の中央のくぼみに入れます。",
                "손에 든 소를 피 가운데 홈에 넣으세요.",
                "Taruh isian ke cekungan di tengah kulit."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.包起來,
            titles = new[] { "⑥ Wrap it up", "⑥ 把餡包起來", "⑥ 包む", "⑥ 감싸기", "⑥ Bungkus isian" },
            bodies = new[]
            {
                "Pat it {wraps} more times to close the wrapper around the filling.",
                "再輕拍 {wraps} 下，讓餅皮慢慢收口包住餡料。",
                "さらに {wraps} 回たたいて、皮で餡を包みます。",
                "{wraps}번 더 두드려 피로 소를 감싸세요.",
                "Tepuk {wraps} kali lagi supaya kulit menutup isian."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.放進模具,
            titles = new[] { "⑦ Into the mold", "⑦ 放進模具", "⑦ 型に入れる", "⑦ 틀에 넣기", "⑦ Masukkan ke cetakan" },
            bodies = new[]
            {
                "Pick up the finished dough and press it into the mooncake mold.",
                "拿起做好的麵團，塞進月餅模具裡壓出花紋。",
                "出来た生地を持ち上げ、月餅の型に押し込んで模様をつけます。",
                "완성된 반죽을 들어 월병 틀에 눌러 무늬를 찍으세요.",
                "Angkat adonan yang sudah jadi dan tekan ke dalam cetakan kue bulan."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.放上烤盤,
            titles = new[] { "⑧ Press onto the tray", "⑧ 壓到烤盤上", "⑧ 天板に押す", "⑧ 팬에 찍기", "⑧ Tekan ke loyang" },
            bodies = new[]
            {
                "Hold the mold over a glowing slot on the tray and press down. This tray takes {count}.",
                "握著模具，對準烤盤上發亮的格子壓下去。這一盤要做 {count} 顆。",
                "型を持ち、天板の光っているマスに合わせて押し付けます。この天板には {count} 個です。",
                "틀을 잡고 팬에서 빛나는 칸에 맞춰 눌러 주세요. 이 팬에는 {count}개가 들어갑니다.",
                "Pegang cetakan, arahkan ke kotak yang menyala di loyang, lalu tekan. Loyang ini butuh {count} buah."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.烤盤進烤箱,
            titles = new[] { "⑨ Into the oven", "⑨ 送進烤箱", "⑨ オーブンへ", "⑨ 오븐에 넣기", "⑨ Masukkan ke oven" },
            bodies = new[]
            {
                "All {count} are ready. Pick up the whole tray and slide it into the oven.",
                "{count} 顆都排好了，把整個烤盤端起來，放進烤箱裡面。",
                "{count} 個そろいました。天板ごと持ち上げてオーブンに入れます。",
                "{count}개가 모두 준비됐습니다. 팬째 들어 오븐에 넣으세요.",
                "{count} sudah tersusun. Angkat loyangnya dan masukkan ke dalam oven."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.刷蛋液,
            titles = new[] { "⑩ Brush on egg wash", "⑩ 刷上蛋液", "⑩ 卵液を塗る", "⑩ 달걀물 바르기", "⑩ Oles kuning telur" },
            bodies = new[]
            {
                "First bake is done! Pick up the brush and paint every mooncake once.",
                "第一輪烤好了！拿起刷子，在每一顆月餅上都刷過一次。",
                "一度目の焼き上がりです！刷毛を持って、月餅すべてに一度ずつ塗ります。",
                "1차 굽기 완료! 붓을 들고 월병마다 한 번씩 발라 주세요.",
                "Panggangan pertama selesai! Ambil kuasnya dan oles setiap kue sekali."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.再送烤箱,
            titles = new[] { "⑪ Bake again", "⑪ 再烤一次", "⑪ もう一度焼く", "⑪ 한 번 더 굽기", "⑪ Panggang lagi" },
            bodies = new[]
            {
                "The egg wash is done. Put the tray back in the oven until golden.",
                "蛋液都刷好了，把烤盤再送回烤箱，烤出金黃色。",
                "卵液が塗れました。天板をオーブンに戻して、きつね色に焼きます。",
                "달걀물을 다 발랐습니다. 팬을 다시 오븐에 넣어 노릇하게 구우세요.",
                "Olesannya selesai. Masukkan kembali loyang ke oven sampai keemasan."
            }
        };

        yield return new Row
        {
            step = MooncakeTutorial.Step.完成,
            titles = new[] { "Done!", "完成！", "完成！", "완성!", "Selesai!" },
            bodies = new[]
            {
                "The mooncakes are out of the oven. Careful, they are hot.",
                "月餅出爐囉，小心燙。",
                "月餅が焼き上がりました。熱いので気をつけて。",
                "월병이 나왔습니다. 뜨거우니 조심하세요.",
                "Kue bulannya sudah matang. Hati-hati, masih panas."
            }
        };
    }
}
