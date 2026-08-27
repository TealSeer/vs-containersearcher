using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
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

        private bool SearchHotkey(KeyCombination key)
        {
            if (currentHighlight is not null) return false;
            var hoveredStack = clientAPI.World.Player.InventoryManager.CurrentHoveredSlot?.Itemstack;
            if (hoveredStack is null) return false;
            var blockAccessor = coreAPI.World.BlockAccessor;
            var playerLoc = clientAPI.World.Player.Entity.Pos.AsBlockPos;
            var radius = config.SearchRange;
            var minPos = playerLoc.AddCopy(-radius, -radius, -radius);
            var maxPos = playerLoc.AddCopy(radius, radius, radius);
            var matchList = new List<BlockPos>();
            blockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) =>
            {
                var blockEntity = blockAccessor.GetBlockEntity<BlockEntityContainer>(new BlockPos(x, y, z));
                if (blockEntity is null) return;
                var blockContents = blockEntity.GetNonEmptyContentStacks(false);
                foreach(var thing in blockContents)
                {
                    if(thing.Class == hoveredStack.Class && thing.Id == hoveredStack.Id) {
                        matchList.Add(blockEntity.Pos);
                        return;
                    }
                }
            });
            if (matchList.Count == 0) return true;
            // Have to copy because calling TryClose modifies list mid-iteration
            foreach(var gui in new List<GuiDialog>(clientAPI.Gui.OpenedGuis))
            {
                gui.TryClose();
            }
            var highlight = new HighlightRenderer(matchList, clientAPI);
            clientAPI.Event.RegisterRenderer(highlight, EnumRenderStage.Opaque, "searchhighlight");
            currentHighlight = highlight;
            return true;
        }

    }
}
