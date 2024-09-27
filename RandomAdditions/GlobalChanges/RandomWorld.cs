using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RandomAdditions;
using SafeSaves;

/// <summary>
/// This edits mission rewards at the end of missions
/// </summary>
[AutoSaveManager]
public class RandomWorld
{
    [SSManagerInst]
    public static RandomWorld inst = new RandomWorld();

    [SSaveField]
    public bool WorldAltered = false;
    [SSaveField]
    public float LootBlocksMulti = 1.0f;
    [SSaveField]
    public float LootXpMulti = 1.0f;
    [SSaveField]
    public float LootBBMulti = 1.0f;
    internal static void BeginCheating()
    {
        ManSaveGame.inst.CurrentState.m_FileHasBeenTamperedWith = true;
        KickStartOptions.AlteredVanilla.SetExtraTextUIOnly("ACTIVE");
        DebugRandAddi.Log("RandomWorld - Enabled sliders");
    }
    internal static void PrepareForSaving()
    {
    }
    internal static void FinishedSaving()
    {
    }
    internal static void FinishedLoading()
    {
        try
        {
            DebugRandAddi.Log("RandomWorld - Resync");
            KickStartOptions.ResyncValues();
        }
        catch (Exception e)
        {
            DebugRandAddi.Log("RandomWorld - error " + e);
        }
    }
}
