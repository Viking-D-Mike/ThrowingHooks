# VDM Throwing Hooks

Adds four throwing tools with a rope to Sailwind. Throw them at a crate, barrel or anything floating, let them bite and pull it to you. No more jumping into the sea for every floating crate.

## The tools

| Tool | Where to buy it | Region | How it flies |
|---|---|---|---|
| Viking grappling axe | Mount Malefic (tavern wall) | Aestrin | Spins and bites with the blade |
| Harpoon | Fort Aestrin | Aestrin | Flies point first. Sometimes catches a fish! |
| Mikhtaf (grapnel) | Gold Rock City (market stall) | Al'Ankh | Spins and hooks with a prong |
| Kaginawa (grappling hook) | Dragon Cliffs | Emerald Archipelago | Spins and hooks with a prong |

The price is shown in the local currency. A new one is put on sale a couple of minutes after you buy one.

## How to use them

1. Take the tool in your hand.
2. Look at something floating (up to about 35 m away) and press the activate key (**R** by default). The tool flies, bites and stays tied to it with the rope.
3. Walk back or just wait: the rope pulls the item towards you with normal physics. It bumps into your hull and climbs over the side.
4. When it is close, press **F** to grab it. The tool goes into a free inventory slot (or drops at your feet if your inventory is full) and the item ends up in your hands.

Press **R** again at any moment to let go of the rope. If you miss, the tool comes back to your hand.

**Fishing with the harpoon:** if you throw the harpoon into the water without hitting anything, there is a small chance (3%) that it comes back with a fish. The fish is the one that lives in that part of the sea, the same as with a fishing rod.

You cannot pull items that are for sale in a shop, items fixed in place, or items that are already inside a boat.

All the tools can be left on tables and boats and are saved with your game. If you have [HooksHangMore](https://thunderstore.io/c/sailwind/p/RadDude/HooksHangMore/) installed, they can be hung on hooks. HooksHangMore is optional.

## Installation

- **With a mod manager** (Gale, r2modman, Thunderstore Mod Manager): install VDM Throwing Hooks and you are done. BepInEx is installed automatically.
- **Manually**: install [BepInExPack](https://thunderstore.io/c/sailwind/p/BepInEx/BepInExPack/), then copy `GarfioMod.dll` and `garfio_bundle` together into a folder inside `BepInEx/plugins` (for example `BepInEx/plugins/ThrowingHooks/`). Both files must be in the same folder.

## Configuration

The settings file is `BepInEx/config/vikingdmike.grapplinghook.cfg` (it is created the first time you start the game with the mod). You can also change everything in game with the ConfigurationManager mod (F1).

- **Hand**, **Harpoon Hand**, **Mikhtaf Hand**, **Kaginawa Hand**: how each tool sits in your hand (distance, height, side and rotation).
- **Hook** sections: how each tool hangs on a hook (HooksHangMore).
- **Shop** sections: where each tool is sold and its price.
- **Harpoon Fishing**: the chance (%) of catching a fish with the harpoon.
- **Colors**: brightness of the tools and the option to remove the sky shine on them.

## Compatibility

- The tools use fixed slots in the game's prefab list (24601, 24602, 24603 and 24604). If another mod uses the same slot, that tool will not be registered and a message is written to the BepInEx log.
- Works together with my other mods.

## Uninstalling

**Important:** if you remove the mod, any save that contains one of these tools will get stuck on the loading screen.

To remove the mod safely, install **Save Cleaner** before loading those saves. It removes the missing items when the save loads. Always keep a backup copy of your saves before installing or removing mods.

## Credits

Made by Viking D. Mike. The harpoon, mikhtaf and kaginawa models were made by the author.

The grappling axe uses two models from [Printables](https://www.printables.com/), modified and combined by the author (fitted together, gaps filled, a metal wedge, ring and rope added):

- Axe head: ["Viking Axe Head"](https://www.printables.com/model/1644867-viking-axe-head) by **bush.tj86**, licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- Handle: [Printables model 589882](https://www.printables.com/model/589882), released into the public domain.
