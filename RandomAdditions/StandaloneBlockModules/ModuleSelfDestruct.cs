using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RandomAdditions;
using TerraTechETCUtil;

// It BLOWS UP after placement after some specified time
public class ModuleSelfDestruct : ExtModule
{
    /*
     "ModuleSelfDestruct":
     {
          "SelfDestructTimer": 3, // Set how long in seconds until it blows up.
          // Note that framerates will have an impact on other time-reliant functions.
          "DetachInstead": false, // Make it detach from the Tech instead.  
          // This makes it renewable but prevents it from staying on the Tech
     }
     */
    public float SelfDestructTimer = 3;
    public bool DetachInstead = false;

    private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleSelfDestruct",
         "This block will explode just after attachment." +
        AltUI.HintString("It's one-use only!"));
    private void DetachForced()
    {
        block.PreExplodePulse = false;
        if (ManNetwork.IsHost && block.IsAttached)
            Singleton.Manager<ManLooseBlocks>.inst.HostDetachBlock(block, false, true);
    }
    private void InitiateSelfDestructSequence()
    {
        if (DetachInstead)
        {
            block.PreExplodePulse = true;
            block.StartMaterialPulse(ManTechMaterialSwap.MaterialTypes.Damage, ManTechMaterialSwap.MaterialColour.Damage);
            InvokeHelper.Invoke(DetachForced, SelfDestructTimer);
        }
        else
            block.damage.SelfDestruct(SelfDestructTimer);
    }
    public override void OnGrabbed()
    {
        hint.Show();
    }
    public override void OnAttach()
    {
        InitiateSelfDestructSequence();
    }
}