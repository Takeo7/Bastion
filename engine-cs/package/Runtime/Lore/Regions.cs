using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ regions

        public sealed class RegionLore
        {
            public RegionLore(string ally, string situation)
            {
                Ally = ally;
                Situation = situation;
            }

            /// <summary>Who sends the reports from there, as it reads after "Aviso de".</summary>
            public string Ally { get; }
            public string Situation { get; }
        }

        public static readonly IReadOnlyDictionary<string, RegionLore> RegionLores = new Dictionary<string, RegionLore>
        {
            ["weu"] = new RegionLore("el Refugio de Bielsa", "Los pueblos de la sierra, bajo el Bastión. La nieve y la altura frenan a los zombis; los Cuervos, no."),
            ["afr"] = new RegionLore("los del Hospital", "La primera ciudad caída. Calles llenas de zombis y farmacias que nadie ha terminado de vaciar."),
            ["eas"] = new RegionLore("la Red Callada", "Un embalse y una central muerta. La Red Callada sobrevivió hablando en lengua de signos: sin un ruido."),
            ["eeu"] = new RegionLore("los Pastores", "Valles cerrados y aldeas que aún resisten. Los Cuervos cobran aquí su peaje."),
            ["me"] = new RegionLore("los del Instituto", "La ciudad muerta donde empezó todo. El Instituto Koper sigue en pie, y nadie quiere entrar."),
            ["na"] = new RegionLore("los Camioneros", "La llanura por donde sube la Marea. Los camioneros la vigilan desde la carretera."),
            ["sa"] = new RegionLore("el Sindicato de la Mina", "Minas y carreteras cortadas. Los mineros resisten con túneles y dinamita."),
            ["oce"] = new RegionLore("las Bodegas", "Granjas, bodegas y un río. Es donde hay comida, y por eso donde más se pelea."),
        };
    }
}
