using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models.Tables
{
    public class FumenDifficult
    {
        public int MusicId { get; set; }
        public int DifficultIndex { get; set; }
        public float Level { get; set; }
        public float Bpm { get; set; }
        public string Creator { get; set; }

        [JsonInclude]
        public string DifficultName => DifficultIndex switch
        {
            0 => "Basic",
            1 => "Advanced",
            2 => "Expert",
            3 => "Master",
            4 => "Lunatic",
            _ => string.Empty
        };

        [JsonIgnore]
        public virtual FumenSet FumenSet { get; }
    }
}
