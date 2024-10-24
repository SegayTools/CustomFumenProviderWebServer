using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models.Tables
{
    public class FumenOwner
    {
        public int MusicId { get; set; }

        public string Contact { get; set; }
        public string PasswordHash { get; set; }

        [JsonIgnore]
        public virtual FumenSet FumenSet { get; }
    }
}
