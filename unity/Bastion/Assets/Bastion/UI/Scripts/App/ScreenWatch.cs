using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bastion.UI.App
{
    /// <summary>
    /// N1's safety net, the Mac's (22/11): with the screen's sync on, once the screen sleeps its presentation never comes,
    /// and the frame waits for it for ever, also after it wakes (1/11, 0.11.3: the battle hung with Arturo's Mac locked). So
    /// the sync goes only while the screen is surely awake: the session not locked (locked, the display goes off soon after),
    /// the display not asleep, and not about to sleep of idleness: the display's own sleep as this Mac has it (pmset's
    /// «displaysleep»; «never», never) less BeforeSleep, read every minute. Seeing it asleep isn't enough: the frame after
    /// waits for a presentation that never comes, and nothing runs again to take the sync off (1/11). Otherwise Dozing,
    /// and the frames go as without the sync (GameSettings.Pacing: the screen's refresh as the cap). Looked at four times a
    /// second; a change paces the frames again (GameSettings.Pace). In the Mac app only; elsewhere never Dozing. If the
    /// Mac's calls aren't there, Dozing for good: no sync rather than a hang. (Producto, 22/11: no fixed time, which took
    /// the sync off while one watched the enemy's turn without touching anything.)
    /// </summary>
    public sealed class ScreenWatch : MonoBehaviour
    {
        /// <summary>The screen may go to sleep: no sync. In the Mac app, so from the start till the first look (Update).</summary>
        public static bool Dozing { get; private set; } = !Application.isEditor && Application.platform == RuntimePlatform.OSXPlayer;

        /// <summary>How long before the display's own sleep of idleness the sync goes (s).</summary>
        const double BeforeSleep = 5;

        /// <summary>The display's sleep not readable: idle this long (s) is near enough (macOS's shortest is a minute).</summary>
        const double Unread = 50;

        /// <summary>The display's own sleep of idleness, in minutes, as pmset says (the power source's in use): 0 never; -1 not read yet; -2 not readable.</summary>
        static volatile int displaySleep = -1;
        static volatile bool reading;
        int saidSleep = -1;
        float nextRead;

        /// <summary>Idle this long (s), the display may be about to sleep: its own sleep less BeforeSleep; never, never.</summary>
        static double IdleLimit
        {
            get
            {
                var minutes = displaySleep;
                return minutes == 0 ? double.MaxValue : minutes > 0 ? Math.Max(Unread, minutes * 60 - BeforeSleep) : Unread;
            }
        }

        /// <summary>pmset's «displaysleep», off the main thread (a process to run).</summary>
        static void ReadSleep()
        {
            try
            {
                using var p = new Process { StartInfo = new ProcessStartInfo("/usr/bin/pmset", "-g") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true } };
                p.Start();
                var text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);
                var m = Regex.Match(text, @"^\s*displaysleep\s+(\d+)", RegexOptions.Multiline);
                displaySleep = m.Success ? int.Parse(m.Groups[1].Value) : -2;
            }
            catch (Exception)
            {
                displaySleep = -2;
            }
            finally
            {
                reading = false;
            }
        }

        const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(CoreGraphics)] static extern uint CGMainDisplayID();
        [DllImport(CoreGraphics)] [return: MarshalAs(UnmanagedType.I1)] static extern bool CGDisplayIsAsleep(uint display);
        /// <summary>kCGEventSourceStateCombinedSessionState (0), kCGAnyInputEventType (~0).</summary>
        [DllImport(CoreGraphics)] static extern double CGEventSourceSecondsSinceLastEventType(int state, uint type);
        [DllImport(CoreGraphics)] static extern IntPtr CGSessionCopyCurrentDictionary();
        [DllImport(CoreFoundation)] static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string text, uint encoding);
        [DllImport(CoreFoundation)] static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);
        [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.I1)] static extern bool CFBooleanGetValue(IntPtr boolean);
        [DllImport(CoreFoundation)] static extern void CFRelease(IntPtr thing);

        static IntPtr lockedKey;
        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.OSXPlayer) return;
            var go = new GameObject("Vigía de la pantalla") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenWatch>();
        }

        /// <summary>For a test (BASTION_PANTALLA_DESPIERTA=1): the screen taken as awake and in use, whatever it is.</summary>
        static readonly bool AwakeForTest = Environment.GetEnvironmentVariable("BASTION_PANTALLA_DESPIERTA") == "1";

        bool looked;

        void Update()
        {
            // The display's sleep as this Mac has it, at the start and every minute (on battery or the mains it may change).
            if (!AwakeForTest && !reading && Time.unscaledTime >= nextRead)
            {
                nextRead = Time.unscaledTime + 60;
                reading = true;
                Task.Run(ReadSleep);
            }
            var sleep = displaySleep;
            if (sleep != saidSleep && sleep != -1)
            {
                saidSleep = sleep;
                Debug.Log($"[pantalla] {(sleep == 0 ? "no se duerme sola: la sincronía no se va por estar sin tocar" : sleep > 0 ? $"se duerme a los {sleep} min sin tocar: la sincronía se va {BeforeSleep} s antes" : $"no se sabe cuándo se duerme: la sincronía se va a los {Unread} s sin tocar")}");
            }
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            var dozing = !AwakeForTest && Look();
            // The first look said in the log, changed or not: that the watch is there.
            if (!looked) Debug.Log($"[pantalla] vigía puesto: {(dozing ? "puede dormirse, sin sincronía" : "despierta")}{(AwakeForTest ? " (prueba)" : "")}");
            looked = true;
            if (dozing == Dozing) return;
            Dozing = dozing;
            Debug.Log($"[pantalla] {(dozing ? "puede dormirse (bloqueada, dormida o sin usar): sin sincronía" : "despierta: con sincronía si está puesta")}");
            GameSettings.Pace();
        }

        /// <summary>Whether the screen may sleep now; any call missing, true from then on (no sync rather than a hang).</summary>
        static bool failed;

        static bool Look()
        {
            if (failed) return true;
            try
            {
                if (CGDisplayIsAsleep(CGMainDisplayID())) return true;
                if (CGEventSourceSecondsSinceLastEventType(0, uint.MaxValue) > IdleLimit) return true;
                return Locked();
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning($"[pantalla] sin las llamadas del Mac ({e.GetType().Name}): sin sincronía");
                return true;
            }
        }

        /// <summary>The session's «CGSSessionScreenIsLocked», there and true only while locked.</summary>
        static bool Locked()
        {
            if (lockedKey == IntPtr.Zero) lockedKey = CFStringCreateWithCString(IntPtr.Zero, "CGSSessionScreenIsLocked", 0x08000100);
            var session = CGSessionCopyCurrentDictionary();
            if (session == IntPtr.Zero) return false;
            try
            {
                var value = CFDictionaryGetValue(session, lockedKey);
                return value != IntPtr.Zero && CFBooleanGetValue(value);
            }
            finally
            {
                CFRelease(session);
            }
        }
    }
}
