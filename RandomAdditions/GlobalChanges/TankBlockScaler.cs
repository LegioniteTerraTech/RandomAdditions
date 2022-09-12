using UnityEngine;

namespace RandomAdditions
{
    public class TankBlockScaler : MonoBehaviour
    {
        //Rescale blocks to chunk size when put on conveyor
        /*
           "RandomAdditions.TankBlockScaler": {
                "AimedDownscale": 0.5, // Multiplier for the scale on downsizing
                // Minimum 0.01, Maximum 0.5.
            },
        */
        private const int stepsAllowedUntilSnap = 80;
        private const float scaleLerp = 5;
        private bool Downscale = true;

        public float AimedDownscale = 0.5f;
        public int attempts = stepsAllowedUntilSnap;

        public void OnPool()
        {
            if (AimedDownscale < 0 || AimedDownscale > 0.5)
            {
                //DebugRandAddi.Log("RandomAdditions: TankBlockScaler value is invalid on block " + gameObject.name + "!  Overriding to 0.5!");
                AimedDownscale = 0.5f;
            }
        }

        public void DownScale(bool downscale)
        {
            Downscale = downscale;
            enabled = true;
        }

        private void Update()
        {
            //Only activate this behaviour to rescale
            if (attempts > 0)
            {
                attempts--;
                Rescale();
            }
            else
            {
                RescaleSnap();
            }
        }
        private void Rescale()
        {
            if (Downscale)
            {
                //DebugRandAddi.Log("RandomAdditions: Firing Rescale Down for " + gameObject.name);
                if (transform.localScale.y < 0.1f + AimedDownscale)
                {
                    transform.localScale = AimedDownscale * Vector3.one;
                    attempts = stepsAllowedUntilSnap;
                    enabled = false;
                    return;
                }
                float reScaleOp = ((AimedDownscale - transform.localScale.y) / scaleLerp) + transform.localScale.y;
                transform.localScale = reScaleOp * Vector3.one;
            }
            else
            {
                //DebugRandAddi.Log("RandomAdditions: Firing Rescale Up for " + gameObject.name);
                if (0.98f < transform.localScale.y)
                {
                    transform.localScale = Vector3.one;
                    attempts = stepsAllowedUntilSnap;
                    enabled = false;
                    return;
                }
                float reScaleOp = ((1 - transform.localScale.y) / scaleLerp) + transform.localScale.y;
                transform.localScale = reScaleOp * Vector3.one;
            }
        }
        private void RescaleSnap()
        {
            if (Downscale)
            {
                transform.localScale = AimedDownscale * Vector3.one;
                attempts = stepsAllowedUntilSnap;
                enabled = false;
            }
            else
            {
                transform.localScale = Vector3.one;
                attempts = stepsAllowedUntilSnap;
                enabled = false;
            }
        }
    }
}
