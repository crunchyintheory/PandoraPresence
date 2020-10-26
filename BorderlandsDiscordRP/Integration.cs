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

            //Testing Code
            //ManualResetEvent Wait = new ManualResetEvent(false);
            //Wait.WaitOne();

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
                if (map.StartsWith("orchid"))
                {
                    map = map.Substring(7);
                    if (map.StartsWith("caves"))
                        return "Hayter's Folly";
                    if (map.StartsWith("wormbelly"))
                        return "Leviathan's Lair";
                    if (map.StartsWith("spire"))
                        return "Magnys Lighthouse";
                    if (map.StartsWith("oasistown"))
                        return "Oasis";
                    if (map.StartsWith("shipgraveyard"))
                        return "The Rustyards";
                    if (map.StartsWith("refinery"))
                        return "Washburne Refinery";
                    if (map.StartsWith("saltflats"))
                        return "Wurmwater";
                }
                #endregion
                #region Torgue
                else if (map.StartsWith("iris"))
                {
                    map = map.Substring(5);
                    if (map.StartsWith("dl1"))
                        return "Torgue Arena";
                    if (map.StartsWith("moxxi"))
                        return "Badass Crater Bar";
                    if (map.StartsWith("hub")) {
                        if (map[3] == '2')
                            return "Southern Raceway";
                        return "Badass Crater of Badassitude";
                    }
                    if (map.StartsWith("dl2")) {
                        if (map.Contains("interior"))
                            return @"Pyro Pete's Bar";
                        return "The Beatdown";
                    }
                    if (map.StartsWith("dl3"))
                        return "The Forge";
                }
                #endregion
                #region Hammerlock
                else if (map.StartsWith("sage"))
                {
                    map = map.Substring(5);
                    if (map.StartsWith("powerstation"))
                        return "Ardorton Station";
                    if (map.StartsWith("cliffs"))
                        return "Candlerakk's Crag";
                    if (map.StartsWith("hyperionship"))
                        return "H.S.S Terminus";
                    if (map.StartsWith("underground"))
                        return "Hunter's Grotto";
                    if (map.StartsWith("rockforest"))
                        return @"Scylla's Grove";
                }
                #endregion
                #region Tina
                if (map.StartsWith("castleexterior"))
                    return @"Hatred's Shadow";
                if (map.StartsWith("castlekeep"))
                    return "Dragon Keep";
                if (map.StartsWith("dark_forest"))
                    return "Dark Forest";
                if (map.StartsWith("dead_forest"))
                    return "Immortal Woods";
                if (map.StartsWith("docks"))
                    return "Unassuming Docks";
                if (map.StartsWith("dungeon"))
                {
                    if(map.Contains("raid"))
                        return "The Winged Storm";
                    
                    return "Lair of Infinite Agony";
                }
                if (map.StartsWith("mines"))
                    return "Mines of Avarice";
                if (map.StartsWith("templeslaughter"))
                    return @"Murderlin's Temple";
                if (map.StartsWith("village"))
                    return "Flamerock Refuge";
                #endregion Tina
                #region Headhunters
                if (map.StartsWith("hunger"))
                    return "Gluttony Gulch";
                if (map.StartsWith("pumpkin"))
                    return "Hallowed Hollow";
                if (map.StartsWith("xmas"))
                    return @"Marcus's Mercenary Shop";
                if (map.StartsWith("testingzone"))
                    return "Digistruct Peak";
                if (map.StartsWith("distillery"))
                    return "Rotgut Distillery";
                if (map.StartsWith("easter"))
                    return "Wam Bam Island";
                #endregion
                #region Lilith
                if (map.StartsWith("sanctintro"))
                    return "Sanctuary";
                if (map.StartsWith("backburner"))
                    return "The Backburner";
                if (map.StartsWith("olddust"))
                    return "Dahl Abandon";
                if (map.StartsWith("sandworm") || map.StartsWith("writhing"))
                    return "The Burrows";
                if (map.StartsWith("helios_hangar"))
                    return "Helios Fallen";
                if (map.StartsWith("researchcenter"))
                    return "Mt. Scarab Research Center";
                #endregion
                #endregion

                if (map.StartsWith("menumap"))
                    return "In Menu";
                if (map.StartsWith("stockade"))
                    return "Arid Nexus - Badlands";
                if (map.StartsWith("fyrestone"))
                    return "Arid Nexus - Boneyard";
                if (map.StartsWith("dam"))
                {
                    if (map.StartsWith("damtop"))
                        return "Bloodshot Ramparts";
                    return "Bloodshot Stronghold";
                }
                if (map.StartsWith("frost"))
                    return "Three Horns Valley";
                if (map.StartsWith("boss_cliffs"))
                    return "The Bunker";
                if (map.StartsWith("caverns"))
                    return "Caustic Caverns";
                if (map.StartsWith("vogchamber"))
                    return "Control Core Angel";
                if (map.StartsWith("interlude"))
                    return "The Dust";
                if (map.StartsWith("tundratrain"))
                    return "End of the Line";
                if (map.StartsWith("ash"))
                    return "Eridium Blight";
                if (map.StartsWith("banditslaughter"))
                    return "Fink's Slaughterhouse";
                if (map.StartsWith("fridge"))
                    return "The Fridge";
                if (map.StartsWith("hypinterlude"))
                    return "Friendship Gulag";
                if (map.StartsWith("icecanyon"))
                    return "Frostburn Canyon";
                if (map.StartsWith("finalbossascent"))
                    return @"Hero's Pass";
                if (map.StartsWith("outwash"))
                    return "Highlands Outwash";
                if (map.StartsWith("grass"))
                {
                    if (map.Contains("lynchwood"))
                        return "Lynchwood";
                    if (map.Contains("cliffs"))
                        return "Thousand Cuts";
                    return "Highlands";
                }
                if (map.StartsWith("luckys"))
                    return "Holy Spirits";
                if (map.StartsWith("hyperioncity"))
                    return "Opportunity";
                if (map.StartsWith("robotslaughter"))
                    return "Ore Chasm";
                if (map.StartsWith("sanctuary") && !map.Contains("hole"))
                    return "Sanctuary";
                if (map.StartsWith("sanctuary_hole"))
                    return "Sanctuary Hole";
                if (map.StartsWith("craterlake"))
                    return "Sawtooth Cauldron";
                if (map.StartsWith("cove_"))
                    return "Southern Shelf - Bay";
                if (map.StartsWith("southernshelf"))
                    return "Southern Shelf";
                if (map.StartsWith("southpawfactory"))
                    return "Southpaw Steam + Power";
                if (map.StartsWith("thresherraid"))
                    return "Terramorphous Peak";
                if (map.StartsWith("ice"))
                    return "Three Horns Divide";
                if (map.StartsWith("tundraexpress"))
                    return "Tundra Express";
                if (map.StartsWith("boss_volcano"))
                    return "Vault of the Warrior";
                if (map.StartsWith("pandorapark") || map.StartsWith("creatureslaughter"))
                    return "Wildlife Exploitation Preserve";
                if (map.StartsWith("glacial"))
                    return "Windshear Waste";
            }
            else if (tps)
            {
                if (map.StartsWith("ma_"))
                {
                    map = map.Substring(3);
                    if (map.StartsWith("leftcluster"))
                        return "Cluster 00773 P4ND0R4";
                    if (map.StartsWith("rightcluster"))
                        return "Cluster 99002 0V3RL00K";
                    if (map.StartsWith("subboss"))
                        return "Cortex";
                    if (map.StartsWith("deck13") || map.StartsWith("finalboss"))
                        return "Deck 13 1/2";
                    if (map.StartsWith("motherboard"))
                        return "Motherless Board";
                    if (map.StartsWith("Nexus"))
                        return "The Nexus";
                    if (map.StartsWith("subconscious"))
                        return "Subconscious";
                }

                if (map.StartsWith("spaceport"))
                    return "Concordia";
                if (map.StartsWith("comfacility"))
                    return "Crisis Scar";
                if (map.StartsWith("innercore"))
                    return "Eleseer";
                if (map.StartsWith("laserboss"))
                    return "Eye of Helios";
                if (map.StartsWith("moonshotintro"))
                    return "Helios Station";
                if (map.StartsWith("centralterminal"))
                    return "Hyperion Hub of Heroism";
                if (map.StartsWith("jacksoffice"))
                    return @"Jack's Office";
                if (map.StartsWith("laser"))
                    return "Lunar Launching Station";
                if (map.StartsWith("meriff"))
                    return @"Meriff's Office";
                if (map.StartsWith("digsite_rk5"))
                    return "Outfall Pumping Station";
                if (map.StartsWith("outlands_combat2"))
                    return "Outlands Canyon";
                if (map.StartsWith("outlands"))
                    return "Outlands Spur";
                if (map.StartsWith("wreck"))
                    return @"Pity's Fall";
                if (map.StartsWith("deadsurface"))
                    return "Regolith Range";
                if (map.StartsWith("randdfacility"))
                    return "Research and Development";
                if (map.StartsWith("moonsurface"))
                    return @"Serenity's Waste";
                if (map.StartsWith("stantonsliver"))
                    return @"Stanton's Liver";
                if (map.StartsWith("sublevel13"))
                    return "Sub-Level 13";
                if (map.StartsWith("dahlfactory"))
                {
                    if (map.Contains("boss"))
                        return "Titan Robot Production Plant";
                    return "Titan Industrial Facility";
                }
                if (map.StartsWith("moon"))
                    return "Triton Flats";
                if (map.StartsWith("access"))
                    return @"Tycho's Ribs";
                if (map.StartsWith("innerhull"))
                    return "Veins of Helios";
                if (map.StartsWith("digsite"))
                    return "Vorago Solitude";

                if (map.StartsWith("moonslaughter"))
                    return "Abandoned Training Facility";
                if (map.StartsWith("eridian_slaughter"))
                    return "The Holodome";
            }


            return "Unknown";
        }
        #endregion
    }
}
