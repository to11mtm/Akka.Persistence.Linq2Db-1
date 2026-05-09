# `GeneralPurposeChannelQueueWithBatch<TInput, TBatch>` Design Doc ✨🌸

> **Ami-chan note, uwu~** This document specifies the next-gen replacement for
> [`ChannelQueueWithBatch`](./ChannelQueueWithBatch.cs). It is a *design* doc —
> no production code is committed alongside it. Implementation lands in a
> follow-up PR after sign-off on the open questions in §10. ❤️

---

## 1. Summary

`GeneralPurposeChannelQueueWithBatch<TInput, TBatch>` is a self-contained,
bounded, eagerly-batching `Channel<TInput, TBatch>`. Producers write
`TInput` items through its `Writer`, the batcher folds them via
user-supplied `seed` / `aggregate` delegates up to a `maxWeight` cost
budget, and consumers read fully formed `TBatch` aggregates through its
`Reader`. It behaves *like* a `BoundedChannel<TBatch>` rather than merely
wrapping one — the input buffer is owned internally and configured via
`BoundedChannelOptions`, exactly as you'd expect from
`Channel.CreateBounded<T>`. Drop-in replacement for the current
`Channel<TInput>` + `ChannelQueueWithBatch<TInput,TBatch>` pair used in
`BaseByteArrayJournalDao`. ⚡️

---

## 2. Motivation

Concrete pain points in the current
[`ChannelQueueWithBatch.cs`](./ChannelQueueWithBatch.cs):

| # | Issue | Code reference |
|---|---|---|
| M1 | Type is only a `ChannelReader<TBatch>`. Callers must construct and hold a separate input `Channel<TInput>` + `ChannelWriter<TInput>`, leaking ownership of the buffer. | `ChannelQueueWithBatch.cs:47` and [`BaseByteArrayJournalDao.cs:61–69, 95–117`](../Journal/Dao/BaseByteArrayJournalDao.cs) |
| M2 | `Completion` simply forwards `_inputReader.Completion`, so a parked `_pending` overflow item is reported as "done" while still holding an unread record → silent data loss on shutdown. | `ChannelQueueWithBatch.cs:117` |
| M3 | `lock(_gate)` spans the entire drain loop *including* user-supplied `costFunction` / `seed` / `aggregate` callbacks. User code runs under our lock — deadlock and tail-latency foot-gun. | `ChannelQueueWithBatch.cs:154–197` |
| M4 | `(bool HasValue, TInput Value)` tuple field copies the whole struct on every read of `_pending`. For large `TInput` this is wasteful. | `ChannelQueueWithBatch.cs:67, 162, 184` |
| M5 | No validation that `costFunction` returns a positive value. A `0` cost makes the drain loop never decrement `remaining`, producing an unbounded batch (memory blow-up, not infinite loop). Negative cost is even worse. | `ChannelQueueWithBatch.cs:173, 178` |
| M6 | A single oversized item silently emits a batch larger than `maxWeight`. Matches `EagerBatchStage`, but is undocumented and surprising. | `ChannelQueueWithBatch.cs:171–173` |
| M7 | No write-side surface: no `WaitToWriteAsync`, no `Complete(Exception?)`, no cancellation. Backpressure is achieved only by callers manually awaiting `WriteAsync` on the *external* channel. | n/a |
| M8 | `WaitToReadAsync` lock-then-delegate has a benign-but-undocumented race window. Threading contract is implicit. | `ChannelQueueWithBatch.cs:129–139` |

uwu~ that's a lot of papercuts for one little class. Time for an upgrade. 💖

---

## 3. Goals & Non-Goals

### Goals 🎯

- Own and manage the bounded input buffer as a **custom ring buffer**
  (circular array + lock + blocked-writer queue), similar to how
  `BoundedChannel<T>` is implemented internally in the .NET runtime.
  Caller supplies `BoundedChannelOptions` for configuration only —
  no inner `Channel<TInput>` is created.
- Inherit from `Channel<TInput, TBatch>` so `q.Reader` / `q.Writer` /
  `Source.ChannelReader(q)` Just Work™.
- MPSC-safe producers, lock-free single-consumer fast path on `TryRead`.
- Drain-on-complete: pending overflow + buffered items emit final batches
  before `Completion` fires.
