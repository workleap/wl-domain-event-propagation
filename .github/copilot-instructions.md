# Workleap.DomainEventPropagation

Azure Event Grid domain event propagation library providing NuGet packages for publishing and subscribing to domain events using both push and pull delivery methods. This is a .NET solution with multiple projects and comprehensive test coverage.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Bootstrap and Build
- Install .NET SDK 9.0.304: `curl -L https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh | bash -s -- --version 9.0.304`
- Install .NET SDK 8.0.404 for tests: `curl -L https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh | bash -s -- --version 8.0.404`
- Update PATH: `export PATH="$HOME/.dotnet:$PATH"`
- Navigate to src directory: `cd /path/to/repo/src`
- Clean and build: `dotnet clean -c Debug && dotnet build -c Debug` -- takes 12 seconds. NEVER CANCEL. Set timeout to 30+ seconds.
- **DO NOT use Release configuration in sandbox environments** -- Release build requires GitVersion and proper Git repository context. It will fail with MSB3073 error in sandboxed environments.

### Testing
- Run all tests: `dotnet test -c Debug --no-restore`
- Tests use testcontainers and may require Docker access.
- **Expected results**: 0 failures.

### Code Quality and Formatting
- Check formatting: `dotnet format --verify-no-changes`
- Fix formatting: `dotnet format`
- **ALWAYS run `dotnet format` before committing** or CI will fail.
- Repository uses Workleap.DotNet.CodingStandards for code standards enforcement.

### PowerShell Build Script (Production)
- For comprehensive builds: `./Build.ps1` (requires proper Git repository context)
- Skip tests in build: `./Build.ps1 -SkipTests`
- **Note**: This script is designed for CI/CD environments and may fail in sandbox due to GitVersion requirements.

## Validation

- ALWAYS manually test publishing and subscription scenarios when making core changes.
- ALWAYS run through complete build and test cycle: clean, build, format, test.
- ALWAYS run `dotnet format` and ensure no formatting issues remain.
- You can build and test the solution locally, but Release configuration requires proper CI environment.
- The solution builds successfully in Debug mode and all tests pass consistently.

## Common Tasks

### Key Projects Structure
```
src/
├── Workleap.DomainEventPropagation.Abstractions      # Core interfaces and attributes
├── Workleap.DomainEventPropagation.Publishing        # Event publishing functionality
├── Workleap.DomainEventPropagation.Subscription      # Push delivery subscription
├── Workleap.DomainEventPropagation.Subscription.PullDelivery  # Pull delivery subscription
├── Workleap.DomainEventPropagation.Analyzers         # Roslyn analyzers for code quality
├── Workleap.DomainEventPropagation.*.ApplicationInsights     # Application Insights integrations
└── *.Tests/                                          # Comprehensive test projects
```

### Frequently Used Commands
```bash
# Complete development workflow
export PATH="$HOME/.dotnet:$PATH"
cd src
dotnet clean -c Debug
dotnet build -c Debug          # 12s - NEVER CANCEL (30s timeout)
dotnet format                  # Fix any formatting issues (20s)
dotnet test -c Debug --no-restore  # 13s - NEVER CANCEL (45s timeout)
```

### Working with Domain Events
- Domain events use `[DomainEvent("event-name")]` attribute on classes implementing `IDomainEvent`
- Publishing uses `IEventPropagationClient.PublishDomainEventAsync()`
- Subscription handlers implement `IDomainEventHandler<TEvent>`
- Supports both Azure Event Grid EventGridEvent and CloudEvent schemas

### Key Files to Check After Changes
- Always check `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` for public API changes
- Review analyzer rules in `Workleap.DomainEventPropagation.Analyzers`
- Test integration points in `*.Tests` projects, especially API and behavioral tests
- Configuration validation in `EventPropagationSubscriptionOptionsValidator.cs`

### Testing Scenarios
When making changes, always validate:
1. **Domain Event Publishing**: Create sample event, publish via `IEventPropagationClient`
2. **Subscription Handling**: Verify handlers receive and process events correctly
3. **Configuration Validation**: Test with various Azure Event Grid configurations
4. **Application Insights Integration**: Verify tracing and telemetry work correctly

### Build Timing Expectations
- **Clean + Build (Debug)**: ~12 seconds
- **Test Suite**: ~13 seconds (162 tests) - 161 pass, 1 skip
- **Format Check**: ~20 seconds  
- **Total Development Cycle**: ~40 seconds

### Common Issues and Solutions
- **GitVersion errors**: Use Debug configuration in sandbox environments
- **Test failures**: Ensure both .NET 8.0 and 9.0 SDKs are installed
- **Format failures**: Run `dotnet format` to fix before committing
- **PublicAPI analyzer errors**: Update PublicAPI.Unshipped.txt for new public APIs

### Repository-Specific Notes
- Repository uses semantic versioning and automated NuGet publishing
- Preview packages published on every main branch commit
- Stable releases created by manually tagging with format `x.y.z`
- Uses Azure Event Grid namespace topics for pull delivery (CloudEvent schema only)
- Comprehensive Roslyn analyzers enforce domain event naming and usage conventions

### CI/CD Integration
- GitHub Actions workflows handle build and publishing
- Build script supports environment variables for NuGet publishing
- Release configuration requires proper Git repository context for GitVersion
- CI uses both .NET 8.0 and 9.0 SDKs for compatibility