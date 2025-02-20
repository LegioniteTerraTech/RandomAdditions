using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RandomAdditions
{
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
    }
}