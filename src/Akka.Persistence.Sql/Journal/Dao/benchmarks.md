## Before Replacing `Source.Queue`:

- With Tags on TagTable, no AsQueryable, PersistGroup200:

```
Mean throughput: 5.83 msg/s/actor, Mean total throughput: 1166.39 msg/s
Median throughput: 5.78 msg/s/actor, Median total throughput: 1156.09 msg/s
```

- With Tags on TagTable, with AsQueryable, PersistGroup200:

```
Mean throughput: 8.94 msg/s/actor, Mean total throughput: 1788.08 msg/s
Median throughput: 8.88 msg/s/actor, Median total throughput: 1776.13 msg/s
```

- No Tags, CSV Mode, PersistGroup200:

```
Mean throughput: 122.98 msg/s/actor, Mean total throughput: 24596.81 msg/s
Median throughput: 126.44 msg/s/actor, Median total throughput: 25287.42 msg/s
```

- No Tags, CSV Mode, PersistGroup400:

```
Mean throughput: 67.41 msg/s/actor, Mean total throughput: 26962.66 msg/s
Median throughput: 69.13 msg/s/actor, Median total throughput: 27650.78 msg/s
```

## After replacing with `ChannelQueueWithBatch`:

- With Tags on TagTable, no AsQueryable, PersistGroup200:

```
Mean throughput: 10.33 msg/s/actor, Mean total throughput: 2066.52 msg/s
Median throughput: 10.41 msg/s/actor, Median total throughput: 2081.77 msg/s
```

- With Tags on TagTable, with AsQueryable, PersistGroup200:

```
Mean throughput: 15.91 msg/s/actor, Mean total throughput: 3181.94 msg/s
Median throughput: 16.00 msg/s/actor, Median total throughput: 3199.69 msg/s
```

- No Tags, CSV Mode, PersistGroup200:

```
Mean throughput: 121.54 msg/s/actor, Mean total throughput: 24307.63 msg/s
Median throughput: 124.40 msg/s/actor, Median total throughput: 24880.36 msg/s
```

- No Tags, CSV Mode, PersistGroup400:

```
Mean throughput: 79.36 msg/s/actor, Mean total throughput: 31743.98 msg/s
Median throughput: 80.45 msg/s/actor, Median total throughput: 32179.37 msg/s
```

# RESULTS:

| Scenario | Batch Size | Implementation | Mean (msg/s/actor) | Mean Total (msg/s) | Median (msg/s/actor) | Median Total (msg/s) | Δ Mean Total |
|---|---|---|---:|---:|---:|---:|---:|
| Tags on TagTable, no AsQueryable | PersistGroup200 | `Source.Queue` | 5.83 | 1,166.39 | 5.78 | 1,156.09 | |
| Tags on TagTable, no AsQueryable | PersistGroup200 | `ChannelQueueWithBatch` | 10.33 | 2,066.52 | 10.41 | 2,081.77 | **+77.2%** |
| Tags on TagTable, with AsQueryable | PersistGroup200 | `Source.Queue` | 8.94 | 1,788.08 | 8.88 | 1,776.13 | |
| Tags on TagTable, with AsQueryable | PersistGroup200 | `ChannelQueueWithBatch` | 15.91 | 3,181.94 | 16.00 | 3,199.69 | **+78.0%** |
| No Tags, CSV Mode | PersistGroup200 | `Source.Queue` | 122.98 | 24,596.81 | 126.44 | 25,287.42 | |
| No Tags, CSV Mode | PersistGroup200 | `ChannelQueueWithBatch` | 121.54 | 24,307.63 | 124.40 | 24,880.36 | **-1.2%** |
| No Tags, CSV Mode | PersistGroup400 | `Source.Queue` | 67.41 | 26,962.66 | 69.13 | 27,650.78 | |
| No Tags, CSV Mode | PersistGroup400 | `ChannelQueueWithBatch` | 79.36 | 31,743.98 | 80.45 | 32,179.37 | **+17.7%** |

