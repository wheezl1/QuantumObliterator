# Changelog

All notable changes to this project are documented here.
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 0.3.0

### Fixed
- **Transfer lightning only fired once per session.** Every shipment after the first produced a
  clipped thunder sound and no visible bolt. The effect was being staged under a deactivated
  GameObject so its `Aoe` components could be disarmed before `Awake` baked the collision mask —
  but reparenting out of an inactive parent activates the object immediately, so it awoke at the
  world origin instead of at the Obliterator, registered its ZDO in the wrong sector, and was
  culled a frame later. The clone is now created with vanilla's own `Object.Instantiate` call and
  disarmed through a Harmony prefix on `Aoe.Awake`.

### Changed
- **Plugin GUID is now `wheezl.quantumobliterator`.** The config file is therefore
  `BepInEx/config/wheezl.quantumobliterator.cfg`; any older `dev.moxie.quantumobliterator.cfg`
  left in a profile is orphaned and can be deleted. Because `NetworkCompatibility` is set to
  `VersionStrictness.Minor`, every client and the server must be on this version.
- The version number now lives in exactly one place, `<PluginVersion>` in
  `QuantumObliterator.csproj`, and is surfaced to `[BepInPlugin]` through a generated constant.
- Lightning spawns now log their position and neutered-component count when `VerboseLogging` is
  enabled.

## 0.2.0

### Added
- **Fuel cost scales with shipment weight** rather than stack count or distance. One fuel item
  pays for `WeightPerFuelUnit` of cargo, and the fuel's own weight is included in the divisor so
  a burned stone never eats into the allowance it paid for.
- **Lever hover text** shows the tag, the current contents' weight, and what the pull will cost —
  in red when the Obliterator cannot currently pay for it.
- A pull whose entire payload is the fuel is refused rather than burning a stone to deliver
  nothing.

### Changed
- Transfer lightning no longer damages anything. Vanilla obliteration keeps its damaging
  lightning; only shipping is harmless, so a blast can't destroy the sign that tags the pair.

## 0.1.0

Initial release.

- Two Obliterators whose nearby signs carry the same text form a linked pair; pulling the lever
  on one ships its contents to the other, anywhere in the world, for the price of one fuel item.
- An Obliterator with no sign behaves exactly like vanilla.
- The destination does not need to be loaded and nobody has to be standing near it.
- Server-authoritative: the client only asks, and the server resolves the pair, validates fuel
  and capacity, and writes both containers.
- A tagged Obliterator never destroys items — no partner is an error, not a pile of coal — and a
  tag matching three or more Obliterators is refused without consuming anything.
