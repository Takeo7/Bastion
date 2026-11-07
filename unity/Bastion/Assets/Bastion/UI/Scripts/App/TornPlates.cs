using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// The torn plates' boxes by their size (Arturo's report of 7/11, the maqueta v39: the sprite «según las proporciones
    /// de cada tarjeta»): every `.panel` of every screen, once laid out and whenever its size changes, gets the class of
    /// the box that fits it (Mixta.uss): a small one, torn all round; a low one, torn along its top and foot; a wide one;
    /// else the maqueta's large one. The sprites are the sheet's, so they go into the build: this only picks.
    /// </summary>
    sealed class TornPlates : MonoBehaviour
    {
        /// <summary>How often the screens are looked over for plates not watched yet (they come and go with the screens).</summary>
        const float Look = 0.1f;

        /// <summary>A plate watched already.</summary>
        const string Watched = "plate--watched";

        static readonly string[] Kinds = { "plate--small", "plate--low", "plate--wide" };

        float lookedAt = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Make()
        {
            var o = new GameObject("Placas rasgadas");
            DontDestroyOnLoad(o);
            o.hideFlags = HideFlags.HideInHierarchy;
            o.AddComponent<TornPlates>();
        }

        void Update()
        {
            if (Time.unscaledTime - lookedAt < Look) return;
            lookedAt = Time.unscaledTime;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                doc.rootVisualElement?.Query(className: "panel").ForEach(Watch);
        }

        static void Watch(VisualElement plate)
        {
            if (plate.ClassListContains(Watched)) return;
            plate.AddToClassList(Watched);
            plate.RegisterCallback<GeometryChangedEvent>(_ => Fit(plate));
            Fit(plate);
        }

        /// <summary>
        /// The box for the plate's size, in HUD pixels: under 60 high or 100 wide, the small one; under 150 high, the low
        /// one (the large one's corners would meet); over 2.4 times as wide as high, the wide one; else the large one.
        /// </summary>
        static void Fit(VisualElement plate)
        {
            var size = plate.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0 || size.y <= 0) return;
            var kind = size.y < 60 || size.x < 100 ? 0 : size.y < 150 ? 1 : size.x > size.y * 2.4f ? 2 : -1;
            for (var i = 0; i < Kinds.Length; i++) plate.EnableInClassList(Kinds[i], i == kind);
        }
    }
}
