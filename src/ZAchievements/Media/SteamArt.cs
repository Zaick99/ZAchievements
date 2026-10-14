using ZAchievements.Steam;

namespace ZAchievements.Media
{
    /// <summary>Where Steam keeps the artwork for a game and its achievements.</summary>
    internal static class SteamArt
    {
        private const string StoreAssets = "https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps";
        private const string CommunityImages = "https://cdn.steamstatic.com/steamcommunity/public/images/apps";

        /// <summary>
        /// The small store capsule, preferred in the client's language, falling back
        /// to the old community logo for games the store has forgotten.
        /// </summary>
        public static string Capsule(Client client, uint appId)
        {
            var language = client.SteamApps008.GetCurrentGameLanguage();

            var capsule = client.SteamApps001.GetAppData(appId, $"small_capsule/{language}");
            if (string.IsNullOrEmpty(capsule) == true && language != "english")
            {
                capsule = client.SteamApps001.GetAppData(appId, "small_capsule/english");
            }

            if (string.IsNullOrEmpty(capsule) == false)
            {
                return $"{StoreAssets}/{appId}/{capsule}";
            }

            var logo = client.SteamApps001.GetAppData(appId, "logo");
            if (string.IsNullOrEmpty(logo) == false)
            {
                return $"{CommunityImages}/{appId}/{logo}.jpg";
            }

            // Nothing known locally — this is where the store keeps every public capsule.
            return $"{StoreAssets}/{appId}/capsule_231x87.jpg";
        }

        public static string AchievementIcon(uint appId, string iconName)
        {
            return string.IsNullOrEmpty(iconName) == true ? null : $"{CommunityImages}/{appId}/{iconName}";
        }
    }
}
