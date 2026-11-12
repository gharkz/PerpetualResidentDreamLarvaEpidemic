using HarmonyLib;
using UnityEngine;

namespace PerpetualResidentDreamLarvaEpidemic
{
    [HarmonyPatch(typeof(BaseListPeople), nameof(BaseListPeople.OnInstantiate))]
    internal static class PerpetualResidentDreamLarvaEpidemicBoard
    {
        private static string T(string text) => UiText.Get(Lang.langCode, text);
        private const string SlotId = "perpetualResidentDreamLarvaEpidemicSupply";

        [HarmonyPostfix]
        private static void AddSupplyToggle(BaseListPeople __instance, Chara a, ItemGeneral b)
        {
            if (!(__instance is ListPeople) || PerpetualResidentDreamLarvaEpidemicPlugin.Instance == null) return;
            // Reserve a slot on every row, including hidden/ineligible rows, because rows are recycled.
            var slot = b.AddSubButton(null, () => { }, null, null, SlotId);
            bool eligible = PerpetualResidentDreamLarvaEpidemicPlugin.IsResident(a);
            slot.gameObject.SetActive(eligible);
            if (!eligible) return;
            var child = slot.transform.Find("PerpetualResidentDreamLarvaEpidemicCheckbox");
            var checkbox = child ? child.GetComponent<UIButton>() :
                Util.Instantiate<UIButton>("UI/Element/Button/ButtonToggle", slot.transform);
            checkbox.name = "PerpetualResidentDreamLarvaEpidemicCheckbox";
            checkbox.mainText.gameObject.SetActive(false);
            var rect = checkbox.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            slot.onClick.RemoveAllListeners();
            var setting = PerpetualResidentDreamLarvaEpidemicPlugin.Instance.ResidentPreference(a);
            checkbox.SetToggle(setting.Value, value =>
            {
                setting.Value = value;
                PerpetualResidentDreamLarvaEpidemicPlugin.Instance.Config.Save();
            });
            checkbox.SetTooltip(t =>
            {
                t.note.Clear();
                t.note.AddHeader(T("Allow item replenishment"));
                t.note.AddText(a.Name + ": " + (setting.Value ? T("On") : T("Off")));
                t.note.AddText(T("Keep the configured item count topped up. Gender filters must also match. Existing items remain when turned off."));
                t.note.Build();
            });
        }
    }
}
