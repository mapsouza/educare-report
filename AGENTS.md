# AGENTS Guide for Educare-Report

## Big Picture
Servico Windows-only para renderizar relatorios C1FlexReport em PDF usando .NET Framework 4.8 e ComponentOne 4.5.20201.416.

## Requisitos no servidor Windows

- .NET Framework 4.8 Runtime/Developer Pack.
- IIS com ASP.NET 4.x habilitado.
- SQL Server Native Client 11 (`SQLNCLI11`) ou provider OleDb equivalente usado na connection string.
- Application Pool em 64 bits, se os providers instalados forem 64 bits.

## Git And Branch Workflow
- Never work directly on the `main`, `dev`, or `Dev` branches.
- Before starting any coding task, run `git branch --show-current` and confirm that the current branch name and purpose are compatible with the requested work.
- If the current branch is `main`, automatically create and switch to a new feature/bugfix branch with a descriptive name before editing code, then continue.
- If the current branch is `dev`, `Dev`, or otherwise incompatible with the request, stop and ask the user how they want to proceed before editing code.
- When the request comes from a GitHub issue, open a pull request for the completed work so the issue, branch, review, and merge history remain organized.

