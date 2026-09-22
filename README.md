# QuantumObliterator

Turns Valheim's Obliterator from an early-game curiosity into a **paired, fuel-charged
freight terminal** — so a no-portal world stays playable once your bases are in the Deep
North and the Ashlands.

Put a sign next to an Obliterator. Two Obliterators whose signs read the same thing become
a linked pair. Pull the lever on one and its contents are moved to the other, anywhere in
the world, for the price of one fuel item.

An Obliterator with no sign behaves **exactly like vanilla**.

## How to use it

1. Build an Obliterator. Build a sign within 5 m of it (configurable).
2. Write the same text on a sign next to a second Obliterator, anywhere on the map.
3. Put one or more **Thunderstones** into the sending Obliterator, along with whatever you
   want to ship.
4. Pull the lever. The fuel is consumed, the lightning fires, and everything else lands in
   the other Obliterator.

The lever's hover text shows the tag, the current weight, and what the pull will cost —
in red if the Obliterator can't currently pay for it.

The destination does not need to be loaded. Nobody has to be standing near it.

### What it costs

Fuel scales with the **weight** of the shipment. One Thunderstone pays for
`WeightPerFuelUnit` of cargo — 1080 by default, roughly three full stacks of metal bars.

```
fuel needed = ceil( total weight / (WeightPerFuelUnit + weight of one fuel item) )
```

Adding the fuel's own weight to the divisor means each stone pays for itself *plus* its
allowance, so the fuel you burn never eats into the budget you paid for. Spare fuel you
leave behind is shipped as ordinary cargo and does count toward the weight.

With the defaults and a 10-weight Thunderstone:

| In the Obliterator | Cost |
| --- | --- |
| 1080 of bars + 1 stone | 1 stone |
| 1081 of bars + 1 stone | refused — needs 2 |
| 1081 of bars + 2 stones | 2 stones |

Set `RequireFuel = false` for free, unlimited shipping.

### Rules

- **A tag must name exactly one pair.** If three Obliterators share a tag, the transfer is
  refused and nothing is consumed. This also means tag squatting can't steal your cargo —
  a stranger reusing your tag just breaks the link for everyone.
- **All-or-nothing.** If the destination can't hold the entire shipment, nothing moves and
  no fuel is spent.
- **A tagged Obliterator never destroys items.** If the tag has no partner, you get an
  error, not a pile of coal. Losing a metal haul because a sign got knocked over would be
  a bad trade.
- Blank signs don't count as tags.

## Messages

| Message | Meaning |
| --- | --- |
| `Shipment sent to "<tag>"` | Success. |
| `No other Obliterator is tagged "<tag>"` | Build or re-sign the far end. |
| `More than one Obliterator is tagged "<tag>"` | Three or more share the tag. |
| `Requires 2 Thunderstone to charge` | Not enough fuel for this weight. |
| `Nothing to ship except the fuel` | The load was only fuel; nothing consumed. |
| `Destination Obliterator is full` | Nothing was moved. |
| `Obliterator contents changed - try again` | The server's view of the contents was stale; retry. |
| `No response from the server` | Request timed out after 10 s; nothing was consumed. |

## Configuration

`BepInEx/config/wheezl.quantumobliterator.cfg`. Every gameplay setting is **admin-only
and server-synced** — clients receive the server's values and cannot override them.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch; `false` restores pure vanilla behaviour. |
| `SignSearchRadius` | `5.0` | How far to look for a tag sign, in metres. |
| `CaseSensitiveTags` | `false` | Whether `Ashlands` and `ashlands` differ. |
| `RequireFuel` | `true` | `false` = free, unlimited transfers. |
| `FuelItems` | `Thunderstone` | Item **prefab** names, tried in order; first affordable one is used. Capitalisation matters. |
| `WeightPerFuelUnit` | `1080.0` | Shipment weight paid for by one unit of fuel. |
| `DestinationEffect` | `true` | Play the lightning at the receiving end too. |
| `VerboseLogging` | `false` | Local only; detailed decisions in `LogOutput.log`. |

## Multiplayer

Required on **both the server and every client** (`CompatibilityLevel.EveryoneMustHaveMod`).
The client initiates the request, so a vanilla client on a modded server would silently fall
through to plain obliteration and destroy its cargo — the mod refuses that situation
outright rather than risking it.

The transfer itself is server-authoritative: the client only asks, and the server resolves
the pair, validates the fuel and capacity, and writes both containers. Nothing about the
cargo is taken on the client's word.

Works on a dedicated server and on a client-hosted world (where the host short-circuits the
network round trip entirely).

## Lightning damage

Vanilla's Obliterator lightning damages nearby build pieces — including the very sign that
tags it, which would silently unlink the pair and let the next pull destroy the cargo for
real. When the Obliterator is **shipping**, the lightning is spawned with all damage
disabled. When it is **obliterating** as vanilla, the damaging lightning is left alone.

## Known limitation

If a player happens to have the **destination** Obliterator's container open *and moves an
item in it* during the moment a shipment lands, their client can save its stale view over
the delivery and the shipped items are lost. Merely having it open is fine — vanilla
reloads the container when it's closed.

The sending end is protected: the container is locked while a transfer is in flight, and
the server aborts if its view of the contents disagrees with the client's.

