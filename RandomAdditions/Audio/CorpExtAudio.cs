using System;
using System.Collections.Generic;

namespace RandomAdditions
{
    /// <summary>
    /// It now does much more than just audio...
    /// </summary>
    public class CorpExtAudio
    {
        public int ID;
        public FactionSubTypes FallbackMusic = FactionSubTypes.NULL;
        public FactionSubTypes CorpEngine = FactionSubTypes.NULL;
        public float EnginePitchDeepMulti = 1;
        public float EnginePitchMax = float.MaxValue;
        public float[] MusicLoopStartOffset = new float[0];
        public List<FMOD.Sound> combatMusicLoaded;

        public bool hasEngineAudio = false;
        public float EngineIdealSpeed = 30;
        public float EngineVolumeMulti = 30;
        public FMOD.Sound CorpEngineAudioIdle;
        public FMOD.Sound CorpEngineAudioRunning;
        public FMOD.Sound CorpEngineAudioStart;
        public FMOD.Sound CorpEngineAudioStop;

        // OTHERS:
        /// <summary> Make all the APs on the weapon block C&S triggered </summary>
        public bool AllWeaponAPsAreCnS = false;
    }
}