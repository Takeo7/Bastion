namespace Bastion.UI.Gear
{
    /// <summary>The grid's words (runs, 16/11; Lore to confirm). The engine's reasons come as they are.</summary>
    public static class GearText
    {
        public const string Stash = "ALMACÉN";
        public const string Pack = "MOCHILA";
        public const string Belt = "CINTURÓN";

        /// <summary>A kit's place, empty: its name in the box.</summary>
        public static string SlotLabel(string slot)
        {
            switch (slot)
            {
                case "primary": return "PRINCIPAL";
                case "melee": return "CUERPO A CUERPO";
                case "pistol": return "PISTOLA";
                case "vest": return "CHALECO";
                // The belt's two have their name over both.
                case "belt0": case "belt1": return "";
                default: return slot?.ToUpperInvariant() ?? "";
            }
        }

        public const string Turn = "Girar";
        public const string Split = "Partir";
        public const string Throw = "Tirar";
        /// <summary>Not to (Partir, Tirar): «Cancelar», not «No» (Lore, 16/11).</summary>
        public const string No = "Cancelar";

        /// <summary>The bin, while nothing is dragged over it.</summary>
        public const string Bin = "TIRAR";
        /// <summary>The bin with a thing over it.</summary>
        public const string BinOver = "Suelta para tirarlo";
        /// <summary>Before throwing a thing away.</summary>
        public static string ThrowAsk(string the, int count = 1) => $"¿Tirar {the}? {(count > 1 ? "Se pierden." : "Se pierde.")}";

        /// <summary>How many go in the new pile.</summary>
        public static string SplitCount(int n, int of) => $"{n} de {of}";
        public const string NoRoomToSplit = "No hay sitio para partirlo";

        /// <summary>Under the stash: how things are handled (Mayús + clic, M3: Lore, 23/11; held while dragging, half the pile).</summary>
        public const string Hint = "Arrastra para mover. Mientras arrastras, R o clic derecho gira. Arrastrar con Mayús mueve la mitad de la pila. Mayús + clic lo pasa entero al otro lado.";
        /// <summary>Mayús + clic with nowhere shown to send it to.</summary>
        public const string NowhereToSend = "No hay adónde pasarlo";
    }
}
