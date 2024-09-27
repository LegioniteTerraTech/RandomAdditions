using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

public class CustomScenery : ModLoadable
{
    [JsonIgnore]
    internal Transform prefab;

    public string Name = "Barry";
    public string Description = "A basic tree scenery";
    public string PrefabName = SceneryTypes.ConeTree.ToString();
    public string MeshName = "BarryMesh";
    public string TextureName = "BarryTex";
    public float GroundRadius = 2.5f;
    public float MinHeightOffset = 0;
    public float MaxHeightOffset = 0;
    public float Health = 50;
    public bool HostileFlora = false;
    public ManDamage.DamageableType DamageableType = ManDamage.DamageableType.Wood;
}