- Validated `costFunction` (positive-cost invariant).
- `IAsyncEnumerable<TBatch>` ergonomics inherited automatically.
- Fault propagation from writer-completion-with-error.

### Non-Goals 🙅‍♀️

- Replacing downstream `SelectAsync` parallelism in the journal DAO.
- Multi-consumer batching (we explicitly target `SingleReader = true`).
- Per-item priority / out-of-order draining.
- Bounding by *batch count* instead of *input items* (called out as
  future work in §10).

---

## 4. Threading Model & Concurrency Contract 🧵

**Target: MPSC** (multi-producer, single-consumer). This matches existing
usage at [`BaseByteArrayJournalDao.cs:99`](../Journal/Dao/BaseByteArrayJournalDao.cs)
where `SingleReader = true` is already set.

- **Producers** invoke `Writer.WriteAsync` / `TryWrite` from any thread.
  All writes to the ring buffer are protected by a shared `_lock` object,
  giving natural MPSC correctness without spin-waiting. Blocked writers
  are parked in a `Queue<PendingWrite>` under the same lock.
- **Consumer** is *exactly one* logical reader (Akka Streams stage
  materialised once). The `_pending` overflow slot is touched **only** by
  the consumer thread — it is consumer-local state and never enters the
  lock.
- **Memory ordering:** every ring-buffer dequeue acquires `_lock`, which
  provides an acquire fence that publishes all prior producer writes to
  the consumer. `_pending` is consumer-local, so plain field reads/writes
  suffice.
- **Misuse:** invoking `Reader.TryRead` from multiple threads is a bug
  and will be guarded only by `Debug.Assert` (no production cost).

> **CopilotNote:** Do *not* be tempted to make this MPMC "for free" by
> adding an interlocked `_pending`. The aggregate function may be
> non-commutative (e.g. `ImmutableList.Concat` in the journal DAO);
> reordering aggregation across consumers breaks ordering guarantees.

---

## 5. Public API Surface 💎

```csharp
public sealed class GeneralPurposeChannelQueueWithBatch<TInput, TBatch>
    : Channel<TInput, TBatch>
{
    public GeneralPurposeChannelQueueWithBatch(
        BoundedChannelOptions inputOptions,
        long maxWeight,
        Func<TInput, long> costFunction,
        Func<TInput, TBatch> seed,
        Func<TBatch, TInput, TBatch> aggregate);

    /// <summary>Convenience for the common case.</summary>
    public static GeneralPurposeChannelQueueWithBatch<TInput, TBatch> CreateBounded(
        int capacity,
        long maxWeight,
        Func<TInput, long> costFunction,
        Func<TInput, TBatch> seed,
        Func<TBatch, TInput, TBatch> aggregate);

    // Inherited from Channel<TInput, TBatch>:
    //   public ChannelReader<TBatch>  Reader { get; }
    //   public ChannelWriter<TInput>  Writer { get; }
}
```

`inputOptions` is normalised internally: `SingleReader` is forced to
`true`; `FullMode` defaults to `Wait` if not set. Other fields
(`Capacity`, `SingleWriter`, `AllowSynchronousContinuations`) are
honoured as-is.

### Producer-side example

```csharp
// Backpressured enqueue, uwu~
await queue.Writer.WriteAsync(entry, ct);

// Or fast-path try:
if (!queue.Writer.TryWrite(entry))
    await queue.Writer.WriteAsync(entry, ct);
```

### Consumer-side example

```csharp
await foreach (var batch in queue.Reader.ReadAllAsync(ct))
    await ProcessBatchAsync(batch);
```

### Migrated DAO example (vs. today)

```csharp
// Before: two fields, manual wiring.
private readonly Channel<WriteQueueEntry> _inputChannel;
private readonly ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet> _batcher;

// After: one field, owned bound + batching together.
private readonly GeneralPurposeChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet> _queue;

_queue = new GeneralPurposeChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet>(
    inputOptions: new BoundedChannelOptions(JournalConfig.DaoConfig.BufferSize)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        AllowSynchronousContinuations = false,
    },
    maxWeight: JournalConfig.DaoConfig.BatchSize,
    costFunction: e => e.Rows.Count,
    seed: r => new WriteQueueSet(
        ImmutableList.Create([r.Tcs]), r.Rows,
        ImmutableList.Create([r.CancellationToken])),
    aggregate: (acc, x) => new WriteQueueSet(
        acc.Tcs.Add(x.Tcs),
        acc.Rows.Concat(x.Rows),
        acc.CancellationTokens.Add(x.CancellationToken)));

Source.ChannelReader(_queue) // implicit Channel<,> → ChannelReader<TBatch>
      .Buffer(...)
      .SelectAsync(...)
      .To(Sink.Ignore<NotUsed>())
      .Run(Materializer);
```

