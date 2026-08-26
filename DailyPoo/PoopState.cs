namespace DailyPoo;

/// <summary>Per-save mod state, persisted inside the save file.</summary>
public sealed class PoopState
{
    /// <summary>Whether the player pooped during the current in-game day.</summary>
    /// <remarks>Defaults to true so a brand-new save gets a grace day instead of waking up constipated.</remarks>
    public bool PoopedToday { get; set; } = true;

    /// <summary>Whether the player skipped a day and is penalized until they poop.</summary>
    public bool IsConstipated { get; set; }
}
