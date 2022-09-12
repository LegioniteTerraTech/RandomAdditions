using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SafeSaves;


public class ModuleItemSilo : RandomAdditions.ModuleItemSilo { };
namespace RandomAdditions
{
    [AutoSaveComponent]
    public class ModuleItemSilo : Module
    {
        // A module that acts as a storage for a single type of resource, but lag-free and without colliders
        //   Relies on ModuleItemStore to keep the stacks of a single type, with only one node to stack from
        //     and also requires it to have max stack height 3 each.
        //  For this reason I can only recommend the GSO Filtered Node Silo for reference with this module
        //    otherwise issues may follow!

        /* Throw this within your JSONBLOCK
        "RandomAdditions.ModuleItemSilo":{ // Add internal resource storage capacity to your block
            "StoresBlocksInsteadOfChunks": false,   // Send blocks the the SCU instead of storing chunks?
            "UseShrinkAnim": true,                  // Do we shrink the items when storing?
            "MaxOutputRate": 0,                     // Max Rate this silo can output at - also determines stack heights. Leave at 0 to auto-set

            // For Chunks: 
            "MaxCapacity": 10,                      // Max resource storage capacity
            "DestroyContentsOnDestruction": false,  // Is this silo that dirty cheap that if it explodes it destroys all chunks inside?
        },
        */
        //Check out SiloGauge too as you will be needing one of those to display what's inside


        //OBSOLETE because the ModuleItemHolder does this automatically lol, just leave that on a low value
        //"EjectOnOverspeed": false,  // Eject if over the speed?
        //"EmergencyEjectSpeed": 75,  // Eject all internals if the silo is over this speed
        //for crash-related speed reasons
        //"ImmedeateOutput": false,   // Do we wait for the animation delay to finish before grabbing the chunk?
        //"ReleaseOffset": 0.2,       // How high it should move the chunk on the output stack with ImmedeateOutput enabled


        //Collection (General)
        public bool StoresBlocksInsteadOfChunks = false;
        public bool UseShrinkAnim = true;
        public int MaxOutputRate = 0;
        private float ReleaseOffset = 0.2f;
        private bool ImmedeateOutput = true;
        //public bool KeepAtLeastOneItemOut = true;//WIP
        public bool Empty = true;

        internal float GetCountPercent
        {
            get
            {
                if (StoresBlocksInsteadOfChunks)
                {
                    if (GetBlockType == BlockTypes.GSOAIController_111)
                        return 0;
                    else
                        return 1;
                }
                else
                    return GetChunkCountPercent;
            }
        }

        //Collection (Chunks)
        public int MaxCapacity = 10;
        public bool DestroyContentsOnDestruction = false;
        //public bool EjectOnOverspeed = false;
        //public float EmergencyEjectSpeed = 75;
        private float GetChunkCountPercent = 0;
        [SSaveField]
        public ChunkTypes GetChunkType = ChunkTypes.Wood;
        /// <summary>
        /// Return the chuck color, unless it's a corp block, then it uses that corp's respective color.
        /// </summary>
        internal Color GetSavedGaugeColor
        {
            get { 

                return SavedGaugeColor; 
            }
        }

        //Collection (Blocks)
        [SSaveField]
        public BlockTypes GetBlockType = BlockTypes.GSOAIController_111;
        [SSaveField]
        public string BlockTypeString = null;


        //Processing
        [SSaveField]
        public int SavedCount = 0;
        public bool WasSearched = false;


        private TankBlock TankBlock;
        private ModuleItemStore itemStore;
        private ModuleItemHolder itemHold;
        private Transform siloSpawn;
        private Transform input;
        private Transform output;
        private List<SiloGauge> gauges = new List<SiloGauge>();
        private List<SiloDisplay> disps = new List<SiloDisplay>();
        /// <summary>
        /// Also used to save the color of the block's corp
        /// </summary>
        private Color SavedGaugeColor = Color.magenta;
        //private bool queuedBlink = false;
        private List<Visible> AbsorbAnimating = new List<Visible>();
        private List<Vector3> AbsorbAnimatingPos = new List<Vector3>();
        private List<Visible> ReleaseAnimating = new List<Visible>();
        private List<Vector3> ReleaseAnimatingPos = new List<Vector3>();
        private List<ModuleItemHolder.Stack> ReleaseTargetNode = new List<ModuleItemHolder.Stack>();

        private int StackSet = 2;
        private bool isSaving = false;
        /*
        private bool isSaving {
            get {
                return Saving;
            }
            set
            {
                DebugRandAddi.Log("RandomAdditions: ModuleItemSilo - setting value " + value);
                Saving = value;
            }
        }*/


        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.SubToBlockAttachConnected(OnAttach, OnDetach);

