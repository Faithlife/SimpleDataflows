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
		public static SimpleDataflow<T> Create<T>(IEnumerable<T> initial, CancellationToken cancellationToken = default)
		{
			var input = new TransformManyBlock<object, T>(_ => initial);
			return new SimpleDataflow<T>(input, input, cancellationToken);
		}

		internal static readonly DataflowLinkOptions DataflowLinkOptions = new DataflowLinkOptions { PropagateCompletion = true };
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
			LinkTo(new TransformBlock<T, TNext>(func, CreateExecutionDataflowBlockOptions()));

		/// <summary>
		/// Links a <c>TransformBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> Transform<TNext>(Func<T, Task<TNext>> func) =>
			LinkTo(new TransformBlock<T, TNext>(func, CreateExecutionDataflowBlockOptions()));

		/// <summary>
		/// Links a <c>TransformBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> Transform<TNext>(Func<T, CancellationToken, Task<TNext>> func) =>
			LinkTo(new TransformBlock<T, TNext>(x => func(x, m_cancellationToken), CreateExecutionDataflowBlockOptions()));

		/// <summary>
		/// Links a <c>TransformManyBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, IEnumerable<TNext>> func) =>
			LinkTo(new TransformManyBlock<T, TNext>(func, CreateExecutionDataflowBlockOptions()));

		/// <summary>
		/// Links a <c>TransformManyBlock</c> to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, Task<IEnumerable<TNext>>> func) =>
			LinkTo(new TransformManyBlock<T, TNext>(func, CreateExecutionDataflowBlockOptions()));

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
			LinkTo(new TransformBlock<T, T>(x => ForItem(x, action), CreateExecutionDataflowBlockOptions()));

		/// <summary>
		/// Executes the specified action on each item in the pipeline.
		/// </summary>
		/// <remarks>Implemented with a <c>TransformBlock</c> that returns the same items.</remarks>
		public SimpleDataflow<T> ForAll(Func<T, Task> action) =>
			LinkTo(new TransformBlock<T, T>(x => ForItemAsync(x, action), CreateExecutionDataflowBlockOptions()));

		/// <summary>
		/// Links the specified block to the pipeline.
		/// </summary>
		public SimpleDataflow<TNext> LinkTo<TNext>(IPropagatorBlock<T, TNext> newOutput)
		{
			m_output.LinkTo(newOutput, SimpleDataflow.DataflowLinkOptions);
			return new SimpleDataflow<TNext>(m_input, newOutput, m_cancellationToken);
		}

		/// <summary>
		/// Executes the pipeline.
		/// </summary>
		public async Task ExecuteAsync()
		{
			m_output.LinkTo(DataflowBlock.NullTarget<T>());
			m_input.Post(this);
			m_input.Complete();
			await m_output.Completion.ConfigureAwait(false);
		}

		internal SimpleDataflow(ITargetBlock<object> input, ISourceBlock<T> output, CancellationToken cancellationToken)
		{
			m_input = input;
			m_output = output;
			m_cancellationToken = cancellationToken;
		}

		private ExecutionDataflowBlockOptions CreateExecutionDataflowBlockOptions() =>
			new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = Environment.ProcessorCount,
				CancellationToken = m_cancellationToken,
				BoundedCapacity = Environment.ProcessorCount,
			};

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

		private readonly ITargetBlock<object> m_input;
		private readonly ISourceBlock<T> m_output;
		private readonly CancellationToken m_cancellationToken;
	}
}
