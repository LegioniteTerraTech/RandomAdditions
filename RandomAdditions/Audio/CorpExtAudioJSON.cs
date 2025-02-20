using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RandomAdditions
{
    public class CorpExtAudioJSON
    {
        public FactionSubTypes CorpFallbackMusic = FactionSubTypes.GSO;
        public float[] MusicLoopStartOffset = new float[0];


        public FactionSubTypes CorpEngine = FactionSubTypes.GSO;
        public float EnginePitchDeepMulti = 1;
        public float EnginePitchMax = float.MaxValue;

        public float EngineIdealSpeed = 30;
        public float EngineMaxPitchMulti = 3;
        public float EngineVolumeMulti = 1f;
    }
}
