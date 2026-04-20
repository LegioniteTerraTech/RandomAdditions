using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using cakeslice;

namespace RandomAdditions
{
	/// <summary>
	/// Adds in a fake, nearly invisible mesh at the root block GameObject level to silence OutlineEffect's massive log spam. 
	/// Will remove this later when it is fixed.
	/// </summary>
    internal class OptimizeOutline : MonoBehaviour
	{
		static OptimizeOutline inst;
		static Mesh tempMesh = new Mesh();
		static List<Transform> toDo = new List<Transform>();
		private bool queued = false;

		public static void Initiate()
		{
			if (!inst)
			{
				inst = new GameObject("OptimizeOutline").AddComponent<OptimizeOutline>();
				Vector3[] vecs = new Vector3[3] { Vector3.zero * 0.001f, Vector3.one * 0.001f, Vector3.up * 0.001f };
				tempMesh = PrismMeshGenerator.GenerateMesh(vecs, Vector3.one * 0.001f, 1);
			}
			if (!inst.queued)
			{
				inst.Invoke("FlagNonRendTransCases", 0.001f);
				inst.queued = true;
			}
		}


		public void FlagNonRendTransCases()
		{
            foreach (var trans in toDo)
            {
				if (trans)
				{
					try
					{
						DebugRandAddi.Log("RandomAdditions: OptimizeOutline acting on " + trans.name);
						FlagNonRendTrans(trans);
					}
					catch { }
				}
			}
			toDo.Clear();
			inst.queued = false;
		}
		public static void FlagNonRendTransStart(Transform trans)
		{
			if (trans)
			{
				Initiate();
				toDo.Add(trans);
			}
		}
        /// <summary>
        /// "cakeslice" spams the logs if it encounters a MeshFilter with no mesh and no "NoOutline" to stop it beforehand.
        /// Returns true if there is a mesh above
        /// </summary>
        /// <param name="trans"></param>
        /// <returns></returns>
        public static bool FlagNonRendTrans(Transform trans)
        {
			if (trans)
			{
				if (trans.GetComponent<NoOutline>())
					return false;
				bool hasMeshAboveThis = false;
                int count = trans.childCount;
				for (int step = 0; step < count; step++)
				{	// Go to the top and get if any branch has mesh
					if (FlagNonRendTrans(trans.GetChild(step)))
						hasMeshAboveThis = true;

                }
                MeshFilter MF = trans.GetComponent<MeshFilter>();
				if (MF)
				{	// There is a mesh renderer here!
					if (MF.sharedMesh == null)
					{   // HAS NO MESH! THIS WILL LOG SPAM SO PUT PLACEHOLDER HERE
						if (!hasMeshAboveThis)
						{   // There was no mesh needed to be displayed before this. We flag the end for cakeslice.
							trans.gameObject.AddComponent<NoOutline>();
							return false;
						}
						else
                        {	// Cannot use NoOutline as there is a visible mesh above this, so the mesh has to be active
                            MF.sharedMesh = tempMesh;
                        }
					}
					if (MF.sharedMesh == null)
						DebugRandAddi.Log("RandomAdditions: OptimizeOutline Failed on " + trans.name);
					return true;
				}
				/*
				else
				{
					Renderer Rend;
					if (MF)
						Rend = trans.GetComponent<Renderer>();
					else
						Rend = trans.GetComponent<SkinnedMeshRenderer>();
				}*/
				return hasMeshAboveThis;
            }
			return false;
        }
    }
}
