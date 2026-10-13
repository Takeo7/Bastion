import { execFileSync } from 'node:child_process';
import { X509Certificate } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// The game is served over https as well. Safari runs the JavaScript of plain
// http pages on a network address without its compiler: the game becomes some
// fifty times slower and seems to hang. Over https it runs at full speed. The
// certificate is self-signed, made with openssl the first time (and again when
// this machine's addresses change) and kept next to the save, so the partner's
// browser warns about it once.

export interface Tls {
  key: Buffer;
  cert: Buffer;
}

/** A certificate for localhost and `addresses`, kept in `dir`; null if openssl is not available. */
export function certificate(dir: string, addresses: string[]): Tls | null {
  const keyPath = join(dir, 'bastion-key.pem');
  const certPath = join(dir, 'bastion-cert.pem');
  const ips = ['127.0.0.1', ...addresses];
  if (existsSync(keyPath) && existsSync(certPath)) {
    try {
      const cert = readFileSync(certPath);
      const x509 = new X509Certificate(cert);
      const covers = ips.every((ip) => x509.checkIP(ip) !== undefined);
      const lasts = Date.parse(x509.validTo) - Date.now() > 7 * 24 * 3600 * 1000;
      if (covers && lasts) return { key: readFileSync(keyPath), cert };
    } catch {
      /* a damaged certificate: made again below */
    }
  }
  mkdirSync(dir, { recursive: true });
  // Apple only accepts certificates that last at most 825 days and name their use.
  const extensions = [
    `subjectAltName=${['DNS:localhost', ...ips.map((ip) => `IP:${ip}`)].join(',')}`,
    'extendedKeyUsage=serverAuth',
    'keyUsage=digitalSignature,keyEncipherment',
    'basicConstraints=critical,CA:FALSE',
  ];
  try {
    execFileSync(
      'openssl',
      ['req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', keyPath, '-out', certPath, '-days', '825', '-subj', '/CN=Bastion (partida local)', ...extensions.flatMap((e) => ['-addext', e])],
      { stdio: 'ignore' },
    );
    return { key: readFileSync(keyPath), cert: readFileSync(certPath) };
  } catch {
    // No openssl (Windows, as it comes), or it failed: the game goes on over http only.
    return null;
  }
}
