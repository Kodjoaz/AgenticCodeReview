---
name: python-analysis
description: Analyze and refactor Python code safely using standardized code-inspection patterns.
---

# Python Analysis Skill

**For agents working with Python codebases**: Use Pylance language server capabilities to analyze code, find errors, and perform refactoring safely.

## When to Use

- Analyzing Python files for syntax/import errors
- Finding where a function/class is used across the codebase
- Refactoring with confidence (rename, extract, inline)
- Understanding module dependencies
- Generating docstrings and type hints

## Capabilities

### Code Analysis
- **Syntax checking**: Find incomplete imports, indentation errors, undefined names
- **Import analysis**: Resolve circular imports, missing modules, unused imports
- **Type hints**: Validate type annotations, infer types from code
- **Documentation**: Generate docstrings from code structure

### Refactoring
- **Safe renaming**: Rename functions/classes across all files
- **Extract methods**: Pull common code into reusable functions
- **Inline operations**: Replace function calls with inline implementations
- **Code generation**: Generate boilerplate from existing patterns

### Environment Analysis
- **Python version checks**: Verify compatibility with project's Python version
- **Module availability**: Check installed packages and versions
- **Environment inspection**: Analyze virtual env setup, sys.path, interpreter details

## Workflow

### Find & Replace Pattern
1. Search for the symbol with `pylanceDocuments` or `pylanceImports`
2. Analyze all usages with `pylanceInvokeRefactoring` (get-usages option)
3. Plan the change
4. Use refactoring to apply consistently across files
5. Run syntax check with `pylanceSyntaxErrors` to validate

### Docstring Generation
1. Use `pylanceDocString` to extract current docs
2. Analyze function signature with `pylanceDocuments`
3. Generate from adjacent patterns (conventions in repo)
4. Validate grammar and completeness

### Type Checking
1. Run `pylanceImports` to find missing types
2. Use `pylanceRunCodeSnippet` to test type narrowing
3. Validate with `pylanceSyntaxErrors`

## Best Practices

- **Don't spray refactoring**: Use refactoring for high-value, localized changes
- **Check before committing**: Always verify `pylanceSyntaxErrors` returns clean before claiming done
- **Respect conventions**: Match existing docstring style, naming patterns, type annotation style
- **Document breaking changes**: If refactoring changes public API, update CHANGELOG
- **Test after refactoring**: Run unit tests (agent's responsibility) to ensure behavior preserved

## Integration with conventional-commits

When refactoring Python code:
- **Rename internal functions**: `refactor: rename X to Y for clarity`
- **Extract common patterns**: `refactor: extract shared auth logic into utils.authenticate_user()`
- **Add type hints**: `refactor: add type hints to services/\*.py`
- **Update docstrings**: `docs: improve docstring coverage in models.py`

See `conventional-commits` skill for commit format details.
