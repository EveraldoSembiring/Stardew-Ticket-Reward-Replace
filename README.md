# Custom Prize Ticket

A [SMAPI](https://smapi.io) mod for **Stardew Valley 1.6** that lets you fully customize the prizes available at Lewis' Prize Ticket Machine using an in-game config menu.

## Features

- **Enable/disable toggle** — turn reward replacement on or off without removing the mod
- **In-game item search** — type to search all items by name or ID; the list appears only while filtering
- **Add & remove rewards** — add any item to the prize pool and remove ones you don't want
- **3-item minimum** — Remove buttons are hidden when only 3 rewards remain
- **Persistent config** — your reward list is saved to `config.json` automatically
- **Harmony-patched** — replaces the hardcoded prize list in `PrizeTicketMenu` at runtime

## Requirements

| Dependency | Version |
|---|---|
| Stardew Valley | 1.6+ |
| SMAPI | 4.0.0+ |
| [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) | any |

## Installation

1. Install [SMAPI](https://smapi.io)
2. Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
3. Download this mod and extract it into your `Stardew Valley/Mods/` folder
4. Launch the game through SMAPI (`StardewModdingAPI.exe`)

## Usage

Open the in-game menu → **Options** → **Mod Config** → **Custom Prize Ticket**.

```
┌─────────────────────────────────────────────────┐
│            Custom Prize Ticket                  │
├─────────────────────────────────────────────────┤
│  [x] Enable Reward Replacement                  │
├─────────────────────────────────────────────────┤
│  Add Reward                                     │
│  [Filter: Diamond____________]                  │
│    Diamond                                      │
│    Diamond Wand                                 │
│  [+ Add to List]   Added: Diamond               │
├─────────────────────────────────────────────────┤
│  Current Rewards                                │
│  Diamond                           [Remove]     │
│  Iridium Bar                       [Remove]     │
│  Prismatic Shard                               │
└─────────────────────────────────────────────────┘
```

1. Check **Enable Reward Replacement** to activate the mod
2. Under **Add Reward**, type in the search box to filter items
3. Click an item from the list to select it
4. Click **+ Add to List** to add it to your reward pool
5. Under **Current Rewards**, click **Remove** to delete an entry (minimum 3 required)
6. Click **Save** — changes take effect the next time you open the prize machine

### Finding Item IDs

Item IDs use a qualified format: `(Type)ID`

| Prefix | Item Type | Example |
|---|---|---|
| `(O)` | Object / crop / forage | `(O)72` — Diamond |
| `(W)` | Weapon | `(W)4` — Pirate's Sword |
| `(H)` | Hat | `(H)28` — Propeller Hat |
| `(F)` | Furniture | `(F)1226` — Bookcase |
| `(BC)` | Big Craftable | `(BC)FishSmoker` — Fish Smoker |
| `(B)` | Boots | `(B)505` — Space Boots |

A full list is available on the [Stardew Valley Wiki — Item IDs](https://stardewvalleywiki.com/Modding:Item_IDs).

## Configuration

`config.json` is created in the mod folder on first launch. It is recommended to edit it through the in-game GMCM menu rather than manually.

```json
{
  "Enabled": true,
  "Rewards": [
    { "ItemId": "(O)631" },
    { "ItemId": "(O)630" },
    { "ItemId": "(H)SportsCap" }
  ]
}
```

| Field | Description |
|---|---|
| `Enabled` | When `false`, the vanilla prize list is used with no changes. |
| `Rewards` | List of reward entries. Each entry requires an `ItemId`. Minimum 3 entries required. |

## Building from Source

Requires the [.NET 6 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
```

Output is placed in `./Build/`. The mod is also automatically deployed to your `Stardew Valley/Mods/` folder by [ModBuildConfig](https://github.com/Pathoschild/SMAPI/tree/develop/src/SMAPI.ModBuildConfig).

## License

[MIT](https://opensource.org/licenses/MIT)
