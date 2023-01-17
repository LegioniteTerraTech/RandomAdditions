using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions.RailSystem
{
    public class UIMiniMapLayerTrain : UIMiniMapLayer
	{
		private Dictionary<RailType, IconPool> iconCache = new Dictionary<RailType, IconPool>();
		private bool WorldMap = false;


		public void InsureInit(UIMiniMapDisplay disp, RectTransform rectT)
		{
			Init(disp);
			m_RectTrans = rectT;
			if (disp.gameObject.name.GetHashCode() == "MapDisplay".GetHashCode())
				WorldMap = true;
		}

		public override void UpdateLayer()
		{
			if (Singleton.playerTank)
				UpdateTrainRoutes();
		}

		private static Color trackColorDefault = new Color(0.05f, 0.1f, 0.05f, 1);
		private const float lengthRescale = 0.0375f;
		private const float widthRescaleWorldMap = 0.15f;
		private const float widthRescaleMiniMap = 0.075f;
		private HashSet<RailTrack> fetchedTracks = new HashSet<RailTrack>();
		private int trainRouteStep = 0;
		private void UpdateTrainRoutes()
		{
			while (trainRouteStep < ManRails.AllRailNodes.Count)
			{
				UpdateTrainRouteStep();
				trainRouteStep++;
			}
			trainRouteStep = 0;
			fetchedTracks.Clear();
			foreach (var item in iconCache)
			{
				item.Value.RemoveAllUnused();
				item.Value.Reset();
			}
		}

		private void UpdateTrainRoutesSlow()
		{
			if (trainRouteStep < ManRails.AllRailNodes.Count)
			{
				UpdateTrainRouteStep();
				trainRouteStep++;
			}
			else
			{
				trainRouteStep = 0;
				fetchedTracks.Clear();
				foreach (var item in iconCache)
				{
					item.Value.RemoveAllUnused();
					item.Value.Reset();
				}
			}
		}

		private void UpdateTrainRouteStep()
		{
			var item = ManRails.AllRailNodes.ElementAt(trainRouteStep);
			var listInt = item.Value.GetAllConnectedLinks();
			//float scaleMap = m_MapDisplay.WorldToUIUnitRatio * m_MapDisplay.CurrentZoomLevel;
			float rad = Singleton.playerTank ? Singleton.playerTank.Radar.GetRange(ModuleRadar.RadarScanType.Techs) : 0f;
			rad *= rad;
			foreach (var item2 in listInt)
			{
				var track = item.Value.GetConnection(item2).LinkTrack;
				if (track != null && !fetchedTracks.Contains(track))
				{
					Vector3[] posi = track.GetRailSegmentPositions();
					int startNodeID = track.StartNode.NodeID;
					int endNodeID = track.EndNode.NodeID;

					Vector3 vec = posi[0] - m_MapDisplay.FocalPoint.ScenePosition;
					Vector2 relVec = vec.ToVector2XZ();
					CalculateIconPosition(relVec, false, rad, 0, out Vector2 posPrev);
					for (int step = 1; step < posi.Length; step++)
					{
						vec = posi[step] - this.m_MapDisplay.FocalPoint.ScenePosition;
						relVec = vec.ToVector2XZ();
						CalculateIconPosition(relVec, false, rad, 0, out Vector2 posNext);
						if (WorldMap || relVec.sqrMagnitude <= rad)
						{
							float dist = (posNext - posPrev).magnitude;
							float rot = Vector2.SignedAngle(Vector2.up, (posNext - posPrev).normalized);
							var icon = SpawnTrackIconFromCache(track.Type, (posNext + posPrev) / 2, dist, rot);
							icon.EnableTooltip(startNodeID + " <-> " + endNodeID, UITooltipOptions.Default);
							//DebugRandAddi.Log("MinimapExtended: Placed " + startNodeID + " <-> " + endNodeID + " railtrack section number "
							//	+ step + " with magnitude " + dist);
						}
						posPrev = posNext;
					}
				}
			}
		}

		private Dictionary<RailType, UIMiniMapElement> prefabs = null;
		private UIMiniMapElement SpawnTrackIconFromCache(RailType type, Vector2 pos, float length, float rotation)
		{
			InsureIcons();
			if (iconCache.TryGetValue(type, out var val))
			{
				var prefab = val.SpawnOrReuse(m_RectTrans);
                switch (type)
                {
                    case RailType.LandGauge2:
						prefab.Icon.color = new Color(1, 0.1f, 0.1f, 1);
						break;
                    case RailType.LandGauge3:
						prefab.Icon.color = trackColorDefault;
						break;
                    case RailType.LandGauge4:
						prefab.Icon.color = new Color(1, 1f, 0.1f, 1);
						break;
                    case RailType.BeamRail:
						prefab.Icon.color = new Color(0.1f, 0.1f, 1f, 1);
						break;
                    case RailType.Revolver:
                        break;
                    case RailType.InclinedElevator:
                        break;
                    default:
						prefab.Icon.color = trackColorDefault;
						break;
                }
				prefab.RectTrans.localRotation = Quaternion.Euler(0, 0, rotation);
				if (WorldMap)
					prefab.RectTrans.localScale = new Vector3(widthRescaleWorldMap, length * lengthRescale, widthRescaleWorldMap);
				else
					prefab.RectTrans.localScale = new Vector3(widthRescaleMiniMap, length * lengthRescale, widthRescaleMiniMap);
				//prefab.RectTrans.sizeDelta = new Vector2(1, length);
				prefab.RectTrans.localPosition = GetIconPos3D(pos, ManRadar.IconType.AreaQuest);
				return prefab;
			}
			throw new Exception("UIMiniMapLayerTrain: SpawnIconFromCache was given RailType " + type.ToString() 
				+ " that has no icons. Could not fetch track!");
		}

		private void InsureIcons()
		{
			if (prefabs != null)
				return;
			prefabs = new Dictionary<RailType, UIMiniMapElement>();
			UIMiniMapElement mainPrefab = ManRadar.inst.GetIconElementPrefab(ManRadar.IconType.FriendlyVehicle);
			if (mainPrefab != null)
			{
                for (int step = 0; step < Enum.GetValues(typeof(RailType)).Length; step++)
                {
					RailType item = (RailType)step;
					UIMiniMapElement newPrefab = mainPrefab.UnpooledSpawn();
					ModContainer MC = ManMods.inst.FindMod("Random Additions");
					Texture2D Tex = KickStart.GetTextureFromModAssetBundle(MC, "MapTrack_" + item.ToString());
					if (Tex != null)
					{
						var im = newPrefab.GetComponent<Image>();
						im.sprite = Sprite.Create(Tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
					}
					else
					{
						var im = newPrefab.GetComponent<Image>();
						Tex = new Texture2D(2, 2);
						Tex.SetPixels(0,0,2,2, new Color[4] { trackColorDefault , trackColorDefault , trackColorDefault , trackColorDefault });
						Tex.Apply(false, false);
						im.sprite = Sprite.Create(Tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
						newPrefab.TrackedVis = null;
					}
					newPrefab.Icon.color = trackColorDefault;
					iconCache.Add(item, new IconPool(newPrefab));
				}
			}
			else
				throw new Exception("UIMiniMapLayerTrain: InsureIcons could not fetch main prefab for icon!");
		}

		internal void PurgeAllIcons()
		{
			if (prefabs == null)
				return;
            foreach (var item in iconCache)
			{
				item.Value.DestroyAll();
			}
			iconCache.Clear();

			foreach (var item in prefabs)
			{
				item.Value.DeletePool();
				Destroy(item.Value);
			}
			prefabs = null;
		}

		internal class IconPool
		{
			private readonly UIMiniMapElement prefab;
			private Stack<UIMiniMapElement> elementsUnused = new Stack<UIMiniMapElement>();
			private Stack<UIMiniMapElement> elementsUsed = new Stack<UIMiniMapElement>();
			public List<UIMiniMapElement> ElementsActive => elementsUsed.ToList();

			internal IconPool(UIMiniMapElement prefab)
			{
				prefab.CreatePool(RailSegment.segmentPoolInitSize);
				this.prefab = prefab;
			}

			internal UIMiniMapElement SpawnOrReuse(RectTransform rectTrans)
			{
				UIMiniMapElement spawned;
				if (elementsUnused.Count > 0)
				{
					spawned = elementsUnused.Pop();
				}
				else
				{
					spawned = prefab.Spawn();
					spawned.RectTrans.SetParent(rectTrans, false);
                    foreach (var item in spawned.GetComponents<MonoBehaviour>())
                    {
						item.enabled = true;
					}
					spawned.gameObject.SetActive(true);
				}
				elementsUsed.Push(spawned);
				return spawned;
			}

			internal void Reset()
			{
				while (elementsUsed.Count > 0)
				{
					elementsUnused.Push(elementsUsed.Pop());
				}
			}
			internal void RemoveAllUnused()
			{
                while(elementsUnused.Count > 0)
                {
					elementsUnused.Pop().Recycle(false);
                }
			}
			internal void DestroyAll()
			{
				RemoveAllUnused();
				while (elementsUsed.Count > 0)
				{
					elementsUsed.Pop().Recycle(false);
				}
				prefab.DeletePool();
				Destroy(prefab);
			}
		}
	}
}
