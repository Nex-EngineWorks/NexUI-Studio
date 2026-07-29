using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using G = emiteat.NexUI.Designer.Editor.Components.DesignerPaletteGroup;
using static emiteat.NexUI.Designer.Editor.Components.NexUIComponentArchetypes;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Game UI components. Where <see cref="NexUILibraryCatalog"/> covers the app-shaped library, this
    /// catalog covers what games actually ship: combat HUD readouts, world/navigation markers,
    /// inventory and crafting, progression and rewards, front-end menus and result screens, and
    /// multiplayer surfaces.
    ///
    /// The point is that a HUD or a results screen should be assembled from the palette, not drawn
    /// from empty boxes every project. Each entry is a first-class component (its own defaults, slots,
    /// states, bindings and backend mapping) - the numbers, art and rules behind it stay the runtime's
    /// job, which is why they declare Partial backend support rather than pretending to be complete.
    /// </summary>
    public static class NexUIGameCatalog
    {
        public static IEnumerable<DesignerComponentDescriptor> Build()
        {
            // ---- Combat HUD ---------------------------------------------------------------
            yield return Meter("HealthBar", "Health Bar", 260, 24, "Player health with damage-delay trail and low-health state.", group: G.Game);
            yield return Meter("ShieldBar", "Shield Bar", 260, 12, "Absorb/overshield layered above the health bar.", group: G.Game);
            yield return Meter("ArmorBar", "Armor Bar", 260, 12, "Armor value shown as its own track.", group: G.Game);
            yield return Meter("ManaBar", "Mana Bar", 260, 18, "Primary caster resource.", group: G.Game);
            yield return Meter("EnergyBar", "Energy Bar", 260, 18, "Regenerating action resource.", group: G.Game);
            yield return Meter("StaminaWheel", "Stamina Wheel", 96, 96, "Radial stamina used by sprint/dodge.", shape: DesignerElementShape.Circle, group: G.Game);
            yield return Meter("BreathMeter", "Breath Meter", 200, 16, "Underwater oxygen countdown.", group: G.Game);
            yield return Meter("HungerMeter", "Hunger Meter", 200, 16, "Survival hunger/thirst need.", group: G.Game);
            yield return Meter("SanityMeter", "Sanity Meter", 200, 16, "Horror/sanity resource with warning states.", group: G.Game);
            yield return Meter("HeatMeter", "Heat Meter", 200, 16, "Weapon or vehicle heat with an overheat threshold.", group: G.Game);
            yield return Meter("UltimateCharge", "Ultimate Charge", 96, 96, "Ultimate ability charge ring with a ready flash.", shape: DesignerElementShape.Circle, group: G.Game);
            yield return Meter("ChargeMeter", "Charge Meter", 160, 16, "Hold-to-charge attack strength.", group: G.Game);
            yield return Meter("ReloadIndicator", "Reload Indicator", 120, 120, "Reload progress ring, optionally with an active-reload window.", shape: DesignerElementShape.Circle, group: G.Game);
            yield return Meter("ThreatMeter", "Threat Meter", 200, 16, "Aggro/threat level against the current target.", group: G.Game);
            yield return Meter("DetectionMeter", "Detection Meter", 80, 80, "Stealth awareness eye filling as the player is spotted.", shape: DesignerElementShape.Circle, group: G.Game);
            yield return Meter("NoiseMeter", "Noise Meter", 160, 16, "How loud the player currently is.", group: G.Game);
            yield return Status("HealthPips", "Health Pips", 160, 24, "Discrete health segments (hearts/pips) instead of a bar.", value: true, group: G.Game);
            yield return Status("AmmoPips", "Ammo Pips", 140, 20, "Remaining rounds drawn as individual pips.", value: true, group: G.Game);
            yield return Status("HitMarker", "Hit Marker", 48, 48, "Crosshair confirmation flash for hit/kill/headshot.", group: G.Game);
            yield return Status("DamageIndicator", "Damage Direction Indicator", 200, 200, "Arc showing the direction incoming damage came from.", group: G.Game);
            yield return Status("StatusEffectIcon", "Status Effect Icon", 40, 40, "One buff/debuff icon with duration and stack count.", value: true, children: true, group: G.Game);
            yield return Collection("AbilityQueue", "Ability Queue", G.Game, 200, 48, "Queued/next ability indicators.", "ability", "Ability Template");
            yield return Collection("StanceSelector", "Stance Selector", G.Game, 220, 48, "Current combat stance or fire mode, cycled by input.", "stance", "Stance Template");
            yield return Collection("GrenadeSelector", "Grenade Selector", G.Game, 180, 56, "Throwable type selector with counts.", "grenade", "Grenade Template");
            yield return Status("ComboRank", "Combo Rank", 80, 80, "Letter grade (S/A/B) for the current combo.", text: "S", group: G.Game);
            yield return Status("AccuracyMeter", "Accuracy Meter", 180, 40, "Rhythm/shooter accuracy readout for the last input.", value: true, group: G.Game);
            yield return Status("JudgementText", "Judgement Text", 200, 48, "Perfect/Great/Miss call-out.", text: "PERFECT", group: G.Game);
            yield return Container("WeaponSlot", "Weapon Slot", G.Game, 120, 56, "Equipped weapon with ammo, icon and slot key.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Ammo", 0, 1), Slot("key", "Key", 0, 1) }, interactive: true, selectable: true);
            yield return Container("VehicleHud", "Vehicle HUD", G.Game, 320, 160, "Vehicle speed, fuel, damage and seat state.", slots: new[] { Slot("content", "Gauges"), Slot("status", "Status", 0, 1) }, value: true);
            yield return Status("GearIndicator", "Gear Indicator", 64, 64, "Current transmission gear.", text: "3", group: G.Game);
            yield return Meter("NitroBar", "Nitro Bar", 200, 16, "Boost/nitro reserve.", group: G.Game);
            yield return Meter("Tachometer", "Tachometer", 160, 160, "Engine RPM dial with a redline zone.", shape: DesignerElementShape.Circle, group: G.Game);

            // ---- World & navigation --------------------------------------------------------
            yield return Container("MapScreen", "Map Screen", G.GameWorld, 720, 480, "Full-screen map with pan/zoom, layers and legend.", slots: new[] { Slot("content", "Map"), Slot("legend", "Legend", 0, 1), Slot("controls", "Controls", 0, 1) }, interactive: true);
            yield return Collection("MapMarkerList", "Map Marker List", G.GameWorld, 260, 320, "Filterable list of map markers and fast-travel points.", "marker", "Marker Template");
            yield return Status("OffscreenIndicator", "Off-screen Indicator", 48, 48, "Arrow pinned to the screen edge pointing at an off-screen target.", group: G.GameWorld);
            yield return Status("LockOnIndicator", "Lock-on Indicator", 64, 64, "Reticle that snaps to the locked target.", group: G.GameWorld);
            yield return Status("DistanceMeter", "Distance Meter", 100, 24, "Distance to the tracked objective.", text: "120m", value: true, group: G.GameWorld);
            yield return Status("ZoneBanner", "Zone Banner", 480, 80, "Area name banner shown on entering a region.", text: "New Area", group: G.GameWorld);
            yield return Status("DiscoveryToast", "Discovery Toast", 360, 64, "Location discovered notification.", text: "Location discovered", children: true, overlay: true, group: G.GameWorld);
            yield return Meter("TravelProgress", "Travel Progress", 320, 16, "Fast-travel or journey progress.", group: G.GameWorld);
            yield return Container("Radar", "Radar", G.GameWorld, 200, 200, "Sweeping radar with contact blips and range rings.", slots: new[] { Slot("blip", "Blip Template", 0, 1) }, shape: DesignerElementShape.Circle);
            yield return Collection("ObjectiveList", "Objective List", G.GameWorld, 300, 200, "Current objectives with completion state.", "objective", "Objective Template");
            yield return Status("WorldTooltip", "World Tooltip", 220, 64, "Tooltip anchored to a world object under the cursor.", text: "Object", overlay: true, group: G.GameWorld);
            yield return Container("PlacementGhost", "Placement Ghost", G.GameWorld, 160, 160, "Build/placement preview with valid and blocked states.", slots: new[] { Slot("content", "Footprint") }, states: SeverityStates);
            yield return Status("CompassMarker", "Compass Marker", 32, 32, "Single marker pip drawn on the compass strip.", group: G.GameWorld);
            yield return Status("DepthMeter", "Depth Meter", 80, 200, "Altitude/depth ladder for flight or diving.", value: true, group: G.GameWorld);
            yield return Status("TimeOfDay", "Time of Day", 140, 40, "In-game clock and day counter.", text: "Day 3 · 18:20", group: G.GameWorld);
            yield return Status("WeatherIndicator", "Weather Indicator", 120, 40, "Current weather and temperature.", text: "Rain", group: G.GameWorld);
            yield return Collection("Letterbox", "Cutscene Letterbox", G.GameWorld, 720, 480, "Cinematic bars with subtitle and skip prompt slots.", "bar", "Bar Template", overlay: true);
            yield return Status("SkipPrompt", "Skip Prompt", 200, 40, "Hold-to-skip hint during a cutscene.", text: "Hold to skip", value: true, group: G.GameWorld);

            // ---- Items & inventory ----------------------------------------------------------
            yield return Container("ItemCard", "Item Card", G.GameItems, 240, 320, "Item art, rarity frame, stats and actions.", slots: new[] { Slot("image", "Icon", 0, 1), Slot("content", "Stats"), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Container("ItemTooltip", "Item Tooltip", G.GameItems, 280, 220, "Hover detail: name, rarity, stats, flavour text.", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Stats"), Slot("footer", "Flavour", 0, 1) }, overlay: true);
            yield return Container("ItemComparison", "Item Comparison", G.GameItems, 480, 260, "Side-by-side equipped vs candidate item stats.", slots: new[] { Slot("left", "Equipped", 0, 1), Slot("right", "Candidate", 0, 1) });
            yield return Status("RarityFrame", "Rarity Frame", 72, 72, "Rarity-colored frame drawn around an item icon.", group: G.GameItems);
            yield return Meter("DurabilityBar", "Durability Bar", 72, 6, "Item durability remaining.", group: G.GameItems);
            yield return Status("StackCount", "Stack Count", 32, 20, "Quantity badge on an item cell.", text: "12", value: true, group: G.GameItems);
            yield return Status("ItemLevelBadge", "Item Level Badge", 40, 20, "Item power/level badge.", text: "84", value: true, group: G.GameItems);
            yield return Container("Paperdoll", "Equipment Paperdoll", G.GameItems, 320, 440, "Character silhouette with equipment slots around it.", slots: new[] { Slot("content", "Slots"), Slot("preview", "Character Preview", 0, 1) });
            yield return Container("LoadoutPanel", "Loadout Panel", G.GameItems, 480, 320, "Named loadout with weapon, gear and perk slots.", slots: new[] { Slot("content", "Slots"), Slot("actions", "Actions", 0, 1) }, selectable: true, interactive: true);
            yield return Collection("BagTabs", "Bag Tabs", G.GameItems, 320, 40, "Inventory category tabs with capacity counts.", "tab", "Tab Template");
            yield return Status("WeightMeter", "Weight Meter", 200, 20, "Carry weight against capacity, with over-encumbered state.", value: true, group: G.GameItems);
            yield return Collection("SortFilterBar", "Sort & Filter Bar", G.GameItems, 400, 40, "Inventory sorting and filtering controls.", "filter", "Filter Template");
            yield return Container("CraftingRecipe", "Crafting Recipe", G.GameItems, 400, 200, "Ingredient list, result preview and craft action.", slots: new[] { Slot("content", "Ingredients"), Slot("result", "Result", 0, 1), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Collection("CraftingQueue", "Crafting Queue", G.GameItems, 320, 200, "Queued crafts with remaining time.", "job", "Job Template");
            yield return Container("UpgradePanel", "Upgrade Panel", G.GameItems, 480, 300, "Upgrade level, cost, success chance and preview of the result.", slots: new[] { Slot("content", "Stats"), Slot("cost", "Cost", 0, 1), Slot("actions", "Actions", 0, 1) }, value: true, interactive: true);
            yield return Container("EnchantPanel", "Enchant Panel", G.GameItems, 460, 300, "Affix selection and reroll with a cost preview.", slots: new[] { Slot("content", "Affixes"), Slot("cost", "Cost", 0, 1), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Container("SalvagePanel", "Salvage Panel", G.GameItems, 420, 280, "Multi-select salvage with the resulting materials.", slots: new[] { Slot("content", "Items"), Slot("result", "Materials", 0, 1), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Collection("VendorList", "Vendor List", G.GameItems, 420, 420, "Merchant stock with price and affordability state.", "offer", "Offer Template");
            yield return Collection("BuybackList", "Buyback List", G.GameItems, 320, 240, "Recently sold items available to buy back.", "item", "Item Template");
            yield return Collection("AuctionRow", "Auction Row", G.GameItems, 480, 44, "Auction listing: item, quantity, bid, buyout, time left.", "column", "Column Template");
            yield return Container("MailItem", "Mail Item", G.GameItems, 420, 72, "Mail entry with attachments and claim action.", slots: new[] { Slot("content", "Content"), Slot("attachments", "Attachments", 0, 1), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Collection("CurrencyBar", "Currency Bar", G.GameItems, 320, 36, "Row of held currencies with add buttons.", "currency", "Currency Template");
            yield return Container("LootTableRow", "Loot Table Row", G.GameItems, 360, 40, "Possible drop with its chance.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Content"), Slot("chance", "Chance", 0, 1) });
            yield return Container("ChestOpen", "Chest Opening", G.GameItems, 480, 360, "Loot box/chest opening surface with reveal slots.", slots: new[] { Slot("content", "Reveal"), Slot("actions", "Actions", 0, 1) }, overlay: true);
            yield return Collection("SummonResult", "Summon Result", G.GameItems, 640, 400, "Gacha pull results grid with rarity reveal.", "result", "Result Template", overlay: true);
            yield return Status("PityCounter", "Pity Counter", 200, 32, "Pulls remaining until a guaranteed rare.", text: "62 / 90", value: true, group: G.GameItems);
            yield return Container("GiftClaim", "Gift Claim", G.GameItems, 360, 200, "Claimable gift with reward preview.", slots: new[] { Slot("content", "Rewards"), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Collection("CollectionAlbum", "Collection Album", G.GameItems, 520, 400, "Owned/unowned collectibles grid.", "entry", "Entry Template");
            yield return Container("CodexEntry", "Codex Entry", G.GameItems, 480, 360, "Lore/bestiary entry with art, description and stats.", slots: new[] { Slot("image", "Art", 0, 1), Slot("content", "Body"), Slot("stats", "Stats", 0, 1) });

            // ---- Progression & rewards ------------------------------------------------------
            yield return Meter("ExperienceBar", "Experience Bar", 480, 20, "XP toward the next level, with a gain animation.", group: G.GameProgression);
            yield return Container("LevelUpPopup", "Level Up Popup", G.GameProgression, 420, 300, "Level-up celebration with unlocked rewards.", slots: new[] { Slot("content", "Rewards"), Slot("actions", "Actions", 0, 1) }, overlay: true);
            yield return Container("SkillTree", "Skill Tree", G.GameProgression, 720, 480, "Pannable talent graph with node connectors.", slots: new[] { Slot("content", "Nodes"), Slot("detail", "Node Detail", 0, 1) }, interactive: true);
            yield return Collection("TalentGrid", "Talent Grid", G.GameProgression, 420, 360, "Grid of talent choices with point spending.", "talent", "Talent Template");
            yield return Status("SkillPointCounter", "Skill Point Counter", 180, 32, "Unspent skill/talent points.", text: "3 points", value: true, group: G.GameProgression);
            yield return Collection("BattlePassTrack", "Battle Pass Track", G.GameProgression, 720, 200, "Free/premium reward tiers along a progress rail.", "tier", "Tier Template");
            yield return Container("SeasonTier", "Season Tier", G.GameProgression, 120, 180, "One battle-pass tier with locked/claimable/claimed state.", slots: new[] { Slot("image", "Reward", 0, 1), Slot("content", "Content") }, interactive: true, selectable: true);
            yield return Collection("DailyLoginCalendar", "Daily Login Calendar", G.GameProgression, 480, 320, "Day grid of login rewards with a streak marker.", "day", "Day Template");
            yield return Container("QuestLog", "Quest Log", G.GameProgression, 720, 480, "Quest list plus the selected quest's detail.", slots: new[] { Slot("content", "Quest List"), Slot("detail", "Detail", 0, 1) }, interactive: true);
            yield return Container("QuestObjective", "Quest Objective", G.GameProgression, 320, 32, "One objective line with progress and completion tick.", slots: new[] { Slot("content", "Text"), Slot("progress", "Progress", 0, 1) }, value: true);
            yield return Container("AchievementRow", "Achievement Row", G.GameProgression, 480, 72, "Achievement with progress, points and unlock date.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Content"), Slot("progress", "Progress", 0, 1) }, value: true);
            yield return Container("MasteryRing", "Mastery Ring", G.GameProgression, 120, 120, "Circular mastery/affinity progress for a character or weapon.", slots: new[] { Slot("content", "Center", 0, 1) }, value: true, shape: DesignerElementShape.Circle);
            yield return Container("ReputationBar", "Reputation Bar", G.GameProgression, 360, 40, "Faction standing with named tiers.", slots: new[] { Slot("content", "Tiers") }, value: true);
            yield return Container("RankProgress", "Rank Progress", G.GameProgression, 360, 120, "Competitive rank, division and points to promotion.", slots: new[] { Slot("emblem", "Emblem", 0, 1), Slot("content", "Content") }, value: true);
            yield return Container("UnlockNotification", "Unlock Notification", G.GameProgression, 380, 100, "Feature or item unlocked banner.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Content") }, overlay: true);
            yield return Collection("RewardPreview", "Reward Preview", G.GameProgression, 360, 140, "Row of rewards a completion will grant.", "reward", "Reward Template");
            yield return Container("MilestoneTrack", "Milestone Track", G.GameProgression, 520, 120, "Event progress with reward checkpoints along a bar.", slots: new[] { Slot("content", "Milestones") }, value: true);
            yield return Meter("EnergyTimer", "Energy Timer", 200, 40, "Stamina/energy with a refill countdown.", group: G.GameProgression);
            yield return Container("VipLevel", "VIP Level", G.GameProgression, 320, 120, "VIP tier with progress and perk list.", slots: new[] { Slot("content", "Perks") }, value: true);

            // ---- Front-end menus & results ---------------------------------------------------
            yield return Container("TitleScreen", "Title Screen", G.GameMenu, 720, 480, "Logo, press-start prompt and build/version footer.", slots: new[] { Slot("logo", "Logo", 0, 1), Slot("content", "Prompt", 0, 1), Slot("footer", "Footer", 0, 1) });
            yield return Container("MainMenu", "Main Menu", G.GameMenu, 420, 480, "Front-end menu column with the primary entries.", slots: new[] { Slot("content", "Entries"), Slot("footer", "Footer", 0, 1) }, interactive: true);
            yield return Container("PauseMenu", "Pause Menu", G.GameMenu, 480, 420, "In-game pause overlay with resume/settings/quit.", slots: new[] { Slot("content", "Entries"), Slot("footer", "Footer", 0, 1) }, overlay: true, interactive: true);
            yield return Collection("SaveSlotList", "Save Slot List", G.GameMenu, 520, 400, "Save files with playtime, chapter and thumbnail.", "slot", "Slot Template");
            yield return Container("SaveSlot", "Save Slot", G.GameMenu, 480, 96, "One save entry with load/overwrite/delete actions.", slots: new[] { Slot("image", "Thumbnail", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, interactive: true, selectable: true);
            yield return Collection("DifficultySelector", "Difficulty Selector", G.GameMenu, 480, 200, "Difficulty options with a description of each.", "option", "Option Template");
            yield return Collection("CharacterSelect", "Character Select", G.GameMenu, 720, 420, "Roster grid with the selected character's preview and stats.", "character", "Character Template");
            yield return Container("CharacterPreview", "Character Preview", G.GameMenu, 320, 420, "Rotatable character render with name and class.", slots: new[] { Slot("content", "Render"), Slot("info", "Info", 0, 1) });
            yield return Collection("LevelSelect", "Level Select", G.GameMenu, 720, 420, "Stage grid with star ratings and lock state.", "level", "Level Template");
            yield return Container("StageCard", "Stage Card", G.GameMenu, 200, 240, "One stage: art, best score, stars, lock state.", slots: new[] { Slot("image", "Art", 0, 1), Slot("content", "Content") }, interactive: true, selectable: true);
            yield return Container("LoadingScreen", "Loading Screen", G.GameMenu, 720, 480, "Loading art, progress bar and rotating tips.", slots: new[] { Slot("image", "Art", 0, 1), Slot("content", "Tip", 0, 1), Slot("progress", "Progress", 0, 1) }, value: true);
            yield return Status("LoadingTip", "Loading Tip", 480, 48, "Rotating gameplay hint shown while loading.", text: "Tip: ...", group: G.GameMenu);
            yield return Status("PressStartPrompt", "Press Start Prompt", 320, 40, "Pulsing 'press any button' prompt.", text: "PRESS START", group: G.GameMenu);
            yield return Container("DeathScreen", "Death Screen", G.GameMenu, 720, 480, "You-died overlay with respawn and quit actions.", slots: new[] { Slot("content", "Message"), Slot("actions", "Actions", 0, 1) }, overlay: true);
            yield return Meter("RespawnTimer", "Respawn Timer", 200, 200, "Countdown until respawn.", shape: DesignerElementShape.Circle, group: G.GameMenu);
            yield return Container("MatchResults", "Match Results", G.GameMenu, 720, 480, "Victory/defeat banner, score breakdown and rewards.", slots: new[] { Slot("header", "Banner", 0, 1), Slot("content", "Breakdown"), Slot("actions", "Actions", 0, 1) }, states: SeverityStates);
            yield return Container("ScoreBreakdown", "Score Breakdown", G.GameMenu, 480, 280, "Line-by-line score with a running total.", slots: new[] { Slot("content", "Lines"), Slot("total", "Total", 0, 1) }, value: true);
            yield return Container("StarRatingResult", "Star Rating Result", G.GameMenu, 320, 120, "One-to-three star stage result with the earned stars animating in.", slots: new[] { Slot("content", "Stars") }, value: true);
            yield return Container("MvpCard", "MVP Card", G.GameMenu, 320, 400, "Post-match standout player with their headline stat.", slots: new[] { Slot("image", "Portrait", 0, 1), Slot("content", "Stats") });
            yield return Container("CreditsRoll", "Credits Roll", G.GameMenu, 720, 480, "Scrolling credits with section headers.", slots: new[] { Slot("content", "Entries") });
            yield return Container("QuitConfirm", "Quit Confirm", G.GameMenu, 420, 200, "Exit confirmation with unsaved-progress warning.", slots: new[] { Slot("content", "Message"), Slot("actions", "Actions", 0, 1) }, overlay: true);
            yield return Container("ControlsDiagram", "Controls Diagram", G.GameMenu, 640, 400, "Controller/keyboard map with labelled callouts.", slots: new[] { Slot("image", "Diagram", 0, 1), Slot("content", "Callouts") });
            yield return Container("TutorialHint", "Tutorial Hint", G.GameMenu, 320, 120, "Contextual tutorial bubble with an input glyph.", slots: new[] { Slot("key", "Glyph", 0, 1), Slot("content", "Text") }, overlay: true);
            yield return Container("AdRewardButton", "Ad Reward Button", G.GameMenu, 220, 56, "Watch-ad-for-reward action with cooldown state.", slots: new[] { Slot("icon", "Icon", 0, 1), Slot("content", "Label", 0, 1) }, interactive: true, value: true);
            yield return Container("RateUsPrompt", "Rate Us Prompt", G.GameMenu, 420, 220, "Store rating request with a dismiss option.", slots: new[] { Slot("content", "Message"), Slot("actions", "Actions", 0, 1) }, overlay: true);

            // ---- Multiplayer ------------------------------------------------------------------
            yield return Collection("TeamRoster", "Team Roster", G.GameMultiplayer, 360, 320, "Team members with score, status and role.", "member", "Member Template");
            yield return Collection("Scoreboard", "Scoreboard", G.GameMultiplayer, 720, 420, "Full match scoreboard, grouped by team.", "row", "Row Template");
            yield return Container("ScoreboardRow", "Scoreboard Row", G.GameMultiplayer, 640, 36, "One player line: name, K/D/A, ping, status.", slots: new[] { Slot("content", "Columns") }, interactive: true);
            yield return Container("LobbySlot", "Lobby Slot", G.GameMultiplayer, 300, 72, "Lobby seat: player or empty/bot/locked.", slots: new[] { Slot("avatar", "Avatar", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, interactive: true, states: Interactive | DesignerComponentState.Empty);
            yield return Container("ReadyCheck", "Ready Check", G.GameMultiplayer, 420, 220, "Accept/decline prompt with a countdown.", slots: new[] { Slot("content", "Message"), Slot("actions", "Actions", 0, 1) }, overlay: true, value: true);
            yield return Container("MatchmakingStatus", "Matchmaking Status", G.GameMultiplayer, 360, 140, "Queue state, elapsed time and estimated wait.", slots: new[] { Slot("content", "Status"), Slot("actions", "Cancel", 0, 1) }, value: true);
            yield return Container("PartyInvite", "Party Invite", G.GameMultiplayer, 360, 140, "Incoming invite with accept/decline.", slots: new[] { Slot("avatar", "Avatar", 0, 1), Slot("content", "Content"), Slot("actions", "Actions", 0, 1) }, overlay: true);
            yield return Container("GuildPanel", "Guild Panel", G.GameMultiplayer, 640, 420, "Guild identity, member list and activity feed.", slots: new[] { Slot("header", "Header", 0, 1), Slot("content", "Members"), Slot("feed", "Activity", 0, 1) });
            yield return Collection("ChatChannelTabs", "Chat Channel Tabs", G.GameMultiplayer, 320, 32, "Chat channel switcher with unread markers.", "channel", "Channel Template");
            yield return Status("VoiceIndicator", "Voice Indicator", 32, 32, "Speaking/muted state for a player.", group: G.GameMultiplayer);
            yield return Status("PingBadge", "Ping Badge", 72, 24, "Latency readout with quality color.", text: "24 ms", value: true, group: G.GameMultiplayer);
            yield return Status("HostBadge", "Host Badge", 60, 22, "Marks the lobby host or party leader.", text: "HOST", group: G.GameMultiplayer);
            yield return Collection("SpectatorBar", "Spectator Bar", G.GameMultiplayer, 640, 64, "Spectated player switcher with their vitals.", "player", "Player Template");
            yield return Container("KillCam", "Kill Cam", G.GameMultiplayer, 720, 480, "Killer identity, weapon and replay controls.", slots: new[] { Slot("header", "Killer", 0, 1), Slot("content", "Replay"), Slot("actions", "Actions", 0, 1) }, overlay: true);
            yield return Container("ReportPlayer", "Report Player", G.GameMultiplayer, 420, 320, "Report reason picker with optional detail.", slots: new[] { Slot("content", "Reasons"), Slot("actions", "Actions", 0, 1) }, overlay: true, interactive: true);
            yield return Collection("FriendInviteList", "Friend Invite List", G.GameMultiplayer, 360, 320, "Friends available to invite, with their status.", "friend", "Friend Template");
            yield return Container("CrossplayToggle", "Crossplay Toggle", G.GameMultiplayer, 360, 48, "Crossplay/region preference with a queue-time note.", slots: new[] { Slot("content", "Label"), Slot("control", "Toggle", 0, 1) }, interactive: true, selectable: true);
            yield return Container("SessionCode", "Session Code", G.GameMultiplayer, 320, 80, "Join code with copy and share actions.", slots: new[] { Slot("content", "Code"), Slot("actions", "Actions", 0, 1) }, interactive: true);
            yield return Collection("ServerList", "Server List", G.GameMultiplayer, 640, 400, "Browsable servers with players, ping and mode.", "server", "Server Template");
            yield return Container("TeamBanner", "Team Banner", G.GameMultiplayer, 360, 80, "Team name, colors and score.", slots: new[] { Slot("content", "Content") }, value: true);
        }
    }
}
