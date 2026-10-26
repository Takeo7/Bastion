// A hidden Electron page with Node, running one of render.ts's tasks (TAREA: {fn, args}) and printing its answer
// after «RESULTADO » (generar.mjs reads it). Quits when done.
const { app, BrowserWindow } = require('electron');
const path = require('path');

app.whenReady().then(async () => {
  const win = new BrowserWindow({ show: false, webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false } });
  // The page's errors and warnings, to the terminal: an exception in render.ts says where.
  win.webContents.on('console-message', (e) => {
    if (e.level === 'error' || e.level === 'warning') console.error(`página: ${e.message}${e.sourceId ? ` (${path.basename(e.sourceId)}:${e.lineNumber})` : ''}`);
  });
  await win.loadFile(path.join(__dirname, 'index.html'));
  const task = JSON.parse(process.env.TAREA || '{}');
  try {
    const out = await win.webContents.executeJavaScript(`${task.fn}(...${JSON.stringify(task.args || [])})`);
    console.log(`RESULTADO ${JSON.stringify(out)}`);
  } catch (e) {
    console.error(`fallo: ${e && e.message ? e.message : e}`);
    process.exitCode = 1;
  }
  app.quit();
});
