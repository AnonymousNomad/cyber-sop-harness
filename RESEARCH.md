# Research Record

Research date: 2026-08-17
Research status: complete for roadmap planning

## Research Question

Determine whether a portable harness that gives arbitrary models cybersecurity SOPs solves a real gap, what already exists, which claims are unsupported, and what architecture can safely support PC, Android, and iPhone operation.

## Verified Market Landscape

### Commercial platforms

XBOW publicly describes autonomous pentesting, attack-path chaining, working-exploit validation, independent validators, complete reproducible traces, scope controls, audit logs, and model routing.

Astra publicly describes autonomous pentesting, contextual business-logic testing, attack chains, a separate AI validator, MCP delivery, and participation in OWASP APTS.

Horizon3 NodeZero publicly describes autonomous internal, external, cloud, Kubernetes, and web testing, attack paths, proof, remediation, one-time-use test architecture, and cloud-hosted execution.

Pentera publicly describes AI-assisted exposure validation, deterministic attack logic, adaptive payloads, controlled execution, audit proof, prioritization, remediation, and revalidation.

Hadrian publicly describes agentic penetration testing, adversarial exposure validation, continuous external attack-surface management, and prioritized exploitable findings.

HackerOne Hai publicly describes agents for report completeness, deduplication, prioritization, insight, validation, and workflow automation. HackerOne separately markets agentic pentesting.

These are vendor product claims. They establish market positioning, not independent assurance.

### Open and research frameworks

Strix is an open-source AI pentesting tool with multi-agent orchestration, Docker execution, multiple model providers, local result storage, web/API testing, business-logic claims, and agent skills.

CAI is an open cybersecurity AI framework with many model providers, security tools, tracing, guardrails, and human-in-the-loop support. Its repository documents separate research and professional licensing considerations.

PentestGPT documents staged pipelines, session persistence, multiple modes, model backends, and a Pentesting Task Tree in its legacy mode. Its USENIX paper reports difficulty maintaining whole-scenario context.

HexStrike is an MCP server exposing a large security-tool surface and autonomous agents. Its README also exposes an arbitrary command endpoint and contains self-reported performance numbers that are not independently verified here.

The existence of these projects disproves a simple claim that model/tool-agnostic cyber agents do not exist.

### Existing security tools

Nmap, Nuclei, Burp Suite, OWASP ZAP, ffuf, httpx, subfinder, Amass, browser automation, cloud scanners, code scanners, and API testing tools already provide substantial execution capability.

The missing layer is not another scanner. It is a neutral control plane that governs heterogeneous tools, records why actions occurred, and proves what actually happened.

## Standards Findings

OWASP APTS v0.1.0 defines 173 tier-required requirements across scope enforcement, safety controls, human oversight, graduated autonomy, auditability, manipulation resistance, supply-chain trust, and reporting. APTS explicitly states that it is governance, not a testing methodology, and has no certification body.

OWASP WSTG v4.2 is the stable version currently documented by OWASP; version 5.0 is under development. WSTG requires versioned scenario references because identifiers can change.

OWASP ASVS 5.0.0 is the current stable release documented by OWASP. ASVS provides testable application-security requirements and versioned references.

NIST SP 800-115 provides planning, execution, analysis, and mitigation guidance for security testing. It was published in 2008 and explicitly is not a complete testing program or an agent-specific standard.

PTES remains useful for engagement phases but its public standard is old. ISSAF is historical and should not be treated as the current normative source.

CISA VDP guidance requires explicit scope, authorized methods, prohibited methods, limited exploitation, immediate stopping on sensitive-data discovery, and third-party authorization where applicable. It distinguishes vulnerability disclosure policies from bug bounties.

NIST AI RMF provides Govern, Map, Measure, and Manage risk-management structure. NIST SP 800-207 requires explicit authentication and authorization rather than implicit trust based on network location.

MITRE ATLAS and ATT&CK provide threat-informed knowledge bases and technique mappings. They do not provide authorization, rules of engagement, evidence requirements, or permission to test.

