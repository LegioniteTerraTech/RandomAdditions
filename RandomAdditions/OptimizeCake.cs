using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using cakeslice;

namespace RandomAdditions
{
	/// <summary>
	/// Remove this later
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
						Debug.Log("RandomAdditions: OptimizeOutline acting on " + trans.name);
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
		public static void FlagNonRendTrans(Transform trans)
        {
			if (trans)
			{
				if (trans.GetComponent<NoOutline>())
					return;
				MeshFilter MF = trans.GetComponent<MeshFilter>();
				if (MF)
				{
					if (MF.sharedMesh == null)
					{
						MF.sharedMesh = tempMesh;
					}
					if (MF.sharedMesh == null)
						Debug.Log("RandomAdditions: OptimizeOutline Failed on " + trans.name);
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
				int count = trans.childCount;
				for (int step = 0; step < count; step++)
					FlagNonRendTrans(trans.GetChild(step));
			}
        }
    }
}
