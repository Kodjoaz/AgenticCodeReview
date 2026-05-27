# TDF.Integration Structural Refactoring Checklist

**Status:** Ready to execute when environment is available  
**Prerequisites:** Close all IDEs, clear build artifacts (bin/obj), ensure no file locks

---

## Phase 1: Folder & Project Renaming

### Step 1.1: Parent Folder Rename
```bash
# From repository root
git mv "Exemples/RAMQ.Samples.Queue.TDF.SeqCon" "Exemples/RAMQ.Samples.Queue.TDF.Integration"
```

### Step 1.2: Rename Sub-Projects (in new folder)
```bash
cd Exemples/RAMQ.Samples.Queue.TDF.Integration

# TDF Projects
git mv "RAMQ.Samples.Queue.TDF.SeqCon.Worker" "RAMQ.Samples.Queue.TDF.Integration.Frontend"
git mv "RAMQ.Samples.Queue.TDF.SeqCon.Subscriber" "RAMQ.Samples.Queue.TDF.Integration.Subscriber"
git mv "RAMQ.Samples.Queue.TDF.SeqCon.Consumer" "RAMQ.Samples.Queue.TDF.Integration.Consumer"
git mv "RAMQ.Samples.Queue.TDF.SeqCon.StateFul" "RAMQ.Samples.Queue.TDF.Integration.Orchestrator"

# HOA5 Projects
git mv "RAMQ.Samples.Queue.HOA5.Backend" "RAMQ.Samples.Queue.HOA5.Integration.Backend"
```

---

## Phase 2: File Renames (.csproj files)

### Step 2.1: Rename .csproj Files

**Frontend:**
```bash
cd RAMQ.Samples.Queue.TDF.Integration.Frontend
mv "RAMQ.Samples.Queue.TDF.SeqCon.Worker.csproj" "RAMQ.Samples.Queue.TDF.Integration.Frontend.csproj"
git add "RAMQ.Samples.Queue.TDF.Integration.Frontend.csproj"
git rm "RAMQ.Samples.Queue.TDF.SeqCon.Worker.csproj"
cd ..
```

**Subscriber:**
```bash
cd RAMQ.Samples.Queue.TDF.Integration.Subscriber
mv "RAMQ.Samples.Queue.TDF.SeqCon.Subscriber.csproj" "RAMQ.Samples.Queue.TDF.Integration.Subscriber.csproj"
git add "RAMQ.Samples.Queue.TDF.Integration.Subscriber.csproj"
git rm "RAMQ.Samples.Queue.TDF.SeqCon.Subscriber.csproj"
cd ..
```

**Consumer:**
```bash
cd RAMQ.Samples.Queue.TDF.Integration.Consumer
mv "RAMQ.Samples.Queue.TDF.SeqCon.Consumer.csproj" "RAMQ.Samples.Queue.TDF.Integration.Consumer.csproj"
git add "RAMQ.Samples.Queue.TDF.Integration.Consumer.csproj"
git rm "RAMQ.Samples.Queue.TDF.SeqCon.Consumer.csproj"
cd ..
```

**Orchestrator:**
```bash
cd RAMQ.Samples.Queue.TDF.Integration.Orchestrator
mv "RAMQ.Samples.Queue.TDF.SeqCon.StateFul.csproj" "RAMQ.Samples.Queue.TDF.Integration.Orchestrator.csproj"
git add "RAMQ.Samples.Queue.TDF.Integration.Orchestrator.csproj"
git rm "RAMQ.Samples.Queue.TDF.SeqCon.StateFul.csproj"
cd ..
```

**HOA5 Backend:**
```bash
cd RAMQ.Samples.Queue.HOA5.Integration.Backend
mv "RAMQ.Samples.Queue.HOA5.Backend.csproj" "RAMQ.Samples.Queue.HOA5.Integration.Backend.csproj"
git add "RAMQ.Samples.Queue.HOA5.Integration.Backend.csproj"
git rm "RAMQ.Samples.Queue.HOA5.Backend.csproj"
cd ..
```

---

## Phase 3: Update Namespaces in .cs Files

### Step 3.1: Frontend Namespace Updates
```bash
# In RAMQ.Samples.Queue.TDF.Integration.Frontend/
# Replace:
#   namespace RAMQ.Samples.Queue.TDF.SeqCon.Worker
# With:
#   namespace RAMQ.Samples.Queue.TDF.Integration.Frontend

find . -name "*.cs" -type f | xargs sed -i 's/namespace RAMQ\.Samples\.Queue\.TDF\.SeqCon\.Worker/namespace RAMQ.Samples.Queue.TDF.Integration.Frontend/g'
```

