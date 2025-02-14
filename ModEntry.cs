using System;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace RunningKey
{
    public class ModConfig
    {
        /// <summary>
        /// Gain speed percent compared to base speed of the character
        /// </summary>
        public int RunningSpeedGain { get; set; } = 70;

        /// <summary>
        /// Sbutton key tu be pressed to enable run
        /// </summary>
        public SButton Key { get; set; } = SButton.LeftShift;

        /// <summary>
        /// Number of stamina losed while running (amount per StaminaTick elapsed)
        /// </summary>
        public int StaminaLose { get; set; } = 1;

        /// <summary>
        /// Stamina wil be losed every StaminaTick gameticks
        /// </summary>
        public int StaminaTick { get; set; } = 30;

        /// <summary>
        /// If this limit is reached, run will be disabled
        /// </summary>
        public int StaminaLimit { get; set; } = 20;

        /// <summary>
        /// Enable run with left stick on gamepad
        /// </summary>
        public bool EnableGamePad { get; set; } = true;

        /// <summary>
        /// Reduction gain speed depends on game, reduction is removed to base gain
        /// @deprecated 
        /// </summary>
        public int RainReductionPercent { get; set; } = 20;
        public int SnowReductionPercent { get; set; } = 30;
        public int HoeReductionPercent { get; set; } = 10;

        /// <summary>
        /// Tile specific gain percent
        /// @deprecated
        /// </summary>
        public int FlooredGainPercent { get; set; } = 10;

    }

    public class ModEntry : Mod
    {
        private ModConfig Config;
        private float baseSpeed;
        private float finalAdded;
        private int baseStamina;
        private int finalMinStamina;
        private int lastTickLose;
        private bool isRunning { get; set; } = false;
        private int originalSpeed = 0;
        private bool wasRunningPad { get; set; } = false;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.DayStarted   += this.DayStart;
            helper.Events.GameLoop.DayEnding    += this.DayEnd;
            helper.Events.GameLoop.SaveLoaded   += this.SaveLoaded;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Running speed gain",
                tooltip: () => "% of speed gainned when pressing the running button",
                getValue: () => this.Config.RunningSpeedGain,
                setValue: value => this.Config.RunningSpeedGain = value
                );

            configMenu.AddKeybind(
                mod: this.ModManifest,
                name: () => "Assigned key",
                tooltip: () => "Key to run, be careful to unset leftshift key in original game key binding if you choose this one",
                getValue: () => this.Config.Key,
                setValue: value => this.Config.Key = value
                );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Stamina losed over time",
                tooltip: () => "% of stamina leached from running, calculated at rate tick in configuration",
                getValue: () => this.Config.StaminaLose,
                setValue: value => this.Config.StaminaLose = value
            );


            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Lose stamina every x tick",
                getValue: () => this.Config.StaminaTick,
                setValue: value => this.Config.StaminaTick = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Limit stamina required to run",
                tooltip: () => "If this limit is reached, run will be disabled",
                getValue: () => this.Config.StaminaLimit,
                setValue: value => this.Config.StaminaLimit = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enable gamepad left stick running option",
                getValue: () => this.Config.EnableGamePad,
                setValue: value => this.Config.EnableGamePad = value
                );

        }

        private void SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            baseSpeed = Game1.player.addedSpeed;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            GamePadState currentState = GamePad.GetState(PlayerIndex.One);

            bool keyRun = this.Helper.Input.IsDown(this.Config.Key);
            bool isMovingThumb = false;
            bool runPressed = false;

            if (this.Config.EnableGamePad)
            {
                isMovingThumb = currentState.ThumbSticks.Left.X > 0.1f || currentState.ThumbSticks.Left.X < -0.1f || currentState.ThumbSticks.Left.Y > 0.1f || currentState.ThumbSticks.Left.Y < -0.1f;
                runPressed = currentState.IsConnected && currentState.Buttons.LeftStick == ButtonState.Pressed;
            }

            if (isRunning && isMovingThumb)
            {
                wasRunningPad = true;
            }else if (runPressed)
            {
                wasRunningPad = true;
            }else
            {
                wasRunningPad = false;
                isRunning = false;
            }

            if ((wasRunningPad || keyRun) && Game1.player.Stamina > finalMinStamina)
            {
                run();
            }else if (!isRunning)
            {
                Game1.player.addedSpeed = baseSpeed;
            }
        }

        private void run()
        {
            this.originalSpeed = Game1.player.Speed;

            CalculateFinalSpeed();

            if (finalAdded > 0)
            {
                //Game1.player.Speed = (int)FinalAdded + 5;
                Game1.player.addedSpeed = (int)finalAdded;
                this.isRunning = true;


                if (lastTickLose + this.Config.StaminaTick < Game1.ticks)
                {
                    Game1.player.Stamina -= this.Config.StaminaLose;
                    lastTickLose = Game1.ticks;
                }
            }
            else
                isRunning = false;
        }

        private void DayStart(object sender, DayStartedEventArgs e)
        {
            // Speed reference
            //BaseSpeed = 5f;

            // Stamina reference
            baseStamina = Game1.player.MaxStamina;
            finalMinStamina = (baseStamina * this.Config.StaminaLimit) / 100;
        }

        private void DayEnd(object sender, DayEndingEventArgs e)
        {
            // Speed reference
            //BaseSpeed = 0f;
        }


        private void CalculateFinalSpeed()
        {
            finalAdded = addPercent(5, this.Config.RunningSpeedGain) - 5;
            if (finalAdded < 0)
            {
                finalAdded = finalAdded * -1;
            }
            /*
            Vector2 TileLocation = Game1.player.getTileLocation();
            if (Game1.player.currentLocation.terrainFeatures.ContainsKey(TileLocation))
            {
                var currentTile = Game1.player.currentLocation.terrainFeatures[TileLocation];

                if (Game1.isRaining)
                {
                    FinalAdded = removePercent(FinalAdded, this.Config.RainReductionPercent);
                }else if (Game1.isSnowing)
                {
                    FinalAdded = removePercent(FinalAdded, this.Config.SnowReductionPercent);
                }

                if(currentTile is StardewValley.TerrainFeatures.Flooring)
                {
                    FinalAdded = addPercent(FinalAdded, this.Config.FlooredGainPercent);
                }else if (currentTile is StardewValley.TerrainFeatures.HoeDirt)
                {
                    FinalAdded = removePercent(FinalAdded, this.Config.HoeReductionPercent);
                }
            }
            */
            var finalSpeed = finalAdded - baseSpeed;
            finalAdded = finalSpeed > 0 ? finalSpeed : 0;
        }

        private float addPercent(float baseNumber, int percent)
        {
            return baseNumber + ((percent / 100f) * baseNumber);
        }

        private float removePercent(float baseNumber, int percent)
        {
            return baseNumber - ((percent / 100f) * baseNumber);
        }
    }
}
