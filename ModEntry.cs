using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace LuckyTicketRewardReplacer
{
    internal sealed class ModEntry : Mod
    {
        // Static reference so the Harmony postfix (which must be static) can access config.
        private static ModEntry _instance = null!;

        private ModConfig _config = null!;

        // GMCM add-form state
        private string _newItemId = "(O)72";
        private string _statusMsg = string.Empty;
        private bool _wasMouseDownAdd;
        private bool _wasMouseDownList;

        public override void Entry(IModHelper helper)
        {
            _instance = this;
            _config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        // ── Game launched: Harmony + GMCM ────────────────────────────────────
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ApplyHarmonyPatch();
            RegisterGmcm();
        }

        private void ApplyHarmonyPatch()
        {
            try
            {
                var ctor = AccessTools.GetDeclaredConstructors(typeof(PrizeTicketMenu)).First();
                new Harmony(ModManifest.UniqueID).Patch(
                    original: ctor,
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(PrizeTicketMenuCtorPostfix))
                );
                Monitor.Log("PrizeTicketMenu patched successfully.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to patch PrizeTicketMenu: {ex.Message}", LogLevel.Error);
            }
        }

        // ── Harmony postfix ───────────────────────────────────────────────────
        // Called after PrizeTicketMenu's constructor finishes.
        // Finds the internal List<Item> field and replaces it with our config.
        private static void PrizeTicketMenuCtorPostfix(PrizeTicketMenu __instance)
        {
            var newItems = _instance._config.Rewards
                .Select(r => ItemRegistry.Create(r.ItemId, allowNull: true))
                .OfType<Item>()
                .ToList();

            var fields = AccessTools.GetDeclaredFields(typeof(PrizeTicketMenu));

            // Log all fields at debug level to help diagnose if something goes wrong.
            _instance.Monitor.Log(
                "PrizeTicketMenu fields: " +
                string.Join(", ", fields.Select(f => $"{f.Name}:{f.FieldType.Name}")),
                LogLevel.Debug);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(List<Item>))
                {
                    field.SetValue(__instance, newItems);
                    _instance.Monitor.Log($"Prize items replaced via '{field.Name}'.", LogLevel.Info);
                    return;
                }
            }

            _instance.Monitor.Log(
                "Could not find a List<Item> field in PrizeTicketMenu — " +
                "the mod may need updating for this game version.",
                LogLevel.Warn);
        }

        // ── GMCM registration ─────────────────────────────────────────────────
        private void RegisterGmcm()
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
            {
                Monitor.Log("GMCM not found — configuration UI unavailable.", LogLevel.Error);
                return;
            }

            gmcm.Register(
                mod: ModManifest,
                reset: () => _config = new ModConfig(),
                save: () => Helper.WriteConfig(_config)
            );

            // ── Add Reward ────────────────────────────────────────────────────
            gmcm.AddSectionTitle(ModManifest, () => "Add Reward");

            gmcm.AddTextOption(
                mod: ModManifest,
                name: () => "Item ID",
                tooltip: () => "Qualified item ID to add (e.g. (O)72 for Diamond, (H)28 for Propeller Hat).",
                getValue: () => _newItemId,
                setValue: v => { _newItemId = v; _statusMsg = string.Empty; }
            );

            gmcm.AddComplexOption(
                mod: ModManifest,
                name: () => string.Empty,
                height: () => 44,
                draw: DrawAddButton,
                beforeMenuOpened: () => { _statusMsg = string.Empty; _wasMouseDownAdd = false; }
            );

            // ── Current Rewards ───────────────────────────────────────────────
            gmcm.AddSectionTitle(ModManifest, () => "Current Rewards");

            gmcm.AddComplexOption(
                mod: ModManifest,
                name: () => string.Empty,
                height: () => Math.Max(1, _config.Rewards.Count) * 40,
                draw: DrawRewardList,
                beforeMenuOpened: () => { _wasMouseDownList = false; }
            );
        }

        // ── GMCM draw callbacks ───────────────────────────────────────────────
        private void DrawAddButton(SpriteBatch b, Vector2 pos)
        {
            Point mouse = new(Game1.getMouseX(true), Game1.getMouseY(true));
            bool isDown = Game1.input.GetMouseState().LeftButton == ButtonState.Pressed;
            bool justClicked = !isDown && _wasMouseDownAdd;
            _wasMouseDownAdd = isDown;

            var btnRect = new Rectangle((int)pos.X, (int)pos.Y + 4, 140, 36);
            b.Draw(Game1.staminaRect, btnRect,
                Color.LightGreen * (btnRect.Contains(mouse) ? 1f : 0.7f));
            b.DrawString(Game1.smallFont, "+ Add to List",
                new Vector2(btnRect.X + 10, btnRect.Y + 9), Game1.textColor);

            if (justClicked && btnRect.Contains(mouse))
                TryAddItem();

            if (!string.IsNullOrEmpty(_statusMsg))
            {
                var color = _statusMsg.StartsWith("Added") ? new Color(0, 128, 0) : Color.Firebrick;
                b.DrawString(Game1.smallFont, _statusMsg,
                    new Vector2(pos.X + 150, pos.Y + 12), color);
            }
        }

        private void DrawRewardList(SpriteBatch b, Vector2 pos)
        {
            Point mouse = new(Game1.getMouseX(true), Game1.getMouseY(true));
            bool isDown = Game1.input.GetMouseState().LeftButton == ButtonState.Pressed;
            bool justClicked = !isDown && _wasMouseDownList;
            _wasMouseDownList = isDown;

            if (_config.Rewards.Count == 0)
            {
                b.DrawString(Game1.smallFont, "(no rewards configured)",
                    new Vector2(pos.X, pos.Y + 8), Color.Gray);
                return;
            }

            for (int i = 0; i < _config.Rewards.Count; i++)
            {
                var reward = _config.Rewards[i];
                int rowY = (int)pos.Y + i * 40;

                var itemData = ItemRegistry.GetData(reward.ItemId);
                if (itemData is not null)
                {
                    b.Draw(itemData.GetTexture(), new Vector2(pos.X, rowY + 4),
                        itemData.GetSourceRect(), Color.White, 0f, Vector2.Zero, 2f,
                        SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, itemData.DisplayName,
                        new Vector2(pos.X + 40, rowY + 10), Game1.textColor);
                }
                else
                {
                    b.DrawString(Game1.smallFont, $"[Unknown] {reward.ItemId}",
                        new Vector2(pos.X, rowY + 10), Color.Firebrick);
                }

                var removeRect = new Rectangle((int)pos.X + 500, rowY + 4, 90, 30);
                b.Draw(Game1.staminaRect, removeRect,
                    Color.Salmon * (removeRect.Contains(mouse) ? 1f : 0.7f));
                b.DrawString(Game1.smallFont, "Remove",
                    new Vector2(removeRect.X + 8, removeRect.Y + 7), Color.White);

                if (justClicked && removeRect.Contains(mouse))
                {
                    _config.Rewards.RemoveAt(i);
                    _wasMouseDownList = false;
                    return;
                }
            }
        }

        private void TryAddItem()
        {
            string id = _newItemId.Trim();
            var data = ItemRegistry.GetData(id);
            if (data is null)
            {
                _statusMsg = $"Unknown item: {id}";
                return;
            }
            _config.Rewards.Add(new RewardEntry { ItemId = id });
            _statusMsg = $"Added: {data.DisplayName}";
        }
    }
}