### Step 3.2: Subscriber Namespace Updates
```bash
# In RAMQ.Samples.Queue.TDF.Integration.Subscriber/
find . -name "*.cs" -type f | xargs sed -i 's/namespace RAMQ\.Samples\.Queue\.TDF\.SeqCon\.Subscriber/namespace RAMQ.Samples.Queue.TDF.Integration.Subscriber/g'
```

### Step 3.3: Consumer Namespace Updates
```bash
# In RAMQ.Samples.Queue.TDF.Integration.Consumer/
find . -name "*.cs" -type f | xargs sed -i 's/namespace RAMQ\.Samples\.Queue\.TDF\.SeqCon\.Consumer/namespace RAMQ.Samples.Queue.TDF.Integration.Consumer/g'
```

### Step 3.4: Orchestrator Namespace Updates
```bash
# In RAMQ.Samples.Queue.TDF.Integration.Orchestrator/
find . -name "*.cs" -type f | xargs sed -i 's/namespace RAMQ\.Samples\.Queue\.TDF\.SeqCon\.StateFul/namespace RAMQ.Samples.Queue.TDF.Integration.Orchestrator/g'
```

### Step 3.5: HOA5 Backend Namespace Updates
```bash
# In RAMQ.Samples.Queue.HOA5.Integration.Backend/
find . -name "*.cs" -type f | xargs sed -i 's/namespace RAMQ\.Samples\.Queue\.HOA5\.Backend/namespace RAMQ.Samples.Queue.HOA5.Integration.Backend/g'
```

---

## Phase 4: Update ProjectReferences in .csproj Files

### Step 4.1: Frontend ProjectReferences
```xml
<!-- RAMQ.Samples.Queue.TDF.Integration.Frontend.csproj -->
<!-- Replace old references: -->
<ProjectReference Include="..\RAMQ.Samples.Queue.TDF.SeqCon.Consumer\..." />

<!-- With: -->
<ProjectReference Include="..\RAMQ.Samples.Queue.TDF.Integration.Consumer\..." />
<ProjectReference Include="..\RAMQ.Samples.Queue.TDF.Integration.Producer\..." />
```

### Step 4.2: Subscriber ProjectReferences
```xml
<!-- RAMQ.Samples.Queue.TDF.Integration.Subscriber.csproj -->
<ProjectReference Include="..\RAMQ.Samples.Queue.TDF.Integration.Consumer\..." />
```

### Step 4.3: Consumer ProjectReferences
```xml
<!-- RAMQ.Samples.Queue.TDF.Integration.Consumer.csproj -->
<!-- Update any inter-project references -->
```

### Step 4.4: Orchestrator ProjectReferences
```xml
<!-- RAMQ.Samples.Queue.TDF.Integration.Orchestrator.csproj -->
<!-- Update any inter-project references -->
```

---

## Phase 5: Update Solution File

```bash
# Back in root of TDF.Integration folder
# Edit RAMQ.Samples.Queue.TDF.Integration.slnx

# Replace all old project paths:
<Project Path="RAMQ.Samples.Queue.TDF.SeqCon.Worker/..." />
<Project Path="RAMQ.Samples.Queue.TDF.SeqCon.Subscriber/..." />
<Project Path="RAMQ.Samples.Queue.TDF.SeqCon.Consumer/..." />
<Project Path="RAMQ.Samples.Queue.TDF.SeqCon.StateFul/..." />
<Project Path="RAMQ.Samples.Queue.HOA5.Backend/..." />

# With new project paths:
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Frontend/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Subscriber/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Consumer/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Orchestrator/..." />
<Project Path="RAMQ.Samples.Queue.HOA5.Integration.Backend/..." />
```

Also rename the solution file itself:
```bash
git mv "RAMQ.Samples.Queue.TDF.SeqCon.slnx" "RAMQ.Samples.Queue.TDF.Integration.slnx"
```

---

## Phase 6: Update Using Statements in .cs Files

Replace all `using` statements:

```csharp
// OLD:
using RAMQ.Samples.Queue.TDF.SeqCon.Worker.Services;
using RAMQ.Samples.Queue.TDF.SeqCon.Worker.Options;
using RAMQ.Samples.Queue.TDF.SeqCon.Worker.Telemetry;

// NEW:
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Services;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Options;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Telemetry;
```

