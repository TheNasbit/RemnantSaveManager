﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RemnantSaveManager.Remnant
{
    public class RemnantCharacter
    {
        public string Archetype { get; set; }
        public List<string> Inventory { get; set; }
        public List<RemnantWorldEvent> CampaignEvents { get; set; }
        public List<RemnantWorldEvent> AdventureEvents { get; set; }

        public int Progression {
            get {
                return this.Inventory.Count;
            }
        }

        private List<RemnantItem> missingItems;

        private RemnantSave save;

        public enum ProcessMode { Campaign, Adventure };

        public override string ToString()
        {
            return this.Archetype + " (" + this.Progression + ")";
        }

        public string ToFullString()
        {
            string str = "CharacterData{ Archetype: " + this.Archetype + ", Inventory: [" + string.Join(", ", this.Inventory) + "], CampaignEvents: [" + string.Join(", ", this.CampaignEvents) + "], AdventureEvents: [" + string.Join(", ", this.AdventureEvents) + "] }";
            return str;
        }

        public RemnantCharacter()
        {
            this.Archetype = "";
            this.Inventory = new List<string>();
            this.CampaignEvents = new List<RemnantWorldEvent>();
            this.AdventureEvents = new List<RemnantWorldEvent>();
            this.missingItems = new List<RemnantItem>();
            this.save = null;
        }

        public void processSaveData(string savetext)
        {
            //get campaign info
            string strCampaignEnd = "/Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C";
            string strCampaignStart = "/Game/Campaign_Main/Quest_Campaign_City.Quest_Campaign_City";
            int campaignEnd = savetext.IndexOf(strCampaignEnd);
            int campaignStart = savetext.IndexOf(strCampaignStart);
            if (campaignStart != -1 && campaignEnd != -1)
            {
                string campaigntext = savetext.Substring(0, campaignEnd);
                campaignStart = campaigntext.LastIndexOf(strCampaignStart);
                campaigntext = campaigntext.Substring(campaignStart);
                RemnantWorldEvent.ProcessEvents(this, campaigntext, RemnantWorldEvent.ProcessMode.Campaign);
            }
            else
            {
                strCampaignEnd = "/Game/Campaign_Clementine/Quest_Campaign_Clementine.Quest_Campaign_Clementine_C";
                strCampaignStart = "/Game/World_Rural/Templates/Template_Rural_Overworld_0";
                campaignEnd = savetext.IndexOf(strCampaignEnd);
                campaignStart = savetext.IndexOf(strCampaignStart);
                if (campaignStart != -1 && campaignEnd != -1)
                {
                    string campaigntext = savetext.Substring(0, campaignEnd);
                    campaignStart = campaigntext.LastIndexOf(strCampaignStart);
                    campaigntext = campaigntext.Substring(campaignStart);
                    RemnantWorldEvent.ProcessEvents(this, campaigntext, RemnantWorldEvent.ProcessMode.Subject2923);
                } else
                {
                    Console.WriteLine("Campaign not found; likely in tutorial mission.");
                }
            }

            //get adventure info
            if (savetext.Contains("Quest_AdventureMode_"))
            {
                string adventureZone = null;
                if (savetext.Contains("Quest_AdventureMode_City_C")) adventureZone = "City";
                if (savetext.Contains("Quest_AdventureMode_Wasteland_C")) adventureZone = "Wasteland";
                if (savetext.Contains("Quest_AdventureMode_Swamp_C")) adventureZone = "Swamp";
                if (savetext.Contains("Quest_AdventureMode_Jungle_C")) adventureZone = "Jungle";
                if (savetext.Contains("Quest_AdventureMode_Snow_C")) adventureZone = "Snow";

                string strAdventureEnd = String.Format("/Game/World_{0}/Quests/Quest_AdventureMode/Quest_AdventureMode_{0}.Quest_AdventureMode_{0}_C", adventureZone);
                int adventureEnd = savetext.IndexOf(strAdventureEnd) + strAdventureEnd.Length;
                string advtext = savetext.Substring(0, adventureEnd);
                string strAdventureStart = String.Format("/Game/World_{0}/Quests/Quest_AdventureMode/Quest_AdventureMode_{0}_0", adventureZone);
                int adventureStart = advtext.LastIndexOf(strAdventureStart) + strAdventureStart.Length;
                advtext = advtext.Substring(adventureStart);
                RemnantWorldEvent.ProcessEvents(this, advtext, RemnantWorldEvent.ProcessMode.Adventure);
            }

            this.missingItems.Clear();
            foreach (RemnantItem[] eventItems in GameInfo.EventItem.Values)
            {
                foreach (RemnantItem item in eventItems)
                {
                    if (!this.Inventory.Contains(item.GetKey()))
                    {
                        if (!this.missingItems.Contains(item))
                        {
                            this.missingItems.Add(item);
                        }
                    }
                }
            }
            this.missingItems.Sort();
        }

        public enum CharacterProcessingMode { All, NoEvents };

        public static List<RemnantCharacter> GetCharactersFromSave(RemnantSave remnantSave)
        {
            return GetCharactersFromSave(remnantSave, CharacterProcessingMode.All);
        }

        public static List<RemnantCharacter> GetCharactersFromSave(RemnantSave remnantSave, CharacterProcessingMode mode)
        {
            List<RemnantCharacter> charData = new List<RemnantCharacter>();
            try
            {
                string profileData = File.ReadAllText(remnantSave.SaveProfilePath);
                string[] characters = profileData.Split(new string[] { "/Game/Characters/Player/Base/Character_Master_Player.Character_Master_Player_C" }, StringSplitOptions.None);
                for (var i = 1; i < characters.Length; i++)
                {
                    RemnantCharacter cd = new RemnantCharacter();
                    cd.Archetype = GameInfo.Archetypes["Undefined"];
                    Match archetypeMatch = new Regex(@"/Game/_Core/Archetypes/[a-zA-Z_]+").Match(characters[i-1]);
                    if (archetypeMatch.Success)
                    {
                        string archetype = archetypeMatch.Value.Replace("/Game/_Core/Archetypes/", "").Split('_')[1];
                        if (GameInfo.Archetypes.ContainsKey(archetype))
                        {
                            cd.Archetype = GameInfo.Archetypes[archetype];
                        } else
                        {
                            cd.Archetype = archetype;
                        }
                    }
                    cd.save = remnantSave;
                    List<string> saveItems = new List<string>();
                    string charEnd = "Character_Master_Player_C";
                    string inventory = characters[i].Substring(0, characters[i].IndexOf(charEnd));

                    Regex rx = new Regex(@"/Items/Weapons(/[a-zA-Z0-9_]+)+/[a-zA-Z0-9_]+");
                    MatchCollection matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Items/Armor/([a-zA-Z0-9_]+/)?[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Items/Trinkets/(BandsOfCastorAndPollux/)?[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Items/Mods/[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Items/Traits/[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Items/QuestItems(/[a-zA-Z0-9_]+)+/[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Quests/[a-zA-Z0-9_]+/[a-zA-Z0-9_]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    rx = new Regex(@"/Player/Emotes/Emote_[a-zA-Z0-9]+");
                    matches = rx.Matches(inventory);
                    foreach (Match match in matches)
                    {
                        saveItems.Add(match.Value);
                    }

                    cd.Inventory = saveItems;
                    charData.Add(cd);
                }

                if (mode == CharacterProcessingMode.All)
                {
                    string[] saves = remnantSave.WorldSaves;
                    for (int i = 0; i < saves.Length && i < charData.Count; i++)
                    {
                        charData[i].processSaveData(File.ReadAllText(saves[i]));
                    }
                }
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("being used by another process"))
                {
                    Console.WriteLine("Save file in use; waiting 0.5 seconds and retrying.");
                    System.Threading.Thread.Sleep(500);
                    charData = GetCharactersFromSave(remnantSave, mode);
                }
            }
            return charData;
        }

        public void LoadWorldData(int charIndex)
        {
            if (this.save != null)
            {
                if (this.CampaignEvents.Count == 0)
                {
                    string[] saves = this.save.WorldSaves;
                    if (charIndex < saves.Length)
                    {
                        try
                        {
                            this.processSaveData(File.ReadAllText(saves[charIndex]));
                        }
                        catch (IOException ex)
                        {
                            if (ex.Message.Contains("being used by another process"))
                            {
                                Console.WriteLine("Save file in use; waiting 0.5 seconds and retrying.");
                                System.Threading.Thread.Sleep(500);
                                this.LoadWorldData(charIndex);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error loading world Data: ");
                            Console.WriteLine("\tCharacterData.LoadWorldData");
                            Console.WriteLine("\t"+ex.ToString());
                        }
                    }
                }
            }
        }

        public List<RemnantItem> GetMissingItems()
        {
            return this.missingItems;
        }
    }
}
