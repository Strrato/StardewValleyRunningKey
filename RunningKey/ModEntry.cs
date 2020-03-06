using System;
using Microsoft.Xna.Framework;
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
        public int RunningSpeedGain { get; set; } = 50;

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
        public int StaminaTick { get; set; } = 15;

        /// <summary>
        /// If this limit is reached, run will be disabled
        /// </summary>
        public int StaminaLimit { get; set; } = 40;

        /// <summary>
        /// Reduction gain speed depends on game, reduction is removed to base gain
        /// </summary>
        public int RainReductionPercent { get; set; } = 20;
        public int SnowReductionPercent { get; set; } = 30;
        public int HoeReductionPercent { get; set; } = 10;

        /// <summary>
        /// Tile specific gain percent
        /// </summary>
        public int FlooredGainPercent { get; set; } = 10;
    }

    public class ModEntry : Mod
    {
        private ModConfig Config;
        private float BaseSpeed;
        private float FinalAdded;
        private int BaseStamina;
        private int FinalMinStamina;
        private int lastTickLose;


        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.DayStarted   += this.DayStart;
            helper.Events.GameLoop.DayEnding    += this.DayEnd;

        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (BaseSpeed > 0 && this.Helper.Input.IsDown(this.Config.Key) && !Game1.player.isRidingHorse() && Game1.player.Stamina > FinalMinStamina)
            {
                CalculateFinalSpeed();
                Game1.player.addedSpeed = (int)FinalAdded;

                if (lastTickLose + this.Config.StaminaTick < Game1.ticks)
                {
                    Game1.player.Stamina -= this.Config.StaminaLose;
                    lastTickLose = Game1.ticks;
                }
            }
        }

        private void DayStart(object sender, DayStartedEventArgs e)
        {
            // Speed reference
            BaseSpeed = Game1.player.speed;

            // Stamina reference
            BaseStamina = Game1.player.MaxStamina;
            FinalMinStamina = (BaseStamina * this.Config.StaminaLimit) / 100;
        }

        private void DayEnd(object sender, DayEndingEventArgs e)
        {
            // Speed reference
            BaseSpeed = 0f;
        }


        private void CalculateFinalSpeed()
        {
            FinalAdded = addPercent(BaseSpeed, this.Config.RunningSpeedGain);
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

            var finalSpeed = FinalAdded - BaseSpeed;
            FinalAdded = finalSpeed > 0 ? finalSpeed : 0;
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
