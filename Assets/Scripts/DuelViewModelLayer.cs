using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HelloWorld
{
    // Local presentation only: draw the held model between spatial labels and gameplay UI.
    internal sealed class DuelViewModelLayer : System.IDisposable
    {
        private const int Layer = 30;
        private const int Mask = 1 << Layer;
        private readonly Camera source, camera;
        private readonly bool sourceIncludedLayer;
        private readonly Dictionary<Transform, Renderer[]> cache = new Dictionary<Transform, Renderer[]>();
        private readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
        private RenderTexture texture;
        private bool visible;
        private readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        public Texture Texture => texture;
        public bool Visible => visible;

        public DuelViewModelLayer(Camera source, Transform parent)
        {
            this.source = source;
            sourceIncludedLayer = (source.cullingMask & Mask) != 0;
            var go = new GameObject("First person model camera"); go.transform.SetParent(parent, false);
            camera = go.AddComponent<Camera>(); camera.enabled = false;
        }
        public void BeginFrame()
        {
            foreach (var pair in originalLayers) if (pair.Key != null) pair.Key.layer = pair.Value;
            originalLayers.Clear();
            visible = false;
        }
        public void Include(Transform root)
        {
            if (root == null) return;
            if (!cache.TryGetValue(root, out var renderers))
                cache[root] = renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var go = renderer.gameObject;
                if (originalLayers.ContainsKey(go)) continue;
                originalLayers.Add(go, go.layer); go.layer = Layer;
            }
        }
        public void Present()
        {
            source.cullingMask &= ~Mask;
            if (originalLayers.Count == 0) return;
            int width = Mathf.Max(1, source.pixelWidth), height = Mathf.Max(1, source.pixelHeight);
            if (texture == null || texture.width != width || texture.height != height)
            {
                ReleaseTexture();
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { name = "First person model RGBA", antiAliasing = 1 };
                texture.Create();
            }
            camera.CopyFrom(source);
            camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            camera.targetTexture = texture; camera.cullingMask = Mask;
            camera.rect = new Rect(0, 0, 1, 1); camera.depth = source.depth + 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.allowHDR = false; camera.allowMSAA = false; camera.nearClipPlane = .025f;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.requiresColorTexture = false; data.requiresDepthTexture = false;
            // Render before UI composition even when the player is running without an on-screen camera render.
            camera.enabled = false;
            request.destination = texture;
            RenderPipeline.SubmitRenderRequest(camera, request);
            visible = true;
        }
        private void ReleaseTexture()
        {
            camera.targetTexture = null;
            if (texture == null) return;
            texture.Release(); Object.Destroy(texture); texture = null;
        }
        public void Dispose()
        {
            BeginFrame(); ReleaseTexture();
            if (source != null && sourceIncludedLayer) source.cullingMask |= Mask;
            if (camera != null) Object.Destroy(camera.gameObject);
        }
    }
}
