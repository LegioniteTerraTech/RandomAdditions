using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TerraTechETCUtil;
using RandomAdditions.Minimap;

namespace RandomAdditions.RailSystem
{
    public class UIMiniMapLayerTrain : UIMiniMapLayerExt
	{
		private const float railIconLengthRescale = 0.0375f;
		private const float railIconWidthRescaleWorldMap = 0.15f;
		private const float railIconWidthRescaleMiniMap = 0.075f;

		private static List<UIMiniMapLayerTrain> layersManaged = new List<UIMiniMapLayerTrain>();

		public static void RemoveAllPre()
		{
			foreach (var item in new List<UIMiniMapLayerTrain>(layersManaged))
			{
				item.PurgeAllIcons();
			}
		}

		private Dictionary<RailType, IconPool> iconCache = new Dictionary<RailType, IconPool>();

		protected override void Init()
		{
			fetchedTracks = new HashSet<RailTrack>();
			layersManaged.Add(this);
		}
		protected override void Show()
		{
			UpdateTrainRoutesImmedeate();
		}
		protected override void Hide()
		{
			ClearAllIcons();
		}
		protected override void Recycle()
		{
			PurgeAllIcons();
			layersManaged.Remove(this);
		}

		public override void OnUpdateLayer()
		{
			UpdateTrainRoutesImmedeate();
		}

		private static Color trackColorBase = new Color(1f, 1f, 1f, 0.8f);
		private static Color trackColorDefault = new Color(0.1f, 0.1f, 0.1f, 1);
		private static Color trackColorDefaultC = new Color(0.1f, 0.1f, 0.1f, 0.325f);
        private HashSet<RailTrack> fetchedTracks = null;
		private int trainRouteStep = 0;
		private void UpdateTrainRoutesImmedeate()
		{
			if (WorldMap && ManRails.fakeNodeStart != null)
				UpdateTrainRouteStep(ManRails.fakeNodeStart, false, true);
			while (trainRouteStep < ManRails.AllRailNodes.Count)
			{
				UpdateTrainRouteStep(ManRails.AllRailNodes.Values.ToList().ElementAt(trainRouteStep), WorldMap, false);
				trainRouteStep++;
			}
			ClearUnusedIcons();
		}