## Independent Evaluation Evidence

The USENIX PentestGPT paper reports that LLMs can perform individual penetration-testing subtasks but struggle to maintain whole-scenario context.

Cybench provides 40 professional-level CTF tasks and evaluates agents across subtasks, tools, and scaffolds.

CyberGym provides 1,507 real-world vulnerability instances across 188 projects. Its abstract reports that top combinations achieved approximately 20 percent success, demonstrating that cyber capability remains difficult.

BountyBench evaluates detection, exploitation, and patching on 25 systems with 40 bug-bounty-style vulnerabilities. Its results show materially different performance across detection, exploitation, and patching.

AgentDojo provides 97 realistic agent tasks and 629 security test cases for prompt injection and tool-use evaluation. It reports that both agents and defenses fail on meaningful portions of the test suite.

Research on CAI prompt injection reports that malicious target responses can hijack cybersecurity agents. This directly supports treating target content and tool output as untrusted data.

## Defensible Gaps

### Portable conformance runtime

The reviewed market contains proprietary safety and validation features and an open governance standard, but no reviewed public product claims to provide a neutral APTS conformance runtime that can enforce the same policy and evidence contract across third-party engines.

This is a scoped research observation, not proof that no such private system exists.

### Canonical evidence and replay

Several vendors advertise traces and independent validation. The opportunity is a portable evidence envelope, hash-linked event chain, replay package, redaction record, and independent verifier that works across models and tools.

### Action-level policy

Prompt rules and tool scopes are not equivalent to an external policy gate. The harness should issue one-use, expiring permits bound to action, target, scope, identity, policy version, and approval.

### Failure-mode catalog

Create explicit controls for unsupported findings, unobserved-action divergence, scope drift, no-novelty loops, prompt injection, excessive privilege, dependency substitution, partial tool output, and evidence-chain tampering.

### Business-logic context

Business-logic testing is marketed by current products, but a portable workflow model with roles, states, invariants, transitions, approval points, replay, concurrency, and explicit unknown coverage remains a strong product feature.

### Methodology operationalization

Convert WSTG, ASVS, PTES, NIST, CISA, and ATLAS references into typed procedures with prerequisites, capability requirements, risk class, evidence, completion oracle, cleanup, and escalation behavior.

## Product Decision

Build an APTS-aligned portable execution-governance layer, not a generic autonomous pentesting product.

The initial implementation target is authorized web/API assessment in local fixtures and owned systems. Network, cloud, identity, binary, and other profiles follow only after the core safety and evidence spine is verified.

## Source Links

- https://github.com/OWASP/APTS
- https://owasp.org/www-project-web-security-testing-guide/
- https://owasp.org/www-project-application-security-verification-standard/
- https://csrc.nist.gov/pubs/sp/800/115/final
- https://www.cisa.gov/vulnerability-disclosure-policy-template
- https://www.cisa.gov/news-events/directives/bod-20-01-develop-and-publish-vulnerability-disclosure-policy
- https://genai.owasp.org/resource/owasp-top-10-for-agentic-applications-for-2026/
- https://www.nist.gov/itl/ai-risk-management-framework
- https://www.usenix.org/conference/usenixsecurity24/presentation/deng
- https://arxiv.org/abs/2408.08926
- https://arxiv.org/abs/2506.02548
- https://arxiv.org/abs/2505.15216
- https://arxiv.org/abs/2406.13352
- https://arxiv.org/abs/2508.21669
- https://xbow.com/platform
- https://www.getastra.com/autonomous-pentesting
- https://horizon3.ai/nodezero/
- https://pentera.io/pentera-platform/
- https://hadrian.io/platform
- https://www.hackerone.com/platform/hai
- https://github.com/usestrix/strix
- https://github.com/aliasrobotics/cai
- https://github.com/GreyDGL/PentestGPT
- https://github.com/0x4m4/hexstrike-ai
