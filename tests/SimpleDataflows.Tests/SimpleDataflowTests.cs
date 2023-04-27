using System.Collections.Concurrent;
using NUnit.Framework;

namespace SimpleDataflows.Tests;

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
			.ForAll(list.Add)
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
			.ForAll(list.Add)
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
		Assert.LessOrEqual(maxRunning, value);
	}

	[Test]
	public async Task EnsureOrdered()
	{
		var list = new List<int>();
		await SimpleDataflow.Create(new[] { 2000, 0 })
			.EnsureOrdered()
			.Transform(async x =>
			{
				await Task.Delay(x);
				return x;
			})
			.MaxDegreeOfParallelism(1)
			.ForAll(list.Add)
			.ExecuteAsync();
		CollectionAssert.AreEqual(new[] { 2000, 0 }, list);
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

	[Test]
	public async Task Exception()
	{
		try
		{
			await SimpleDataflow.Create()
				.ForAll(_ => throw new InvalidOperationException())
				.ExecuteAsync();
			Assert.Fail();
		}
		catch (InvalidOperationException)
		{
		}
	}

	[Test]
	public async Task Cancelled()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		try
		{
			await SimpleDataflow.Create(cts.Token)
				.ForAll(_ => throw new InvalidOperationException())
				.ExecuteAsync();
			Assert.Fail();
		}
		catch (OperationCanceledException)
		{
		}
	}

	[Test]
	public async Task ExecuteTwice()
	{
		var dataflow = SimpleDataflow.Create();
		await dataflow.ExecuteAsync();
		Assert.ThrowsAsync<InvalidOperationException>(dataflow.ExecuteAsync);
	}
}
