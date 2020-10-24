using System;
using System.Timers;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BorderlandsDiscordRP.Properties;
using DiscordRPC;
using DiscordRPC.Logging;
using static BLIO;
using System.Text.RegularExpressions;
using System.IO;

namespace BorderlandsDiscordRP
{
    class Integration
    {

        #region Variables

        #region Discord Specific Things
        // The discord client
        public static DiscordRpcClient client;

        // The pipe discord is located on. If set to -1, the client will scan for the first available pipe.
        private static int discordPipe = -1;

        // The ID of the client using Discord RP.
        private static string bl2ClientID = Settings.Default.bl2ClientID;
        private static string tpsClientID = Settings.Default.tpsClientId;

        // The double (in milliseconds) for how much we update
        private static double timeToUpdate = Settings.Default.timeUpdate;

        // The level of logging to use
        private static LogLevel logLevel = LogLevel.Warning;

        // The timer we use to update our discord rpc
        private static System.Timers.Timer timer = new System.Timers.Timer(15000);

        // If we're connected to discord
        private static bool connected = false;

        #endregion

        #region Game Specific Stuff
        // The boolean of the game
        private static bool bl2 = true;
        private static bool tps = false;

        private static string lastKnownMap = "The Borderlands";
        private static string lastKnownMission = "In Menu";
        private static string lastKnownChar = "Unknown";
        private static int lastKnownLevel = 0;
        #endregion

        #endregion

        #region Constructors
        public static int Create()
        {
            string dir = Directory.GetCurrentDirectory();
            if(!dir.EndsWith("Binaries\\Win32"))
            {
                dir = Path.Combine(dir, "Binaries", "Win32");
            }

            string game;

            if (File.Exists(Path.Combine(dir, "Borderlands2.exe")))
            {
                game = "Borderlands2.exe";
            }
            else if (File.Exists(Path.Combine(dir, "BorderlandsPreSequel.exe")))
            {
                game = "BorderlandsPreSequel.exe";
            }
            else
            {
                return 1;
            }

            var process = Process.Start(Path.Combine(dir, game));
            setupClient();
            process.WaitForExit();
            client.ClearPresence();

            return 0;
        }
        #endregion

        #region Setup 
        private static void setupClient()
        {
            connected = false;

            string clientID = tps ? tpsClientID : bl2ClientID;

            // Create a new client
            client = new DiscordRpcClient(clientID);

            // Create the logger
            client.Logger = new ConsoleLogger() { Level = logLevel, Coloured = true };

            client.OnReady += (sender, msg) =>
            {
                connected = true;
            };

            timer.Elapsed += timerHandler;
            timer.AutoReset = true;
            timer.Interval = 2500;
            timer.Start();


            // Connect to discord
            client.Initialize();

            Thread childThread = new Thread(CallToChildThread);

            childThread.Start();
            while (childThread.IsAlive)
                continue;

        }

        public static void CallToChildThread()
        {
            while (!connected)
            {
                Thread.Sleep(100);
            }
        }
        #endregion

        #region Handlers
        static void timerHandler(object sender, ElapsedEventArgs args)
        {
            // Change our timer interval just in case.
            timer.Interval = timeToUpdate;

            bool lastKnownBl2 = bl2;
            bool lastKnownTPS = tps;

            Process[] bl2Array = Process.GetProcesses().Where(p => p.ProcessName.Contains("Borderlands2")).ToArray();
            Process[] tpsArray = Process.GetProcesses().Where(p => p.ProcessName.Contains("BorderlandsPreSequel")).ToArray();
            bl2 = bl2Array.Length > 0;
            tps = tpsArray.Length > 0;
            var launchDate = new DateTime();
            if (bl2)
                launchDate = bl2Array.FirstOrDefault().StartTime;
            else if (tps)
                launchDate = tpsArray.FirstOrDefault().StartTime;

            //client.Invoke();


            if (!bl2 && !tps)
            {
                client.ClearPresence();
                return;
            }

            if (lastKnownBl2 != bl2 || lastKnownTPS != tps)
            {
                setupClient();
            }

            int level = getCurrentLevel();
            string map = getCurrentMap();

            RichPresence presence = new RichPresence()
            {
                Details = getCurrentMission(),
                State = level > 0 ? string.Format("{1} {0} ({2} of 4)", level, getCurrentClass(), getPlayersInLobby()) : "",
                Assets = new Assets()
                {
                    LargeImageKey = Regex.Replace(map, @"[ '.\-,/]", "").ToLower(),
                    LargeImageText = map,
                    SmallImageKey = "default",
                    SmallImageText = bl2 ? "Borderlands 2" : "Borderlands: The Pre-Sequel"
                },
                Timestamps = new Timestamps(launchDate.ToUniversalTime())
            };

            client.SetPresence(presence);
        }
        #endregion

