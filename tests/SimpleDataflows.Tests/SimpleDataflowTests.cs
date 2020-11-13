using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SimpleDataflows.Tests
{
	public class SimpleDataflowTests
	{
		[Test]
		public async Task Success()
		{
			var list = new ConcurrentBag<int>();
			await SimpleDataflow.Create(new[] { 1, 10, 100 })
				.TransformMany(x => Enumerable.Range(1, x))
				.Batch(10)
				.Transform(async x => await Task.Run(x.Sum))
				.Batch(int.MaxValue)
				.Transform(x => x.Sum())
				.ForAll(x => list.Add(x))
				.ExecuteAsync();
			CollectionAssert.AreEqual(new[] { 5106 }, list);
		}
	}
}
