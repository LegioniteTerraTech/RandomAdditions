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
    [JsonIgnore]
    internal ModContainer mod;

    [Doc("The name of the resource chunk")]
    public string Name = "Terry";
    [Doc("The description of the resource chunk")]
    public string Description = "A basic resource chunk";
    [Doc("The ingame Prefab to use for this. See the _Export folder for exported Chunks for more details.")]
    public string PrefabName = ChunkTypes.Wood.ToString();
    [Doc("The custom mesh name to use for the Chunk")]
    public string MeshName = "TerryMesh";
    [Doc("The custom texture/material to use for the Chunk")]
    public string TextureName = "TerryTex";
    [Doc("The health of the chunk")]
    public float Health = 50;
    [Doc("The damageable type to use for damage recieving")]
    public ManDamage.DamageableType DamageableType = ManDamage.DamageableType.Standard;
    [Doc("The rarity of the resource")]
    public ChunkRarity Rarity = ChunkRarity.Common;
    [Doc("How heavy the Chunk is")]
    public float Mass = 0.25f;
    [Doc("How much this sells for in BB.")]
    public int Cost = 8;
    [Doc("The friction of the Chunk when it is moving.")]
    public float DynamicFriction = 0.8f;
    [Doc("The friction of the Chunk when it is stationary.")]
    public float StaticFriction = 0.8f;
    [Doc("The bounciness of the Chunk.")]
    public float Restitution = 1;
}
