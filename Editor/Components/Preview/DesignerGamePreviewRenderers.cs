using System.Collections.Generic;

namespace emiteat.NexUI.Designer.Editor.Components.Preview
{
    /// <summary>
    /// Canvas previews for <see cref="NexUIGameCatalog"/>. Game UI is mostly built from a handful of
    /// recurring shapes - a bar, a ring, a slot, a row, a card, a grid - so entries map onto the
    /// existing renderers rather than growing a bespoke renderer per HUD readout. The canvas needs to
    /// communicate which shape an element is; the art and the numbers are the project's job.
    /// </summary>
    internal static class DesignerGamePreviewRenderers
    {
        public static void Register(Dictionary<string, IUIDesignerComponentPreviewRenderer> byId)
        {
            var bar = new LinearFillPreviewRenderer();
            var ring = new RadialPreviewRenderer(spin: false);
            var spin = new RadialPreviewRenderer(spin: true);
            var rows = new CollectionPreviewRenderer(grid: false);
            var grid = new CollectionPreviewRenderer(grid: true);
            var iconRow = new IconRowPreviewRenderer();
            var slot = new SlotPreviewRenderer();
            var listRow = new ListRowPreviewRenderer();
            var tile = new StatTilePreviewRenderer();
            var table = new TablePreviewRenderer();
            var tabs = new TabStripPreviewRenderer();
            var alert = new AlertPreviewRenderer();
            var image = new ImagePreviewRenderer(fullBleed: true);
            var icon = new ImagePreviewRenderer(fullBleed: false);
            var skeleton = new SkeletonPreviewRenderer();
            var splitter = new SplitterPreviewRenderer();
            var empty = new EmptyStatePreviewRenderer();
            var keyPrompt = new KeyPromptPreviewRenderer();
            var crosshair = new CrosshairPreviewRenderer();
            var minimap = new MinimapPreviewRenderer();
            var stepper = new StepperPreviewRenderer();
            var rating = new RatingPreviewRenderer();

            // ---- Combat HUD: bars, rings and pip rows --------------------------------------
            foreach (var id in new[]
                     {
                         "HealthBar", "ShieldBar", "ArmorBar", "ManaBar", "EnergyBar", "BreathMeter",
                         "HungerMeter", "SanityMeter", "HeatMeter", "ChargeMeter", "ThreatMeter",
                         "NoiseMeter", "NitroBar"
                     })
                byId[id] = bar;
            foreach (var id in new[]
                     {
                         "StaminaWheel", "UltimateCharge", "ReloadIndicator", "DetectionMeter",
                         "Tachometer", "RespawnTimer", "MasteryRing"
                     })
                byId[id] = ring;
            byId["HealthPips"] = iconRow;
            byId["AmmoPips"] = iconRow;
            byId["StatusEffectIcon"] = slot;
            byId["AbilityQueue"] = iconRow;
            byId["StanceSelector"] = tabs;
            byId["GrenadeSelector"] = iconRow;
            byId["WeaponSlot"] = slot;
            byId["HitMarker"] = crosshair;
            byId["DamageIndicator"] = crosshair;
            byId["ComboRank"] = tile;
            byId["AccuracyMeter"] = bar;
            byId["JudgementText"] = tile;
            byId["GearIndicator"] = tile;
            byId["VehicleHud"] = tile;

            // ---- World & navigation ---------------------------------------------------------
            byId["MapScreen"] = minimap;
            byId["Radar"] = minimap;
            byId["MapMarkerList"] = rows;
            byId["ObjectiveList"] = rows;
            byId["OffscreenIndicator"] = crosshair;
            byId["LockOnIndicator"] = crosshair;
            byId["CompassMarker"] = crosshair;
            byId["DistanceMeter"] = tile;
            byId["TimeOfDay"] = tile;
            byId["WeatherIndicator"] = tile;
            byId["DepthMeter"] = bar;
            byId["TravelProgress"] = bar;
            byId["ZoneBanner"] = alert;
            byId["DiscoveryToast"] = alert;
            byId["WorldTooltip"] = alert;
            byId["PlacementGhost"] = empty;
            byId["Letterbox"] = empty;
            byId["SkipPrompt"] = keyPrompt;

            // ---- Items & inventory ------------------------------------------------------------
            byId["ItemCard"] = tile;
            byId["ItemTooltip"] = alert;
            byId["ItemComparison"] = splitter;
            byId["RarityFrame"] = slot;
            byId["DurabilityBar"] = bar;
            byId["WeightMeter"] = bar;
            byId["StackCount"] = tile;
            byId["ItemLevelBadge"] = tile;
            byId["PityCounter"] = bar;
            byId["Paperdoll"] = grid;
            byId["LoadoutPanel"] = grid;
            byId["BagTabs"] = tabs;
            byId["SortFilterBar"] = iconRow;
            byId["CraftingRecipe"] = listRow;
            byId["CraftingQueue"] = rows;
            byId["UpgradePanel"] = tile;
            byId["EnchantPanel"] = rows;
            byId["SalvagePanel"] = grid;
            byId["VendorList"] = rows;
            byId["BuybackList"] = rows;
            byId["AuctionRow"] = listRow;
            byId["MailItem"] = listRow;
            byId["CurrencyBar"] = iconRow;
            byId["LootTableRow"] = listRow;
            byId["ChestOpen"] = spin;
            byId["SummonResult"] = grid;
            byId["GiftClaim"] = tile;
            byId["CollectionAlbum"] = grid;
            byId["CodexEntry"] = skeleton;

            // ---- Progression & rewards ----------------------------------------------------------
            byId["ExperienceBar"] = bar;
            byId["ReputationBar"] = bar;
            byId["MilestoneTrack"] = bar;
            byId["EnergyTimer"] = bar;
            byId["QuestObjective"] = listRow;
            byId["AchievementRow"] = listRow;
            byId["LevelUpPopup"] = tile;
            byId["SkillTree"] = grid;
            byId["TalentGrid"] = grid;
            byId["SkillPointCounter"] = tile;
            byId["BattlePassTrack"] = iconRow;
            byId["SeasonTier"] = slot;
            byId["DailyLoginCalendar"] = grid;
            byId["QuestLog"] = splitter;
            byId["RankProgress"] = tile;
            byId["VipLevel"] = tile;
            byId["UnlockNotification"] = alert;
            byId["RewardPreview"] = iconRow;

            // ---- Menus & results -------------------------------------------------------------------
            byId["TitleScreen"] = empty;
            byId["MainMenu"] = rows;
            byId["PauseMenu"] = rows;
            byId["SaveSlotList"] = rows;
            byId["SaveSlot"] = listRow;
            byId["DifficultySelector"] = rows;
            byId["CharacterSelect"] = grid;
            byId["CharacterPreview"] = image;
            byId["LevelSelect"] = grid;
            byId["StageCard"] = tile;
            byId["LoadingScreen"] = skeleton;
            byId["LoadingTip"] = alert;
            byId["PressStartPrompt"] = keyPrompt;
            byId["DeathScreen"] = empty;
            byId["MatchResults"] = tile;
            byId["ScoreBreakdown"] = table;
            byId["StarRatingResult"] = rating;
            byId["MvpCard"] = tile;
            byId["CreditsRoll"] = rows;
            byId["QuitConfirm"] = alert;
            byId["ControlsDiagram"] = image;
            byId["TutorialHint"] = alert;
            byId["AdRewardButton"] = listRow;
            byId["RateUsPrompt"] = alert;

            // ---- Multiplayer -------------------------------------------------------------------------
            byId["TeamRoster"] = rows;
            byId["Scoreboard"] = table;
            byId["ScoreboardRow"] = listRow;
            byId["LobbySlot"] = listRow;
            byId["ReadyCheck"] = alert;
            byId["MatchmakingStatus"] = tile;
            byId["PartyInvite"] = listRow;
            byId["GuildPanel"] = splitter;
            byId["ChatChannelTabs"] = tabs;
            byId["VoiceIndicator"] = icon;
            byId["PingBadge"] = tile;
            byId["HostBadge"] = tile;
            byId["SpectatorBar"] = iconRow;
            byId["KillCam"] = image;
            byId["ReportPlayer"] = rows;
            byId["FriendInviteList"] = rows;
            byId["CrossplayToggle"] = listRow;
            byId["SessionCode"] = stepper;
            byId["ServerList"] = table;
            byId["TeamBanner"] = tile;
        }
    }
}
