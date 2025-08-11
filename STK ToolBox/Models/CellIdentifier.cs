using System;

namespace STK_ToolBox.Models
{
    public class CellIdentifier : IEquatable<CellIdentifier>
    {
        public int Bank { get; set; }
        public int Bay { get; set; }
        public int Level { get; set; }

        public CellIdentifier(int bank, int bay, int level)
        {
            Bank = bank;
            Bay = bay;
            Level = level;
        }

        public override bool Equals(object obj) => Equals(obj as CellIdentifier);
        public bool Equals(CellIdentifier other)
        {
            return other != null &&
                   Bank == other.Bank &&
                   Bay == other.Bay &&
                   Level == other.Level;
        }
        public override int GetHashCode()
        {
            return Bank.GetHashCode() ^ Bay.GetHashCode() ^ Level.GetHashCode();
        }
        public override string ToString() => $"({Bank}, {Bay}, {Level})";

    }
}
