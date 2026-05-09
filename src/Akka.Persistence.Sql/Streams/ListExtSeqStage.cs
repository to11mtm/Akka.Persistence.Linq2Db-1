// -----------------------------------------------------------------------
//  <copyright file="ListExtSeqStage.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Streams;
using Akka.Streams.Stage;
using LanguageExt;

namespace Akka.Persistence.Sql.Streams
{
    /// <summary>
    ///     A <see cref="Sink{TIn,TMat}"/> stage that collects all upstream elements into a
    ///     <see cref="List{T}"/> and then converts to <see cref="Seq{T}"/> exactly once on
    ///     completion, avoiding the per-element array re-allocation that
    ///     <see cref="ExtSeqStage{T}"/> incurs when calling <c>Seq&lt;T&gt;.Add()</c> repeatedly.
    ///     <para>
    ///         CopilotNote: Use this in hot paths where many elements flow through the sink —
    ///         the internal <see cref="List{T}"/> doubles capacity geometrically, so we only
    ///         re-allocate O(log N) times instead of O(N). UwU~ 🐾
    ///     </para>
    /// </summary>
    /// <typeparam name="T">Element type flowing into the sink.</typeparam>
    public sealed class ListExtSeqStage<T> : GraphStageWithMaterializedValue<SinkShape<T>, Task<Seq<T>>>
    {
        private readonly int _initialCapacity;

        /// <summary>
        ///     The inlet for incoming stream elements~
        /// </summary>
        public readonly Inlet<T> In = new("ListSeq.in");

        /// <summary>
        ///     Creates a new <see cref="ListExtSeqStage{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">
        ///     Initial capacity of the internal <see cref="List{T}"/>.
        ///     When you have a rough idea of the expected element count, pass it here
        ///     to avoid even the first few geometric resizes. Defaults to 16. ✨
        /// </param>
        public ListExtSeqStage(int initialCapacity = 16)
        {
            _initialCapacity = initialCapacity;
            Shape = new SinkShape<T>(In);
        }

        /// <inheritdoc/>
        protected override Attributes InitialAttributes { get; } = Attributes.CreateName("listLanguageExtSeqSink");

        /// <inheritdoc/>
        public override SinkShape<T> Shape { get; }

        /// <inheritdoc/>
        public override ILogicAndMaterializedValue<Task<Seq<T>>> CreateLogicAndMaterializedValue(
            Attributes inheritedAttributes)
        {
            var promise = new TaskCompletionSource<Seq<T>>();

            return new LogicAndMaterializedValue<Task<Seq<T>>>(
                new Logic(this, promise),
                promise.Task);
        }

        /// <inheritdoc/>
        public override string ToString() => "ListLanguageExtSeqStage";

        private sealed class Logic : InGraphStageLogic
        {
            private readonly TaskCompletionSource<Seq<T>> _promise;
            private readonly ListExtSeqStage<T> _stage;

            // 🐾 Single mutable list — geometric growth, no per-element array copy.
            private readonly List<T> _buf;
            private bool _completionSignalled;

            public Logic(ListExtSeqStage<T> stage, TaskCompletionSource<Seq<T>> promise) : base(stage.Shape)
            {
                _stage = stage;
                _promise = promise;
                _buf = new List<T>(stage._initialCapacity);

                SetHandler(stage.In, this);
            }

            /// <inheritdoc/>
            public override void OnPush()
            {
                _buf.Add(Grab(_stage.In));
                Pull(_stage.In);
            }

            /// <inheritdoc/>
            public override void OnUpstreamFinish()
            {
                // ✨ Convert once at the very end — O(N) single pass, no incremental re-alloc.
                _promise.TrySetResult(_buf.ToSeq());
                _completionSignalled = true;
                CompleteStage();
            }

            /// <inheritdoc/>
            public override void OnUpstreamFailure(Exception e)
            {
                _promise.TrySetException(e);
                _completionSignalled = true;
                FailStage(e);
            }

            /// <inheritdoc/>
            public override void PostStop()
            {
                if (!_completionSignalled)
                    _promise.TrySetException(new AbruptStageTerminationException(this));
            }

            /// <inheritdoc/>
            public override void PreStart() => Pull(_stage.In);
        }
    }
}

