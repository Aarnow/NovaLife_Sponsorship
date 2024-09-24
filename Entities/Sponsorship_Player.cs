using SQLite;
using System.Collections.Generic;

namespace Sponsorship.Entities
{
    public class Sponsorship_Player : ModKit.ORM.ModEntity<Sponsorship_Player>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }

        public int PlayerId { get; set; }
        public string DiscordUsername { get; set; }

        public int MentorId { get; set; }
        
        public string MenteePlayers { get; set; }
        [Ignore]
        public List<int> LMenteePlayers { get; set; } = new List<int>();

        public int ConnectionCount { get; set; }
        public int LastConnection { get; set; }

        public string CreatedAt { get; set; }

        public Sponsorship_Player() { }
    }
}