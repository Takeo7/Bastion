using System;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>The radio's voices (portraits.ts VOICES): who each speaker is drawn as, how turned, lit only from behind or not.</summary>
    public sealed class Voices
    {
        public Func<GameObject> Make;
        public float turn = -0.3f;
        public bool backlit;
        public bool female;

        /// <summary>The voice of `speaker` (SpeakerIds); null, none yet.</summary>
        public static Voices Of(string speaker) => null;
    }
}
