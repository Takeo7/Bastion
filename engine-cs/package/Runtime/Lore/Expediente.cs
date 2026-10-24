using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // -------------------------------------------------------------- Expediente

        public sealed class ExpedientePage
        {
            public ExpedientePage(string title, string summary, IReadOnlyList<string> paragraphs, string? note = null)
            {
                Title = title;
                Summary = summary;
                Paragraphs = paragraphs;
                Note = note;
            }

            public string Title { get; }
            /// <summary>The page in one line, for the level-up pop-up (the full page opens on request).</summary>
            public string Summary { get; }
            public IReadOnlyList<string> Paragraphs { get; }
            /// <summary>Handwritten in the margin, signed; not every page has one.</summary>
            public string? Note { get; }
        }

        /// <summary>One page per Bastion level: what the Bastion has found out by then.</summary>
        public static readonly IReadOnlyList<ExpedientePage> Expediente = new[]
        {
            new ExpedientePage(
                "Atalaya",
                "La Pandemia lo vació todo. Atalaya cayó en otoño; los que quedan empiezan de cero en un claro del pinar.",
                new[]
                {
                    "Hace dos inviernos llegó la Pandemia. Quien se contagia muere en un día y se levanta. Cayeron las ciudades, se fue el ejército y la radio se quedó muda.",
                    "Atalaya era una estación militar: muros de hormigón, un refugio bajo tierra y una sola carretera. Aguantó dos años. En otoño llegó una horda y fue derecha a ella, como si alguien le hubiera enseñado el camino. Salimos en el todoterreno de Teo, con la caravana enganchada detrás. El autobús se quedó en la cochera de Atalaya, y el nombre de La Lata ha pasado al todoterreno.",
                    "Hemos parado en un claro del pinar, sobre Bielsa. Lo que se levante aquí será el Bastión. Lo dirigen dos personas, y lo importante lo deciden juntas. El año pasado, en otro refugio, una líder sola lo vendió a los Cuervos a cambio de su vida.",
                },
                "Lo que entra por la puerta se apunta aquí. — N. A."),
            new ExpedientePage(
                "Notas de la doctora",
                "De noche ven igual que de día. Una puerta no los para. No todos los mordiscos contagian.",
                new[]
                {
                    "Notas de la Dra. Nwosu. La oscuridad no les quita nada: de noche ven igual que de día. Los que perdemos vista somos nosotros.",
                    "Una puerta cerrada no los para: la empujan y pasan. Solo les tapa la vista. Para frenarlos sirve un paso estrecho, no una puerta.",
                    "No todos los mordiscos contagian. El de un muerto corriente, uno de cada cuatro; el de un Acorazado, uno de cada tres. Los gases de un Hinchado, uno de cada dos.",
                }),
            new ExpedientePage(
                "Los Cuervos",
                "Los Cuervos atraen hordas con sirenas contra quien no les paga. Sin sirena, la zona respira.",
                new[]
                {
                    "Los Cuervos no son una banda: son muchas, con un mismo negocio. Cobran peaje en comida y en gente.",
                    "Su arma más sucia es la sirena de un coche patrulla. Lo llevan cerca de quien no paga, la encienden y dejan que la horda haga el resto.",
                    "Cuando se calló la sirena de los Llanos, los zombis se dispersaron en un día. Sin ruido, se pierden.",
                },
                "Las sirenas primero. Siempre. — N. A."),
            new ExpedientePage(
                "La Marea",
                "Una horda inmensa sube desde los Llanos con el deshielo. Desde hoy, la contamos en kilómetros.",
                new[]
                {
                    "La radio vuelve a funcionar, y lo primero que hemos oído son los demás enclaves contando lo mismo: algo sube desde los Llanos de Ceniza.",
                    "No es una horda: son todas a la vez. Miles de zombis caminando juntos hacia la sierra con el deshielo. La llaman la Marea.",
                    "Los Nidos la alimentan. Cada Nido que quememos la frena. Desde hoy, el reloj cuenta kilómetros.",
                },
                "Doce kilómetros. Contadlos. — N. A."),
            new ExpedientePage(
                "Los inmunes",
                "Hay gente a la que la Pandemia no mata. Su sangre podría ser una vacuna.",
                new[]
                {
                    "Hay gente a la que la Pandemia no mata. Se contagian, tienen fiebre y al día siguiente siguen vivos.",
                    "Los Cuervos lo saben y los cazan: un inmune vale más que un camión de comida.",
                    "La Dra. Nwosu dice que con la sangre de un inmune y los cuadernos del Instituto se podría hacer una vacuna.",
                },
                "No es un experimento. Es una niña. — A. N."),
            new ExpedientePage(
                "Koper",
                "La Pandemia salió del Instituto Koper. Teo estaba allí aquella noche.",
                new[]
                {
                    "Los cuadernos del Instituto Koper cuentan lo que pasó: buscaban una vacuna y crearon otra cosa. La primera muestra se perdió una noche de octubre.",
                    "Aquella noche había un vigilante de guardia. Firmó el parte de entrada a las once y el de salida a las seis. Entre medias, una puerta quedó abierta.",
                    "Ese vigilante era Teodor Vidić.",
                },
                "Lo sé desde el primer día. No sabía cómo decíroslo. — T. V."),
            new ExpedientePage(
                "La Congregación",
                "La Congregación empuja la Marea con campanas y radio. Solo la Voz sabe cómo pararla.",
                new[]
                {
                    "La Marea no sube sola. La Congregación cree que los zombis están salvados, y los guía con campanas hacia los vivos.",
                    "Los dirige una voz por radio: Celia Marsh, la que presentaba el programa de la tarde. Cada noche les dice hacia dónde caminar.",
                    "Si alguien sabe cómo parar la Marea, es ella. Hay que sacarla de su emisora.",
                },
                "Esta vez la necesitamos viva. — N. A."),
            new ExpedientePage(
                "La vacuna",
                "La vacuna está lista y la Marea, en la sierra. Allí decidiréis los dos qué hacer con ella.",
                new[]
                {
                    "La Dra. Nwosu ha terminado la vacuna: la sangre de Noor, los cuadernos de Koper y muchas noches sin dormir.",
                    "La Marea ya está en la sierra. Si la paramos, habrá que decidir qué hacemos con la vacuna, y eso lo decidiréis vosotros dos.",
                },
                "Volved. — N. A. y T. V."),
        };
    }
}
