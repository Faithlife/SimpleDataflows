using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
			CollectionAssert.AreEqual(new[] { 1 + 55 + 5050 }, list);
		}

		[Test]
		public async Task VoidStart()
		{
			var list = new ConcurrentBag<int>();
			await SimpleDataflow.Create()
				.TransformMany(_ => Enumerable.Range(1, 100))
				.Batch(10)
				.Transform(async x => await Task.Run(x.Sum))
				.Batch(int.MaxValue)
				.Transform(x => x.Sum())
				.ForAll(x => list.Add(x))
				.ExecuteAsync();
			CollectionAssert.AreEqual(new[] { 5050 }, list);
		}

		[Test]
		public async Task BoundedCapacity([Values(1, 2)] int value)
		{
			var monitor = new object();
			var running = 0;
			var maxRunning = 0;
			await SimpleDataflow.Create(Enumerable.Range(0, 4))
				.BoundedCapacity(value)
				.Transform(x =>
				{
					lock (monitor)
						running++;
					Thread.Sleep(1000);
					lock (monitor)
						maxRunning = Math.Max(running--, maxRunning);
					return x;
				})
				.ExecuteAsync();
			Assert.LessOrEqual(value, maxRunning);
		}

		[TestCase(true)]
		[TestCase(null)]
		[TestCase(false, Explicit = true, Reason = "Build servers don't always have multiple CPUs.")]
		public async Task EnsureOrdered(bool? value)
		{
			var list = new List<int>();
			var dataflow = SimpleDataflow.Create(new[] { 2000, 0 });
			if (value != null)
				dataflow = dataflow.EnsureOrdered(value.Value);
			await dataflow
				.Transform(async x =>
				{
					await Task.Delay(x);
					return x;
				})
				.MaxDegreeOfParallelism(1)
				.ForAll(x => list.Add(x))
				.ExecuteAsync();
			CollectionAssert.AreEqual((value ?? true) ? new[] { 2000, 0 } : new[] { 0, 2000 }, list);
		}

		[Test]
		public async Task MaxDegreeOfParallelism([Values(1, 2)] int value)
		{
			var monitor = new object();
			var running = 0;
			var maxRunning = 0;
			await SimpleDataflow.Create(Enumerable.Range(0, 4))
				.MaxDegreeOfParallelism(value)
				.Transform(x =>
				{
					lock (monitor)
						running++;
					Thread.Sleep(1000);
					lock (monitor)
						maxRunning = Math.Max(running--, maxRunning);
					return x;
				})
				.ExecuteAsync();
			Assert.AreEqual(value, maxRunning);
		}
	}
}