---

## 6. Internal Design 🛠️

```text
┌─────────────────────────────────────────────────────────────────────┐
│  GeneralPurposeChannelQueueWithBatch<TInput, TBatch>                │
│  (extends Channel<TInput, TBatch>)                                  │
│                                                                     │
│  ┌──────────────────────────┐                                       │
│  │  BatchWriter             │  IsBatchWriter : ChannelWriter<TInput>│
│  │  TryWrite / WriteAsync   │  (delegates into shared ring buffer)  │
│  └────────────┬─────────────┘                                       │
│               │ _lock                                               │
│               ▼                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Ring Buffer (circular array + shared _lock)                 │   │
│  │                                                              │   │
│  │  TInput[] _buffer      ← fixed-size circular array           │   │
│  │  int _head, _count     ← protected by _lock                  │   │
│  │  Queue<PendingWrite>   ← blocked writers (FullMode=Wait)     │   │
│  │  TCS<bool>? _blockedReader ← at most ONE (SingleReader=true) │   │
│  │  bool _doneWriting     ← set when writer calls Complete()    │   │
│  │  Exception? _doneError ← non-null if faulted                 │   │
│  └──────────────────────────┬───────────────────────────────────┘   │
│                             │ consumer dequeues here                │
│                             ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  BatchReader : ChannelReader<TBatch>                         │   │
│  │  ─ consumer-local _pendingItem / _hasPending  (no lock)      │   │
│  │  ─ TryRead: dequeues items 1-by-1 (lock per dequeue),        │   │
│  │             runs seed/aggregate/costFunction OUTSIDE lock    │   │
│  │  ─ Completion TCS resolves only when ring empty +            │   │
│  │    !_hasPending + _doneWriting                               │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.1 Shared State Layout

```csharp
// ── Shared state: all fields below protected by _lock ───────────────
private readonly object _lock = new();
private readonly TInput[] _buffer;          // length = capacity from options
private int _head;                          // index of oldest item
private int _count;                         // number of stored items
                                            // tail = (_head + _count) % capacity
private readonly Queue<PendingWrite> _blockedWriters = new();
private TaskCompletionSource<bool>? _blockedReader; // at most one (SingleReader)
private bool _doneWriting;
private Exception? _doneError;

// ── Consumer-local state: ONLY touched by the single reader ─────────
// CopilotNote: these fields are intentionally NOT under _lock.
// They are safe because SingleReader=true is enforced by the ctor.
private TInput _pendingItem;    // overflow item from prior TryRead
private bool _hasPending;

// ── Reader's own Completion task ────────────────────────────────────
private readonly TaskCompletionSource<bool> _readerCompletion
    = new(TaskCreationOptions.RunContinuationsAsynchronously);

// ── PendingWrite (blocked producer waiting for room) ────────────────
private struct PendingWrite
{
    public TInput Item;
    public TaskCompletionSource<bool> Tcs;   // completed when item enqueued
    public CancellationTokenRegistration CancellationReg;
}
```

> **CopilotNote:** `_buffer` is a flat `TInput[]`. Head advances on each
> dequeue; tail = `(_head + _count) % _buffer.Length`. No `ArraySegment`
> or `Span` wrapping needed — direct indexed access keeps GC pressure low.

### 6.2 Write Path (`TryWrite` / `WriteAsync`)

```
TryWrite(item):
  lock:
    if _doneWriting: return false              // channel closed
    if _count < capacity:
      _buffer[(_head + _count) % cap] = item   // enqueue at tail
      _count++
      WakeBlockedReader()                       // reader was parked? notify
      return true
    switch FullMode:
      DropWrite:  return false
      DropNewest: return true (item silently dropped)
      DropOldest: _buffer[_head] = item         // overwrite oldest
                  _head = (_head + 1) % cap     // (count unchanged)
                  WakeBlockedReader(); return true
      Wait:       return false                   // caller must use WriteAsync

