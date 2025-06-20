using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

public class AnimetteController : RandomAdditions.AnimetteController { }
namespace RandomAdditions
{
    public enum AnimLoopWay
    {
        Trip,
        BackAndForth,
        OneWayRepeat,
    }

    public enum AnimCondition
    {
        None,
        Any,

        // Block General
        Attached,
        Anchored,

        // Block specific
        ManagerManaged,
        Clickable,
        WeaponSpooling,

        // RA 
        TileLoader,
        WeaponSwitch,
        Tether,
        Rails,

        // Projectiles
        TorpedoProjectile,
        ProxProjectile,
    }


    /// <summary>
    /// Runs crude block animations
    /// </summary>
    public class AnimetteController : ChildModule
    {
        // All
        public bool DefaultOnStart = false;
        public bool DefaultOnEnd = false;
        public float DefaultPosition = 0; //[0-1] The position this sets to when removed

        public AnimLoopWay Way = AnimLoopWay.BackAndForth;

        // Standalone
        public AnimCondition Condition = AnimCondition.Any;
        public float Speed = 1;
        public bool Repeating = false;
        public AniLinear[] lin { 
            get 
            {
                if (animettes == null)
                    GetAnimettes();
                return animettes;
            }
            set
            {
                animettes = value;
            }
        }


        // Non-Public
        public float TimeLength => 1 / Speed;
        private bool use100 = false;
        private AniLinear[] animettes;
        private bool returning = false;
        private bool fetched = false;
        private bool digits = false;
        private int MaxNum = 0;

        public float CurrentTime { get; private set; } = 0;
        public bool IsReversing => Speed < 0;

        protected override void Pool()
        {
            //DebugRandAddi.Log("RandomAdditions: AnimetteController - ON POOL");
        }
        protected override void PostPoolScenery()
        {
            //DebugRandAddi.Log("RandomAdditions: AnimetteController - ON POST POOL(Scenery)");
            GetAnimettes();
        }
        protected override void PostPoolBlock()
        {
            //DebugRandAddi.Log("RandomAdditions: AnimetteController - ON POST POOL(Block)");

            GetAnimettes();
        }
        protected void GetAnimettes()
        {
            if (fetched)
                return;
            AniLinear[] temp = GetComponentsInChildren<AniLinear>(true);
            List<AniLinear> lins = new List<AniLinear>();
            foreach (var item in temp)
            {
                if (item.GetComponentInParents<AnimetteController>(false) == this)
                {
                    item.Init();
                    lins.Add(item);
                }
            }
            lin = lins.ToArray();
            if (lin != null && lin.Length > 0)
            {
                digits = true;
                foreach (var item in lin)
                {
                    if (!(item is AnimetteNumber))
                        digits = false;
                    if (digits)
                    {
                        if (MaxNum == 0)
                            MaxNum = 8;
                        else if (MaxNum == 8)
                            MaxNum = 10;
                        else
                            MaxNum = MaxNum * 10;
                    }
                    item.time = TimeLength;
                    item.Init(this);
                }
                if (lin.Length == 3)
                {
                    use100 = true;
                }
                DebugRandAddi.Info("AnimetteController in " + name + " has grabbed " + lin.Length + " Linear Animettes");
                DefaultPosition = Mathf.Clamp01(DefaultPosition);
                SetState(DefaultPosition);
                enabled = false;
            }
            else
                BlockDebug.ThrowWarning(true, "AnimetteController in " + gameObject.name + " expects an Animette in hierachy, but there is none!");
            fetched = true;
            if (Condition == AnimCondition.ManagerManaged)
                enabled = false;
        }

        public override void OnAttach()
        {
            DebugRandAddi.Info("RandomAdditions: AnimetteController - ON ATTACH");
            switch (Condition)
            {
                case AnimCondition.Attached:
                    Speed = Mathf.Abs(Speed);
                    Run();
                    break;
                case AnimCondition.Anchored:
                    block.tank.AnchorEvent.Subscribe(RunAnchor);
                    RunAnchor(null, tank.IsAnchored, false);
                    break;
                default:
                    break;
            }
        }

