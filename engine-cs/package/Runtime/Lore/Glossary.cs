using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------------- glossary

        /// <summary>One line for each word of the world, for tooltips.</summary>
        public static readonly IReadOnlyDictionary<string, string> Glossary = new Dictionary<string, string>
        {
            ["Pandemia"] = "Empezó hace dos inviernos en el Instituto Koper. Quien se contagia muere en un día y se levanta como zombi.",
            ["Zombi"] = "Va a por el vivo que ve. Si no ve a nadie, va hacia el último ruido que oyó.",
            ["Corredor"] = "Zombi rápido que ve de lejos.",
            ["Acorazado"] = "Zombi con chaleco y casco: tiene blindaje.",
            ["Chillón"] = "Zombi que no muerde: grita. El grito asusta a los soldados cercanos, atrae a los zombis que lo oyen y levanta a los muertos.",
            ["Hinchado"] = "Zombi lento. Revienta al morir o al llegar a un soldado: hace daño a quien tenga al lado y puede contagiarlo.",
            ["Cuervos"] = "Bandas de saqueadores armados. Atacan a otros vivos y atraen hordas con sirenas. Los zombis también van a por ellos.",
            ["Congregación"] = "Secta que cree que los zombis están salvados. Los guía con campanas y por radio.",
            ["Marea"] = "Horda enorme que sube desde los Llanos de Ceniza. Si llega al Bastión, se pierde la campaña.",
            ["Nido"] = "Sitio donde se juntan los zombis. Hace crecer la Marea; quemarlo la hace retroceder.",
            ["Sirena"] = "Mientras suena, atrae a todos los zombis de la zona. Puede ser un coche patrulla, una ambulancia o la alarma de una nave.",
            ["Ruido"] = "Cada acción hace ruido. Un disparo se oye a entre 12 y 22 casillas, según el arma; una granada, a 26; una motosierra, a 20; un registro, a 2. Los enemigos van a ver qué ha sido. El ruido no te descubre: te descubre que te vean.",
            ["Horda"] = "Los muertos que entran por los bordes. El ruido y los turnos que pasan llenan el medidor, en dB: a 90 se oye llegar una oleada, que entra un turno después. En la Limpieza solo lo llena el ruido; en las demás misiones, además, llegan unos pocos cada turno.",
            ["Saqueo"] = "Misión de registrar y volver con el botín. No hay límite de turnos, pero entran zombis por los bordes, y antes cuanto más ruido hagas.",
            ["Registrar"] = "Una acción, dentro de una habitación; cada una se registra una vez. Casas: comida y medicinas. Oficinas: piezas. Tiendas: comida. Almacenes: de todo.",
            ["Cajas"] = "Hasta tres por mapa: armas, medicinas, munición o comida. Se abren con una acción, desde al lado.",
            ["Mochila"] = "Tres huecos, cuatro con la Chapistería. Soltar no cuesta; pasar algo al de al lado, una acción. El botín cuenta si vuelve. Lo de un caído queda en el suelo.",
            ["Armería"] = "Las armas del Bastión. Cada una es de quien la lleva.",
            ["Cargadores"] = "Los hace el Taller: cada soldado sale con uno por tanda, hasta tres. Valen para todas las armas; recargar llena primero la principal.",
            ["Suministros"] = "Para construir, reparar, subir de nivel, reclutar, equipar y contactar zonas.",
            ["Medicinas"] = "Tratar a un mordido cuesta 2 ✚ (1 con la Enfermería a nivel 2). Una dosis quita una misión de baja a un herido. En la misión, curan 3 con una acción.",
            ["Mordisco"] = "El de un zombi puede contagiar. En la misión, el contagiado pierde 1 de salud cada turno y, sin Cuarentena, no puede subir a La Lata. Al volver tiene fiebre: si no se le trata, se convierte. La Enfermería y la Cuarentena dan más tiempo. Si muere contagiado, se levanta.",
            ["Cuarentena"] = "Sin ella, un mordido no puede subir a La Lata y se queda en la misión.",
            ["Comida"] = "Cada soldado come una ración al día; una misión es un día. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan a la mitad de velocidad.",
            ["Huerto"] = "Cada Huerto da 5 raciones por misión a nivel 1, 10 a nivel 2 y 15 a nivel 3.",
            ["Piezas"] = "Para que el Taller haga armas y armaduras mejores, y para reponer trampas.",
            ["Noche"] = $"De noche, a oscuras, tus soldados ven {Sight.Night} casillas en vez de {Sight.Day}. Una farola alumbra {Content.Night.StreetLight} casillas a su alrededor, y un foco, {Content.Night.Floodlight}. Hasta que te descubren, los enemigos notan a un soldado a oscuras a la mitad de distancia. Hay más zombis, y el botín vale la mitad más.",
            ["Enclave"] = "Supervivientes con los que hay un trato por radio: en cada convoy dejan lo pactado en un punto de cambio, mientras su zona no caiga. No son amigos.",
            ["Rumor"] = "Algo que se oye por radio en una zona. Si se escucha durante unas misiones, se sabe qué es.",
            ["Encargo"] = "Cadena de misiones y decisiones con un desenlace que se queda.",
            ["Taller de oficios"] = "Cada misión llena más las barras.",
            ["La Lata"] = "Lleva a la escuadra y la espera en la zona de evacuación. Todoterreno, furgoneta, camión y autobús: cada uno, más plazas.",
            ["Evacuar"] = "Se sale " + Bus.In + " desde la zona de evacuación: lo pide un jugador y lo acepta el otro. Quien esté fuera cuando sale, se pierde. En una Limpieza, salir es retirarse, y la misión se pierde.",
            ["Recluta"] = "Quien acaba de unirse al Bastión, con cuchillo y pistola.",
            ["Barras"] = "Seis: Disparo, Cuerpo a cuerpo, Lanzar, Curar, Investigar y Forma física. Se llenan haciendo eso mismo en las misiones. Cada barra llena sube un poco su habilidad y da un punto para el tablero.",
            ["Tablero"] = "Las habilidades de cada soldado, alrededor de su Instinto. Se compran con puntos de habilidad, al lado de una que ya tenga.",
            ["Veteranía"] = "Cuenta los puntos gastados en el tablero: un rango cada cinco (Novato, Fogueado, Curtido, Veterano y Superviviente). Cada rango da +10 de voluntad, y desde Veterano hay una tercera acción por turno.",
        };
    }
}
