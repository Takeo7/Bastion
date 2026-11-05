using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A full-screen pass of the renderers (the fog's, the grade's: ScreenPassMake) drawn from the camera's picture into a
    /// new one, which URP then takes as the camera's (Arturo, 4/11, the performance pass). URP's own full-screen pass
    /// copies the picture first to read it, and that copy was most of what the grade cost (0.08 to 0.26 ms a frame) and a
    /// part of the fog's (0.04 to 0.17). The same pixels to the bit: what a stencil keeps the pass off (the fog's, off the
    /// figures) is drawn as the picture was by the material's `keepPass`, its stencil the other way round. On a camera
    /// whose picture goes on to another (a stack's), copied first and drawn over it, as URP's.
    /// </summary>
    public sealed class ScreenPass : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public Material passMaterial;
        public int passIndex;
        /// <summary>What the pass reads besides the picture (the depth, the normals).</summary>
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        /// <summary>The camera's depth and stencil bound, for the pass's stencil test.</summary>
        public bool bindDepthStencilAttachment;
        /// <summary>The material's pass that copies the picture where `passIndex`'s stencil test fails (−1: none fails).</summary>
        public int keepPass = -1;

        Pass pass;

        public override void Create()
        {
            pass = new Pass(name) { owner = this };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;
            if (passMaterial == null || passIndex < 0 || passIndex >= passMaterial.passCount || keepPass >= passMaterial.passCount) return;
            pass.renderPassEvent = injectionPoint;
            pass.ConfigureInput(requirements);
            // (A picture of the camera's own to read, not the screen's.)
            pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(pass);
        }

        sealed class Pass : ScriptableRenderPass
        {
            public ScreenPass owner;
            static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture"), BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");

            public Pass(string name) => profilingSampler = new ProfilingSampler(name);

            sealed class Data
            {
                public Material material;
                public int index, keep;
                public TextureHandle source;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_" + owner.name + "Picture";
                desc.clearBuffer = false;
                var other = renderGraph.CreateTexture(desc);
                bool swap = camera.resolveFinalTarget;
                if (!swap) renderGraph.AddBlitPass(source, other, Vector2.one, Vector2.zero, passName: "Copy " + passName);
                var (from, destination) = swap ? (source, other) : (other, source);
                using (var builder = renderGraph.AddRasterRenderPass<Data>(passName, out var data, profilingSampler))
                {
                    data.material = owner.passMaterial;
                    data.index = owner.passIndex;
                    data.keep = swap ? owner.keepPass : -1;
                    data.source = from;
                    builder.UseTexture(from, AccessFlags.Read);
                    // (As URP's full-screen pass binds what it needs.)
                    if ((owner.requirements & ScriptableRenderPassInput.Depth) != 0) builder.UseTexture(resources.cameraDepthTexture);
                    if ((owner.requirements & ScriptableRenderPassInput.Normal) != 0 && resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture);
                    if ((owner.requirements & ScriptableRenderPassInput.Color) != 0 && resources.cameraOpaqueTexture.IsValid()) builder.UseTexture(resources.cameraOpaqueTexture);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    if (owner.bindDepthStencilAttachment) builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((Data d, RasterGraphContext context) =>
                    {
                        block.Clear();
                        block.SetTexture(BlitTexture, d.source);
                        block.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.index, MeshTopology.Triangles, 3, 1, block);
                        if (d.keep >= 0) context.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.keep, MeshTopology.Triangles, 3, 1, block);
                    });
                }
                if (swap) resources.cameraColor = destination;
            }
        }
    }
}
