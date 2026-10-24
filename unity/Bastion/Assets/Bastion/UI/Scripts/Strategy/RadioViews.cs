using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// A radio transmission (strategy/transmissions.ts RadioPanel.play): a scene's lines, said one at a time in the card
    /// docked over the base, its placeholders ({saludo}, {yo}) already filled (Radio does it).
    /// </summary>
    [Serializable]
    public sealed class RadioView
    {
        /// <summary>The scene's id (SceneIds), told back when it ends.</summary>
        public string Scene;
        /// <summary>«Relevo», under the line with its count.</summary>
        public string Title;
        /// <summary>Where it is heard («Línea interna · El Bastión»), after «Transmisión».</summary>
        public string Channel;
        public List<RadioLineView> Lines = new List<RadioLineView>();
    }

    [Serializable]
    public sealed class RadioLineView
    {
        /// <summary>The speaker's id (SpeakerIds): their bust (Arte's Portraits.Voice) and the Coro's own look.</summary>
        public string Who;
        public string Name;
        public string Role;
        public string Text;
        /// <summary>The rule under one of Morse's warnings, in small print; null, none.</summary>
        public string Rule;
    }
}
