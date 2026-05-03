using System;
using UnityEngine;
using Newtonsoft.Json;

public class CustomScenery : ModLoadable
{
    [JsonIgnore]
    internal TerrainObject prefab;
    [JsonIgnore]
    internal string ID
    {
        get => fileName; 
        set => fileName = value;
    }

    [Doc("The name of the scenery. Note the filename will be the actual ID of the scenery")]
    public string Name = "Barry";
    [Doc("The description to display for the scenery")]
    public string Description = "A basic tree scenery";
    [Doc("The ingame Prefab to use for this. See the _Export folder for exported Scenery for more details.")]
    public string PrefabName = "Biome7Tree05";
    [Doc("The custom mesh name to use for the full HP visual stage")]
    public string MeshName = "BarryMesh";
    [Doc("The custom texture/material to use for ALL stages")]
    public string TextureName = "BarryTex";
    [Doc("The expected spherical radius this takes up for pathfinding purposes")]
    public float GroundRadius = 2.5f;
    [Doc("The minimum random height this resource node should be placed at.  Does not need to intersect ground.")]
    public float MinHeightOffset = 0;
    [Doc("The maximum random height this resource node should be placed at.  Does not need to intersect ground.")]
    public float MaxHeightOffset = 0;
    [Doc("The total health of this scenery node.  Damage stages are automatically evenly spaced within this value.  Any values below or equal to zero will set this to invulnerable.")]
    public float Health = 50;
    [Doc("If this attacks any Techs.  Note some high types of Advanced AI can target and shoot hostile flora!")]
    public bool HostileFlora = false;
    public ManDamage.DamageableType DamageableType = ManDamage.DamageableType.Wood;
}
