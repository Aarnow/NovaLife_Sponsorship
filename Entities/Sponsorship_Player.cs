using Life.Network;
using ModKit.Utils;
using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sponsorship.Entities
{
    public class Sponsorship_Player : ModKit.ORM.ModEntity<Sponsorship_Player>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }

        public string PlayerSteamId { get; set; }
        public string PlayerFullName { get; set; }

        public int MentorId { get; set; }
        public string MentorFullName { get; set; }
        public bool MentorRewardClaimed {  get; set; }

        public string MenteePlayers { get; set; }
        [Ignore]
        public List<int> LMenteePlayers { get; set; } = new List<int>();

        public string RewardRecovered { get; set; }
        [Ignore]
        public List<int> LRewardRecovered { get; set; } = new List<int>();

        public int ConnectionCount { get; set; }
        public int LastConnection { get; set; }

        public long CreatedAt { get; set; }

        public Sponsorship_Player() { }

        public static Task<bool> Create(Sponsorship_Player currentPlayer, Player player)
        {
            currentPlayer.PlayerSteamId = player.steamId.ToString();
            currentPlayer.PlayerFullName = player.GetFullName();
            currentPlayer.MentorId = 0;
            currentPlayer.MentorFullName = "";
            currentPlayer.MentorRewardClaimed = false;
            currentPlayer.LMenteePlayers = new List<int>();
            currentPlayer.MenteePlayers = ListConverter.WriteJson(currentPlayer.LMenteePlayers);
            currentPlayer.LRewardRecovered = new List<int>();
            currentPlayer.RewardRecovered = ListConverter.WriteJson(currentPlayer.LRewardRecovered);
            currentPlayer.ConnectionCount = 1;
            currentPlayer.LastConnection = DateUtils.GetNumericalDateOfTheDay();
            currentPlayer.CreatedAt = DateUtils.GetCurrentTime();

            return currentPlayer.Save();
        }
    }
}