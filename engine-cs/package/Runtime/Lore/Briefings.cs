using System.Text.RegularExpressions;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------------- briefings

        /// <summary>
        /// The marks in a mission's presentation (Ana, 22/10 #28): **key words** in
        /// white bold, ==the objective== in yellow (UI renders them). Where a text is
        /// shown plain, without them.
        /// </summary>
        public static string PlainMarks(string text)
        {
            return Regex.Replace(text, "\\*\\*|==", "");
        }
    }
}
