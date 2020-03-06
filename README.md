# Running key for Stardew Valley

First, sorry for my english, I try to improve my english skills but it take times ...


This mod add a configurable key enable running (or sprint, technicaly the base game is running but it still a bit slow...)
The default key is left shift, but the game use it to walk. You can change it, or like I do change the walk key in game option (walk key is called "Run" because of base running option)

Running consume stamina over time, but you can configure the amount of stamina losed while running.
Run speed can be configured too.

Configuration options :
```
{
  "RunningSpeedGain": 30,       <- Base percent of speed added while running
  "Key": "LeftShift",           <- Key used for run
  "StaminaLose": 1,             <- How many stamina did you lose while running
  "StaminaTick": 15,            <- Update stamina per tiks (it's like a frame rate), you will lose StaminaLose amount every StaminaTick of running
  "StaminaLimit": 50,           <- Minimum percent of stamina you need to run, if current stamina is under this amount, you can't run.
  "RainReductionPercent": 20,   <- How many percent the Base percent have to be reduced when it's rainning
  "SnowReductionPercent": 30,   <- Same, but with snow
  "HoeReductionPercent": 10,    <- Same, but on Hoe dirt floor
  "FlooredGainPercent": 10      <- How many percent the Base percent have to be incrased when player is on a floored ground
}
```
