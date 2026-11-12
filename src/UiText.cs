using System.Collections.Generic;

namespace PerpetualResidentDreamLarvaEpidemic
{
    internal static class UiText
    {
        // The English text is the fallback; language codes match Elin and Stolen Goods Filter.
        internal static readonly Dictionary<string, string[]> Translations = new Dictionary<string, string[]>
        {
            { "Epidemic Config", new[] { "幼虫の設定", "幼虫设置" } },
            { "Perpetual Resident Dream Larva Epidemic", new[] { "住民の夢の幼虫・自動補充", "居民梦之幼虫自动补充" } },
            { "Keeps your chosen item topped up. Choose how many each person gets below.", new[] { "選んだアイテムを補充します。1人あたりの個数を下で設定できます。", "自动补充所选物品。在下方设置每人持有的数量。" } },
            { "Enable item replenishment", new[] { "自動補充を有効にする", "开启自动补充" } },
            { "Item settings", new[] { "アイテム設定", "物品设置" } },
            { "Changes apply now and save automatically. Existing extras stay in your bags.", new[] { "変更はすぐに反映・保存されます。余分なアイテムは削除しません。", "更改立即生效并自动保存，不会移除已有的多余物品。" } },
            { "Item ID: ", new[] { "アイテムID：", "物品ID：" } },
            { "Item ID", new[] { "アイテムID", "物品ID" } },
            { "Item count: ", new[] { "個数：", "数量：" } },
            { "Item count (1–2147483647)", new[] { "個数（1～2147483647）", "数量（1～2147483647）" } },
            { "Enter a positive whole number up to 2147483647. Settings unchanged.", new[] { "1～2147483647の整数を入力してください。設定は変更していません。", "请输入1至2147483647之间的整数。设置未更改。" } },
            { "Also supply party allies", new[] { "パーティの仲間にも補充する", "也为队友补充" } },
            { "Recipients", new[] { "補充対象", "补充对象" } },
            { "Choose who gets items. These choices also apply to party allies. Guests and livestock are excluded.", new[] { "補充する相手を選びます。仲間にも適用されます。客と家畜は対象外です。", "选择补充对象，也适用于队友。访客和牲畜除外。" } },
            { "Female", new[] { "女性", "女性" } },
            { "Male", new[] { "男性", "男性" } },
            { "???", new[] { "???", "???" } },
            { "Also supply the player", new[] { "プレイヤーにも補充する", "也为玩家补充" } },
            { "Use Allow item replenishment on the resident board to opt individuals out, including while in your party. Those choices are saved per world.", new[] { "住民ボードの「自動補充を許可」で個別に除外できます。仲間になっている間も有効です。この設定はワールドごとに保存されます。", "在居民面板中取消“允许自动补充”可单独排除某人，入队后也有效。此设置按世界保存。" } },
            { "Existing matching items count, including accessible carried bags. Uses normal stacking. If no stack or free slot is available, quietly skips that recipient. Existing extras are never removed.", new[] { "中身を確認できる携帯バッグ内も数えます。通常のスタック処理を使います。重ねられず空き枠もなければ静かにスキップします。余分な物は削除しません。", "会计入随身可查看容器中的同类物品，并使用正常堆叠。无法堆叠且没有空位时会安静跳过，不会移除多余物品。" } },
            { "Disabling stops replenishment. Ordinary items already supplied remain in your save.", new[] { "オフにすると補充が止まります。補充済みのアイテムは残ります。", "关闭后停止补充，已经获得的物品会保留。" } },
            { "Eligible recipients here (including player): ", new[] { "このエリアの対象者数（プレイヤーを含む）：", "当前区域符合条件的人数（含玩家）：" } },
            { "Item replenishment: ", new[] { "自動補充：", "自动补充：" } },
            { "On", new[] { "オン", "开启" } },
            { "Off", new[] { "オフ", "关闭" } },
            { "Allow item replenishment", new[] { "自動補充を許可", "允许自动补充" } },
            { "Keep the configured item count topped up. Gender filters must also match. Existing items remain when turned off.", new[] { "設定した個数まで補充します。性別の選択にも一致する必要があります。オフにしても既存のアイテムは残ります。", "补充至设定数量，同时需符合性别筛选。关闭后保留已有物品。" } },
            { "Replenishment paused after an error. See BepInEx log.", new[] { "エラーのため補充を停止しました。BepInExのログを確認してください。", "出现错误，补充已暂停。请查看BepInEx日志。" } },
            { "Unknown item ID. Settings unchanged.", new[] { "アイテムIDが見つかりません。設定は変更していません。", "找不到该物品ID。设置未更改。" } },
            { "Item count must be a positive whole number. Settings unchanged.", new[] { "個数は1以上の整数にしてください。設定は変更していません。", "数量必须是正整数。设置未更改。" } },
            { "Could not save settings. Settings unchanged. See BepInEx log.", new[] { "設定を保存できませんでした。変更は反映していません。BepInExのログを確認してください。", "无法保存设置，更改未生效。请查看BepInEx日志。" } },
            { "Saved and applied.", new[] { "保存して反映しました。", "已保存并生效。" } },
            { "Unknown item ID. Replenishment is paused.", new[] { "アイテムIDが見つからないため、補充を停止しました。", "找不到物品ID，补充已暂停。" } }
        };

        internal static string Get(string languageCode, string text)
        {
            if (text == null) return "";
            int index = languageCode == "JP" ? 0 : languageCode == "CN" ? 1 : -1;
            return index >= 0 && Translations.TryGetValue(text, out var values) ? values[index] : text;
        }
    }
}
