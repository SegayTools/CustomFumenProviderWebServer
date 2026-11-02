using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models.Tables
{
    public class FumenOwner
    {
        public int MusicId { get; set; }

        public string Contact { get; set; }

        /// <summary>
        /// (可选)IM 联系方式, 如 QQ号 / Discord 用户名
        /// </summary>
        public string ImContact { get; set; }

        public string PasswordHash { get; set; }

        [JsonIgnore]
        public virtual FumenSet FumenSet { get; }
    }
}
