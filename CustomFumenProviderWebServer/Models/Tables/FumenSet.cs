using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CustomFumenProviderWebServer.Models.Tables
{
    public class FumenSet
    {
        public int MusicId { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public DateTime UpdateTime { get; set; }

        public PublishState PublishState { get; set; }

        public int BossCardId { get; set; }
        public string BossCardName { get; set; }
        public int BossLevel { get; set; }
        public int BossVoiceNo { get; set; }
        public int BossLockHpCoef { get; set; }

        public string WaveAttribute { get; set; }

        public int StageId { get; set; }
        public string StageName { get; set; }

        public int MusicRightsId { get; set; }
        public string MusicRightsName { get; set; }

        public int GenreId { get; set; }
        public string GenreName { get; set; }

        public int VersionId { get; set; }
        public string VersionName { get; set; }

        public virtual ICollection<FumenDifficult> FumenDifficults { get; set; } = new List<FumenDifficult>();
    }
}
