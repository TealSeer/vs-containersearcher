using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace ContainerSearcher
{
    public class ContainerSearcherModSystem : ModSystem
    {
        public IShaderProgram? highlightShader;
        private ContainerSearcherConfig config;
        private ICoreAPI coreAPI;
        private ICoreClientAPI clientAPI;
        private HighlightRenderer? currentHighlight;
        private int accum5s = 0;
        private long gameListenerId = -1;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        private void LoadConfig()
        {
            try
            {
                config = clientAPI.LoadModConfig<ContainerSearcherConfig>($"{Mod.Info.ModID}.json") ?? new();
                clientAPI.StoreModConfig(config, $"{Mod.Info.ModID}.json");
            } catch(Exception e)
            {
                Mod.Logger.Error($"Could not load config! Reason: {e}");
                config = new();
            }
        }

        private bool LoadShader()
        {
            highlightShader = clientAPI?.Shader.NewShaderProgram();
            if (highlightShader is null) return false;
            highlightShader.AssetDomain = Mod.Info.ModID;
            highlightShader.VertexShader = clientAPI?.Shader.NewShader(EnumShaderType.VertexShader);
            highlightShader.FragmentShader = clientAPI?.Shader.NewShader(EnumShaderType.FragmentShader);
            clientAPI?.Shader.RegisterFileShaderProgram("searchglow", highlightShader);
            return highlightShader.Compile();
        }

        public override void Start(ICoreAPI api)
        {
            coreAPI = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientAPI = api;
            clientAPI.Input.RegisterHotKey($"{Mod.Info.ModID}:search", Lang.Get($"{Mod.Info.ModID}:search"), GlKeys.U);
            clientAPI.Input.SetHotKeyHandler($"{Mod.Info.ModID}:search", SearchHotkey);
            if(!LoadShader())
            {
                Mod.Logger.Fatal("Shader failed to load!");
            }
            clientAPI.Event.ReloadShader += LoadShader;
            gameListenerId = clientAPI.Event.RegisterGameTickListener(OnGameTick1000ms, 1000);
            LoadConfig();
            base.StartClientSide(clientAPI);
        }

        public override void Dispose()
        {
            base.Dispose();
            currentHighlight?.Dispose();
            currentHighlight = null;
            if(gameListenerId != -1) clientAPI.Event.UnregisterGameTickListener(gameListenerId);
            highlightShader?.Dispose();
        }

        private void OnGameTick1000ms(float dt)
        {
            if (currentHighlight is null) return;
            if(++accum5s >= 5)
            {
                accum5s = 0;
                clientAPI.Event.UnregisterRenderer(currentHighlight, EnumRenderStage.Opaque);
                currentHighlight.Dispose();
                currentHighlight = null;
            }
        }

        private void CloseInventories(InventoryBase? inventory)
        {
            if (inventory is null) return;
            // Character inventory does not respond to CloseInventory so we have to close the GUI directly
            if (inventory is InventoryBasePlayer)
            {
                // Have to copy because calling TryClose modifies list mid-iteration
                foreach (var gui in new List<GuiDialog>(clientAPI.Gui.OpenedGuis))
                {
                    if (gui is (GuiDialogInventory or GuiDialogCharacter)) gui.TryClose();
                }
            }
            else
            {
                clientAPI.World.Player.InventoryManager.CloseInventoryAndSync(inventory);
            }
        }

        private void SnapToBlock(BlockPos block)
        {
            var blockCenter = block.ToVec3f().Add(0.5f, 0.0f, 0.5f);
            var playerCamera = clientAPI.World.Player.Entity.CameraPos.ToVec3f();
            var targetVec = new Vec3f(blockCenter.X - playerCamera.X, blockCenter.Y - playerCamera.Y, blockCenter.Z - playerCamera.Z).Normalize();
            var yaw = Math.Atan2(targetVec.X, targetVec.Z);
            var pitch = Math.PI + Math.Asin(-targetVec.Y);
            clientAPI.World.Player.CameraYaw = (float)yaw;
            clientAPI.World.Player.Entity.Pos.Pitch = (float)pitch;
        }

        private bool SearchHotkey(KeyCombination key)
        {
            if (currentHighlight is not null) return false;
            var hoveredStack = clientAPI.World.Player.InventoryManager.CurrentHoveredSlot?.Itemstack;
            var stackInventory = clientAPI.World.Player.InventoryManager.CurrentHoveredSlot?.Inventory;
            if (hoveredStack is null) return false;
            var blockAccessor = coreAPI.World.BlockAccessor;
            var playerLoc = clientAPI.World.Player.Entity.Pos.AsBlockPos;
            var radius = config.SearchRange;
            var minPos = playerLoc.AddCopy(-radius, -radius, -radius);
            var maxPos = playerLoc.AddCopy(radius, radius, radius);
            var matchList = new SortedList<float, BlockPos>();
            blockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) =>
            {
                var blockEntity = blockAccessor.GetBlockEntity<BlockEntityContainer>(new BlockPos(x, y, z));
                if (blockEntity is null) return;
                var blockContents = blockEntity.GetNonEmptyContentStacks(false);
                foreach(var thing in blockContents)
                {
                    if(thing.Class == hoveredStack.Class && thing.Id == hoveredStack.Id) {
                        var distanceToPlayer = blockEntity.Pos.DistanceTo(playerLoc);
                        matchList.Add(distanceToPlayer, blockEntity.Pos);
                        return;
                    }
                }
            });
            if (matchList.Count == 0) return true;
            CloseInventories(stackInventory);
            SnapToBlock(matchList.GetValueAtIndex(0));
            var highlight = new HighlightRenderer(matchList.Values, clientAPI);
            clientAPI.Event.RegisterRenderer(highlight, EnumRenderStage.Opaque, "searchhighlight");
            currentHighlight = highlight;
            return true;
        }

    }
}