            gauges = gameObject.transform.GetComponentsInChildren<SiloGauge>().ToList();
            disps = gameObject.transform.GetComponentsInChildren<SiloDisplay>().ToList();
            itemStore = gameObject.GetComponent<ModuleItemStore>();
            itemHold = gameObject.GetComponent<ModuleItemHolder>();
            siloSpawn = gameObject.transform.Find("_siloSpawn");
            input = gameObject.transform.Find("_input");
            output = gameObject.transform.Find("_output");
            int possibleAPs = 0;
            if (StoresBlocksInsteadOfChunks)
            {
                SavedGaugeColor = new Color(0.46f, 0.52f, 0.46f, 1);
                foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                {
                    if (stack.apConnectionIndices.Length > possibleAPs)
                        possibleAPs = stack.apConnectionIndices.Length;
                    if (!stack.CanAcceptObjectType(ObjectTypes.Block))
                    {
                        LogHandler.ThrowWarning("RandomAdditions: \nModuleItemSilo NEEDS ModuleItemHolder to handle blocks instead of chunks to operate.\n<b>Set ModuleItemHolder's m_AcceptFlags to 1!</b>\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                        break;
                    }
                }
            }
            else
            {
                foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                {
                    if (stack.apConnectionIndices.Length > possibleAPs)
                        possibleAPs = stack.apConnectionIndices.Length;
                    if (!stack.CanAcceptObjectType(ObjectTypes.Chunk))
                    {
                        LogHandler.ThrowWarning("RandomAdditions: \nModuleItemSilo NEEDS ModuleItemHolder to handle chunks to operate.\n<b>Set ModuleItemHolder's m_AcceptFlags to 0!</b>\nThis operation cannot be handled automatically.\n  Cause of error - Block " + TankBlock.name);
                        break;
                    }
                }
            }
            int stackOverride;
            if (MaxOutputRate > 0)
            {
                stackOverride = MaxOutputRate * 2;
                StackSet = MaxOutputRate;
            }
            else
            {
                stackOverride = possibleAPs * 2;
                StackSet = possibleAPs;
            }
            itemHold.OverrideStackCapacity(stackOverride);  //  MUST be at least 2
            DebugRandAddi.Info("RandomAdditions: ModuleItemSilo - Set stacks capacity to " + stackOverride);
            if (gauges.Count == 0)
            {
                DebugRandAddi.Info("RandomAdditions: ModuleItemSilo - There are no gauges on this silo.\n  Block " + TankBlock.name);
            }
            else
            {
                foreach (SiloGauge sGauge in gauges)
                {
                    sGauge.Setup(this);
                }
            }
            if (disps.Count == 0)
            {
                DebugRandAddi.Info("RandomAdditions: ModuleItemSilo - Detected no displays on silo.\n  Block " + TankBlock.name);
            }
            else
            {
                foreach (SiloDisplay sDisp in disps)
                {
                    sDisp.Setup(this);
                }
            }
            if (siloSpawn.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ModuleItemSilo - SILO SPAWN NOT SET!!!  defaulting to center of silo! \n  Cause of error - Block " + TankBlock.name);
                siloSpawn = transform;
            }
            if (input.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ModuleItemSilo - INPUT NOT SET!!!  defaulting to the spot _siloSpawn is set to! \n  Cause of error - Block " + TankBlock.name);
                input = siloSpawn;
            }
            if (output.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: OUTPUT SPAWN NOT SET!!!  defaulting to the spot _siloSpawn is set to!\n  Cause of error - Block " + TankBlock.name);
                output = siloSpawn;
            }
            if (itemStore.IsNull())
            {
                //DebugRandAddi.Log("RandomAdditions: ModuleItemSilo NEEDS ModuleItemStore to operate correctly.  If you are doing this without ModuleItemStore, you are doing it WRONG!!!  THE RESOURCES WILL HANDLE BADLY!!!");
                LogHandler.ThrowWarning("RandomAdditions: \nModuleItemSilo NEEDS ModuleItemStore to operate correctly. \n<b>THE BLOCK WILL NOT BE ABLE TO DO ANYTHING!!!</b>\n  Cause of error - Block " + TankBlock.name);
            }
            if (MaxCapacity <= 0)
            {
                LogHandler.ThrowWarning("RandomAdditions: \nModuleItemSilo cannot have a MaxCapacity below or equal to zero!\n  Cause of error - Block " + TankBlock.name);
            }
        }
        private void OnAttach()
        {
            TankBlock.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            //TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerializeText));
            TankBlock.tank.Holders.HBEvent.Subscribe(OnHeartbeat);

            ResetGaugesAndDisplays();
            isSaving = false;
            ExtUsageHint.ShowExistingHint(4005);
        }
        private void OnDetach()
        {
            TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            //TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerializeText));

            //DebugRandAddi.Log("RandomAdditions: isSaving " + isSaving);
            if (!isSaving || ManTechSwapper.inst.CheckOperatingOnTech(block.tank))
            {   // Only eject when the world is NOT saving
                if (!StoresBlocksInsteadOfChunks)
                {
                    if (SavedCount > 0)
                    {
                        TechAudio.AudioTickData sound = TechAudio.AudioTickData.ConfigureOneshot(this, TechAudio.SFXType.ItemCannonDelivered);
                        try
                        {
                            TankBlock.tank.TechAudio.PlayOneshot(sound);
                        }
                        catch { }
                    }
                }
                // SILO COMPROMISED!  EJECT ALL!
                EmergencyEjectAllContents();
                GetChunkCountPercent = (float)SavedCount / (float)MaxCapacity;
                ResetGaugesAndDisplays();
            }
            TankBlock.tank.Holders.HBEvent.Unsubscribe(OnHeartbeat);
        }
        private void OnHeartbeat(int HartC, TechHolders.Heartbeat HartStep)
        {
            if (HartStep == TechHolders.Heartbeat.PrePass)
            {
                CheckStore();
            }
            if (HartStep == TechHolders.Heartbeat.PostPass)
            {
                GetChunkCountPercent = (float)SavedCount / (float)MaxCapacity;
                //DebugRandAddi.Log("RandomAdditions: fill is " + GetChunkCountPercent);
                CheckRelease();
                WasSearched = false;
                UpdateGaugesAndDisplays();
            }
        }
        private void OnSpawn()
        {   // reset for new blocks/new spawns
            GetChunkType = ChunkTypes.Null;
            GetBlockType = BlockTypes.GSOAIController_111;
            SavedCount = 0;
            SavedGaugeColor = Color.black;
            isSaving = false;
        }
        private void OnRecycle()
        {   // reset for new blocks/new spawns
            /*
            DebugRandAddi.Assert(true, "ON RECYCLE " + name);
            EmergencyEjectAllContents();
            GetChunkCountPercent = (float)SavedCount / (float)MaxCapacity;
            ResetGaugesAndDisplays();
            GetChunkType = ChunkTypes.Null;
            GetBlockType = BlockTypes.GSOAIController_111;
            SavedCount = 0;
            SavedChunkColor = Color.black;
            isSaving = false;*/
        }


