// Forja3D — ajustes de importación del audio para móvil: efectos cortos descomprimidos (sin latencia),
// ambientes y música comprimidos en memoria (poca RAM), todo en mono.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Forja3D.EditorTools
{
    public class Forja3DAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Forja3DAudio/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            bool largo = assetPath.Contains("ambiente_") || assetPath.Contains("musica_");
            var s = imp.defaultSampleSettings;
            s.loadType = largo ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = largo ? 0.6f : 0.75f;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = largo;
        }
    }
}
#endif