WriteAsync(item, ct):
  ct.ThrowIfCancellationRequested()
  lock:
    fast path: same as TryWrite Wait-mode accept (return completed)
    if _doneWriting: throw ChannelClosedException
    // Buffer full, FullMode=Wait — park the writer
    pw = new PendingWrite { Item=item, Tcs=new TCS(RunContinuationsAsynchronously) }
    register ct cancellation → removes pw from queue, sets pw.Tcs cancelled
    _blockedWriters.Enqueue(pw)
  await pw.Tcs.Task  // released when a reader dequeues an item
```

### 6.3 Read Path (`TryRead` — batch)

The critical insight: the `_lock` is **held only for the ring-buffer
dequeue** (O(1), no user code). All `costFunction` / `seed` / `aggregate`
calls happen **outside the lock**. This fixes M3. UwU~

```
BatchReader.TryRead(out TBatch batch):

  // ── Step 1: obtain the first item ──────────────────────────────
  TInput firstItem
  if _hasPending:                          // consumer-local, NO LOCK
    firstItem = _pendingItem
    _hasPending = false
  else:
    lock: if _count > 0: dequeue → firstItem; DrainBlockedWriter(); else return false
    // if dequeue fails AND _doneWriting: CheckCompletion()

  // ── Step 2: seed the batch ── OUTSIDE LOCK ─────────────────────
  long cost = costFunction(firstItem)      // validated > 0 (see §6.5)
  batch     = seed(firstItem)
  remaining = maxWeight - cost

  // ── Step 3: eagerly drain ── OUTSIDE LOCK between dequeues ─────
  while remaining > 0:
    TInput next
    lock: if _count > 0: dequeue → next; DrainBlockedWriter()
          else: break

    // OUTSIDE LOCK:
    cost = costFunction(next)
    if cost > remaining:
      _pendingItem = next; _hasPending = true   // consumer-local, NO LOCK
      break

    batch     = aggregate(batch, next)           // user code, NO LOCK
    remaining -= cost

  return true

DrainBlockedWriter():    // called inside _lock after freeing one slot
  if _blockedWriters.Count == 0: return
  pw = _blockedWriters.Dequeue()
  _buffer[(_head + _count) % cap] = pw.Item
  _count++
  pw.CancellationReg.Dispose()
  pw.Tcs.TrySetResult(true)
  // (no WakeBlockedReader needed — consumer already awake, it called us)
```

### 6.4 `WaitToReadAsync` and `WakeBlockedReader`

```
BatchReader.WaitToReadAsync(ct):
  if _hasPending: return s_trueTask            // cached, zero-alloc, NO LOCK

  lock:
    if _count > 0:        return s_trueTask    // cached
    if _doneWriting && _count == 0:
      CheckCompletion()                        // may resolve _readerCompletion
      return s_falseTask                       // cached
    // Park the reader (at most one due to SingleReader)
    tcs = new TCS<bool>(RunContinuationsAsynchronously | allow-sync flag)
    _blockedReader = tcs
    // register ct → cancel tcs + clear _blockedReader
  return new ValueTask<bool>(tcs.Task)

WakeBlockedReader():    // called inside _lock after enqueuing an item
  if _blockedReader is null: return
  tcs = _blockedReader; _blockedReader = null
  tcs.TrySetResult(true)   // continuation runs on thread-pool (async flag)
```

`s_trueTask` and `s_falseTask` are `static readonly ValueTask<bool>`
cached instances, mirroring the same optimisation in `BoundedChannel<T>`.

### 6.5 Cost Validation

```
// Called inside TryRead before seeding; throws OUTSIDE _lock.
if (cost <= 0)
    throw new InvalidOperationException(
        $"costFunction returned {cost} for item of type {typeof(TInput).Name}. " +
        "Cost must be greater than zero.");
