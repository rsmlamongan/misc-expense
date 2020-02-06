using System.Linq;
using System.Collections.Generic;

namespace MiscExpenxe
{
    class Misc
    {
        public int Id { get; set; }
        public int Row { get; set; }
        public Dictionary<string, double> Expenses { get; set; } = new Dictionary<string, double>();
        public IEnumerable<KeyValuePair<string, double>> ExpensesOrdered =>
            from entry in Expenses orderby entry.Key ascending select entry;
    }
}
