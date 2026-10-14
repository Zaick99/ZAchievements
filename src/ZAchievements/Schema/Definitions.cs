namespace ZAchievements.Schema
{
    /// <summary>An achievement as the game declares it, before the user's state is known.</summary>
    internal sealed record AchievementDefinition(
        string Id,
        string Name,
        string Description,
        string Icon,
        string IconLocked,
        bool IsHidden,
        int Permission);

    internal abstract record StatDefinition(string Id, string DisplayName, bool IncrementOnly, int Permission);

    internal sealed record IntegerStatDefinition(string Id, string DisplayName, bool IncrementOnly, int Permission)
        : StatDefinition(Id, DisplayName, IncrementOnly, Permission);

    internal sealed record FloatStatDefinition(string Id, string DisplayName, bool IncrementOnly, int Permission)
        : StatDefinition(Id, DisplayName, IncrementOnly, Permission);
}
