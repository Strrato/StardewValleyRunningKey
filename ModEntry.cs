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
        private int baseSpeed;
        private float finalAdded;
        private int baseStamina;
        private int finalMinStamina;
        private int lastTickLose;
        private bool stateChanged = false;
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

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Running speed gain",
                tooltip: () => "% of speed gainned when pressing the running button",
                getValue: () => this.Config.RunningSpeedGain,
                setValue: value => this.Config.RunningSpeedGain = (int)value,
                min: 1f,
                max: 300f,
                interval: 1
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
                tooltip: () => "# of stamina leached from running, calculated at rate tick in configuration",
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
                name: () => "Enable gamepad left stick",
                getValue: () => this.Config.EnableGamePad,
                setValue: value => this.Config.EnableGamePad = value
            );

        }

        private void SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            baseSpeed = Game1.player.speed;
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
                stateChanged = true;
            }else if (!isRunning && stateChanged)
            {
                Game1.player.speed = baseSpeed;
                stateChanged = false;
            }
        }

        private void run()
        {
            this.originalSpeed = Game1.player.Speed;

            CalculateFinalSpeed();

            if (finalAdded > 0)
            {
                //Game1.player.Speed = (int)FinalAdded + 5;
                Game1.player.speed = (int)finalAdded;
                this.isRunning = true;


                if (lastTickLose + this.Config.StaminaTick < Game1.ticks)
                {
                    // No more free stamina from running
                    if (this.Config.StaminaLose > 0)
                    {
                        Game1.player.Stamina -= this.Config.StaminaLose;
                    }
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

            if (!stateChanged)
            {
                // Was previously not running, take the previous speed as reference
                baseSpeed = getSpeedPlayerBuffs() + Game1.player.speed;
            }

            var finalSpeed = finalAdded + baseSpeed;
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

        private int getSpeedPlayerBuffs()
        {
            float result = 0;

            if (Game1.player.buffs.AppliedBuffs.Count > 0)
            {
                foreach (KeyValuePair<string, Buff> buff in Game1.player.buffs.AppliedBuffs)
                {
                    
                    if (buff.Value.effects.Speed.Value != 0)
                    {
                        result += buff.Value.effects.Speed.Value;
                    }
                }
            }

            return (int)result;
        }
    }
}
