# PandoraPresence

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
* Current level of your character
* Current map
* Number of players in the lobby
* How long you've been playing

![Borderlands 2](https://github.com/crunchyintheory/pandorapresence/blob/master/docs/bl2.png)
![Borderlands: The Pre-Sequel](https://github.com/crunchyintheory/pandorapresence/blob/master/docs/bltps.png)
<br>

Installation
-----------
1. Download [PandoraPresence](https://github.com/crunchyintheory/PandoraPresence/releases) here.
2. Installing CommandInjector
	  1. Quit the game if running.
	  2. [Download the latest version of `ddraw.dll`/PluginLoader).](https://github.com/c0dycode/BorderlandsPluginLoader/releases)
	  3. Locate the `Win32` folder within your game's `Binaries` folder. ![Win32 folder](https://i.imgur.com/oIMd2Qa.png)
	  4. Copy `ddraw.dll` **and `Launcher.exe`** to the `Win32` folder. ![ddraw.dll](https://i.imgur.com/inyMgSv.png) ![Launcher.exe](https://i.imgur.com/UU9ziIw.png)
	  5. In the `Win32` folder, create a folder called `Plugins`. ![Plugins folder](https://i.imgur.com/2DfkqSo.png)
	  6. [Download the latest version of CommandInjector.](https://github.com/c0dycode/BL-CommandInjector/blob/master/CommandInjector.zip)
	  7. Open the `CommandInjector.zip` file to view its contents.
	  8. Copy `CommandInjector-UHD.dll` (If you're installing it for BL2) or `CommandInjectorTPS-UHD.dll` (If you're installing it for TPS) to the `Plugins` folder you created. ![CommandInjector.dll](https://i.imgur.com/phk3YBI.png)
3. Run the game, and PandoraPresence will automatically start and stop with the game.
