---
name: threat-model
description: Structured threat modeling pass for auth, tenant boundary, data flow, and externally exposed platform changes.
---

# Threat Model Skill

Primary owners:
- `SecurityEngineer`
- `SolutionArchitect`
- `PlatformEngineer` for boundary and exposure changes

---

## When to Use

- Authentication, authorization, SSO, OIDC, or token flow changes
- Cross-tenant access paths or tenant-isolation-sensitive changes
- Public endpoint, proxy, gateway, or webhook exposure
- New service-to-service trust boundaries
- Secrets, signing keys, or privileged credential flows
- New external integrations that carry customer data or control-plane actions

---

## Threat Modeling Pass

Work through these in order.

### 1. Assets and Trust Boundaries

Identify:
- protected assets
- actors and identities
- inbound and outbound trust boundaries
- privileged operations

Required output:
- what must be protected
- where the trust boundary changes

### 2. Entry Points and Data Flows

Map:
- user entry points
- service entry points
- background or webhook paths
- data stores and message hops

Required output:
- one concise request/data flow from ingress to persistence or action

### 3. STRIDE Pass

For each critical boundary or flow, assess:
- `spoofing`
- `tampering`
- `repudiation`
- `information disclosure`
- `denial of service`
- `elevation of privilege`

Only keep threats that are credible in this platform context.

### 4. Tenant and Role Isolation Check

Explicitly answer:
- can one tenant influence or read another tenant's data?
- can one role bypass approval or administrative checks?
- can an internal service call skip an intended user-level authorization boundary?

### 5. Mitigations and Residual Risk

List:
- required mitigations before implementation approval
- monitoring or audit signals required at release
- residual risks that remain acceptable only with human approval

---

## Threat Model Output

```text
THREAT MODEL

Change:
- [one-line description]

Protected assets:
- [asset]

Trust boundaries:
- [boundary]

Critical flow:
- [entry point -> service -> store/action]

Threats:
- [STRIDE category] — [credible threat] — [impact]

Tenant/role isolation findings:
- [finding]

Required mitigations before approval:
- [mitigation]

Monitoring and audit requirements:
- [signal or log]

Residual risks:
- [none | risk]

Recommendation:
- APPROVE FOR DESIGN
- CONDITIONAL — mitigation required first
- BLOCKED — architecture or control gap too large
```

---

## Guardrails

- Do not generate generic threat catalogs with no link to the actual flow.
- Prefer 3 to 7 credible threats over exhaustive but low-value enumeration.
- If tenant isolation, auth bypass, or privileged action ambiguity remains unresolved, return `BLOCKED`.
