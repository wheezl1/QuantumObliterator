# QuantumObliterator

## Description

Put a sign next to an Obliterator and the text on that sign becomes its tag. Two Obliterators
tagged with the same text are linked. Pull the lever on one and everything inside it is moved to
the other, anywhere on the map, for the cost of one or more fuel items (Thunderstones by
default).

An Obliterator with no sign next to it works exactly like it does in vanilla.

The point is long-distance hauling in a world where portals can't carry metal, once your bases
are spread across the map.

## Features

- Ship items between any two Obliterators whose signs match, at any distance.
- The receiving end doesn't need to be loaded, and nobody has to be standing near it.
- Fuel cost scales with the weight of the shipment, not distance or stack count.
- The lever's hover text shows the tag, what the Obliterator currently holds, and what the pull
  will cost. It turns red when there isn't enough fuel.
- A tagged Obliterator never destroys items. If the tag has no partner, if three or more
  Obliterators share a tag, if there isn't enough fuel, or if the far end is full, you get a
  message and nothing is consumed.
- All or nothing: if the whole shipment doesn't fit, none of it moves and no fuel is spent.
- The lightning on a shipment does no damage, so it can't destroy the sign that tags the pair.
  Normal obliteration keeps vanilla's damaging lightning.
- Transfers are handled by the server, not the client.

## Compatibility

Built and tested against Valheim 1.0.15 (build 25390630), BepInExPack Valheim 5.4.2350 and
Jotunn 2.30.2.

Required on the server and on every client. A vanilla client connecting to a modded server would
fall through to ordinary obliteration and destroy its cargo, so the mod refuses that setup rather
than risk it. Works on dedicated servers and on client-hosted worlds.

Known issue: if someone has the receiving Obliterator's container open and moves an item around
inside it at the exact moment a shipment lands, that delivery can be lost. Just having it open is
fine.

## Configuration

`BepInEx/config/wheezl.quantumobliterator.cfg`. Every gameplay setting is admin-only and synced
from the server, so clients get the server's values and can't override them.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. `false` restores vanilla behaviour. |
| `SignSearchRadius` | `5.0` | How far from an Obliterator to look for a sign, in metres. |
| `CaseSensitiveTags` | `false` | Whether `Ashlands` and `ashlands` are different tags. |
| `RequireFuel` | `true` | `false` makes transfers free and unlimited. |
| `FuelItems` | `Thunderstone` | Comma-separated item prefab names, tried in order. Capitalisation matters. |
| `WeightPerFuelUnit` | `1080.0` | How much shipment weight one fuel item pays for. |
| `DestinationEffect` | `true` | Play the lightning at the receiving end as well. |
| `VerboseLogging` | `false` | Local only. Writes transfer details to `LogOutput.log`. |

One Thunderstone covers 1080 weight by default, which is roughly three full stacks of metal bars.
The stone you burn is paid for on top of that, so it doesn't eat into the allowance.

## Installation

Use a mod manager and install QuantumObliterator. It depends on BepInExPack Valheim and Jotunn,
which the manager installs for you.

To install by hand, put `QuantumObliterator.dll` in `BepInEx/plugins/`. Jotunn needs to be
installed already.

## Author

Made by wheezl. Source: https://github.com/wheezl1/QuantumObliterator

## AI Use

This is my first Valheim mod. I'm not an experienced C# developer, and I used AI heavily to write
and debug it. The mod is tested and works, but the code was written with a lot of AI assistance
and you should judge it on that basis.
