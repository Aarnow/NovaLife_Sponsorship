using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sponsorship.Entities
{
    public class Sponsorship_Reward : ModKit.ORM.ModEntity<Sponsorship_Reward>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }

        public int ItemId { get; set; }
        public int ItemQuantity { get; set; }
        public int MenteeRequired { get; set; }

        public Sponsorship_Reward() { }
    }
}
