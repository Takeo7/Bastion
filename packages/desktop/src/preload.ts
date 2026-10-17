// The app's pages and what each may ask of it (main.ts checks every request again):
// - the start screen (a file of the app): host a game or join one;
// - the game's page (the host's server, here or on the partner's machine): only a
//   way out (quit, back to the start screen) and whether it is the host's window,
//   for «Salir del juego» (playtest 17/10). Nothing else from the network.
import { contextBridge, ipcRenderer } from 'electron';

if (location.protocol === 'file:') {
  contextBridge.exposeInMainWorld('bastion', {
    info: (): Promise<{ last: string; version: string; uncapped: boolean; games: unknown[] } | null> => ipcRenderer.invoke('bastion:info'),
    /** Resolves with what went wrong, or null once the game is loading. */
    host: (): Promise<string | null> => ipcRenderer.invoke('bastion:host'),
    join: (address: string): Promise<string | null> => ipcRenderer.invoke('bastion:join', address),
    /** The games heard on the local network, each time that list changes. */
    onGames: (fn: (games: { name: string; address: string; players: string[]; phase: string }[]) => void): void => {
      ipcRenderer.on('bastion:games', (_e, games) => fn(games));
    },
  });
} else if (location.protocol === 'http:' || location.protocol === 'https:') {
  contextBridge.exposeInMainWorld('bastion', {
    /** Closes the app; as host, its server with it (the game is saved after every change). The page asks first. */
    quit: (): void => ipcRenderer.send('bastion:quit'),
    /** Back to the start screen (Ser anfitrión / Unirse), with `note` saying why if there is one. */
    toLauncher: (note?: string): void => ipcRenderer.send('bastion:toLauncher', note),
    /** This window is the host's: the game's server runs in this app, and quitting ends it for the partner. */
    hosting: ipcRenderer.sendSync('bastion:hosting') === true,
  });
}
