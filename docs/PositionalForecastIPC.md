# Positional Forecast IPC

Wrath Combo publishes the next positional selected by its configured rotation.
The channel is optional: Wrath does not require a consumer, and consumers must
continue operating when Wrath is unavailable.

## Current snapshot

Channel: `WrathCombo.PositionalForecast.Get.V1`

Signature: `Func<string>`

The returned value is pipe-delimited:

```text
version|actionId|requirement|gcdsUntil|targetObjectId|generation
```

- `version`: currently `1`
- `requirement`: `0` none, `1` rear, `2` flank
- `gcdsUntil`: one-based; `1` means the next GCD is the positional
- `targetObjectId`: the target for which the forecast was calculated
- `generation`: monotonically increasing update identifier

## Change notification

Channel: `WrathCombo.PositionalForecast.Changed.V1`

Signature:

```csharp
Action<uint, int, int, ulong, long>
```

Parameters are `actionId`, `requirement`, `gcdsUntil`, `targetObjectId`, and
`generation`.

Consumers should subscribe to the notification and invoke the snapshot getter
on startup or after reconnecting. Forecasts are transient and should be expired
if updates stop.
