// Starts GOV.UK Frontend components (accordion, tabs, header menu, etc.).
//
// GOV.UK Frontend expects to run once per full page load. Blazor can change the page without
// a full load, in two ways:
//  - enhanced navigation: following a link swaps the page content in place
//  - stream rendering: content (e.g. health check results) arrives after the first paint
// Either way, new [data-module] elements would never be started, and enhanced navigation also
// strips the classes GOV.UK Frontend needs from <body>. So we watch the DOM and
// start any components that appear. Components already started are skipped: GOV.UK Frontend
// raises an InitError for them, which we ignore.
import { initAll } from '../lib/govuk-frontend/govuk-frontend.min.js';

// The template's inline <script> adds these classes on a full page load, but Blazor's enhanced
// navigation resets <body>'s attributes from the server HTML, so they must be re-added each time.
function ensureBodyClasses() {
    document.body.classList.add('js-enabled');
    if ('noModule' in HTMLScriptElement.prototype) {
        document.body.classList.add('govuk-frontend-supported');
    }
}

function initialise() {
    ensureBodyClasses();
    initAll({
        scope: document.body,
        onError(error) {
            if (error?.name !== 'InitError') {
                console.error(error);
            }
        }
    });
}

let scheduled = false;

function scheduleInitialise() {
    if (scheduled) {
        return;
    }
    scheduled = true;
    requestAnimationFrame(() => {
        scheduled = false;
        initialise();
    });
}

initialise();

new MutationObserver(scheduleInitialise).observe(document.body, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['class']
});
