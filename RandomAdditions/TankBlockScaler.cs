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

        public bool Downscale = true;
        public float AimedDownscale = 0.5f;
        public int attempts = 25;

        public void OnPool()
        {
            if (AimedDownscale < 0 || AimedDownscale > 0.5)
            {
                //Debug.Log("RandomAdditions: TankBlockScaler value is invalid on block " + gameObject.name + "!  Overriding to 0.5!");
                AimedDownscale = 0.5f;
            }
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
                attempts = 25;
                enabled = false;
            }
        }
        private void Rescale()
        {
            if (Downscale)
            {
                //Debug.Log("RandomAdditions: Firing Rescale Down for " + gameObject.name);
                if (AimedDownscale - 0.1f < transform.localScale.y && transform.localScale.y < 0.1f + AimedDownscale)
                {
                    transform.localScale = AimedDownscale * Vector3.one;
                    attempts = 25;
                    enabled = false;
                    return;
                }
                float reScaleOp = ((AimedDownscale - transform.localScale.y) / 4) + transform.localScale.y;
                transform.localScale = reScaleOp * Vector3.one;
            }
            else
            {
                //Debug.Log("RandomAdditions: Firing Rescale Up for " + gameObject.name);
                if (0.9f < transform.localScale.y && transform.localScale.y < 1.1f)
                {
                    transform.localScale = Vector3.one;
                    attempts = 25;
                    enabled = false;
                    return;
                }
                float reScaleOp = ((1 - transform.localScale.y) / 3) + transform.localScale.y;
                transform.localScale = reScaleOp * Vector3.one;
            }
        }
    }
}
