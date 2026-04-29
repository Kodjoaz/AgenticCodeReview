---
name: security-review-checklist
description: Apply security review checklist for auth, secrets, validation, and compliance gates.
---

# Security Review Checklist Skill

**For SecurityEngineer and all agents implementing security-sensitive features**: Standardized security checklist to prevent vulnerabilities from slipping through code review.

## When to Use

- Adding authentication or authorization logic
- Handling secrets, API keys, or credentials
- Implementing rate limiting or access controls
- Adding new API endpoints with sensitive data
- Changing database schema that affects data access
- Updating dependencies (especially security-sensitive packages)

## The Checklist

### 🔐 Authentication & Authorization (Always)

- [ ] **Validate authentication state** on every protected endpoint
  - [ ] JWT token expiration checked?
  - [ ] Token signature validated?
  - [ ] User identity extracted from token?
  - [ ] Session timeout enforced?

- [ ] **RBAC rules applied** if feature touches user permissions
  - [ ] Role required documented (e.g., `role:admin`)?
  - [ ] Permission check in middleware before business logic?
  - [ ] Least-privilege principle followed?
  - [ ] Test case for unauthorized access returns 403?

- [ ] **No hardcoded credentials** anywhere
  - [ ] Search codebase: `password`, `secret`, `api_key` in non-test files?
  - [ ] All secrets loaded from environment variables?
  - [ ] `.env` file never committed to Git?

### Data Protection (If Handling Sensitive Data)

- [ ] **Encryption in transit**
  - [ ] HTTPS enforced for all endpoints?
  - [ ] No sensitive data in query parameters (use POST body)?
  - [ ] TLS certificate valid and not self-signed in production?

- [ ] **Encryption at rest** (if storing sensitive data)
  - [ ] Database credentials encrypted?
  - [ ] User passwords hashed (bcrypt/scrypt, not MD5)?
  - [ ] API keys stored in Key Vault, not in database?
  - [ ] PII (names, emails) encrypted if stored?

- [ ] **Data access control**
  - [ ] User can only access their own data (multi-tenant isolation)?
  - [ ] Admin operations require explicit audit logging?
  - [ ] Bulk data exports require approval?

### 🛡️ Input Validation (Always)

- [ ] **All inputs validated before use**
  - [ ] Query parameters checked for SQL injection?
  - [ ] Request body JSON schema validated?
  - [ ] File uploads size-limited and type-checked?
  - [ ] No eval() or exec() on user input?

- [ ] **Output encoding**
  - [ ] HTML output escaped to prevent XSS?
  - [ ] JSON responses use proper Content-Type header?
  - [ ] API errors don't leak stack traces or internal details?

### 📊 Logging & Monitoring (If Security-Critical)

- [ ] **Security events logged**
  - [ ] Failed authentication attempts logged?
  - [ ] Privilege escalation attempts logged?
  - [ ] Bulk data access logged?
  - [ ] No sensitive data (passwords, tokens) in logs?

- [ ] **Monitoring in place**
  - [ ] Alerts for failed auth attempts (>5 in 5 min)?
  - [ ] Alerts for permission denials (>10 in 5 min)?
  - [ ] Log retention >= 30 days?

### Dependency & Supply Chain (Code Review)

- [ ] **Dependencies reviewed**
  - [ ] New packages checked for known vulnerabilities?
  - [ ] Pinned versions (not `*` or `latest`)?
  - [ ] No abandoned packages used?
  - [ ] License compliance checked?

- [ ] **Third-party integration reviewed**
  - [ ] OAuth provider verified (Google, GitHub, Azure AD)?
  - [ ] Webhook signatures validated (not just accepting requests)?
  - [ ] API keys scoped to minimal permissions?

### Testing (Security Engineer Responsibility)
  - [ ] Test: invalid input -> 400

- [ ] **Integration tests with multiple users**
  - [ ] User A cannot access User B's data
  - [ ] Admin can override when appropriate
  - [ ] Rate limits enforced

## Automation & Tooling

### Pre-Commit Hooks
```bash
# Prevent committing secrets
git-secrets --pre_commit_hook -- "$@"

# Check for hardcoded credentials
grep -r "password\|secret\|api_key" src/ --include="*.py" --include="*.ts"
```

### CI/CD Checks
- **SAST** (Static Application Security Testing): `bandit` (Python), `eslint-plugin-security` (JavaScript)
- **Dependency scanning**: GitHub Dependabot alerts
- **Container scanning**: Trivy scans docker images for CVEs

### Manual Review Points
1. Authentication flow changes -> SecurityEngineer reviews
2. Permission model changes -> SecurityEngineer + DataEngineer review
3. Third-party integrations -> SecurityEngineer approves
4. Public API changes -> SecurityEngineer confirms no data leaks

## Decision Tree

```
Is this feature security-sensitive?
├─ YES: Authentication / Authorization / Secrets / Access Control?
│  └─ Run full checklist above
│  └─ Require SecurityEngineer approval
│
├─ NO: Database schema / Data access?
│  └─ Run "Data Protection" section
│  └─ Require DataEngineer + SecurityEngineer approval
│
└─ NO: UI / Styling / Display logic?
   └─ Run "Input Validation" section (XSS prevention)
   └─ Require FrontendEngineer approval
```

## Integration with conventional-commits

When committing security changes:
- **Auth feature**: `feat(auth): add multi-factor authentication support`
- **Security fix**: `security: fix XSS vulnerability in agent dashboard`
- **Dependency update**: `chore(deps): update cryptography library to fix CVE-2026-xxxx`
- **Access control**: `refactor(rbac): enforce least-privilege role assignment`

## Common Vulnerabilities & Prevention

| Vulnerability | How It Happens | How to Prevent |
|---|---|---|
| **SQL Injection** | User input directly in SQL query | Use parameterized queries (SQLAlchemy with `?` placeholders) |
| **XSS (Cross-Site Scripting)** | Unsafe HTML rendering of user input | Use template auto-escaping (React/Jinja2 default) |
| **CSRF (Cross-Site Request Forgery)** | No token validation on state-changing requests | Use CSRF token middleware; SameSite cookies |
| **Authentication Bypass** | Missing auth check on endpoint | Middleware enforces `@require_auth` on all routes |
| **Hardcoded Secrets** | API keys in source code | Use Key Vault; rotate credentials regularly |
| **Privilege Escalation** | User can modify their own role | Database constraint: users cannot self-promote |
| **Data Exposure** | Sensitive fields returned in API response | Explicitly `exclude` from Pydantic serialization |

## Approval Gate

**Security Review Required**: Yes/No decision tree

| Change Type | Required? | Checklist | Approver |
|---|---|---|---|
| Authentication | [x] | Full | SecurityEngineer |
| Authorization/RBAC | [x] | Full | SecurityEngineer |
| Secrets/Credentials | [x] | Full | SecurityEngineer |
| Data encryption | [x] | "Data Protection" | SecurityEngineer + DataEngineer |
| Third-party integration | [x] | "Dependency & Supply Chain" | SecurityEngineer |
| New API endpoint | [x] | "Input Validation" | SecurityEngineer |
| UI changes | ⚠️ | "Input Validation" (XSS only) | FrontendEngineer |
| Database schema | ⚠️ | "Data Protection" | DataEngineer |
| Dependency update | ⚠️ | "Dependency & Supply Chain" | QualityEngineer |
