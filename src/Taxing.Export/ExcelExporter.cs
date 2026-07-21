using ClosedXML.Excel;
using Taxing.Core.Models;

namespace Taxing.Export;

/// <summary>
/// Produces a multi-sheet Excel workbook (one sheet per ITR schedule) whose column
/// headers mirror the corresponding ITR utility fields, ready for transcription.
/// </summary>
public static class ExcelExporter
{
    public static byte[] ToWorkbook(ItrResult result)
    {
        using var wb = new XLWorkbook();

        BuildA2(wb, result);
        BuildA3(wb, result);
        BuildFsi(wb, result);
        BuildTr(wb, result);
        BuildForm67(wb, result);
        BuildCapitalGains(wb, result);
        BuildWarnings(wb, result);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static IXLWorksheet AddSheet(XLWorkbook wb, string name, params string[] headers)
    {
        var ws = wb.Worksheets.Add(name);
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
        }
        return ws;
    }

    // Prevent CSV/Excel formula injection by neutralizing leading formula triggers.
    private static XLCellValue Safe(string? text)
    {
        var s = text ?? string.Empty;
        if (s.Length > 0 && (s[0] is '=' or '+' or '-' or '@'))
            s = "'" + s;
        return s;
    }

    private static void BuildA2(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "FA-A2 Custodial",
            "Country Code", "Name of Institution", "Address", "Account Number",
            "Status", "Account Opening Date", "Peak Balance (INR)",
            "Closing Balance (INR)", "Gross Amount Credited (INR)");
        int row = 2;
        foreach (var a in r.ScheduleFaA2)
        {
            ws.Cell(row, 1).Value = Safe(a.CountryCodeItr);
            ws.Cell(row, 2).Value = Safe(a.InstitutionName);
            ws.Cell(row, 3).Value = Safe(a.InstitutionAddress);
            ws.Cell(row, 4).Value = Safe(a.AccountNumber);
            ws.Cell(row, 5).Value = Safe(a.Status);
            ws.Cell(row, 6).Value = a.AccountOpenedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ws.Cell(row, 7).Value = a.PeakBalanceInr;
            ws.Cell(row, 8).Value = a.ClosingBalanceInr;
            ws.Cell(row, 9).Value = a.GrossCreditedInr;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildA3(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "FA-A3 Equity",
            "Country/Region name", "Country Name and Code", "Name of entity",
            "Address of entity", "ZIP Code", "Nature of entity",
            "Date of acquiring the interest", "Initial value of the investment",
            "Peak value of investment during the Period", "Closing balance",
            "Total gross amount paid/credited with respect to the holding during the period",
            "Total gross proceeds from sale or redemption of investment during the period");
        int row = 2;
        foreach (var a in r.ScheduleFaA3)
        {
            ws.Cell(row, 1).Value = Safe(a.CountryCode);
            ws.Cell(row, 2).Value = Safe($"{a.CountryCode} ({a.CountryCodeItr})");
            ws.Cell(row, 3).Value = Safe(a.EntityName);
            ws.Cell(row, 4).Value = Safe(a.EntityAddress);
            ws.Cell(row, 5).Value = string.Empty; // not available in source statement metadata
            ws.Cell(row, 6).Value = Safe("Equity and Debt Interest");
            ws.Cell(row, 7).Value = a.AcquisitionDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ws.Cell(row, 8).Value = a.InitialValueInr;
            ws.Cell(row, 9).Value = a.PeakValueInr;
            ws.Cell(row, 10).Value = a.ClosingValueInr;
            ws.Cell(row, 11).Value = a.GrossDividendInr;
            ws.Cell(row, 12).Value = a.ProceedsInr;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildFsi(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "FSI",
            "Country Code", "Head of Income", "Income Date", "Income (INR)",
            "Foreign Tax Paid (INR)", "Treaty Article", "FX Rate", "FX Rate Date");
        int row = 2;
        foreach (var f in r.ScheduleFsi)
        {
            ws.Cell(row, 1).Value = Safe(f.CountryCodeItr);
            ws.Cell(row, 2).Value = Safe(f.IncomeHead);
            ws.Cell(row, 3).Value = f.IncomeDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 4).Value = f.IncomeInr;
            ws.Cell(row, 5).Value = f.ForeignTaxInr;
            ws.Cell(row, 6).Value = Safe(f.TreatyArticle);
            ws.Cell(row, 7).Value = f.FxRate;
            ws.Cell(row, 8).Value = f.FxRateDate.ToString("yyyy-MM-dd");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildTr(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "TR",
            "Country Code", "Foreign Tax Paid (INR)", "Relief Claimed (INR)", "Section");
        int row = 2;
        foreach (var t in r.ScheduleTr)
        {
            ws.Cell(row, 1).Value = Safe(t.CountryCodeItr);
            ws.Cell(row, 2).Value = t.ForeignTaxPaidInr;
            ws.Cell(row, 3).Value = t.ReliefClaimedInr;
            ws.Cell(row, 4).Value = Safe(t.ReliefSection);
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildForm67(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "Form 67",
            "Country Code", "Source of Income", "Income (INR)",
            "Foreign Tax Paid (INR)", "Treaty Article", "FX Rate", "FX Rate Date");
        int row = 2;
        foreach (var f in r.Form67)
        {
            ws.Cell(row, 1).Value = Safe(f.CountryCodeItr);
            ws.Cell(row, 2).Value = Safe(f.SourceOfIncome);
            ws.Cell(row, 3).Value = f.IncomeInr;
            ws.Cell(row, 4).Value = f.ForeignTaxInr;
            ws.Cell(row, 5).Value = Safe(f.TreatyArticle);
            ws.Cell(row, 6).Value = f.FxRate;
            ws.Cell(row, 7).Value = f.FxRateDate.ToString("yyyy-MM-dd");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildCapitalGains(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "CG",
            "Symbol", "Acquisition Date", "Sale Date", "Quantity",
            "Proceeds (INR)", "Cost (INR)", "Gain (INR)", "Term", "Holding Months");
        int row = 2;
        foreach (var g in r.CapitalGains)
        {
            ws.Cell(row, 1).Value = Safe(g.Symbol);
            ws.Cell(row, 2).Value = g.AcquisitionDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 3).Value = g.SaleDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 4).Value = g.Quantity;
            ws.Cell(row, 5).Value = g.ProceedsInr;
            ws.Cell(row, 6).Value = g.CostInr;
            ws.Cell(row, 7).Value = g.GainInr;
            ws.Cell(row, 8).Value = g.Term.ToString();
            ws.Cell(row, 9).Value = g.HoldingMonths;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildWarnings(XLWorkbook wb, ItrResult r)
    {
        var ws = AddSheet(wb, "Notes", "Warnings & Assumptions");
        int row = 2;
        foreach (var w in r.Warnings)
        {
            ws.Cell(row, 1).Value = Safe(w);
            row++;
        }
        ws.Column(1).Width = 120;
    }
}
