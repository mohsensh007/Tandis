/* Offline behavioral smoke tests. Node/Linux or Windows' built-in JScript; no npm packages. */
(function () {
    var windows = typeof WScript !== 'undefined';
    var root = windows ? WScript.Arguments(0) : process.argv[2];
    function read(path) {
        if (!windows) return require('fs').readFileSync(root + '/' + path, 'utf8');
        var stream = new ActiveXObject('ADODB.Stream');
        stream.Type = 2; stream.Charset = 'utf-8'; stream.Open();
        stream.LoadFromFile(root + '/' + path);
        var text = stream.ReadText(); stream.Close(); return text;
    }
    function log(text) { if (windows) WScript.Echo(text); else console.log(text); }
    function fail(text) {
        if (windows) { WScript.Echo(text); WScript.Quit(1); }
        throw new Error(text);
    }
    var count = 0;
    function check(ok, text) { if (!ok) fail('FAIL: ' + text); count++; log('PASS: ' + text); }
    function latinDigits(value) { return String(value).replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); }); }
    // The legacy runner only needs ES5 collection/JSON shims for synthetic local test data.
    if (!Array.prototype.forEach) Array.prototype.forEach = function (fn) { for (var i = 0; i < this.length; i++) fn(this[i], i); };
    if (!Array.prototype.map) Array.prototype.map = function (fn) { var a = []; this.forEach(function (x, i) { a.push(fn(x, i)); }); return a; };
    if (!Array.prototype.filter) Array.prototype.filter = function (fn) { var a = []; this.forEach(function (x, i) { if (fn(x, i)) a.push(x); }); return a; };
    if (!Array.prototype.reduce) Array.prototype.reduce = function (fn, init) { this.forEach(function (x, i) { init = fn(init, x, i); }); return init; };
    if (!Object.keys) Object.keys = function (o) { var a = []; for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) a.push(k); return a; };
    if (!String.prototype.trim) String.prototype.trim = function () { return this.replace(/^\s+|\s+$/g, ''); };
    var parse = typeof JSON !== 'undefined' ? JSON.parse : function (text) { return eval('(' + text + ')'); };
    var fixtures = parse(read('.test-output/body-fixtures.json'));
    var source = read('TandisWebApp/wwwroot/js/body-analysis.js');
    var view = read('TandisWebApp/Views/BodyAnalysis/Index.cshtml');
    var css = read('TandisWebApp/wwwroot/css/body-analysis.css');
    var icons = read('TandisWebApp/wwwroot/lib/bootstrap-icons/font/bootstrap-icons.min.css');
    var ids = {}, docEvents = {}, winEvents = {}, apiResult = fixtures.saved, lastApi = null;
    var document;
    function Element(id, classes) {
        this.id = id || ''; this.attrs = {}; this.children = []; this.parentNode = null;
        this.style = {}; this.hidden = false; this.disabled = false; this.inert = false;
        this.textContent = ''; this.innerHTML = ''; this.value = ''; this.clientWidth = 300;
        this.tagName = 'DIV'; this.events = {}; this.classes = classes || '';
        var self = this;
        this.classList = {
            add: function (name) { if (!self.hasClass(name)) self.classes += ' ' + name; },
            remove: function () { for (var i = 0; i < arguments.length; i++) self.classes = (' ' + self.classes + ' ').replace(' ' + arguments[i] + ' ', ' '); },
            contains: function (name) { return self.hasClass(name); }
        };
    }
    Element.prototype.hasClass = function (name) { return (' ' + this.classes + ' ').indexOf(' ' + name + ' ') >= 0; };
    Element.prototype.getAttribute = function (name) { return this.attrs[name]; };
    Element.prototype.setAttribute = function (name, value) { this.attrs[name] = String(value); };
    Element.prototype.addEventListener = function (type, fn) { this.events[type] = fn; };
    Element.prototype.appendChild = function (child) {
        if (child.parentNode) child.parentNode.children = child.parentNode.children.filter(function (x) { return x !== child; });
        child.parentNode = this; this.children.push(child); return child;
    };
    Element.prototype.contains = function (node) { while (node) { if (node === this) return true; node = node.parentNode; } return false; };
    Element.prototype.focus = function () { document.activeElement = this; };
    Element.prototype.matches = function (selector) {
        return selector.charAt(0) === '.' ? this.hasClass(selector.slice(1)) : selector.charAt(0) === '[' ? this.attrs[selector.slice(1, -1)] !== undefined : selector.toUpperCase() === this.tagName;
    };
    Element.prototype.closest = function (selector) { var node = this; while (node) { if (node.matches(selector)) return node; node = node.parentNode; } return null; };
    Element.prototype.querySelectorAll = function (selector) {
        var found = [];
        function visit(node) { node.children.forEach(function (child) { if (child.matches(selector) || (selector.indexOf('button:') === 0 && child.tagName === 'BUTTON')) found.push(child); visit(child); }); }
        visit(this); return found;
    };
    Element.prototype.querySelector = function (selector) {
        if (selector === 'svg') return this.svg || (this.svg = new Element('', 'ba-chart-svg'));
        return this.querySelectorAll(selector)[0] || null;
    };
    var body = new Element('body'); body.tagName = 'BODY';
    document = {
        body: body, activeElement: null,
        getElementById: function (id) { return ids[id] || null; },
        addEventListener: function (type, fn) { (docEvents[type] || (docEvents[type] = [])).push(fn); }
    };
    var window = { addEventListener: function (type, fn) { winEvents[type] = fn; } };
    function add(id, classes, parent) { var el = new Element(id, classes); if (id) ids[id] = el; (parent || body).appendChild(el); return el; }
    var names = ['ba-data', 'baAge', 'baGender', 'baLatestEmpty', 'baLatestGrid', 'baLatestDate', 'baLatestWeight', 'baLatestWaist', 'baLatestAbdomen', 'baLatestChest', 'baLatestArm', 'baLatestThigh', 'baLatestHip', 'baMetrics', 'baNoData', 'baAdviceNote', 'baAdviceText', 'baUpdatedHint', 'baChartBox', 'baLegend', 'baChartStat', 'baTableBody', 'baTableWrap', 'baTableEmpty', 'baRecCount', 'baHeight', 'baActivity', 'baHistory', 'baGoal', 'baSessions', 'baDate', 'baWeight', 'baWaist', 'baAbdomen', 'baChest', 'baArm', 'baThigh', 'baHip', 'btnSaveProfile', 'btnSaveRecord'];
    names.forEach(function (name) { add(name); });
    ids.baHeight.value = '180'; ids.baWeight.value = '85';
    ids.baActivity.value = '2'; ids.baGoal.value = '3'; ids.baSessions.value = '3';
    var modal = add('baMetricHelpModal'); modal.hidden = true;
    var close = add('', 'ba-help-close', modal); close.tagName = 'BUTTON'; close.attrs['data-ba-help-close'] = '';
    ['baHelpTitle', 'baHelpIntro', 'baHelpIcon', 'baHelpPoints', 'baHelpValue'].forEach(function (id) { add(id, '', modal); });
    var matches, metricPattern = /id="(m[A-Z][A-Za-z]+)" data-help-key="([A-Za-z]+)"/g, cards = [];
    while ((matches = metricPattern.exec(view))) {
        var card = add(matches[1], 'ba-metric', ids.baMetrics); card.attrs['data-help-key'] = matches[2];
        add('', 'ba-m-value', card); add('', 'ba-m-unit', card); add('', 'ba-m-badge', card);
        var trigger = add('', 'ba-help-btn', card); trigger.tagName = 'BUTTON'; cards.push(card);
    }
    var toolbar = add('baChartToolbar');
    var chip = add('', 'ba-chip', toolbar); chip.attrs['data-key'] = 'waistCm'; chip.attrs['data-label'] = 'Waist'; chip.attrs['data-unit'] = 'cm';
    var initialJSON = read('.test-output/body-fixtures.json');
    // A minimal JSON facade for the old Windows runner, injected into this script's global realm.
    if (windows) source = source.replace(/return '[^']*'\[d\];/g, 'return d;');
    var execute = new Function('document', 'window', 'apiCall', 'showToast', 'confirm', 'IntersectionObserver', 'setTimeout', 'clearTimeout', 'JSON', source);
    ids['ba-data'].textContent = initialJSON;
    var dataParser = { parse: function () { return fixtures.initial; } };
    function apiCall(url, method, payload, callback) { lastApi = { url: url, payload: payload }; callback({ success: true, data: apiResult }); }
    execute(document, window, apiCall, function () {}, function () { return true; }, undefined, function (fn) { fn(); }, function () {}, dataParser);
    function emit(type, target, key, shift) {
        var event = { target: target, key: key, shiftKey: shift, prevented: false, preventDefault: function () { this.prevented = true; }, stopPropagation: function () {} };
        (docEvents[type] || []).forEach(function (handler) { handler(event); }); return event;
    }
    check(ids.baMetrics.style.display !== 'none' && ids.mBmi.querySelector('.ba-m-value').textContent !== '\u2014', 'metrics visible immediately from persisted latest record');
    check(latinDigits(ids.baLatestWeight.textContent) === '90', 'last measurement visible on initial load');
    check(ids.baChartBox.innerHTML.indexOf('ba-chart-line') >= 0 && ids.baChartBox.innerHTML.indexOf('pathLength="1000"') >= 0, 'animated chart classes and normalized path length');
    check(ids.baChartBox.innerHTML.indexOf('NaN') < 0 && ids.baChartBox.innerHTML.indexOf('Sahel,Tahoma') >= 0, 'finite SVG with local theme font');
    check(cards.length === 12, 'twelve independently addressable help buttons');
    var seenTitles = {}, seenIntros = {};
    ids.baAge.inert = true;
    cards.forEach(function (card) {
        var trigger = card.querySelector('.ba-help-btn');
        emit('click', trigger);
        var title = ids.baHelpTitle.textContent, intro = ids.baHelpIntro.textContent;
        check(!modal.hidden && title && intro && !seenTitles[title] && !seenIntros[intro], 'specific explanation for ' + card.attrs['data-help-key']);
        seenTitles[title] = true; seenIntros[intro] = true;
        check(body.classList.contains('ba-help-open') && ids.baHeight.inert && document.activeElement === close, 'dialog focus and background isolation');
        var tab = emit('keydown', close, 'Tab');
        check(tab.prevented && document.activeElement === close, 'dialog traps Tab');
        emit('keydown', close, 'Escape');
        check(modal.hidden && !ids.baHeight.inert && ids.baAge.inert && document.activeElement === trigger, 'Escape restores focus and previous inert state');
    });
    ids.btnSaveRecord.events.click();
    check(lastApi.url === '/api/BodyAnalysis/SaveRecord' && lastApi.payload.weightKg === 85, 'save uses actual form payload');
    check(latinDigits(ids.baLatestWeight.textContent) === '85' && ids.baLatestWaist.textContent === '\u2014', 'save replaces latest summary without stale optional fields');
    check(ids.baTableBody.innerHTML.indexOf('data-id="4"') >= 0, 'post-save history preserves usable API recordId');
    var del = add('', 'ba-row-del'); del.attrs['data-id'] = '4';
    apiResult = fixtures.deleted; emit('click', del);
    check(lastApi.payload.recordId === 4 && latinDigits(ids.baLatestWeight.textContent) === '90', 'deleting latest renders next record and its metrics');
    apiResult = fixtures.incomplete; winEvents.pageshow({ persisted: true });
    check(ids.baMetrics.style.display !== 'none' && ids.mBmi.querySelector('.ba-m-value').textContent === '\u2014', 'partial profile keeps latest/trend visible and clears unavailable metrics');
    apiResult = fixtures.empty; winEvents.pageshow({ persisted: true });
    check(ids.baMetrics.style.display === 'none' && ids.baLatestGrid.style.display === 'none' && ids.baLatestEmpty.style.display !== 'none', 'no records clears summary and all old results');
    apiResult = fixtures.initial; winEvents.pageshow({ persisted: true });
    check(lastApi.url === '/api/BodyAnalysis/Data' && ids.baMetrics.style.display !== 'none', 'back-forward cache restore fetches current server results');
    var only = fixtures.initial.records[0];
    apiResult = { latest: only, records: [only], profile: fixtures.initial.profile, metrics: fixtures.initial.metrics };
    winEvents.pageshow({ persisted: true });
    check(ids.baChartBox.innerHTML.indexOf('NaN') < 0 && ids.baChartBox.innerHTML.indexOf('<circle') >= 0, 'single-point/flat chart has a finite padded range');
    apiResult = { latest: only, records: [{ recordDate: '<img onerror=x>', weightKg: 90 }, { recordDate: '<script>x</script>', weightKg: 90 }], profile: fixtures.initial.profile, metrics: fixtures.initial.metrics };
    winEvents.pageshow({ persisted: true });
    check(ids.baChartBox.innerHTML.indexOf('<img') < 0 && ids.baChartBox.innerHTML.indexOf('<script>') < 0, 'chart dates are escaped');
    ids.baChartBox.clientWidth = 280; winEvents.resize();
    check(ids.baChartBox.innerHTML.indexOf('viewBox="0 0 268 250"') >= 0, 'chart recalculates a readable narrow viewport');
    check(css.indexOf('prefers-reduced-motion: reduce') >= 0 && css.indexOf('stroke-dashoffset: 0 !important') >= 0, 'reduced-motion users see a complete static chart');
    check(css.indexOf('min-width: 320px') < 0, 'chart no longer forces page overflow');
    var iconPattern = /class="bi (bi-[a-z0-9-]+)(?:[\s"])/g, missing = [];
    while ((matches = iconPattern.exec(view))) if (icons.indexOf('.' + matches[1] + '::before') < 0) missing.push(matches[1]);
    check(missing.length === 0, 'all body-analysis icons exist in the bundled font: ' + missing.join(','));
    // Parse modified shared scripts too. Quoted catch is equivalent syntax for the ES3 Windows parser.
    ['payment.js', 'member-messages.js', 'home.js'].forEach(function (name) {
        var script = read('TandisWebApp/wwwroot/js/' + name);
        if (windows) script = script.replace(/\.catch\s*\(/g, '["catch"](');
        new Function(script);
    });
    var report = read('TandisWebApp/Views/Home/Reports.cshtml');
    check(report.indexOf('member-reports.css') >= 0 && read('TandisWebApp/wwwroot/css/member-reports.css').indexOf('tr.finance-record') >= 0, 'mobile financial rows share desktop data/pager hooks');
    var homeView = read('TandisWebApp/Views/Home/Index.cshtml');
    var homeJs = read('TandisWebApp/wwwroot/js/home.js');
    var activeSansesMarker = "apiCall('/api/Register/ActiveSanses'", activeSansesCalls = 0, activeSansesAt = 0;
    while ((activeSansesAt = homeJs.indexOf(activeSansesMarker, activeSansesAt)) >= 0) {
        activeSansesCalls++;
        activeSansesAt += activeSansesMarker.length;
    }
    check(homeView.indexOf('<script src="~/js/home.js" asp-append-version="true"></script>') >= 0 &&
          homeView.indexOf('var faDigits') < 0 && homeJs.indexOf('function loadDashboardData') >= 0,
          'home dashboard behavior is extracted to the versioned external script');
    check(homeView.indexOf(activeSansesMarker) < 0 && activeSansesCalls === 1,
          'home dashboard keeps one active-sanses request in home.js');
    var adminLayout = read('TandisWebApp/Views/Shared/_AdminLayout.cshtml');
    var adminResponsive = read('TandisWebApp/wwwroot/css/admin-responsive.css');
    var adminMotion = read('TandisWebApp/wwwroot/js/admin-motion.js');
    check(adminLayout.indexOf('admin-responsive.css') >= 0 && adminLayout.indexOf('data-admin-shell') >= 0 && adminLayout.indexOf('admin-motion.js') >= 0, 'admin layout loads the shared mobile and motion layer');
    check(adminResponsive.indexOf('table-responsive') >= 0 && adminResponsive.indexOf('rep-card') >= 0 && adminResponsive.indexOf('prefers-reduced-motion: reduce') >= 0, 'admin responsive layer covers reports, tables and reduced motion');
    check(adminMotion.indexOf('IntersectionObserver') >= 0 && adminMotion.indexOf('prefers-reduced-motion') >= 0 && adminMotion.indexOf('MutationObserver') >= 0, 'admin motion layer reveals static and dynamic cards accessibly');
    check(adminLayout.indexOf('notifBadge') < 0 && adminLayout.indexOf('loadNotifBell') < 0, 'admin notification bell is removed when members cannot message admins');
    check(adminLayout.indexOf('abn-switching') >= 0 && adminResponsive.indexOf('adminNavJelly') >= 0, 'admin bottom navigation has a jelly active-item transition');
    check(adminResponsive.indexOf('abn-active::after') < 0, 'admin bottom navigation keeps one active background layer');
    var reportsPage = read('TandisWebApp/Views/Admin/Reports.cshtml');
    check(reportsPage.indexOf('admin-reports-grid') >= 0 && reportsPage.indexOf('admin-report-link') >= 0 && reportsPage.indexOf('grid-template-columns: minmax(0, 1fr)') >= 0, 'admin report cards use a full-width mobile grid');
    var adminIndex = read('TandisWebApp/Views/Admin/Index.cshtml');
    var adminMessages = read('TandisWebApp/Views/Admin/Messages.cshtml');
    var adminMessagesCss = read('TandisWebApp/wwwroot/css/admin-messages.css');
    check(adminIndex.indexOf('inside-card-list') >= 0 && adminIndex.indexOf('inside-person-card') >= 0 && adminMessagesCss.indexOf('inside-person-card') >= 0, 'admin presence list has a dedicated mobile card layout');
    check(adminMessages.indexOf('recipient-button-copy') >= 0 && adminMessagesCss.indexOf('recSummary') >= 0 && adminMessagesCss.indexOf('sent-message-row') >= 0, 'admin messaging UI wraps recipient summaries and sent rows on mobile');
    var chartsPage = read('TandisWebApp/Views/Admin/Charts.cshtml');
    var chartsCss = read('TandisWebApp/wwwroot/css/admin-charts.css');
    check(chartsPage.indexOf('ch-chart-animate') >= 0 && chartsPage.indexOf('chAxisY') >= 0 && chartsCss.indexOf('prefers-reduced-motion: reduce') >= 0, 'admin charts constrain labels and support accessible animation');
    var athleteView = read('TandisWebApp/Views/Athlete/Index.cshtml');
    var athleteCss = read('TandisWebApp/wwwroot/css/athlete.css');
    var athleteJs = read('TandisWebApp/wwwroot/js/athlete.js');
    check(athleteView.indexOf('id="summaryModal"') >= 0 && athleteView.indexOf('sum-ring-fg') >= 0 && athleteView.indexOf('sumMoveCount') >= 0, 'athlete summary modal keeps its live ring and completed-move hooks');
    check(athleteCss.indexOf('#summaryModal .sum-ring-fg') >= 0 && athleteCss.indexOf('fill: none') >= 0 && athleteCss.indexOf('stroke-dashoffset') >= 0 && athleteCss.indexOf('@media (prefers-reduced-motion: reduce)') >= 0, 'athlete summary ring is visible, animated and reduced-motion safe');
    check(athleteJs.indexOf("fetch('/Athlete/Summary'") >= 0 && athleteJs.indexOf('sumCountUp') >= 0 && athleteJs.indexOf('summaryReducedMotion') >= 0, 'athlete summary still uses the live API and accessible rendering flow');
    check(athleteView.indexOf('weekdayFa = Model.WeekdayFa') >= 0 && athleteView.indexOf('JsonSerializerDefaults.Web') >= 0 && athleteJs.indexOf('p.prgID') >= 0 && athleteJs.indexOf('D.myLocker.boxNo') >= 0, 'athlete initial payload keeps program and locker fields in camelCase');
    var athleteController = read('TandisWebApp/Controllers/AthleteController.cs');
    check(athleteController.indexOf('[HttpGet("/Athlete/Day")]') >= 0 && athleteController.indexOf('JsonSerializerDefaults.Web') >= 0 && athleteJs.indexOf('dayItems = data.items') >= 0 && athleteJs.indexOf('states[l.itemID]') >= 0, 'athlete day endpoint matches the exercise renderer casing');
    var coachLayout = read('TandisWebApp/Views/Coach/_CoachLayout.cshtml');
    var coachCss = read('TandisWebApp/wwwroot/css/coach-panel.css');
    var coachJs = read('TandisWebApp/wwwroot/js/coach-panel.js');
    check(coachLayout.indexOf('coach-panel.css') >= 0 && coachLayout.indexOf('coach-panel.js') >= 0 && coachCss.indexOf('coach-glass-body') >= 0, 'coach panel loads the shared mobile-first theme layer');
    check(coachCss.indexOf('prefers-reduced-motion: reduce') >= 0 && coachJs.indexOf('coach-motion-item') >= 0, 'coach panel has reduced-motion-safe entry motion');
    var memberLayout = read('TandisWebApp/Views/Shared/_Layout.cshtml');
    var loginLayout = read('TandisWebApp/Views/Shared/_LoginLayout.cshtml');
    var mobilePerformanceCss = read('TandisWebApp/wwwroot/css/mobile-performance.css');
    check(memberLayout.indexOf('currentPath == "/home/reports"') >= 0 && memberLayout.indexOf('currentPath == "/register/renew"') >= 0 &&
          memberLayout.indexOf('if (loadReportPager)') >= 0 && memberLayout.indexOf('if (loadPayment)') >= 0 &&
          adminLayout.indexOf('currentPath == "/admin/messages"') >= 0 && adminLayout.indexOf('if (loadReportPager)') >= 0 &&
          coachLayout.indexOf('currentPath.StartsWith("/coach/programedit")') >= 0 && coachLayout.indexOf('if (loadJalaliCalendar)') >= 0,
          'member, coach and admin page-specific scripts are route-gated');
    check(memberLayout.indexOf('~/css/mobile-performance.css') >= 0 &&
          coachLayout.indexOf('~/css/mobile-performance.css') >= 0 &&
          adminLayout.indexOf('~/css/mobile-performance.css') >= 0 &&
          loginLayout.indexOf('~/css/mobile-performance.css') >= 0 &&
          mobilePerformanceCss.indexOf('@media (max-width: 767.98px)') >= 0 &&
          mobilePerformanceCss.indexOf('backdrop-filter: none') >= 0 &&
          mobilePerformanceCss.indexOf('animation-duration') >= 0,
          'shared mobile performance layer is loaded by all layouts and scopes expensive effects');
    check(memberLayout.indexOf('width="38" height="38" decoding="async"') >= 0 &&
          coachLayout.indexOf('width="34" height="34" decoding="async"') >= 0 &&
          adminLayout.indexOf('width="34" height="34" decoding="async"') >= 0 &&
          loginLayout.indexOf('width="120" height="120" decoding="async"') >= 0 &&
          loginLayout.indexOf('width="40" height="40" decoding="async"') >= 0,
          'shared square logo images declare intrinsic dimensions and async decoding');
    check(coachLayout.indexOf('js-logout') >= 0 && coachLayout.indexOf('id="coachLogoutModal"') >= 0 && coachLayout.indexOf('data-bs-dismiss="modal"') >= 0 && coachLayout.indexOf('btnConfirmCoachLogout') >= 0 && coachLayout.indexOf('getOrCreateInstance') >= 0, 'coach logout uses the same cancel/confirm modal flow as admin');
    check(memberLayout.indexOf('js-logout') >= 0 && memberLayout.indexOf('id="logoutConfirmModal"') >= 0 && memberLayout.indexOf('data-bs-dismiss="modal"') >= 0 && memberLayout.indexOf('btnConfirmMemberLogout') >= 0 && memberLayout.indexOf('getOrCreateInstance') >= 0, 'member logout uses the same cancel/confirm modal flow as admin');
    var login = read('TandisWebApp/Views/Account/Login.cshtml');
    check(login.indexOf('limitNationalCodeInput') >= 0 && login.indexOf('slice(0, 10)') >= 0 && login.indexOf('inputmode="numeric"') >= 0, 'login limits numeric national-code input to ten digits');
    log('Completed ' + count + ' JavaScript/UI checks (mock DOM, not visual or database integration).');
})();