		private void UpdateTrainRoutesSlow()
		{
			if (trainRouteStep < ManRails.AllRailNodes.Count)
			{
				UpdateTrainRouteStep(ManRails.AllRailNodes.Values.ToList().ElementAt(trainRouteStep), WorldMap, false);
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

		private static List<Vector3> RailSegIterateCache = new List<Vector3>();
		private void UpdateTrainRouteStep(RailTrackNode node, bool useThick, bool isFake)
		{
			//float scaleMap = m_MapDisplay.WorldToUIUnitRatio * m_MapDisplay.CurrentZoomLevel;
			float rad = Singleton.playerTank ? Singleton.playerTank.Radar.GetRange(ModuleRadar.RadarScanType.Techs) : 50f;
			rad *= rad;
			foreach (var item2 in node.GetAllConnectedLinks())
			{
				var track = item2.LinkTrack;
				if (track != null && !fetchedTracks.Contains(track))
				{
					fetchedTracks.Add(track);
					RailSegIterateCache.Clear();
					track.GetRailSegmentPositions(RailSegIterateCache);
					// Only displays RailTracks longer than length 64 to prevent clutter
					int startNodeID = track.StartNode.NodeID;
					int endNodeID = track.EndNode.NodeID;

					Vector3 vec = RailSegIterateCache[0] - m_MapDisplay.FocalPoint.ScenePosition;
					Vector2 relVec = vec.ToVector2XZ();
					CalculateIconPosition(relVec, false, rad, 0, out Vector2 posPrev);
					for (int step = 1; step < RailSegIterateCache.Count; step++)
					{
						vec = RailSegIterateCache[step] - this.m_MapDisplay.FocalPoint.ScenePosition;
						relVec = vec.ToVector2XZ();
						CalculateIconPosition(relVec, false, rad, 0, out Vector2 posNext);
						if (WorldMap || relVec.sqrMagnitude <= rad)
						{
							float dist = (posNext - posPrev).magnitude;
							float rot = Vector2.SignedAngle(Vector2.up, (posNext - posPrev).normalized);
							var icon = SpawnTrackIconFromCache(track.Type, (posNext + posPrev) / 2, dist, rot, useThick, isFake);
							icon.EnableTooltip(startNodeID + " <-> " + endNodeID, UITooltipOptions.Default);
							//DebugRandAddi.Log("MinimapExtended: Placed " + startNodeID + " <-> " + endNodeID + " railtrack section number "
							//	+ step + " with magnitude " + dist);
						}
						posPrev = posNext;
					}
				}
			}
		}

		private UIMiniMapElement SpawnTrackIconFromCache(RailType type, Vector2 pos, float length, float rotation, bool useThick, bool useFake)
		{
            InsureIcons();
			if (iconCache.TryGetValue(type, out var val))
			{
				var prefab = val.ReuseOrSpawn(m_RectTrans);
				Color colorSet = new Color(0, 0, 0, 1f);
                switch (type)
                {
                    case RailType.LandGauge2:
                        colorSet = new Color(1, 0.1f, 0.1f, 1);
						break;
                    case RailType.LandGauge3:
                        colorSet = trackColorDefault;
						break;
                    case RailType.LandGauge4:
                        colorSet = new Color(1, 1f, 0.1f, 1);
						break;
                    case RailType.BeamRail:
                        colorSet = new Color(0.1f, 0.1f, 1f, 1);
						break;
                    case RailType.Revolver:
                        break;
                    case RailType.Funicular:
                        break;
                    default:
                        colorSet = trackColorDefault;
						break;
                }
				if (useFake)
					colorSet = Color.Lerp(colorSet, new Color(0, 0, 0, 0), 0.5f);

                prefab.Icon.color = colorSet;

                prefab.RectTrans.localRotation = Quaternion.Euler(0, 0, rotation);
				if (useThick)
				{
					prefab.RectTrans.localScale = new Vector3(railIconWidthRescaleWorldMap, length * railIconLengthRescale, railIconWidthRescaleWorldMap);
					//float rescaleVertH = railIconWidthRescaleWorldMap / (length * railIconLengthRescale * 2);
					//prefab.RectTrans.offsetMax = new Vector3(1, rescaleVertH);
					//prefab.RectTrans.offsetMin = new Vector3(1, -rescaleVertH);
				}
				else
				{
					prefab.RectTrans.localScale = new Vector3(railIconWidthRescaleMiniMap, length * railIconLengthRescale, railIconWidthRescaleMiniMap);
					//float rescaleVertH = railIconWidthRescaleMiniMap / (length * railIconLengthRescale * 2);
					//prefab.RectTrans.offsetMax = new Vector3(1, rescaleVertH);
					//prefab.RectTrans.offsetMin = new Vector3(1, -rescaleVertH);
				}
				//prefab.RectTrans.sizeDelta = new Vector2(1, length);
				prefab.RectTrans.localPosition = GetIconPos3D(pos, ManRadar.IconType.AreaQuest);
				return prefab;
			}
			throw new Exception("UIMiniMapLayerTrain: SpawnIconFromCache was given RailType " + type.ToString() 
				+ " that has no icons. Could not fetch track!");
		}

		private void InsureIcons()
		{
			if (iconCache.Count != 0)
				return;
			UIMiniMapElement mainPrefab = ManRadar.inst.GetIconElementPrefab(ManRadar.IconType.FriendlyVehicle);
			if (mainPrefab != null)
			{
                for (int step = 0; step < Enum.GetValues(typeof(RailType)).Length; step++)
                {
					RailType item = (RailType)step;
					UIMiniMapElement newPrefab = mainPrefab.UnpooledSpawn();
					ModContainer MC = ManMods.inst.FindMod("Random Additions");
					Texture2D Tex = ResourcesHelper.GetTextureFromModAssetBundle(MC, "MapTrack_" + item.ToString());
					if (Tex != null)
					{
						var im = newPrefab.GetComponent<Image>();
						im.sprite = Sprite.Create(Tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
						im.type = Image.Type.Tiled;
					}
					else
					{
						var im = newPrefab.GetComponent<Image>();
						Tex = new Texture2D(2, 2);
						Tex.SetPixels(0,0,2,2, new Color[4] { trackColorBase, trackColorBase, trackColorBase, trackColorBase });
						Tex.Apply(false, false);
						im.sprite = Sprite.Create(Tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
						//im.type = Image.Type.Tiled;
						newPrefab.TrackedVis = null;
					}
					newPrefab.Icon.color = trackColorDefault;
					newPrefab.gameObject.SetActive(false);
					iconCache.Add(item, new IconPool(newPrefab, RailSegment.segmentPoolInitSize));
				}
			}
			else
				throw new Exception("UIMiniMapLayerTrain: InsureIcons could not fetch main prefab for icon!");
		}

		public void ClearUnusedIcons()
		{
			trainRouteStep = 0;
			fetchedTracks.Clear();
			foreach (var item in iconCache)
			{
				item.Value.RemoveAllUnused();
				item.Value.Reset();
			}
		}
		public void ClearAllIcons()
		{
			trainRouteStep = 0;
			fetchedTracks.Clear();
			foreach (var item in iconCache)
			{
				item.Value.RemoveAll();
			}
		}

		internal void PurgeAllIcons()
		{
			if (iconCache.Count == 0)
				return;
            foreach (var item in iconCache)
			{
				item.Value.DestroyAll();
			}
			iconCache.Clear();
		}

	}
}
