using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The HUD's icons by id (an ability's, «weapon:sniper», «stat:hp», «ui:check»…), each a sprite of the INTERFACE packs of
    /// Synty in Assets/Synty (not in the repository: their licence). An asset in Resources, so a build carries the sprites
    /// it names; made by HudIcons.Make (synty-icons.css and tools/ui/synty-sprites.py of the 0.10.1). A missing one is
    /// null: the key stays blank.
    /// </summary>
    [CreateAssetMenu(menuName = "Bastión/Iconos del HUD")]
    public sealed class HudIconTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public Sprite Sprite;
        }

        public List<Entry> Entries = new List<Entry>();

        Dictionary<string, Sprite> byId;

        public Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null)
            {
                byId = new Dictionary<string, Sprite>();
                foreach (var e in Entries)
                    if (e.Sprite != null) byId[e.Id] = e.Sprite;
            }
            return byId.TryGetValue(id, out var s) ? s : null;
        }

        void OnValidate() => byId = null;
    }
}
