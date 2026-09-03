namespace PbiAssure.Reporting;

/// <summary>
/// The PBI Assure mark.
///
/// Three nodes joined into an "A": one above, two below, with a crossbar. It reads as the
/// initial of the product name and as the thing the product actually does — following a
/// dependency from one object to the objects beneath it. Kept to a single stroke weight so it
/// survives at favicon size, and drawn in <c>currentColor</c> so it inherits the theme.
/// </summary>
public static class BrandIdentity
{
    /// <summary>The mark on its own, for a wordmark lockup. Inherits the surrounding colour.</summary>
    public const string MarkSvg =
        "<svg class=\"brand-mark\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" " +
        "stroke-width=\"1.9\" stroke-linecap=\"round\" aria-hidden=\"true\" focusable=\"false\">" +
        "<path d=\"M11.1 6.4 6.1 17.3M12.9 6.4l5 10.9M8.4 14.2h7.2\"/>" +
        "<circle cx=\"12\" cy=\"4.4\" r=\"2.1\" fill=\"currentColor\" stroke=\"none\"/>" +
        "<circle cx=\"5.2\" cy=\"19.2\" r=\"2.1\" fill=\"currentColor\" stroke=\"none\"/>" +
        "<circle cx=\"18.8\" cy=\"19.2\" r=\"2.1\" fill=\"currentColor\" stroke=\"none\"/>" +
        "</svg>";

    /// <summary>
    /// The mark knocked out of an accent tile, as a data URI. A generated report has no sibling
    /// files to link to, so its icon has to travel inside the document.
    /// </summary>
    public const string FaviconDataUri =
        "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E" +
        "%3Crect width='24' height='24' rx='5' fill='%234f46e5'/%3E" +
        "%3Cg fill='none' stroke='%23ffffff' stroke-width='1.9' stroke-linecap='round'%3E" +
        "%3Cpath d='M11.1 6.4 6.1 17.3M12.9 6.4l5 10.9M8.4 14.2h7.2'/%3E%3C/g%3E" +
        "%3Cg fill='%23ffffff'%3E%3Ccircle cx='12' cy='4.4' r='2.1'/%3E" +
        "%3Ccircle cx='5.2' cy='19.2' r='2.1'/%3E%3Ccircle cx='18.8' cy='19.2' r='2.1'/%3E%3C/g%3E%3C/svg%3E";
}
