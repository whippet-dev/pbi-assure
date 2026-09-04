namespace PbiAssure.Web;

/// <summary>
/// The application's top-level destinations. A page passes its own value to <c>AppNavigation</c> so
/// exactly one navigation link carries <c>aria-current="page"</c>.
///
/// This replaces the earlier two-destination boolean. Navigation stays a dumb component: it is told
/// which page it is on rather than resolving the route itself, so it needs no injected
/// <c>NavigationManager</c> and remains testable as plain markup.
/// </summary>
public enum AppPage
{
    Analyse,
    Coverage,
    About,
}
