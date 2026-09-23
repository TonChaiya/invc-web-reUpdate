using System.Globalization;

namespace Invc.Core.Diagnostics;

/// <summary>
/// Values for HTML <c>&lt;input type="date"&gt;</c> / <c>type="datetime-local"</c>.
/// The application runs under th-TH, whose calendar is Buddhist, so <c>ToString("yyyy-MM-dd")</c> renders the year as
/// 2569 — which browsers reject, leaving the field empty (production defect 2026-09-23: the return form showed no
/// default date). HTML date controls are defined on the proleptic Gregorian calendar, so these values are always
/// formatted with the invariant culture. Display text elsewhere keeps the Thai calendar.
/// </summary>
public static class HtmlDateValue
{
    /// <summary>"yyyy-MM-dd" (Gregorian) for <c>type="date"</c>.</summary>
    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"yyyy-MM-ddTHH:mm" (Gregorian) for <c>type="datetime-local"</c>.</summary>
    public static string DateTimeLocal(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    public static string? Date(DateTime? value) => value is null ? null : Date(value.Value);
    public static string? DateTimeLocal(DateTime? value) => value is null ? null : DateTimeLocal(value.Value);
}
