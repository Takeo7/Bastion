// The start screen's only way out of its page: host a game or join one
// (main.ts). Game pages from the network get nothing: they are plain web pages.
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
}
