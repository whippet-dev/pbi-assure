namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    // Small per-entry UI snapshots, scoped to this loaded document. No report data or storage layer.
    private const string InvestigationHistoryScript = """
      const session = `${Date.now()}-${Math.random()}`;
      const controls = [...mainContent.querySelectorAll('input[type="search"], select')].filter(control => control.id);
      const disclosures = new Map([...mainContent.querySelectorAll('details')].map(detail => {
        const section = detail.closest('[data-report-section]');
        const key = detail.id ? `id:${detail.id}`
          : `${section?.id}:${[...section.querySelectorAll('details')].indexOf(detail)}`;
        return [key, detail];
      }));
      const selectedReveal = new Map();
      const selectionNote = document.getElementById('investigation-selection-note');
      const returnRow = document.getElementById('investigation-return-row');
      const returnLink = document.getElementById('investigation-return');
      let routeRevision = 0;
      let entry = null;
      let handledLocation = '';
      let restoring = false;
      history.scrollRestoration = 'manual';

      const fragmentFromLocation = () => {
        try { return decodeURIComponent(location.hash.slice(1)); }
        catch { return ''; }
      };
      const resetSelectedDestination = () => {
        const hadSelection = selectedReveal.size > 0;
        selectedReveal.forEach((hidden, element) => { element.hidden = hidden; });
        selectedReveal.clear();
        selectionNote.hidden = true;
        // A reader may have edited the controls while investigating the selected exception.
        // Reapply their current filters rather than treating its earlier visibility as permanent.
        if (hadSelection) {
          filterFindings();
          investigationRunners.forEach(run => run());
        }
      };
      const revealSelectedDestination = target => {
        const section = target.closest('[data-report-section]');
        for (let element = target; element && element !== section; element = element.parentElement) {
          if (element.hidden && !element.matches('[data-lineage-card]')) {
            selectedReveal.set(element, true);
            element.hidden = false;
          }
        }
        selectionNote.hidden = selectedReveal.size === 0;
        if (selectedReveal.size > 0) {
          if (target instanceof HTMLDetailsElement) target.querySelector('summary')?.after(selectionNote);
          else target.prepend(selectionNote);
        }
      };
      const focusReference = element => {
        if (!(element instanceof HTMLElement) || element === document.body) return null;
        if (element.id) return { id: element.id };
        const container = element.closest('[id]');
        if (!container || !mainContent.contains(container)) return null;
        const tag = element.tagName.toLowerCase();
        return { id: container.id, tag, index: [...container.querySelectorAll(tag)].indexOf(element) };
      };
      const resolveFocus = reference => {
        const container = reference && document.getElementById(reference.id);
        if (!container) return null;
        if (!reference.tag) return container;
        if (!['a', 'button', 'input', 'select', 'summary', 'h2', 'h3'].includes(reference.tag)) return null;
        return container.querySelectorAll(reference.tag)[reference.index] || null;
      };
      const snapshot = (focus = document.activeElement) => ({
        section: mainContent.dataset.activeSection,
        parent: sectionLinks.find(link => link.getAttribute('aria-current') === 'page')?.dataset.sectionTarget,
        context: mainContent.dataset.activeSection === 'lineage'
          ? lineageCards.find(card => !card.hidden)?.querySelector('h2')?.textContent : null,
        controls: Object.fromEntries(controls.map(control => [control.id, control.value])),
        open: [...disclosures].filter(([, detail]) => detail.open).map(([key]) => key),
        focus: focusReference(focus),
        initiatingId: focus?.closest?.('.semantic-object, .finding-card, [data-investigation-item], [data-lineage-card]')?.id || null,
        scroll: { x: window.scrollX, y: window.scrollY }
      });
      const saveEntry = (focus = document.activeElement) => {
        if (!entry || restoring) return;
        entry.view = snapshot(focus);
        history.replaceState({ ...history.state, reportInvestigation: entry }, '', location.href);
      };
      const originLabel = origin => {
        const parent = origin.view.parent || origin.view.section;
        const name = parent === 'semantic-usage' ? 'Model' : parent === 'reports' ? 'Reports'
          : sectionLinks.find(link => link.dataset.sectionTarget === parent)?.querySelector('span')?.textContent || 'previous context';
        const search = Object.entries(origin.view.controls).find(([id, value]) => id.endsWith('-search') && value &&
          document.getElementById(id)?.closest('[data-report-section]')?.id === origin.view.section)?.[1];
        return `Return to ${name}${search ? `: search “${search}”` : origin.view.context ? `: ${origin.view.context}` : ''}`;
      };
      const updateReturn = () => {
        const origin = entry?.origin;
        returnRow.hidden = !origin;
        if (origin) {
          returnLink.textContent = originLabel(origin);
          returnLink.setAttribute('href', origin.fragment ? `#${origin.fragment}` : '#summary');
        }
        else returnLink.removeAttribute('href');
      };
      const restoreEntry = saved => {
        restoring = true;
        routeRevision += 1;
        resetSelectedDestination();
        controls.forEach(control => {
          const value = saved.view.controls[control.id];
          if (typeof value === 'string') control.value = value;
        });
        filterFindings();
        investigationRunners.forEach(run => run());
        if (!saved.fragment || !revealFragmentTarget(saved.fragment, { restore: true })) activateSection('summary');
        disclosures.forEach((detail, key) => { detail.open = saved.view.open.includes(key); });
        entry = saved;
        updateReturn();
        const revision = routeRevision;
        requestAnimationFrame(() => {
          if (revision !== routeRevision) return;
          const exact = resolveFocus(saved.view.focus);
          const fallback = document.querySelector(`[data-report-section="${mainContent.dataset.activeSection}"] h2`);
          const focus = exact?.getClientRects().length ? exact : fallback;
          focus?.focus({ preventScroll: true });
          window.scrollTo({ left: saved.view.scroll.x, top: saved.view.scroll.y, behavior: 'instant' });
          restoring = false;
        });
      };
      const canRoute = fragment => !fragment || usageShortcuts.has(fragment) || Boolean(document.getElementById(fragment)?.closest('[data-report-section]'));
      const navigate = (fragment, link, { external = false } = {}) => {
        if (!canRoute(fragment)) return false;
        // Capture the initiating control before revealing anything in the destination.
        saveEntry(link || document.activeElement);
        const isCollection = !fragment || reportSections.some(section => section.id === fragment) || usageShortcuts.has(fragment);
        const contextFor = value => {
          let target = document.getElementById(value);
          if (target?.dataset.objectSummary) target = document.getElementById(target.dataset.objectSummary);
          return target?.closest('[data-lineage-card]:not([data-lineage-card="visual"])');
        };
        const localSwitch = contextFor(fragment) && contextFor(fragment) === contextFor(entry?.fragment);
        const origin = isCollection ? null : entry?.origin
          ? { ...entry.origin, distance: entry.origin.distance + 1 }
          : entry && !localSwitch ? { fragment: entry.fragment, view: entry.view, distance: 1 } : null;
        restoring = true;
        routeRevision += 1;
        resetSelectedDestination();
        if (!fragment) activateSection('summary', { focus: true });
        else if (reportSections.some(section => section.id === fragment)) activateSection(fragment, { focus: true });
        else revealFragmentTarget(fragment, { focus: true });
        entry = { session, fragment, origin, view: snapshot() };
        if (external) history.replaceState({ ...history.state, reportInvestigation: entry }, '', location.href);
        else history.pushState({ reportInvestigation: entry }, '', fragment ? `#${fragment}` : location.pathname + location.search);
        handledLocation = location.href;
        updateReturn();
        restoring = false;
        return true;
      };

      document.querySelectorAll('a[href^="#"]').forEach(link => {
        if (link.classList.contains('skip-link') || link === returnLink) return;
        link.addEventListener('click', event => {
          // Preserve modified/native link actions such as opening a fresh report tab.
          if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
          let fragment;
          try { fragment = decodeURIComponent(link.getAttribute('href').slice(1)); }
          catch { event.preventDefault(); return; }
          if (navigate(fragment, link)) event.preventDefault();
        });
      });
      returnLink.addEventListener('click', event => {
        if (!entry?.origin || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        saveEntry();
        history.go(-entry.origin.distance);
      });
      const initialFragment = fragmentFromLocation();
      if (!initialFragment || !revealFragmentTarget(initialFragment)) activateSection('summary');
      entry = { session, fragment: canRoute(initialFragment) ? initialFragment : '', origin: null, view: snapshot() };
      history.replaceState({ ...history.state, reportInvestigation: entry }, '', location.href);
      handledLocation = location.href;
      // Native fragment processing during load can overwrite an earlier focus move. Route first,
      // then focus after load; a user navigation in the meantime cancels this initial handoff.
      const initialRevision = routeRevision;
      const focusInitial = () => requestAnimationFrame(() => {
        if (initialFragment && canRoute(initialFragment) && routeRevision === initialRevision)
          revealFragmentTarget(initialFragment, { focus: true, restore: true });
      });
      if (document.readyState === 'complete') focusInitial();
      else window.addEventListener('load', focusInitial, { once: true });
      const onHistory = () => {
        const saved = history.state?.reportInvestigation;
        handledLocation = location.href;
        if (saved?.session === session) restoreEntry(saved);
        else {
          // Entries from a previous load have no origin in this document session. Reload/session
          // persistence is deliberately outside this slice; never invent a Return for those entries.
          if (saved?.session) entry = null;
          const fragment = fragmentFromLocation();
          if (!navigate(fragment, null, { external: true })) navigate('', null, { external: true });
        }
      };
      window.addEventListener('popstate', onHistory);
      window.addEventListener('hashchange', () => {
        if (handledLocation !== location.href) onHistory();
      });
      // Cosmetic UI changes update this entry only. They never create history steps.
      let checkpointPending = false;
      const checkpoint = () => {
        if (restoring || checkpointPending) return;
        checkpointPending = true;
        requestAnimationFrame(() => { checkpointPending = false; saveEntry(); });
      };
      mainContent.addEventListener('input', checkpoint);
      mainContent.addEventListener('change', checkpoint);
      mainContent.addEventListener('toggle', checkpoint, true);
      mainContent.addEventListener('focusin', checkpoint);
      window.addEventListener('scroll', checkpoint, { passive: true });
    """;
}
