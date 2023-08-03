﻿using System;
using System.ComponentModel;
using System.IO;

namespace RemnantSaveManager.Remnant
{
    public class SaveBackup : IEditableObject
    {
        struct SaveData
        {
            internal string name;
            internal DateTime date;
            internal bool keep;
            internal bool active;
        }

        public event EventHandler<UpdatedEventArgs> Updated;
        private SaveData saveData;
        private SaveData backupData;
        private bool inTxn = false;
        //private int[] progression;
        //private List<RemnantCharacter> charData;
        private RemnantSave save;
        public string Name
        {
            get
            {
                return this.saveData.name;
            }
            set
            {
                if (value.Equals(""))
                {
                    this.saveData.name = this.saveData.date.Ticks.ToString();
                } else
                {
                    this.saveData.name = value;
                }
                //OnUpdated(new UpdatedEventArgs("Name"));
            }
        }
        public DateTime SaveDate
        {
            get {
                return this.saveData.date;
            }
            set
            {
                this.saveData.date = value;
                //OnUpdated(new UpdatedEventArgs("SaveDate"));
            }
        }
        public string Progression
        {
            get
            {
                return string.Join(",", this.save.Characters);
            }
        }
        public bool Keep
        {
            get
            {
                return this.saveData.keep;
            }
            set
            {
                this.saveData.keep = value;
                //OnUpdated(new UpdatedEventArgs("Keep"));
            }
        }
        public bool Active
        {
            get
            {
                return this.saveData.active;
            }
            set
            {
                this.saveData.active = value;
                //OnUpdated(new UpdatedEventArgs("Active"));
            }
        }

        public RemnantSave Save
        {
            get
            {
                return this.save;
            }
        }

        //public SaveBackup(DateTime saveDate)
        public SaveBackup(string savePath)
        {
            this.save = new RemnantSave(savePath);
            this.saveData = new SaveData();
            this.saveData.name = this.SaveDateTime.Ticks.ToString();
            this.saveData.date = this.SaveDateTime;
            this.saveData.keep = false;
        }

        /*public void setProgression(List<List<string>> allItemList)
        {

            int[] prog = new int[allItemList.Count];
            for (int i=0; i < allItemList.Count; i++)
            {
                prog[i] = allItemList[i].Count;
            }
            this.progression = prog;
        }
        public List<RemnantCharacter> GetCharacters()
        {
            return this.charData;
        }
        public void LoadCharacterData(string saveFolder)
        {
            this.charData = RemnantCharacter.GetCharactersFromSave(saveFolder, RemnantCharacter.CharacterProcessingMode.NoEvents);
        }*/

        // Implements IEditableObject
        void IEditableObject.BeginEdit()
        {
            if (!this.inTxn)
            {
                this.backupData = this.saveData;
                this.inTxn = true;
            }
        }

        void IEditableObject.CancelEdit()
        {
            if (this.inTxn)
            {
                this.saveData = this.backupData;
                this.inTxn = false;
            }
        }

        void IEditableObject.EndEdit()
        {
            if (this.inTxn)
            {
                if (!this.backupData.name.Equals(this.saveData.name))
                {
                    this.OnUpdated(new UpdatedEventArgs("Name"));
                }
                if (!this.backupData.date.Equals(this.saveData.date))
                {
                    this.OnUpdated(new UpdatedEventArgs("SaveDate"));
                }
                if (!this.backupData.keep.Equals(this.saveData.keep))
                {
                    this.OnUpdated(new UpdatedEventArgs("Keep"));
                }
                if (!this.backupData.active.Equals(this.saveData.active))
                {
                    this.OnUpdated(new UpdatedEventArgs("Active"));
                }
                this.backupData = new SaveData();
                this.inTxn = false;
            }
        }

        public void OnUpdated(UpdatedEventArgs args)
        {
            EventHandler<UpdatedEventArgs> handler = this.Updated;
            if (null != handler) handler(this, args);
        }

        private DateTime SaveDateTime
        {
            get
            {
                return File.GetLastWriteTime(this.save.SaveProfilePath);
            }
        }
    }

    public class UpdatedEventArgs : EventArgs
    {
        private readonly string _fieldName;

        public UpdatedEventArgs(string fieldName) {
            this._fieldName = fieldName;
        }

        public string FieldName
        {
            get { return this._fieldName; }
        }
    }
}
