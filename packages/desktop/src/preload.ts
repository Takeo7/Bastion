// The start screen's only way out of its page: host a game or join one
// (main.ts). Game pages from the network get nothing: they are plain web pages.
import { contextBridge, ipcRenderer } from 'electron';

if (location.protocol === 'file:') {
  contextBridge.exposeInMainWorld('bastion', {
    info: (): Promise<{ last: string; version: string; uncapped: boolean } | null> => ipcRenderer.invoke('bastion:info'),
    /** Resolves with what went wrong, or null once the game is loading. */
    host: (): Promise<string | null> => ipcRenderer.invoke('bastion:host'),
    join: (address: string): Promise<string | null> => ipcRenderer.invoke('bastion:join', address),
  });
}
