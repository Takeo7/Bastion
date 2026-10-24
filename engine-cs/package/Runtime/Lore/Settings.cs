namespace Bastion.Engine
{
    public static partial class Lore
    {
        // --------------------------------------------------------------- settings

        /// <summary>Ajustes and Controles (playtest 18/10 #3, #6, #7): labels, their help, and the plan's log lines.</summary>
        public static class SettingsText
        {
            /// <summary>#3: a soldier's route planned without the command, walked when it comes.</summary>
            public const string Plans = "Seguir las rutas planeadas";
            public const string PlansAbout = "Sin el mando, puedes marcar el camino de un soldado. Cuando te llega el mando, lo recorre solo.";
            /// <summary>#6: the slider.</summary>
            public const string EnemySpeed = "Velocidad del turno enemigo";
            public const string EnemySpeedAbout = "A qué velocidad se ve el turno enemigo: movimientos, apariciones y detecciones.";
            /// <summary>#7: the ping's line in Controles (an infinitive, like the rest); its keys go beside it.</summary>
            public const string PingControls = "Señalar un sitio unos segundos, para que lo veáis los dos";
            /// <summary>Playtest 19/10 #15: the camera goes by itself to whoever's turn it is (18 letters at most).</summary>
            public const string CameraFollow = "Cámara al soldado";
            public const string CameraFollowAbout = "Al cambiar el turno o el mando, la cámara va sola al soldado al que le toca, salvo si acabas de moverla tú.";
        }

        /// <summary>#3: the soldier walks the planned route: «Mateo sigue la ruta planeada.»</summary>
        public static string PlanFollowedLine(string who)
        {
            return $"{who} sigue la ruta planeada.";
        }

        /// <summary>#3: the route can no longer be walked; it stays marked: «Mateo ya no llega: la ruta sigue marcada.»</summary>
        public static string PlanBlockedLine(string who)
        {
            return $"{who} ya no llega: la ruta sigue marcada.";
        }
    }
}
