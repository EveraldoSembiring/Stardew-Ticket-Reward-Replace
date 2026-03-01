# Lucky Ticket Reward Replacer

A [SMAPI](https://smapi.io) mod for **Stardew Valley 1.6** that lets you fully customize the prizes available at Lewis' Prize Ticket Machine — with an in-game editor menu, no file editing required.

## Features

- **In-game editor** — open a menu at any time to add or remove rewards on the fly
- **Per-item ticket cost** — set how many prize tickets each reward requires
- **Instant apply** — changes take effect immediately without restarting the game
- **Persistent config** — your reward list is saved to `config.json` automatically

## Requirements

| Dependency | Version |
|---|---|
| Stardew Valley | 1.6+ |
| SMAPI | 4.0.0+ |

## Installation

1. Install [SMAPI](https://smapi.io)
2. Download this mod and extract it into your `Stardew Valley/Mods/` folder
3. Launch the game through SMAPI (`StardewModdingAPI.exe`)

## Usage

Press **F8** anywhere in-game to open the reward editor.

```
┌─────────────────────────────────────────────────┐
│          Prize Ticket Reward Editor             │
├─────────────────────────────────────────────────┤
│  Item Name                  Tickets             │
│  ─────────────────────────────────────────────  │
│  Diamond                       1          [Del] │
│  Iridium Bar                   2          [Del] │
│  Prismatic Shard               5          [Del] │
├─────────────────────────────────────────────────┤
│  Item ID (e.g. (O)72):  [________]              │
│  Tickets: [__]               [Add Item]         │
│                          [Save & Close]         │
└─────────────────────────────────────────────────┘
```

### Editor Controls

| Action | Input |
|---|---|
| Open editor | F8 (configurable) |
| Switch between input fields | Tab |
| Add item (from cost field) | Enter |
| Remove a reward | Click **Del** |
| Scroll reward list | Mouse wheel or ^/v buttons |
| Save and close | **Save & Close** button or Escape |

### Finding Item IDs

Item IDs use a qualified format: `(Type)ID`

| Prefix | Item Type | Example |
|---|---|---|
| `(O)` | Object / crop / forage | `(O)72` — Diamond |
| `(W)` | Weapon | `(W)4` — Pirate's Sword |
| `(H)` | Hat | `(H)28` — Propeller Hat |
| `(F)` | Furniture | `(F)1226` — Bookcase |
| `(B)` | Boots | `(B)505` — Space Boots |

A full list is available on the [Stardew Valley Wiki — Item IDs](https://stardewvalleywiki.com/Modding:Item_IDs).

## Configuration

`config.json` is created in the mod folder on first launch.

```json
{
  "OpenEditorKey": "F8",
  "PrizeTicketItemId": "(O)897",
  "Rewards": [
    { "ItemId": "(O)72",  "TicketCost": 1 },
    { "ItemId": "(O)337", "TicketCost": 2 },
    { "ItemId": "(O)74",  "TicketCost": 5 }
  ]
}
```

| Field | Description |
|---|---|
| `OpenEditorKey` | Keybind to open the editor. Supports SMAPI [multi-key bindings](https://stardewvalleywiki.com/Modding:Player_Guide/Key_Bindings). |
| `PrizeTicketItemId` | Qualified item ID of the prize ticket used as currency. Change this if the default doesn't match your game version. |
| `Rewards` | List of reward entries. Each entry has an `ItemId` and a `TicketCost`. |

## Building from Source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
```

Output is placed in `./Build/`. The mod is also automatically deployed to your `Stardew Valley/Mods/` folder by [ModBuildConfig](https://github.com/Pathoschild/SMAPI/tree/develop/src/SMAPI.ModBuildConfig).

## License

[MIT](https://opensource.org/licenses/MIT)
