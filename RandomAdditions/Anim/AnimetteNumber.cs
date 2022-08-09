using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class AnimetteNumber : RandomAdditions.AnimetteNumber { }
namespace RandomAdditions
{
    /// <summary>
    /// Look up Seven Segment Display on the internet for more information.
    /// Just slap this with the appropreate children GameObjects to make it work
    /// One number from 0-9
    /// </summary>
    public class AnimetteNumber : AniLinear
    {
        private Transform A;
        private Transform B;
        private Transform C;
        private Transform D;
        private Transform E;
        private Transform F;
        private Transform G;
        private byte num = 1;

        protected override void Setup()
        {
            enabled = true;
            try
            {
                A = transform.Find("_A");
                B = transform.Find("_B");
                C = transform.Find("_C");
                D = transform.Find("_D");
                E = transform.Find("_E");
                F = transform.Find("_F");
                G = transform.Find("_G");
            }
            catch
            {
                LogHandler.ThrowWarning("Make sure SevenSegDisplay has _A through _G declared as valid GameObjects!");
                enabled = false;
            }
        }
        protected override void UpdateTrans(float currentTime) 
        {
            int num = Mathf.RoundToInt((currentTime * 10f) + 0.025f);
            //DebugRandAddi.Log("AnimetteNumber in " + name + " set to " + num);
            SnapToNumber(num);
        }
        internal void SnapToNumber(int num)
        {
            if (this.num != num)
            {
                switch (num)
                {
                    case 1:
                        A.gameObject.SetActive(false);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(false);
                        E.gameObject.SetActive(false);
                        F.gameObject.SetActive(false);
                        G.gameObject.SetActive(false);
                        break;
                    case 2:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(false);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(true);
                        F.gameObject.SetActive(false);
                        G.gameObject.SetActive(true);
                        break;
                    case 3:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(false);
                        F.gameObject.SetActive(false);
                        G.gameObject.SetActive(true);
                        break;
                    case 4:
                        A.gameObject.SetActive(false);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(false);
                        E.gameObject.SetActive(false);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(true);
                        break;
                    case 5:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(false);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(false);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(true);
                        break;
                    case 6:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(false);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(true);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(true);
                        break;
                    case 7:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(false);
                        E.gameObject.SetActive(false);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(false);
                        break;
                    case 8:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(true);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(true);
                        break;
                    case 9:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(false);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(true);
                        break;
                    default:
                        A.gameObject.SetActive(true);
                        B.gameObject.SetActive(true);
                        C.gameObject.SetActive(true);
                        D.gameObject.SetActive(true);
                        E.gameObject.SetActive(true);
                        F.gameObject.SetActive(true);
                        G.gameObject.SetActive(false);
                        this.num = 0;
                        return;
                }
                this.num = (byte)num;
            }
        }
    }
}
