// The desktop app, seen from the game's page (packages/desktop preload.ts):
// what it lets the page do, which is only to leave. «Salir de Bastión» and its
// question are UI's (ui/quit.ts), used by the lobby, the base's menu and the
// battle's; its words are Lore's (engine lore.ts QUIT_TEXT).

/** What the app gives the game's page; absent in a browser, where there is no button to quit. */
export interface DesktopBridge {
  /** Closes the app; as host, the game's server with it (everything is already saved). */
  quit(): void;
  /** Back to the app's start screen (Ser anfitrión / Unirse), with `note` there saying why (at most 200 characters). */
  toLauncher(note?: string): void;
  /** This window is the host's: quitting disconnects the partner until it is opened again. */
  hosting: boolean;
}

/** The app's bridge, or null in a browser. */
export function desktop(): DesktopBridge | null {
  const bridge = (window as { bastion?: Partial<DesktopBridge> }).bastion;
  return bridge && typeof bridge.quit === 'function' ? (bridge as DesktopBridge) : null;
}
