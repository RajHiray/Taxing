# Taxing — ITR Foreign Asset &amp; Income Calculator

A **Blazor WebAssembly** static web app that helps a salaried resident of India (for example, a
**Microsoft India** employee whose **Microsoft RSUs vest into a US brokerage account** and earn
dividends) prepare the foreign-asset and foreign-income parts of **ITR-2 / ITR-3**.

Upload your broker statement and Taxing computes ready-to-transcribe values for:

- **Schedule FA — A2**: foreign custodial (brokerage) account
- **Schedule FA — A3**: foreign equity &amp; debt interest (held shares)
- **Schedule FSI**: foreign source income (dividends)
- **Schedule TR** &amp; **Form 67**: relief for foreign tax paid (DTAA / FTC)
- **Schedule CG**: capital gains on share sales (FIFO lot matching)

> ⚠️ **Not tax advice.** Taxing is a calculation aid. Verify every figure and consult a qualified
> Chartered Accountant before filing.

## Privacy by design

All parsing and calculation run **entirely in your browser** (WebAssembly). Your statement is
never uploaded to any server and is cleared when you close the tab. Account numbers are masked
(all but the last four characters) in every output.

## Key conventions (the "CA maths")

| Concern | Rule applied |
| --- | --- |
| Schedule FA period | Calendar year (1 Jan – 31 Dec) |
| Income period (FSI/CG/TR) | Financial year (1 Apr – 31 Mar) |
| Income conversion | **Rule 128** — SBI TT buying rate on the last day of the month *preceding* accrual |
| FA valuation | SBI TTBR on the valuation date, with preceding-working-day fallback |
| Capital gains term | Foreign/unlisted shares are long-term if held &gt; 24 months |
| Capital gains cost | Vesting fair-market value (already taxed as perquisite) — avoids double taxation |

## Project structure

```
Taxing.sln
├─ src/
│  ├─ Taxing.Core/      # domain models, FX service, Rule 128, FA/FSI/TR/CG engine
│  ├─ Taxing.Parsing/   # broker CSV profiles (Fidelity, Morgan Stanley, E*TRADE)
│  ├─ Taxing.Export/    # Excel (ClosedXML) + JSON exporters
│  └─ Taxing.Web/       # Blazor WebAssembly UI
└─ tests/
   └─ Taxing.Core.Tests/  # unit tests (FX, Rule 128, schedules, parsing, export)
```

## Build, test, run

```bash
dotnet build                                   # build all projects
dotnet test                                    # run unit tests
dotnet run --project src/Taxing.Web            # run the app locally
```

Then open the printed `http://localhost:<port>` URL.

## Usage

1. Select your **broker** and **assessment year**.
2. Enter **account details** (for Schedule FA) — these come from your **Fidelity December
   account statement (PDF)** or account profile and are typed in manually (the transaction export
   does not contain them).
3. Upload the broker **statement CSV** — Fidelity's **transaction history export** from
   *Accounts &amp; Trade → Portfolio → Activity &amp; Orders → Download (CSV)*, covering the period
   you are filing.
4. Provide **SBI TTBR rates** — the State Bank of India **Telegraphic Transfer Buying Rate**
   (month-end values, from a public SBI rate source; a sample set is bundled — replace with official
   rates) and the **31 December price** per symbol.
5. Click **Calculate**, review the tabbed schedules, and **download** the Excel workbook or JSON.

## Deployment

Pushes to `main` are published to **GitHub Pages** by
[`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml). The workflow rewrites
the `<base href>` to `/Taxing/`, adds a SPA `404.html` fallback and a `.nojekyll` marker.

Enable it under **Settings → Pages → Build and deployment → Source: GitHub Actions**.

## Disclaimer

This software is provided "as is", without warranty of any kind. It does not constitute
professional tax, legal or financial advice.