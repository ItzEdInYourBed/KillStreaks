﻿using Rocket.Core.Plugins;
using System.Linq;
using Rocket.Core.Logging;
using Rocket.Unturned.Player;
using Rocket.Unturned.Events;
using Steamworks;
using System.Collections.Generic;
using Rocket.Unturned.Chat;
using Rocket.Unturned;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using Rocket.API.Collections;
using Rocket.Core;

namespace ExtraConcentratedJuice.KillStreaks
{
    public class KillStreaks : RocketPlugin<KillStreaksConfig>
    {
        public static string[] deathCauses = { "GUN", "MELEE", "PUNCH", "ROADKILL" };
        public Dictionary<string, int> killCount;
        public static KillStreaks instance;
        public const string KSFILEPATH = "Plugins/KillStreaks/data.xml";

        protected override void Load()
        {
            instance = this;

            if (!File.Exists(KSFILEPATH))
            {
                Logger.Log("Datafile not found, creating one now...");
                File.Create(KSFILEPATH).Dispose();
            }

            Logger.Log("---------------------------");
            Logger.Log("Extra's KillStreaks Loaded!");
            Logger.Log("---------------------------");
            Logger.Log("> Restart Persistence: " + Configuration.Instance.enable_restart_persistence);
            if (Configuration.Instance.enable_restart_persistence)
            {
                Logger.Log(" Restart persistence enabled, loading data...");
                try
                {
                    killCount = DeserializePlayerDict();
                }
                catch (XmlException)
                {
                    Logger.Log(" Failed to deserialize datafile. This is normal for a first run.");
                    Logger.Log(" Delete data.xml in plugin folder if this keeps on happening.");
                    Logger.Log(" MAKE SURE THAT THE PLUGIN IS PROPERLY UNLOADED.");
                    killCount = new Dictionary<string, int>();
                }
            }
            else
            {
                killCount = new Dictionary<string, int>();
            }
            Logger.Log("> Remove Killstreak on Disconnect: " + Configuration.Instance.remove_streak_on_disconnect);
            Logger.Log("> Killstreak Divisor: " + Configuration.Instance.kill_divisor);
            Logger.Log("> Killstreak Threshold: " + Configuration.Instance.kill_streak_threshold);

            UnturnedPlayerEvents.OnPlayerDeath += OnDeath;
            U.Events.OnPlayerDisconnected += OnDisconnected;
            U.Events.OnPlayerConnected += OnConnected;

        }

        protected override void Unload()
        {
            if (Configuration.Instance.enable_restart_persistence)
            {
                Logger.LogWarning("enable_restart_persistence is set to TRUE.");
                Logger.LogWarning("Saving killstreak data to a file...");
                SerializePlayerDict(killCount);
                Logger.LogWarning("Done!");
            }
            else
            {
                Logger.LogWarning("enable_restart_persistence is set to FALSE");
                Logger.LogWarning("The plugin will not save data across restarts.");
            }
        }

        public static void SerializePlayerDict(Dictionary<string, int> dict)
        {
            List<KSPlayer> players = new List<KSPlayer>();
            foreach (KeyValuePair<string, int> kv in dict)
            {
                players.Add(new KSPlayer(kv.Key, kv.Value));
            }
            XmlSerializer serializer = new XmlSerializer(typeof(List<KSPlayer>));
            using (TextWriter stream = new StreamWriter(KSFILEPATH, false))
            {
                serializer.Serialize(stream, players);
            }
        }

        public static Dictionary<string, int> DeserializePlayerDict()
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            XmlSerializer serializer = new XmlSerializer(typeof(List<KSPlayer>));
            List<KSPlayer> players;
            using (TextReader stream = new StreamReader(KSFILEPATH))
            {
                players = (List<KSPlayer>)serializer.Deserialize(stream);
            }
            foreach (KSPlayer player in players)
            {
                dict[player.Id] = player.Kills;
            }
            return dict;
        }

        public class KSPlayer
        {
            public string Id;
            public int Kills;

            public KSPlayer()
            {
            }

            public KSPlayer(string id, int kills)
            {
                Id = id;
                Kills = kills;
            }
        }

        private void OnConnected(UnturnedPlayer player)
        {
            if (!killCount.TryGetValue(player.Id, out int count))
            {
                killCount[player.Id] = 0;
            }
        }

        private void OnDisconnected(UnturnedPlayer player)
        {
            if (killCount.TryGetValue(player.Id, out int count) && Configuration.Instance.remove_streak_on_disconnect)
            {
                killCount.Remove(player.Id);

            }
        }

        private void OnDeath(UnturnedPlayer player, SDG.Unturned.EDeathCause cause, SDG.Unturned.ELimb limb, CSteamID killer)
        {
            if (killCount.TryGetValue(player.Id, out int playerKillCount))
            {
                if (killCount[player.Id] >= Configuration.Instance.kill_streak_lost_threshold && Configuration.Instance.kill_streak_lost_threshold > 0)
                {
                    UnturnedChat.Say(string.Format(Configuration.Instance.kill_streak_lose_message, player.DisplayName, killCount[player.Id]), UnturnedChat.GetColorFromName(Configuration.Instance.kill_streak_lost_message_color, UnityEngine.Color.red));
                }
                killCount[player.Id] = 0;
            }

            string deathCause = cause.ToString();

            if (!deathCauses.Contains(deathCause))
            {
                return;
            }

            UnturnedPlayer killerPlayer = UnturnedPlayer.FromCSteamID(killer);

            if (player.Id == killerPlayer.Id)
            {
                return;
            }

            if (killCount.TryGetValue(killerPlayer.Id, out int killerKillCount))
            {
                killCount[killerPlayer.Id] = killerKillCount + 1;
            }
            else
            {
                killCount[killerPlayer.Id] = 0;
            }

            if (killCount[killerPlayer.Id] % Configuration.Instance.kill_divisor == 0 && killCount[killerPlayer.Id] >= Configuration.Instance.kill_streak_threshold)
            {
                UnturnedChat.Say(string.Format(Configuration.Instance.kill_streak_message, killerPlayer.DisplayName, killCount[killerPlayer.Id]), UnturnedChat.GetColorFromName(Configuration.Instance.kill_streak_message_color, UnityEngine.Color.magenta));
                foreach (KillStreaksConfig.CommandGroup group in Configuration.Instance.CommandGroups)
                {
                    if (killCount[killerPlayer.Id] >= group.KillMin && (killCount[killerPlayer.Id] <= group.KillMax || group.KillMax <= 0))
                    {
                        foreach(string cmd in group.Commands)
                        {
                            R.Commands.Execute(new Rocket.API.ConsolePlayer(), string.Format(cmd, killerPlayer.DisplayName));
                        }
                    }
                }
            }
        }

        public override TranslationList DefaultTranslations
        {
            get
            {
                return new TranslationList()
                {
                    {"killstreak_increment", "[KillStreaks] Your killstreak has been incremented."},
                    {"killstreak_count", "[KillStreaks] You are on a {0} killstreak."},
                    {"killstreak_remove", "[KillStreaks] Your killstreak has been reset."},
                };
            }
        }
    }
}