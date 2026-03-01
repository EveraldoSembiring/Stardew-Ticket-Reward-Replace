using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace CustomPrizeTicket
{
    internal sealed class ModEntry : Mod
    {
        // Static reference so the Harmony patches (which must be static) can access instance state.
        private static ModEntry _instance = null!;
        private static Dictionary<FieldInfo, List<Item>>? _savedItemLists;

        private const int QueueLength = 100;
        private PrizeQueueData _prizeQueue = new();

        private ModConfig _config = null!;
        private string[] _allItemIds = Array.Empty<string>();

        // GMCM add-form state
        private string _newItemId = "(O)72";
        private string _statusMsg = string.Empty;
        private bool _wasMouseDownAdd;
        private bool _wasMouseDownList;
        private bool _wasMouseDownPicker;

        // Item picker filter state
        private TextBox? _filterBox;
        private string[] _filteredItemIds = Array.Empty<string>();
        private string _lastFilter = "";
        private int _itemListScroll;
        private int _prevScrollWheelValue;

        public override void Entry(IModHelper helper)
        {
            _instance = this;
            _config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        // ── Game launched: Harmony + GMCM ────────────────────────────────────
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ApplyHarmonyPatch();
            BuildItemList();
            RegisterGmcm();
        }

        private void BuildItemList()
        {
            _allItemIds = ItemRegistry.ItemTypes
                .SelectMany(t => t.GetAllIds().Select(id => t.Identifier + id))
                .OrderBy(id => ItemRegistry.GetData(id)?.DisplayName ?? id)
                .ToArray();

            Monitor.Log($"Loaded {_allItemIds.Length} items for reward picker.", LogLevel.Debug);
        }

        private void ApplyHarmonyPatch()
        {
            try
            {
                var harmony = new Harmony(ModManifest.UniqueID);

                var ctor = AccessTools.GetDeclaredConstructors(typeof(PrizeTicketMenu)).First();
                harmony.Patch(
                    original: ctor,
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(PrizeTicketMenuCtorPostfix))
                );

                var draw = AccessTools.Method(typeof(PrizeTicketMenu), "draw",
                    new[] { typeof(SpriteBatch) });
                if (draw != null)
                {
                    harmony.Patch(
                        original: draw,
                        prefix: new HarmonyMethod(typeof(ModEntry), nameof(PrizeTicketMenuDrawPrefix)),
                        postfix: new HarmonyMethod(typeof(ModEntry), nameof(PrizeTicketMenuDrawPostfix))
                    );
                }

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
            if (!_instance._config.Enabled)
                return;

            var fields = AccessTools.GetDeclaredFields(typeof(PrizeTicketMenu));

            // Log all fields at debug level to help diagnose if something goes wrong.
            _instance.Monitor.Log(
                "PrizeTicketMenu fields: " +
                string.Join(", ", fields.Select(f => $"{f.Name}:{f.FieldType.Name}")),
                LogLevel.Debug);

            // Refill queue to QueueLength using shuffle-bag so distribution is even.
            int needed = QueueLength - _instance._prizeQueue.ItemIds.Count;
            if (needed > 0)
                _instance._prizeQueue.ItemIds.AddRange(_instance.ShuffleBagFill(needed));
            _instance.Helper.Data.WriteSaveData("PrizeQueue", _instance._prizeQueue);

            bool replaced = false;
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(List<Item>))
                {
                    var newItems = _instance._prizeQueue.ItemIds
                        .Select(id => ItemRegistry.Create(id, allowNull: true))
                        .OfType<Item>()
                        .ToList();
                    field.SetValue(__instance, newItems);
                    _instance.Monitor.Log($"Prize items replaced via '{field.Name}'.", LogLevel.Info);
                    replaced = true;
                }
            }

            if (!replaced)
                _instance.Monitor.Log(
                    "Could not find a List<Item> field in PrizeTicketMenu — " +
                    "the mod may need updating for this game version.",
                    LogLevel.Warn);
        }

        // Trim list before draw so items beyond the visible slots are never rendered.
        // The 4th slot only appears after the movingRewardTrack animation finishes.
        private static void PrizeTicketMenuDrawPrefix(PrizeTicketMenu __instance)
        {
            _savedItemLists = null;

            int drawLimit = 3;
            var trackField = AccessTools.DeclaredField(typeof(PrizeTicketMenu), "movingRewardTrack");
            if (trackField != null)
            {
                var val = trackField.GetValue(__instance);
                bool trackDone = val switch
                {
                    bool b   => !b,
                    int i    => i <= 0,
                    float f  => f <= 0f,
                    _        => true
                };
                if (trackDone) drawLimit = 4;
            }

            foreach (var field in AccessTools.GetDeclaredFields(typeof(PrizeTicketMenu)))
            {
                if (field.FieldType != typeof(List<Item>)) continue;
                var list = (List<Item>?)field.GetValue(__instance);
                if (list == null || list.Count <= drawLimit) continue;
                _savedItemLists ??= new Dictionary<FieldInfo, List<Item>>();
                _savedItemLists[field] = list;
                field.SetValue(__instance, list.Take(drawLimit).ToList());
            }
        }

        // Restore the full list after draw so click/purchase logic still sees all items.
        private static void PrizeTicketMenuDrawPostfix(PrizeTicketMenu __instance)
        {
            if (_savedItemLists == null) return;
            foreach (var (field, list) in _savedItemLists)
                field.SetValue(__instance, list);
            _savedItemLists = null;
        }

        // ── GMCM registration ─────────────────────────────────────────────────
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => InitializePrizeQueue();

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.OldMenu is not PrizeTicketMenu oldMenu) return;

            // Save whatever items remain in the menu as the new queue state.
            foreach (var field in AccessTools.GetDeclaredFields(typeof(PrizeTicketMenu)))
            {
                if (field.FieldType == typeof(List<Item>) && field.GetValue(oldMenu) is List<Item> list)
                {
                    _prizeQueue.ItemIds = list.Select(i => i.QualifiedItemId).ToList();
                    Helper.Data.WriteSaveData("PrizeQueue", _prizeQueue);
                    Monitor.Log($"Prize queue saved: {_prizeQueue.ItemIds.Count} items remaining.", LogLevel.Debug);
                    return;
                }
            }
        }

        private void ClearPrizeQueue()
        {
            _prizeQueue = new PrizeQueueData();
            if (Context.IsWorldReady)
                Helper.Data.WriteSaveData("PrizeQueue", _prizeQueue);
        }

        private void InitializePrizeQueue()
        {
            _prizeQueue = Helper.Data.ReadSaveData<PrizeQueueData>("PrizeQueue") ?? new PrizeQueueData();

            int needed = QueueLength - _prizeQueue.ItemIds.Count;
            if (needed > 0)
                _prizeQueue.ItemIds.AddRange(ShuffleBagFill(needed));

            Helper.Data.WriteSaveData("PrizeQueue", _prizeQueue);
            Monitor.Log($"Prize queue: {string.Join(", ", _prizeQueue.ItemIds)}", LogLevel.Debug);
        }

        // Shuffle-bag fill: cycles through a shuffled copy of rewards so every item
        // appears roughly equally often and consecutive duplicates are unlikely.
        private IEnumerable<string> ShuffleBagFill(int count)
        {
            var rewards = _config.Rewards;
            if (rewards.Count == 0)
            {
                for (int i = 0; i < count; i++) yield return "(O)72";
                yield break;
            }

            var bag = new List<string>();
            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0)
                {
                    bag.AddRange(rewards.Select(r => r.ItemId));
                    for (int j = bag.Count - 1; j > 0; j--)
                    {
                        int k = Game1.random.Next(j + 1);
                        (bag[j], bag[k]) = (bag[k], bag[j]);
                    }
                }
                yield return bag[^1];
                bag.RemoveAt(bag.Count - 1);
            }
        }

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
                reset: () => { _config = new ModConfig(); ClearPrizeQueue(); },
                save: () => { Helper.WriteConfig(_config); ClearPrizeQueue(); }
            );

            gmcm.AddBoolOption(
                mod: ModManifest,
                name: () => "Enable Reward Replacement",
                tooltip: () => "When disabled, Lewis' prize machine uses the vanilla default rewards.",
                getValue: () => _config.Enabled,
                setValue: v => _config.Enabled = v
            );

            // ── Add Reward ────────────────────────────────────────────────────
            gmcm.AddSectionTitle(ModManifest, () => "Add Reward");

            gmcm.AddComplexOption(
                mod: ModManifest,
                name: () => string.Empty,
                height: () => string.IsNullOrWhiteSpace(_lastFilter) ? 52 : 52 + 8 * 40,
                draw: DrawItemPicker,
                beforeMenuOpened: () =>
                {
                    _filterBox = new TextBox(
                        Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                        null, Game1.smallFont, Game1.textColor);
                    _filterBox.Text = "";
                    _lastFilter = "";
                    _filteredItemIds = _allItemIds;
                    _itemListScroll = 0;
                    _prevScrollWheelValue = Game1.input.GetMouseState().ScrollWheelValue;
                    _wasMouseDownPicker = false;
                },
                beforeMenuClosed: () =>
                {
                    if (_filterBox != null && Game1.keyboardDispatcher.Subscriber == _filterBox)
                        Game1.keyboardDispatcher.Subscriber = null;
                    _filterBox = null;
                }
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

            const string addLabel = "+ Add to List";
            var addTextSize = Game1.smallFont.MeasureString(addLabel);
            var btnRect = new Rectangle((int)pos.X, (int)pos.Y + 4, (int)addTextSize.X + 24, 36);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height,
                btnRect.Contains(mouse) ? Color.Wheat : Color.White, 4f, false);
            Utility.drawTextWithShadow(b, addLabel, Game1.smallFont,
                new Vector2(btnRect.X + 12, btnRect.Y + (btnRect.Height - (int)addTextSize.Y) / 2), Game1.textColor);

            if (justClicked && btnRect.Contains(mouse))
                TryAddItem();

            if (!string.IsNullOrEmpty(_statusMsg))
            {
                var color = _statusMsg.StartsWith("Added") ? new Color(0, 128, 0) : Color.Firebrick;
                b.DrawString(Game1.smallFont, _statusMsg,
                    new Vector2(btnRect.Right + 12, btnRect.Y + (btnRect.Height - (int)addTextSize.Y) / 2), color);
            }
        }

        private void DrawItemPicker(SpriteBatch b, Vector2 pos)
        {
            if (_filterBox == null) return;

            Point mouse = new(Game1.getMouseX(true), Game1.getMouseY(true));
            bool isDown = Game1.input.GetMouseState().LeftButton == ButtonState.Pressed;
            bool justClicked = !isDown && _wasMouseDownPicker;

            // ── Filter box ────────────────────────────────────────────────────────
            _filterBox.X = (int)pos.X;
            _filterBox.Y = (int)pos.Y;
            _filterBox.Width = 400;
            _filterBox.Draw(b, false);

            if (justClicked && new Rectangle(_filterBox.X, _filterBox.Y, _filterBox.Width, 48).Contains(mouse))
                _filterBox.SelectMe();

            // ── Recompute filtered list when text changes ──────────────────────────
            string currentText = _filterBox.Text ?? "";
            if (currentText != _lastFilter)
            {
                _lastFilter = currentText;
                _itemListScroll = 0;
                _filteredItemIds = string.IsNullOrWhiteSpace(currentText)
                    ? _allItemIds
                    : _allItemIds.Where(id =>
                    {
                        var data = ItemRegistry.GetData(id);
                        return (data?.DisplayName?.Contains(currentText, StringComparison.OrdinalIgnoreCase) == true)
                            || id.Contains(currentText, StringComparison.OrdinalIgnoreCase);
                    }).ToArray();
            }

            // ── Scroll wheel ──────────────────────────────────────────────────────
            const int visibleRows = 8;
            int listTopY = (int)pos.Y + 52;
            var listRegion = new Rectangle((int)pos.X, listTopY, 560, visibleRows * 40);
            int currentWheel = Game1.input.GetMouseState().ScrollWheelValue;
            if (listRegion.Contains(mouse) && currentWheel != _prevScrollWheelValue)
            {
                int delta = (_prevScrollWheelValue - currentWheel) / 120;
                _itemListScroll = Math.Clamp(_itemListScroll + delta, 0,
                    Math.Max(0, _filteredItemIds.Length - visibleRows));
            }
            _prevScrollWheelValue = currentWheel;

            // ── Item rows (only when filter is active) ────────────────────────────
            if (!string.IsNullOrWhiteSpace(currentText))
            {
                for (int i = 0; i < visibleRows; i++)
                {
                    int idx = i + _itemListScroll;
                    if (idx >= _filteredItemIds.Length) break;

                    string id = _filteredItemIds[idx];
                    var itemData = ItemRegistry.GetData(id);
                    int rowY = listTopY + i * 40;
                    var rowRect = new Rectangle((int)pos.X, rowY, 500, 36);

                    if (id == _newItemId)
                        b.Draw(Game1.staminaRect, rowRect, new Color(100, 200, 100, 150));
                    else if (rowRect.Contains(mouse))
                        b.Draw(Game1.staminaRect, rowRect, new Color(200, 200, 200, 80));

                    b.DrawString(Game1.smallFont, itemData?.DisplayName ?? id,
                        new Vector2(rowRect.X + 6, rowY + 10), Game1.textColor);

                    if (justClicked && rowRect.Contains(mouse))
                    {
                        _newItemId = id;
                        _statusMsg = string.Empty;
                    }
                }

                // ── Scrollbar ─────────────────────────────────────────────────────
                if (_filteredItemIds.Length > visibleRows)
                {
                    int sbX = (int)pos.X + 506;
                    int sbH = visibleRows * 40;
                    b.Draw(Game1.staminaRect, new Rectangle(sbX, listTopY, 8, sbH), Color.Gray * 0.4f);
                    int thumbH = Math.Max(16, sbH * visibleRows / _filteredItemIds.Length);
                    int thumbY = listTopY + (sbH - thumbH) * _itemListScroll /
                                 Math.Max(1, _filteredItemIds.Length - visibleRows);
                    b.Draw(Game1.staminaRect, new Rectangle(sbX, thumbY, 8, thumbH), Color.Gray);
                }
            }

            _wasMouseDownPicker = isDown;
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
                string displayName = itemData?.DisplayName ?? $"[Unknown] {reward.ItemId}";
                Color textColor = itemData is not null ? Game1.textColor : Color.Firebrick;
                b.DrawString(Game1.smallFont, displayName, new Vector2(pos.X + 6, rowY + 10), textColor);

                if (_config.Rewards.Count > 3)
                {
                    const string removeLabel = "Remove";
                    var removeTextSize = Game1.smallFont.MeasureString(removeLabel);
                    var removeRect = new Rectangle((int)pos.X + 500, rowY + 4, (int)removeTextSize.X + 24, 30);
                    IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                        new Rectangle(432, 439, 9, 9),
                        removeRect.X, removeRect.Y, removeRect.Width, removeRect.Height,
                        removeRect.Contains(mouse) ? Color.OrangeRed : Color.Salmon, 4f, false);
                    Utility.drawTextWithShadow(b, removeLabel, Game1.smallFont,
                        new Vector2(removeRect.X + 12, removeRect.Y + (removeRect.Height - (int)removeTextSize.Y) / 2), Game1.textColor);

                    if (justClicked && removeRect.Contains(mouse))
                    {
                        _config.Rewards.RemoveAt(i);
                        _wasMouseDownList = false;
                        return;
                    }
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
