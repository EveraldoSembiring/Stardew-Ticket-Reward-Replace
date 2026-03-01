using System.Collections.Generic;

namespace LuckyTicketRewardReplacer
{
    public class RewardEntry
    {
        public string ItemId { get; set; } = "(O)72";
    }

    public class ModConfig
    {
        /// <summary>Items available from Lewis' prize machine. Each costs 1 prize ticket.</summary>
        public List<RewardEntry> Rewards { get; set; } = new()
        {
            // Vanilla defaults (from PrizeTicketMenu source)
            new() { ItemId = "(O)631" },
            new() { ItemId = "(O)630" },
            new() { ItemId = "(F)BluePinstripeBed" },
            new() { ItemId = "(F)BluePinstripeDoubleBed" },
            new() { ItemId = "(O)633" },
            new() { ItemId = "(O)632" },
            new() { ItemId = "(O)Book_Friendship" },
            new() { ItemId = "(H)SportsCap" },
            new() { ItemId = "(BC)FishSmoker" },
            new() { ItemId = "(BC)Dehydrator" },
            new() { ItemId = "(F)FancyHousePlant1" },
            new() { ItemId = "(F)FancyHousePlant2" },
            new() { ItemId = "(F)FancyHousePlant3" },
            new() { ItemId = "(F)CowDecal" },
            new() { ItemId = "(O)226" },
            new() { ItemId = "(F)FancyTree1" },
            new() { ItemId = "(F)FancyTree2" },
            new() { ItemId = "(F)FancyTree3" },
            new() { ItemId = "(F)PigPainting" },
        };
    }
}
