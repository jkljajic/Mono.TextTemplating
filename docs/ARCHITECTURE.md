# Mono.TextTemplating — Architecture

## Pipeline

```
.tt file (string content)
        │
        ▼
┌──────────────────────────────────────────────┐
│  Tokeniser (State machine)                    │
│  ─────────────────────────                   │
│  Scans character-by-character for T4 tags:    │
│    <# ... #>  → Block                         │
│    <#= ... #> → Expression                    │
│    <#+ ... #> → Helper (class feature)        │
│    <#@ ... #> → Directive                     │
│  Emits: (State, Value, Location) tuples       │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│  ParsedTemplate                              │
│  ────────────────                            │
│  Consumes token stream:                       │
│    - Groups directive attributes              │
│    - Recursively resolves <#@ include #>      │
│    - Validates directive structure            │
│  Output: List<Directive> + List<TemplateSegment> │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│  TemplatingEngine                            │
│  ─────────────────                           │
│  1. GetSettings() — parse template directive  │
│  2. ProcessDirectives() — run processors      │
│  3. GenerateCompileUnit() — build CodeDOM AST │
│  4. GenerateCode() — compile to assembly      │
│  5. Create CompiledTemplate — wrap result     │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│  CompiledTemplate                            │
│  ────────────────                            │
│  - Loads compiled assembly                   │
│  - Instantiates generated class              │
│  - Wires Host/Session properties             │
│  - .Process() runs TransformText()           │
│  - Returns string output                     │
└──────────────────────────────────────────────┘
```

## Key Types

| Type | Role |
|---|---|
| `TemplatingEngine` | Core orchestrator (963 LOC) — parsing, CodeDOM gen, compilation |
| `TemplateGenerator` | Default host implementation (373 LOC) — file I/O, assembly resolution |
| `Tokeniser` | Lexer (295 LOC) — state machine scanning `.tt` content |
| `ParsedTemplate` | AST builder (339 LOC) — directive parsing, include resolution |
| `CompiledTemplate` | Runtime wrapper (113 LOC) — loads and executes compiled template |
| `TemplateSettings` | Settings bag (75 LOC) — parsed from `<#@ template #>` |
| `TextTransformation` | Base class (219 LOC) — `Write`, `WriteLine`, `Error`, indentation |
| `Engine` | Thin facade (58 LOC) — implements `ITextTemplatingEngine` |

## Namespaces

- **`Mono.TextTemplating`** — Engine core: tokenizer, parser, compiler, template runner
- **`Microsoft.VisualStudio.TextTemplating`** — Public API contracts matching VS T4 SDK

## Cross-Cutting Concerns

| Concern | Current | Target |
|---|---|---|
| Compilation | `CodeDomProvider` (CSharpCodeProvider) | `Microsoft.CodeAnalysis` (Roslyn) |
| Isolation | `AppDomain` / `MarshalByRefObject` | `AssemblyLoadContext` (collectible) |
| File I/O | `File.ReadAllText` / `File.WriteAllText` | Same + async overloads |
| Error handling | `CompilerErrorCollection` | Same |
| Extensibility | `DirectiveProcessor`, `ITextTemplatingEngineHost` | Same + `ITemplateCompiler` |
