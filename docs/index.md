# SimpleDataflows

[![NuGet](https://img.shields.io/nuget/v/SimpleDataflows.svg)](https://www.nuget.org/packages/SimpleDataflows)

## Overview

Perhaps the simplest way to implement parallel data processing in .NET is to use [`Parallel.ForEach`](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library). For more advanced workflows, you can use [Parallel LINQ](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/introduction-to-plinq).

However, neither of those solutions handle asynchronous I/O very well. For "parallelizing CPU-intensive and I/O-intensive applications that have high throughput and low latency," consider using [TPL Dataflow](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library).

But what if you want the power of TPL Dataflow but the simplicity of Parallel LINQ? That's what this library is designed to address. **SimpleDataflows** is a lightweight abstraction over TPL Dataflow that makes it easier to connect dataflow blocks into a simple linear pipeline.

## Usage

To define a dataflow, first call [`SimpleDataflow.Create`](SimpleDataflows/SimpleDataflow/Create.md) with the objects that should be posted to the first dataflow block. (Alternatively, call `Create` without a collection, which will start the first block with a single empty `ValueTuple`. Ignore that value and read the initial data from an external source.) To support cancellation, use the cancellation token parameter.

To link a block to the dataflow, call one of the dataflow block methods, such as [`Transform`](SimpleDataflows/SimpleDataflow-1/Transform.md). Each dataflow block method returns an object that should be used to link the next block to the dataflow.

To finish the dataflow definition and start it running, call and await [`ExecuteAsync`](SimpleDataflows/SimpleDataflow-1/ExecuteAsync.md) on the return value of the last dataflow block method. This posts the initial collection to the first dataflow block and returns when all of the data has been fully processed, ignoring any resulting data, which should normally be written somewhere as a side effect of the last dataflow block.

```csharp
var wordCounts = new ConcurrentDictionary<string, int>();
await SimpleDataflow.Create(Directory.EnumerateFiles(".", "*.txt"))
    .Transform(async path => await File.ReadAllTextAsync(path))
    .TransformMany(text => text.Split())
    .ForAll(word => wordCounts.AddOrUpdate(word, _ => 1, (_, x) => x + 1))
    .ExecuteAsync();
```

If an exception is thrown, the pipeline is automatically cancelled. To ensure that long-running blocks respect automatic cancellation, use dataflow block methods that provide a cancellation token to the specified delegate.

See [the documentation](SimpleDataflows/SimpleDataflow-1.md) for descriptions of each dataflow block method, including [`TransformMany`](SimpleDataflows/SimpleDataflow-1/TransformMany.md), [`Batch`](SimpleDataflows/SimpleDataflow-1/Batch.md), and [`ForAll`](SimpleDataflows/SimpleDataflow-1/ForAll.md).

By default, [`MaxDegreeOfParallelism`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.dataflow.executiondataflowblockoptions.maxdegreeofparallelism) is set to `Environment.ProcessorCount / 2` and [`EnsureOrdered`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.dataflow.dataflowblockoptions.ensureordered) is set to `false`. To customize this or other settings, include calls to the [corresponding methods](SimpleDataflows/SimpleDataflow-1.md) to impact all of the dataflow blocks that follow, e.g. [`MaxDegreeOfParallelism`](SimpleDataflows/SimpleDataflow-1/MaxDegreeOfParallelism.md).
