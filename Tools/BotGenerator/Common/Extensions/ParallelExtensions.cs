using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class ParallelExtensions
    {
        public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            async Task AwaitPartition(IEnumerator<T> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield(); // prevents a sync/hot thread hangup
                        await body(partition.Current);
                    }
                }
            }
            return Task.WhenAll(Partitioner.Create(source).GetPartitions(dop).AsParallel().Select(p => AwaitPartition(p)));
        }
    }
}