        // Conveyor step actions
        /// <summary>
        /// On 3rd item, we store to the silo
        /// </summary>
        private void CheckStore()
        {
            if (!StoresBlocksInsteadOfChunks)
            {
                if (SavedCount < MaxCapacity)
                {
                    foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                    {
                        HandleStoreStackChunks(stack);
                    }
                }
                else if (SavedCount > MaxCapacity)
                {
                    DebugRandAddi.LogError("RandomAdditions: SILO IS OVERLOADED BEYOND LIMIT!!!");
                    //Probably a block change in update - eject extras!
                    EmergencyEjectRelieve();
                }
            }
            else
            {
                foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                {
                    HandleStoreStackBlocks(stack);
                }
            }
        }

        /// <summary>
        /// If there's only one item, we extract from the silo
        /// </summary>
        private void CheckRelease()
        {
            if (!StoresBlocksInsteadOfChunks)
            {
                if (SavedCount > 0)
                {
                    foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                    {
                        HandleReleaseStackChunks(stack);
                    }
                }
            }
            else
            {
                if (GetBlockType == BlockTypes.GSOAIController_111)
                    return;
                foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                {
                    HandleReleaseStackBlocks(stack);
                }
            }
        }

        // Item handling
        private void HandleStoreStackChunks(ModuleItemHolder.Stack stack)
        {
            int FireTimes = stack.NumItems - StackSet;
            for (int step = 0; step < FireTimes; step++)
            {
                if (SavedCount >= MaxCapacity)
                    break;
                var toManage = stack.FirstItem;
                if (!toManage.pickup)
                    continue;
                if (SavedCount == 0)
                {
                    Empty = false;
                    GetChunkType = toManage.pickup.ChunkType;
                    //Save the chunk's color for gauge referencing
                    SetColorOfGauges();
                }
                if (toManage.pickup.ChunkType == GetChunkType)
                {
                    QueueStoreAnim(toManage);
                    SavedCount++;
                    if (SavedCount >= MaxCapacity)
                        break;
                }
                else
                {
                    DebugRandAddi.LogError("RandomAdditions: SILO INPUT REQUEST DENIED!!!  WRONG INPUT FORCED BY PLAYER!!!");
                    toManage.SetHolder(null, true);//DROP IT NOW!!!
                    if (toManage.InBeam == true)
                    {
                        LogHandler.ThrowWarning("RandomAdditions: \nModuleItemSilo: Critical error on handling invalid chunk");
                    }
                }
            }
        }
        private void HandleStoreStackBlocks(ModuleItemHolder.Stack stack)
        {
            int FireTimes = stack.NumItems - StackSet;
            for (int step = 0; step < FireTimes; step++)
            {
                var toManage = stack.FirstItem;
                if (!toManage.block)
                    continue;
                if (GetBlockType == BlockTypes.GSOAIController_111)
                {
                    Empty = false;
                    GetBlockType = toManage.block.BlockType;
                    SetColorOfGauges();
                }
                if (toManage.block.BlockType == GetBlockType)
                {
                    QueueStoreAnim(toManage);
                    if (Singleton.Manager<ManPlayer>.inst.InventoryIsUnrestricted) { }
                    else if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                    {
                        if (block.tank?.netTech?.NetPlayer?.Inventory)
                            block.tank?.netTech?.NetPlayer?.Inventory.HostAddItem(GetBlockType, 1);
                    }
                    else
                        Singleton.Manager<ManPlayer>.inst.AddBlockToInventory(GetBlockType);
                }
                else
                {
                    DebugRandAddi.LogError("RandomAdditions: SILO INPUT REQUEST DENIED!!!  WRONG INPUT FORCED BY PLAYER!!!");
                    toManage.InBeam = false;//DROP IT NOW!!!
                    if (toManage.InBeam == true)
                    {
                        LogHandler.ThrowWarning("RandomAdditions: \nModuleItemSilo: Critical error on handling invalid block");
                    }
                }
            }
        }
        private void HandleReleaseStackChunks(ModuleItemHolder.Stack stack)
        {
            int FireTimes = stack.NumItems - StackSet;
            for (int step = 0; step > FireTimes; step--)
            {
                if (GetChunkType == ChunkTypes.Null)
                {
                    DebugRandAddi.LogError("RandomAdditions: SILO HAS A NULL SAVEDCHUNK TYPE!!!");
                    SavedCount = 0;
                    break;
                }
                SavedCount--;
                QueueReleaseAnim(stack);
                if (SavedCount <= 0)
                {
                    Empty = true;
                    GetChunkType = ChunkTypes.Null;
                    //Reset color for gauge referencing
                    SetColorOfGauges();
                    if (SavedCount < 0)
                    {
                        DebugRandAddi.LogError("RandomAdditions: SILO HAS NEGATIVE RESOURCES!!!");
                        SavedCount = 0;// well... we can't compensate for negatives can we...
                    }
                    break;
                }
            }
        }
        private void HandleReleaseStackBlocks(ModuleItemHolder.Stack stack)
        {
            int FireTimes = stack.NumItems - StackSet;
            for (int step = 0; step > FireTimes; step--)
            {
                if (Singleton.Manager<ManPlayer>.inst.InventoryIsUnrestricted)
                {
                    if (GetBlockType == BlockTypes.GSOAIController_111)
                    {
                        DebugRandAddi.LogError("RandomAdditions: SILO HAS A NULL SAVEDBLOCK TYPE!!!");
                        break;
                    }
                    QueueReleaseAnim(stack);
                    SavedCount = MaxCapacity;
                }
                else
                {
                    try
                    {
                        int availQuant;
                        bool isMP = Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer();
                        if (isMP)
                        {
                            if (block.tank?.netTech?.NetPlayer?.Inventory)
                            {
                                NetInventory NI = block.tank.netTech.NetPlayer.Inventory;
                                if (NI.IsAvailableToLocalPlayer(GetBlockType))
                                {
                                    availQuant = NI.GetQuantity(GetBlockType);
                                    if (availQuant > 0)
                                    {
                                        availQuant--;
                                        NI.SetBlockCount(GetBlockType, availQuant);

                                        QueueReleaseAnim(stack);
                                        SavedCount = availQuant;
                                        if (availQuant <= 0)
                                        {
                                            Empty = true;
                                            GetBlockType = BlockTypes.GSOAIController_111;
                                            SetColorOfGauges();
                                            if (availQuant < 0)
                                                DebugRandAddi.Log("RandomAdditions: SILO HAS NEGATIVE BLOCKS!!!");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (Singleton.Manager<ManPlayer>.inst.PlayerInventory != null)
                            {
                                SingleplayerInventory SI = (SingleplayerInventory)Singleton.Manager<ManPlayer>.inst.PlayerInventory;
                                if (SI.IsAvailableToLocalPlayer(GetBlockType))
                                {
                                    availQuant = SI.GetQuantity(GetBlockType);
                                    if (availQuant > 0)
                                    {
                                        availQuant--;
                                        SI.SetBlockCount(GetBlockType, availQuant);

                                        QueueReleaseAnim(stack);
                                        SavedCount = availQuant;
                                        if (availQuant <= 0)
                                        {
                                            Empty = true;
                                            GetBlockType = BlockTypes.GSOAIController_111;
                                            SetColorOfGauges();
                                            if (availQuant < 0)
                                                DebugRandAddi.LogError("RandomAdditions: SILO HAS NEGATIVE BLOCKS!!!");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }


        // "Animations"
        private void QueueStoreAnim(Visible toManage)
        {
            TechAudio.AudioTickData sound = TechAudio.AudioTickData.ConfigureOneshot(this, TechAudio.SFXType.ItemResourceConsumed);
            try
            {
                TankBlock.tank.TechAudio.PlayOneshot(sound);
            }
            catch { }

            toManage.SetHolder(null, true);//DROP IT NOW!!!
            toManage.SetGrabTimeout(2);//disable grabbing of it
            toManage.SetItemCollectionTimeout(2);//disable more
            toManage.SetInteractionTimeout(2);// disable ALL
            toManage.ColliderSwapper.EnableCollision(false);
            if ((bool)toManage.pickup)
            {
                toManage.pickup.ClearRigidBody(true);
            }
            else if ((bool)toManage.block)
            {
                toManage.block.ClearRigidBody(true);
            }
            AbsorbAnimating.Add(toManage);
            AbsorbAnimatingPos.Add(transform.InverseTransformPoint(toManage.centrePosition));
        }
        private void QueueReleaseAnim(ModuleItemHolder.Stack stack)
        {
            TechAudio.AudioTickData sound = TechAudio.AudioTickData.ConfigureOneshot(this, TechAudio.SFXType.ItemResourceProduced);
            try
            {
                TankBlock.tank.TechAudio.PlayOneshot(sound);
            }
            catch { }
            if (ImmedeateOutput)
            {
                Visible toManage;
                if (!StoresBlocksInsteadOfChunks)
                {
                    toManage = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)GetChunkType), siloSpawn.position, Quaternion.identity);
                }
                else
                {
                    toManage = KickStart.SpawnBlockS(GetBlockType, siloSpawn.position, Quaternion.identity, out _).visible;
                }
                stack.Take(toManage);
                ReleaseAnimating.Add(toManage);
                toManage.ColliderSwapper.EnableCollision(false);
            }
            else
            {
                Visible toManage;
                if (!StoresBlocksInsteadOfChunks)
                    toManage = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)GetChunkType), siloSpawn.position, Quaternion.identity);
                else
                {
                    toManage = KickStart.SpawnBlockS(GetBlockType, siloSpawn.position, Quaternion.identity, out _).visible;
                }
                stack.Take(toManage);
                //toManage.SetHolder(null, true);
                toManage.SetGrabTimeout(1);//disable grabbing of it
                toManage.SetItemCollectionTimeout(1);//disable more
                toManage.SetInteractionTimeout(1);// disable ALL
                ReleaseTargetNode.Add(stack);
                ReleaseAnimating.Add(toManage);
                ReleaseAnimatingPos.Add(transform.InverseTransformPoint(toManage.centrePosition));
                toManage.ColliderSwapper.EnableCollision(false);
            }
        }


        // Utilities
        public void ResetGaugesAndDisplays()
        {
            foreach (SiloDisplay sDisp in disps)
            {
                sDisp.UpdateDisplay();
            }
            foreach (SiloGauge sGauge in gauges)
            {
                sGauge.SnapGauge();
            }
        }
        public void UpdateGaugesAndDisplays()
        {
            foreach (SiloDisplay sDisp in disps)
            {
                sDisp.UpdateDisplay();
            }
            foreach (SiloGauge sGauge in gauges)
            {
                sGauge.UpdateGauge();
            }
        }
        private void SetColorOfGauges()
        {
            if (StoresBlocksInsteadOfChunks)
            {
                FactionSubTypes FST = ManSpawn.inst.GetCorporation(GetBlockType);
                switch (FST)
                {
                    case FactionSubTypes.NULL:
                        SavedGaugeColor = new Color(0.5f, 0.5f, 0.5f, 1);
                        break;
                    case FactionSubTypes.GSO:
                        SavedGaugeColor = new Color(0.45f, 0.49f, 0.56f, 1);
                        break;
                    case FactionSubTypes.GC:
                        SavedGaugeColor = new Color(1f, 1f, 0.3f, 1);
                        break;
                    case FactionSubTypes.EXP:
                        SavedGaugeColor = new Color(0.4f, 0.4f, 0.4f, 1);
                        break;
                    case FactionSubTypes.VEN:
                        SavedGaugeColor = new Color(1f, 0.3f, 0.3f, 1);
                        break;
                    case FactionSubTypes.HE:
                        SavedGaugeColor = new Color(0.1f, 0.1f, 0.1f, 1);
                        break;
                    case FactionSubTypes.SPE:
                        SavedGaugeColor = new Color(0.4f, 0.4f, 0.4f, 1);
                        break;
                    case FactionSubTypes.BF:
                        SavedGaugeColor = new Color(0.96f, 0.96f, 0.96f, 1);
                        break;
                    default:
                        SavedGaugeColor = new Color(0.46f, 0.52f, 0.46f, 1);
                        break;
                }
            }
            else
            {
                switch (GetChunkType)
                {
                    case ChunkTypes.Wood:
                        SavedGaugeColor = new Color(1, 0.75f, 0.42f, 1);
                        break;
                    case ChunkTypes.FibronChunk:
                        SavedGaugeColor = new Color(0.99f, 0.85f, 0.49f, 1);
                        break;
                    case ChunkTypes.RubberJelly:
                        SavedGaugeColor = new Color(0.62f, 0.45f, 0.18f, 1);
                        break;
                    case ChunkTypes.RubberBrick:
                        SavedGaugeColor = new Color(0.28f, 0.28f, 0.28f, 1);
                        break;
                    case ChunkTypes.LuxiteShard:
                        SavedGaugeColor = new Color(0.82f, 0.77f, 0.13f, 1);
                        break;
                    case ChunkTypes.LuxianCrystal:
                        SavedGaugeColor = new Color(1f, 1f, 0.29f, 1);
                        break;
                    case ChunkTypes.PlumbiteOre:
                        SavedGaugeColor = new Color(0.62f, 0.62f, 0.62f, 1);
                        break;
                    case ChunkTypes.PlumbiaIngot:
                        SavedGaugeColor = new Color(0.73f, 0.73f, 0.73f, 1);
                        break;
                    case ChunkTypes.TitaniteOre:
                        SavedGaugeColor = new Color(0.48f, 0.79f, 1f, 1);
                        break;
                    case ChunkTypes.TitanicAlloy:
                        SavedGaugeColor = new Color(0.24f, 0.4f, 0.79f, 1);
                        break;
                    case ChunkTypes.CarbiteOre:
                        SavedGaugeColor = new Color(0.35f, 0.35f, 0.35f, 1);
                        break;
                    case ChunkTypes.CarbiusBrick:
                        SavedGaugeColor = new Color(0.1f, 0.1f, 0.1f, 1);
                        break;
                    case ChunkTypes.RoditeOre:
                        SavedGaugeColor = new Color(0.27f, 1f, 0.72f, 1);
                        break;
                    case ChunkTypes.RodiusCapsule:
                        SavedGaugeColor = new Color(0.08f, 0.83f, 0.65f, 1);
                        break;
                    case ChunkTypes.OleiteJelly:
                        SavedGaugeColor = new Color(0.38f, 0.12f, 0.12f, 1);
                        break;
                    case ChunkTypes.OlasticBrick:
                        SavedGaugeColor = new Color(0.91f, 0.26f, 0.26f, 1);
                        break;
                    case ChunkTypes.IgniteShard:
                        SavedGaugeColor = new Color(0.91f, 0.5f, 0.15f, 1);
                        break;
                    case ChunkTypes.IgnianCrystal:
                        SavedGaugeColor = new Color(1f, 0.62f, 0.01f, 1);
                        break;
                    case ChunkTypes.EruditeShard:
                        SavedGaugeColor = new Color(0.15f, 0.65f, 0.12f, 1);
                        break;
                    case ChunkTypes.ErudianCrystal:
                        SavedGaugeColor = new Color(0.25f, 0.96f, 0.17f, 1);
                        break;
                    case ChunkTypes.CelestiteShard:
                        SavedGaugeColor = new Color(0.23f, 0.78f, 1f, 1);
                        break;
                    case ChunkTypes.CelestianCrystal:
                        SavedGaugeColor = new Color(0.19f, 0.65f, 1f, 1);
                        break;
                    default:
                        SavedGaugeColor = new Color(0.46f, 0.52f, 46f, 1);
                        break;
                }
            }
        }
        private void TryHandleSpeed()
        {
            /*
            if (EjectOnOverspeed && SavedCount > 0 && TankBlock.tank.IsNotNull())
            {
                TankBlock.PreExplodePulse = false;
                if (EmergencyEjectSpeed - 10 < TankBlock.tank.GetForwardSpeed())
                {
                    if (!queuedBlink && !TankBlock.PreExplodePulse)
                    {
                        TankBlock.PreExplodePulse = true;
                        queuedBlink = true;
                    }
                }
                else
                {
                    if (queuedBlink)
                    {
                        TankBlock.PreExplodePulse = false;
                        queuedBlink = false;
                    }
                }
                if (EmergencyEjectSpeed < TankBlock.tank.GetForwardSpeed())
                {
                    DebugRandAddi.Log("RandomAdditions: Silo " + gameObject.name + " has gone over their maximum stable speed and will eject everything to prevent exploding!");
                    EmergencyEjectAllContents();
                }
            }
            else
            {
                if (queuedBlink)
                {
                    TankBlock.PreExplodePulse = false;
                    queuedBlink = false;
                }
            }*/
        }


        // Main
        /// <summary>
        /// Run chunk "animator" module and update displays
        /// </summary>
        private void Update()
        {
            int fireTimes = AbsorbAnimating.Count;
            for (int step = 0; step < fireTimes; step++)
            {
                Visible toManage = AbsorbAnimating.ElementAt(step);
                Vector3 toManagePos = AbsorbAnimatingPos.ElementAt(step);
                Vector3 fL = siloSpawn.localPosition;
                toManagePos = ((fL - toManagePos) / 8) + toManagePos;
                AbsorbAnimatingPos[step] = toManagePos;
                toManage.centrePosition = transform.TransformPoint(toManagePos);
                Vector3 item = toManagePos;
                if (UseShrinkAnim)
                {
                    if (toManage.GetComponent<TankBlockScaler>())
                        toManage.trans.localScale = ((Vector3.one / 4) + (Vector3.one * Mathf.Min((fL - toManagePos).magnitude * 0.75f, 0.75f))) * toManage.GetComponent<TankBlockScaler>().AimedDownscale;
                    else
                        toManage.trans.localScale = (Vector3.one / 4) + (Vector3.one * Mathf.Min((fL - toManagePos).magnitude * 0.75f, 0.75f));
                }
                if (fL.x - 0.01f < item.x && item.x < fL.x + 0.01f && fL.y - 0.01f < item.y && item.y < fL.y + 0.01f && fL.z - 0.01f < item.z && item.z < fL.z + 0.01f)
                {
                    AbsorbAnimating.RemoveAt(step);
                    AbsorbAnimatingPos.RemoveAt(step);
                    toManage.ColliderSwapper.EnableCollision(true);
                    toManage.RemoveFromGame();
                    step--;
                    fireTimes--;
                }
            }

            int fireTimes2 = ReleaseAnimating.Count;
            for (int step = 0; step < fireTimes2; step++)
            {
                if (ImmedeateOutput)
                {
                    Visible toManage = ReleaseAnimating.ElementAt(step);
                    if (toManage.IsNotNull())
                    {
                        float scaler = 1;
                        if (toManage.GetComponent<TankBlockScaler>())
                            scaler = toManage.GetComponent<TankBlockScaler>().AimedDownscale;
                        
                        if (0.9f * scaler < toManage.trans.localScale.y || !UseShrinkAnim)
                        {
                            toManage.trans.localScale = Vector3.one;
                            toManage.ColliderSwapper.EnableCollision(true);
                            ReleaseAnimating.RemoveAt(step);
                            step--;
                            fireTimes2--;
                        }
                        else if (UseShrinkAnim)
                        {
                            if (toManage.trans.localScale.y == scaler)
                                toManage.trans.localScale = (Vector3.one / 4) * scaler;
                            toManage.trans.localScale = Vector3.one * ((((1 - toManage.trans.localScale.y) / 8) + toManage.trans.localScale.y) * scaler);
                        }
                    }
                    else
                    {
                        ReleaseAnimating.RemoveAt(step);
                        step--;
                        fireTimes2--;
                    }
                }
                else
                {
                    Visible toManage = ReleaseAnimating.ElementAt(step);
                    if (toManage.IsNotNull())
                    {
                        Vector3 toManagePos = ReleaseAnimatingPos.ElementAt(step);
                        ModuleItemHolder.Stack targNode = ReleaseTargetNode.ElementAt(step);
                        Vector3 fL = targNode.basePos + (ReleaseOffset * Vector3.up);
                        toManagePos = ((fL - toManagePos) / 8) + toManagePos;
                        ReleaseAnimatingPos[step] = toManagePos;
                        toManage.centrePosition = transform.TransformPoint(toManagePos);
                        Vector3 item = toManagePos;
                        if (UseShrinkAnim)
                            toManage.trans.localScale = Vector3.one - (Vector3.one * Mathf.Min((fL - toManagePos).magnitude * 0.75f, 0.75f));
                        if (fL.x - 0.01f < item.x && item.x < fL.x + 0.01f && fL.y - 0.01f < item.y && item.y < fL.y + 0.01f && fL.z - 0.01f < item.z && item.z < fL.z + 0.01f)
                        {
                            toManage.trans.localScale = Vector3.one;
                            ReleaseTargetNode.RemoveAt(step);
                            ReleaseAnimating.RemoveAt(step);
                            ReleaseAnimatingPos.RemoveAt(step);
                            if (StoresBlocksInsteadOfChunks)
                                toManage.block.InitRigidbody();
                            else
                                toManage.pickup.InitRigidbody();
                            toManage.ColliderSwapper.EnableCollision(true);
                            toManage.SetGrabTimeout(0);
                            toManage.SetItemCollectionTimeout(0);
                            toManage.SetInteractionTimeout(0);
                            step--;
                            fireTimes2--;
                        }
                    }
                    else
                    {
                        ReleaseTargetNode.RemoveAt(step);
                        ReleaseAnimating.RemoveAt(step);
                        ReleaseAnimatingPos.RemoveAt(step);
                        step--;
                        fireTimes2--;
                    }
                }
            }

            foreach (var item in gauges)
            {
                item.UpdateScale();
            }
        }


        // Chunk saving
        public void EmergencyEjectAllContents()
        {
            foreach (Visible toManage in AbsorbAnimating)
            {   //was already added!
                if (!toManage.isActive)
                    continue;
                toManage.trans.localScale = Vector3.one;
                toManage.ColliderSwapper.EnableCollision(true);
                if (StoresBlocksInsteadOfChunks)
                    toManage.block.InitRigidbody();
                else
                    toManage.pickup.InitRigidbody();
                toManage.RemoveFromGame();
            }
            foreach (Visible toManage in ReleaseAnimating)
            {
                if (!toManage.isActive)
                    continue;
                toManage.trans.localScale = Vector3.one;
                if (StoresBlocksInsteadOfChunks)
                    toManage.block.InitRigidbody();
                else
                    toManage.pickup.InitRigidbody();
                toManage.ColliderSwapper.EnableCollision(true);
                toManage.SetGrabTimeout(0);
                toManage.SetItemCollectionTimeout(0);
                toManage.SetInteractionTimeout(0);
            }
            AbsorbAnimating.Clear();
            AbsorbAnimatingPos.Clear();
            ReleaseAnimating.Clear();
            ReleaseAnimatingPos.Clear();
            ReleaseTargetNode.Clear();

            if (!StoresBlocksInsteadOfChunks)
            {
                if (DestroyContentsOnDestruction && TankBlock.damage.AboutToDie)
                {
                    DebugRandAddi.Log("RandomAdditions: Silo " + gameObject.name + " is unstable on death and has destroyed all their stored contents!");
                    SavedCount = 0;
                    GetChunkType = ChunkTypes.Null;
                    return;
                }
                while (SavedCount > 0)
                {
                    //var itemSpawn2 = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Scenery, (int)SceneryTypes.Pillar), siloSpawn.position, Quaternion.identity, true);
                    SavedCount--;
                    var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)GetChunkType), siloSpawn.position, Quaternion.identity);
                    itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
                }
            }
            SavedCount = 0;
            GetBlockType = BlockTypes.GSOAIController_111;
            GetChunkType = ChunkTypes.Null;
        }
        public void EmergencyEjectRelieve()
        {
            TechAudio.AudioTickData sound = TechAudio.AudioTickData.ConfigureOneshot(this, TechAudio.SFXType.LightMachineGun);
            try
            {
                TankBlock.tank.TechAudio.PlayOneshot(sound);
            }
            catch { }
            while (SavedCount > MaxCapacity)
            {
                SavedCount--;
                var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)GetChunkType), siloSpawn.position, Quaternion.identity);
                itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
            }
            UpdateGaugesAndDisplays();
        }



