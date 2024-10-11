using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CustomFumenProviderWebServer.Models.Tables
{
    public class FumenSet
    {
        public int MusicId { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        public int BossCardId { get; set; }
        public DateTime UpdateTime { get; set; }
        public int BossLevel { get; set; }
        public string WaveAttribute { get; set; }
        public virtual ICollection<FumenDifficult> FumenDifficults { get; set; } = new List<FumenDifficult>();
    }
}
