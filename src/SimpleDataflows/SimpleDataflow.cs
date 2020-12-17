using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SimpleDataflows
{
	/// <summary>
	/// Supports a linear pipeline of data using TPL Dataflow.
	/// </summary>
	public static class SimpleDataflow
	{
		/// <summary>
		/// Starts a linear pipeline of data using TPL Dataflow.
		/// </summary>
		public static SimpleDataflow<ValueTuple> Create(CancellationToken cancellationToken = default)
		{
			var input = new TransformBlock<ValueTuple, ValueTuple>(x => x);
			return new SimpleDataflow<ValueTuple>(input, input, CreateDefaultBlockOptions(cancellationToken));
		}

		/// <summary>
		/// Starts a linear pipeline of data using TPL Dataflow.
		/// </summary>
		public static SimpleDataflow<T> Create<T>(IEnumerable<T> initial, CancellationToken cancellationToken = default)
		{
			var input = new TransformManyBlock<ValueTuple, T>(_ => initial);
			return new SimpleDataflow<T>(input, input, CreateDefaultBlockOptions(cancellationToken));
		}

		internal static readonly DataflowLinkOptions DataflowLinkOptions = new DataflowLinkOptions { PropagateCompletion = true };

		private static ExecutionDataflowBlockOptions CreateDefaultBlockOptions(CancellationToken cancellationToken) =>
			new ExecutionDataflowBlockOptions
			{
				BoundedCapacity = Environment.ProcessorCount,
				CancellationToken = cancellationToken,
				EnsureOrdered = true,
				MaxDegreeOfParallelism = Environment.ProcessorCount,
			};
	}

	/// <summary>
	/// A linear pipeline of data using TPL dataflow.
	/// </summary>
	[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "generic type")]
	public sealed class SimpleDataflow<T>
	{
		/// <summary>
		/// Links a <c>TransformBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> Transform<TNext>(Func<T, TNext> func) =>
			LinkTo(new TransformBlock<T, TNext>(func, m_nextBlockOptions));

		/// <summary>
		/// Links a <c>TransformBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> Transform<TNext>(Func<T, Task<TNext>> func) =>
			LinkTo(new TransformBlock<T, TNext>(func, m_nextBlockOptions));

		/// <summary>
		/// Links a <c>TransformBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> Transform<TNext>(Func<T, CancellationToken, Task<TNext>> func) =>
			LinkTo(new TransformBlock<T, TNext>(x => func(x, m_nextBlockOptions.CancellationToken), m_nextBlockOptions));

		/// <summary>
		/// Links a <c>TransformManyBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, IEnumerable<TNext>> func) =>
			LinkTo(new TransformManyBlock<T, TNext>(func, m_nextBlockOptions));

		/// <summary>
		/// Links a <c>TransformManyBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, Task<IEnumerable<TNext>>> func) =>
			LinkTo(new TransformManyBlock<T, TNext>(func, m_nextBlockOptions));

		/// <summary>
		/// Links a <c>BatchBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<T[]> Batch(int batchSize) =>
			LinkTo(new BatchBlock<T>(batchSize));

		/// <summary>
		/// Executes the specified action on each item in the pipeline.
		/// </summary>
		/// <remarks>Implemented with a <c>TransformBlock</c> that returns the same items.</remarks>
		public SimpleDataflow<T> ForAll(Action<T> action) =>
			LinkTo(new TransformBlock<T, T>(x => ForItem(x, action), m_nextBlockOptions));

		/// <summary>
		/// Executes the specified action on each item in the pipeline.
		/// </summary>
		/// <remarks>Implemented with a <c>TransformBlock</c> that returns the same items.</remarks>
		public SimpleDataflow<T> ForAll(Func<T, Task> action) =>
			LinkTo(new TransformBlock<T, T>(x => ForItemAsync(x, action), m_nextBlockOptions));

		/// <summary>
		/// Links the specified block to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> LinkTo<TNext>(IPropagatorBlock<T, TNext> newOutput)
		{
			m_output.LinkTo(newOutput, SimpleDataflow.DataflowLinkOptions);
			return new SimpleDataflow<TNext>(m_input, newOutput, m_nextBlockOptions);
		}

		/// <summary>
		/// Sets the bounded capacity for the next blocks. (Default Environment.ProcessorCount.)
		/// </summary>
		public SimpleDataflow<T> BoundedCapacity(int value)
		{
			m_nextBlockOptions.BoundedCapacity = value;
			return this;
		}

		/// <summary>
		/// Sets ordered processing for the next blocks. (Default true.)
		/// </summary>
		public SimpleDataflow<T> EnsureOrdered(bool value)
		{
			m_nextBlockOptions.EnsureOrdered = value;
			return this;
		}

		/// <summary>
		/// Sets the maximum degree of parallelism for the next blocks. (Default Environment.ProcessorCount.)
		/// </summary>
		public SimpleDataflow<T> MaxDegreeOfParallelism(int value)
		{
			m_nextBlockOptions.MaxDegreeOfParallelism = value;
			return this;
		}

		/// <summary>
		/// Executes the pipeline.
		/// </summary>
		public async Task ExecuteAsync()
		{
			m_output.LinkTo(DataflowBlock.NullTarget<T>());
			if (!await m_input.SendAsync(default).ConfigureAwait(false))
				throw new InvalidOperationException("Input rejected.");
			m_input.Complete();
			await m_output.Completion.ConfigureAwait(false);
		}

		internal SimpleDataflow(ITargetBlock<ValueTuple> input, ISourceBlock<T> output, ExecutionDataflowBlockOptions nextBlockOptions)
		{
			m_input = input;
			m_output = output;
			m_nextBlockOptions = nextBlockOptions;
		}

		private static T ForItem(T item, Action<T> action)
		{
			action(item);
			return item;
		}

		private static async Task<T> ForItemAsync(T item, Func<T, Task> action)
		{
			await action(item).ConfigureAwait(false);
			return item;
		}

		private readonly ITargetBlock<ValueTuple> m_input;
		private readonly ISourceBlock<T> m_output;
		private readonly ExecutionDataflowBlockOptions m_nextBlockOptions;
	}
}