        // Save operations
        [Serializable]
        private new class SerialData : SerialData<SerialData>
        {   // This could be optimised for storage down the line
            public ChunkTypes savedChunk;
            public BlockTypes savedBlock;
            public int savedCount;
            public float savedColorR;
            public float savedColorG;
            public float savedColorB;
        }
        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {   // On Save (non-snap)
                    //var tile = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position);
                    //tile.
                    //DebugRandAddi.Log("RandomAdditions: isSaving " + tile.IsCreated + tile.IsLoaded + tile.IsPopulated);
                    if (Singleton.Manager<ManPointer>.inst.targetVisible)
                    {
                        if (!Singleton.Manager<ManPointer>.inst.targetVisible.block == TankBlock)
                        {
                            isSaving = true;// only disable ejecting when the world is removed
                        }
                        // The block saves every time it is grabbed, but for what purpose if it's being removed?!
                    }
                    else
                        isSaving = true;// only disable ejecting when the world is removed
                    if (ManSaveGame.Storing)
                    {   // Only save on world save
                        var bloc = ManSpawn.inst.GetBlockPrefab(GetBlockType);
                        if (bloc)
                            BlockTypeString = bloc.name;
                        if (this.SerializeToSafe())
                        {
                            DebugRandAddi.Log("SAVING " + TankBlock.name);
                            DebugRandAddi.Log("Block type is " + GetBlockType);
                        }
                    }
                }
                else
                {   //Load from Save
                    try
                    {
                        isSaving = false;
                        DebugRandAddi.Log("LOADING " + TankBlock.name);
                        DebugRandAddi.Log("Block type prev is " + GetBlockType);
                        if (this.DeserializeFromSafe())
                        {
                            if (BlockTypeString != null)
                                GetBlockType = KickStart.GetProperBlockType(GetBlockType, BlockTypeString);
                            DebugRandAddi.Log("Block type is now " + GetBlockType);
                        }
                        SetColorOfGauges();
                        ResetGaugesAndDisplays();
                    }
                    catch { }
                }
            }
            catch { }
        }
        private void OnSerializeText(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {   // On Snapshot saving
                    DebugRandAddi.Assert(true, "SAVING(text) " + TankBlock.name);
                }
                else
                {   //Load from Snapshot
                    DebugRandAddi.Assert(true, "LOADING(text) " + TankBlock.name);
                }
            }
            catch { }
        }
    }
}
