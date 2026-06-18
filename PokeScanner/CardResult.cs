using System;

namespace PokeScanner
{
    public class CardResult
    {
        public string CardId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Number { get; set; } = "";
        public string SetName { get; set; } = "";
        public string Hp { get; set; } = "";
        public int Score { get; set; }
        public string DisplayText => $"{Name} #{Number} ({SetName}) HP={Hp} match={Score}%";
    }
}
