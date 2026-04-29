---
name: security-planning
description: Security design and rollout checklist for auth, proxy, network, secret, and compliance-sensitive changes.
---

# Security Planning Skill

Primary owners:
- `SecurityEngineer`
- `PlatformEngineer`
- `SolutionArchitect` for security-sensitive design work

---

## When to Use

- Authentication or authorization changes
- Proxy, gateway, or middleware changes
- Secret handling changes
- Network policy or egress changes
- Public exposure or rollout-sequencing changes

---

## Security Planning Pass

For every qualifying task, answer these before implementation is accepted.

### 1. Trust Boundary

- What is the caller identity?
- Where is authentication enforced?
- Which values come from trusted session context versus user-controlled headers or payload?

### 2. Authorization Boundary

- Which role or permission is required?
- Is least privilege preserved?
- Is tenant scope enforced at the same boundary as the permission check?

### 3. Secret and Key Handling

- Where do keys or secrets live?
- How are rotation and rollback handled?
- Are secrets kept out of source, logs, and error payloads?

### 4. Network and Runtime Exposure

- What outbound destinations are required?
- What egress should be denied by default?
- Are timeouts, retries, and keepalive settings explicit?

### 5. Audit and Detection

- Which failures must be logged?
- Which metrics or alerts prove safe rollout?
- What would indicate abuse, drift, or rollback risk?

---

## Required Deliverables

For a security-sensitive change, produce a short plan covering:

```text
SECURITY PLAN

Boundary:
- [auth/authz/tenant notes]

Controls:
- [specific controls]

Rollout:
- [feature flags, sequencing, rollback]

Evidence required:
- [tests, scans, metrics, logs]
```

---

## Hard Stops

Stop and escalate if any of these are true:

- Role or tenant is sourced from client-controlled headers without server validation
- Secrets or keys would be committed or logged
- Fallback auth path would bypass a failed stronger auth check
- Public exposure changes lack audit or rollback path
- Network policy is broadened without explicit justification
