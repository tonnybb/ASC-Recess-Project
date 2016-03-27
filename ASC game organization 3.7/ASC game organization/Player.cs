using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASC_game_organization
{
    class Player : IComparable<Player>
    {
        public Player(string name, string department, int gamesPlayed, int ranUniqueId, int shiftStart, int shiftEnd )
        {
            Name = name;
            GamesPlayed = gamesPlayed;
            RanUniqueId = ranUniqueId;
            Department = department;
            ShiftStart = shiftStart;
            ShiftEnd = shiftEnd;
        }

        // Getter/Setter for the Name property. How to get: string aName = obj.Name; How to set: obj.Name = aName;
        public string Name { get; set; }
        public int GamesPlayed { get; set; }
        public int RanUniqueId { get; set; }
        public string Department { get; set; }
        public int ShiftStart { get; set; }
        public int ShiftEnd { get; set; }

        public int CompareTo(Player obj)
        {
            if (this.GamesPlayed == obj.GamesPlayed)
            {
                return 0;
            }
            else if (this.GamesPlayed < obj.GamesPlayed)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }

    }
}