```

The exception propagates out of `TryRead`, faults `_readerCompletion`,
and the consumer's `ReadAllAsync` loop will surface it. (M5 fixed.)

### 6.6 Completion Plumbing

```
Writer.TryComplete(Exception? error):
  lock:
    if _doneWriting: return false
    _doneWriting = true; _doneError = error
    if error != null:
      // Fault every blocked writer
      foreach pw in _blockedWriters: pw.Tcs.TrySetException(error)
      _blockedWriters.Clear()
    else:
      // Release blocked writers so they can re-check (they will observe
      // ChannelClosedException on their next attempt)
      foreach pw in _blockedWriters: pw.Tcs.TrySetException(ClosedException)
      _blockedWriters.Clear()
    CheckCompletion()   // if buffer already empty, resolve now
    return true

CheckCompletion():   // must be called inside _lock
  if !_doneWriting || _count > 0 || _hasPending: return
  if _blockedReader != null:
    tcs = _blockedReader; _blockedReader = null
    tcs.TrySetResult(false)              // nothing left to read
  if _doneError != null:
    _readerCompletion.TrySetException(_doneError)
  else:
    _readerCompletion.TrySetResult(true)
```

`BatchReader.Completion` simply returns `_readerCompletion.Task`.

> **CopilotNote:** `CheckCompletion` is also called from `TryRead` (after a
> dequeue that empties the buffer) and from `WaitToReadAsync` (when
> reaching the park branch with an empty + completed buffer). This ensures
> `_readerCompletion` resolves promptly regardless of which code path
> discovers the drain-complete condition. 🌸

### 6.7 `inputOptions` Normalisation

```csharp
// ctor normalisation:
inputOptions.SingleReader = true;  // enforced; we optimise around it
// AllowSynchronousContinuations drives the TCS creation flags (stored as a field)
// FullMode, Capacity, SingleWriter are honoured as-is
```

---

## 7. Correctness Analysis 🔍

| # | Scenario | Current behavior | New specified behavior |
|---|---|---|---|
| C1 | Empty channel + writer completes | `WaitToReadAsync → false`, `Completion` done | Same. |
| C2 | One item buffered + writer completes | Batch emitted; **`Completion` may already be done** before consumer reads it (M2) | Batch emitted; `Completion` only resolves *after* consumer has drained pending + buffer. |
| C3 | Pending overflow + writer completes | Same as M2: completion races ahead | Pending item is emitted as final batch, then `Completion` resolves. |
| C4 | Single oversized item (`cost > maxWeight`) | Silently emits over-budget batch | Same, **documented** on ctor xmldoc. |
| C5 | `costFunction` returns 0 | Drain loop never decrements `remaining` → unbounded batch | `InvalidOperationException` thrown from `TryRead`, faults `Completion`. |
| C6 | `costFunction` returns negative | `remaining` *grows* → unbounded batch | Same `InvalidOperationException`. |
| C7 | Writer faults via `TryComplete(ex)` | Forwarded as faulted `Completion` | Same; any in-flight pending item is **discarded** and the exception wins. |
| C8 | `WriteAsync` cancelled while buffer full | n/a (caller-owned writer) | Propagates `OperationCanceledException`; no item enqueued. |
| C9 | `TryWrite` after `Complete()` | n/a | Returns `false` (delegated to inner writer). |
| C10 | Concurrent producers | Safe (caller's responsibility on the external channel) | Safe (serialised by `_lock` on the ring buffer). |
| C11 | Concurrent *consumers* | Serialised via `_gate` lock | **Undefined / `Debug.Assert` failure.** SPSC contract documented. |

---

## 8. Performance Considerations ⚡️

- **Lock held only for ring-buffer dequeue (O(1))** — `costFunction`,
  `seed`, and `aggregate` are always called outside the lock. The
  critical section is a handful of arithmetic ops and an indexed
  array write/read. Compare: old design held `_gate` across all user
  callbacks for the entire batch (M3 fixed). 🔥
- **Cache-friendly buffer layout** — `TInput[]` is a single contiguous
  heap allocation. Sequential dequeuing walks cache lines in order until
  wrap-around; no pointer-chasing through a linked `Queue<T>` node chain.
- **Cached `ValueTask<bool>`** — `s_trueTask` / `s_falseTask` static
  fields (mirroring `BoundedChannel<T>`'s optimisation) eliminate
  `ValueTask` boxing on the hot `WaitToReadAsync` paths.
- **No tuple / struct-copy for overflow** (M4) — two plain typed fields
  (`_pendingItem`, `_hasPending`) are consumer-local and cost nothing
  to access.
- **Cost computed once per item**, reused across validation and budget
  subtraction; no double-evaluation.
- **Blocked writers: `Queue<PendingWrite>` allocation only on
  contention** — the `TCS` inside `PendingWrite` is allocated only when
  `FullMode = Wait` and the buffer is actually full. In the common
  low-load case, `TryWrite` succeeds in the lock's fast path with zero
  heap allocation beyond the item itself.
- **`AllowSynchronousContinuations = false`** kept as default in
  `CreateBounded` factory to preserve the journal DAO's actor-thread
  isolation. Setting it to `true` removes the thread-pool hop for
  continuations but risks re-entrancy on the Akka dispatcher — document
  this trade-off in the ctor xmldoc.
- **Out-of-scope but flagged:** the user `aggregate` in the journal DAO
  uses `ImmutableList.Concat` ([DAO L113–117](../Journal/Dao/BaseByteArrayJournalDao.cs))
  which is the dominant allocator. A future PR could swap to a builder /
  pooled `List<T>`. Not this design's problem. 🍡

---

## 9. Migration Plan 🚚 *(Deferred — tracked separately)*

> ⏸️ **Status: DEFERRED.** Migration of `BaseByteArrayJournalDao` and deletion
> of the old `ChannelQueueWithBatch` are out of scope for this PR. They will be
> addressed in a dedicated follow-up issue once the new implementation is
> reviewed, merged, and has test coverage. The notes below are preserved for
> reference only.

<details>
<summary>Deferred migration notes (click to expand)</summary>

1. Add `GeneralPurposeChannelQueueWithBatch.cs` next to the existing
   `ChannelQueueWithBatch.cs`. Keep both temporarily.
2. Update [`BaseByteArrayJournalDao.cs:61–117`](../Journal/Dao/BaseByteArrayJournalDao.cs):
   - Collapse `_inputChannel` + `_batcher` into a single `_queue` field.
   - Replace ctor wiring with the §5 example.
   - Update `QueueWriteJournalRows` to call `_queue.Writer.TryWrite` /
     fall back to `await _queue.Writer.WriteAsync(...)` for true
     backpressure (today the code uses `TryWrite` only and relies on
     `BoundedChannelFullMode.Wait` semantics from the side; behaviour
     is preserved with the explicit `WriteAsync` path).
   - Reuse the existing `Completion` triage at
     [DAO L173–182](../Journal/Dao/BaseByteArrayJournalDao.cs) verbatim;
     its semantics (`Dropped` / `Failure` / `QueueClosed`) map 1:1.
3. Update [`Journal/Dao/ChannelQueueWithBatch.md`](../Journal/Dao/ChannelQueueWithBatch.md)
   status section to mark it superseded by this doc.
4. Once green, delete `ChannelQueueWithBatch.cs` and its test (if any).

**Behaviour change to call out in release notes:** `Completion` of the
queue is now strictly observed *after* all batches are drained, fixing
a small shutdown-race window where in-flight writes could be lost.

</details>

---

## 10. Open Questions ❓

1. **Inheritance vs composition.** Inherit from `Channel<TInput, TBatch>`
   (recommended) for drop-in `Source.ChannelReader(queue)` compatibility,
   ~~or expose `Reader` / `Writer` properties on a standalone class?~~
   *Recommendation:* inheritance.
2. **Bound semantics.** Today the input bound is in *items*
   (`BufferSize = 5000`) and the batch bound is in *weight*
   (`BatchSize = 100`). Keep both, ~~or expose a unified weight-based
   input bound?~~ *Recommendation:* keep both; no behaviour change.
3. **Oversized-item passthrough.** Preserve `EagerBatchStage` semantics
   (item passes through as a one-element over-budget batch), ~~or reject
   such items synchronously from `Writer.TryWrite`?~~ *Recommendation:*
   preserve, document loudly.
4. **Final partial batch on faulted completion.** When the writer
   faults, do we still emit any in-flight aggregated batch before
   propagating the fault? *Recommendation:* Yes — allow drain, matches
   `BoundedChannel<T>` semantics.
5. **Non-generic abstract base** for testing seams? NO!

---

## 11. Testing Strategy 🧪

New spec at
`src/Akka.Persistence.Sql.Tests/Utility/GeneralPurposeChannelQueueWithBatchSpec.cs`,
xUnit + FluentAssertions to match project conventions.

Unit tests:

- `EmptyChannel_CompleteWriter_CompletionResolves` (C1)
- `BufferedItem_CompleteWriter_BatchEmittedBeforeCompletion` (C2)
- `OverflowPending_CompleteWriter_PendingDrainedBeforeCompletion` (C3)
- `OversizedItem_EmittedAsOverBudgetBatch_DocumentedBehavior` (C4)
- `ZeroCost_ThrowsInvalidOperationException` (C5)
- `NegativeCost_ThrowsInvalidOperationException` (C6)
- `WriterFaulted_CompletionPropagatesException` (C7)
- `WriteAsync_CancelledWhileBufferFull_ThrowsAndDoesNotEnqueue` (C8)
- `TryWrite_AfterComplete_ReturnsFalse` (C9)
- `WaitToReadAsync_WithPending_ReturnsCachedTrue` (perf/alloc check)

Stress / property tests:

- `NProducers_SingleConsumer_AllItemsObservedExactlyOnce` — randomised
  cost, randomised producer count, asserts sum-of-batches equals
  sum-of-inputs.
- `BackpressureUnderSlowConsumer_ProducersBlockWithoutDataLoss`.

Regression tests:

- One named test per row of the §2 issue table, asserting the new
  behaviour and pinning the fix.

---

---

## 12. Benchmark Plan ⚡️🍡

> **Goal:** compare raw throughput and allocation of the new
> `GeneralPurposeChannelQueueWithBatch` against the current
> `ChannelQueueWithBatch` (inner `Channel<T>` + reader wrapper pair)
> on a **single-threaded** produce → drain loop. No concurrency stress —
> just hot-path perf numbers that motivate (or challenge!) the ring-buffer
> design.

### Location

New file:

```
src/Akka.Persistence.Sql.Benchmarks/Utility/ChannelQueueBatchBenchmarks.cs
```

Uses the existing `MicroBenchmarkConfig` (adds `MemoryDiagnoser` +
`MarkdownExporter.GitHub`) from
`Akka.Persistence.Sql.Benchmarks/Configurations/Configs.cs`. No new
NuGet dependencies needed — `BenchmarkDotNet` is already referenced. 🎉

### Benchmark Fixture

```csharp
/// <summary>
/// Micro-benchmarks comparing the legacy ChannelQueueWithBatch (inner Channel wrapper)
/// against the new GeneralPurposeChannelQueueWithBatch (custom ring buffer).
/// Single-threaded: no concurrent producers or consumers. UwU~
/// </summary>
/// <remarks>
/// CopilotNote: Keep these benchmarks free of async overhead where possible.
/// TryWrite / TryRead are the hot paths we care about here.
/// </remarks>
[Config(typeof(MicroBenchmarkConfig))]
[BenchmarkCategory("ChannelQueueBatch")]
public class ChannelQueueBatchBenchmarks
{
    // ── Params ────────────────────────────────────────────────────────────
    /// <summary>Total number of items enqueued per benchmark invocation.</summary>
    [Params(100, 1_000, 10_000)]
    public int ItemCount { get; set; }

