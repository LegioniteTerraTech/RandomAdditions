using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

public class CustomChunk : ModLoadable
{
    [JsonIgnore]
    internal Transform prefab;
    [JsonIgnore]
    internal ResourceManager.ResourceDefWrapper prefabBase;

    public string Name = "Terry";
    public string Description = "A basic resource chunk";
    public string PrefabName = ChunkTypes.Wood.ToString();
    public string MeshName = "TerryMesh";
    public string TextureName = "TerryTex";
    public float Health = 50;
    public ChunkRarity Rarity = ChunkRarity.Common;
    public float Mass = 0.25f;
    public int Cost = 8;
    public float DynamicFriction = 0.8f;
    public float StaticFriction = 0.8f;
    public float Restitution = 1;
}
