using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ModuleItemSilo : RandomAdditions.ModuleItemSilo { };
namespace RandomAdditions
{
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
                    if (Empty)
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
        internal ChunkTypes GetChunkType { get; private set; }
        internal Color GetSavedChunkColor
        {
            get { return SavedChunkColor; }
        }

        //Collection (Blocks)
        internal BlockTypes GetBlockType { get; private set; }


        //Processing
        public int SavedCount = 0;
        public bool WasSearched = false;

        private TankBlock TankBlock;
        private ModuleItemStore itemStore;
        private ModuleItemHolder itemHold;
        private Transform siloSpawn;
        private Transform input;
        private Transform output;
        private SiloGauge[] gauges = Array.Empty<SiloGauge>();
        private SiloDisplay[] disps = Array.Empty<SiloDisplay>();
        private Color SavedChunkColor;
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
                Debug.Log("RandomAdditions: ModuleItemSilo - setting value " + value);
                Saving = value;
            }
        }*/


        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.AttachEvent.Subscribe(OnAttach);
            TankBlock.DetachEvent.Subscribe(OnDetach);

            gauges = gameObject.transform.GetComponentsInChildren<SiloGauge>();
            disps = gameObject.transform.GetComponentsInChildren<SiloDisplay>();
            itemStore = gameObject.GetComponent<ModuleItemStore>();
            itemHold = gameObject.GetComponent<ModuleItemHolder>();
            siloSpawn = gameObject.transform.Find("_siloSpawn");
            input = gameObject.transform.Find("_input");
            output = gameObject.transform.Find("_output");
            int possibleAPs = 0;
            if (StoresBlocksInsteadOfChunks)
            {
                SavedChunkColor = new Color(0.46f, 0.52f, 46f, 1);
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
            Debug.Log("RandomAdditions: ModuleItemSilo - Set stacks capacity to " + stackOverride);
            if (gauges.Length == 0)
            {
                Debug.Log("RandomAdditions: ModuleItemSilo - Detected no gauges on silo!\n  Cause of error - Block " + TankBlock.name);
            }
            else
            {
                foreach (SiloGauge sGauge in gauges)
                {
                    sGauge.siloMain = this;
                }
            }
            if (disps.Length == 0)
            {
                Debug.Log("RandomAdditions: ModuleItemSilo - Detected no displays on silo.\n  Cause of error - Block " + TankBlock.name);
            }
            else
            {
                foreach (SiloDisplay sDisp in disps)
                {
                    sDisp.siloMain = this;
                }
            }
            if (siloSpawn.IsNull())
            {
                Debug.Log("RandomAdditions: ModuleItemSilo - SILO SPAWN NOT SET!!!  defaulting to center of silo! \n  Cause of error - Block " + TankBlock.name);
                siloSpawn = transform;
            }
            if (input.IsNull())
            {
                Debug.Log("RandomAdditions: ModuleItemSilo - INPUT NOT SET!!!  defaulting to the spot _siloSpawn is set to! \n  Cause of error - Block " + TankBlock.name);
                input = siloSpawn;
            }
            if (output.IsNull())
            {
                Debug.Log("RandomAdditions: OUTPUT SPAWN NOT SET!!!  defaulting to the spot _siloSpawn is set to!\n  Cause of error - Block " + TankBlock.name);
                output = siloSpawn;
            }
            if (itemStore.IsNull())
            {
                //Debug.Log("RandomAdditions: ModuleItemSilo NEEDS ModuleItemStore to operate correctly.  If you are doing this without ModuleItemStore, you are doing it WRONG!!!  THE RESOURCES WILL HANDLE BADLY!!!");
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
            TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.tank.Holders.HBEvent.Subscribe(OnHeartbeat); 
            UpdateGaugesAndDisplays();
            isSaving = false;
        }
        private void OnDetach()
        {
            //Debug.Log("RandomAdditions: isSaving " + isSaving);
            if (!isSaving)
            {   // cant eject when the world is saving
                if (SavedCount > 0)
                {
                    TechAudio.AudioTickData sound = TechAudio.AudioTickData.ConfigureOneshot(this, TechAudio.SFXType.ItemCannonDelivered);
                    try
                    {
                        TankBlock.tank.TechAudio.PlayOneshot(sound);
                    }
                    catch { }
                    // SILO COMPROMISED!  EJECT ALL!
                    EmergencyEjectAllContents();
                    UpdateGaugesAndDisplays();
                }
            }
            TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
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
                //Debug.Log("RandomAdditions: fill is " + GetChunkCountPercent);
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
            SavedChunkColor = Color.black;
            isSaving = false;
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
                    Debug.Log("RandomAdditions: SILO IS OVERLOADED BEYOND LIMIT!!!");
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
                foreach (ModuleItemHolder.Stack stack in itemHold.Stacks)
                {
                    if (GetBlockType == BlockTypes.GSOAIController_111)
                        break;
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
                if (SavedCount == 0)
                {
                    Empty = false;
                    GetChunkType = toManage.pickup.ChunkType;
                    //Save the chunk's color for gauge referencing
                    SaveTextureOfChunk();
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
                    Debug.Log("RandomAdditions: SILO INPUT REQUEST DENIED!!!  WRONG INPUT FORCED BY PLAYER!!!");
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
                if (SavedCount >= MaxCapacity)
                    break;
                var toManage = stack.FirstItem;
                if (SavedCount == 0)
                {
                    Empty = false;
                    GetBlockType = toManage.block.BlockType;
                }
                if (toManage.block.BlockType == GetBlockType)
                {
                    QueueStoreAnim(toManage);
                    if (Singleton.Manager<ManPlayer>.inst.InventoryIsUnrestricted) { }
                    else if (Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                    {
                        Singleton.Manager<NetInventory>.inst.HostAddItem(GetBlockType, 1);
                    }
                    else
                        Singleton.Manager<SingleplayerInventory>.inst.HostAddItem(GetBlockType, 1);
                }
                else
                {
                    Debug.Log("RandomAdditions: SILO INPUT REQUEST DENIED!!!  WRONG INPUT FORCED BY PLAYER!!!");
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
                    Debug.Log("RandomAdditions: SILO HAS A NULL SAVEDCHUNK TYPE!!!");
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
                    SaveTextureOfChunk();
                    if (SavedCount < 0)
                    {
                        Debug.Log("RandomAdditions: SILO HAS NEGATIVE RESOURCES!!!");
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
                        Debug.Log("RandomAdditions: SILO HAS A NULL SAVEDBLOCK TYPE!!!");
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
                            if (Singleton.Manager<NetInventory>.inst.IsAvailableToLocalPlayer(GetBlockType))
                            {
                                availQuant = Singleton.Manager<NetInventory>.inst.GetQuantity(GetBlockType);
                                if (availQuant > 0)
                                {
                                    availQuant--;
                                    Singleton.Manager<NetInventory>.inst.SetBlockCount(GetBlockType, availQuant);

                                    QueueReleaseAnim(stack);
                                    SavedCount = availQuant;
                                    if (availQuant <= 0)
                                    {
                                        Empty = true;
                                        GetBlockType = BlockTypes.GSOAIController_111;
                                        if (availQuant < 0)
                                            Debug.Log("RandomAdditions: SILO HAS NEGATIVE BLOCKS!!!");
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (Singleton.Manager<SingleplayerInventory>.inst.IsAvailableToLocalPlayer(GetBlockType))
                            {
                                availQuant = Singleton.Manager<SingleplayerInventory>.inst.GetQuantity(GetBlockType);
                                if (availQuant > 0)
                                {
                                    availQuant--;
                                    Singleton.Manager<SingleplayerInventory>.inst.SetBlockCount(GetBlockType, availQuant);

                                    QueueReleaseAnim(stack);
                                    SavedCount = availQuant;
                                    if (availQuant <= 0)
                                    {
                                        Empty = true;
                                        GetBlockType = BlockTypes.GSOAIController_111;
                                        if (availQuant < 0)
                                            Debug.Log("RandomAdditions: SILO HAS NEGATIVE BLOCKS!!!");
                                        break;
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
                    toManage = Singleton.Manager<ManSpawn>.inst.SpawnBlock(GetBlockType, siloSpawn.position, Quaternion.identity).visible;
                    toManage.block.InitNew();
                }
                stack.Take(toManage);
                ReleaseAnimating.Add(toManage);
            }
            else
            {
                Visible toManage;
                if (!StoresBlocksInsteadOfChunks)
                    toManage = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)GetChunkType), siloSpawn.position, Quaternion.identity);
                else
                {
                    toManage = Singleton.Manager<ManSpawn>.inst.SpawnBlock(GetBlockType, siloSpawn.position, Quaternion.identity).visible;
                    toManage.block.InitNew();
                }
                stack.Take(toManage);
                //toManage.SetHolder(null, true);
                toManage.SetGrabTimeout(1);//disable grabbing of it
                toManage.SetItemCollectionTimeout(1);//disable more
                toManage.SetInteractionTimeout(1);// disable ALL
                ReleaseTargetNode.Add(stack);
                ReleaseAnimating.Add(toManage);
                ReleaseAnimatingPos.Add(transform.InverseTransformPoint(toManage.centrePosition));
            }
        }


        // Utilities
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
        private void SaveTextureOfChunk()
        {
            switch (GetChunkType)
            {
                case ChunkTypes.Wood:
                    SavedChunkColor = new Color(1, 0.75f, 0.42f, 1);
                    break;
                case ChunkTypes.FibronChunk:
                    SavedChunkColor = new Color(0.99f, 0.85f, 0.49f, 1);
                    break;
                case ChunkTypes.RubberJelly:
                    SavedChunkColor = new Color(0.62f, 0.45f, 0.18f, 1);
                    break;
                case ChunkTypes.RubberBrick:
                    SavedChunkColor = new Color(0.28f, 0.28f, 0.28f, 1);
                    break;
                case ChunkTypes.LuxiteShard:
                    SavedChunkColor = new Color(0.82f, 0.77f, 0.13f, 1);
                    break;
                case ChunkTypes.LuxianCrystal:
                    SavedChunkColor = new Color(1f, 1f, 0.29f, 1);
                    break;
                case ChunkTypes.PlumbiteOre:
                    SavedChunkColor = new Color(0.62f, 0.62f, 0.62f, 1);
                    break;
                case ChunkTypes.PlumbiaIngot:
                    SavedChunkColor = new Color(0.73f, 0.73f, 0.73f, 1);
                    break;
                case ChunkTypes.TitaniteOre:
                    SavedChunkColor = new Color(0.48f, 0.79f, 1f, 1);
                    break;
                case ChunkTypes.TitanicAlloy:
                    SavedChunkColor = new Color(0.24f, 0.4f, 0.79f, 1);
                    break;
                case ChunkTypes.CarbiteOre:
                    SavedChunkColor = new Color(0.35f, 0.35f, 0.35f, 1);
                    break;
                case ChunkTypes.CarbiusBrick:
                    SavedChunkColor = new Color(0.1f, 0.1f, 0.1f, 1);
                    break;
                case ChunkTypes.RoditeOre:
                    SavedChunkColor = new Color(0.27f, 1f, 0.72f, 1);
                    break;
                case ChunkTypes.RodiusCapsule:
                    SavedChunkColor = new Color(0.08f, 0.83f, 0.65f, 1);
                    break;
                case ChunkTypes.OleiteJelly:
                    SavedChunkColor = new Color(0.38f, 0.12f, 0.12f, 1);
                    break;
                case ChunkTypes.OlasticBrick:
                    SavedChunkColor = new Color(0.91f, 0.26f, 0.26f, 1);
                    break;
                case ChunkTypes.IgniteShard:
                    SavedChunkColor = new Color(0.91f, 0.5f, 0.15f, 1);
                    break;
                case ChunkTypes.IgnianCrystal:
                    SavedChunkColor = new Color(1f, 0.62f, 0.01f, 1);
                    break;
                case ChunkTypes.EruditeShard:
                    SavedChunkColor = new Color(0.15f, 0.65f, 0.12f, 1);
                    break;
                case ChunkTypes.ErudianCrystal:
                    SavedChunkColor = new Color(0.25f, 0.96f, 0.17f, 1);
                    break;
                case ChunkTypes.CelestiteShard:
                    SavedChunkColor = new Color(0.23f, 0.78f, 1f, 1);
                    break;
                case ChunkTypes.CelestianCrystal:
                    SavedChunkColor = new Color(0.19f, 0.65f, 1f, 1);
                    break;
                default:
                    SavedChunkColor = new Color(0.46f, 0.52f, 46f, 1);
                    break;
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
                    Debug.Log("RandomAdditions: Silo " + gameObject.name + " has gone over their maximum stable speed and will eject everything to prevent exploding!");
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
        /// Run chunk "animator" module
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
                    toManage.trans.Recycle();
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
                            toManage.trans.localScale = Vector3.one * scaler;
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
        }


        // Chunk saving
        public void EmergencyEjectAllContents()
        {
            foreach (Visible toManage in AbsorbAnimating)
            {
                //was already added!
                toManage.trans.localScale = Vector3.one;
                toManage.Recycle();
            }
            foreach (Visible toManage in ReleaseAnimating)
            {
                toManage.trans.localScale = Vector3.one;
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
                    Debug.Log("RandomAdditions: Silo " + gameObject.name + " is unstable on death and has destroyed all their stored contents!");
                    SavedCount = 0;
                    GetChunkType = ChunkTypes.Null;
                    UpdateGaugesAndDisplays();
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
            else
                SavedCount = 0;
            GetBlockType = BlockTypes.GSOAIController_111;
            GetChunkType = ChunkTypes.Null;
            UpdateGaugesAndDisplays();
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
            if (saving)
            {   // On general saving
                //var tile = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position);
                //tile.
                //Debug.Log("RandomAdditions: isSaving " + tile.IsCreated + tile.IsLoaded + tile.IsPopulated);
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
                if (!Singleton.Manager<ManScreenshot>.inst.TakingSnapshot)
                {   // Only save on world save
                    SerialData serialData = new SerialData()
                    {
                        savedChunk = GetChunkType,
                        savedBlock = GetBlockType,
                        savedCount = SavedCount,
                        savedColorR = SavedChunkColor.r,
                        savedColorG = SavedChunkColor.g,
                        savedColorB = SavedChunkColor.b
                    };
                    serialData.Store(blockSpec.saveState);
                }
            }
            else
            {   //Load from snap
                try
                {
                    isSaving = false;
                    SerialData serialData2 = SerialData<SerialData>.Retrieve(blockSpec.saveState);
                    if (serialData2 != null)
                    {
                        GetBlockType = serialData2.savedBlock;

                        GetChunkType = serialData2.savedChunk;
                        SavedCount = serialData2.savedCount;
                        SavedChunkColor.r = serialData2.savedColorR;
                        SavedChunkColor.g = serialData2.savedColorG;
                        SavedChunkColor.b = serialData2.savedColorB;
                    }
                }
                catch { }
            }
        }
    }
}