---

## Phase 7: Update Configuration

**local.settings.json** - Update assembly names if referenced:
```json
{
  "AppSettings": {
    "ApplicationName": "TDF Integration Frontend"  // was "TDF SeqCon Worker"
  }
}
```

---

## Phase 8: Verify & Commit

### Step 8.1: Verify Structure
```bash
# From root of TDF.Integration folder
dotnet build
```

### Step 8.2: Search for Remaining Old References
```bash
grep -r "TDF.SeqCon" --include="*.cs" --include="*.csproj" --include="*.slnx"
grep -r "HOA5.Backend" --include="*.cs" --include="*.csproj"
```

Should return NO results (except in this checklist and MIGRATION.md as documentation).

### Step 8.3: Commit
```bash
cd ../../..  # Back to repository root

git add -A
git commit -m "refactor: rename TDF.SeqCon → TDF.Integration, HOA5.Backend → HOA5.Integration.Backend

Phase 1 complete: Structural alignment with target architecture (TDFPoC-Spec.md v3.0)

Changes:
- Rename parent folder: RAMQ.Samples.Queue.TDF.SeqCon → RAMQ.Samples.Queue.TDF.Integration
- Rename Worker → Frontend (Frontend is more descriptive for this component)
- Rename StateFul → Orchestrator (Durable Functions orchestration is clearer)
- Rename HOA5.Backend → HOA5.Integration.Backend (consistency)
- Update all namespaces, .csproj ProjectReferences, solution file
- Update local.settings.json, using statements, application names

Result: Symmetric architecture - all TDF components under TDF.Integration, all HOA5 components under HOA5.Integration

Next phases:
- Phase 2: Split HOA5.Consumer into HOA5.Integration.Subscriber + HOA5.Integration.Consumer
- Phase 3: Complete namespace/reference verification
- Phase 4: Final testing and validation

Co-Authored-By: Claude Haiku 4.5 <noreply@anthropic.com>"
```

---

## Troubleshooting

### If Folder Rename Fails with "Permission Denied"
1. Close all IDEs (Visual Studio, VS Code)
2. Run `dotnet clean` to remove build artifacts
3. Clear obj/bin folders manually
4. Run antivirus/file lock checker
5. Try rename again

### If .csproj Rename Fails
- Ensure IDE is closed
- Use `git add` + `git rm` instead of direct file operations
- Verify files aren't in use: `lsof | grep csproj` (on Unix) or Task Manager (Windows)

### If Tests Fail After Rename
```bash
# Clean build
dotnet clean
dotnet build
dotnet test
```

---

## Validation Checklist

After completing all phases, verify:

- [ ] Folder structure matches target:
  ```
  Exemples/RAMQ.Samples.Queue.TDF.Integration/
  ├── RAMQ.Samples.Queue.TDF.Integration.Frontend/
  ├── RAMQ.Samples.Queue.TDF.Integration.Producer/
  ├── RAMQ.Samples.Queue.TDF.Integration.Subscriber/
  ├── RAMQ.Samples.Queue.TDF.Integration.Consumer/
  ├── RAMQ.Samples.Queue.TDF.Integration.Orchestrator/
  ├── RAMQ.Samples.Queue.HOA5.Integration.Subscriber/
  ├── RAMQ.Samples.Queue.HOA5.Integration.Consumer/
  └── RAMQ.Samples.Queue.HOA5.Integration.Backend/
  ```

- [ ] All .csproj files renamed correctly
- [ ] All namespaces updated (no `TDF.SeqCon` or `HOA5.Backend` references)
- [ ] Solution file updated with new project paths
- [ ] `dotnet build` succeeds
- [ ] No compiler warnings about missing namespaces
- [ ] Git history preserved (use `git log --follow` to verify)

---

## Summary

This refactoring achieves:
✅ **Symmetric architecture** - TDF.Integration + HOA5.Integration naming
✅ **Clarity** - Component names reflect their roles (Frontend, Subscriber, Consumer, Orchestrator, Producer)
✅ **Alignment** - Matches TDFPoC-Spec.md v3.0 target state
✅ **Clean abstractions** - Already implemented (IProducerMessage, ITdfProducerService, etc.)
✅ **Git history preserved** - All renames tracked via git

Estimated time: 30-45 minutes
