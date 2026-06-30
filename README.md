# Educare-Report

Servico Windows-only para renderizar relatorios C1FlexReport em PDF usando .NET Framework 4.8 e ComponentOne 4.5.20201.416.

## Requisitos no servidor Windows

- .NET Framework 4.8 Runtime/Developer Pack.
- IIS com ASP.NET 4.x habilitado.
- SQL Server Native Client 11 (`SQLNCLI11`) ou provider OleDb equivalente usado na connection string.
- Application Pool em 64 bits, se os providers instalados forem 64 bits.

## Endpoints

- `POST /api/relatorios/upload-flxr`
  - `multipart/form-data`
  - campo `arquivo`
  - salva/substitui em `App_Data/RelatoriosFlexReport`

- `POST /api/relatorios/gerar-pdf`
  - JSON:

```json
{
  "arquivo": "LePerini.flxr",
  "relatorio": "rptRelAval2025EI",
  "sql": "SELECT ...",
  "conexao": "Provider=SQLNCLI11;Persist Security Info=False;User ID=...;Password=...;Encrypt=yes;Initial Catalog=...;Data Source=...;DataTypeCompatibility=80;MARS Connection=False;Package Size=32767",
  "parametros": ["1º Semestre", "25/06/2026"],
  "titulo": "Avaliação Descritiva"
}
```

## Seguranca

Configure `ReportApiKey` no `Web.config`. Quando preenchido, as chamadas devem enviar:

`X-Report-Api-Key: valor-configurado`

Deixe o site acessivel apenas internamente ou via firewall/reverse proxy. A pasta `App_Data` nao e servida diretamente pelo IIS.
