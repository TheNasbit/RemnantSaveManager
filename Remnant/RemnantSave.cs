using System;
using System.Collections.Generic;
using System.IO;

namespace RemnantSaveManager.Remnant
{
    public class RemnantSave
    {
        private string savePath;
        private string profileFile;
        private List<RemnantCharacter> saveCharacters;
        private RemnantSaveType saveType;
        private WindowsSave winSave;

        public RemnantSave(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new Exception(path + " does not exist.");
            }

            if (File.Exists(path + "\\profile.sav"))
            {
                this.saveType = RemnantSaveType.Normal;
                this.profileFile = "profile.sav";
            }
            else
            {
                var winFiles = Directory.GetFiles(path, "container.*");
                if (winFiles.Length > 0)
                {
                    this.winSave = new WindowsSave(winFiles[0]);
                    this.saveType = RemnantSaveType.WindowsStore;
                    this.profileFile = this.winSave.Profile;
                }
                else
                {
                    throw new Exception(path + " is not a valid save.");
                }
            }
            this.savePath = path;
            this.saveCharacters = RemnantCharacter.GetCharactersFromSave(this, RemnantCharacter.CharacterProcessingMode.NoEvents);
        }

        public string SaveFolderPath
        {
            get
            {
                return this.savePath;
            }
        }

        public string SaveProfilePath
        {
            get
            {
                return this.savePath + $@"\{this.profileFile}";
            }
        }
        public RemnantSaveType SaveType
        {
            get { return this.saveType; }
        }

        public List<RemnantCharacter> Characters
        {
            get
            {
                return this.saveCharacters;
            }
        }
        public string[] WorldSaves
        {
            get
            {
                if (this.saveType == RemnantSaveType.Normal)
                {
                    return Directory.GetFiles(this.SaveFolderPath, "save_*.sav");
                }
                else
                {
                    System.Console.WriteLine(this.winSave.Worlds.ToArray());
                    return this.winSave.Worlds.ToArray();
                }
            }
        }

        public bool Valid
        {
            get
            {
                return this.saveType == RemnantSaveType.Normal || this.winSave.Valid;
            }
        }

        public static Boolean ValidSaveFolder(String folder)
        {
            if (!Directory.Exists(folder))
            {
                return false;
            }

            if (File.Exists(folder + "\\profile.sav"))
            {
                return true;
            }
            else
            {
                var winFiles = Directory.GetFiles(folder, "container.*");
                if (winFiles.Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateCharacters()
        {
            this.saveCharacters = RemnantCharacter.GetCharactersFromSave(this);
        }

        public void UpdateCharacters(RemnantCharacter.CharacterProcessingMode mode)
        {
            this.saveCharacters = RemnantCharacter.GetCharactersFromSave(this, mode);
        }
    }

    public enum RemnantSaveType
    {
        Normal,
        WindowsStore
    }
}
