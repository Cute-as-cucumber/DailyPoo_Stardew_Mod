using System;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Objects;

namespace DailyPoo;

public sealed class ModEntry : Mod
{
    private const int HeartEmote = 20;
    private const int SadEmote = 28;
    private const int HappyEmote = 32;
    private const int QuestionEmote = 8;
    private const int PauseEmote = 40;
    private const string SaveKey = "daily-poo-state";

    private const int AutoPoopDelayTicks = 180; // ~3 seconds seated before auto-poop
    private const string FertilizerId = "(O)368"; // vanilla Basic Fertilizer

    private ModConfig Config = null!;
    private PoopState State = new();

    private int SitTicks;

    private string ToiletId => $"{this.ModManifest.UniqueID}_Toilet";
    private string ToiletQualifiedId => $"(F){this.ToiletId}";
    private string ToiletTextureName => $"Mods/{this.ModManifest.UniqueID}/Toilet";
    private string MailId => $"{this.ModManifest.UniqueID}_ToiletMail";

    private int PoopEmote => this.Config.Emote?.Trim().ToLowerInvariant() == "happy" ? HappyEmote : HeartEmote;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();

        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.DayEnding += this.OnDayEnding;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

        this.Monitor.Log("Daily Poo loaded. Sit on your toilet and press P to poop.", LogLevel.Info);
    }

    /**********
    ** Content
    **********/

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))
        {
            e.Edit(asset =>
            {
                // fields: name/type/tilesheet size/bounding box/rotations/price/placement/display name/sprite index/texture
                // backslashes are the game's path separator inside slash-delimited data
                string texture = this.ToiletTextureName.Replace('/', '\\');
                asset.AsDictionary<string, string>().Data[this.ToiletId] =
                    $"{this.ToiletId}/chair/1 2/1 1/1/500/-1/Toilet/0/{texture}";
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(this.ToiletTextureName))
        {
            e.LoadFromModFile<Texture2D>("assets/toilet.png", AssetLoadPriority.Exclusive);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
        {
            e.Edit(asset =>
            {
                asset.AsDictionary<string, string>().Data[this.MailId] =
                    "Dear @,^^Word around town is that every farmer eventually needs one of these. Don't ask how I knew.^^"
                    + "Sit on it once a day and take care of business!^^   -Robin"
                    + $" %item id {this.ToiletQualifiedId} 1 %%"
                    + "[#]A Very Necessary Gift";
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
        {
            e.Edit(asset =>
            {
                if (asset.AsDictionary<string, ShopData>().Data.TryGetValue("Carpenter", out ShopData? shop))
                {
                    shop.Items.Add(new ShopItemData
                    {
                        Id = this.ToiletId,
                        ItemId = this.ToiletQualifiedId,
                        Price = 500
                    });
                }
            });
        }
    }

    /**********
    ** Config menu
    **********/

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(
            this.ModManifest,
            reset: () => this.Config = new ModConfig(),
            save: () => this.Helper.WriteConfig(this.Config));

        gmcm.AddKeybindList(this.ModManifest,
            () => this.Config.PoopKey, v => this.Config.PoopKey = v,
            () => "Poop key",
            () => "Pressed while sitting on the toilet.");
        gmcm.AddBoolOption(this.ModManifest,
            () => this.Config.PlaySound, v => this.Config.PlaySound = v,
            () => "Play sound");
        gmcm.AddNumberOption(this.ModManifest,
            () => this.Config.PenaltyFraction, v => this.Config.PenaltyFraction = v,
            () => "Constipated energy cap",
            () => "Fraction of max energy available while constipated.",
            min: 0.05f, max: 1f, interval: 0.05f);
        gmcm.AddTextOption(this.ModManifest,
            () => this.Config.Emote, v => this.Config.Emote = v,
            () => "Poop emote",
            () => "Emote bubble shown after pooping.",
            allowedValues: new[] { "heart", "happy" });
        gmcm.AddBoolOption(this.ModManifest,
            () => this.Config.FestivalGrace, v => this.Config.FestivalGrace = v,
            () => "Festival grace",
            () => "Skip the poop requirement on festival days, when the toilet may be unreachable.");
        gmcm.AddBoolOption(this.ModManifest,
            () => this.Config.AutoPoop, v => this.Config.AutoPoop = v,
            () => "Auto-poop",
            () => "Sitting on the toilet for about 3 seconds poops automatically.");
        gmcm.AddBoolOption(this.ModManifest,
            () => this.Config.ProduceFertilizer, v => this.Config.ProduceFertilizer = v,
            () => "Produce fertilizer",
            () => "Each poop produces one Basic Fertilizer. Waste not.");
    }

    /**********
    ** State & daily cycle
    **********/

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.State = Context.IsMainPlayer
            ? this.Helper.Data.ReadSaveData<PoopState>(SaveKey) ?? new PoopState()
            : new PoopState();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (Context.IsMainPlayer)
            this.Helper.Data.WriteSaveData(SaveKey, this.State);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // deliver the free toilet once per save
        if (!Game1.player.mailReceived.Contains(this.MailId) && !Game1.player.mailbox.Contains(this.MailId))
            Game1.player.mailbox.Add(this.MailId);

        // skipped a day without pooping -> constipated until they poop
        if (!this.State.PoopedToday)
            this.State.IsConstipated = true;
        this.State.PoopedToday = false;

        if (this.State.IsConstipated)
        {
            this.CapStamina();
            Game1.player.doEmote(SadEmote);
            Game1.addHUDMessage(new HUDMessage("Ugh, I feel so heavy... I really should have pooped yesterday.", HUDMessage.error_type));
        }
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        // no penalty for festival days: the player may not be able to reach their toilet
        if (this.Config.FestivalGrace && Utility.isFestivalDay(Game1.dayOfMonth, Game1.season))
            this.State.PoopedToday = true;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        // while constipated, food can't push energy above the cap (checked ~once per second)
        if (this.State.IsConstipated && e.IsMultipleOf(60))
            this.CapStamina();

        // auto-poop after sitting on the toilet for a moment
        if (this.Config.AutoPoop && this.NeedsToPoop && this.IsOnToilet
            && Game1.activeClickableMenu is null && !Game1.eventUp)
        {
            if (++this.SitTicks >= AutoPoopDelayTicks)
            {
                this.SitTicks = 0;
                this.DoPoop();
            }
        }
        else
        {
            this.SitTicks = 0;
        }
    }

    /**********
    ** Poop action
    **********/

    private bool IsOnToilet =>
        Game1.player.isSitting.Value
        && Game1.player.sittingFurniture is Furniture furniture
        && furniture.QualifiedItemId == this.ToiletQualifiedId;

    private bool NeedsToPoop => !this.State.PoopedToday || this.State.IsConstipated;

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        // note: can't use Context.IsPlayerFree here, it's false while sitting
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null || Game1.eventUp)
            return;
        if (!this.Config.PoopKey.JustPressed())
            return;

        if (!this.IsOnToilet)
        {
            Game1.player.doEmote(QuestionEmote);
            Game1.addHUDMessage(new HUDMessage("Not here... I need my toilet.", HUDMessage.error_type));
            return;
        }

        if (!this.NeedsToPoop)
        {
            Game1.player.doEmote(PauseEmote);
            this.ShowCornerMessage("Again? I've got nothing left...");
            return;
        }

        this.DoPoop();
    }

    private void DoPoop()
    {
        bool wasConstipated = this.State.IsConstipated;
        this.State.PoopedToday = true;
        this.State.IsConstipated = false;

        Game1.player.doEmote(this.PoopEmote);
        if (this.Config.PlaySound)
            Game1.playSound("slosh");
        this.ShowCornerMessage(wasConstipated ? "Finally! I feel SO much lighter now!" : "I feel lighter now!");

        if (this.Config.ProduceFertilizer)
        {
            Item fertilizer = ItemRegistry.Create(FertilizerId);
            Game1.player.addItemByMenuIfNecessary(fertilizer);
            Game1.addHUDMessage(HUDMessage.ForItemGained(
                fertilizer,
                1,
                $"{this.ModManifest.UniqueID}/Fertilizer"
            ));
        }

        this.Monitor.Log("Player pooped.", LogLevel.Trace);
    }

    /**********
    ** Corner reminder
    **********/

    private void ShowCornerMessage(string text)
    {
        Game1.addHUDMessage(HUDMessage.ForCornerTextbox(text));
    }

    /**********
    ** Helpers
    **********/

    private void CapStamina()
    {
        float cap = Game1.player.MaxStamina * Math.Clamp(this.Config.PenaltyFraction, 0.05f, 1f);
        if (Game1.player.stamina > cap)
            Game1.player.stamina = cap;
    }
}