        public override void OnDetach()
        {
            DebugRandAddi.Info("RandomAdditions: AnimetteController - ON DETACH");
            switch (Condition)
            {
                case AnimCondition.Attached:
                    Speed = -Mathf.Abs(Speed);
                    Run();
                    break;
                case AnimCondition.Anchored:
                    block.tank.AnchorEvent.Unsubscribe(RunAnchor);
                    RunAnchor(null, false, false);
                    Stop();
                    break;
                default:
                    Stop();
                    break;
            }
        }

        public void RunAnchor(ModuleAnchor module, bool anchored, bool unused)
        {
            DebugRandAddi.Info("RandomAdditions: AnimetteController - RUN ANCHOR " + anchored);
            RunBool(anchored);
        }

        public void RunBool(bool Deploy)
        {
            if (Deploy)
            {
                Speed = Mathf.Abs(Speed);
                Run();
            }
            else
            {
                Speed = -Mathf.Abs(Speed);
                Run();
            }
        }


        public void Run()
        {
            DebugRandAddi.Info("RUNNING ANIMATION");
            if (DefaultOnStart)
                SetState(DefaultPosition);
            returning = Way == AnimLoopWay.BackAndForth;
            enabled = true;
        }
        public void RunOnce()
        {
            if (DefaultOnStart)
                SetState(DefaultPosition);
            else
                SetState(0);
            Repeating = false;
            returning = Way == AnimLoopWay.BackAndForth;
            enabled = true;
        }
        public void RunRepeat()
        {
            if (DefaultOnStart)
                SetState(DefaultPosition);
            Repeating = true;
            enabled = true;
        }

        public void Update()
        {
            CurrentTime += Time.deltaTime * Speed;
            if (CurrentTime < 0 || CurrentTime > 1)
            {
                if (returning)
                {
                    Rebound();
                    returning = false;
                }
                else if (Repeating)
                {
                    Rebound();
                }
                else
                {
                    Stop();
                }
            }
            foreach (var item in lin)
            {
                item.UpdateThis(CurrentTime);
            }
        }

        public void Rebound()
        {
            switch (Way)
            {
                case AnimLoopWay.Trip:
                    SetState(Mathf.PingPong(CurrentTime, 1));
                    Speed = -Speed;
                    //DebugRandAddi.Log("ANIMATION REBOUND " + currentTime + "  sped " + Speed);
                    break;
                case AnimLoopWay.OneWayRepeat:
                    SetState(Mathf.Repeat(CurrentTime, 1));
                    //DebugRandAddi.Log("ANIMATION REPEAT " + currentTime + "  sped " + Speed);
                    break;
                default:
                    SetState(Mathf.PingPong(CurrentTime, 1));
                    Speed = -Speed;
                    //DebugRandAddi.Log("ANIMATION REBOUND " + currentTime + "  sped " + Speed);
                    break;
            }
        }
        public void Stop()
        {
            //DebugRandAddi.Log("ANIMATION STOPPED " + currentTime);
            if (DefaultOnEnd)
                SetState(DefaultPosition);
            else
            {
                SetState(Mathf.Clamp01(CurrentTime));
            }
            returning = false;
            Repeating = false;
            enabled = false;
        }


        public void SetState(float number)
        {
            CurrentTime = number;
            if (digits)
            {
                DisplayOnDigits(Mathf.RoundToInt(MaxNum * number), Mathf.RoundToInt(100 * number));
            }
            else
            {
                foreach (var item in lin)
                {
                    item.UpdateThis(CurrentTime);
                }
            }
        }

        public void DisplayOnDigits(int number, int number100)
        {
            if (use100)
                number = number100;
            int numControls = lin.Length;
            for (int step = 0; step < numControls; step++)
            {
                int numval = Mathf.RoundToInt((int)(number / Mathf.Pow(10, step))) % 10;
                //DebugRandAddi.Log("ANIMATION NUMBER EVAL " + numval + " ON DIGIT " + step);
                lin[step].UpdateThis(numval / 10f);
            }
        }
    }
}
