// The textures' proposal (Arturo's cuadro 148): what halving them looks like, without touching an asset. A lab alone.
using System;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The same pictures as SettlementLab.Soldados (the soldier's sheet, near) and MapLab.Day (a city map, BASTION_AT for
    /// how near) with every texture drawn at half its size (QualitySettings.globalTextureMipmapLimit = 1, in memory, for
    /// this run alone, put back as it quits): a 4096 atlas as 2048, and what is already 2048 as 1024, so a little worse than
    /// bringing the 4096 ones down alone. BASTION_TEX_MITAD=0: as they are, for the «before».
    ///   BASTION_TEX_MITAD=1 unity/tools/captura.sh -shotSetup Bastion.EditorTools.TexturasLab.Soldados -shotSize 1440x900 -shotOut &lt;file&gt;
    ///   BASTION_TEX_MITAD=1 BASTION_AT=20,20,8 unity/tools/captura.sh -shotSetup Bastion.EditorTools.TexturasLab.Mapa -shotCamera "Shot camera" -shotOut &lt;file&gt;
    /// </summary>
    public static class TexturasLab
    {
        static void Half()
        {
            int limit = Environment.GetEnvironmentVariable("BASTION_TEX_MITAD") == "1" ? 1 : 0;
            // (Unity writes QualitySettings.asset as the batch run quits: the first run of this lab left the project at half
            // its textures, 24/11, caught in git status and taken back. Put back before it quits; after a run, look that
            // `git status unity/Bastion/ProjectSettings` is clean all the same.)
            int was = QualitySettings.globalTextureMipmapLimit;
            UnityEditor.EditorApplication.quitting += () => QualitySettings.globalTextureMipmapLimit = was;
            QualitySettings.globalTextureMipmapLimit = limit;
            Debug.Log($"[texturas] every texture at 1/{1 << limit} of its size, for this run");
        }

        public static void Soldados()
        {
            Half();
            SettlementLab.Soldados();
        }

        /// <summary>PortraitLab.Muestra's busts (BASTION_OUT) so: unity/tools/unity.sh -executeMethod Bastion.EditorTools.TexturasLab.Retratos -quit</summary>
        public static void Retratos()
        {
            Half();
            PortraitLab.Muestra();
        }

        public static void Mapa()
        {
            Half();
            MapLab.Day();
        }
    }
}
