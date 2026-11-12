namespace PerpetualResidentDreamLarvaEpidemic
{
    internal sealed class ReplenishmentClock
    {
        private long? last;
        internal void Reset() => last = null;
        internal bool Due(long turn)
        {
            if (last.HasValue && last.Value == turn) return false;
            last = turn;
            return true; // First observation and each new turn; no replay of skipped ticks.
        }
    }
}

