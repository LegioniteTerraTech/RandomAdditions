using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using TerraTechETCUtil;

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

        private List<Vector3> HeldVisPos = new List<Vector3>();
        private List<Visible> HeldVis = new List<Visible>();
        private float fetchedHeight = 1;
        private float fetchedHeightIncrementScale = 1;

        private ModuleItemHolder theHolder;


        protected override void Pool()
        {
            try
            {
                theHolder = gameObject.GetComponent<ModuleItemHolder>();
                theHolder.TakeItemEvent.Subscribe(OnHeldAdd);
                theHolder.ReleaseItemEvent.Subscribe(OnHeldRemove);

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

            int countCheck = HeldVis.Count;
            for ( ; 0 < countCheck; )
            {
                try
                {
                    Visible visStored = HeldVis.ElementAt(0);
                    if (!visStored.IsNull() && visStored.isActive)
                    {
                        visStored.pickup.InitRigidbody();
                    }
                    HeldVis.RemoveAt(0);
                    HeldVisPos.RemoveAt(0);
                    countCheck--;
                }
                catch
                {
                    DebugRandAddi.LogError("RandomAdditions: ModuleItemFixedHolderBeam.OnDetach - Could not remove Held error");
                }
            }
        }

        public void OnHeartbeat(int HartC, TechHolders.Heartbeat HartStep)
        {
            /*
            if (HartStep == TechHolders.Heartbeat.PrePass)
            {
            }
            if (HartStep == TechHolders.Heartbeat.PostPass)
            {
                VaildateHeld();
            }*/
        }

        public void OnHeldAdd(Visible vis, ModuleItemHolder.Stack transferstack)
        {
            if (vis) //Add to animation handler!
            {
                if (!HeldVis.Contains(vis))
                {
                    HeldVis.Add(vis);
                    HeldVisPos.Add(transform.InverseTransformPoint(vis.centrePosition));
                    ItemIgnoreCollision.Insure(vis).ForceIgnoreTank(tank);
                    //DebugRandAddi.Log("RandomAdditions: OnHeldAdd - Added " + vis.name); 
                }
                else
                    DebugRandAddi.Assert("ModuleItemFixedHolderBeam - Was told to add a visible already present in the managed list!  Block " + gameObject.name);
            }
            else
                DebugRandAddi.Assert("ModuleItemFixedHolderBeam - Was told to add a NULL visible!  Block " + gameObject.name);
        }
        public void OnHeldRemove(Visible vis, ModuleItemHolder.Stack unused, ModuleItemHolder.Stack transferstack)
        {
            if (vis)
            {
                if (HeldVis.Contains(vis))
                {
                    int index = HeldVis.IndexOf(vis);
                    if (transferstack == null || !transferstack.myHolder.GetComponent<ModuleItemFixedHolderBeam>())
                        vis.pickup.InitRigidbody();
                    HeldVisPos.RemoveAt(index);
                    HeldVis.RemoveAt(index);
                }
                else
                    DebugRandAddi.Assert("ModuleItemFixedHolderBeam - Was told to remove a visible not present in the managed list!  Block " + gameObject.name);
                
            }
            else
                DebugRandAddi.Assert("ModuleItemFixedHolderBeam - Was told to remove a NULL visible! Block " + gameObject.name);
        }

        public void VaildateHeld()
        {
            int countCheck = HeldVis.Count;
            for (int step = 0; step < countCheck; step++)
            {
                try
                {
                    Visible visStored = HeldVis.ElementAt(step);
                    if (visStored.IsNull())
                    {
                        //Stop managing invaild chunks
                        HeldVis.RemoveAt(step);
                        HeldVisPos.RemoveAt(step);
                        step--;
                        countCheck--;
                        //DebugRandAddi.Log("RandomAdditions: GetLocalizedPosition - Removed visible at " + step);
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
                        HeldVis.RemoveAt(step);
                        HeldVisPos.RemoveAt(step);
                        visStored.pickup.InitRigidbody();
                        step--;
                        countCheck--;
                        //DebugRandAddi.Log("RandomAdditions: GetLocalizedPosition - Removed visible at " + step);
                    }
                }
                catch
                {
                    DebugRandAddi.LogError("RandomAdditions: GetLocalizedPosition.VaildateHeld - Could not remove Held error");
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
        private void HoldVisible(int heldVisIndex)
        {
            int countCheck = HeldVis.Count;

            if (countCheck > 0)
            {
                // ANIMATE THE CHUNK
                Visible vis = HeldVis.ElementAt(heldVisIndex);
                ModuleItemHolder.Stack stak = vis.holderStack;
                int stepLev = stak.items.IndexOf(vis);
                Vector3 final = HeldVisPos.ElementAt(heldVisIndex);

                Vector3 offset = theHolder.UpDir * ((float)(stepLev * 2 + 1) * (0.5f * fetchedHeightIncrementScale) + fetchedHeight);
                Vector3 posInStack = transform.InverseTransformPoint(stak.BasePosWorldOffsetLocal(offset));
                if (vis.UsePrevHeldPos && ManNetwork.IsHost)
                {
                    float beat = 1f - (tank.Holders.NextHeartBeatTime - Time.time) / tank.Holders.CurrentHeartbeatInterval;
                    //DebugRandAddi.Log("RandomAdditions: GetLocalizedPosition.VaildateHeld - " + beat);
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
                HeldVisPos[heldVisIndex] = final;

                Vector3 posSet = transform.TransformPoint(final);

                vis.trans.SetPositionIfChanged(posSet);
            }

        }

        public void Update()
        {
            if (FixateToTech && block.IsAttached)
            {
                int fireTimes = HeldVis.Count;
                for (int step = 0; step < fireTimes; step++)
                {
                    Visible vis = HeldVis.ElementAt(step);
                    Vector3 posSet = transform.TransformPoint(HeldVisPos[step]);
                    Quaternion rotSet = Quaternion.AngleAxis(Time.deltaTime * 72f, theHolder.UpDir) * vis.trans.rotation;

                    vis.trans.position = posSet;
                    vis.trans.rotation = rotSet;
                }
            }
        }
        public void FixedUpdate()
        {
            if (FixateToTech && block.IsAttached)
            {
                int fireTimes = HeldVis.Count;
                for (int step = 0; step < fireTimes; step++)
                {
                    HoldVisible(step);
                }
                /*
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
                }*/
            }
        }
    }
}
