using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace DailyPoo;

public sealed class ModConfig
{
    /// <summary>The key that triggers the poop action. LeftStick is the left-stick click, which vanilla doesn't use.</summary>
    public KeybindList PoopKey { get; set; } = KeybindList.Parse("P, LeftStick");

    /// <summary>Whether to play a sound when pooping.</summary>
    public bool PlaySound { get; set; } = true;

    /// <summary>Fraction of max energy available while constipated (0.05 to 1).</summary>
    public float PenaltyFraction { get; set; } = 0.5f;

    /// <summary>Emote bubble shown after pooping: "heart" or "happy".</summary>
    public string Emote { get; set; } = "heart";

    /// <summary>Whether festival days don't require pooping (the toilet may be unreachable).</summary>
    public bool FestivalGrace { get; set; } = true;

    /// <summary>Whether sitting on the toilet for about 3 seconds poops automatically.</summary>
    public bool AutoPoop { get; set; } = true;

    /// <summary>Whether pooping produces one Basic Fertilizer. Waste not.</summary>
    public bool ProduceFertilizer { get; set; } = true;
}
