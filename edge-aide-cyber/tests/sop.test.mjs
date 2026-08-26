import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { SOPCompiler } from '../src/sop/compiler.mjs';
import { CoverageLedger } from '../src/sop/coverage.mjs';

/* ── SOP Compiler Tests ── */

describe('sop compiler', () => {
  // Mock tool registry
  const mockRegistry = {
    has: (name) => ['dns.reverse', 'http.headers', 'nmap.scan'].includes(name),
    get: (name) => ({ name }),
  };

  const validSOP = {
    id: 'test-sop',
    name: 'Test SOP',
    steps: [
      { id: 'step1', name: 'Step One', tool: 'dns.reverse', riskLevel: 'R1', dependsOn: [] },
      { id: 'step2', name: 'Step Two', tool: 'http.headers', riskLevel: 'R1', dependsOn: ['step1'] },
      { id: 'step3', name: 'Step Three', tool: 'nmap.scan', riskLevel: 'R2', dependsOn: ['step1'], approvalRequired: true },
    ],
  };

  it('validates a correct SOP', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const errors = compiler.validate(validSOP);
    assert.equal(errors.length, 0, `unexpected errors: ${errors.join(', ')}`);
  });

  it('loads and retrieves an SOP', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const result = compiler.load(validSOP);
    assert.equal(result.ok, true);
    assert.equal(compiler.get('test-sop').name, 'Test SOP');
    assert.equal(compiler.has('test-sop'), true);
  });

  it('rejects SOP without id', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const errors = compiler.validate({ name: 'no id', steps: [{ id: 's1', name: 's', dependsOn: [] }] });
    assert.ok(errors.some(e => e.includes('"id"')));
  });

  it('rejects SOP without name', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const errors = compiler.validate({ id: 'x', steps: [{ id: 's1', name: 's', dependsOn: [] }] });
    assert.ok(errors.some(e => e.includes('"name"')));
  });

  it('rejects empty steps array', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const errors = compiler.validate({ id: 'x', name: 'y', steps: [] });
    assert.ok(errors.some(e => e.includes('at least one step')));
  });

  it('rejects duplicate step IDs', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const sop = {
      id: 'dup',
      name: 'Dup',
      steps: [
        { id: 'same', name: 'A', dependsOn: [] },
        { id: 'same', name: 'B', dependsOn: [] },
      ],
    };
    const errors = compiler.validate(sop);
    assert.ok(errors.some(e => e.includes('duplicate step id')));
  });

  it('rejects missing dependsOn reference', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const sop = {
      id: 'bad-dep',
      name: 'Bad Dep',
      steps: [
        { id: 'a', name: 'A', dependsOn: ['nonexistent'] },
      ],
    };
    const errors = compiler.validate(sop);
    assert.ok(errors.some(e => e.includes('not found')));
  });

  it('rejects cycle in dependencies', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const sop = {
      id: 'cyclic',
      name: 'Cyclic',
      steps: [
        { id: 'a', name: 'A', dependsOn: ['b'] },
        { id: 'b', name: 'B', dependsOn: ['a'] },
      ],
    };
    const errors = compiler.validate(sop);
    assert.ok(errors.some(e => e.includes('cycle')));
  });

  it('rejects unknown tool reference', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const sop = {
      id: 'bad-tool',
      name: 'Bad Tool',
      steps: [
        { id: 'a', name: 'A', tool: 'nonexistent.tool', dependsOn: [] },
      ],
    };
    const errors = compiler.validate(sop);
    assert.ok(errors.some(e => e.includes('not in adapter registry')));
  });

  it('rejects invalid riskLevel', () => {
    const compiler = new SOPCompiler(mockRegistry);
    const sop = {
      id: 'bad-risk',
      name: 'Bad Risk',
      steps: [
        { id: 'a', name: 'A', riskLevel: 'R9', dependsOn: [] },
      ],
    };
    const errors = compiler.validate(sop);
    assert.ok(errors.some(e => e.includes('riskLevel')));
  });

  it('topological sort produces correct order', () => {
    const compiler = new SOPCompiler(mockRegistry);
    compiler.load(validSOP);
    const order = compiler.topoSort('test-sop');
    // step1 must come before step2 and step3
    assert.ok(order.indexOf('step1') < order.indexOf('step2'));
    assert.ok(order.indexOf('step1') < order.indexOf('step3'));
    assert.equal(order.length, 3);
  });

  it('lists loaded SOPs', () => {
    const compiler = new SOPCompiler(mockRegistry);
    compiler.load(validSOP);
    const list = compiler.list();
    assert.equal(list.length, 1);
    assert.equal(list[0].id, 'test-sop');
  });

  it('returns null for unknown SOP', () => {
    const compiler = new SOPCompiler(mockRegistry);
    assert.equal(compiler.get('nonexistent'), null);
  });

  it('handles SOP with no tool registry', () => {
    const compiler = new SOPCompiler(null);
    const sop = {
      id: 'no-registry',
      name: 'No Registry',
      steps: [{ id: 'a', name: 'A', tool: 'anything', dependsOn: [] }],
    };
    const errors = compiler.validate(sop);
    // Should not error on tool check since registry is null
    assert.equal(errors.length, 0);
  });
});

