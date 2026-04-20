using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SafeSaves;
using TerraTechETCUtil;
using UnityEngine;
using static Crate;

namespace RandomAdditions
{
    public interface ReplicaControllable
    {
        void GetOurButtons(ModuleUIButtons buttons);
        void DisplayOnReplicaGUI(ModuleReplica MR);
    }
    public abstract class ModuleReplica : ExtModule
    {

        protected object subjectToDisplay = null;
        protected Transform displayTrans = null;
        protected float ourScale = 1f;

        private ModuleUIButtons buttonGUI;

        protected override void Pool()
        {
            enabled = true;
            try
            {
                displayTrans = KickStart.HeavyTransformSearch(transform, "_display");
            }
            catch { }
            if (displayTrans == null)
            {
                displayTrans = transform;
                BlockDebug.ThrowWarning(true, "RandomAdditions: \nModuleReplica NEEDS a GameObject in hierarchy named \"_display\" for the display effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }
            block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
        }

        private List<ReplicaControllable> replicaControls = new List<ReplicaControllable>();
        private void InsureGetReplicaParts()
        {
            if (replicaControls.Any())
                return;
            foreach (var item in gameObject.GetComponents<ReplicaControllable>())
            {
                if (item != null)
                {
                    replicaControls.Add(item);
                }
            }
        }

        public void InsureGUI()
        {
            if (buttonGUI == null)
            {
                buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Replica", true);
                buttonGUI.AddElement(() => "Open Menu", ToggleOpenGUI, 
                    () => UIHelpersExt.GetGUIIcon("Icon_Options"));
                InsureButtonsBeforeParts(buttonGUI);
                InsureGetReplicaParts();
                foreach (var item in replicaControls)
                    item.GetOurButtons(buttonGUI);
            }
        }
        protected abstract void InsureButtonsBeforeParts(ModuleUIButtons gui);
        public float ToggleOpenGUI(float unused)
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
            return 1;
        }
        public bool DoShowTheGUI = false;
        const float displacement = 0.75f;
        public Rect ourRect = new Rect((Display.main.systemWidth - (Display.main.systemWidth * displacement)) / 2,
                    (Display.main.systemHeight - (Display.main.systemHeight * displacement)) / 2,
                    Display.main.systemWidth * displacement, 
                    Display.main.systemHeight * displacement);
        public void OnGUI()
        {
            if (DoShowTheGUI)
                AltUI.Window(90132433, ourRect, ShowTheGUI, block.name, 
                    () => { DoShowTheGUI = false; }, true, true);
        }
        private string floatString = string.Empty;
        protected virtual void ShowTheGUI(int id)
        {
            if (TempPortedGUI.DisplayFloat(ourScale, ref floatString, out float newVal))
            {
                ourScale = newVal;
                OnRescaled(ourScale);
            }
            foreach (var item in replicaControls)
                item.DisplayOnReplicaGUI(this);
        }



        public override void OnAttach()
        {
            InsureGUI();
            ManReplicas.allReps.Add(this);
            InvokeHelper.Invoke(LoadTheSubject, 0.05f);
        }
        public override void OnDetach()
        {
            ManReplicas.allReps.Remove(this);
            DoShowTheGUI = false;
        }

        internal void LoadTheSubject()
        {
            if (subjectToDisplay == null)
                subjectToDisplay = GetTheSubjectRef();
        }
        protected abstract object GetTheSubjectRef();
        internal abstract void ReleaseTheSubjectIfAny();

        public void ReplaceUpdateSubjectDelayed()
        {
            InvokeHelper.CancelInvoke(ReplaceUpdateSubjectDelayed_Internal);
            InvokeHelper.Invoke(ReplaceUpdateSubjectDelayed_Internal, 1);
        }
        private void ReplaceUpdateSubjectDelayed_Internal()
        {
            ReleaseTheSubjectIfAny();
            LoadTheSubject();
        }

        protected abstract void OnRescaled(float newScale);

        protected abstract void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec);
    }

}
