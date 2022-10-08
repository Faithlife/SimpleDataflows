using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Dataflow;

namespace SimpleDataflows;

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
		return new SimpleDataflow<ValueTuple>(input, input, CreateDefaultBlockOptions(cancellationToken), CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
	}

	/// <summary>
	/// Starts a linear pipeline of data using TPL Dataflow.
	/// </summary>
	public static SimpleDataflow<T> Create<T>(IEnumerable<T> initial, CancellationToken cancellationToken = default)
	{
		var input = new TransformManyBlock<ValueTuple, T>(_ => initial);
		return new SimpleDataflow<T>(input, input, CreateDefaultBlockOptions(cancellationToken), CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
	}

	internal static readonly DataflowLinkOptions DataflowLinkOptions = new() { PropagateCompletion = true };

	private static ExecutionDataflowBlockOptions CreateDefaultBlockOptions(CancellationToken cancellationToken) =>
		new()
		{
			BoundedCapacity = DataflowBlockOptions.Unbounded,
			CancellationToken = cancellationToken,
			EnsureOrdered = false,
			MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
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
		LinkTo(new TransformBlock<T, TNext>(value =>
		{
			try
			{
				return func(value);
			}
			catch (Exception) when (CancelAndRethrow())
			{
				throw;
			}
		}, m_nextBlockOptions));

	/// <summary>
	/// Links a <c>TransformBlock</c> to the pipeline.
	/// </summary>
	public SimpleDataflow<TNext> Transform<TNext>(Func<T, Task<TNext>> func) =>
		LinkTo(new TransformBlock<T, TNext>(
			async value =>
			{
				try
				{
					return await func(value).ConfigureAwait(false);
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));

	/// <summary>
	/// Links a <c>TransformBlock</c> to the pipeline.
	/// </summary>
	public SimpleDataflow<TNext> Transform<TNext>(Func<T, CancellationToken, Task<TNext>> func)
	{
		var cancellationToken = m_cancellationTokenSource.Token;
		return LinkTo(new TransformBlock<T, TNext>(
			async value =>
			{
				try
				{
					return await func(value, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));
	}

	/// <summary>
	/// Links a <c>TransformManyBlock</c> to the pipeline.
	/// </summary>
	public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, IEnumerable<TNext>> func) =>
		LinkTo(new TransformManyBlock<T, TNext>(value =>
		{
			try
			{
				return func(value);
			}
			catch (Exception) when (CancelAndRethrow())
			{
				throw;
			}
		}, m_nextBlockOptions));

	/// <summary>
	/// Links a <c>TransformManyBlock</c> to the pipeline.
	/// </summary>
	/// <remarks>If necessary, use <c>Enumerable.AsEnumerable</c> to cast the return value to an
	/// <c>IEnumerable&lt;T&gt;</c> when using a lambda expression.</remarks>
	public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, Task<IEnumerable<TNext>>> func) =>
		LinkTo(new TransformManyBlock<T, TNext>(
			async value =>
			{
				try
				{
					return await func(value).ConfigureAwait(false);
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));

	/// <summary>
	/// Links a <c>TransformManyBlock</c> to the pipeline.
	/// </summary>
	/// <remarks>If necessary, use <c>Enumerable.AsEnumerable</c> to cast the return value to an
	/// <c>IEnumerable&lt;T&gt;</c> when using a lambda expression.</remarks>
	public SimpleDataflow<TNext> TransformMany<TNext>(Func<T, CancellationToken, Task<IEnumerable<TNext>>> func)
	{
		var cancellationToken = m_cancellationTokenSource.Token;
		return LinkTo(new TransformManyBlock<T, TNext>(
			async value =>
			{
				try
				{
					return await func(value, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));
	}

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
		LinkTo(new TransformBlock<T, T>(
			value =>
			{
				try
				{
					action(value);
					return value;
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));

	/// <summary>
	/// Executes the specified action on each item in the pipeline.
	/// </summary>
	/// <remarks>Implemented with a <c>TransformBlock</c> that returns the same items.</remarks>
	public SimpleDataflow<T> ForAll(Func<T, Task> action) =>
		LinkTo(new TransformBlock<T, T>(
			async value =>
			{
				try
				{
					await action(value).ConfigureAwait(false);
					return value;
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));

	/// <summary>
	/// Executes the specified action on each item in the pipeline.
	/// </summary>
	/// <remarks>Implemented with a <c>TransformBlock</c> that returns the same items.</remarks>
	public SimpleDataflow<T> ForAll(Func<T, CancellationToken, Task> action)
	{
		var cancellationToken = m_cancellationTokenSource.Token;
		return LinkTo(new TransformBlock<T, T>(
			async value =>
			{
				try
				{
					await action(value, cancellationToken).ConfigureAwait(false);
					return value;
				}
				catch (Exception) when (CancelAndRethrow())
				{
					throw;
				}
			}, m_nextBlockOptions));
	}

	/// <summary>
	/// Links the specified block to the pipeline.
	/// </summary>
	public SimpleDataflow<TNext> LinkTo<TNext>(IPropagatorBlock<T, TNext> newOutput)
	{
		m_output.LinkTo(newOutput, SimpleDataflow.DataflowLinkOptions);
		return new SimpleDataflow<TNext>(m_input, newOutput, m_nextBlockOptions, m_cancellationTokenSource);
	}

	/// <summary>
	/// Sets the bounded capacity for the next blocks.
	/// </summary>
	/// <remarks>Use this setting to avoid running out of memory while earlier blocks wait for later blocks.
	/// If this method is not called, the default is <c>DataflowBlockOptions.Unbounded</c>.</remarks>
	public SimpleDataflow<T> BoundedCapacity(int value)
	{
		m_nextBlockOptions.BoundedCapacity = value;
		return this;
	}

	/// <summary>
	/// Sets ordered processing for the next blocks.
	/// </summary>
	/// <remarks>If this method is not called, the default is unordered.</remarks>
	public SimpleDataflow<T> EnsureOrdered(bool value = true)
	{
		m_nextBlockOptions.EnsureOrdered = value;
		return this;
	}

	/// <summary>
	/// Sets the maximum degree of parallelism for the next blocks.
	/// </summary>
	/// <remarks>If this method is not called, the default is <c>Environment.ProcessorCount / 2</c>.</remarks>
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
		m_cancellationTokenSource.Dispose();
	}

	private bool CancelAndRethrow()
	{
		m_cancellationTokenSource.Cancel();
		return false;
	}

	internal SimpleDataflow(ITargetBlock<ValueTuple> input, ISourceBlock<T> output, ExecutionDataflowBlockOptions nextBlockOptions, CancellationTokenSource cancellationTokenSource)
	{
		m_input = input;
		m_output = output;
		m_nextBlockOptions = nextBlockOptions;
		m_cancellationTokenSource = cancellationTokenSource;
	}

	private readonly ITargetBlock<ValueTuple> m_input;
	private readonly ISourceBlock<T> m_output;
	private readonly ExecutionDataflowBlockOptions m_nextBlockOptions;
	private readonly CancellationTokenSource m_cancellationTokenSource;
}
