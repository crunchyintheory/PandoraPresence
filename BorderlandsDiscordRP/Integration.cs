using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using BorderlandsDiscordRP.Properties;
using DiscordRPC;
using DiscordRPC.Logging;
using static BLIO;
using Timer = System.Timers.Timer;

namespace BorderlandsDiscordRP
{
    internal static class Integration
    {

        #region Variables

        #region Discord Specific Things
        // The discord client
        private static DiscordRpcClient _client = null!;

        // The pipe discord is located on. If set to -1, the client will scan for the first available pipe.
#pragma warning disable CS0414 // Field is assigned but its value is never used
        public static int DiscordPipe = -1;
#pragma warning restore CS0414 // Field is assigned but its value is never used

        // The ID of the client using Discord RP.
        private static readonly string Bl2ClientId = Settings.Default.bl2ClientID;
        private static readonly string TpsClientId = Settings.Default.tpsClientId;

        // The double (in milliseconds) for how much we update
        private static readonly double TimeToUpdate = Settings.Default.timeUpdate;

        // The level of logging to use
        private const LogLevel LogLevel = DiscordRPC.Logging.LogLevel.Warning;

        // The timer we use to update our discord rpc
        private static readonly Timer Timer = new Timer(15000);

        // If we're connected to discord
        private static bool _connected;

        #endregion

        #region Game Specific Stuff
        // The boolean value of the game
        private static bool _bl2 = true;
        private static bool _tps;

        private static string _lastKnownMap = "The Borderlands";
        private static string _lastKnownChar = "Unknown";
        private static int _lastKnownLevel;
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

            string[] args = Environment.GetCommandLineArgs();
            string argLine = string.Join(" ", args.Skip(1));

            Process process = Process.Start(Path.Combine(dir, game), argLine) ?? throw new InvalidOperationException();
            Thread child = Integration.SetupClient();
            process.WaitForExit();
            Integration._client.ClearPresence();
            child.Abort();

            //Testing Code
            //ManualResetEvent Wait = new ManualResetEvent(false);
            //Wait.WaitOne();

            return 0;
        }
        #endregion

        #region Setup 
        private static Thread SetupClient()
        {
            Integration._connected = false;

            string clientId = Integration._tps ? Integration.TpsClientId : Integration.Bl2ClientId;

            // Create a new client
            Integration._client = new DiscordRpcClient(clientId);

            // Create the logger
            Integration._client.Logger = new ConsoleLogger { Level = Integration.LogLevel, Coloured = true };

            Integration._client.OnReady += (_, _) =>
            {
                Integration._connected = true;
            };

            Integration.Timer.Elapsed += Integration.TimerHandler;
            Integration.Timer.AutoReset = true;
            Integration.Timer.Interval = 2500;
            Integration.Timer.Start();


            // Connect to discord
            Integration._client.Initialize();

            Thread childThread = new Thread(Integration.CallToChildThread);

            childThread.Start();
            while (childThread.IsAlive)
            {
            }

            return childThread;

        }

        private static void CallToChildThread()
        {
            while (!Integration._connected)
            {
                Thread.Sleep(100);
            }
        }
        #endregion

        #region Handlers

