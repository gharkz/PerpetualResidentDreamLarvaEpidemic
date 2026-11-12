using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PerpetualResidentDreamLarvaEpidemic
{
    [HarmonyPatch]
    internal static class PerpetualResidentDreamLarvaEpidemicTab
    {
        private const string TabId = "PerpetualResidentDreamLarvaEpidemic";
        private sealed class State
        {
            internal Window.Setting.Tab Tab;
            internal UIText Count;
            internal LayoutElement Width;
            internal bool AddedWidth;
            internal float MinWidth, PreferredWidth, FlexibleWidth;
            internal int WidthPriority;
            internal bool LayoutActive;
            internal bool ControlWidth, ExpandWidth;
            internal bool ControlHeight, ExpandHeight;
            internal float LastWidth = -1;
            internal Vector2 NoteSize;
            internal RectTransform Content, OriginalContent;
            internal Transform NoteParent;
            internal int NoteSibling;
            internal Vector2 NoteAnchorMin, NoteAnchorMax, NotePivot, NotePosition;
            internal bool OriginalContentActive;
            internal ContentSizeFitter NoteFitter;
            internal ContentSizeFitter.FitMode NoteHorizontalFit;
            internal bool TabOffsetApplied;
            internal Vector2 TabPosition;
        }

        // State belongs to the tab, so closing the window does not retain its UI objects.
        private static State GetState(Window window)
        {
            foreach (var tab in window.setting.tabs)
                if (tab.refObj is State state) return state;
            return null;
        }

        [HarmonyPatch(typeof(Window), nameof(Window.BuildTabs))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void AddTab(Window __instance)
        {
            if (!(__instance.controller is WindowChara) || __instance.setting.tabs.Count < 2) return;
            var existing = GetState(__instance);
            if (existing != null)
            {
                __instance.setting.tabs.Remove(existing.Tab);
                __instance.setting.tabs.Add(existing.Tab);
                return;
            }
            // Share the same content host used by skills; RefreshSkill supplies our own contents.
            var tab = __instance.AddTab(TabId, __instance.setting.tabs[1].content);
            tab.refObj = new State { Tab = tab };
        }

        [HarmonyPatch(typeof(Window), nameof(Window.BuildTabs))]
        [HarmonyPostfix]
        private static void LabelTab(Window __instance)
        {
            var state = GetState(__instance);
            if (state != null && state.Tab.button) state.Tab.button.mainText.text = "Epidemic Config";
            LowerTabBar(__instance);
        }

        [HarmonyPatch(typeof(Window), nameof(Window.ApplySkin))]
        [HarmonyPostfix]
        private static void LowerTabBar(Window __instance)
        {
            if (!(__instance.controller is WindowChara) || !__instance.rectTab) return;
            var state = GetState(__instance);
            if (state == null) return;
            var rect = __instance.rectTab;
            if (state.TabOffsetApplied && rect.anchoredPosition == state.TabPosition) return;
            float scale = Mathf.Max(0.01f, Mathf.Abs(rect.lossyScale.y));
            rect.anchoredPosition += Vector2.down * (5f / scale);
            state.TabPosition = rect.anchoredPosition;
            state.TabOffsetApplied = true;
        }

        [HarmonyPatch(typeof(Window), nameof(Window.OnKill))]
        [HarmonyPostfix]
        private static void RememberSafeTab(Window __instance)
        {
            var state = GetState(__instance);
            // Vanilla restores the remembered index before BuildTabs adds dynamic tabs.
            // Remember the profile instead of an index absent from the original prefab.
            if (state != null && __instance.CurrentTab == state.Tab)
                Window.dictTab[__instance.idWindow] = 0;
        }

        [HarmonyPatch(typeof(WindowChara), nameof(WindowChara.SetChara))]
        [HarmonyPostfix]
        private static void SetChara(WindowChara __instance)
        {
            var state = GetState(__instance.window);
            if (state == null) return;
            state.Tab.disable = !__instance.chara.IsPC;
            if (state.Tab.button) state.Tab.button.gameObject.SetActive(!state.Tab.disable);
            if (state.Tab.disable && IsSelected(__instance)) __instance.window.SwitchContent(0);
        }

        internal static bool IsSelected(WindowChara sheet)
        {
            var state = GetState(sheet.window);
            return state != null && sheet.window.CurrentTab == state.Tab;
        }

        [HarmonyPatch(typeof(WindowChara), nameof(WindowChara.OnSwitchContent))]
        [HarmonyPrefix]
        private static void RestoreOnTabChange(WindowChara __instance)
        {
            var state = GetState(__instance.window);
            if (state != null && __instance.window.idTab != __instance.window.setting.tabs.IndexOf(state.Tab))
                RestoreLayout(__instance, state);
        }

        [HarmonyPatch(typeof(WindowChara), nameof(WindowChara.RefreshSkill))]
        [HarmonyPrefix]
        private static bool Render(WindowChara __instance, int idTab)
        {
            var state = GetState(__instance.window);
            if (state == null) return true;
            if (__instance.window.setting.tabs.IndexOf(state.Tab) != idTab)
            {
                RestoreLayout(__instance, state);
                return true;
            }
            __instance.contentList.DestroyChildren();
            var note = __instance.note;
            note.Clear();
            note.transform.SetAsLastSibling();
            note.AddHeader(PerpetualResidentDreamLarvaEpidemicPlugin.DisplayName);
            note.AddText("Keeps your chosen item topped up. Choose how many each person gets below.");
            AddCheckbox(note, "Enable item replenishment", PerpetualResidentDreamLarvaEpidemicPlugin.Instance.SupplyEnabled, __instance);
            var plugin = PerpetualResidentDreamLarvaEpidemicPlugin.Instance;
            note.AddHeader("Item settings");
            note.AddText("Changes apply now and save automatically. Existing extras stay in your bags.");
            note.AddButton("Item ID: " + plugin.ItemId, () =>
                Dialog.InputName("Item ID", plugin.ItemId, (cancel, value) =>
                {
                    if (cancel) return;
                    plugin.ApplyItemSettings(value, plugin.ItemCount, plugin.IncludePartyAllies);
                    Refresh(__instance);
                }));
            note.AddButton("Item count: " + plugin.ItemCount, () =>
                Dialog.InputName("Item count (1–2147483647)", plugin.ItemCount.ToString(), (cancel, value) =>
                {
                    if (cancel) return;
                    if (!int.TryParse(value, out var count) || count < 1)
                        plugin.SettingsMessage = "Enter a positive whole number up to 2147483647. Settings unchanged.";
                    else plugin.ApplyItemSettings(plugin.ItemId, count, plugin.IncludePartyAllies);
                    Refresh(__instance);
                }));
            var partyToggle = note.AddToggle("", plugin.IncludePartyAllies, value =>
            {
                plugin.ApplyItemSettings(plugin.ItemId, plugin.ItemCount, value);
                Refresh(__instance);
            });
            partyToggle.mainText.text = "Also supply party allies";
            note.AddHeader("Recipients");
            note.AddText("Choose who gets items. These choices also apply to party allies. Guests and livestock are excluded.");
            note.Space();
            AddCheckbox(note, "Female", PerpetualResidentDreamLarvaEpidemicPlugin.Instance.Female, __instance);
            AddCheckbox(note, "Male", PerpetualResidentDreamLarvaEpidemicPlugin.Instance.Male, __instance);
            AddCheckbox(note, "???", PerpetualResidentDreamLarvaEpidemicPlugin.Instance.Unknown, __instance);
            note.Space();
            AddCheckbox(note, "Also supply the player", PerpetualResidentDreamLarvaEpidemicPlugin.Instance.IncludePlayer, __instance);
            note.AddText("Use Allow item replenishment on the resident board to opt individuals out, including while in your party. Those choices are saved per world.");
            note.AddText("Existing matching items count, including accessible carried bags. Uses normal stacking. If no stack or free slot is available, quietly skips that recipient. Existing extras are never removed.");
            note.AddText("Disabling stops replenishment. Ordinary items already supplied remain in your save.");
            state.Count = note.AddText("").text1;

            UpdateCount(__instance);
            FitLayout(__instance, state);
            note.Build();
            __instance.scrollSkill.Refresh();
            return false;
        }

        private static void FitLayout(WindowChara sheet, State state)
        {
            var note = sheet.note;
            var group = note.layout as HorizontalOrVerticalLayoutGroup;
            if (!state.LayoutActive)
            {
                var rect = note.GetComponent<RectTransform>();
                state.NoteParent = rect.parent;
                state.NoteSibling = rect.GetSiblingIndex();
                state.NoteAnchorMin = rect.anchorMin;
                state.NoteAnchorMax = rect.anchorMax;
                state.NotePivot = rect.pivot;
                state.NotePosition = rect.anchoredPosition;
                state.NoteSize = rect.sizeDelta;
                state.OriginalContent = sheet.scrollSkill.content;
                state.OriginalContentActive = state.OriginalContent.gameObject.activeSelf;
                var host = new GameObject("EpidemicConfigContent", typeof(RectTransform),
                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(RectMask2D));
                state.Content = host.GetComponent<RectTransform>();
                state.Content.SetParent(sheet.scrollSkill.viewport ? sheet.scrollSkill.viewport : sheet.scrollSkill.transform, false);
                state.Content.anchorMin = new Vector2(0, 1);
                state.Content.anchorMax = new Vector2(1, 1);
                state.Content.pivot = new Vector2(0, 1);
                state.Content.sizeDelta = Vector2.zero;
                state.Content.anchoredPosition = Vector2.zero;
                var contentLayout = host.GetComponent<VerticalLayoutGroup>();
                contentLayout.padding = new RectOffset(12, 24, 8, 16);
                contentLayout.childControlWidth = true;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandHeight = false;
                host.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                // Move the note out before hiding the old host; never hide its ancestors.
                rect.SetParent(state.Content, false);
                state.OriginalContent.gameObject.SetActive(false);
                sheet.scrollSkill.content = state.Content;
                state.NoteFitter = note.GetComponent<ContentSizeFitter>();
                if (state.NoteFitter)
                {
                    state.NoteHorizontalFit = state.NoteFitter.horizontalFit;
                    state.NoteFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
                state.Width = note.GetComponent<LayoutElement>();
                state.AddedWidth = !state.Width;
                if (!state.Width) state.Width = note.gameObject.AddComponent<LayoutElement>();
                state.MinWidth = state.Width.minWidth;
                state.PreferredWidth = state.Width.preferredWidth;
                state.FlexibleWidth = state.Width.flexibleWidth;
                state.WidthPriority = state.Width.layoutPriority;
                if (group)
                {
                    state.ControlWidth = group.childControlWidth;
                    state.ExpandWidth = group.childForceExpandWidth;
                    state.ControlHeight = group.childControlHeight;
                    state.ExpandHeight = group.childForceExpandHeight;
                }
                state.LayoutActive = true;
            }
            var viewport = sheet.scrollSkill.viewport ? sheet.scrollSkill.viewport : sheet.scrollSkill.GetComponent<RectTransform>();
            float width = Mathf.Max(100, viewport.rect.width - 36);
            state.Width.layoutPriority = 1000;
            state.Width.minWidth = 0;
            state.Width.preferredWidth = width;
            state.Width.flexibleWidth = 0;
            note.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (group)
            {
                group.childControlWidth = true;
                group.childForceExpandWidth = true;
                group.childControlHeight = true;
                group.childForceExpandHeight = false;
            }
            foreach (var fitter in note.GetComponentsInChildren<ContentSizeFitter>(true))
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            foreach (var text in note.GetComponentsInChildren<UIText>(true))
            {
                // Elin's formatter otherwise uses old prefab preferred widths and
                // switches Overflow back on in its deferred text callback.
                if (text.hyphenation)
                {
                    text.hyphenation.usePreferredWidth = false;
                    text.hyphenation.useParentWidth = false;
                }
                var bounds = text.GetComponent<LayoutElement>();
                if (!bounds) bounds = text.gameObject.AddComponent<LayoutElement>();
                bounds.layoutPriority = 1000;
                bounds.minWidth = 0;
                bounds.preferredWidth = Mathf.Max(80, width - (group ? group.padding.horizontal : 0) - 32);
                bounds.flexibleWidth = 0;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            state.LastWidth = viewport.rect.width;
            LayoutRebuilder.ForceRebuildLayoutImmediate(state.Content);
            foreach (var text in note.GetComponentsInChildren<UIText>(true))
            {
                if (text.hyphenation) text.hyphenation._SetText(text.text, false);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(state.Content);
        }

        private static void RestoreLayout(WindowChara sheet, State state)
        {
            if (!state.LayoutActive) return;
            var rect = sheet.note.GetComponent<RectTransform>();
            rect.SetParent(state.NoteParent, false);
            rect.SetSiblingIndex(state.NoteSibling);
            rect.anchorMin = state.NoteAnchorMin;
            rect.anchorMax = state.NoteAnchorMax;
            rect.pivot = state.NotePivot;
            rect.sizeDelta = state.NoteSize;
            rect.anchoredPosition = state.NotePosition;
            sheet.scrollSkill.content = state.OriginalContent;
            state.OriginalContent.gameObject.SetActive(state.OriginalContentActive);
            if (state.NoteFitter) state.NoteFitter.horizontalFit = state.NoteHorizontalFit;
            Object.DestroyImmediate(state.Content.gameObject);
            if (state.AddedWidth) Object.DestroyImmediate(state.Width);
            else if (state.Width)
            {
                state.Width.minWidth = state.MinWidth;
                state.Width.preferredWidth = state.PreferredWidth;
                state.Width.flexibleWidth = state.FlexibleWidth;
                state.Width.layoutPriority = state.WidthPriority;
            }
            var group = sheet.note.layout as HorizontalOrVerticalLayoutGroup;
            if (group)
            {
                group.childControlWidth = state.ControlWidth;
                group.childForceExpandWidth = state.ExpandWidth;
                group.childControlHeight = state.ControlHeight;
                group.childForceExpandHeight = state.ExpandHeight;
            }
            state.LayoutActive = false;
        }

        private static void AddCheckbox(UINote note, string label, ConfigEntry<bool> setting, WindowChara sheet)
        {
            var toggle = note.AddToggle("", setting.Value, value =>
            {
                setting.Value = value;
                PerpetualResidentDreamLarvaEpidemicPlugin.Instance.Config.Save();
                UpdateCount(sheet);
            });
            toggle.mainText.text = label;
        }

        private static void Refresh(WindowChara sheet)
        {
            if (!sheet || !IsSelected(sheet)) return;
            var state = GetState(sheet.window);
            sheet.RefreshSkill(sheet.window.setting.tabs.IndexOf(state.Tab));
        }

        internal static void UpdateCount(WindowChara sheet)
        {
            var state = GetState(sheet.window);
            if (state == null || !state.Count || PerpetualResidentDreamLarvaEpidemicPlugin.Instance == null) return;
            if (state.LayoutActive && IsSelected(sheet))
            {
                var viewport = sheet.scrollSkill.viewport ? sheet.scrollSkill.viewport : sheet.scrollSkill.GetComponent<RectTransform>();
                if (Mathf.Abs(viewport.rect.width - state.LastWidth) > 1)
                {
                    FitLayout(sheet, state);
                    sheet.note.Build();
                    sheet.scrollSkill.Refresh();
                }
            }
            state.Count.text = "Eligible recipients here (including player): " + PerpetualResidentDreamLarvaEpidemicPlugin.Instance.CountRecipients() +
                "\nItem replenishment: " + (PerpetualResidentDreamLarvaEpidemicPlugin.Instance.SupplyEnabled.Value ? "On" : "Off") +
                "\n" + (PerpetualResidentDreamLarvaEpidemicPlugin.Instance.SettingsMessage ?? "") +
                (PerpetualResidentDreamLarvaEpidemicPlugin.Instance.SupplyError == null ? "" : "\n" + PerpetualResidentDreamLarvaEpidemicPlugin.Instance.SupplyError);
        }
    }
}
