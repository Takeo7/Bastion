using UnityEditor;
using UnityEngine;

namespace Bastion.Client.Audio.EditorTools
{
    /// <summary>
    /// How the generated sound is imported (Resources/Generado, unity/tools/sonido.sh; docs/UNITY-SONIDO.md): all of it
    /// Vorbis, at its own rate. The effects, decompressed on load (the mix reads their samples once and keeps them); the
    /// live pieces and the combat's beds, minutes long, streamed from disk (a Tap takes them into the mix).
    /// </summary>
    internal sealed class SoundImport : AssetPostprocessor
    {
        private const string Folder = "/Client/Audio/Resources/Generado/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            var streamed = assetPath.Contains(Folder + "vivos/") || assetPath.Contains(Folder + "grabados/bed-");
            var s = importer.defaultSampleSettings;
            s.loadType = streamed ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = streamed ? 0.5f : 0.7f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = !streamed;
            importer.defaultSampleSettings = s;
            importer.forceToMono = false;
            importer.loadInBackground = streamed;
        }
    }
}
