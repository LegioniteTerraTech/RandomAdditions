using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

public class ModuleItemFixedHolderBeam : RandomAdditions.ModuleItemFixedHolderBeam { };
namespace RandomAdditions
{
    public class ModuleItemFixedHolderBeam : ExtModule
    {
        /*
           "RandomAdditions.ModuleItemFixedHolderBeam": {},// Lock and load.
        */

        public bool FixateToTech = true;
        public bool AllowOtherTankCollision = true;

        private List<Vector3> HandoffAnimPos = new List<Vector3>();
        private List<Visible> HandoffAnim = new List<Visible>();
        private float fetchedHeight = 1;
        private float fetchedHeightIncrementScale = 1;

        private ModuleItemHolder theHolder;


        protected override void Pool()
        {
            try
            {
                theHolder = gameObject.GetComponent<ModuleItemHolder>();
                var grabbedModuleBeam = gameObject.GetComponent<ModuleItemHolderBeam>();

                FieldInfo heightGrab = typeof(ModuleItemHolderBeam).GetField("m_BeamBaseHeight", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo incrementGrab = typeof(ModuleItemHolderBeam).GetField("m_HeightIncrementScale", BindingFlags.NonPublic | BindingFlags.Instance);

                fetchedHeight = (float)heightGrab.GetValue(grabbedModuleBeam);
                fetchedHeightIncrementScale = (float)incrementGrab.GetValue(grabbedModuleBeam);
            }
            catch { }
        }
        public override void OnAttach()
        {
            tank.Holders.HBEvent.Subscribe(OnHeartbeat);
        }
        public override void OnDetach()
        {
            tank.Holders.HBEvent.Unsubscribe(OnHeartbeat);
        }

        public void OnHeartbeat(int HartC, TechHolders.Heartbeat HartStep)
        {
            if (HartStep == TechHolders.Heartbeat.PrePass)
            {
            }
            if (HartStep == TechHolders.Heartbeat.PostPass)
            {
                VaildateHeld();
            }
        }
        public void VaildateHeld()
        {
            int countCheck = HandoffAnim.Count;
            for (int step = 0; step < countCheck; step++)
            {
                try
                {
                    Visible visStored = HandoffAnim.ElementAt(step);
                    if (visStored.IsNull())
                    {
                        //Stop managing invaild chunks
                        HandoffAnim.RemoveAt(step);
                        HandoffAnimPos.RemoveAt(step);
                        step--;
                        countCheck--;
                        //Debug.Log("RandomAdditions: GetLocalizedPosition - Removed visible at " + step);
                    }
                    bool InStack = false;
                    int fireTimes = theHolder.NumStacks;
                    for (int stepStak = 0; stepStak < fireTimes; stepStak++)
                    {
                        ModuleItemHolder.Stack stak = theHolder.GetStack(stepStak);
                        int itemLevel = stak.items.Count;
                        for (int stepLev = 0; stepLev < itemLevel; stepLev++)
                        {
                            Visible vis = stak.items.ElementAt(stepLev);
                            if (vis = visStored)
                                InStack = true;
                        }
                    }
                    if (!InStack)
                    {
                        //Stop managing invaild chunks
                        HandoffAnim.RemoveAt(step);
                        HandoffAnimPos.RemoveAt(step);
                        step--;
                        countCheck--;
                        //Debug.Log("RandomAdditions: GetLocalizedPosition - Removed visible at " + step);
                    }
                }
                catch
                {
                    Debug.Log("RandomAdditions: GetLocalizedPosition.VaildateHeld - Could not remove error");
                }
            }
        }

        /// <summary>
        /// Returns the chunk's position relative to the Tech
        /// </summary>
        /// <param name="inputPos"></param>
        /// <param name="vis"></param>
        /// <param name="stak"></param>
        /// <param name="stepLev"></param>
        /// <returns></returns>
        public Vector3 GetLocalizedPosition(Vector3 inputPos, Visible vis, ModuleItemHolder.Stack stak, int stepLev)
        {
            Vector3 final = inputPos;
            int countCheck = HandoffAnim.Count;
            if (!HandoffAnim.Contains(vis)) //Add to animation handler!
            {
                HandoffAnim.Add(vis);
                HandoffAnimPos.Add(transform.InverseTransformPoint(inputPos));
                vis.gameObject.GetComponent<ItemIgnoreCollision>().ForceIgnoreTank(tank);
                //Debug.Log("RandomAdditions: GetLocalizedPosition - Added " + vis.name);
                countCheck = HandoffAnim.Count;
            }

            if (countCheck > 0)
            {
                // ANIMATE THE CHUNK
                int index = HandoffAnim.IndexOf(vis);
                final = HandoffAnimPos.ElementAt(index);
                Vector3 offset = theHolder.UpDir * ((float)(stepLev * 2 + 1) * (0.5f * fetchedHeightIncrementScale) + fetchedHeight);
                Vector3 posInStack = transform.InverseTransformPoint(stak.BasePosWorldOffsetLocal(offset));
                if (vis.UsePrevHeldPos && ManNetwork.IsHost)
                {
                    float beat = 1f - (tank.Holders.NextHeartBeatTime - Time.time) / tank.Holders.CurrentHeartbeatInterval;
                    //Debug.Log("RandomAdditions: GetLocalizedPosition.VaildateHeld - " + beat);
                    final = Vector3.Lerp(final, posInStack, Maths.SinEaseInOut(beat));
                    if (beat == Mathf.Infinity)
                    {
                        final = posInStack;//LOCK POSITION
                    }
                    if (beat >= 0.9f)
                    {
                        vis.UsePrevHeldPos = false;
                    }
                }
                else
                {
                    final = Vector3.Lerp(final, posInStack, 0.1f);
                }
                HandoffAnimPos[index] = final;
            }

            return transform.TransformPoint(final);
        }

        public void FixedUpdate()
        {
            if (FixateToTech && block.IsAttached)
            {
                int fireTimes = theHolder.NumStacks;
                for (int step = 0; step < fireTimes; step++)
                {
                    ModuleItemHolder.Stack stak = theHolder.GetStack(step);
                    int itemLevel = stak.items.Count;
                    for (int stepLev = 0; stepLev < itemLevel; stepLev++)
                    {
                        Visible vis = stak.items.ElementAt(stepLev);

                        Vector3 posSet = GetLocalizedPosition(vis.centrePosition, vis, stak, stepLev);
                        Quaternion rotSet = Quaternion.AngleAxis(Time.deltaTime * 72f, theHolder.UpDir) * vis.trans.rotation;

                        vis.trans.SetPositionAndRotationIfChanged(posSet, rotSet);
                    }
                }
            }
        }
    }
}