        private static void TimerHandler(object sender, ElapsedEventArgs args)
        {
            // Change our timer interval just in case.
            Integration.Timer.Interval = Integration.TimeToUpdate;

            bool lastKnownBl2 = Integration._bl2;
            bool lastKnownTps = Integration._tps;

            Process? bl2Process = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.Contains("Borderlands2"));
            Process? tpsProcess = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.Contains("BorderlandsPreSequel"));
            DateTime launchDate = new();
            if (bl2Process != null)
            {
                Integration._bl2 = true;
                launchDate = bl2Process.StartTime;
            }
            else if (tpsProcess != null)
            {
                Integration._tps = true;
                launchDate = tpsProcess.StartTime;
            }

            //client.Invoke();


            if (!Integration._bl2 && !Integration._tps)
            {
                Integration._client.ClearPresence();
                return;
            }

            if (lastKnownBl2 != Integration._bl2 || lastKnownTps != Integration._tps)
            {
                Integration.SetupClient();
            }

            int level = Integration.GetCurrentLevel();
            string map = Integration.GetCurrentMap();

            RichPresence presence = new RichPresence
            {
                Details = Integration.GetCurrentMission(),
                State = level > 0 ? string.Format("{1} {0} ({2} of 4)", level, Integration.GetCurrentClass(), Integration.GetPlayersInLobby()) : "",
                Assets = new Assets
                {
                    LargeImageKey = Regex.Replace(map, @"[ '.\-,/]", "").ToLower(),
                    LargeImageText = map,
                    SmallImageKey = "default",
                    SmallImageText = Integration._bl2 ? "Borderlands 2" : "Borderlands: The Pre-Sequel"
                },
                Timestamps = new Timestamps(launchDate.ToUniversalTime())
            };

            Integration._client.SetPresence(presence);
        }
        #endregion

        #region Data Fetchers
        private static int GetPlayersInLobby()
        {
            IReadOnlyList<BLObject> players = BLIO.GetAll("WillowPlayerPawn");
            //players.Distinct().Where(o => o.Name.Contains("Loader."));
            int count = players.Distinct().Count(o => o.Name.Contains("Loader."));
            int characters = count != 0 ? count : 1;
            return characters;
        }

        private static string GetCurrentMap()
        {
            IReadOnlyDictionary<BLObject, object?> players = BLIO.GetAll("WorldInfo", "NavigationPointList");
            IEnumerable<KeyValuePair<BLObject, object?>> dict = players.Where(p => p.Key.Name.Contains("Loader."));

            string pylon = dict.Aggregate("", (current, kvp) => current + (((BLObject?)kvp.Value)?.Name ?? string.Empty));
            string mapName = Integration.MapFileToActualMap(pylon);
            if (pylon != null && (mapName.Trim() == "" || pylon.Contains("Fake")))
                mapName = Integration._lastKnownMap;
            else
                Integration._lastKnownMap = mapName;

            return mapName;
        }

        private static string GetCurrentMission()
        {
            IReadOnlyDictionary<BLObject, object?> dict = BLIO.GetAll("HUDWidget_Missions", "CachedMissionName");
            //KeyValuePair<BLObject, object>[] arr = dict.Where(m => m.Key.Name.Contains("Transient")).ToArray();
            string? mission = dict.FirstOrDefault().Value?.ToString();

            if (mission == null || mission.Trim() == "")
                mission = "In Menu";

            return mission;
        }

        private static string GetCurrentClass()
        {
            //IReadOnlyDictionary<BLObject, object> dict = Blio.GetAll("WillowPlayerController", "CharacterClass");
            //KeyValuePair<BLObject, object>[] arr = dict.Where(m => m.Key.Name.Contains("Loader")).ToArray();
            string? characterClass = (BLObject.GetPlayerController()?["CharacterClass"] as BLObject)?.Name;
            if (characterClass == null || characterClass.Trim() == "")
                characterClass = Integration._lastKnownChar;
            else
                Integration._lastKnownChar = characterClass;

            return Integration.ClassToCharacterName(characterClass);
        }

        private static int GetCurrentLevel()
        {

            IReadOnlyDictionary<BLObject, object?> dict = BLIO.GetAll("WillowHUDGFxMovie", "CachedLevel");
            //KeyValuePair<BLObject, object?>[] arr = dict.Where(m => m.Key.Name.Contains("Transient")).ToArray();
            string? lev = dict.FirstOrDefault().Value?.ToString();

            if (int.TryParse(lev, out int level) && level != 0)
                Integration._lastKnownLevel = level;
            else
                level = Integration._lastKnownLevel;
            return level;

        }
        #endregion

        #region Helpers
        private static string ClassToCharacterName(string charClass)
        {
            if (Integration._bl2)
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

            else if (Integration._tps)
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

        private static string MapFileToActualMap(string map)
        {
            if (map == "")
                return "Unknown";
            map = map.ToLower(CultureInfo.InvariantCulture);
            if (Integration._bl2)
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
                    if (map.StartsWith("hub"))
                    {
                        return map[3] == '2' ? "Southern Raceway" : "Badass Crater of Badassitude";
                    }
                    if (map.StartsWith("dl2"))
                    {
                        return map.Contains("interior") ? @"Pyro Pete's Bar" : "The Beatdown";
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
                if (map.StartsWith("sanctintro") || map.StartsWith("gaiussanctuary"))
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
                    return map.StartsWith("damtop") ? "Bloodshot Ramparts" : "Bloodshot Stronghold";
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
                    return map.Contains("cliffs") ? "Thousand Cuts" : "Highlands";
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
            else if (Integration._tps)
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
                    return map.Contains("boss") ? "Titan Robot Production Plant" : "Titan Industrial Facility";
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
