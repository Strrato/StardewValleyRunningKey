using System;
using System.Collections.Generic;
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
        /// Extra speed bonus applied when the base game Auto Run option is disabled AND the player is walking.
        /// This compensate the walk/run baseline difference trying to make sprint feel consistent.
        /// </summary>
        public int AutoRunOffCompensation { get; set; } = 2;
    }

    public class ModEntry : Mod
    {
#pragma warning disable CS8618
        private ModConfig _config;
#pragma warning restore CS8618

        private sealed class PlayerRunState
        {
            public int BaseSpeed;
            public int BaseStamina;
            public int FinalMinStamina;
            public int LastTickLose;
            public bool StateChanged;
            public bool IsRunning;
            public bool WasRunningPad;
        }

        private readonly Dictionary<long, PlayerRunState> _playerState = new();

        private PlayerRunState GetState(Farmer farmer)
        {
            if (!_playerState.TryGetValue(farmer.UniqueMultiplayerID, out var state))
            {
                state = new PlayerRunState
                {
                    BaseSpeed = farmer.speed,
                    BaseStamina = farmer.MaxStamina,
                    FinalMinStamina = (farmer.MaxStamina * _config.StaminaLimit) / 100,
                    LastTickLose = 0,
                    StateChanged = false,
                    IsRunning = false,
                    WasRunningPad = false
                };
                _playerState[farmer.UniqueMultiplayerID] = state;
            }

            return state;
        }

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.DayStarted += DayStart;
            helper.Events.GameLoop.DayEnding += DayEnd;
            helper.Events.GameLoop.SaveLoaded += SaveLoaded;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => _config = new ModConfig(),
                save: () => Helper.WriteConfig(_config)
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Running speed gain",
                tooltip: () => "% of speed gained when pressing the running button",
                getValue: () => _config.RunningSpeedGain,
                setValue: value => _config.RunningSpeedGain = (int)value,
                min: 1f,
                max: 300f,
                interval: 1
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => "Assigned key",
                tooltip: () => "Key to run, be careful to unset leftshift key in original game key binding if you choose this one",
                getValue: () => _config.Key,
                setValue: value => _config.Key = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Stamina lost over time",
                tooltip: () => "# of stamina leached from running, calculated at rate tick in configuration",
                getValue: () => _config.StaminaLose,
                setValue: value => _config.StaminaLose = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Lose stamina every x tick",
                getValue: () => _config.StaminaTick,
                setValue: value => _config.StaminaTick = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Limit stamina required to run",
                tooltip: () => "If this limit is reached, run will be disabled",
                getValue: () => _config.StaminaLimit,
                setValue: value => _config.StaminaLimit = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Enable gamepad left stick",
                getValue: () => _config.EnableGamePad,
                setValue: value => _config.EnableGamePad = value
            );
            
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Compensation for Auto Run disabled when walking",
                tooltip: () => "If auto run is disabled and the player is walking, this value will be added to the base speed to compensate the walk/run baseline difference trying to make sprint feel consistent.",
                getValue: () => _config.AutoRunOffCompensation,
                setValue: value => _config.AutoRunOffCompensation = value
            );
        }

        private void SaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _playerState.Clear();

            if (Context.IsWorldReady)
            {
                var farmer = Game1.player;
                var state = GetState(farmer);
                state.BaseSpeed = farmer.speed;
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            Farmer farmer = Game1.player;
            PlayerRunState state = GetState(farmer);

            GamePadState currentState = GamePad.GetState(PlayerIndex.One);

            bool keyRun = Helper.Input.IsDown(_config.Key);
            bool isMovingThumb = false;
            bool runPressed = false;

            if (_config.EnableGamePad)
            {
                isMovingThumb =
                    currentState.ThumbSticks.Left.X > 0.1f || currentState.ThumbSticks.Left.X < -0.1f ||
                    currentState.ThumbSticks.Left.Y > 0.1f || currentState.ThumbSticks.Left.Y < -0.1f;
                runPressed = currentState.IsConnected && currentState.Buttons.LeftStick == ButtonState.Pressed;
            }

            bool isMoving = farmer.isMoving();

            if (state.IsRunning && isMovingThumb || runPressed)
            {
                state.WasRunningPad = true;
            }
            else
            {
                state.WasRunningPad = false;
                state.IsRunning = false;
            }

            if ((state.WasRunningPad || keyRun) && isMoving && farmer.Stamina > state.FinalMinStamina)
            {
                Run(farmer, state, isMoving);
                state.StateChanged = true;
            }
            else if (!state.IsRunning && state.StateChanged)
            {
                farmer.speed = state.BaseSpeed;
                state.StateChanged = false;
            }
        }

        private void Run(Farmer farmer, PlayerRunState state, bool isMoving)
        {
            float finalSpeed = CalculateFinalSpeed(farmer, state);

            if (finalSpeed > 0)
            {
                farmer.speed = (int)finalSpeed;
                state.IsRunning = true;

                if (isMoving && state.LastTickLose + _config.StaminaTick < Game1.ticks)
                {
                    if (_config.StaminaLose > 0)
                        farmer.Stamina -= _config.StaminaLose;

                    state.LastTickLose = Game1.ticks;
                }
            }
            else
            {
                state.IsRunning = false;
            }
        }

        private void DayStart(object? sender, DayStartedEventArgs e)
        {
            // Recompute stamina thresholds per farmer (for split-screen).
            foreach (var farmer in Game1.getAllFarmers())
            {
                var state = GetState(farmer);
                state.BaseStamina = farmer.MaxStamina;
                state.FinalMinStamina = (state.BaseStamina * _config.StaminaLimit) / 100;

                // Reset speed baseline at the start of day to avoid drift.
                state.BaseSpeed = farmer.speed;
                state.StateChanged = false;
                state.IsRunning = false;
                state.WasRunningPad = false;
                state.LastTickLose = 0;
            }
        }

        private void DayEnd(object? sender, DayEndingEventArgs e)
        {
            // no-op
        }

        private float CalculateFinalSpeed(Farmer farmer, PlayerRunState state)
        {
            // Capture a stable baseline speed when transitioning into running.
            if (!state.StateChanged)
            {
                // Include buffs in baseline so we can restore correctly and avoid stacking.
                state.BaseSpeed = farmer.speed + GetSpeedPlayerBuffs(farmer);
            }
            
            float finalAdded = AddPercent(5, _config.RunningSpeedGain) - 5;
            if (finalAdded < 0)
                finalAdded *= -1;

            float finalSpeed = finalAdded + state.BaseSpeed;
            
            // Case when auto run is disabled.
            if (!Game1.options.autoRun && !farmer.running && _config.AutoRunOffCompensation > 0)
            {
                finalSpeed += _config.AutoRunOffCompensation;
            }
            
            return finalSpeed > 0 ? finalSpeed : 0;
        }

        private float AddPercent(float baseNumber, int percent)
        {
            return baseNumber + ((percent / 100f) * baseNumber);
        }

        private float RemovePercent(float baseNumber, int percent)
        {
            return baseNumber - ((percent / 100f) * baseNumber);
        }

        private int GetSpeedPlayerBuffs(Farmer farmer)
        {
            float result = 0;

            if (farmer.buffs.AppliedBuffs.Count > 0)
            {
                foreach (KeyValuePair<string, Buff> buff in farmer.buffs.AppliedBuffs)
                {
                    if (buff.Value.effects.Speed.Value != 0)
                        result += buff.Value.effects.Speed.Value;
                }
            }

            return (int)result;
        }
    }
}