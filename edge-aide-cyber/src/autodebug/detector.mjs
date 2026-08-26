import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import path from 'node:path';

const execFileAsync = promisify(execFile);

const CHECKERS = {
  '.js':  { cmd: 'node', args: ['--check'] },
  '.mjs': { cmd: 'node', args: ['--check'] },
  '.ts':  { cmd: 'npx', args: ['tsc', '--noEmit'] },
  '.json': { cmd: 'node', args: ['-e', 'JSON.parse(require("fs").readFileSync(process.argv[1],"utf8"))'] },
};

function parseNodeCheckError(output, filename) {
  const errors = [];
  const lines = output.split('\n').filter(Boolean);
  for (const line of lines) {
    const match = line.match(/^(.*?):(\d+):(\d+)\s*[-—]\s*(.*)/);
    if (match) {
      errors.push({
        file: match[1] === filename ? filename : match[1],
        line: parseInt(match[2], 10),
        column: parseInt(match[3], 10),
        message: match[4].trim(),
        severity: 'error',
      });
    }
  }
  if (errors.length === 0 && output.includes('SyntaxError')) {
    const synMatch = output.match(/SyntaxError:\s*(.*)/);
    errors.push({
      file: filename,
      line: 0,
      column: 0,
      message: synMatch ? synMatch[1] : 'Syntax error',
      severity: 'error',
    });
  }
  return errors;
}

export async function checkFile(workspacePath, relativePath) {
  const ext = path.extname(relativePath).toLowerCase();
  const checker = CHECKERS[ext];

  if (!checker) {
    return { file: relativePath, checked: false, errors: [], reason: 'no checker for ' + ext };
  }

  const fullPath = path.join(workspacePath, relativePath);
  const args = [...checker.args, fullPath];

  const result = await execFileAsync(checker.cmd, args, {
    timeout: 10000,
    cwd: workspacePath,
    encoding: 'utf8',
    maxBuffer: 1024 * 1024,
  }).catch(err => ({
    error: true,
    stderr: err.stderr || '',
    stdout: err.stdout || '',
    code: err.code,
  }));

  const combined = `${result.stdout || ''}${result.stderr || ''}`;
  const hasError = result.error ||
    combined.includes('SyntaxError') ||
    combined.includes('Unexpected') ||
    combined.includes('Missing') ||
    combined.includes('Unterminated') ||
    combined.includes('Expected');

  if (!hasError) {
    return { file: relativePath, checked: true, errors: [], clean: true };
  }

  const errors = parseNodeCheckError(combined, relativePath);
  return { file: relativePath, checked: true, errors, clean: false };
}
