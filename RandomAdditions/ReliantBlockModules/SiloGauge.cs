using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SiloGauge : RandomAdditions.SiloGauge { };
namespace RandomAdditions
{
    public class SiloGauge : MonoBehaviour
    {
        /* Throw this within your JSONBLOCK inside of a GameObject you want to display for
        "RandomAdditions.SiloGauge":{ // Add a display for your ModuleItemSilo
            "MaxHeightMultiplier": 5,      // Scaling of the mesh at full capacity
            "MinHeightMultiplier": 1,      // Scaling of the mesh at empty
            "DisplayDampener": 3,          // Dampener for the updating of the display

            "AllowCustomTextures": false,       // Use the existing textures?
            "UseResourceColorsForGauge": false, // Use stored resource colors for the gauge?
        },
        */

        public float MaxHeightMultiplier = 5;
        public float MinHeightMultiplier = 1;
        public float DisplayDampener = 3;

        public bool AllowCustomTextures = false;
        public bool UseResourceColorsForGauge = false;


        private ModuleItemSilo siloMain;
        private bool updatingDisplay;
        private ChunkTypes displayChunk;
        private Shader shade;
        private Texture graphics;
        private Texture recoloredGraphics = Texture2D.blackTexture;
        private bool displayDamperInstant = false;
        private float ScaleToAimFor = 0;

        public void Setup(ModuleItemSilo MIS)
        {
            siloMain = MIS;
            var shader = Resources.FindObjectsOfTypeAll<Shader>().Where(s => s.name == "Standard").ElementAt(1); ////Standard
            if (shader.IsNull())
                LogHandler.ThrowWarning("RandomAdditions: \nSiloGauge: Could not find any shader!   ALERT CODER!!!");
            shade = shader;

            var meshV = gameObject.GetComponent<MeshRenderer>();
            if (!meshV)
            {
                gameObject.AddComponent<MeshRenderer>();
            }
            var meshR = gameObject.GetComponent<MeshRenderer>();
            if (AllowCustomTextures)
            {
                graphics = meshR.material.mainTexture;
            }
            else
            {
                Texture2D toReplace = Texture2D.whiteTexture;
                graphics = toReplace;
                gameObject.GetComponent<MeshRenderer>().material = new Material(shader);
            }
            if (DisplayDampener < 0.01f)
                displayDamperInstant = true;
            UpdateTextures();
        }

        public void SnapGauge()
        {
            UpdateTextures();
            displayChunk = siloMain.GetChunkType;
            ScaleToAimFor = (MaxHeightMultiplier - MinHeightMultiplier) * siloMain.GetCountPercent + MinHeightMultiplier;
            Vector3 toSet = transform.localScale;
            toSet.y = ScaleToAimFor;
            transform.localScale = toSet;
            updatingDisplay = false;
        }
        public void UpdateGauge()
        {
            if (siloMain.GetChunkType != displayChunk)
            {
                UpdateTextures();
            }
            displayChunk = siloMain.GetChunkType;
            ScaleToAimFor = (MaxHeightMultiplier - MinHeightMultiplier) * siloMain.GetCountPercent + MinHeightMultiplier;
            updatingDisplay = true;
            //DebugRandAddi.Log("RandomAdditions: ScaleToAimFor " + ScaleToAimFor + " | update " + updatingDisplay);
        }
        public void UpdateTextures()
        {
            if (!AllowCustomTextures)
            {
                Texture2D toReplace = Texture2D.whiteTexture;
                graphics = toReplace;
                gameObject.GetComponent<MeshRenderer>().material = new Material(shade);
            }
            if (UseResourceColorsForGauge)
            {
                RecolorTexture();
            }
            else
                recoloredGraphics = graphics;
            var change = gameObject.GetComponent<MeshRenderer>();
            change.material.SetColor("_Color", siloMain.GetSavedGaugeColor);
            change.material.SetTexture("_MainTex", recoloredGraphics);
        }

        public void RecolorTexture()
        {
            Color pixC = siloMain.GetSavedGaugeColor;

            Texture2D reColor = (Texture2D)graphics;
            for (int stepY = 0; graphics.height > stepY; stepY++)
            {
                for (int stepX = 0; graphics.width > stepX; stepX++)
                {
                    Color pix = reColor.GetPixel(stepX, stepY);
                    pix = (pix + pixC) / 2;
                    reColor.SetPixel(stepX, stepY, pix);
                }
            }
            recoloredGraphics = reColor;
        }

        internal void UpdateScale()
        {
            if (updatingDisplay)
            {
                StepSize();
            }
        }
        private void StepSize()
        {
            //DebugRandAddi.Log("RandomAdditions: Firing StepSize");
            Vector3 toSet = transform.localScale;
            if (displayDamperInstant)
            {
                toSet.y = ScaleToAimFor;
                transform.localScale = toSet;
                updatingDisplay = false;
            }
            else
            {
                //DebugRandAddi.Log("RandomAdditions: Firing StepSize");
                if (ScaleToAimFor - 0.01f < transform.localScale.y && transform.localScale.y < 0.01f + ScaleToAimFor)
                {
                    toSet.y = ScaleToAimFor;
                    transform.localScale = toSet;
                    updatingDisplay = false;
                    return;
                }
                float reScaleOp = ((ScaleToAimFor - transform.localScale.y) / DisplayDampener) + transform.localScale.y;
                toSet.y = reScaleOp;
                transform.localScale = toSet;
            }
        }
    }
}
