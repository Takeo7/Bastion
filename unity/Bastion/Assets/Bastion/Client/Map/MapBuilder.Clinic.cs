using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A clinic's own surfaces (0.10.1's medicalFinish.ts, inspired by the Apocalypse hospital reference): a map of the
    /// tiles that are a clinic's, its wear and how many storeys it has, for Building.shader to tile its floors and paint
    /// its walls' insides with; global, as the cutaway's and the ground's occlusion are. Wear changes the paint and the
    /// tiles, never what can be walked or seen.
    /// </summary>
    public sealed partial class MapBuilder
    {
        static readonly int MedicalMap = Shader.PropertyToID("_Medical"), MedicalSize = Shader.PropertyToID("_MedicalSize");
        Texture2D medicalMap;

        void MedicalFinish()
        {
            bool any = false;
            var pixels = new Color32[s.Width * s.Height];
            for (int i = 0; i < plan.Owner.Length && i < pixels.Length; i++)
            {
                int owner = plan.Owner[i];
                if (owner < 0 || owner >= plan.Buildings.Count || plan.Buildings[owner].medicalWear is not double wear) continue;
                pixels[i] = new Color32((byte)Mathf.RoundToInt(30 + (float)wear * 210), (byte)plan.Buildings[owner].storeys, 0, 255);
                any = true;
            }
            if (medicalMap == null || medicalMap.width != s.Width || medicalMap.height != s.Height)
            {
                if (medicalMap != null) DestroyNow(medicalMap);
                medicalMap = new Texture2D(s.Width, s.Height, TextureFormat.RGBA32, false, true) { name = "Clínicas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }
            medicalMap.SetPixels32(pixels);
            medicalMap.Apply(false);
            Shader.SetGlobalTexture(MedicalMap, medicalMap);
            Shader.SetGlobalVector(MedicalSize, new Vector4(s.Width, s.Height, any ? 1 : 0, 0));
        }
    }
}
