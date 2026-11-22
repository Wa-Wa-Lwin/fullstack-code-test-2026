# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Payment integration take-home assessment ("fullstack-code-test-2026"). Only the backend exists so far: a .NET 10 (SDK 10.0.x) ASP.NET Core Minimal API, which is still the default template (`/weatherforecast`). `docker-compose.yml` exists at the root but is empty. There is no frontend or test project yet.

## Commands

Run these from `backend/`:

```sh
dotnet build PaymentIntegration.slnx                                   # build all projects
dotnet run --project PaymentIntegration.Api                            # http://localhost:5263 ("http" profile)
dotnet run --project PaymentIntegration.Api --launch-profile https     # https://localhost:7067
```

In Development, Swagger UI is at `/swagger` and the OpenAPI document is at `/openapi/v1.json`. `PaymentIntegration.Api/PaymentIntegration.Api.http` holds sample requests.

There are no tests yet. Once a test project is added to the solution, use `dotnet test PaymentIntegration.slnx`, or `dotnet test --filter "FullyQualifiedName~<Name>"` for a single test.

If the build fails with "file is locked by PaymentIntegration.Api", a `dotnet run` instance is still running. Stop it first.

## Architecture

The solution (`backend/PaymentIntegration.slnx`, the new XML solution format) is laid out as Clean Architecture, with one folder per project:

- `PaymentIntegration.Api`: web host, endpoints, DI composition (`Program.cs`)
- `PaymentIntegration.Application`: use cases and interfaces (ports)
- `PaymentIntegration.Domain`: entities and domain rules, with no dependencies
- `PaymentIntegration.Infrastructure`: implementations of external concerns (payment gateway clients, persistence)

The class libraries contain only placeholder `Class1.cs` files, and **no `ProjectReference`s exist between them yet**. When you add them, follow the intended dependency direction: Api → Application + Infrastructure, Infrastructure → Application, Application → Domain.

Every project must stay in its own subfolder. A `.csproj` placed directly in `backend/` would glob the other projects' `.cs` and `obj/` files, which causes duplicate-assembly-attribute build errors.
