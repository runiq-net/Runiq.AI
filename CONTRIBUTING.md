# Contributing to Runiq AI

Thank you for your interest in contributing to Runiq AI! This guide will help you get started.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- A code editor with C# support (Visual Studio, VS Code with C# Dev Kit, Rider, etc.)

## Getting Started

1. **Fork** the repository on GitHub.
2. **Clone** your fork locally:

   ```bash
   git clone https://github.com/<your-username>/Runiq.AI.git
   cd Runiq.AI
   ```

3. **Restore** dependencies:

   ```bash
   dotnet restore
   ```

4. **Build** the solution:

   ```bash
   dotnet build
   ```

5. **Run tests** to make sure everything works:

   ```bash
   dotnet test
   ```

## Branching Strategy

Create a feature branch from `main` using the following naming conventions:

| Type | Branch Name Example |
| --- | --- |
| Feature | `feature/my-new-feature` |
| Bug fix | `fix/issue-description` |
| Documentation | `docs/update-readme` |
| Refactoring | `refactor/improve-agent-validation` |

## Making Changes

1. Keep your changes **focused and small**. One PR per logical change.
2. Add or update **XML documentation comments** for any public APIs you add or modify.
3. Add or update **unit tests** when changing runtime behavior.
4. Follow existing **code style and formatting** conventions in the project.

## Commit Messages

Use clear and descriptive commit messages:

```
<type>: <short description>

<optional body explaining the change>
```

**Types**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`

**Examples**:

```
docs: add XML doc comments to Workflow domain types
feat: add retry policy support to FlowStepBuilder
fix: resolve duplicate tool registration validation
```

## Pull Requests

1. Push your branch to your fork.
2. Open a pull request against `main` on the upstream repository.
3. Fill in the PR description explaining **what** changed and **why**.
4. Make sure CI checks pass before requesting a review.

## Coding Guidelines

- Use **C# 13** language features where appropriate.
- Prefer `sealed` classes and records for public API types.
- Use XML doc comments (`/// <summary>`) on all public types and members.
- Validate constructor parameters with guard clauses.
- Follow the existing project structure when adding new files.

## Reporting Issues

Found a bug or have a feature request? [Open an issue](https://github.com/runiq-net/Runiq.AI/issues) with:

- A clear title and description.
- Steps to reproduce (for bugs).
- Expected vs. actual behavior.

## License

By contributing to Runiq AI, you agree that your contributions will be licensed under the [MIT License](LICENSE).
