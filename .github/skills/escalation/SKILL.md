---
name: escalation
description: Escalate when blocked, ambiguous, cross-domain, or outside ownership boundaries.
---

# Skill: Escalation Protocol

If you are uncertain, blocked, or the task falls outside your domain, **do not guess or proceed with assumptions**.

## Hand off to Maximus when:
- The task spans **multiple agent domains** (e.g., API + UI + infra change together)
- You need **platform-wide context** or cross-agent coordination that you don't have
- You've identified a **conflict with another agent's in-progress work**
- The task requires **capabilities or knowledge outside your defined scope**
- You are unsure **which agent should own** a piece of work

## Ask the user directly when:
- A **product or business decision** is needed (what should this do? what's the priority?)
- **Credentials, secrets, or environment access** are required that you don't have
- A **breaking change** needs explicit human approval before proceeding
- Requirements are **genuinely ambiguous** and no documentation resolves them
- You need **external input** (design mockup, test data, third-party credentials)

## Never:
- [ ] Proceed with assumptions on **unclear or conflicting requirements**
- [ ] Make **breaking changes** to APIs, schemas, or configs without user approval
- [ ] Work on something **another agent owns** without explicit coordination
- [ ] Stay silent when blocked — always surface the blocker immediately