        #region Data Fetchers
        private static int getPlayersInLobby()
        {
            int characters = 1;
            IReadOnlyList<BLObject> players = GetAll("WillowPlayerPawn");
            players.Distinct().Where(o => o.Name.Contains("Loader."));
            int count = players.Distinct().Where(o => o.Name.Contains("Loader.")).Count();
            characters = count != 0 ? count : 1;
            return characters;
        }

        private static string getCurrentMap()
        {
            string mapName = "Unknown";
            IReadOnlyDictionary<BLObject, object> players = GetAll("WorldInfo", "NavigationPointList");
            KeyValuePair<BLObject, object>[] dict = players.Distinct()
                .Where(p => p.Key.Name.Contains("Loader.")).ToArray();

            string pylon = "";
            foreach (KeyValuePair<BLObject, object> kvp in dict)
            {
                pylon = ((BLObject)kvp.Value)?.Name;
            }
            mapName = mapFileToActualMap(pylon);
            if (mapName.Trim() == "" || pylon.Contains("Fake"))
                mapName = lastKnownMap;
            else
                lastKnownMap = mapName;

            return mapName;
        }

        private static string getCurrentMission()
        {
            string mission = "Unknown";

            IReadOnlyDictionary<BLObject, object> dict = GetAll("HUDWidget_Missions", "CachedMissionName");
            KeyValuePair<BLObject, object>[] arr = dict.Where(m => m.Key.Name.Contains("Transient")).ToArray();
            mission = dict.FirstOrDefault().Value?.ToString();

            if (mission == null || mission.Trim() == "")
                mission = "In Menu";
            else
                lastKnownMission = mission;

            return mission;
        }

        private static string getCurrentClass()
        {
            IReadOnlyDictionary<BLObject, object> dict = GetAll("WillowPlayerController", "CharacterClass");
            KeyValuePair<BLObject, object>[] arr = dict.Where(m => m.Key.Name.Contains("Loader")).ToArray();
            string characterClass = ((BLObject)arr.FirstOrDefault().Value)?.Name;
            if (characterClass == null || characterClass.Trim() == "")
                characterClass = lastKnownChar;
            else
                lastKnownChar = characterClass;

            return classToCharacterName(characterClass);
        }

        private static int getCurrentLevel()
        {

            IReadOnlyDictionary<BLObject, object> dict = GetAll("WillowHUDGFxMovie", "CachedLevel");
            KeyValuePair<BLObject, object>[] arr = dict.Where(m => m.Key.Name.Contains("Transient")).ToArray();
            string lev = dict.FirstOrDefault().Value?.ToString();

            if (int.TryParse(lev, out int level) && level != 0)
                lastKnownLevel = level;
            else
                level = lastKnownLevel;
            return level;

        }
        #endregion

        #region Helpers
        private static string classToCharacterName(string charClass)
        {
            if (bl2)
            {
                if (charClass.Contains("Assassin"))
                    return "Zer0";
                if (charClass.Contains("Lilac_PlayerClass"))
                    return "Krieg";
                if (charClass.Contains("Mercenary"))
                    return "Salvador";
                if (charClass.Contains("Siren"))
                    return "Maya";
                if (charClass.Contains("Soldier"))
                    return "Axton";
                if (charClass.Contains("Tulip_Mechromancer"))
                    return "Gaige";
            }

            if (tps)
            {
                if (charClass.Contains("Crocus"))
                    return "Aurelia";
                if (charClass.Contains("Enforcer"))
                    return "Wilhelm";
                if (charClass.Contains("Gladiator"))
                    return "Athena";
                if (charClass.Contains("Lawbringer"))
                    return "Nisha";
                if (charClass.Contains("Prototype"))
                    return "Claptrap";
                if (charClass.Contains("Quince_Doppel"))
                    return "Jack";
            }

            return "Unknown";
        }

