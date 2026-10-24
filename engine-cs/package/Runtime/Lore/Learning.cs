using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------------- learning

        /// <summary>How each deed reads in the debrief (docs/FASE10.md §3), one and many, and whether the noun is feminine.</summary>
        private static readonly IReadOnlyDictionary<string, (string One, string Many, bool Feminine)> DeedWords = new Dictionary<string, (string, string, bool)>
        {
            [DeedIds.Shot] = ("disparo", "disparos", false),
            [DeedIds.Hit] = ("acierto", "aciertos", false),
            [DeedIds.GunKill] = ("baja", "bajas", true),
            [DeedIds.Swing] = ("tajo", "tajos", false),
            [DeedIds.Blow] = ("golpe", "golpes", false),
            [DeedIds.BladeKill] = ("baja", "bajas", true),
            [DeedIds.Throw] = ("lanzamiento", "lanzamientos", false),
            [DeedIds.ThrowHit] = ("lanzamiento con blanco", "lanzamientos con blanco", false),
            [DeedIds.HealOther] = ("cura a un compañero", "curas a compañeros", true),
            [DeedIds.HealSelf] = ("cura propia", "curas propias", true),
            [DeedIds.Search] = ("registro", "registros", false),
            [DeedIds.Fix] = ("arreglo", "arreglos", false),
            [DeedIds.Hunker] = ("vez a cubierto", "veces a cubierto", true),
            [DeedIds.Run] = ("carrera", "carreras", true),
        };

        private static readonly string[] Numbers = { "", "un", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez" };

        /// <summary>`NUMBERS[n] ?? n`: the word up to ten, the figure after.</summary>
        private static string NumberWord(int n)
        {
            return n >= 0 && n < Numbers.Length ? Numbers[n] : $"{n}";
        }

        /// <summary>«un acierto», «una baja», «tres golpes», «12 registros».</summary>
        private static string DeedCount(string deed, int n)
        {
            var (one, many, feminine) = DeedWords[deed];
            if (n == 1) return $"{(feminine ? "una" : "un")} {one}";
            return $"{NumberWord(n)} {many}";
        }

        /// <summary>
        /// The debrief's line for a skill a mission taught: «+3 de Tiro: dos aciertos y
        /// una baja». What the base adds on top (the Taller de oficios, the Camposanto)
        /// goes at the end: «… (+1 en la base)». `deeds`: Deeds, times each was done.
        /// </summary>
        public static string LearnedLine(string skill, int gained, IReadOnlyDictionary<string, int> deeds)
        {
            var own = DeedIds.All.Where((d) => Bars.DeedXp[d].Skill == skill && deeds.TryGetValue(d, out var n) && n > 0).ToList();
            var raw = own.Sum((d) => Bars.DeedXp[d].Xp * deeds[d]);
            var why = own.Select((d) => DeedCount(d, deeds[d])).ToList();
            var head = $"+{gained} de {Bars.SkillDefs[skill].Name}";
            var @base = gained > raw && raw > 0 ? $" (+{gained - raw} en la base)" : "";
            return why.Count > 0 ? $"{head}: {JoinAnd(why)}{@base}" : head;
        }

        /// <summary>
        /// Bars filled on coming back (docs/FASE10.md §2), one line per line: «Ana
        /// llena una barra de Disparo: +7 % de puntería con armas de fuego. Tiene un
        /// punto para el tablero.». `growth`, what each bar added.
        /// </summary>
        public static string BarLine(string who, string skill, IReadOnlyList<int> growth)
        {
            var n = growth.Count;
            var bars = n == 1 ? "una barra" : $"{NumberWord(n)} barras";
            var percent = growth.Sum();
            var stat = LowerFirst(Bars.SkillDefs[skill].Stat);
            return $"{who} llena {bars} de {Bars.SkillDefs[skill].Name}: +{percent} % de {stat}. {(n == 1 ? "Tiene un punto de habilidad" : $"Tiene {NumberWord(n)} puntos de habilidad")} para el tablero.";
        }

        /// <summary>
        /// A weapon that comes back worn or broken (docs/FASE9.md §5): «La escopeta de Ana
        /// vuelve gastada.», «El fusil de asalto de Kofi vuelve roto.». Its gender from
        /// the article of FOUND_WEAPONS' find.
        /// </summary>
        public static string WornLine(string who, string kind, bool broken = false)
        {
            var w = Content.FoundWeapons[kind];
            var female = w.Find.StartsWith("una ", StringComparison.Ordinal);
            return $"{(female ? "La" : "El")} {w.Name.ToLowerInvariant()} de {who} vuelve {(broken ? (female ? "rota" : "roto") : female ? "gastada" : "gastado")}.";
        }

        /// <summary>
        /// Each cell of the hex board (board.ts), in a line of the world, in the
        /// Bastión's voice: never the effect, which the sheet shows apart
        /// (docs/FASE10.md §6). By cell id, the core's too.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> BoardAbout = new Dictionary<string, string>
        {
            ["core"] = "Lo que le ha mantenido vivo hasta hoy. Lo demás se aprende encima.",
            ["steadyHand"] = "Respirar, soltar el aire a medias, y entonces.",
            ["grip"] = "Cinta en el mango y la muñeca firme.",
            ["looseArm"] = "Se pasó la infancia tirando piedras al río.",
            ["bandage"] = "Apretar donde sangra, y apretar bien.",
            ["tinker"] = "Desmonta cualquier cosa para ver cómo va.",
            ["stamina"] = "Subía a la sierra corriendo antes de que hubiera de qué huir.",
            ["lightTrigger"] = "El gatillo no se aprieta: se acaricia.",
            ["sidearm"] = "La pistola es lo último que queda. Más vale que sirva.",
            ["edge"] = "Una hoja afilada cada noche corta a la primera.",
            ["sidestep"] = "Un muerto embiste en línea recta. Basta con no estar ahí.",
            ["sharpStone"] = "Mejor una piedra con aristas que una redonda.",
            ["curtain"] = "Humo de sobra para cruzar una calle.",
            ["calmVoice"] = "Hablar bajo cuando todos gritan.",
            ["revival"] = "Un cachete, su nombre en voz alta, y vuelve.",
            ["lookout"] = "Mira antes de entrar. Siempre.",
            ["quiet"] = "Abre los cajones como si hubiera alguien durmiendo.",
            ["crouch"] = "Lo primero que se aprende fuera.",
            ["quickReflex"] = "Salta antes de saber por qué.",
            ["aimedShot"] = "Es lo único que los para del todo.",
            ["closeIn"] = "A esa distancia no hay excusas.",
            ["fineTrigger"] = "Una pistola bien cuidada no tiembla.",
            ["elbowRoom"] = "Cuando le rodean, hay que abrir hueco a golpes.",
            ["bladeCrit"] = "A la sien, a la nuca. Donde acaba.",
            ["closeRanks"] = "Juntos, no se cuela nadie.",
            ["salvo"] = "La anilla, el brazo, y a lo siguiente.",
            ["bigCharge"] = "Teo pone algo más de lo que dice el manual.",
            ["decoy"] = "Un golpe contra una chapa lejos de aquí, y todos miran allí.",
            ["bridge"] = "Los del otro también son de los nuestros.",
            ["bigKit"] = "Una bolsa más grande y mejor ordenada.",
            ["firstAid"] = "Primero se aparta al herido de la línea de tiro; luego se le cura.",
            ["patchUp"] = "Cinta, alambre y un poco de paciencia.",
            ["deft"] = "Las cerraduras le duran poco.",
            ["crossfire"] = "Dos ángulos y ningún sitio donde esconderse.",
            ["sprinter"] = "Cuando hay que correr, llega antes que nadie.",
            ["lungs"] = "Aguanta sin resollar donde otros se paran.",
            ["dodgeFire"] = "De esquina en esquina, nunca en línea recta.",
            ["rapidFire"] = "Cuando no hay tiempo para apuntar dos veces.",
            ["flanker"] = "Por donde no miran, duele más.",
            ["rooftop"] = "Arriba se respira, y se ve todo.",
            ["coldBlood"] = "Esperar a que se acerquen sin que tiemble la mano.",
            ["implacable"] = "Sin pararse a mirar al que acaba de caer.",
            ["heavyBlow"] = "Todo el peso del cuerpo en el golpe.",
            ["parry"] = "Por debajo de la línea de tiro, siempre.",
            ["grit"] = "Ha visto de cerca lo que otros solo ven de lejos.",
            ["goodArm"] = "Llega más lejos, y siempre lleva una de más.",
            ["shrapnel"] = "Tornillos y clavos dentro de la carga.",
            ["dazzle"] = "Un destello de más en el bolsillo.",
            ["overhand"] = "Por encima del hombro, llega más lejos.",
            ["lightHands"] = "Nwosu le enseñó qué dar, cuánto y cuándo.",
            ["suture"] = "Aguja, hilo y pulso.",
            ["bedside"] = "Alguien que se sienta al lado y no se va.",
            ["toughSkin"] = "Las heridas pequeñas ya ni las nota.",
            ["care"] = "Limpia el arma cada noche, aunque no la haya usado.",
            ["sideBySide"] = "Con el otro al lado, se apunta mejor.",
            ["parapet"] = "El compañero cubre lo que uno no ve.",
            ["picklock"] = "Un alambre doblado abre más puertas que una llave.",
            ["runAndGun"] = "Cuando hace falta, saca fuerzas de donde no hay.",
            ["sturdy"] = "Nació para trabajar, no para quejarse.",
            ["temper"] = "Ya no le tiembla nada.",
            ["fixedPost"] = "Elige su sitio y no se mueve de él.",
            ["chainShot"] = "Si el primero entra, el segundo ya va de camino.",
            ["hollowPoint"] = "Munición de caza, limada en el taller de Teo.",
            ["bigBore"] = "Una pistola vieja, de las que ya no se fabrican.",
            ["sentryCrit"] = "Quien espera, elige dónde dar.",
            ["deathFromAbove"] = "Desde arriba, uno cae y el siguiente ya está en la mira.",
            ["bladeMaster"] = "Años de monte antes de que todo empezara.",
            ["earBlow"] = "Un buen golpe en la cabeza aturde hasta a los muertos.",
            ["butcher"] = "Antes de la Pandemia, trabajaba en un matadero.",
            ["bladeWall"] = "Quien llega a su lado no da el segundo paso.",
            ["backstab"] = "Si no le ve llegar, no se gira a tiempo.",
            ["safeBlast"] = "Sabe dónde están los suyos antes de soltar la anilla.",
            ["blastWave"] = "La metralla vuela más lejos de lo que parece.",
            ["bandolier"] = "Una cinta cruzada al pecho, y una granada más.",
            ["boulder"] = "No hace falta que sea bonita, solo que pese.",
            ["demolition"] = "Abría galerías en la mina de Rudnik con dinamita.",
            ["restoration"] = "Una vuelta por todos, sin dejarse a nadie.",
            ["surgeon"] = "Ha cosido en sitios peores que este.",
            ["lullaby"] = "Sabe qué decir para que nadie salga corriendo.",
            ["spares"] = "Vendas y gasas de sobra en cada bolsillo.",
            ["triage"] = "Mientras venda, se pone en medio.",
            ["morePatch"] = "Siempre le queda un trozo de cinta.",
            ["binoculars"] = "Unos prismáticos de cazador, sin una raya.",
            ["coolHead"] = "Primero piensa, luego se mueve.",
            ["shoulder"] = "Dos que se conocen pelean como uno.",
            ["pointOut"] = "Un gesto, y el otro ya sabe dónde disparar.",
            ["shadowstep"] = "Cuando quieren apuntarle, ya no está.",
            ["leathery"] = "Ni los muertos pueden con su pellejo.",
            ["eel"] = "Se escurre entre las manos de cualquiera.",
            ["vest"] = "Ha salido de peores.",
            ["dugIn"] = "Sacos terreros, y a esperar.",
            ["lightningHands"] = "La pistola sale antes de que nadie se dé cuenta.",
            ["apRounds"] = "Todo chaleco tiene un hueco. Hay que saber dónde.",
            ["shredder"] = "Lo que no atraviesa, rompe.",
            ["serenity"] = "Si da, da de verdad.",
            ["coveringFire"] = "Mientras vigila, nadie toca a los suyos.",
            ["noEscape"] = "De cerca, ya no hay adónde huir.",
            ["steelNerves"] = "Ha oído gritar a un Chillón muchas veces.",
            ["cleanCut"] = "Un tajo, y no hace falta otro.",
            ["waist"] = "Un giro de cadera vale más que un escudo.",
            ["onGuard"] = "La hoja en alto y los pies quietos.",
            ["hardHead"] = "Se ha llevado más golpes de los que cuenta.",
            ["sweep"] = "Un paso de más es un mordisco.",
            ["thrower"] = "Una piedra bien tirada llega a la otra acera.",
            ["smokeEyes"] = "Sabe dónde estaban antes de que subiera el humo.",
            ["doubleCharge"] = "El doble de lo que hace falta, por si acaso.",
            ["crater"] = "Donde estalla, no queda nada en pie.",
            ["fog"] = "Para salir de un sitio sin que lo vean.",
            ["stoning"] = "Una buena pedrada tumba a cualquiera.",
            ["rounds"] = "Otra vuelta antes de que acabe el día.",
            ["fieldMedic"] = "Ha curado tirado en el barro, bajo la lluvia.",
            ["ironBlood"] = "Come poco, duerme menos y no enferma nunca.",
            ["cheer"] = "Con alguien así al lado, se aguanta un poco más.",
            ["courage"] = "Tiene miedo, como todos. No se le nota.",
            ["kitBag"] = "El maletín de un médico de pueblo, encontrado en una casa vacía.",
            ["engineer"] = "Trabajaba en la central del Pantano Gris.",
            ["magnifier"] = "Se fija en lo que nadie mira.",
            ["weakSpot"] = "Todo tiene un punto por donde cede.",
            ["temperance"] = "Ha oído gritos peores que los de un Chillón.",
            ["lynx"] = "Ve moverse algo en la niebla antes que nadie.",
            ["bodge"] = "No queda bonito, pero aguanta hasta casa.",
            ["marathon"] = "Va y vuelve de Bielsa sin parar.",
            ["ironBody"] = "Las caídas, los golpes, el frío: todo le resbala.",
            ["poise"] = "Nada le hace perder la calma.",
            ["feline"] = "Cae de pie y sigue corriendo.",
            ["ironWill"] = "Sabe por qué sigue, y eso le basta.",
            ["agile"] = "Salta vallas como si no estuvieran.",
            ["killZone"] = "Una calle, un arma y toda la noche.",
            ["faceoff"] = "Cuando le rodean, se dispara a todos a la vez.",
            ["rupture"] = "Una herida abierta por la que entra todo lo demás.",
            ["suppression"] = "Que no asome la cabeza. Ni lo intente.",
            ["marker"] = "Donde da uno, apuntan todos.",
            ["heavyHand"] = "El arma de siempre, y la costumbre de usarla.",
            ["eagleEye"] = "Lo que otros ven como una mancha, aquí tiene cara.",
            ["secondWind"] = "Cada uno que cae le da fuerzas para el siguiente.",
            ["cleaver"] = "Lo que corta leña corta hueso.",
            ["slippery"] = "Nunca está donde le esperan.",
            ["duelist"] = "De uno en uno, no hay muerto que aguante.",
            ["rampart"] = "Cuero, cinta y lo que haga falta bajo la ropa.",
            ["bladeDance"] = "La hoja, siempre en medio de lo que viene.",
            ["killerInstinct"] = "Lo que antes era alguien ya no lo es. No lo piensa.",
            ["blaster"] = "Las cargas las prepara a mano, y nunca le han fallado.",
            ["shockwave"] = "Lo que no alcanza la metralla, lo alcanza el golpe.",
            ["cartridge"] = "Cada hueco del cinturón, aprovechado.",
            ["flares"] = "Para cuando hay que cegar a muchos a la vez.",
            ["lure"] = "Los muertos van al ruido. Basta con decidir dónde suena.",
            ["ironArm"] = "Años cargando sacos en el puerto de Koper.",
            ["sling"] = "Una tira de cuero y un buen giro.",
            ["miracles"] = "Lo que otros darían por perdido, se cose.",
            ["presence"] = "Basta con que esté para que los demás se calmen.",
            ["vigor"] = "Se recupera antes que nadie.",
            ["serenityMind"] = "Ha visto morir a mucha gente. Ya no se asusta.",
            ["warKit"] = "Material militar que se sacó de Atalaya.",
            ["fieldHospital"] = "Donde se para, se cura a todos.",
            ["goodHand"] = "Los heridos se calman en cuanto le ven las manos.",
            ["mastermind"] = "Teo dice que tiene manos de taller.",
            ["inseparable"] = "Saben dónde está el otro sin mirar.",
            ["watchman"] = "Mientras uno avanza, el otro mira por los dos.",
            ["anatomy"] = "Sabe dónde dar para que no se levanten.",
            ["watchtower"] = "Desde lo alto de cualquier cosa, todo el pueblo.",
            ["shadow"] = "Se mueve junto a las paredes, sin ruido y sin prisa.",
            ["ingenuity"] = "Con lo que hay a mano, arregla lo que haga falta.",
            ["titan"] = "Grande como un armario, y igual de difícil de mover.",
            ["longRunner"] = "Lleva dos inviernos sin dejar de correr.",
            ["rock"] = "Le pueden dar. No le mueven.",
            ["untouchable"] = "Dicen que nadie le ha puesto la mano encima.",
            ["bulwark"] = "Donde se planta, los demás se ponen detrás.",
            ["lastStand"] = "No piensa caer antes que nadie.",
            ["tireless"] = "Nunca se sienta antes que los demás.",
        };

        /// <summary>Floating over a soldier whose bar fills in the field (docs/PRUEBA-2026-10-16.md #10): «DISPARO +7 %».</summary>
        public static string BarFillFloat(string skill, int percent)
        {
            return $"{Bars.SkillDefs[skill].Name.ToUpperInvariant()} +{percent} %";
        }

        /// <summary>What filling a bar or reaching a rank gives: `Short` beside the bar, `Long` for its tooltip.</summary>
        public sealed class RewardText
        {
            public RewardText(string @short, string @long)
            {
                Short = @short;
                Long = @long;
            }

            public string Short { get; }
            public string Long { get; }
        }

        /// <summary>
        /// At the end of a bar (#16): what filling it gives. `short` beside the bar,
        /// «+5–10 % · +1 punto»; `long` for its tooltip. «Punto de habilidad» in full:
        /// no «PH», which reads as the pH and is the only acronym the game would have.
        /// </summary>
        public static RewardText BarReward(string skill)
        {
            var low = Bars.BarGrowth[0];
            var high = Bars.BarGrowth[1];
            var stat = LowerFirst(Bars.SkillDefs[skill].Stat);
            return new RewardText(
                $"+{low}–{high} % · +1 punto",
                $"Al llenarse: entre un {low} y un {high} % más de {stat}, y un punto de habilidad para el tablero.");
        }

        /// <summary>At the end of the veterancy bar: what the next rank gives (its name by the soldier's sex, "f" or "m"), or null at the top.</summary>
        public static RewardText? RankReward(int rank, string? sex = null)
        {
            var next = rank + 1 >= 0 && rank + 1 < Bars.Veterancy.Count ? Bars.Veterancy[rank + 1] : null;
            if (next == null) return null;
            var name = sex == "f" ? next.Female : next.Name;
            var third = rank + 1 == Content.ThirdActionRank;
            return new RewardText(
                third ? $"+{Bars.WillPerRank} de voluntad · +1 acción" : $"+{Bars.WillPerRank} de voluntad",
                $"{name}, con {Bars.RankStep} puntos de habilidad más gastados en el tablero: +{Bars.WillPerRank} de voluntad{(third ? ", y una tercera acción por turno" : "")}.");
        }
    }
}
