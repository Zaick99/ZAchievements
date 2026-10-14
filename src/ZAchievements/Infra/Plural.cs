namespace ZAchievements.Infra
{
    internal static class Plural
    {
        /// <summary>"1 game", "12 games".</summary>
        public static string Of(int count, string one, string many)
        {
            return $"{count:N0} {(count == 1 ? one : many)}";
        }
    }
}
