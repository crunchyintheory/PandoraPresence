# BorderlandsDiscordRP

Based on [FromDarkHell's BorderlandsDiscordRP](https://github.com/FromDarkHell/BorderlandsDiscordRP)

IMPROVED with
* Level specific images!
* Bug fixes!
* Commander Lilith Support!
* ...and more

(Below is the original setup guide, modified to fit the new setup process).

Description
-----------
This is a program coded in C# that uses [Discord](http://discordapp.com/)'s [Rich Presence](https://discordapp.com/rich-presence) feature to display Borderlands 2 / Borderlands: The Pre-Sequel information on your discord profile while the game is running.
<br>
Information like:
* Current mission selected
* Current character being played
* Current level of said character being played
* How many players in the lobby with you (including you)
* How long you've been playing
* What map you're in
* What game you're playing

![Borderlands 2 Discord RP](https://puu.sh/CiCrw/4cbe503c94.png)
![Borderlands TPS Discord RP](https://puu.sh/CiCvX/cd3260c23f.png)
<br>
Programs like this is possible due to [c0dycode's CommandInjector](https://github.com/c0dycode/BL-CommandInjector), [mopioid's BLIO library](https://github.com/mopioid/BLIO), Discord for making Discord, [Lachee's discord-rpc-csharp library](https://github.com/Lachee/discord-rpc-csharp), and FromDarkHell for uh making this.


Installation
-----------
1. Download [BorderlandsDiscordRP](https://github.com/crunchyintheory/BorderlandsDiscordRP/releases) here.
2. Installing CommandInjector
	  1. Quit the game if running.
	  2. [Download the latest version of `ddraw.dll`/PluginLoader).](https://github.com/c0dycode/BorderlandsPluginLoader/releases)
	  3. Locate the `Win32` folder within your game's `Binaries` folder. ![Win32 folder](https://i.imgur.com/t6OI06l.png)
	  4. Copy `ddraw.dll` **and `Launcher.exe`** to the `Win32` folder. ![ddraw.dll](https://i.imgur.com/FHfiSqg.png) ![Launcher.exe](https://i.imgur.com/ydzBruZ.png)
	  5. In the `Win32` folder, create a folder called `Plugins`. ![Plugins folder](https://i.imgur.com/CDdoKDs.png)
	  6. [Download the latest version of CommandInjector.](https://github.com/c0dycode/BL-CommandInjector/blob/master/CommandInjector.zip)
	  7. Open the `CommandInjector.zip` file to view its contents. ![CommandInjector.zip](https://i.imgur.com/xjTdT70.png)
	  8. Copy `CommandInjector-UHD.dll` (If you're installing it for BL2) or `CommandInjectorTPS-UHD.dll` (If you're installing it for TPS) to the `Plugins` folder you created. ![CommandInjector.dll](https://i.imgur.com/mMHraRu.png)
3. Run the game, and Borderlands RP will automatically start and stop with the game.