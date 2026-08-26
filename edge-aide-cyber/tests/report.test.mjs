import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ReportGenerator } from "../src/report/generator.mjs";

describe("report generator", () => {
  const mockEvidence = {
    count: 5,
    recent: () => [
      { type: "action.executed", hash: "abc123", timestamp: "2024-01-01T00:00:00Z" },
    ],
  };

  const mockLedger = {
    getStatus: () => ({ completed: 3, total: 5, percentage: 60 }),
  };

  const engagement = {
    target: "example.com",
    scope: ["example.com"],
    operatorId: "tester",
  };

  const findings = [
    { title: "XSS in search", severity: "high", target: "example.com/search", description: "Reflected XSS" },
    { title: "Info disclosure", severity: "low", target: "example.com/headers", description: "Server version exposed" },
  ];

  it("generates markdown report", () => {
    const gen = new ReportGenerator({ evidenceChain: mockEvidence, coverageLedger: mockLedger });
    const md = gen.generateMarkdown(engagement, findings, "recon-basic");
    assert.ok(md.includes("Security Assessment Report"));
    assert.ok(md.includes("example.com"));
    assert.ok(md.includes("XSS in search"));
    assert.ok(md.includes("HIGH"));
    assert.ok(md.includes("60%"));
    assert.ok(md.includes("Total findings: 2"));
  });

  it("generates JSON report", () => {
    const gen = new ReportGenerator({ evidenceChain: mockEvidence, coverageLedger: mockLedger });
    const json = JSON.parse(gen.generateJSON(engagement, findings, "recon-basic"));
    assert.equal(json.report.engagement.target, "example.com");
    assert.equal(json.report.findings.length, 2);
    assert.ok(json.report.generatedAt);
  });

  it("handles missing evidence chain", () => {
    const gen = new ReportGenerator({ evidenceChain: null, coverageLedger: null });
    const md = gen.generateMarkdown(engagement, [], null);
    assert.ok(md.includes("Security Assessment Report"));
    assert.ok(md.includes("Total findings: 0"));
  });
});