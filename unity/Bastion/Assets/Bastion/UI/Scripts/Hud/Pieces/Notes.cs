using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The small notes over the bar (hud.ts toast, notice; docs/UNITY-UI.md §2.12): a short one that goes by itself (what
    /// the server refused, «recarga»…), and a key moment of the partner's with «Ver (F)», in their colour.
    /// </summary>
    public sealed class Notes
    {
        const long ToastMs = 2600;

        readonly VisualElement host;
        readonly Label toast;
        readonly VisualElement notice;
        IVisualElementScheduledItem toastTimer;

        public Notes(VisualElement host)
        {
            this.host = host;
            notice = Ui.El("note note--partner", host);
            toast = Ui.Text("", "note", host);
            toast.pickingMode = PickingMode.Ignore;
            Ui.Show(notice, false);
            Ui.Show(toast, false);
        }

        public void Toast(string text)
        {
            toastTimer?.Pause();
            toast.text = text;
            Ui.Show(toast, true);
            toastTimer = toast.schedule.Execute(() => Ui.Show(toast, false)).StartingIn(ToastMs);
        }

        /// <summary>The partner's moment, with «Ver (F)» calling `onSee`; null text clears it.</summary>
        public void Notice(string text, string color = null, Action onSee = null)
        {
            notice.Clear();
            Ui.Show(notice, !string.IsNullOrEmpty(text));
            if (string.IsNullOrEmpty(text)) return;
            notice.style.borderLeftColor = Ui.Hex(color, Color.white);
            Ui.Text(text, null, notice);
            if (onSee != null) notice.Add(new Button(onSee) { text = HudText.Notes.See });
        }
    }
}
