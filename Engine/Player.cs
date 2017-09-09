using System;
using System.Collections.Generic;
using System.Text;

namespace Engine
{
    public class Player
    {
        public int CurrentHitPoints { get; set; }
        public int MaximumHitPoints { get; set; }
        public int Gold { get; set; }
        public int ExperiencePoints { get; set; }
        public int Level { get; set; }
    }
}
