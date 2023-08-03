using System;
using System.Text.RegularExpressions;

namespace RemnantSaveManager.Remnant
{
    public class RemnantItem : IEquatable<Object>, IComparable
    {
        public enum RemnantItemMode
        {
            Normal,
            Hardcore,
            Survival
        }

        private string itemKey;
        private string itemType;
        private string itemName;
        private string itemAltName;
        private string ItemKey {
            get { return this.itemKey; }
            set
            {
                try
                {
                    this.itemKey = value;
                    this.itemType = "Uncategorized";
                    this.itemName = this.itemKey.Substring(this.itemKey.LastIndexOf('/') + 1);
                    if (this.itemKey.Contains("/Weapons/"))
                    {
                        this.itemType = "Weapon";
                        if (this.itemName.Contains("Mod_")) this.itemName = this.itemName.Replace("/Weapons/", "/Mods/");
                    }
                    if (this.itemKey.Contains("/Armor/") || this.itemKey.Contains("TwistedMask"))
                    {
                        this.itemType = "Armor";
                        if (this.itemKey.Contains("TwistedMask"))
                        {
                            this.itemName = "TwistedMask (Head)";
                        }
                        else
                        {
                            string[] parts = this.itemName.Split('_');
                            this.itemName = parts[2] + " (" + parts[1] + ")";
                        }
                    }
                    if (this.itemKey.Contains("/Trinkets/") || this.itemKey.Contains("BrabusPocketWatch")) this.itemType = "Trinket";
                    if (this.itemKey.Contains("/Mods/")) this.itemType = "Mod";
                    if (this.itemKey.Contains("/Traits/")) this.itemType = "Trait";
                    if (this.itemKey.Contains("/Emotes/")) this.itemType = "Emote";

                    this.itemName = this.itemName.Replace("Weapon_", "").Replace("Root_", "").Replace("Wasteland_", "").Replace("Swamp_", "").Replace("Pan_", "").Replace("Atoll_", "").Replace("Mod_", "").Replace("Trinket_", "").Replace("Trait_", "").Replace("Quest_", "").Replace("Emote_", "").Replace("Rural_", "").Replace("Snow_", "");
                    if (!this.itemType.Equals("Armor"))
                    {
                        this.itemName = Regex.Replace(this.itemName, "([a-z])([A-Z])", "$1 $2");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error processing item name: " + ex.Message);
                    this.itemName = value;
                }
            }
        }

        public string ItemName
        {
            get
            {
                if (this.itemAltName != null) return this.itemAltName;
                return this.itemName;
            }
        }
        public string ItemType { get { return this.itemType; } }
        public RemnantItemMode ItemMode { get; set; }
        public string ItemNotes { get; set; }
        public string ItemAltName { get { return this.itemAltName; } set { this.itemAltName = value; } }

        public RemnantItem(string key)
        {
            this.ItemKey = key;
            this.ItemMode = RemnantItemMode.Normal;
            this.ItemNotes = "";
        }

        public RemnantItem(string key, RemnantItemMode mode)
        {
            this.ItemKey = key;
            this.ItemMode = mode;
            this.ItemNotes = "";
        }

        public string GetKey()
        {
            return this.itemKey;
        }

        public override string ToString()
        {
            return this.itemType + ": " + this.ItemName;
        }

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null))
            {
                return false;
            }
            else if (!this.GetType().Equals(obj.GetType()))
            {
                if (obj.GetType() == typeof(string))
                {
                    return (this.GetKey().Equals(obj));
                }
                return false;
            }
            else
            {
                RemnantItem rItem = (RemnantItem)obj;
                return (this.GetKey().Equals(rItem.GetKey()) && this.ItemMode == rItem.ItemMode);
            }
        }

        public override int GetHashCode()
        {
            return this.itemKey.GetHashCode();
        }

        public int CompareTo(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null))
            {
                return 1;
            }
            else if (!this.GetType().Equals(obj.GetType()))
            {
                if (obj.GetType() == typeof(string))
                {
                    return (this.GetKey().CompareTo(obj));
                }
                return this.ToString().CompareTo(obj.ToString());
            }
            else
            {
                RemnantItem rItem = (RemnantItem)obj;
                if (this.ItemMode != rItem.ItemMode)
                {
                    return this.ItemMode.CompareTo(rItem.ItemMode);
                }
                return this.itemKey.CompareTo(rItem.GetKey());
            }
        }
    }
}
