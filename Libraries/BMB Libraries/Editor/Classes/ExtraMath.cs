using System.Collections.Generic;
using System.Linq;

namespace InventoryInventor.BMBLibraries
{
    namespace Classes
    {
        public class ExtraMath
        {
            public static IEnumerable<IEnumerable<T>> GetPowerSet<T>(IEnumerable<T> input)
            {
                var seed = new List<IEnumerable<T>>() { Enumerable.Empty<T>() }
                  as IEnumerable<IEnumerable<T>>;

                return input.Aggregate(seed, (a, b) =>
                  a.Concat(a.Select(x => x.Concat(new List<T>() { b }))));
            }
        }
    }
}