        private static string mapFileToActualMap(string map)
        {
            if (map == null)
                return "Unknown";

            map = map.ToLower(CultureInfo.InvariantCulture);
            if (bl2)
            {
                #region DLC

                #region Scarlett
                if (map.Contains("orchid"))
                {
                    if (map.Contains("caves"))
                        return "Hayter's Folly";
                    if (map.Contains("wormbelly"))
                        return "Leviathan's Lair";
                    if (map.Contains("spire"))
                        return "Magnys Lighthouse";
                    if (map.Contains("oasistown"))
                        return "Oasis";
                    if (map.Contains("shipgraveyard"))
                        return "The Rustyards";
                    if (map.Contains("refinery"))
                        return "Washburne Refinery";
                    if (map.Contains("saltflats"))
                        return "Wurmwater";
                }
                #endregion
                #region Torgue
                else if (map.Contains("iris"))
                {
                    if (map.Contains("dl1"))
                        return "Torgue Arena";
                    if (map.Contains("moxxi"))
                        return "Badass Crater Bar";
                    if (map.Contains("hub") && !map.Contains("hub2"))
                        return "Badass Crater of Badassitude";
                    if (map.Contains("dl2") && !map.Contains("interior"))
                        return "The Beatdown";
                    if (map.Contains("dl3"))
                        return "The Forge";
                    if (map.Contains("interior"))
                        return @"Pyro Pete's Bar";
                    if (map.Contains("hub2"))
                        return "Southern Raceway";
                }
                #endregion
                else if (map.Contains("sage"))
                {
                    if (map.Contains("powerstation"))
                        return "Ardorton Station";
                    if (map.Contains("cliffs"))
                        return "Candlerakk's Crag";
                    if (map.Contains("hyperionship"))
                        return "H.S.S Terminus";
                    if (map.Contains("underground"))
                        return "Hunter's Grotto";
                    if (map.Contains("rockforest"))
                        return @"Scylla's Grove";
                }
                #region Tina
                if (map.Contains("dark_forest"))
                    return "Dark Forest";
                if (map.Contains("castlekeep"))
                    return "Dragon Keep";
                if (map.Contains("village"))
                    return "Flamerock Refuge";
                if (map.Contains("castleexterior"))
                    return @"Hatred's Shadow";
                if (map.Contains("dead_forest"))
                    return "Immortal Woods";
                if (map.Contains("dungeon") && !map.Contains("raid"))
                    return "Lair of Infinite Agony";
                if (map.Contains("raid"))
                    return "The Winged Storm";
                if (map.Contains("mines"))
                    return "Mines of Avarice";
                if (map.Contains("templeslaughter"))
                    return @"Murderlin's Temple";
                if (map.Contains("docks"))
                    return "Unassuming Docks";
                #endregion Tina
                #region Headhunters
                if (map.Contains("hunger"))
                    return "Gluttony Gulch";
                if (map.Contains("pumpkin"))
                    return "Hallowed Hollow";
                if (map.Contains("xmas"))
                    return @"Marcus's Mercenary Shop";
                if (map.Contains("testingzone"))
                    return "Digistruct Peak";
                if (map.Contains("distillery"))
                    return "Rotgut Distillery";
                if (map.Contains("easter"))
                    return "Wam Bam Island";
                #endregion
                #region Lilith
                if (map.Contains("sanctintro"))
                    return "Sanctuary";
                if (map.Contains("backburner"))
                    return "The Backburner";
                if (map.Contains("olddust"))
                    return "Dahl Abandon";
                if (map.Contains("sandworm") || map.Contains("writhing"))
                    return "The Burrows";
                if (map.Contains("helios_hangar"))
                    return "Helios Fallen";
                if (map.Contains("researchcenter"))
                    return "Mt. Scarab Research Center";
                #endregion
                #endregion

                if (map.Contains("menumap"))
                    return "In Menu";
                if (map.Contains("stockade"))
                    return "Arid Nexus - Badlands";
                if (map.Contains("fyrestone"))
                    return "Arid Nexus - Boneyard";
                if (map.Contains("damtop"))
                    return "Bloodshot Ramparts";
                if (map.Contains("dam") && !map.Contains("damtop"))
                    return "Bloodshot Stronghold";
                if (map.Contains("frost"))
                    return "Three Horns Valley";
                if (map.Contains("boss_cliffs"))
                    return "The Bunker";
                if (map.Contains("caverns"))
                    return "Caustic Caverns";
                if (map.Contains("vogchamber"))
                    return "Control Core Angel";
                if (map.Contains("interlude"))
                    return "The Dust";
                if (map.Contains("tundratrain"))
                    return "End of the Line";
                if (map.Contains("ash"))
                    return "Eridium Blight";
                if (map.Contains("banditslaughter"))
                    return "Fink's Slaughterhouse";
                if (map.Contains("fridge"))
                    return "The Fridge";
                if (map.Contains("hypinterlude"))
                    return "Friendship Gulag";
                if (map.Contains("icecanyon"))
                    return "Frostburn Canyon";
                if (map.Contains("finalbossascent"))
                    return @"Hero's Pass";
                if (map.Contains("outwash"))
                    return "Highlands Outwash";
                if (map.Contains("grass") && !map.Contains("lynchwood"))
                    return "Highlands";
                if (map.Contains("luckys"))
                    return "Holy Spirits";
                if (map.Contains("grass"))
                    return "Lynchwood";
                if (map.Contains("hyperioncity"))
                    return "Opportunity";
                if (map.Contains("robotslaughter"))
                    return "Ore Chasm";
                if (map.Contains("sanctuary") && !map.Contains("hole"))
                    return "Sanctuary";
                if (map.Contains("sanctuary_hole"))
                    return "Sanctuary Hole";
                if (map.Contains("craterlake"))
                    return "Sawtooth Cauldron";
                if (map.Contains("cove"))
                    return "Southern Shelf - Bay";
                if (map.Contains("southernshelf"))
                    return "Southern Shelf";
                if (map.Contains("Southpaw Factory"))
                    return "Southpaw Steam + Power";
                if (map.Contains("thresherraid"))
                    return "Terramorphous Peak";
                if (map.Contains("ice"))
                    return "Three Horns Divide";
                if (map.Contains("tundraexpress"))
                    return "Tundra Express";
                if (map.Contains("boss_volcano"))
                    return "Vault of the Warrior";
                if (map.Contains("pandorapark") || map.Contains("creatureslaughter"))
                    return "Wildlife Exploitation Preserve";
                if (map.Contains("glacial"))
                    return "Windshear Waste";
            }
            else if (tps)
            {
                if (map.Contains("ma_"))
                {
                    if (map.Contains("leftcluster"))
                        return "Cluster 00773 P4ND0R4";
                    if (map.Contains("rightcluster"))
                        return "Cluster 99002 0V3RL00K";
                    if (map.Contains("subboss"))
                        return "Cortex";
                    if (map.Contains("deck13") || map.Contains("finalboss"))
                        return "Deck 13 1/2";
                    if (map.Contains("motherboard"))
                        return "Motherless Board";
                    if (map.Contains("Nexus"))
                        return "The Nexus";
                    if (map.Contains("subconscious"))
                        return "Subconscious";
                }

                if (map.Contains("spaceport"))
                    return "Concordia";
                if (map.Contains("comfacility"))
                    return "Crisis Scar";
                if (map.Contains("innercore"))
                    return "Eleseer";
                if (map.Contains("laserboss"))
                    return "Eye of Helios";
                if (map.Contains("moonshotintro"))
                    return "Helios Station";
                if (map.Contains("centralterminal"))
                    return "Hyperion Hub of Heroism";
                if (map.Contains("jacksoffice"))
                    return @"Jack's Office";
                if (map.Contains("laser"))
                    return "Lunar Launching Station";
                if (map.Contains("meriff"))
                    return @"Meriff's Office";
                if (map.Contains("digsite_rk5"))
                    return "Outfall Pumping Station";
                if (map.Contains("outlands_combat2"))
                    return "Outlands Canyon";
                if (map.Contains("outlands"))
                    return "Outlands Spur";
                if (map.Contains("wreck"))
                    return @"Pity's Fall";
                if (map.Contains("deadsurface"))
                    return "Regolith Range";
                if (map.Contains("randdfacility"))
                    return "Research and Development";
                if (map.Contains("moonsurface"))
                    return @"Serenity's Waste";
                if (map.Contains("stantonsliver"))
                    return @"Stanton's Liver";
                if (map.Contains("sublevel13"))
                    return "Sub-Level 13";
                if (map.Contains("dahlfactory") && !map.Contains("boss"))
                    return "Titan Industrial Facility";
                if (map.Contains("dahlfactory_boss"))
                    return "Titan Robot Production Plant";
                if (map.Contains("moon"))
                    return "Triton Flats";
                if (map.Contains("access"))
                    return @"Tycho's Ribs";
                if (map.Contains("innerhull"))
                    return "Veins of Helios";
                if (map.Contains("digsite"))
                    return "Vorago Solitude";

                if (map.Contains("moonslaughter"))
                    return "Abandoned Training Facility";
                if (map.Contains("eridian_slaughter"))
                    return "The Holodome";
            }


            return "Unknown";
        }
        #endregion
    }
}
