using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ContainerSearcher
{
    public class HighlightRenderer(IList<BlockPos> blocks, ICoreClientAPI clientAPI) : IRenderer, IDisposable
    {
        public double RenderOrder => 0.99f;
        public int RenderRange => 24;
        protected IList<BlockPos> blocks = blocks;
        protected ICoreClientAPI clientAPI = clientAPI;
        private readonly WireframeCube cubeMesh = WireframeCube.CreateUnitCube(clientAPI);
        private readonly Matrixf mvMatrix = new();

        public void Dispose()
        {
            cubeMesh.Dispose();
        }

        void IRenderer.OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var render = clientAPI.Render;
            render.GLDisableDepthTest();
            var highlightShader = clientAPI.ModLoader.GetModSystem<ContainerSearcherModSystem>().highlightShader;
            if (highlightShader is null) return;
            var cameraPos = clientAPI.World.Player.Entity.CameraPos;
            highlightShader.Use();
            foreach(var block in blocks)
            {
                mvMatrix.Identity();
                mvMatrix.Set(clientAPI.Render.CameraMatrixOrigin);
                mvMatrix.Translate(block.X - cameraPos.X, block.Y - cameraPos.Y, block.Z - cameraPos.Z);
                highlightShader.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
                highlightShader.UniformMatrix("modelViewMatrix", mvMatrix.Values);
                render.RenderMesh(cubeMesh.modelRef);
            }
            highlightShader.Stop();
            render.GLEnableDepthTest();
        }
    }
}
