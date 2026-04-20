using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SafeSaves;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    [AutoSaveComponent]
    public class ModuleReplicaPoster : ModuleReplica
    {
        [SSaveField]
        private string subjectFileName = string.Empty;
        private Texture2D subjectActual = null;
        protected MeshRenderer texHolder = null;
        protected override void Pool()
        {
            base.Pool();
            texHolder = displayTrans.GetComponent<MeshRenderer>();
            if (texHolder == null)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: \nModuleReplicaPoster NEEDS a GameObject in hierarchy named \"_display\" for the display effect that also has a valid model!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                enabled = false;
                block.damage.SelfDestruct(0.1f);
                return;
            }
            if (texHolder.material == null)
            {
                var defaultMat = ResourcesHelper.GetMaterialFromBaseGameAllFast("default");
                texHolder.material = new Material(defaultMat);
                texHolder.material.mainTexture = UIHelpersExt.NullSprite.texture;
            }
        }
        public void INSURE_VALID_FILE()
        {
            if (subjectFileName == null)
                subjectFileName = string.Empty;
        }

        protected override object GetTheSubjectRef()
        {
            INSURE_VALID_FILE();
            if (ManReplicas.TryGetReplica(subjectFileName, out subjectActual))
            {
                texHolder.material.mainTexture = subjectActual;
            }
            return subjectActual;
        }
        internal override void ReleaseTheSubjectIfAny()
        {
            texHolder.material.mainTexture = UIHelpersExt.NullSprite.texture;
        }
        protected override void OnRescaled(float newScale)
        {
            texHolder.transform.localScale = newScale * Vector3.one;
        }


        protected override void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {   // On Save (non-snap)
                    this.SerializeToSafe();
                }
                else
                {   //Load from Save
                    try
                    {
                        if (this.DeserializeFromSafe())
                        {
                            LoadTheSubject();
                            OnRescaled(ourScale);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        protected override void InsureButtonsBeforeParts(ModuleUIButtons gui)
        { 

        }

        protected override void ShowTheGUI(int id)
        {
            if (TempPortedGUI.DisplayString(subjectFileName, out var string2))
            {
                subjectFileName = string2;
                ReplaceUpdateSubjectDelayed();
            }
            GUILayout.Label("Found:");
            TempPortedGUI.DisplayBooleanNoNoise(subjectToDisplay != null);
            base.ShowTheGUI(id);
        }
    }
}
