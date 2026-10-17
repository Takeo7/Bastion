// The page's first step (index.html). On a website with a password (server
// access.ts, docs/WEB.md), the password before anything else: the game's
// modules start fetching models as soon as they load, and without it every one
// of those is refused, so they are not even loaded. Then the game (main.ts).
// Nothing here may import the game or its styles: its stylesheets keep their
// order only when the game brings them all itself.

/** Whether this browser may have the game. Anything but a plain no (the app, a server without a password, no network) goes on. */
async function mayEnter(): Promise<boolean> {
  try {
    return (await fetch('/__access', { cache: 'no-store' })).status !== 401;
  } catch {
    return true;
  }
}

if (await mayEnter()) await import('./main');
else (await import('./ui/password')).showPasswordScreen(document.getElementById('app')!);

// A module (for the await above), with nothing to give.
export {};
