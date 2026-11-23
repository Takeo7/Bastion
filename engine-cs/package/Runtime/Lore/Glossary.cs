using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------------- glossary

        /// <summary>One line for each word of the world, for tooltips.</summary>
        public static readonly IReadOnlyDictionary<string, string> Glossary = new Dictionary<string, string>
        {
            ["Pandemia"] = "Empezó hace dos inviernos en el Instituto Koper. Desde entonces, quien muere, sea de lo que sea, se levanta como zombi.",
            ["Zombi"] = "Va a por el vivo que ve. Si no ve a nadie, va hacia el último ruido que oyó.",
            ["Corredor"] = "Zombi rápido que ve de lejos.",
            ["Acorazado"] = "Zombi con chaleco y casco: tiene blindaje.",
            ["Chillón"] = "Zombi que no muerde: grita. El grito asusta a los soldados cercanos, atrae a los zombis que lo oyen y levanta a los muertos.",
            ["Hinchado"] = "Zombi lento. Revienta al morir o al llegar a un soldado: hace daño a quien tenga al lado.",
            ["Cuervos"] = "Bandas de saqueadores armados. Atacan a otros vivos y atraen hordas con sirenas. Los zombis también van a por ellos.",
            ["Congregación"] = "Secta que cree que los zombis están salvados. Los guía con campanas y por radio.",
            ["Marea"] = "Horda enorme que sube desde los Llanos de Ceniza. Si llega al Bastión, se pierde la campaña.",
            ["Nido"] = "Sitio donde se juntan los zombis. Hace crecer la Marea; quemarlo la hace retroceder.",
            ["Sirena"] = "Mientras suena, atrae a todos los zombis de la zona. Puede ser un coche patrulla, una ambulancia o la alarma de una nave.",
            ["Ruido"] = "Cada acción hace ruido. Un disparo se oye a entre 12 y 22 casillas, según el arma, y uno de arco o de ballesta, a 1 o 2; una granada, a 26; una motosierra, a 20; un registro, a 2. Los enemigos van a ver qué ha sido. El ruido no te descubre: te descubre que te vean.",
            // Gameplay, 16/11 (0.12.26): the extra movement and the dead's cone while hidden (Paths.ConcealedBonus, Los.InNoticeCone);
            // the unaware living's too since G23.5 (Gameplay, 21/11).
            ["Ocultación"] = $"Salvo en un Asedio, la escuadra empieza cada misión oculta: los enemigos no saben que estáis ahí. Mientras dura, cada soldado tiene {Content.ConcealedMobility} puntos de movimiento más, y un zombi, o un vivo que aún no está alerta, solo nota a quien tiene delante: por detrás o por un lado, no te nota aunque estés pegado a él. Se rompe cuando un enemigo ve a un soldado o cuando atacáis, salvo con el golpe silencioso o un tiro callado. Entonces cada soldado pierde {Content.ConcealedMobility} de los puntos de movimiento que le queden.",
            // Of the living too since G23.5 (Arturo, cuadro 99): the Saqueador and the Cazador, never the Cabecilla.
            ["Golpe silencioso"] = "Mata siempre, sin ruido y sin romper la ocultación, a un Zombi, un Corredor, un Saqueador o un Cazador que tengas al lado, si aún no está alerta y no te tiene delante. No sirve contra el Cabecilla ni con la motosierra.",
            // Arco y ballesta (cuadro 102): the kill with arrows nobody else sees (Rules.Attack, SilentKill), and their ammunition.
            ["Tiro callado"] = "Un tiro de arco o de ballesta que mata no rompe la ocultación ni despierta al grupo del muerto, si no os ve ningún otro enemigo. Es como el golpe silencioso, pero de lejos. Si no mata, o alguien os ve, es un tiro como otro.",
            ["Flechas"] = $"La munición del arco y la ballesta, {Content.ArrowsPile} en cada casilla de la mochila. El arco gasta una en cada tiro, y la ballesta, una al recargar. La que acierta se recupera al registrar el cuerpo; la que falla se pierde.",
            // N2 (Arturo, 22/11, cuadros 119, 122 and 127: through the windows, storeys 1 and 2; the roofs' parapet, by the stairs): Content.FallDamage a storey, through armour (Rules.MoveAlong); a floor a grenade
            // breaks, the same (Rules.BreakFloor). The dead jump too; the living take the stairs (Paths.Leaps).
            ["Saltar"] = $"Bajar de una planta por una ventana, sin escalera. Las azoteas tienen parapeto: de ellas se baja por la escalera. Quita {Content.FallDamage} de salud por planta, aunque lleve blindaje, y puede matar. No hace ruido. Si se llega por la escalera, se va por ella; si solo se llega saltando, se avisa antes. Cargando a alguien no se salta. Caer con el suelo que hunde una granada hace el mismo daño. Los muertos también saltan y se hacen daño; los vivos bajan por la escalera.",
            // N9 (cuadros 114, 115 and 118): the game's level, and a stage's (RunMap.DifficultyOf: more and harder groups, a bigger
            // horde, a little more found), which is another thing.
            ["Dificultad"] = "La de la partida: Fácil, Medio, Difícil o Supervivencia. Se elige al crearla y no se cambia. Cambia cuántos muertos hay, lo rápida que es la horda y cuántos suministros se encuentran y se ganan. En Supervivencia, además, las heridas no se curan al volver a la base. En todas, si no queda ningún soldado vivo, ni en la base, la partida se pierde.",
            ["Peligro"] = "Lo dura que es una etapa: sube con la fila del mapa y con el capítulo. Con más peligro, los enemigos son más y más duros, la horda trae más muertos y se encuentran algo más de suministros. No es la dificultad de la partida.",
            // The expeditions' boss of chapter 1 (H6).
            ["El Toro"] = "El jefe del capítulo 1: un muerto enorme que embiste y tumba. Matarlo supera el capítulo.",
            ["En el suelo"] = "Tras una embestida, el soldado queda en el suelo hasta vuestro turno: −20 de defensa y sin vigilancia. Levantarse le cuesta una acción.",
            ["Horda"] = "Los muertos que entran por los bordes. El ruido llena el medidor, en dB: a 90 se oye llegar una oleada, que entra un turno después. Salvo en la Limpieza, además, llegan unos pocos cada turno, el doble mientras suena la alarma de un Sabotaje.",
            ["Saqueo"] = "Misión de registrar y volver con el botín. Entran zombis por los bordes, y antes cuanto más ruido hagas.",
            ["Registrar"] = "Una acción, dentro de una habitación; cada una se registra una vez. Casas: comida y medicinas. Oficinas: piezas. Tiendas: comida. Almacenes: de todo.",
            ["Cajas"] = "Hasta tres por mapa: armas, medicinas, munición o comida. Se abren con una acción, desde al lado.",
            ["Mochila"] = "Tres huecos, cuatro con la Chapistería. Lo que es igual va en el mismo hueco, sin límite, y cabe aunque esté llena; cada arma, en el suyo. Soltar no cuesta; pasar algo al de al lado, una acción. El botín cuenta si vuelve. Lo de un caído queda en el suelo.",
            ["Armería"] = "Las armas del Bastión. Cada una es de quien la lleva.",
            ["Cargadores"] = "Los hace el Taller: cada soldado sale con uno por tanda, hasta tres. Valen para todas las armas; recargar llena primero la principal.",
            ["Suministros"] = "Para construir, reparar, subir de nivel, reclutar, equipar y contactar zonas.",
            ["Medicinas"] = "Una dosis quita una misión de baja a un herido. En la misión, curan 3 con una acción; con Manos de enfermero, sin gastarla.",
            ["Mordisco"] = "Solo hace daño: no contagia. Un soldado que muere en una misión, lo mate quien lo mate, se levanta como zombi.",
            ["Comida"] = "Cada soldado come una ración al día; una misión es un día. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan a la mitad de velocidad.",
            ["Huerto"] = "Cada Huerto da 5 raciones por misión a nivel 1, 10 a nivel 2 y 15 a nivel 3.",
            ["Piezas"] = "Para que el Taller haga armas y armaduras mejores, y para reponer trampas.",
            ["Noche"] = $"De noche, a oscuras, tus soldados ven {Sight.Night} casillas en vez de {Sight.Day}. Una farola alumbra {Content.Night.StreetLight} casillas a su alrededor, y un foco, {Content.Night.Floodlight}. Hasta que te descubren, los enemigos notan a un soldado a oscuras a la mitad de distancia. Hay más zombis, y el botín vale la mitad más.",
            ["Enclave"] = "Supervivientes con los que hay un trato por radio: en cada convoy dejan lo pactado en un punto de cambio, mientras su zona no caiga. No son amigos.",
            ["Rumor"] = "Algo que se oye por radio en una zona. Si se escucha durante unas misiones, se sabe qué es.",
            ["Encargo"] = "Cadena de misiones y decisiones con un desenlace que se queda.",
            ["Taller de oficios"] = "Cada misión llena más las barras.",
            ["La Lata"] = "Lleva a la escuadra y la espera en la zona de evacuación. Moto, todoterreno, furgoneta y autobús: cada uno, más plazas.",
            ["Evacuar"] = "Al llegar a una casilla de " + Bus.Name + ", el soldado sube y está a salvo; en su turno puede bajar. " + Bus.Name + " sale cuando lo pide un jugador y lo acepta el otro, o sola cuando ya están todos dentro. Quien esté fuera se pierde. En una Limpieza, salir antes de acabar es retirarse: la etapa se supera sin la recompensa.",
            ["Recluta"] = "Quien acaba de unirse al Bastión, con un cuchillo.",
            ["Barras"] = "Seis: Disparo, Cuerpo a cuerpo, Lanzar, Curar, Registrar y Forma física. Se llenan haciendo eso mismo en los combates. Cada barra llena sube un poco su habilidad y da un punto para el tablero.",
            ["Tablero"] = "Las habilidades de cada soldado, alrededor de su Instinto. Se compran con puntos de habilidad, al lado de una que ya tenga.",
            ["Veteranía"] = "Cuenta los puntos gastados en el tablero: un rango cada cinco (Novato, Fogueado, Curtido, Veterano y Superviviente). Cada rango da +10 de voluntad, y desde Veterano hay una tercera acción por turno.",
        };
    }
}