    /// <summary>
    /// Max weight per batch. Small value → many small batches.
    /// Large value → few large batches (tests aggregate call depth).
    /// </summary>
    [Params(10, 100)]
    public long MaxWeight { get; set; }

    // ── SUT types ─────────────────────────────────────────────────────────
    // (Use int as TInput and List<int> as TBatch for zero-domain overhead.)
    private Channel<int> _legacyInputChannel = null!;
    private ChannelQueueWithBatch<int, List<int>> _legacyBatcher = null!;

    private GeneralPurposeChannelQueueWithBatch<int, List<int>> _newQueue = null!;

    // ── Setup ─────────────────────────────────────────────────────────────
    [GlobalSetup]
    public void Setup()
    {
        // Legacy: separate channel + reader-wrapper pair
        _legacyInputChannel = Channel.CreateBounded<int>(
            new BoundedChannelOptions(ItemCount)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            });

        _legacyBatcher = new ChannelQueueWithBatch<int, List<int>>(
            inputReader:   _legacyInputChannel.Reader,
            maxWeight:     MaxWeight,
            costFunction:  _ => 1,
            seed:          x => new List<int> { x },
            aggregate:     (acc, x) => { acc.Add(x); return acc; });

        // New: self-contained ring-buffer channel
        _newQueue = GeneralPurposeChannelQueueWithBatch<int, List<int>>.CreateBounded(
            capacity:      ItemCount,
            maxWeight:     MaxWeight,
            costFunction:  _ => 1,
            seed:          x => new List<int> { x },
            aggregate:     (acc, x) => { acc.Add(x); return acc; });
    }

    // ── Benchmarks ────────────────────────────────────────────────────────

    /// <summary>Enqueue ItemCount items into the legacy channel (write path only).</summary>
    [Benchmark(Baseline = true, Description = "Legacy – Write")]
    public void Legacy_Write()
    {
        for (var i = 0; i < ItemCount; i++)
            _legacyInputChannel.Writer.TryWrite(i);
    }

    /// <summary>Enqueue ItemCount items into the new ring-buffer queue (write path only).</summary>
    [Benchmark(Description = "New – Write")]
    public void New_Write()
    {
        for (var i = 0; i < ItemCount; i++)
            _newQueue.Writer.TryWrite(i);
    }

    /// <summary>
    /// Round-trip: fill then fully drain the legacy queue, collecting all batches.
    /// Exercises the entire hot path: TryWrite → TryRead → seed/aggregate loop.
    /// </summary>
    [Benchmark(Description = "Legacy – Write + Drain")]
    public int Legacy_WriteAndDrain()
    {
        // Fill
        for (var i = 0; i < ItemCount; i++)
            _legacyInputChannel.Writer.TryWrite(i);

        // Drain all batches
        var totalItems = 0;
        while (_legacyBatcher.TryRead(out var batch))
            totalItems += batch.Count;

        return totalItems; // prevent dead-code elimination
    }

    /// <summary>
    /// Round-trip: fill then fully drain the new queue, collecting all batches.
    /// Direct apples-to-apples comparison with Legacy_WriteAndDrain.
    /// </summary>
    [Benchmark(Description = "New – Write + Drain")]
    public int New_WriteAndDrain()
    {
        // Fill
        for (var i = 0; i < ItemCount; i++)
            _newQueue.Writer.TryWrite(i);

        // Drain all batches
        var totalItems = 0;
        while (_newQueue.Reader.TryRead(out var batch))
            totalItems += batch.Count;

        return totalItems;
    }
}
```

### What each benchmark measures

| Benchmark | Measures |
|---|---|
| `Legacy – Write` | Raw `TryWrite` throughput on the inner `BoundedChannel<int>`. |
| `New – Write` | Raw `TryWrite` throughput on the new ring-buffer write path (lock + index math). |
| `Legacy – Write + Drain` | Full round-trip allocation + CPU for old design. **This is the primary regression signal.** |
| `New – Write + Drain` | Full round-trip for new design. Lock held only O(1) per dequeue; `seed`/`aggregate` outside lock. |

`MemoryDiagnoser` will show allocations per operation — we expect the new
design to allocate fewer ephemeral objects on the read path due to no
`(bool, TInput)` tuple and no `_gate` monitor-object contention.

### How to run 🚀

```powershell
# from repo root, Release build required for valid BDN numbers! uwu~
cd src/Akka.Persistence.Sql.Benchmarks
dotnet run -c Release -- --filter "*ChannelQueueBatch*"
```

### Acceptance criteria 🎯

- `New – Write + Drain` throughput ≥ `Legacy – Write + Drain` across all
  `[Params]` combinations (no regression).
- `New – Write + Drain` allocated bytes per op ≤ legacy (ideally lower
  due to removed tuple + `_gate` overhead).
- Results saved to `BenchmarkDotNet.Artifacts/` and attached to the PR as
  a GitHub Markdown table (auto-produced by `MarkdownExporter.GitHub`).

> **CopilotNote:** If the ring-buffer `TryWrite` lock shows up as hot in
> the write-only benchmark (it shouldn't for single-threaded), that's a
> sign the normalisation / ctor cost is bleeding into the iteration.
> Check `[IterationSetup]` vs `[GlobalSetup]` placement. 🌸

---

> *Ami-chan signing off, uwu~* This doc is ready for review. Please
> resolve §10 open questions before the implementation PR lands. 🌸✨💖

