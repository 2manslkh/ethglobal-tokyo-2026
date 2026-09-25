using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class LocalBlocks
    {
        [Serializable] private sealed class Saved { public List<string> authors = new List<string>(); }
        public HashSet<string> Read(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return new HashSet<string>();
            try { return new HashSet<string>(JsonUtility.FromJson<Saved>(PlayerPrefs.GetString("tagtag.blocks." + uid, "{}")).authors ?? new List<string>()); }
            catch { return new HashSet<string>(); }
        }
        public void Save(string uid, HashSet<string> authors)
        {
            PlayerPrefs.SetString("tagtag.blocks." + uid, JsonUtility.ToJson(new Saved { authors = new List<string>(authors) }));
            PlayerPrefs.Save();
        }
        public void Remove(string uid) { PlayerPrefs.DeleteKey("tagtag.blocks." + uid); PlayerPrefs.Save(); }
    }
}
