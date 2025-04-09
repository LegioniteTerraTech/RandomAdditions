using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions.RailSystem
{

    internal class UnstableTrack
    {
        private const float DetachDelta = 1.5f;
        internal readonly RailTrack tracked;
        internal bool setup;
        internal Vector3 StartDelta;
        internal Vector3 EndDelta;
        internal UnstableTrack(RailTrack toTrack)
        {
            tracked = toTrack;
            setup = false;
            StartDelta = Vector3.zero;
            EndDelta = Vector3.zero;
        }
        internal bool CheckStillConnected()
        {
            if (tracked.Fake)
                return true; // Skip the checks since fake tracks are not real
            if (!setup)
            {
                setup = true;
                StartDelta = -(tracked.StartConnection.RailEndPosOnNode().ScenePosition -
                    tracked.StartConnection.RailEndPositionOnRailScene());
                EndDelta = -(tracked.EndConnection.RailEndPosOnNode().ScenePosition -
                    tracked.EndConnection.RailEndPositionOnRailScene());
            }
            Vector3 startDelta = tracked.StartConnection.RailEndPosOnNode().ScenePosition -
                tracked.StartConnection.RailEndPositionOnRailScene() + StartDelta;
            Vector3 endDelta = tracked.EndConnection.RailEndPosOnNode().ScenePosition -
                tracked.EndConnection.RailEndPositionOnRailScene() + EndDelta;
            bool still = startDelta.WithinBox(DetachDelta) && endDelta.WithinBox(DetachDelta);
            if (!still)
                DebugRandAddi.Log("UnstableTrack.CheckStillConnected() - detached due to start delta " +
                    startDelta.ToString() + " and end delta " + endDelta.ToString());
            return still;
        }
    }
}
