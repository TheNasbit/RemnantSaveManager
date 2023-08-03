using System;
using System.Collections.Generic;
using System.IO;

namespace RemnantSaveManager.RemnantTwo
{
    public class RemnantTwoSave
    {
        private string savePath;
        private string profileFile;

        public RemnantTwoSave(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new Exception(path + " does not exist.");
            }

            if (File.Exists(path + "\\profile.sav"))
            {
                this.profileFile = "profile.sav";
            }
            this.savePath = path;
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

        public string[] WorldSaves
        {
            get
            {
                return Directory.GetFiles(this.SaveFolderPath, "save_*.sav");
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
            return false;
        }
    }
}
