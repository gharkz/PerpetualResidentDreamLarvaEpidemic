namespace PerpetualResidentDreamLarvaEpidemic
{
    internal static class WorkItemPolicy
    {
        internal static int MissingCount(int desired, long existing)
            => (int)System.Math.Max(0L, (long)desired - System.Math.Max(0L, existing));

        internal static bool InScope(bool atHome, bool assignedHere, bool partyAlly, bool includePartyAllies)
            => (atHome && assignedHere) || (partyAlly && includePartyAllies);

        internal static bool ShouldSupply(bool eligible, bool hasItem, bool full)
            => eligible && !hasItem && !full;

        internal static bool Matches(int gender, bool female, bool male, bool unknown,
            bool player, bool resident, bool present, bool guest, bool livestock)
        {
            if (player || !resident || !present || guest || livestock) return false;
            switch (gender)
            {
                case 0: return unknown;
                case 1: return female;
                case 2: return male;
                default: return false;
            }
        }
    }
}

