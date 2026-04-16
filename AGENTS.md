# Repository Guidelines

## Project Structure & Module Organization
This is a .NET solution organized by layer:
- `AINWZ/` — ASP.NET Core web host and entry point.
- `AINWZ.Application/` — application services, contracts, and DTOs.
- `AINWZ.Domain/` — core domain models and rules.
- `AINWZ.Infrastructure/` — persistence, external integrations, and framework services.
- `AINWZ.slnx` — solution root.

Keep new code in the layer that owns the responsibility. Avoid cross-layer dependencies unless they follow the existing reference chain.

## Build, Test, and Development Commands
Use the .NET SDK from the solution root:
- `dotnet restore` — restore NuGet packages.
- `dotnet build AINWZ.slnx` — compile the full solution.
- `dotnet run --project AINWZ/AINWZ.csproj` — start the web API locally.
- `dotnet test` — run tests when a test project is present.

## Coding Style & Naming Conventions
- C# uses 4-space indentation and standard .NET formatting.
- Project files target `net10.0`; keep nullable context aligned with existing `disable` settings unless explicitly changing the project policy.
- Use PascalCase for types, methods, and public members; camelCase for locals and parameters.
- Match existing folder names such as `Contracts/Auth/Dto` and `Contracts/Users/Dto`.
- Prefer minimal, explicit changes consistent with current solution patterns.

## Testing Guidelines
There is no test project in the repository yet. When adding tests, place them in a separate `*.Tests` project and name test files after the unit under test, such as `UserServiceTests.cs`. Prefer clear arrange-act-assert structure and keep tests deterministic.

## Commit & Pull Request Guidelines
No commit history is available in this workspace, so follow concise imperative commits, for example: `add user contract dto`. For pull requests, include:
- a short summary of the change
- linked issue or requirement, if any
- notes on build/test results
- screenshots only when UI changes are involved

## Security & Configuration Tips
Do not commit secrets, local connection strings, or environment-specific overrides. Keep configuration in appsettings files or environment variables, and verify generated build output remains ignored by `.gitignore`.
