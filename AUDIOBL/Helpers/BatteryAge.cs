namespace AUDIOBL.Helpers;

/// <summary>How old the last known battery reading is.</summary>
public enum BatteryFreshness { Fresh, Stale1h, Stale6h }

public static class BatteryAge
{
    /// <summary>Fresh &lt; 1h, Stale1h between 1h and 6h, Stale6h beyond 6h.</summary>
    public static BatteryFreshness Evaluate(DateTime? timestamp)
    {
        if (timestamp == null) return BatteryFreshness.Fresh;
        var age = DateTime.Now - timestamp.Value;
        if (age >= TimeSpan.FromHours(6)) return BatteryFreshness.Stale6h;
        if (age >= TimeSpan.FromHours(1)) return BatteryFreshness.Stale1h;
        return BatteryFreshness.Fresh;
    }
}
