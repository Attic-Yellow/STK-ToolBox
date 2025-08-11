using System.Collections.Generic;
using STK_ToolBox.Models;

namespace STK_ToolBox.Helpers
{
    public class TeachingCell
    {
        public int Bank { get; set; }
        public int Bay { get; set; }
        public int Level { get; set; }
        public int Base { get; set; }
        public int Zaxis_Get { get; set; }
        public int Zaxis_Put { get; set; }
        public int Laxis { get; set; }
        public int Saxis { get; set; }
        public bool IsDead { get; set; }
    }
}