/* ── Coverage Ledger Tests ── */

describe('coverage ledger', () => {
  const mockCipher = {
    append: async () => {},
  };

  const simpleSOP = {
    id: 'simple',
    name: 'Simple',
    steps: [
      { id: 's1', name: 'Step 1' },
      { id: 's2', name: 'Step 2' },
      { id: 's3', name: 'Step 3' },
    ],
  };

  it('starts tracking an SOP', () => {
    const ledger = new CoverageLedger(mockCipher);
    const ref = ledger.startSOP(simpleSOP);
    assert.equal(ref.sop.id, 'simple');
    assert.equal(ref.currentStepIndex, 0);
    assert.ok(ref.startedAt);
  });

  it('records step status', () => {
    const ledger = new CoverageLedger(mockCipher);
    ledger.startSOP(simpleSOP);
    ledger.recordStep('simple', 's1', 'completed');
    ledger.recordStep('simple', 's2', 'failed');
    ledger.recordStep('simple', 's3', 'skipped');

    const status = ledger.getStatus('simple');
    assert.equal(status.completed, 2); // completed + skipped
    assert.equal(status.total, 3);
    assert.equal(status.percentage, 67);
    assert.equal(status.stepStatus.s1, 'completed');
    assert.equal(status.stepStatus.s2, 'failed');
    assert.equal(status.stepStatus.s3, 'skipped');
  });

  it('returns empty status for unknown SOP', () => {
    const ledger = new CoverageLedger(mockCipher);
    const status = ledger.getStatus('nonexistent');
    assert.equal(status.total, 0);
    assert.equal(status.percentage, 0);
  });

  it('isComplete returns false when steps are pending', () => {
    const ledger = new CoverageLedger(mockCipher);
    ledger.startSOP(simpleSOP);
    ledger.recordStep('simple', 's1', 'completed');
    assert.equal(ledger.isComplete('simple'), false);
  });

  it('isComplete returns true when all steps done', () => {
    const ledger = new CoverageLedger(mockCipher);
    ledger.startSOP(simpleSOP);
    ledger.recordStep('simple', 's1', 'completed');
    ledger.recordStep('simple', 's2', 'completed');
    ledger.recordStep('simple', 's3', 'completed');
    assert.equal(ledger.isComplete('simple'), true);
  });

  it('exports markdown report', () => {
    const ledger = new CoverageLedger(mockCipher);
    ledger.startSOP(simpleSOP);
    ledger.recordStep('simple', 's1', 'completed');
    ledger.recordStep('simple', 's2', 'failed');
    const md = ledger.export('simple', 'markdown');
    assert.ok(md.includes('SOP Coverage Report'));
    assert.ok(md.includes('simple'));
    assert.ok(md.includes('✅'));
    assert.ok(md.includes('❌'));
  });

  it('exports JSON report', () => {
    const ledger = new CoverageLedger(mockCipher);
    ledger.startSOP(simpleSOP);
    ledger.recordStep('simple', 's1', 'completed');
    const json = JSON.parse(ledger.export('simple', 'json'));
    assert.equal(json.sopId, 'simple');
    assert.ok(json.startedAt);
    assert.equal(json.steps.length, 3); // all steps pre-registered as pending
  });

  it('completeSOP marks completion time', () => {
    const ledger = new CoverageLedger(mockCipher);
    ledger.startSOP(simpleSOP);
    ledger.completeSOP('simple');
    const json = JSON.parse(ledger.export('simple', 'json'));
    assert.ok(json.completedAt);
  });

  it('resumes existing SOP ledger', () => {
    const ledger = new CoverageLedger(mockCipher);
    const ref1 = ledger.startSOP(simpleSOP);
    ledger.recordStep('simple', 's1', 'completed');
    const ref2 = ledger.startSOP(simpleSOP);
    assert.equal(ref2.startedAt, ref1.startedAt);
    const status = ledger.getStatus('simple');
    assert.equal(status.completed, 1);
  });
});
