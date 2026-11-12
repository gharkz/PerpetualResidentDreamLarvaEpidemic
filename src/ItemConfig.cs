using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PerpetualResidentDreamLarvaEpidemic
{
    [DataContract]
    internal sealed class ItemConfig
    {
        [DataMember(Name = "ItemId")]
        public string ItemId;

        [DataMember(Name = "ItemCount")]
        public int ItemCount = 1;

        [DataMember(Name = "_ItemCountHelp")]
        public string ItemCountHelp = "Number to keep per recipient (default 1). Any positive integer up to 2147483647. Existing stacks count; extras are not removed. Edit in the character-sheet mod tab to apply and save immediately.";

        [DataMember(Name = "IncludePartyAllies")]
        public bool IncludePartyAllies = true;

        [DataMember(Name = "_IncludePartyAlliesHelp")]
        public string IncludePartyAlliesHelp = "true: also supply party allies in any loaded area. false: only supply home residents. Gender filters and resident opt-outs still apply. Edit in the character-sheet mod tab to apply and save immediately.";

        internal static bool TrySave(string path, ItemConfig config, out string error)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (string.IsNullOrWhiteSpace(config.ItemId) || config.ItemCount < 1)
                    throw new ArgumentException("Enter an item ID and a positive item count.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                    new DataContractJsonSerializer(typeof(ItemConfig)).WriteObject(stream, config);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = "Settings were not saved: " + ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        [OnDeserializing]
        private void SetDefaults(StreamingContext context)
        {
            IncludePartyAllies = true;
            ItemCount = 1;
        }

        internal static ItemConfig Load(string path, string defaultItemId, Action<string> warn)
        {
            var defaults = new ItemConfig { ItemId = defaultItemId };
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ItemConfig));
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                        serializer.WriteObject(stream, defaults);
                    return defaults;
                }

                ItemConfig config;
                using (var stream = File.OpenRead(path))
                    config = serializer.ReadObject(stream) as ItemConfig;
                if (string.IsNullOrWhiteSpace(config?.ItemId))
                {
                    warn("ItemId is missing or blank in " + path + "; using default: " + defaultItemId);
                    if (config == null) return defaults;
                    config.ItemId = defaultItemId;
                }
                config.ItemId = config.ItemId.Trim();
                if (config.ItemCount < 1)
                {
                    warn("ItemCount must be positive; using default: 1.");
                    config.ItemCount = 1;
                }
                return config;
            }
            catch (Exception ex)
            {
                warn("Could not load item config " + path + "; using default: " + defaultItemId + ". " + ex.Message);
                return defaults;
            }
        }
    }
}
