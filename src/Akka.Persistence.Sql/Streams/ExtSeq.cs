// -----------------------------------------------------------------------
//  <copyright file="ExtSeq.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Streams.Dsl;
using LanguageExt;

namespace Akka.Persistence.Sql.Streams
{
    public static class ExtSeq
    {
        /// <summary>
        ///     Returns a <see cref="Sink{TIn,TMat}"/> that collects all elements into a
        ///     <see cref="Seq{TIn}"/> by calling <c>Seq&lt;T&gt;.Add()</c> on every push.
        ///     Simple, but incurs O(N) array re-allocations for N elements.
        ///     Prefer <see cref="SeqViaList{TIn}"/> for high-volume sinks. 🐾
        /// </summary>
        public static Sink<TIn, Task<Seq<TIn>>> Seq<TIn>() => Sink.FromGraph(new ExtSeqStage<TIn>());

        /// <summary>
        ///     Returns a <see cref="Sink{TIn,TMat}"/> that collects all elements into an
        ///     internal <see cref="List{TIn}"/> (geometric growth) and converts to
        ///     <see cref="Seq{TIn}"/> exactly once on stream completion.
        ///     <para>
        ///         This avoids the per-element array re-allocation that <see cref="Seq{TIn}"/>
        ///         incurs, making it much more allocation-friendly for larger sequences. UwU~ ✨
        ///     </para>
        /// </summary>
        /// <param name="initialCapacity">
        ///     Initial capacity hint for the internal <see cref="List{TIn}"/>.
        ///     Pass a rough upper-bound when known to skip the first few geometric resizes.
        ///     Defaults to 16.
        /// </param>
        public static Sink<TIn, Task<Seq<TIn>>> SeqViaList<TIn>(int initialCapacity = 16)
            => Sink.FromGraph(new ListExtSeqStage<TIn>(initialCapacity));
    }
}
