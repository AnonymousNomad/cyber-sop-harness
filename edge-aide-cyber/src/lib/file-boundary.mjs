import path from 'node:path';
import fs from 'node:fs/promises';

export class PathEscapeError extends Error {
  constructor(attemptedPath, root) {
    super(`path escapes workspace boundary: ${attemptedPath} (root: ${root})`);
    this.name = 'PathEscapeError';
    this.code = 'PATH_ESCAPE';
  }
}

export function createFileBoundary(rootDir) {
  const resolvedRoot = path.resolve(rootDir);

  function safeResolve(...segments) {
    const joined = path.resolve(resolvedRoot, ...segments);
    if (!joined.startsWith(resolvedRoot + path.sep) && joined !== resolvedRoot) {
      throw new PathEscapeError(joined, resolvedRoot);
    }
    return joined;
  }

  async function safeRealPath(...segments) {
    const resolved = safeResolve(...segments);
    try {
      const real = await fs.realpath(resolved);
      if (!real.startsWith(resolvedRoot + path.sep) && real !== resolvedRoot) {
        throw new PathEscapeError(real, resolvedRoot);
      }
      return real;
    } catch (err) {
      if (err.code === 'ENOENT') return resolved;
      throw err;
    }
  }

  async function readFile(...segments) {
    const filePath = await safeRealPath(...segments);
    return fs.readFile(filePath, 'utf8');
  }

  async function writeFile(filePath, content) {
    const resolved = safeResolve(filePath);
    await fs.mkdir(path.dirname(resolved), { recursive: true });
    return fs.writeFile(resolved, content, 'utf8');
  }

  async function appendFile(filePath, content) {
    const resolved = safeResolve(filePath);
    await fs.mkdir(path.dirname(resolved), { recursive: true });
    return fs.appendFile(resolved, content, 'utf8');
  }

  async function listDir(...segments) {
    const dirPath = await safeRealPath(...segments);
    return fs.readdir(dirPath);
  }

  async function stat(...segments) {
    const targetPath = await safeRealPath(...segments);
    return fs.stat(targetPath);
  }

  async function mkdir(...segments) {
    const dirPath = safeResolve(...segments);
    return fs.mkdir(dirPath, { recursive: true });
  }

  async function exists(...segments) {
    try {
      const resolved = safeResolve(...segments);
      const real = await fs.realpath(resolved).catch(() => resolved);
      if (!real.startsWith(resolvedRoot + path.sep) && real !== resolvedRoot) {
        return false;
      }
      await fs.stat(real);
      return true;
    } catch {
      return false;
    }
  }

  return Object.freeze({
    readFile,
    writeFile,
    appendFile,
    listDir,
    stat,
    mkdir,
    exists,
    resolve: safeResolve,
    get root() { return resolvedRoot; },
  });
}
