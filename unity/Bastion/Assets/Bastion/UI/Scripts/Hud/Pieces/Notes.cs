using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The small notes over the bar (hud.ts toast, notice, packOffer; docs/UNITY-UI.md §2.12): a short one that goes by
    /// itself (what the server refused, «recarga»…), a key moment of the partner's with «Ver (F)», in their colour, and a
    /// find left on the floor with the backpack full, with «Cambiar» for a while.
    /// </summary>
    public sealed class Notes
    {
        const long ToastMs = 2600, OfferMs = 7000;

        readonly VisualElement host;
        readonly Label toast;
        readonly VisualElement notice;
        readonly VisualElement offer;
        IVisualElementScheduledItem toastTimer, offerTimer;

        public Notes(VisualElement host)
        {
            this.host = host;
            offer = Ui.El("note note--offer", host);
            Ui.Show(offer, false);
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

        /// <summary>The short note gone now, if it still says `text` (a question answered: «Pulsa otra vez para terminar…»).</summary>
        public void ClearToast(string text)
        {
            if (toast.text != text) return;
            toastTimer?.Pause();
            Ui.Show(toast, false);
        }

        /// <summary>A line with one button (`act`, calling `onAct`), for OfferMs: «Mochila llena: 2 medicinas, al suelo.» «Cambiar».</summary>
        public void Offer(string text, string act, Action onAct)
        {
            offerTimer?.Pause();
            offer.Clear();
            Ui.Text(text, null, offer);
            offer.Add(new Button(() =>
            {
                Ui.Show(offer, false);
                onAct?.Invoke();
            }) { text = act });
            Ui.Show(offer, true);
            offerTimer = offer.schedule.Execute(() => Ui.Show(offer, false)).StartingIn(OfferMs);
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
