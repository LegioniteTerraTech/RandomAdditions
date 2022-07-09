using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    public enum AnimLoopWay
    {
        Trip,
        BackAndForth,
        OneWay,
    }

    public enum AnimCondition
    {
        OnlyOther,
        Attached,
        Anchored,
    }


    /// <summary>
    /// Runs crude block animations
    /// </summary>
    public class ManAnimette : ChildModule
    {
        // All
        public bool DefaultOnRun = true;
        public bool DefaultOnEnd = true;
        public float DefaultPosition = 0; //[0-1] The position this sets to when removed

        public AnimLoopWay Way = AnimLoopWay.BackAndForth;

        // Standalone
        public AnimCondition Condition = AnimCondition.Attached;
        public float Speed = 1;
        public bool Repeating = false;


        // Non-Public
        public float TimeLength => 1 / Speed;
        private AniLinear[] lin;
        private bool returning = false;

        protected float targetTime = 0;
        protected float currentTime = 0;

        protected override void Pool()
        {
            lin = GetComponentsInChildren<AniLinear>();
            if (lin != null)
            {
                foreach (var item in lin)
                {
                    item.time = TimeLength;
                    item.Init(this);
                }
                DefaultPosition = Mathf.Clamp01(DefaultPosition);
                enabled = false;
            }
            else
                LogHandler.ThrowWarning("ManAnimette expects an Animette in hierachy, but there is none!");


        }

        public override void OnAttach()
        {
            switch (Condition)
            {
                case AnimCondition.Attached:
                    Speed = Mathf.Abs(Speed);
                    Run();
                    break;
                case AnimCondition.Anchored:
                    block.tank.AnchorEvent.Subscribe(RunAnchor);
                    break;
                default:
                    break;
            }
        }

        public override void OnDetach()
        {
            switch (Condition)
            {
                case AnimCondition.Attached:
                    Speed = -Mathf.Abs(Speed);
                    Run();
                    break;
                case AnimCondition.Anchored:
                    block.tank.AnchorEvent.Unsubscribe(RunAnchor);
                    Stop();
                    SetState(DefaultPosition);
                    break;
                default:
                    Stop();
                    SetState(DefaultPosition);
                    break;
            }
        }

        public void RunAnchor(ModuleAnchor module, bool anchored, bool unused)
        {
            RunBool(anchored);
        }

        public void RunBool(bool Deployed)
        {
            if (Deployed)
            {
                if (!currentTime.Approximately(1))
                {
                    Speed = Mathf.Abs(Speed);
                    Run();
                }
            }
            else
            {
                if (currentTime.Approximately(0))
                {
                    Speed = -Mathf.Abs(Speed);
                    Run();
                }
            }
        }


        public void Run()
        {
            if (DefaultOnRun)
                SetState(DefaultPosition);
            else
                SetState(0);
            returning = Way == AnimLoopWay.BackAndForth;
            enabled = true;
        }
        public void RunOnce()
        {
            if (DefaultOnRun)
                SetState(DefaultPosition);
            else
                SetState(0);
            Repeating = false;
            returning = Way == AnimLoopWay.BackAndForth;
            targetTime = 1;
            enabled = true;
        }
        public void RunRepeat()
        {
            if (DefaultOnRun)
                SetState(DefaultPosition);
            Repeating = true;
            enabled = true;
        }

        public void Update()
        {
            currentTime += Time.deltaTime * Speed;
            if (currentTime < 0 || currentTime > 1)
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
                item.UpdateThis(currentTime);
            }
        }

        public void Rebound()
        {
            switch (Way)
            {
                case AnimLoopWay.OneWay:
                    currentTime = Mathf.Repeat(currentTime, 1);
                    SetState(currentTime);
                    break;
                default:
                    if (Speed > 0)
                    {
                        targetTime = 0;
                    }
                    else
                    {
                        targetTime = 1;
                    }
                    currentTime = Mathf.PingPong(currentTime, 1);
                    SetState(currentTime);
                    Speed -= Speed;
                    break;
            }
        }
        public void Stop()
        {
            if (DefaultOnEnd)
                SetState(DefaultPosition);
            returning = false;
            Repeating = false;
            enabled = false;
        }


        public void SetState(float number)
        {
            currentTime = number;
            foreach (var item in lin)
            {
                item.UpdateThis(currentTime);
            }
        }
    }
}
