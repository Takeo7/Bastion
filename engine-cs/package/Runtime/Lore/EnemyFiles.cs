using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------- enemy files

        public sealed class EnemyFile
        {
            public EnemyFile(string what, string does, string tip)
            {
                What = what;
                Does = does;
                Tip = tip;
            }

            /// <summary>What it is, in the world's words.</summary>
            public string What { get; }
            /// <summary>What it does on the field.</summary>
            public string Does { get; }
            /// <summary>How to deal with it.</summary>
            public string Tip { get; }
        }

        /// <summary>The Bastion's file on each enemy, for the enemy sheet in combat.</summary>
        public static readonly IReadOnlyDictionary<string, EnemyFile> EnemyFiles = new Dictionary<string, EnemyFile>
        {
            ["zombie"] = new EnemyFile("El muerto corriente.", "Lento. No se cubre ni tiene miedo. Va a por el vivo más cercano que vea; si no ve a nadie, hacia el último ruido. Muerde cuerpo a cuerpo: uno de cada cuatro mordiscos contagia.", "Peligroso en grupo. El ruido los atrae."),
            ["xenoid"] = new EnemyFile("Zombi reciente y rápido.", "Corre más que los demás y ve de lejos. Ataca cuerpo a cuerpo y también contagia, algo menos que un zombi.", "Llega antes de lo que parece: pon vigilancia en su camino."),
            ["mec"] = new EnemyFile("Zombi con chaleco y casco.", "Blindaje 1: resta 1 de daño a cada golpe. Aguanta mucho. Es el que más contagia.", "Los explosivos le quitan el blindaje, y la munición perforante lo atraviesa."),
            ["sectoid"] = new EnemyFile("Zombi que grita.", "No muerde. Su grito desorienta o asusta a los soldados cercanos y se oye a 16 casillas: atrae a todos los zombis que lo oyen. Levanta a los muertos que tiene cerca.", "Mátalo primero."),
            ["bloater"] = new EnemyFile("Zombi lleno de gases.", "Lento. Revienta al morir o al llegar a un soldado: 3 de daño a quien tenga al lado, y contagia a uno de cada dos.", "Mátalo de lejos, y lejos de tus soldados."),
            ["trooper"] = new EnemyFile("Saqueador de los Cuervos.", "Escopeta o fusil de caza, y alguna granada. Se cubre y dispara.", "Rómpele la cobertura o flanquéalo. Sus disparos también atraen zombis."),
            ["officer"] = new EnemyFile("Jefe de un grupo de Cuervos.", "Mejor arma y más aguante que un Saqueador. Usa la vigilancia.", "Es el más peligroso del grupo: dispárale primero."),
            ["lancer"] = new EnemyFile("Cuervo que captura en vez de matar.", "Red y porra cuerpo a cuerpo: el soldado inmovilizado empieza su siguiente turno con una sola acción.", "Corre mucho y no necesita línea de tiro. Pon vigilancia en su camino y no dejes a nadie solo."),
            ["relay"] = new EnemyFile("La alarma: un coche patrulla, una ambulancia o la de una nave.", "No dispara. Tiene salud y blindaje 1. Mientras suena, la zona se llena de zombis.", "Es el objetivo: hay que callarla antes de que acabe la cuenta atrás. Los explosivos ayudan."),
        };
    }
}
