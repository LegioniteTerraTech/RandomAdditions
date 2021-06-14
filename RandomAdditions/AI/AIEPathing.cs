using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions.AI
{

    public static class AIEPathing
    {
        public static List<Tank> Allies
        {
            get {
                return AIEnhancedCore.Allies;
            }
        }


        public static Tank ClosestAlly(Vector3 tankPos, out float bestValue)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            bestValue = 500;
            int bestStep = 0;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //Debug.Log("RandomAdditions: ClosestAllyProcess " + closestTank.name);
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Crash on ClosestAllyProcess " + e);
            }
            return closestTank;
        }

        public static Tank ClosestAllyPrecision(Vector3 tankPos, out float bestValue)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            int bestStep = 0;
            Tank closestTank = null;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude - AIEnhancedCore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                    if (bestValue > temp && temp != 0)
                    {
                        bestValue = temp;
                        bestStep = stepper;
                    }
                }
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                closestTank = Allies.ElementAt(bestStep);
                //Debug.Log("RandomAdditions: ClosestAllyProcess " + closestTank.name);
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Crash on ClosestAllyPrecisionProcess " + e);
            }
            return closestTank;
        }


        public static Tank SecondClosestAlly(Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            bestValue = 500;
            auxBestValue = 500;
            int bestStep = 0;
            int auxStep = 0;
            Tank closestTank;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp && temp != 0)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                secondTank = Allies.ElementAt(auxStep);
                closestTank = Allies.ElementAt(bestStep);
                auxBestValue = (Allies.ElementAt(auxStep).rbody.position - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                //Debug.Log("RandomAdditions: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Crash on SecondClosestAllyProcess " + e);
            }
            Debug.Log("RandomAdditions: SecondClosestAlly - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }

        public static Tank SecondClosestAllyPrecision(Vector3 tankPos, out Tank secondTank, out float bestValue, out float auxBestValue)
        {
            // Finds the two closest allies and outputs their respective distances as well as their beings
            //  DEMANDS MORE PROCESSING THAN THE ABOVE
            bestValue = 500;
            auxBestValue = 500;
            int bestStep = 0;
            int auxStep = 0;
            Tank closestTank;
            try
            {
                for (int stepper = 0; Allies.Count > stepper; stepper++)
                {
                    float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude - AIEnhancedCore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                    if (bestValue > temp && temp != 0)
                    {
                        auxStep = bestStep;
                        bestStep = stepper;
                        auxBestValue = bestValue;
                        bestValue = temp;
                    }
                    else if (bestValue < temp && auxBestValue > temp && temp != 0)
                    {
                        auxStep = stepper;
                        auxBestValue = temp;
                    }
                }
                /*
                if (auxBestValue == 500 && Allies.Count > 2)
                { //TRY AGAIN
                    for (int stepper = Allies.Count; 0 < stepper; stepper--)
                    {
                        float temp = (Allies.ElementAt(stepper).rbody.position - tankPos).sqrMagnitude - AIEnhancedCore.Extremes(Allies.ElementAt(stepper).blockBounds.extents);
                        if (bestValue > temp && temp != 0)
                        {
                            auxStep = bestStep;
                            bestStep = stepper;
                            auxBestValue = bestValue;
                            bestValue = temp;
                        }
                    }
                }
                if (auxBestValue == 500)
                    Debug.Log("RandomAdditions: SecondClosestAllyPrecisionProcess EPIC FAIL!");
                */
                secondTank = Allies.ElementAt(auxStep);
                closestTank = Allies.ElementAt(bestStep);
                auxBestValue = (Allies.ElementAt(auxStep).rbody.position - tankPos).magnitude;
                bestValue = (Allies.ElementAt(bestStep).rbody.position - tankPos).magnitude;
                //Debug.Log("RandomAdditions: ClosestAllyProcess " + closestTank.name);
                return closestTank;
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Crash on SecondClosestAllyPrecisionProcess " + e);
            }
            Debug.Log("RandomAdditions: SecondClosestAllyPrecision - COULD NOT FETCH TANK");
            secondTank = null;
            return null;
        }

    }
}
