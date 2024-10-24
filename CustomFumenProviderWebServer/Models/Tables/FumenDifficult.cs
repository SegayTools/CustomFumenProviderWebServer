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

        public int BellCount { get; set; }

        public int FlickCount { get; set; }

        public int HoldCount { get; set; }

        public int TapCount { get; set; }

        public int BulletCount { get; set; }

        public int SoflanCount { get; set; }

        public int BpmCount { get; set; }

        public int MeterCount { get; set; }

        public int BeamCount { get; set; }



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
