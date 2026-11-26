using System;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PerpetualResidentDreamLarvaEpidemic
{
    [BepInPlugin(Id, DisplayName, Version)]
    public sealed class PerpetualResidentDreamLarvaEpidemicPlugin : BaseUnityPlugin
    {
        // Default for generated JSON and fallback when the configured ID cannot be used.
        public const string DefaultItemId = "dreambug";
        internal string ItemId { get; private set; } = DefaultItemId;
        internal bool IncludePartyAllies { get; private set; } = true;
        internal int ItemCount { get; private set; } = 1;
        public const string Id = "gharkz.elin.perpetualresidentdreamlarvaepidemic";
        public const string DisplayName = "Perpetual Resident Dream Larva Epidemic";
        public const string Version = "0.3.3";

        internal static PerpetualResidentDreamLarvaEpidemicPlugin Instance;
        internal ConfigEntry<bool> Female, Male, Unknown, SupplyEnabled, IncludePlayer;
        internal string SupplyError;
        internal string SettingsMessage;
        private static string ItemConfigPath => Path.Combine(Paths.ConfigPath, Id + ".json");
        private Harmony harmony;
        private float nextRefresh;
        private readonly ReplenishmentClock clock = new ReplenishmentClock();
        private Game observedGame;
        private Zone observedZone;
        private bool itemValidated;

        private void Awake()
        {
            Instance = this;
            var itemConfig = ItemConfig.Load(ItemConfigPath,
                DefaultItemId, message => Logger.LogWarning(message));
            ItemId = itemConfig.ItemId;
            IncludePartyAllies = itemConfig.IncludePartyAllies;
            ItemCount = itemConfig.ItemCount;
            Female = Config.Bind("Recipients", "Female", true, "Supply female home residents.");
            Male = Config.Bind("Recipients", "Male", false, "Supply male home residents.");
            Unknown = Config.Bind("Recipients", "Unknown", false, "Supply home residents whose gender is ???.");
            SupplyEnabled = Config.Bind("Supply", "Enabled", true, "Keep the JSON ItemCount of the selected item in eligible inventories, checking every player turn.");
            IncludePlayer = Config.Bind("Recipients", "IncludePlayer", true, "Also supply the player while at a player-owned home; independent of gender filters.");
            SupplyEnabled.SettingChanged += (_, __) => { clock.Reset(); SupplyError = null; itemValidated = false; };
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(PerpetualResidentDreamLarvaEpidemicPlugin).Assembly);
            Support.BugReport.Init(DisplayName, Id, Version, Id, () => new[]
            {
                "Supply: " + (SupplyEnabled.Value ? "on" : "off") + ", item " + ItemId + " x" + ItemCount,
                "Recipients: female " + Female.Value + ", male " + Male.Value + ", unknown " + Unknown.Value + ", player " + IncludePlayer.Value + ", party allies " + IncludePartyAllies,
                "Supply error: " + (SupplyError ?? "none"),
            });
            Logger.LogInfo(DisplayName + " " + Version + " loaded. Item inventory replenishment only.");
        }

        private void Update()
        {
            try { ReplenishOnTick(); }
            catch (Exception ex)
            {
                // Pause after a failure instead of retrying creation every frame.
                SupplyError = "Replenishment paused after an error. See BepInEx log.";
                Logger.LogError(ex);
                Support.BugReport.Note(ex);
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            var sheet = WindowChara.Instance;
            if (sheet && sheet.gameObject.activeInHierarchy && PerpetualResidentDreamLarvaEpidemicTab.IsSelected(sheet))
                PerpetualResidentDreamLarvaEpidemicTab.UpdateCount(sheet);
        }

        internal bool ApplyItemSettings(string itemId, int itemCount, bool includePartyAllies)
        {
            itemId = itemId?.Trim();
            if (string.IsNullOrEmpty(itemId) || !EClass.sources.things.map.ContainsKey(itemId))
            {
                SettingsMessage = "Unknown item ID. Settings unchanged.";
                return false;
            }
            if (itemCount < 1)
            {
                SettingsMessage = "Item count must be a positive whole number. Settings unchanged.";
                return false;
            }
            var config = new ItemConfig { ItemId = itemId, ItemCount = itemCount, IncludePartyAllies = includePartyAllies };
            if (!ItemConfig.TrySave(ItemConfigPath, config, out var error))
            {
                Logger.LogWarning(error);
                SettingsMessage = "Could not save settings. Settings unchanged. See BepInEx log.";
                return false;
            }
            ItemId = itemId;
            ItemCount = itemCount;
            IncludePartyAllies = includePartyAllies;
            itemValidated = false;
            SupplyError = null;
            clock.Reset();
            SettingsMessage = "Saved and applied.";
            return true;
        }

        internal static bool IsResident(Chara c)
        {
            return c != null && !c.IsPC && c.IsPCFaction && c.homeZone != null &&
                c.homeZone.IsPCFaction && !c.IsGuest() && c.memberType != FactionMemberType.Livestock;
        }

        internal ConfigEntry<bool> ResidentPreference(Chara c)
        {
            string world = BitConverter.ToString(Encoding.UTF8.GetBytes(Game.id ?? "unknown")).Replace("-", "");
            return Config.Bind("ResidentSupply", "World_" + world + "_Seed_" + EClass.game.seed + "_Resident_" + c.uid,
                true, "Allow item replenishment for this resident; gender selection also applies.");
        }

        internal bool IsRecipient(Chara c)
        {
            if (c == null || !c.IsAliveInCurrentZone) return false;
            if (c.IsPC) return AtHome() && IncludePlayer.Value;
            return c.bio != null && WorkItemPolicy.Matches(c.bio.gender, Female.Value, Male.Value, Unknown.Value,
                false, WorkItemPolicy.InScope(AtHome(), IsResident(c) && c.homeZone == EClass._zone,
                    c.IsPCParty, IncludePartyAllies), true, c.IsGuest(),
                c.memberType == FactionMemberType.Livestock) && ResidentPreference(c).Value;
        }

        private static bool AtHome()
        {
            return InLoadedZone() && EClass._zone.IsPCFaction;
        }

        private static bool InLoadedZone()
        {
            return EClass.core != null && EClass.game != null && EClass.game.player != null && EClass.pc != null && EClass._zone != null &&
                EClass._zone.isStarted && EClass._map != null;
        }

        internal int CountRecipients()
        {
            if (!InLoadedZone()) return 0;
            int count = 0;
            foreach (var c in EClass._map.charas)
                if (IsRecipient(c)) count++;
            return count;
        }

        private void ReplenishOnTick()
        {
            // _zone and pc dereference game/player internally, including at the title screen.
            if (!InLoadedZone())
            {
                observedGame = null;
                observedZone = null;
                clock.Reset();
                return;
            }
            if (EClass.game != observedGame || EClass._zone != observedZone)
            {
                observedGame = EClass.game;
                observedZone = EClass._zone;
                clock.Reset();
                itemValidated = false;
                SupplyError = null;
            }
            if (!SupplyEnabled.Value || !InLoadedZone())
            {
                clock.Reset();
                return;
            }
            if (SupplyError != null || Game.isPaused || !clock.Due(EClass.player.stats.turns)) return;
            if (!itemValidated)
            {
                if (!EClass.sources.things.map.ContainsKey(ItemId))
                {
                    Logger.LogWarning("Unknown configured item ID: " + ItemId + "; using default: " + DefaultItemId);
                    ItemId = DefaultItemId;
                }
                if (string.IsNullOrWhiteSpace(ItemId) || !EClass.sources.things.map.ContainsKey(ItemId))
                {
                    SupplyError = "Unknown item ID. Replenishment is paused.";
                    Logger.LogError(SupplyError + " ID: " + ItemId);
                    return;
                }
                itemValidated = true;
            }
            foreach (var c in EClass._map.charas)
            {
                if (!IsRecipient(c)) continue;
                int missing = WorkItemPolicy.MissingCount(ItemCount, CountItems(c.things));
                if (missing == 0) continue;
                var item = ThingGen.Create(ItemId).SetNum(missing);
                // No PickOrDrop fallback: never litter the map when a container is full.
                // Let the game select a compatible stack even in a full inventory.
                var dest = c.things.GetDest(item);
                if (!dest.IsValid) { item.Destroy(); continue; }
                if (dest.stack != null)
                {
                    if (!item.TryStackTo(dest.stack)) item.Destroy();
                }
                else dest.container.AddThing(item);
            }
        }

        private long CountItems(ThingContainer things)
        {
            long count = 0;
            foreach (var item in things)
            {
                if (item.isDestroyed) continue;
                if (item.id == ItemId && item.Num > 0) count += item.Num;
                if (item.CanSearchContents) count += CountItems(item.things);
                if (count >= ItemCount) return ItemCount;
            }
            return count;
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }
}

