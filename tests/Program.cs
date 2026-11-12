using System;
using PerpetualResidentDreamLarvaEpidemic;

int checks = 0;
void Assert(bool actual, bool expected, string name)
{
    checks++;
    if (actual != expected) throw new Exception(name);
}
for (int mask = 0; mask < 8; mask++)
{
    foreach (int gender in new[] { -1, 0, 1, 2, 3 })
    {
        int bit = gender == 1 ? 1 : gender == 2 ? 2 : gender == 0 ? 4 : 0;
        Assert(WorkItemPolicy.Matches(gender, (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0,
            false, true, true, false, false), (mask & bit) != 0, $"mask={mask}, gender={gender}");
    }
}
Assert(WorkItemPolicy.Matches(1,true,true,true,true,true,true,false,false),false,"player handled separately");
Assert(WorkItemPolicy.Matches(1,true,true,true,false,false,true,false,false),false,"visitor");
Assert(WorkItemPolicy.Matches(1,true,true,true,false,true,false,false,false),false,"absent/dead");
Assert(WorkItemPolicy.Matches(1,true,true,true,false,true,true,true,false),false,"guest");
Assert(WorkItemPolicy.Matches(1,true,true,true,false,true,true,false,true),false,"livestock");
Assert(WorkItemPolicy.ShouldSupply(false,false,false),false,"opted out");
Assert(WorkItemPolicy.ShouldSupply(true,false,true),false,"full inventory");
Assert(WorkItemPolicy.ShouldSupply(true,true,false),false,"existing item");
Assert(WorkItemPolicy.ShouldSupply(true,false,false),true,"empty eligible inventory");
var clock = new ReplenishmentClock();
int inventory = 0;
for (int turn=1; turn<=100; turn++)
{
    if(clock.Due(turn) && WorkItemPolicy.ShouldSupply(true,inventory>0,false)) inventory++;
    Assert(inventory==1,true,"100 turns never accumulate extras");
    Assert(clock.Due(turn),false,"same-turn frames never repeat");
}
inventory=0;
Assert(clock.Due(101),true,"removed item retried next tick");
if(WorkItemPolicy.ShouldSupply(true,inventory>0,false)) inventory++;
Assert(inventory==1,true,"replacement is one");
Assert(clock.Due(900),true,"time skip single tick");
Assert(clock.Due(900),false,"no catch-up burst");
Assert(clock.Due(2),true,"save rewind starts new tick");
clock.Reset();
Assert(clock.Due(2),true,"world change allows initial fill");
string configDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PerpetualResidentDreamLarvaEpidemicChecks-" + Guid.NewGuid());
string configPath = System.IO.Path.Combine(configDirectory, "settings.json");
try
{
    int warnings = 0;
    string LoadItem() => ItemConfig.Load(configPath, "default_item", _ => warnings++).ItemId;
    Assert(LoadItem() == "default_item", true, "missing config uses default");
    Assert(System.IO.File.Exists(configPath), true, "missing config generated");
    Assert(LoadItem() == "default_item", true, "generated config readable");
    Assert(warnings == 0, true, "default config has no warnings");
    Assert(ItemConfig.Load(configPath, "default_item", _ => {}).ItemCount == 1, true, "generated count defaults to one");
    foreach (int target in new[] { 1, 5, int.MaxValue })
    {
        System.IO.File.WriteAllText(configPath, "{\"ItemId\":\"custom_item\",\"ItemCount\":" + target + "}");
        Assert(ItemConfig.Load(configPath, "default_item", _ => {}).ItemCount == target, true, "configured count preserved");
    }
    foreach (int invalidCount in new[] { 0, -1 })
    {
        System.IO.File.WriteAllText(configPath, "{\"ItemId\":\"custom_item\",\"ItemCount\":" + invalidCount + "}");
        Assert(ItemConfig.Load(configPath, "default_item", _ => {}).ItemCount == 1, true, "nonpositive count falls back");
    }
    System.IO.File.Delete(configPath);
    LoadItem();
    Assert(ItemConfig.Load(configPath, "default_item", _ => {}).IncludePartyAllies, true, "generated party default enabled");
    Assert(System.IO.File.ReadAllText(configPath).Contains("_IncludePartyAlliesHelp"), true, "generated config documents party flag");
    System.IO.File.WriteAllText(configPath, "{\"ItemId\":\"custom_item\",\"IncludePartyAllies\":false}");
    Assert(ItemConfig.Load(configPath, "default_item", _ => {}).IncludePartyAllies, false, "explicit false preserved");
    System.IO.File.WriteAllText(configPath, "{\"IncludePartyAllies\":false}");
    Assert(ItemConfig.Load(configPath, "default_item", _ => {}).IncludePartyAllies, false, "item fallback preserves party flag");
    System.IO.File.WriteAllText(configPath, "{\"ItemId\":\" custom_item \"}");
    Assert(LoadItem() == "custom_item", true, "custom ID read and trimmed");
    Assert(ItemConfig.Load(configPath, "default_item", _ => {}).ItemCount == 1, true, "old JSON defaults count to one");
    Assert(ItemConfig.Load(configPath, "default_item", _ => {}).IncludePartyAllies, true, "old config defaults party flag on");
    foreach (string invalid in new[] { "{}", "null", "{\"ItemId\":null}", "{\"ItemId\":\" \"}", "invalid json" })
    {
        System.IO.File.WriteAllText(configPath, invalid);
        int previousWarnings = warnings;
        Assert(LoadItem() == "default_item", true, "invalid config falls back");
        Assert(warnings == previousWarnings + 1, true, "fallback logs warning");
        Assert(System.IO.File.ReadAllText(configPath) == invalid, true, "existing config preserved");
    }
    using (var locked = System.IO.File.Open(configPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
        Assert(LoadItem() == "default_item", true, "unreadable config falls back");
    var updated = new ItemConfig { ItemId = "updated_item", ItemCount = 12, IncludePartyAllies = false };
    Assert(ItemConfig.TrySave(configPath, updated, out var saveError), true, "UI settings replace existing config");
    Assert(saveError == null, true, "successful save has no error");
    var loaded = ItemConfig.Load(configPath, "default_item", _ => {});
    Assert(loaded.ItemId == "updated_item" && loaded.ItemCount == 12 && !loaded.IncludePartyAllies, true, "all UI settings persist");
    string previousConfig = System.IO.File.ReadAllText(configPath);
    Assert(ItemConfig.TrySave(configPath, new ItemConfig { ItemId = "updated_item", ItemCount = 0 }, out _), false, "invalid UI values rejected");
    Assert(System.IO.File.ReadAllText(configPath) == previousConfig, true, "invalid update preserves file");
    using (var locked = System.IO.File.Open(configPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
        Assert(ItemConfig.TrySave(configPath, updated, out _), false, "failed save reported");
    Assert(System.IO.File.ReadAllText(configPath) == previousConfig, true, "failed save preserves file");
    Assert(System.IO.Directory.GetFiles(configDirectory, "*.tmp").Length == 0, true, "temporary saves cleaned up");
    System.IO.File.Delete(configPath);
    Assert(ItemConfig.TrySave(configPath, updated, out _), true, "UI save creates missing config");
}
finally
{
    if (System.IO.File.Exists(configPath)) System.IO.File.Delete(configPath);
    if (System.IO.Directory.Exists(configDirectory)) System.IO.Directory.Delete(configDirectory);
}
Assert(WorkItemPolicy.InScope(false, false, true, true), true, "traveling party ally included");
Assert(WorkItemPolicy.InScope(false, true, true, false), false, "traveling party flag disabled");
Assert(WorkItemPolicy.InScope(false, false, false, true), false, "non-party outsider excluded");
Assert(WorkItemPolicy.InScope(true, true, false, false), true, "home resident unaffected by party flag");
Assert(WorkItemPolicy.InScope(true, false, true, true), true, "party visitor included at home");
Assert(WorkItemPolicy.InScope(true, false, true, false), false, "party visitor excluded when disabled");
Assert(WorkItemPolicy.MissingCount(5, 2) == 3, true, "only missing quantity supplied");
Assert(WorkItemPolicy.MissingCount(5, 5) == 0, true, "target count stops replenishment");
Assert(WorkItemPolicy.MissingCount(5, 8) == 0, true, "extras preserved");
Assert(WorkItemPolicy.MissingCount(int.MaxValue, 0) == int.MaxValue, true, "maximum configured count supported");
Assert(WorkItemPolicy.MissingCount(int.MaxValue, (long)int.MaxValue + 100) == 0, true, "multiple stacks cannot overflow count");
Console.WriteLine($"PASS: {checks} eligibility, no-flood, tick, and JSON config checks.");
